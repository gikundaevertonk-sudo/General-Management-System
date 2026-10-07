Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class NotificationService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
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

        ''' <summary>
        ''' Raise a low-stock alert for specific products at one location, if not already outstanding.
        ''' </summary>
        ''' <param name="shopId">Where the stock ran low; Nothing is the central pool.</param>
        Public Sub RaiseLowStockFor(productIds As IEnumerable(Of Integer), Optional shopId As Integer? = Nothing)
            Dim ids = productIds.Distinct().ToList()
            If ids.Count = 0 Then Return

            Dim held = QuantitiesAt(shopId)
            Dim products = Uow.Repository(Of Product)().Query().
                Where(Function(p) ids.Contains(p.Id) AndAlso p.IsActive).ToList().
                Where(Function(p) CarriesAt(held, p.Id, shopId) AndAlso
                                  held.GetValueOrDefault(p.Id, 0D) <= p.ReorderLevel).ToList()
            If products.Count = 0 Then Return

            Dim watchers = StockWatchers()
            For Each p In products
                RaiseLowStock(p, shopId, held.GetValueOrDefault(p.Id, 0D), watchers)
            Next
            Uow.SaveChanges()
        End Sub

        ''' <summary>
        ''' Scan every location; used on a schedule or on demand. Each shop is checked against its
        ''' own shelves, so a branch that is empty raises an alert even while the company is
        ''' holding plenty somewhere else.
        ''' </summary>
        Public Function RunLowStockScan() As Result(Of Integer)
            If Denied(PermissionCodes.Inventory.View) Then Return Forbidden(Of Integer)()

            Dim products = Uow.Repository(Of Product)().Query().Where(Function(p) p.IsActive).ToList()
            Dim watchers = StockWatchers()

            ' Central first, then every open shop. A closed shop is not restocked, so alerting on it
            ' would only produce noise nobody can act on.
            Dim locations As New List(Of Integer?) From {Nothing}
            locations.AddRange(Uow.Repository(Of Shop)().Query().Where(Function(s) s.IsActive).
                               Select(Function(s) s.Id).ToList().Select(Function(id) CType(id, Integer?)))

            Dim raised = 0
            Dim stillLow As New HashSet(Of String)()
            For Each location In locations
                Dim held = QuantitiesAt(location)
                For Each p In products
                    If Not CarriesAt(held, p.Id, location) Then Continue For
                    Dim quantity = held.GetValueOrDefault(p.Id, 0D)
                    If quantity > p.ReorderLevel Then Continue For
                    stillLow.Add(DedupeKeyFor(p.Id, location))
                    If RaiseLowStock(p, location, quantity, watchers) Then raised += 1
                Next
            Next

            ' Clear alerts for locations that have recovered. Matched on the key's location part so
            ' restocking one branch does not silence another branch's alert for the same product.
            Dim openLowStock = Uow.Repository(Of Notification)().Query().
                Where(Function(n) n.Type = NotificationType.LowStock AndAlso Not n.IsRead).ToList()
            For Each n In openLowStock
                If stillLow.Contains(LocationKeyOf(n.DedupeKey)) Then Continue For
                n.IsRead = True
                n.ReadAtUtc = Clock.UtcNow
                Uow.Repository(Of Notification)().Update(n)
            Next

            Uow.SaveChanges()
            Return Result(Of Integer).Ok(raised)
        End Function

        ''' <summary>
        ''' Whether a location stocks the product at all. Central carries everything; a shop
        ''' carries what has ever been sent to it (a ShopStock row, which stays at zero after it
        ''' sells out). Without this, every branch raised a Critical alert for each product it
        ''' was never meant to sell.
        ''' </summary>
        Private Shared Function CarriesAt(held As Dictionary(Of Integer, Decimal), productId As Integer,
                                          shopId As Integer?) As Boolean
            Return Not shopId.HasValue OrElse held.ContainsKey(productId)
        End Function

        ''' <summary>How much of each product one location is holding.</summary>
        Private Function QuantitiesAt(shopId As Integer?) As Dictionary(Of Integer, Decimal)
            Dim allocated = Uow.Repository(Of ShopStock)().Query().
                Select(Function(s) New With {s.ShopId, s.ProductId, s.QuantityOnHand}).ToList()

            If shopId.HasValue Then
                Return allocated.Where(Function(s) s.ShopId = shopId.Value).
                    ToDictionary(Function(s) s.ProductId, Function(s) s.QuantityOnHand)
            End If

            Dim held = allocated.GroupBy(Function(s) s.ProductId).
                ToDictionary(Function(g) g.Key, Function(g) g.Sum(Function(s) s.QuantityOnHand))
            Return Uow.Repository(Of Product)().Query().
                Select(Function(p) New With {p.Id, p.QuantityOnHand}).ToList().
                ToDictionary(Function(p) p.Id, Function(p) p.QuantityOnHand - held.GetValueOrDefault(p.Id, 0D))
        End Function

        ''' <summary>
        ''' Who a low-stock alert should reach: the people who can actually do something about it.
        ''' </summary>
        ''' <remarks>
        ''' Defined as everyone holding <c>shops.allocate</c> - managers and administrators - rather
        ''' than by role name, so a custom role that can move stock is included automatically.
        ''' Counter staff are deliberately left out: they cannot restock their own shop, so the
        ''' alert would be noise they are powerless to clear.
        '''
        ''' An empty list means nobody holds that permission, and the caller broadcasts instead of
        ''' dropping the alert on the floor.
        ''' </remarks>
        Private Function StockWatchers() As List(Of Integer)
            Dim roleIds = Uow.Repository(Of RolePermission)().Query().
                Join(Uow.Repository(Of Permission)().Query(),
                     Function(rp) rp.PermissionId, Function(p) p.Id, Function(rp, p) New With {rp.RoleId, p.Code}).
                Where(Function(x) x.Code = PermissionCodes.Shops.Allocate).
                Select(Function(x) x.RoleId).ToList()
            If roleIds.Count = 0 Then Return New List(Of Integer)()

            Return Uow.Repository(Of User)().Query().
                Where(Function(u) u.IsActive AndAlso roleIds.Contains(u.RoleId)).
                Select(Function(u) u.Id).ToList()
        End Function

        Private Shared Function DedupeKeyFor(productId As Integer, shopId As Integer?) As String
            Return $"lowstock:{If(shopId.HasValue, shopId.Value.ToString(), "central")}:{productId}"
        End Function

        ''' <summary>The location-and-product part of a dedupe key, dropping any per-recipient suffix.</summary>
        Private Shared Function LocationKeyOf(dedupeKey As String) As String
            Dim parts = If(dedupeKey, String.Empty).Split(":"c)
            If parts.Length < 3 Then Return If(dedupeKey, String.Empty)
            Return String.Join(":", parts(0), parts(1), parts(2))
        End Function

        ''' <summary>
        ''' Raises one alert per recipient, so each manager can clear their own copy without
        ''' hiding it from the others.
        ''' </summary>
        Private Function RaiseLowStock(p As Product, shopId As Integer?, quantity As Decimal,
                                       watchers As List(Of Integer)) As Boolean
            Dim locationName = If(shopId.HasValue,
                                  If(Uow.Repository(Of Shop)().GetById(shopId.Value)?.Name, $"Shop #{shopId.Value}"),
                                  "Central")
            Dim baseKey = DedupeKeyFor(p.Id, shopId)
            Dim severity = If(quantity <= 0D, NotificationSeverity.Critical, NotificationSeverity.Warning)
            Dim title = $"Low stock at {locationName}: {p.Name}"
            Dim message = $"'{p.Name}' ({p.Sku}) is at {quantity:0.###} {p.UnitOfMeasure} at {locationName}, " &
                          $"reorder level {p.ReorderLevel:0.###}."

            ' No recipient holds the permission, so there is nobody to address it to. Broadcast
            ' rather than lose it entirely.
            Dim targets As New List(Of Integer?)()
            If watchers.Count = 0 Then
                targets.Add(Nothing)
            Else
                targets.AddRange(watchers.Select(Function(id) CType(id, Integer?)))
            End If

            Dim any = False
            Dim repo = Uow.Repository(Of Notification)()
            Dim outstanding = repo.Query().
                Where(Function(n) Not n.IsRead AndAlso n.DedupeKey.StartsWith(baseKey)).
                Select(Function(n) n.DedupeKey).ToList().ToHashSet(StringComparer.Ordinal)

            For Each target In targets
                Dim key = $"{baseKey}:{If(target.HasValue, target.Value.ToString(), "all")}"
                If outstanding.Contains(key) Then Continue For

                repo.Add(New Notification With {
                    .Type = NotificationType.LowStock,
                    .Severity = severity,
                    .Title = title,
                    .Message = message,
                    .TargetUserId = target,
                    .RelatedEntityName = NameOf(Product),
                    .RelatedEntityId = p.Id.ToString(),
                    .DedupeKey = key,
                    .CreatedAtUtc = Clock.UtcNow
                })
                any = True
            Next
            Return any
        End Function
    End Class

End Namespace
