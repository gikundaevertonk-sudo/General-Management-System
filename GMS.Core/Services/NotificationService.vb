Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class NotificationService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock)
            MyBase.New(uow, currentUser, clock)
        End Sub

        ''' <summary>Notifications for the current user: their own plus broadcasts.</summary>
        Public Function ListForCurrentUser(Optional unreadOnly As Boolean = False,
                                           Optional take As Integer = 50) As IReadOnlyList(Of Notification)
            Dim uid = CurrentUser.UserId
            Dim q = Uow.Repository(Of Notification)().Query().
                Where(Function(n) Not n.TargetUserId.HasValue OrElse (uid.HasValue AndAlso n.TargetUserId.Value = uid.Value))
            If unreadOnly Then q = q.Where(Function(n) Not n.IsRead)
            Return q.OrderByDescending(Function(n) n.CreatedAtUtc).Take(Math.Max(1, take)).ToList()
        End Function

        Public Function UnreadCountForCurrentUser() As Integer
            Return ListForCurrentUser(unreadOnly:=True, take:=Integer.MaxValue).Count
        End Function

        Public Function MarkRead(id As Integer) As Result
            Dim repo = Uow.Repository(Of Notification)()
            Dim n = repo.GetById(id)
            If n Is Nothing Then Return NotFound("Notification")
            If Not n.IsRead Then
                n.IsRead = True
                n.ReadAtUtc = Clock.UtcNow
                repo.Update(n)
                Uow.SaveChanges()
            End If
            Return Result.Ok()
        End Function

        Public Function MarkAllReadForCurrentUser() As Result
            Dim repo = Uow.Repository(Of Notification)()
            For Each n In ListForCurrentUser(unreadOnly:=True, take:=Integer.MaxValue)
                n.IsRead = True
                n.ReadAtUtc = Clock.UtcNow
                repo.Update(n)
            Next
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function Create(type As NotificationType, severity As NotificationSeverity,
                               title As String, message As String,
                               Optional targetUserId As Integer? = Nothing,
                               Optional relatedEntityName As String = "",
                               Optional relatedEntityId As String = "",
                               Optional dedupeKey As String = "") As Notification
            Dim n As New Notification With {
                .Type = type,
                .Severity = severity,
                .Title = title,
                .Message = message,
                .TargetUserId = targetUserId,
                .RelatedEntityName = If(relatedEntityName, String.Empty),
                .RelatedEntityId = If(relatedEntityId, String.Empty),
                .DedupeKey = If(dedupeKey, String.Empty),
                .CreatedAtUtc = Clock.UtcNow
            }
            Uow.Repository(Of Notification)().Add(n)
            Uow.SaveChanges()
            Return n
        End Function

        ''' <summary>Raise a low-stock alert for specific products if not already outstanding.</summary>
        Public Sub RaiseLowStockFor(productIds As IEnumerable(Of Integer))
            Dim ids = productIds.Distinct().ToList()
            If ids.Count = 0 Then Return

            Dim products = Uow.Repository(Of Product)().Query().
                Where(Function(p) ids.Contains(p.Id) AndAlso p.IsActive AndAlso p.QuantityOnHand <= p.ReorderLevel).ToList()

            For Each p In products
                RaiseLowStock(p)
            Next
            Uow.SaveChanges()
        End Sub

        ''' <summary>Scan the whole catalogue; used on a schedule or on demand.</summary>
        Public Function RunLowStockScan() As Result(Of Integer)
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of Integer)()

            Dim low = Uow.Repository(Of Product)().Query().
                Where(Function(p) p.IsActive AndAlso p.QuantityOnHand <= p.ReorderLevel).ToList()

            Dim raised = 0
            For Each p In low
                If RaiseLowStock(p) Then raised += 1
            Next

            ' Clear alerts for products that have recovered.
            Dim openLowStock = Uow.Repository(Of Notification)().Query().
                Where(Function(n) n.Type = NotificationType.LowStock AndAlso Not n.IsRead).ToList()
            For Each n In openLowStock
                Dim pid As Integer
                If Integer.TryParse(n.RelatedEntityId, pid) AndAlso Not low.Any(Function(p) p.Id = pid) Then
                    n.IsRead = True
                    n.ReadAtUtc = Clock.UtcNow
                    Uow.Repository(Of Notification)().Update(n)
                End If
            Next

            Uow.SaveChanges()
            Return Result(Of Integer).Ok(raised)
        End Function

        Private Function RaiseLowStock(p As Product) As Boolean
            Dim key = $"lowstock:{p.Id}"
            Dim exists = Uow.Repository(Of Notification)().Query().
                Any(Function(n) n.DedupeKey = key AndAlso Not n.IsRead)
            If exists Then Return False

            Uow.Repository(Of Notification)().Add(New Notification With {
                .Type = NotificationType.LowStock,
                .Severity = If(p.QuantityOnHand <= 0D, NotificationSeverity.Critical, NotificationSeverity.Warning),
                .Title = $"Low stock: {p.Name}",
                .Message = $"'{p.Name}' ({p.Sku}) is at {p.QuantityOnHand:0.###} {p.UnitOfMeasure}, reorder level {p.ReorderLevel:0.###}.",
                .RelatedEntityName = NameOf(Product),
                .RelatedEntityId = p.Id.ToString(),
                .DedupeKey = key,
                .CreatedAtUtc = Clock.UtcNow
            })
            Return True
        End Function
    End Class

End Namespace
