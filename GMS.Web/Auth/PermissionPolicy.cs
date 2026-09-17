using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace GMS.Web.Auth;

/// <summary>Requirement carrying the permission code a page/handler needs.</summary>
public sealed class PermissionRequirement(string code) : IAuthorizationRequirement
{
    public string Code { get; } = code;
}

/// <summary>Passes when the signed-in user has a matching <c>perm</c> claim.</summary>
public sealed class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim(Claims.Permission, requirement.Code))
            context.Succeed(requirement);
        return Task.CompletedTask;
    }
}

/// <summary>
/// Turns <c>[Authorize("perm:products.view")]</c> into a policy that requires an
/// authenticated user with that permission claim. Everything else falls back to
/// the default provider.
/// </summary>
public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private const string Prefix = "perm:";
    private readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() =>
        Task.FromResult(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(policyName[Prefix.Length..]))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}

/// <summary>
/// Redirects a signed-in user whose password must be changed to the change-password
/// page before they can use anything else.
/// </summary>
public sealed class MustChangePasswordMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        var user = context.User;
        var path = context.Request.Path;

        var mustChange = user.Identity?.IsAuthenticated == true
                         && user.HasClaim(Claims.MustChangePassword, "1");

        var allowed = path.StartsWithSegments("/Account")
                      || path.StartsWithSegments("/css")
                      || path.StartsWithSegments("/js")
                      || path.StartsWithSegments("/lib")
                      || path.StartsWithSegments("/_framework");

        if (mustChange && !allowed)
        {
            context.Response.Redirect("/Account/ChangePassword");
            return;
        }

        await next(context);
    }
}
