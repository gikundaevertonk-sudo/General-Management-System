Imports System.Linq
Imports System.Text
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>
    ''' Read-only aggregations for the Reports module. Returns plain data; each
    ''' front end renders it (and can pass rows to <see cref="ToCsv(Of T)"/> for a
    ''' simple export while PDF/Excel is handled UI-side).
    ''' </summary>
    Public NotInheritable Class ReportService
        Inherits ServiceBase

        Private ReadOnly _inventory As InventoryService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock,
                       inventory As InventoryService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _inventory = Guard.NotNull(inventory)
        End Sub

        ''' <param name="shopId">
        ''' Narrow the report to one shop. A caller pinned to a shop always gets their own, whatever
        ''' is asked for - which is what lets a shop attendant run their own branch's figures
        ''' without being able to see anyone else's.
        ''' </param>
        Public Function SalesSummary(range As DateRange, Optional shopId As Integer? = Nothing) As Result(Of SalesSummaryReport)
            If Denied(PermissionCodes.Reports.View) Then Return Forbidden(Of SalesSummaryReport)()

            Dim q = Uow.Repository(Of Transaction)().Query().
                Where(Function(t) t.Type = TransactionType.Sale _
                              AndAlso t.Status = TransactionStatus.Confirmed _
                              AndAlso t.TransactionDate >= range.FromUtc _
                              AndAlso t.TransactionDate < range.ToUtc)

            Dim scope = ResolveShopScope(shopId)
            If scope.HasValue Then
                Dim only = scope.Value
                q = q.Where(Function(t) t.ShopId.HasValue AndAlso t.ShopId.Value = only)
            End If

            Dim txns = q.ToList()
            Dim txnIds = txns.Select(Function(t) t.Id).ToHashSet()
            Dim lines = Uow.Repository(Of TransactionLine)().Query().
                Where(Function(l) txnIds.Contains(l.TransactionId)).ToList()
            Dim productsById = Uow.Repository(Of Product)().Query().ToDictionary(Function(p) p.Id, Function(p) p)

            Dim perProduct = lines.GroupBy(Function(l) l.ProductId).
                Select(Function(g)
                           Dim p = productsById.GetValueOrDefault(g.Key)
                           Return New SalesSummaryLine With {
                               .ProductId = g.Key,
                               .Sku = If(p?.Sku, "(deleted)"),
                               .ProductName = If(p?.Name, "(deleted product)"),
                               .QuantitySold = g.Sum(Function(l) l.Quantity),
                               .Revenue = g.Sum(Function(l) l.LineSubtotal)
                           }
                       End Function).
                OrderByDescending(Function(r) r.Revenue).ToList()

            Return Result(Of SalesSummaryReport).Ok(New SalesSummaryReport With {
                .Range = range,
                .TransactionCount = txns.Count,
                .Subtotal = txns.Sum(Function(t) t.Subtotal),
                .Tax = txns.Sum(Function(t) t.TaxTotal),
                .Total = txns.Sum(Function(t) t.Total),
                .Lines = perProduct
            })
        End Function

        ''' <summary>
        ''' What the stock is worth. With a shop, what that shop is holding; without one, the whole
        ''' organisation. A caller pinned to a shop always gets their own.
        ''' </summary>
        Public Function InventoryValuation(Optional shopId As Integer? = Nothing) As Result(Of IReadOnlyList(Of InventoryValuationRow))
            If Denied(PermissionCodes.Reports.View) Then Return Forbidden(Of IReadOnlyList(Of InventoryValuationRow))()

            Dim scope = ResolveShopScope(shopId)
            If scope.HasValue Then
                ' Delegated rather than re-derived: InventoryService owns every balance, and a
                ' second place computing "how much is at this shop" is a second place to get it wrong.
                Dim held = _inventory.GetStockAt(scope, New QueryOptions With {.PageSize = QueryOptions.MaxPageSize},
                                                 includeEmpty:=True)
                If held.Failed Then Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Fail(held.ErrorMessage)
                Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Ok(
                    held.Value.Items.Select(Function(r) New InventoryValuationRow With {
                        .ProductId = r.ProductId,
                        .Sku = r.Sku,
                        .ProductName = r.ProductName,
                        .QuantityOnHand = r.QuantityOnHand,
                        .UnitCost = r.UnitCost,
                        .ValueAtCost = r.ValueAtCost,
                        .BelowReorderLevel = r.BelowReorderLevel
                    }).ToList())
            End If

            Dim rows = Uow.Repository(Of Product)().Query().Where(Function(p) p.IsActive).
                OrderBy(Function(p) p.Name).
                Select(Function(p) New InventoryValuationRow With {
                    .ProductId = p.Id,
                    .Sku = p.Sku,
                    .ProductName = p.Name,
                    .QuantityOnHand = p.QuantityOnHand,
                    .UnitCost = p.CostPrice,
                    .ValueAtCost = Math.Round(p.QuantityOnHand * p.CostPrice, 2),
                    .BelowReorderLevel = p.QuantityOnHand <= p.ReorderLevel
                }).ToList()
            Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Ok(rows)
        End Function

        ''' <summary>What is at or below its reorder level, at one location or across the organisation.</summary>
        Public Function LowStock(Optional shopId As Integer? = Nothing) As Result(Of IReadOnlyList(Of InventoryValuationRow))
            Dim valued = InventoryValuation(shopId)
            If valued.Failed Then Return valued
            Return Result(Of IReadOnlyList(Of InventoryValuationRow)).Ok(
                valued.Value.Where(Function(r) r.BelowReorderLevel).
                             OrderBy(Function(r) r.QuantityOnHand).ToList())
        End Function

        ''' <summary>Minimal RFC 4180 CSV writer for report rows (no external dependency).</summary>
        Public Shared Function ToCsv(Of T)(rows As IEnumerable(Of T)) As String
            Return ToCsv(CType(rows, System.Collections.IEnumerable))
        End Function

        ''' <summary>
        ''' CSV for a sequence whose element type is not known at compile time.
        ''' Columns come from the runtime type of the first element.
        ''' </summary>
        Public Shared Function ToCsv(rows As System.Collections.IEnumerable) As String
            Dim materialised As New List(Of Object)()
            For Each r In rows
                materialised.Add(r)
            Next

            Dim sb As New StringBuilder()
            If materialised.Count = 0 Then Return sb.ToString()

            Dim props = materialised(0).GetType().GetProperties().Where(Function(p) p.CanRead AndAlso p.GetIndexParameters().Length = 0).ToArray()
            sb.AppendLine(String.Join(",", props.Select(Function(p) Escape(p.Name))))
            For Each row In materialised
                sb.AppendLine(String.Join(",", props.Select(Function(p) Escape(Convert.ToString(p.GetValue(row))))))
            Next
            Return sb.ToString()
        End Function

        Private Shared Function Escape(value As String) As String
            Dim v = If(value, String.Empty)
            If v.Contains(","c) OrElse v.Contains(""""c) OrElse v.Contains(vbLf) OrElse v.Contains(vbCr) Then
                Return """" & v.Replace("""", """""") & """"
            End If
            Return v
        End Function
    End Class

End Namespace
