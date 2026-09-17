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

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock, notifications As NotificationService)
            MyBase.New(uow, currentUser, clock)
            _notifications = Guard.NotNull(notifications)
        End Sub

        Public Function GetSummary() As Result(Of DashboardSummary)
            If Denied(PermissionCodes.Reports.View) AndAlso Denied(PermissionCodes.Products.View) Then
                Return Forbidden(Of DashboardSummary)()
            End If

            Dim products = Uow.Repository(Of Product)().Query().ToList()
            Dim today = DateRange.Today(Clock.UtcNow)
            Dim mtd = DateRange.MonthToDate(Clock.UtcNow)

            Dim confirmedSales = Uow.Repository(Of Transaction)().Query().
                Where(Function(t) t.Type = TransactionType.Sale AndAlso t.Status = TransactionStatus.Confirmed).ToList()

            Dim summary As New DashboardSummary With {
                .ProductCount = products.Count,
                .CustomerCount = Uow.Repository(Of Customer)().Query().Count(),
                .LowStockCount = products.Where(Function(p) p.IsActive AndAlso p.QuantityOnHand <= p.ReorderLevel).Count(),
                .InventoryValueAtCost = Math.Round(products.Sum(Function(p) p.QuantityOnHand * p.CostPrice), 2),
                .SalesTodayTotal = confirmedSales.Where(Function(t) t.TransactionDate >= today.FromUtc AndAlso t.TransactionDate < today.ToUtc).
                                                  Sum(Function(t) t.Total),
                .SalesMonthToDateTotal = confirmedSales.Where(Function(t) t.TransactionDate >= mtd.FromUtc AndAlso t.TransactionDate <= mtd.ToUtc).
                                                        Sum(Function(t) t.Total),
                .OpenDraftCount = Uow.Repository(Of Transaction)().Query().Count(Function(t) t.Status = TransactionStatus.Draft),
                .UnreadNotificationCount = _notifications.UnreadCountForCurrentUser(),
                .RecentActivity = RecentActivity()
            }
            Return Result(Of DashboardSummary).Ok(summary)
        End Function

        Private Function RecentActivity() As IReadOnlyList(Of ActivityItem)
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
