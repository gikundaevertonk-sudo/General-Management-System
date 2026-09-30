using GMS.Core.Contracts;
using GMS.Core.Data;
using GMS.Core.Enums;
using GMS.Core.Services;
using GMS.Core.Sync;
using Microsoft.EntityFrameworkCore;

namespace GMS.Tests;

/// <summary>
/// The desktop client's offline store: what it records for the sync to send later.
/// </summary>
/// <remarks>
/// Runs on SQLite, not the in-memory store, because the outbox is written by GmsDbContext and
/// only exists there.
/// </remarks>
public class LocalStoreTests
{
    private static List<OutboxEntry> Outbox(TestHost host) =>
        host.Get<GmsDbContext>().Set<OutboxEntry>().AsNoTracking().OrderBy(o => o.Seq).ToList();

    private static void ClearOutbox(TestHost host) =>
        host.Get<GmsDbContext>().Set<OutboxEntry>().ExecuteDelete();

    private static int NewProduct(TestHost host, string sku = "SKU-1") =>
        host.Get<ProductService>().Create(new ProductInput
        {
            Sku = sku, Name = "Bottled Water 500ml", UnitOfMeasure = "ea",
            UnitPrice = 50m, CostPrice = 25m, ReorderLevel = 10, IsActive = true,
        }).Value.Id;

    [Fact]
    public void A_row_created_here_gets_an_id_the_server_can_never_issue()
    {
        using var host = new TestHost(sqlite: true).WithBaseline();
        host.NewTenant();

        var first = NewProduct(host, "A");
        var second = NewProduct(host, "B");

        Assert.True(first < 0, $"expected a negative id, got {first}");
        Assert.Equal(first - 1, second);
    }

    [Fact]
    public void Creating_a_row_queues_it_to_be_sent()
    {
        using var host = new TestHost(sqlite: true).WithBaseline();
        host.NewTenant();
        ClearOutbox(host);

        var product = NewProduct(host);

        var queued = Outbox(host).Where(o => o.EntityName == "Product").ToList();
        var insert = Assert.Single(queued);
        Assert.Equal(OutboxOperation.Insert, insert.Operation);
        Assert.Equal(product, insert.LocalId);
    }

    [Fact]
    public void An_edit_queues_only_the_fields_that_actually_changed()
    {
        // Sending every field would overwrite changes someone else made on the server to the
        // fields this edit never touched.
        using var host = new TestHost(sqlite: true).WithBaseline();
        host.NewTenant();
        var products = host.Get<ProductService>();
        var id = NewProduct(host);
        ClearOutbox(host);

        var current = products.GetById(id).Value;
        Assert.True(products.Update(id, new ProductInput
        {
            Sku = current.Sku, Name = current.Name, UnitOfMeasure = current.UnitOfMeasure,
            UnitPrice = 65m, CostPrice = current.CostPrice, ReorderLevel = current.ReorderLevel,
            IsActive = current.IsActive,
        }).Succeeded);

        var update = Assert.Single(Outbox(host), o => o.EntityName == "Product");
        Assert.Equal(OutboxOperation.Update, update.Operation);
        var changed = update.ChangedProperties.Split(',');
        Assert.Contains("UnitPrice", changed);
        Assert.DoesNotContain("Name", changed);
        Assert.DoesNotContain("Sku", changed);
    }

    [Fact]
    public void Rows_copied_down_from_the_server_are_not_sent_back()
    {
        using var host = new TestHost(sqlite: true).WithBaseline();
        host.NewTenant();
        ClearOutbox(host);
        var context = host.Get<GmsDbContext>();

        context.SuppressOutbox = true;
        try { NewProduct(host); }
        finally { context.SuppressOutbox = false; }

        Assert.Empty(Outbox(host));
    }

    [Fact]
    public void A_sale_made_offline_queues_the_document_its_lines_and_its_stock_movements()
    {
        using var host = new TestHost(sqlite: true).WithBaseline();
        host.NewTenant();
        var product = NewProduct(host);
        var supplier = host.Get<SupplierService>().Create(new SupplierInput { Name = "Coast", IsActive = true }).Value.Id;
        var txns = host.Get<TransactionService>();
        var purchase = txns.CreateDraft(TransactionType.Purchase, supplier, host.Clock.UtcNow, "", null).Value.Id;
        txns.AddLine(purchase, new TransactionLineInput { ProductId = product, Quantity = 10m, UnitPrice = 20m });
        Assert.True(txns.Confirm(purchase).Succeeded);
        ClearOutbox(host);

        var sale = txns.CreateDraft(TransactionType.Sale, null, host.Clock.UtcNow, "", "Walk-in").Value.Id;
        txns.AddLine(sale, new TransactionLineInput { ProductId = product, Quantity = 3m, UnitPrice = 50m });
        var confirm = txns.Confirm(sale);

        Assert.True(confirm.Succeeded, confirm.ErrorMessage);
        Assert.Equal(7m, host.Get<ProductService>().GetById(product).Value.QuantityOnHand);
        var entities = Outbox(host).Select(o => o.EntityName).Distinct().ToList();
        Assert.Contains("Transaction", entities);
        Assert.Contains("TransactionLine", entities);
        Assert.Contains("StockMovement", entities);
        // Confirming re-attaches the document's lines, which EF tracks as a deleted-and-updated
        // pair of the same row. That is an update in the database, and must not queue a delete.
        Assert.DoesNotContain(Outbox(host), o => o.Operation == OutboxOperation.Delete);
    }
}
