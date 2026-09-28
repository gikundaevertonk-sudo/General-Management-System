Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class ShopInput
        Public Property Name As String = String.Empty
        Public Property Code As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property IsActive As Boolean = True
    End Class

    ''' <summary>
    ''' The organization's shops: the central point a client manages every branch from.
    ''' </summary>
    ''' <remarks>
    ''' Stock is not this service's business - <see cref="InventoryService"/> owns every balance
    ''' and every movement, here as everywhere else. This one owns the shops themselves and the
    ''' figures that summarise them.
    ''' </remarks>
    Public NotInheritable Class ShopService
        Inherits ServiceBase

        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock,
                       audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _audit = Guard.NotNull(audit)
        End Sub

        Public Function Search(options As QueryOptions, Optional activeOnly As Boolean = False) As Result(Of PagedResult(Of Shop))
            If Denied(PermissionCodes.Shops.View) Then Return Forbidden(Of PagedResult(Of Shop))()

            Dim q = Visible()
            If activeOnly Then q = q.Where(Function(s) s.IsActive)
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(s) s.Name.ToLower().Contains(term) OrElse s.Code.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim items = q.OrderBy(Function(s) s.Name).Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of Shop)).Ok(
                New PagedResult(Of Shop)(items, total, options.Page, options.PageSize))
        End Function

        ''' <summary>Every shop the caller may see, for pickers. Small table, so unpaged.</summary>
        Public Function List(Optional activeOnly As Boolean = True) As Result(Of IReadOnlyList(Of Shop))
            If Denied(PermissionCodes.Shops.View) Then Return Forbidden(Of IReadOnlyList(Of Shop))()
            Dim q = Visible()
            If activeOnly Then q = q.Where(Function(s) s.IsActive)
            Return Result(Of IReadOnlyList(Of Shop)).Ok(q.OrderBy(Function(s) s.Name).ToList())
        End Function

        Public Function GetById(id As Integer) As Result(Of Shop)
            If Denied(PermissionCodes.Shops.View) Then Return Forbidden(Of Shop)()
            If OutsideShopScope(id) Then Return ForbiddenShop(Of Shop)()
            Dim entity = Uow.Repository(Of Shop)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of Shop)("Shop"), Result(Of Shop).Ok(entity))
        End Function

        ''' <summary>
        ''' The organization's shops with their headline figures, for the central overview.
        ''' </summary>
        ''' <remarks>
        ''' Every figure is read in one pass over stock, staff and the catalogue rather than per
        ''' shop. A query per shop per column is what made the operator's organization list slow
        ''' enough to need fixing; there is no reason to write that shape again here.
        ''' </remarks>
        Public Function GetOverview() As Result(Of IReadOnlyList(Of ShopSummaryRow))
            If Denied(PermissionCodes.Shops.View) Then Return Forbidden(Of IReadOnlyList(Of ShopSummaryRow))()

            Dim shops = Visible().OrderBy(Function(s) s.Name).ToList()
            If Not shops.Any() Then Return Result(Of IReadOnlyList(Of ShopSummaryRow)).Ok(New List(Of ShopSummaryRow)())

            Dim products = Uow.Repository(Of Product)().Query().
                Select(Function(p) New With {p.Id, p.CostPrice, p.ReorderLevel}).
                ToList().
                ToDictionary(Function(p) p.Id)

            ' Projected to plain integers before grouping. Grouping in the query would have EF
            ' hand back IGrouping instances it cannot translate; the shop count is tiny, so the
            ' grouping is better done here than argued with.
            Dim staffByShop = Uow.Repository(Of User)().Query().
                Where(Function(u) u.IsActive AndAlso u.ShopId.HasValue).
                Select(Function(u) u.ShopId.Value).
                ToList().
                GroupBy(Function(shopId) shopId).
                ToDictionary(Function(g) g.Key, Function(g) g.Count())

            Dim stockByShop = Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.QuantityOnHand <> 0D).
                ToList().
                GroupBy(Function(s) s.ShopId).
                ToDictionary(Function(g) g.Key, Function(g) g.ToList())

            Dim rows = shops.Select(
                Function(shop)
                    Dim held = stockByShop.GetValueOrDefault(shop.Id, New List(Of ShopStock)())
                    Dim value = 0D
                    Dim low = 0
                    For Each item In held
                        Dim product = products.GetValueOrDefault(item.ProductId)
                        If product Is Nothing Then Continue For
                        value += item.QuantityOnHand * product.CostPrice
                        If item.QuantityOnHand <= product.ReorderLevel Then low += 1
                    Next
                    Return New ShopSummaryRow With {
                        .ShopId = shop.Id,
                        .Name = shop.Name,
                        .Code = shop.Code,
                        .IsActive = shop.IsActive,
                        .StaffCount = staffByShop.GetValueOrDefault(shop.Id, 0),
                        .SkuCount = held.Count,
                        .QuantityOnHand = held.Sum(Function(s) s.QuantityOnHand),
                        .StockValueAtCost = Math.Round(value, 2),
                        .LowStockCount = low}
                End Function).ToList()

            Return Result(Of IReadOnlyList(Of ShopSummaryRow)).Ok(rows)
        End Function

        Public Function Create(input As ShopInput) As Result(Of Shop)
            If Denied(PermissionCodes.Shops.Manage) Then Return Forbidden(Of Shop)()

            Dim errors = Validate(input, Nothing)
            If errors.Any() Then Return Result(Of Shop).Fail(errors)

            ' OrganizationId is set here rather than left to the store to stamp, for the same
            ' reason UserService.Create sets it: the stamp exists only on the EF path, and the
            ' code-uniqueness check below matches on OrganizationId.
            Dim entity As New Shop With {.OrganizationId = TenantContext.OrganizationId}
            Apply(entity, input)
            entity.CreatedAtUtc = Clock.UtcNow
            entity.CreatedByUserId = CurrentUser.UserId
            Uow.Repository(Of Shop)().Add(entity)
            _audit.Record(NameOf(Shop), entity.Id.ToString(), AuditAction.Create)
            Uow.SaveChanges()
            Return Result(Of Shop).Ok(entity)
        End Function

        Public Function Update(id As Integer, input As ShopInput) As Result
            If Denied(PermissionCodes.Shops.Manage) Then Return Forbidden()
            If OutsideShopScope(id) Then Return ForbiddenShop()
            Dim repo = Uow.Repository(Of Shop)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Shop")

            Dim errors = Validate(input, id)
            If errors.Any() Then Return Result.Fail(errors)

            Apply(entity, input)
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            _audit.Record(NameOf(Shop), entity.Id.ToString(), AuditAction.Update)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Closes or reopens a shop.
        ''' </summary>
        ''' <remarks>
        ''' Closing is refused while the shop still holds stock or still has staff pinned to it.
        ''' Neither would be lost - the balances and the accounts stay exactly where they are -
        ''' but both would become unreachable: the stock has no owner who can sell it and the
        ''' staff have nowhere to sign in to. Send the stock back and move the people first.
        ''' </remarks>
        Public Function SetActive(id As Integer, isActive As Boolean) As Result
            If Denied(PermissionCodes.Shops.Manage) Then Return Forbidden()
            If OutsideShopScope(id) Then Return ForbiddenShop()
            Dim repo = Uow.Repository(Of Shop)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Shop")

            If Not isActive Then
                Dim held = Uow.Repository(Of ShopStock)().Query().
                    Where(Function(s) s.ShopId = id).
                    Sum(Function(s) CType(s.QuantityOnHand, Decimal?))
                If If(held, 0D) <> 0D Then
                    Return Result.Fail(
                        $"'{entity.Name}' still holds {If(held, 0D):0.###} units of stock. " &
                        "Allocate it back to central or to another shop before closing.")
                End If

                Dim staff = Uow.Repository(Of User)().Query().
                    Count(Function(u) u.IsActive AndAlso u.ShopId.HasValue AndAlso u.ShopId.Value = id)
                If staff > 0 Then
                    Return Result.Fail(
                        $"{staff} active user(s) are assigned to '{entity.Name}'. Move them to another shop first.")
                End If
            End If

            entity.IsActive = isActive
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            _audit.Record(NameOf(Shop), entity.Id.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {
                            {"IsActive", New FieldChange(Not isActive, isActive)}})
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' The shops the caller may see: all of them, or only their own when they are pinned.
        ''' </summary>
        Private Function Visible() As IQueryable(Of Shop)
            Dim q = Uow.Repository(Of Shop)().Query()
            Dim pinned = PinnedShopId
            If Not pinned.HasValue Then Return q
            Dim mine = pinned.Value
            Return q.Where(Function(s) s.Id = mine)
        End Function

        Private Function Validate(input As ShopInput, existingId As Integer?) As List(Of String)
            Dim errors As New List(Of String)()
            If input Is Nothing Then
                errors.Add("No data supplied.")
                Return errors
            End If
            If String.IsNullOrWhiteSpace(input.Name) Then errors.Add("Name is required.")
            If String.IsNullOrWhiteSpace(input.Code) Then errors.Add("A short code is required.")

            If Not String.IsNullOrWhiteSpace(input.Code) Then
                Dim code = input.Code.Trim().ToLower()
                Dim orgId = TenantContext.OrganizationId
                ' Compared against a plain integer rather than the Integer? parameter: identity
                ' values start at 1, so 0 excludes nothing, and EF is spared a nullable captured
                ' in the expression tree - the shape that fails to translate under VB closures.
                ' Matched on OrganizationId explicitly so the check is honest under the in-memory
                ' store too, which applies no tenant filter of its own.
                Dim excludeId = If(existingId, 0)
                Dim clash = Uow.Repository(Of Shop)().Query().
                    Any(Function(s) s.OrganizationId = orgId AndAlso s.Code.ToLower() = code AndAlso s.Id <> excludeId)
                If clash Then errors.Add("Another shop already uses that code.")
            End If
            Return errors
        End Function

        Private Shared Sub Apply(entity As Shop, input As ShopInput)
            entity.Name = input.Name.Trim()
            entity.Code = input.Code.Trim()
            entity.Address = If(input.Address, String.Empty).Trim()
            entity.Phone = If(input.Phone, String.Empty).Trim()
            entity.IsActive = input.IsActive
        End Sub
    End Class

End Namespace
