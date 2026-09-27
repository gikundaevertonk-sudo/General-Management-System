using GMS.Core.Common;
using GMS.Core.Contracts;
using GMS.Core.Enums;
using GMS.Core.Services;

namespace GMS.Tests;

/// <summary>
/// Stock arithmetic and what a confirmed document is allowed to do to it.
/// </summary>
/// <remarks>
/// These are the rules that decide whether the figures a business runs on are right, so they are
/// worth pinning: a sale that is allowed to take stock it does not have leaves the count negative
/// and every later report wrong.
/// </remarks>
public class StockAndSalesTests
{
    private static int GivenAProduct(TestHost host, string sku = "SKU-1", decimal price = 50m)
    {
        var result = host.Get<ProductService>().Create(new ProductInput
        {
            Sku = sku,
            Name = "Bottled Water 500ml",
            UnitOfMeasure = "ea",
            UnitPrice = price,
            CostPrice = price / 2,
            ReorderLevel = 10,
            IsActive = true,
        });
        Assert.True(result.Succeeded, result.ErrorMessage);
        return result.Value.Id;
    }

    /// <summary>
    /// Brings stock in through a confirmed purchase - the only way to raise it, short of an
    /// inventory adjustment. A purchase needs a supplier: unlike a sale, there is no walk-in case,
    /// because goods always came from someone.
    /// </summary>
    private static int GivenStock(TestHost host, int productId, decimal quantity)
    {
        var supplier = host.Get<SupplierService>().Create(new SupplierInput
        {
            Name = "Coast Distributors",
            IsActive = true,
        });
        Assert.True(supplier.Succeeded, supplier.ErrorMessage);

        var txns = host.Get<TransactionService>();
        var draft = txns.CreateDraft(TransactionType.Purchase, supplier.Value.Id, host.Clock.UtcNow, "stock in", null);
        Assert.True(draft.Succeeded, draft.ErrorMessage);

        Assert.True(txns.AddLine(draft.Value.Id, new TransactionLineInput
        {
            ProductId = productId, Quantity = quantity, UnitPrice = 10m,
        }).Succeeded);

        Assert.True(txns.Confirm(draft.Value.Id).Succeeded);
        return draft.Value.Id;
    }

    private static decimal OnHand(TestHost host, int productId) =>
        host.Get<ProductService>().GetById(productId).Value.QuantityOnHand;

    [Fact]
    public void A_confirmed_purchase_increases_stock()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);

        GivenStock(host, product, 20m);

        Assert.Equal(20m, OnHand(host, product));
    }

    [Fact]
    public void A_sale_cannot_take_stock_that_is_not_there()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);
        var txns = host.Get<TransactionService>();

        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in");
        Assert.True(txns.AddLine(draft.Value.Id, new TransactionLineInput
        {
            ProductId = product, Quantity = 4m, UnitPrice = 50m,
        }).Succeeded);

        var confirm = txns.Confirm(draft.Value.Id);

        Assert.True(confirm.Failed);
        Assert.Contains("stock", confirm.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0m, OnHand(host, product));
    }

    [Fact]
    public void A_confirmed_sale_reduces_stock_by_exactly_what_was_sold()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);
        GivenStock(host, product, 20m);
        var txns = host.Get<TransactionService>();

        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in");
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 4m, UnitPrice = 50m });
        Assert.True(txns.Confirm(draft.Value.Id).Succeeded);

        Assert.Equal(16m, OnHand(host, product));
    }

    [Fact]
    public void A_draft_does_not_move_stock_until_it_is_confirmed()
    {
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);
        GivenStock(host, product, 20m);
        var txns = host.Get<TransactionService>();

        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in");
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 5m, UnitPrice = 50m });

        Assert.Equal(20m, OnHand(host, product));
    }

    [Fact]
    public void A_sale_can_be_recorded_for_someone_who_is_not_a_customer()
    {
        // The walk-in case: a name kept with the transaction, and no customer account created.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);
        GivenStock(host, product, 5m);
        var txns = host.Get<TransactionService>();

        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Jane Passing-Trade");
        Assert.True(draft.Succeeded, draft.ErrorMessage);
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 1m, UnitPrice = 50m });

        Assert.True(txns.Confirm(draft.Value.Id).Succeeded);
        var customers = host.Get<CustomerService>().Search(new QueryOptions());
        Assert.Empty(customers.Value.Items);
    }

    [Fact]
    public void A_confirmed_sale_cannot_be_confirmed_twice()
    {
        // Otherwise stock would be taken again for one sale.
        using var host = new TestHost().WithBaseline();
        host.NewTenant();
        var product = GivenAProduct(host);
        GivenStock(host, product, 20m);
        var txns = host.Get<TransactionService>();

        var draft = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in");
        txns.AddLine(draft.Value.Id, new TransactionLineInput { ProductId = product, Quantity = 2m, UnitPrice = 50m });
        Assert.True(txns.Confirm(draft.Value.Id).Succeeded);

        Assert.True(txns.Confirm(draft.Value.Id).Failed);
        Assert.Equal(18m, OnHand(host, product));
    }
}
