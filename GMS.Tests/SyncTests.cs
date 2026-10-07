using GMS.Core.Contracts;
using GMS.Core.Data;
using GMS.Core.Enums;
using GMS.Core.Models;
using GMS.Core.Services;
using GMS.Core.Sync;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GMS.Tests;

/// <summary>
/// Runs only when GMS_TEST_POSTGRES names a PostgreSQL server the tests may create and drop
/// databases on, e.g. <c>Host=localhost;Port=55432;Username=postgres;Database=postgres</c>.
/// Never point it at Supabase.
/// </summary>
public sealed class PostgresFactAttribute : FactAttribute
{
    public static string? Server => Environment.GetEnvironmentVariable("GMS_TEST_POSTGRES");

    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Server))
            Skip = "Set GMS_TEST_POSTGRES to a disposable PostgreSQL server to run the sync tests.";
    }
}

/// <summary>A throwaway database built from db/supabase/schema.sql, dropped afterwards.</summary>
public sealed class ScratchDatabase : IDisposable
{
    private readonly string _name = $"gms_sync_{Guid.NewGuid():N}";
    public string ConnectionString { get; }

    public ScratchDatabase()
    {
        using (var admin = new NpgsqlConnection(PostgresFactAttribute.Server))
        {
            admin.Open();
            using var create = new NpgsqlCommand($"create database {_name}", admin);
            create.ExecuteNonQuery();
        }

        ConnectionString = new NpgsqlConnectionStringBuilder(PostgresFactAttribute.Server) { Database = _name }.ToString();
        using var connection = new NpgsqlConnection(ConnectionString);
        connection.Open();
        using var schema = new NpgsqlCommand(File.ReadAllText(SchemaPath()), connection);
        schema.ExecuteNonQuery();
    }

    /// <summary>Found from this source file rather than the output folder, which may be anywhere.</summary>
    private static string SchemaPath([System.Runtime.CompilerServices.CallerFilePath] string thisFile = "")
    {
        var path = Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "db", "supabase", "schema.sql");
        if (File.Exists(path)) return path;
        throw new FileNotFoundException("db/supabase/schema.sql not found beside the test project.", path);
    }

    public void Dispose()
    {
        NpgsqlConnection.ClearAllPools();
        using var admin = new NpgsqlConnection(PostgresFactAttribute.Server);
        admin.Open();
        using var drop = new NpgsqlCommand($"drop database if exists {_name} with (force)", admin);
        drop.ExecuteNonQuery();
    }
}

/// <summary>
/// The desktop client working offline and catching up: its changes reach PostgreSQL, the
/// server's reach it, and two computers that sold the same stock while offline both count.
/// </summary>
/// <remarks>
/// "Server" is a GMS on PostgreSQL, standing in for GMS.Web or a desktop that stayed online.
/// Each "till" is a GMS on its own SQLite file, standing in for one desktop install.
/// </remarks>
public class SyncTests
{
    private sealed class World : IDisposable
    {
        public ScratchDatabase Database { get; } = new();
        public TestHost Server { get; }
        public Organization Org { get; }

        public World()
        {
            Server = new TestHost(postgres: Database.ConnectionString).WithBaseline();
            Org = Server.NewTenant();
        }

        public (TestHost Host, SyncEngine Engine) NewTill(string code)
        {
            var till = new TestHost(sqlite: true, deviceCode: code);
            till.Tenant.OrganizationId = Org.Id;
            till.User.AllPermissions = true;
            return (till, new SyncEngine(till.SqlitePath, Database.ConnectionString));
        }

        public SyncReport Sync((TestHost Host, SyncEngine Engine) till)
        {
            var report = till.Engine.Run(Org.Id);
            Assert.True(report.Reachable, string.Join("; ", report.Errors));
            Assert.True(report.Failed == 0, string.Join("; ", report.Errors));
            // What a screen does after a sync: forget rows it read before the sync changed them.
            till.Host.Get<GmsDbContext>().ChangeTracker.Clear();
            Server.Get<GmsDbContext>().ChangeTracker.Clear();
            return report;
        }

