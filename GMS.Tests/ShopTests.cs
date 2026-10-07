using GMS.Core.Common;
using GMS.Core.Enums;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// Shops: where an organisation's stock sits, and who may touch it there.
/// </summary>
/// <remarks>
/// The rule these all circle is that availability is decided at a location, never company-wide.
/// Get that wrong and a branch sells goods that are physically in another branch - the counter
/// takes the money, the shelf is empty, and every figure afterwards is a guess.
///
/// Shop confinement is tested by setting <see cref="FakeCurrentUser.ShopId"/> while leaving
/// <see cref="FakeCurrentUser.AllPermissions"/> on. That combination is the point: an attendant is
/// not someone with fewer permissions, it is someone whose permissions stop at their own shop, and
/// a test that dropped permissions too could not tell the two apart.
/// </remarks>
public class ShopTests
{
    private static int GivenAShop(TestHost host, string name, string code)
    {
        var result = host.Get<ShopService>().Create(new ShopInput { Name = name, Code = code, IsActive = true });
        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Value.Id;
    }

    private static int GivenAProduct(TestHost host, string sku = "WATER-500")
    {
        var result = host.Get<ProductService>().Create(new ProductInput
        {
            Sku = sku,
            Name = "Bottled Water 500ml",
            UnitOfMeasure = "ea",
            UnitPrice = 50m,
            CostPrice = 25m,
            ReorderLevel = 10,
            IsActive = true,
        });
        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Value.Id;
    }

    /// <summary>Brings stock into the central pool through a confirmed purchase.</summary>
    private static void GivenCentralStock(TestHost host, int productId, decimal quantity)
    {
        var supplier = host.Get<SupplierService>().Create(new SupplierInput { Name = "Coast Distributors", IsActive = true });
        Assert.True(supplier.Succeeded, supplier.ErrorMessage);

        var txns = host.Get<TransactionService>();
        var draft = txns.CreateDraft(TransactionType.Purchase, supplier.Value.Id, host.Clock.UtcNow, "stock in", null);
        Assert.True(draft.Succeeded, draft.ErrorMessage);
        Assert.True(txns.AddLine(draft.Value.Id, new TransactionLineInput
        {
            ProductId = productId, Quantity = quantity, UnitPrice = 10m,
        }).Succeeded);
        Assert.True(txns.Confirm(draft.Value.Id).Succeeded);
    }

    /// <summary>What one location holds of one product, read the way a screen would read it.</summary>
    private static decimal HeldAt(TestHost host, int? shopId, int productId)
    {
        var rows = host.Get<InventoryService>().GetStockAt(shopId, new QueryOptions { PageSize = 200 }, includeEmpty: true);
        Assert.True(rows.Succeeded, rows.ErrorMessage);
        return rows.Value.Items.Single(r => r.ProductId == productId).QuantityOnHand;
    }

    private static decimal OrganisationTotal(TestHost host, int productId) =>
        host.Get<ProductService>().GetById(productId).Value.QuantityOnHand;

    private static int GivenASaleOf(TestHost host, int productId, decimal quantity, int? shopId)
    {
        var txns = host.Get<TransactionService>();
        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in", shopId);
        Assert.True(draft.Succeeded, draft.ErrorMessage);
        Assert.True(txns.AddLine(draft.Value.Id, new TransactionLineInput
        {
            ProductId = productId, Quantity = quantity, UnitPrice = 50m,
        }).Succeeded);
        return draft.Value.Id;
    }

    [Fact]
    public void Two_shops_in_one_organisation_cannot_share_a_code()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        GivenAShop(host, "Westlands", "WL");

        var second = host.Get<ShopService>().Create(new ShopInput { Name = "Westlands Annex", Code = "wl" });

        Assert.True(second.Failed);
        Assert.Contains("code", second.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Stock_bought_without_a_shop_lands_in_the_central_pool()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var shop = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);

        GivenCentralStock(host, product, 100m);

