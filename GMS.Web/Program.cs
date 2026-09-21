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
});

builder.Services.AddHttpContextAccessor();

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
    });

builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
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