        public void Dispose()
        {
            Server.Dispose();
            Database.Dispose();
        }
    }

    private static int NewProduct(TestHost host, string sku, decimal price = 50m) =>
        Ok(host.Get<ProductService>().Create(new ProductInput
        {
            Sku = sku, Name = $"Product {sku}", UnitOfMeasure = "ea",
            UnitPrice = price, CostPrice = price / 2, ReorderLevel = 1, IsActive = true,
        })).Id;

    private static void StockIn(TestHost host, int productId, decimal quantity)
    {
        var supplier = Ok(host.Get<SupplierService>().Create(new SupplierInput { Name = $"Supplier {Guid.NewGuid():N}", IsActive = true })).Id;
        var txns = host.Get<TransactionService>();
        var draft = Ok(txns.CreateDraft(TransactionType.Purchase, supplier, host.Clock.UtcNow, "", null)).Id;
        Ok(txns.AddLine(draft, new TransactionLineInput { ProductId = productId, Quantity = quantity, UnitPrice = 10m }));
        Assert.True(txns.Confirm(draft).Succeeded);
    }

    private static Transaction Sell(TestHost host, int productId, decimal quantity, int? customerId = null)
    {
        var txns = host.Get<TransactionService>();
        var draft = Ok(txns.CreateDraft(TransactionType.Sale, customerId, host.Clock.UtcNow, "", customerId is null ? "Walk-in" : null)).Id;
        Ok(txns.AddLine(draft, new TransactionLineInput { ProductId = productId, Quantity = quantity, UnitPrice = 50m }));
        var confirmed = txns.Confirm(draft);
        Assert.True(confirmed.Succeeded, confirmed.ErrorMessage);
        return Ok(txns.GetById(draft));
    }

    private static decimal OnHand(TestHost host, int productId) =>
        Ok(host.Get<ProductService>().GetById(productId)).QuantityOnHand;

    private static T Ok<T>(GMS.Core.Common.Result<T> result)
    {
        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Value;
    }

    private static int ServerIdOf<T>(World world, Guid syncId) where T : EntityBase =>
        world.Server.Get<GmsDbContext>().Set<T>().IgnoreQueryFilters().Single(e => e.SyncId == syncId).Id;

    [PostgresFact]
    public void The_first_sync_copies_the_organization_down_and_sign_in_then_works_offline()
    {
        using var world = new World();
        var product = NewProduct(world.Server, "SKU-1");
        StockIn(world.Server, product, 20m);
        var till = world.NewTill("T1");

        var report = world.Sync(till);

        Assert.True(report.Received > 0);
        Assert.Equal(20m, OnHand(till.Host, product));
        var signIn = till.Host.Get<AuthService>().SignInWithTenant(
            DataSeeder.DefaultAdminUserName, DataSeeder.DefaultAdminPassword, world.Org.Code);
        Assert.True(signIn.Succeeded, signIn.ErrorMessage);
        Assert.Equal(world.Org.Id, signIn.Value.OrganizationId);
    }

    [PostgresFact]
    public void Everything_made_offline_reaches_the_server_with_its_references_intact()
    {
        using var world = new World();
        var till = world.NewTill("T1");
        world.Sync(till);

        // All offline: a new product, stock bought in for it, and a sale to a new customer.
        var product = NewProduct(till.Host, "OFF-1");
        StockIn(till.Host, product, 10m);
        var customer = Ok(till.Host.Get<CustomerService>().Create(new CustomerInput { Name = "Amina", IsActive = true })).Id;
        var sale = Sell(till.Host, product, 3m, customer);
        Assert.True(product < 0 && customer < 0 && sale.Id < 0, "rows made offline take local ids");
        Assert.StartsWith($"SAL-{sale.TransactionDate.Year}-T1-", sale.TransactionNumber);

        world.Sync(till);

        var server = world.Server.Get<GmsDbContext>();
        var serverProduct = ServerIdOf<Product>(world, till.Host.Get<GmsDbContext>().Products.IgnoreQueryFilters().Single(p => p.Id == product).SyncId);
        var serverSale = server.Transactions.IgnoreQueryFilters().Include(t => t.Lines).Single(t => t.TransactionNumber == sale.TransactionNumber);
        var serverCustomer = server.Customers.IgnoreQueryFilters().Single(c => c.Name == "Amina");
        Assert.Equal(serverCustomer.Id, serverSale.CustomerId);
        Assert.Equal(serverProduct, Assert.Single(serverSale.Lines).ProductId);
        Assert.Equal(TransactionStatus.Confirmed, serverSale.Status);
        Assert.Equal(7m, OnHand(world.Server, serverProduct));
        Assert.Equal(2, server.StockMovements.IgnoreQueryFilters().Count(m => m.ProductId == serverProduct));

        // And the till keeps its own ids, now matched to the server's.
        Assert.Equal(0, till.Engine.PendingCount(world.Org.Id));
        Assert.Equal(7m, OnHand(till.Host, product));
        Assert.Equal(sale.TransactionNumber, Ok(till.Host.Get<TransactionService>().GetById(sale.Id)).TransactionNumber);
    }

