using GMS.Core.DependencyInjection;
using GMS.Core.Abstractions;
using GMS.Core.Services;
using GMS.Web.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages(options =>
{
    // Everything requires a signed-in user unless a page opts out with [AllowAnonymous].
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToFolder("/Account");
    options.Conventions.AllowAnonymousToPage("/Error");
    // No AllowAnonymousToFolder("/Platform") here, however tempting. That adds
    // IAllowAnonymous to the endpoint metadata, which the authorization middleware honours
    // ahead of any [Authorize] on the page - it would switch the console's own protection
    // off. The operator pages carry [Authorize] naming the Platform scheme, and its policy
    // is combined with the folder policy above rather than replacing it.
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<PlatformOperatorOptions>(
    builder.Configuration.GetSection(PlatformOperatorOptions.Section));

// GMS.Core service layer. Persistence is PostgreSQL (Supabase) when a connection string is
// configured (ConnectionStrings:Gms via user-secrets/env), otherwise falls back to the
// in-memory store for a database-free local run.
var connectionString = builder.Configuration.GetConnectionString("Gms");
var usingDatabase = !string.IsNullOrWhiteSpace(connectionString);
if (usingDatabase)
    builder.Services.AddGmsCorePostgres(connectionString!);
else
    builder.Services.AddGmsCore();

builder.Services.AddScoped<ICurrentUser, WebCurrentUser>();
builder.Services.AddScoped<ITenantContext, WebTenantContext>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/Denied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "gms.auth";
    })
    // A second, independent cookie for the system owner. Separate scheme and separate cookie
    // name: a tenant session can never be mistaken for an operator session, and signing in
    // here does not sign you into any organization.
    .AddCookie(PlatformAuth.Scheme, options =>
    {
        options.LoginPath = "/Platform/Login";
        options.LogoutPath = "/Platform/Logout";
        options.AccessDeniedPath = "/Platform/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(4);
        options.SlidingExpiration = true;
        options.Cookie.Name = PlatformAuth.Cookie;
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(PlatformAuth.Policy, policy =>
    {
        policy.AddAuthenticationSchemes(PlatformAuth.Scheme);
        // Not RequireAuthenticatedUser(). This policy is combined with the site-wide one,
        // and both schemes get authenticated into a single principal - so "is anyone signed
        // in" would be satisfied by an ordinary tenant cookie. The identity must have come
        // from the operator scheme specifically.
        policy.RequireAssertion(context =>
            context.User.Identities.Any(i => i.IsAuthenticated && i.AuthenticationType == PlatformAuth.Scheme));
    });
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<TrialExpiryMiddleware>();
app.UseMiddleware<MustChangePasswordMiddleware>();
app.UseAuthorization();

app.MapRazorPages();

// Demo data is only useful without a real database; never inject it into Supabase unless
// explicitly asked for (Seed:Demo=true in config).
var seedDemo = !usingDatabase || builder.Configuration.GetValue<bool>("Seed:Demo");
SeedData(app, seedDemo);

app.Run();

static void SeedData(WebApplication app, bool seedDemo)
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;

    // No HttpContext here, so WebCurrentUser acts as a system principal for seeding.
    var seeder = sp.GetRequiredService<DataSeeder>();
    seeder.SeedBaseline();
    if (seedDemo)
    {
        seeder.SeedDemo(
            sp.GetRequiredService<CategoryService>(),
            sp.GetRequiredService<ProductService>(),
            sp.GetRequiredService<TransactionService>(),
            sp.GetRequiredService<CustomerService>(),
            sp.GetRequiredService<SupplierService>());
    }
}
