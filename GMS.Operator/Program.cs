using GMS.Core.DependencyInjection;
using GMS.Core.Abstractions;
using GMS.Operator.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Every page needs the operator signed in. Only the sign-in and sign-out pages opt out,
    // and Logout opts out of nothing else - see its page model for why it must not be
    // anonymous.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/Error");
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<PlatformOperatorOptions>(
    builder.Configuration.GetSection(PlatformOperatorOptions.Section));

// Same database as GMS.Web - the console exists to manage the tenants living in it. What is
// separated is the process, the host and the credential, not the data.
var connectionString = builder.Configuration.GetConnectionString("Gms");
if (!string.IsNullOrWhiteSpace(connectionString))
    builder.Services.AddGmsCorePostgres(connectionString);
else
    builder.Services.AddGmsCore();

builder.Services.AddScoped<ICurrentUser, OperatorCurrentUser>();
builder.Services.AddScoped<ITenantContext, NoTenantContext>();

// One scheme, and it is the default one. In GMS.Web the operator cookie had to be a second,
// non-default scheme so it could never be confused with a tenant session; here there are no
// tenant sessions to confuse it with, so the whole class of bug that caused goes away.
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        // Kept distinct from the tenant application's "gms.auth" so the two can never collide
        // if they are ever served from the same domain.
        options.Cookie.Name = PlatformAuth.Cookie;
        options.Cookie.SameSite = SameSiteMode.Strict;
        options.Cookie.HttpOnly = true;
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Deliberately none of GMS.Web's middleware. TenantContextMiddleware resolves an organization
// from the request, TrialExpiryMiddleware locks out lapsed tenants and MustChangePasswordMiddleware
// redirects tenant users - all three are about being a tenant, which the operator is not. The
// console also has no seeding step: GMS.Web owns the baseline, and the console creates
// organizations on demand through OrganizationService.
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

app.Run();