    [PostgresFact]
    public void Two_tills_selling_the_same_last_units_offline_are_both_kept_and_flagged()
    {
        using var world = new World();
        var product = NewProduct(world.Server, "LAST-5");
        StockIn(world.Server, product, 5m);
        var a = world.NewTill("TA");
        var b = world.NewTill("TB");
        world.Sync(a);
        world.Sync(b);

        Sell(a.Host, product, 4m);
        Sell(b.Host, product, 4m);
        world.Sync(a);
        var second = world.Sync(b);

        Assert.Equal(-3m, OnHand(world.Server, product));
        Assert.Equal(2, world.Server.Get<GmsDbContext>().Transactions.Count(t => t.Type == TransactionType.Sale));
        Assert.NotEmpty(second.NegativeStock);
        var flag = Assert.Single(world.Server.Get<GmsDbContext>().Notifications, n => n.DedupeKey.StartsWith("negative-stock:"));
        Assert.Equal(NotificationSeverity.Critical, flag.Severity);

        world.Sync(a);
        Assert.Equal(-3m, OnHand(a.Host, product));
    }

    [PostgresFact]
    public void Offline_and_online_edits_to_different_fields_of_one_record_are_both_kept()
    {
        using var world = new World();
        var product = NewProduct(world.Server, "EDIT-1", price: 50m);
        var till = world.NewTill("T1");
        world.Sync(till);

        var offline = Ok(till.Host.Get<ProductService>().GetById(product));
        Assert.True(till.Host.Get<ProductService>().Update(product, InputFrom(offline, price: 65m)).Succeeded);
        var online = Ok(world.Server.Get<ProductService>().GetById(product));
        Assert.True(world.Server.Get<ProductService>().Update(product, InputFrom(online, name: "Renamed online")).Succeeded);

        world.Sync(till);

        var onServer = Ok(world.Server.Get<ProductService>().GetById(product));
        Assert.Equal(65m, onServer.UnitPrice);
        Assert.Equal("Renamed online", onServer.Name);
        var onTill = Ok(till.Host.Get<ProductService>().GetById(product));
        Assert.Equal(65m, onTill.UnitPrice);
        Assert.Equal("Renamed online", onTill.Name);
    }

    private static ProductInput InputFrom(Product p, decimal? price = null, string? name = null) => new()
    {
        Sku = p.Sku, Name = name ?? p.Name, UnitOfMeasure = p.UnitOfMeasure, UnitPrice = price ?? p.UnitPrice,
        CostPrice = p.CostPrice, ReorderLevel = p.ReorderLevel, IsActive = p.IsActive, CategoryId = p.CategoryId,
    };

    [PostgresFact]
    public void A_row_deleted_on_the_server_disappears_from_the_till()
    {
        using var world = new World();
        var customer = Ok(world.Server.Get<CustomerService>().Create(new CustomerInput { Name = "Leaving", IsActive = true })).Id;
        var till = world.NewTill("T1");
        world.Sync(till);
        Assert.True(till.Host.Get<CustomerService>().GetById(customer).Succeeded);

        world.Server.Get<GmsDbContext>().Customers.Where(c => c.Id == customer).ExecuteDelete();
        world.Sync(till);

        Assert.False(till.Host.Get<CustomerService>().GetById(customer).Succeeded);
    }

