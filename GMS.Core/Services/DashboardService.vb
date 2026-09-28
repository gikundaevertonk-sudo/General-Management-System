Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class DashboardService
        Inherits ServiceBase

        Private ReadOnly _notifications As NotificationService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, notifications As NotificationService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _notifications = Guard.NotNull(notifications)
        End Sub

        Public Function GetSummary() As Result(Of DashboardSummary)
            If Denied(PermissionCodes.Reports.View) AndAlso Denied(PermissionCodes.Products.View) Then
                Return Forbidden(Of DashboardSummary)()
            End If

            Dim products = Uow.Repository(Of Product)().Query().ToList()
            Dim today = DateRange.Today(Clock.UtcNow)
            Dim mtd = DateRange.MonthToDate(Clock.UtcNow)

            ' Everything below is narrowed to the caller's shop when they have one. Without this a
            ' shop attendant's dashboard would read the whole company back to them - its takings,
            ' its stock value and every other branch's activity - which is precisely what being
            ' pinned to a shop is supposed to prevent. The catalogue and customer list stay
            ' organisation-wide because they genuinely are shared.
            Dim pinned = PinnedShopId

            Dim drafts = Uow.Repository(Of Transaction)().Query().Where(Function(t) t.Status = TransactionStatus.Draft)
            Dim sales = Uow.Repository(Of Transaction)().Query().
                Where(Function(t) t.Type = TransactionType.Sale AndAlso t.Status = TransactionStatus.Confirmed)
            If pinned.HasValue Then
                Dim mine = pinned.Value
                drafts = drafts.Where(Function(t) t.ShopId.HasValue AndAlso t.ShopId.Value = mine)
                sales = sales.Where(Function(t) t.ShopId.HasValue AndAlso t.ShopId.Value = mine)
            End If
            Dim confirmedSales = sales.ToList()

            Dim heldHere = QuantitiesInScope(pinned)

            Dim summary As New DashboardSummary With {
                .ProductCount = products.Count,
                .CustomerCount = Uow.Repository(Of Customer)().Query().Count(),
                .LowStockCount = products.Where(Function(p) p.IsActive AndAlso
                                                    heldHere.GetValueOrDefault(p.Id, 0D) <= p.ReorderLevel).Count(),
                .InventoryValueAtCost = Math.Round(
                    products.Sum(Function(p) heldHere.GetValueOrDefault(p.Id, 0D) * p.CostPrice), 2),
                .SalesTodayTotal = confirmedSales.Where(Function(t) t.TransactionDate >= today.FromUtc AndAlso t.TransactionDate < today.ToUtc).
                                                  Sum(Function(t) t.Total),
                .SalesMonthToDateTotal = confirmedSales.Where(Function(t) t.TransactionDate >= mtd.FromUtc AndAlso t.TransactionDate <= mtd.ToUtc).
                                                        Sum(Function(t) t.Total),
                .OpenDraftCount = drafts.Count(),
                .UnreadNotificationCount = _notifications.UnreadCountForCurrentUser(),
                .RecentActivity = RecentActivity(pinned)
            }
            Return Result(Of DashboardSummary).Ok(summary)
        End Function

        ''' <summary>
        ''' Quantity per product for the figures above: the shop's own when the caller is pinned to
        ''' one, the organisation's cached total otherwise.
        ''' </summary>
        Private Function QuantitiesInScope(pinned As Integer?) As Dictionary(Of Integer, Decimal)
            If Not pinned.HasValue Then
                Return Uow.Repository(Of Product)().Query().
                    Select(Function(p) New With {p.Id, p.QuantityOnHand}).ToList().
                    ToDictionary(Function(p) p.Id, Function(p) p.QuantityOnHand)
            End If

            Dim mine = pinned.Value
            Return Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ShopId = mine).
                Select(Function(s) New With {s.ProductId, s.QuantityOnHand}).ToList().
                ToDictionary(Function(s) s.ProductId, Function(s) s.QuantityOnHand)
        End Function

        ''' <summary>
        ''' The organisation's recent changes, or nothing at all for a caller pinned to a shop.
        ''' </summary>
        ''' <remarks>
        ''' Audit rows record who changed what, not where, so there is no honest way to show a
        ''' shop attendant only their branch's. Showing them the whole organisation's would hand
        ''' them every other branch's activity, so this shows them none: an empty panel is a
        ''' smaller loss than a leak.
        ''' </remarks>
        Private Function RecentActivity(pinned As Integer?) As IReadOnlyList(Of ActivityItem)
            If pinned.HasValue Then Return New List(Of ActivityItem)()

            ' Materialise first: the summary string is built in memory, not in SQL.
            Return Uow.Repository(Of AuditEntry)().Query().
                OrderByDescending(Function(a) a.TimestampUtc).Take(10).ToList().
                Select(Function(a) New ActivityItem With {
                    .WhenUtc = a.TimestampUtc,
                    .Summary = $"{a.UserName} {a.Action.ToString().ToLower()}d {a.EntityName} #{a.EntityId}",
                    .EntityName = a.EntityName,
                    .EntityId = a.EntityId
                }).ToList()
        End Function
    End Class

End Namespace