        Assert.Equal(100m, HeldAt(host, null, product));
        Assert.Equal(0m, HeldAt(host, shop, product));
    }

    [Fact]
    public void Allocating_moves_stock_to_a_shop_without_changing_what_the_organisation_owns()
    {
        // The whole point of an allocation: nothing was bought, sold or lost, it only moved.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var shop = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 100m);

        var moved = host.Get<InventoryService>().Allocate(product, null, shop, 30m, "opening stock");

        Assert.True(moved.Succeeded, moved.ErrorMessage);
        Assert.Equal(30m, HeldAt(host, shop, product));
        Assert.Equal(70m, HeldAt(host, null, product));
        Assert.Equal(100m, OrganisationTotal(host, product));
    }

    [Fact]
    public void A_shop_cannot_be_sent_more_than_the_central_pool_holds()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var shop = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 10m);

        var moved = host.Get<InventoryService>().Allocate(product, null, shop, 11m, "too much");

        Assert.True(moved.Failed);
        Assert.Equal(0m, HeldAt(host, shop, product));
        Assert.Equal(10m, HeldAt(host, null, product));
    }

    [Fact]
    public void Stock_can_be_moved_straight_from_one_shop_to_another()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 100m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 40m, "").Succeeded);

        Assert.True(inventory.Allocate(product, west, east, 15m, "rebalance").Succeeded);

        Assert.Equal(25m, HeldAt(host, west, product));
        Assert.Equal(15m, HeldAt(host, east, product));
        Assert.Equal(60m, HeldAt(host, null, product));
        Assert.Equal(100m, OrganisationTotal(host, product));
    }

    [Fact]
    public void A_shop_cannot_sell_stock_that_is_sitting_in_another_shop()
    {
        // The company has 100. This shop has 5. A sale of 6 here must fail anyway.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 100m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 5m, "").Succeeded);
        Assert.True(inventory.Allocate(product, null, east, 80m, "").Succeeded);

        var sale = GivenASaleOf(host, product, 6m, west);
        var confirm = host.Get<TransactionService>().Confirm(sale);

        Assert.True(confirm.Failed);
        Assert.Contains("Westlands", confirm.ErrorMessage);
        Assert.Equal(5m, HeldAt(host, west, product));
        Assert.Equal(100m, OrganisationTotal(host, product));
    }

    [Fact]
    public void A_sale_at_a_shop_draws_down_that_shop_and_leaves_the_others_alone()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 100m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 30m, "").Succeeded);
        Assert.True(inventory.Allocate(product, null, east, 20m, "").Succeeded);

        var sale = GivenASaleOf(host, product, 12m, west);
        Assert.True(host.Get<TransactionService>().Confirm(sale).Succeeded);

        Assert.Equal(18m, HeldAt(host, west, product));
        Assert.Equal(20m, HeldAt(host, east, product));
        Assert.Equal(50m, HeldAt(host, null, product));
        Assert.Equal(88m, OrganisationTotal(host, product));
    }

    [Fact]
    public void Cancelling_a_shop_sale_puts_the_stock_back_where_it_came_from()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 40m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, west, 40m, "").Succeeded);
        var txns = host.Get<TransactionService>();
        var sale = GivenASaleOf(host, product, 9m, west);
        Assert.True(txns.Confirm(sale).Succeeded);

        Assert.True(txns.Cancel(sale, "customer changed their mind").Succeeded);

        Assert.Equal(40m, HeldAt(host, west, product));
        Assert.Equal(0m, HeldAt(host, null, product));
        Assert.Equal(40m, OrganisationTotal(host, product));
    }

    [Fact]
    public void One_sale_listing_the_same_product_twice_cannot_overdraw_a_shop()
    {
        // Two lines of 4 against a balance of 6. Checked per product, not per line, or each line
        // passes on its own and the shop goes negative.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 6m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, west, 6m, "").Succeeded);

        var txns = host.Get<TransactionService>();
        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in", west);
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 4m, UnitPrice = 50m });
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 4m, UnitPrice = 50m });

        Assert.True(txns.Confirm(draft.Value.Id).Failed);
        Assert.Equal(6m, HeldAt(host, west, product));
    }

    [Fact]
    public void An_attendant_sees_only_their_own_shop()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        GivenAShop(host, "Eastleigh", "EL");

        host.User.ShopId = west;
        var visible = host.Get<ShopService>().List(activeOnly: false);

        Assert.True(visible.Succeeded, visible.ErrorMessage);
        Assert.Equal("Westlands", Assert.Single(visible.Value).Name);
    }

    [Fact]
    public void An_attendants_sale_is_pinned_to_their_shop_whatever_they_ask_for()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 50m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 25m, "").Succeeded);
        Assert.True(inventory.Allocate(product, null, east, 25m, "").Succeeded);

        // Signed in at Westlands, asking for Eastleigh.
        host.User.ShopId = west;
        var sale = GivenASaleOf(host, product, 5m, east);
        Assert.True(host.Get<TransactionService>().Confirm(sale).Succeeded);

        host.User.ShopId = null;
        Assert.Equal(20m, HeldAt(host, west, product));
        Assert.Equal(25m, HeldAt(host, east, product));
    }

    [Fact]
    public void An_attendant_cannot_send_another_shops_stock_anywhere()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 50m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, east, 50m, "").Succeeded);

        host.User.ShopId = west;
        var stolen = host.Get<InventoryService>().Allocate(product, east, west, 50m, "helping myself");

        Assert.True(stolen.Failed);
        host.User.ShopId = null;
        Assert.Equal(50m, HeldAt(host, east, product));
        Assert.Equal(0m, HeldAt(host, west, product));
    }

    [Fact]
    public void An_attendant_cannot_open_another_shops_transaction()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        var elsewhere = GivenASaleOf(host, product, 1m, east);

        host.User.ShopId = west;
        var peek = host.Get<TransactionService>().GetById(elsewhere);

        Assert.True(peek.Failed);
    }

    [Fact]
    public void A_shop_still_holding_stock_cannot_be_closed()
    {
        // Closing it would strand the stock: nobody left who can sell it, and it still counts
        // towards the organisation's total.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 12m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, west, 12m, "").Succeeded);

        var closed = host.Get<ShopService>().SetActive(west, false);

        Assert.True(closed.Failed);
        Assert.Contains("stock", closed.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void A_shop_can_be_closed_once_its_stock_has_been_sent_back()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 12m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 12m, "").Succeeded);

        Assert.True(inventory.Allocate(product, west, null, 12m, "returning").Succeeded);
        var closed = host.Get<ShopService>().SetActive(west, false);

        Assert.True(closed.Succeeded, closed.ErrorMessage);
        Assert.Equal(12m, HeldAt(host, null, product));
    }

    [Fact]
    public void An_adjustment_at_a_shop_changes_that_shop_and_the_organisation_total()
    {
        // Unlike an allocation, breakage really does destroy stock, so both figures move.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 30m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 30m, "").Succeeded);

        var broken = inventory.Adjust(product, StockMovementDirection.Out, 4m, "breakage", west);

        Assert.True(broken.Succeeded, broken.ErrorMessage);
        Assert.Equal(26m, HeldAt(host, west, product));
        Assert.Equal(26m, OrganisationTotal(host, product));
    }

    [Fact]
    public void Reconcile_rebuilds_both_the_organisation_total_and_every_shop_balance()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 60m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 25m, "").Succeeded);
        var sale = GivenASaleOf(host, product, 5m, west);
        Assert.True(host.Get<TransactionService>().Confirm(sale).Succeeded);

        var rebuilt = inventory.Reconcile(product);

        Assert.True(rebuilt.Succeeded, rebuilt.ErrorMessage);
        Assert.Equal(55m, rebuilt.Value);
        Assert.Equal(20m, HeldAt(host, west, product));
        Assert.Equal(35m, HeldAt(host, null, product));
    }

    [Fact]
    public void The_ledger_records_which_location_each_movement_happened_at()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 20m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, west, 8m, "opening").Succeeded);

        var ledger = host.Get<InventoryService>().GetLedger(product, new QueryOptions { PageSize = 50 });

        Assert.True(ledger.Succeeded, ledger.ErrorMessage);
        var allocations = ledger.Value.Items.Where(r => r.Reason == StockMovementReason.Allocation).ToList();
        Assert.Equal(2, allocations.Count);
        Assert.Contains(allocations, r => r.Location == "Westlands" && r.Direction == StockMovementDirection.In && r.QuantityAfter == 8m);
        Assert.Contains(allocations, r => r.Location == InventoryService.CentralLocationName && r.Direction == StockMovementDirection.Out && r.QuantityAfter == 12m);
    }

    [Fact]
    public void A_user_cannot_be_pinned_to_a_shop_that_is_closed()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        Assert.True(host.Get<ShopService>().SetActive(west, false).Succeeded);

        var roles = host.Get<RoleService>().List();
        var attendantRole = roles.Value.Single(r => r.Role.Name == "Shop Attendant").Role.Id;
        var created = host.Get<UserService>().Create(new UserInput
        {
            UserName = "amina", Email = "amina@example.invalid", FullName = "Amina",
            RoleId = attendantRole, ShopId = west, IsActive = true,
        }, "Temp#12345");

        Assert.True(created.Failed);
        Assert.Contains("closed", created.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deleting_an_organisation_takes_its_shops_and_their_balances_with_it()
    {
        // The operator's delete removes children by hand rather than trusting ON DELETE CASCADE,
        // so every new table has to be added to it or a deleted tenant leaves rows behind.
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant("doomed", "Doomed Ltd");
        var shop = GivenAShop(host, "Westlands", "WL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 20m);
        Assert.True(host.Get<InventoryService>().Allocate(product, null, shop, 20m, "").Succeeded);

        host.User.IsPlatformOperator = true;
        var deleted = host.Get<OrganizationService>().DeleteOrganization(org.Id);

        Assert.True(deleted.Succeeded, deleted.ErrorMessage);
        host.User.IsPlatformOperator = false;
        Assert.Empty(host.Get<ShopService>().List(activeOnly: false).Value);
    }

    [Fact]
    public void An_attendants_dashboard_reports_their_shop_and_not_the_company()
    {
        // Otherwise the counter staff at one branch read back the whole company's takings and
        // stock value, which is the opposite of what pinning them to a shop is for.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var east = GivenAShop(host, "Eastleigh", "EL");
        var product = GivenAProduct(host);
        GivenCentralStock(host, product, 100m);
        var inventory = host.Get<InventoryService>();
        Assert.True(inventory.Allocate(product, null, west, 10m, "").Succeeded);
        Assert.True(inventory.Allocate(product, null, east, 40m, "").Succeeded);
        var txns = host.Get<TransactionService>();
        var elsewhere = GivenASaleOf(host, product, 20m, east);
        Assert.True(txns.Confirm(elsewhere).Succeeded);

        host.User.ShopId = west;
        var summary = host.Get<DashboardService>().GetSummary();

        Assert.True(summary.Succeeded, summary.ErrorMessage);
        // 10 units at cost 10 - the confirmed purchase above set the cost price to what was paid -
        // and none of Eastleigh's 40 units or its sale.
        Assert.Equal(100m, summary.Value.InventoryValueAtCost);
        Assert.Equal(0m, summary.Value.SalesMonthToDateTotal);
        Assert.Empty(summary.Value.RecentActivity);
    }

    [Fact]
    public void A_shop_attendant_signs_in_confined_to_their_shop()
    {
        // End to end: the shop on the account has to reach the principal the front ends build
        // their session from, or nothing above is ever enforced in the real application.
        using var host = new TestHost().WithBaseline();
        var org = host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var roles = host.Get<RoleService>().List();
        var attendantRole = roles.Value.Single(r => r.Role.Name == "Shop Attendant").Role.Id;
        Assert.True(host.Get<UserService>().Create(new UserInput
        {
            UserName = "amina", Email = "amina@example.invalid", FullName = "Amina",
            RoleId = attendantRole, ShopId = west, IsActive = true,
        }, "Temp#12345").Succeeded);

        var signedIn = host.Get<AuthService>().SignInWithTenant("amina", "Temp#12345", org.Code);

        Assert.True(signedIn.Succeeded, signedIn.ErrorMessage);
        Assert.Equal(west, signedIn.Value.ShopId);
        Assert.Equal("Westlands", signedIn.Value.ShopName);
        Assert.True(signedIn.Value.HasPermission("transactions.confirm"));
        Assert.False(signedIn.Value.HasPermission("shops.allocate"));
    }

    [Fact]
    public void A_branch_is_alerted_only_for_products_it_stocks()
    {
        // A branch that was sent a product and is now short must be alerted; a branch that was
        // never sent one does not carry it, and a Critical alert for it is noise nobody can act on.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var west = GivenAShop(host, "Westlands", "WL");
        var water = GivenAProduct(host, "WATER-500");
        var juice = GivenAProduct(host, "JUICE-1L");
        GivenCentralStock(host, water, 100m);
        GivenCentralStock(host, juice, 100m);
        Assert.True(host.Get<InventoryService>().Allocate(water, null, west, 4m, "").Succeeded);

        var scan = host.Get<NotificationService>().RunLowStockScan();

        Assert.True(scan.Succeeded, scan.ErrorMessage);
        var keys = host.Get<GMS.Core.Abstractions.IUnitOfWork>().Repository<GMS.Core.Models.Notification>()
            .Query().Select(n => n.DedupeKey).ToList();
        Assert.Contains(keys, k => k.StartsWith($"lowstock:{west}:{water}:"));
        Assert.DoesNotContain(keys, k => k.StartsWith($"lowstock:{west}:{juice}:"));
    }
}
