using GMS.Core.Data;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// Queries that only fail on PostgreSQL.
/// </summary>
/// <remarks>
/// Under EF Core 10 a VB lambda that reads <c>.Value</c> of a captured Integer? becomes a SQL
/// parameter named after the compiler's closure field, e.g. <c>Closure_2_$VB$Local_existingId_Value</c>,
/// and Npgsql rejects the <c>$</c> in it. The in-memory store and SQLite never notice. Each test
/// here is a screen that failed that way against the real database.
/// </remarks>
public class PostgresQueryTests
{
    private static (ScratchDatabase Db, TestHost Host) NewServer()
    {
        var db = new ScratchDatabase();
        var host = new TestHost(postgres: db.ConnectionString).WithBaseline();
        host.NewTenant();
        return (db, host);
    }

    [PostgresFact]
    public void A_product_can_be_edited()
    {
        var (db, host) = NewServer();
        using (db) using (host)
        {
            var products = host.Get<ProductService>();
            var id = products.Create(new ProductInput { Sku = "P-1", Name = "Before", UnitOfMeasure = "ea", IsActive = true }).Value.Id;

            var result = products.Update(id, new ProductInput { Sku = "P-1", Name = "After", UnitOfMeasure = "ea", IsActive = true });

            Assert.True(result.Succeeded, result.ErrorMessage);
        }
    }

    [PostgresFact]
    public void A_customer_can_be_edited()
    {
        var (db, host) = NewServer();
        using (db) using (host)
        {
            var customers = host.Get<CustomerService>();
            var id = customers.Create(new CustomerInput { Name = "Before", Code = "C-1", IsActive = true }).Value.Id;

            var result = customers.Update(id, new CustomerInput { Name = "After", Code = "C-1", IsActive = true });

            Assert.True(result.Succeeded, result.ErrorMessage);
        }
    }

    [PostgresFact]
    public void A_user_can_be_edited()
    {
        var (db, host) = NewServer();
        using (db) using (host)
        {
            var users = host.Get<UserService>();
            var role = host.Get<GmsDbContext>().Roles.First().Id;
            var id = users.Create(new UserInput { UserName = "jo", FullName = "Jo", Email = "jo@example.invalid", RoleId = role }, "Temp#Pass2026").Value.Id;

            var result = users.Update(id, new UserInput { UserName = "jo", FullName = "Jo Renamed", Email = "jo@example.invalid", RoleId = role });

            Assert.True(result.Succeeded, result.ErrorMessage);
        }
    }

    [PostgresFact]
    public void Notifications_can_be_listed()
    {
        var (db, host) = NewServer();
        using (db) using (host)
        {
            var list = host.Get<NotificationService>().ListForCurrentUser();

            Assert.NotNull(list);
        }
    }
}