    [PostgresFact]
    public void A_change_sent_again_after_a_lost_connection_is_not_inserted_twice()
    {
        using var world = new World();
        var till = world.NewTill("T1");
        world.Sync(till);
        var customer = Ok(till.Host.Get<CustomerService>().Create(new CustomerInput { Name = "Once", IsActive = true }));
        world.Sync(till);

        // As if the connection dropped after the server saved the row but before the till
        // recorded that it had: the change is still queued and the till has no server id for it.
        var local = till.Host.Get<GmsDbContext>();
        local.Set<IdMapEntry>().Where(m => m.EntityName == "Customer").ExecuteDelete();
        local.SuppressOutbox = true;
        local.Set<OutboxEntry>().Add(new OutboxEntry
        {
            OrganizationId = world.Org.Id, EntityName = "Customer", LocalId = customer.Id,
            RowSyncId = customer.SyncId, Operation = OutboxOperation.Insert, CreatedAtUtc = DateTime.UtcNow,
        });
        local.SaveChanges();
        local.SuppressOutbox = false;
        local.ChangeTracker.Clear();

        world.Sync(till);

        Assert.Equal(1, world.Server.Get<GmsDbContext>().Customers.Count(c => c.Name == "Once"));
        Assert.Equal(0, till.Engine.PendingCount(world.Org.Id));
    }

    [PostgresFact]
    public void A_row_whose_transaction_commits_late_still_reaches_the_till()
    {
        using var world = new World();
        var till = world.NewTill("T1");
        world.Sync(till);

        // A slow writer takes its sync_version now but commits later.
        using var slow = new NpgsqlConnection(world.Database.ConnectionString);
        slow.Open();
        using var tx = slow.BeginTransaction();
        using (var insert = new NpgsqlCommand(
            "insert into customers (organization_id, name, is_active, created_at_utc) values (@org, 'Slow', true, now())", slow, tx))
        {
            insert.Parameters.AddWithValue("org", world.Org.Id);
            insert.ExecuteNonQuery();
        }

        // Meanwhile the shared sequence moves on - other organizations' traffic - and this
        // organization commits a quicker row, then the till syncs while the slow one is open.
        using (var others = new NpgsqlConnection(world.Database.ConnectionString))
        {
            others.Open();
            using var burn = new NpgsqlCommand(
                "select nextval('gms_sync_version_seq') from generate_series(1, 1000)", others);
            burn.ExecuteNonQuery();
        }
        Ok(world.Server.Get<CustomerService>().Create(new CustomerInput { Name = "Quick", IsActive = true }));
        world.Sync(till);
        // The open transaction does not hold back what has already committed.
        Assert.Contains("Quick", till.Host.Get<GmsDbContext>().Customers.Select(c => c.Name).ToList());

        tx.Commit();
        world.Sync(till);

        var names = till.Host.Get<GmsDbContext>().Customers.Select(c => c.Name).ToList();
        Assert.Contains("Quick", names);
        Assert.Contains("Slow", names);
    }

    [PostgresFact]
    public void Nothing_is_lost_while_the_server_cannot_be_reached()
    {
        using var world = new World();
        var till = world.NewTill("T1");
        world.Sync(till);
        Ok(till.Host.Get<CustomerService>().Create(new CustomerInput { Name = "Queued", IsActive = true }));

        var unreachable = new SyncEngine(till.Host.SqlitePath,
            "Host=127.0.0.1;Port=1;Username=postgres;Database=postgres;Timeout=2");
        var report = unreachable.Run(world.Org.Id);

        Assert.False(report.Reachable);
        Assert.Equal(1, till.Engine.PendingCount(world.Org.Id));
        world.Sync(till);
        Assert.Equal(1, world.Server.Get<GmsDbContext>().Customers.Count(c => c.Name == "Queued"));
    }
}
