Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class ProductInput
        Public Property Sku As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property CategoryId As Integer?
        Public Property UnitPrice As Decimal
        Public Property CostPrice As Decimal
        Public Property UnitOfMeasure As String = "each"
        Public Property ReorderLevel As Decimal
        Public Property IsActive As Boolean = True
    End Class

    Public NotInheritable Class ProductService
        Inherits ServiceBase

        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _audit = Guard.NotNull(audit)
        End Sub

        Public Function Search(options As QueryOptions,
                               Optional categoryId As Integer? = Nothing,
                               Optional activeOnly As Boolean = False,
                               Optional lowStockOnly As Boolean = False) As Result(Of PagedResult(Of Product))

            If Denied(PermissionCodes.Products.View) Then Return Forbidden(Of PagedResult(Of Product))()

            Dim q = Uow.Repository(Of Product)().Query()

            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(p) p.Name.ToLower().Contains(term) OrElse p.Sku.ToLower().Contains(term))
            End If
            If categoryId.HasValue Then
                Dim cid = categoryId.Value
                q = q.Where(Function(p) p.CategoryId.HasValue AndAlso p.CategoryId.Value = cid)
            End If
            If activeOnly Then q = q.Where(Function(p) p.IsActive)

            If PinnedShopId.HasValue Then Return SearchForShop(q, options, lowStockOnly)

            If lowStockOnly Then q = q.Where(Function(p) p.QuantityOnHand <= p.ReorderLevel)

            Dim total = q.Count()
            Dim items = SortProducts(q, options).Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of Product)).Ok(
                New PagedResult(Of Product)(items, total, options.Page, options.PageSize))
        End Function

        ''' <summary>
        ''' The catalogue as one shop sees it: the same products, but each one's quantity and price
        ''' are that shop's rather than the organisation's.
        ''' </summary>
        ''' <remarks>
        ''' Rewriting <c>QuantityOnHand</c> and <c>UnitPrice</c> on the returned instances is
        ''' deliberate, and safe because EF reads are NoTracking - these are detached copies that
        ''' are never saved, and the caller only reads them. It is done here rather than by giving
        ''' the caller a different row type so that every existing screen and picker in both front
        ''' ends shows an attendant the right figures without being rewritten. Without it the
        ''' Products list told someone standing at one counter what the whole company held.
        '''
        ''' <c>GetById</c> deliberately does *not* do this: it is what the product editor loads and
        ''' saves back, so a shop price must never be able to overwrite the catalogue price. Nobody
        ''' pinned to a shop can edit products today, and this keeps that true even if they could.
        '''
        ''' Filtering, sorting and paging move in-memory because the figures being filtered on are
        ''' no longer the ones in the products table. A small business's catalogue is small enough
        ''' that this costs nothing; the organisation-wide path above is untouched.
        ''' </remarks>
        Private Function SearchForShop(q As IQueryable(Of Product), options As QueryOptions,
                                       lowStockOnly As Boolean) As Result(Of PagedResult(Of Product))
            Dim shopId = PinnedShopId.Value
            Dim atShop = Uow.Repository(Of ShopStock)().Query().
                Where(Function(s) s.ShopId = shopId).
                Select(Function(s) New With {s.ProductId, s.QuantityOnHand, s.UnitPrice}).
                ToList()
            Dim quantities = atShop.ToDictionary(Function(s) s.ProductId, Function(s) s.QuantityOnHand)
            Dim prices = atShop.Where(Function(s) s.UnitPrice.HasValue).
                ToDictionary(Function(s) s.ProductId, Function(s) s.UnitPrice.Value)

            Dim all = q.ToList()
            For Each p In all
                p.QuantityOnHand = quantities.GetValueOrDefault(p.Id, 0D)
                If prices.ContainsKey(p.Id) Then p.UnitPrice = prices(p.Id)
            Next
            If lowStockOnly Then all = all.Where(Function(p) p.QuantityOnHand <= p.ReorderLevel).ToList()

            Dim sorted = SortProducts(all.AsQueryable(), options).ToList()
            Return Result(Of PagedResult(Of Product)).Ok(
                New PagedResult(Of Product)(sorted.Skip(options.Skip).Take(options.PageSize).ToList(),
                                            sorted.Count, options.Page, options.PageSize))
        End Function

        Public Function GetById(id As Integer) As Result(Of Product)
            If Denied(PermissionCodes.Products.View) Then Return Forbidden(Of Product)()
            Dim entity = Uow.Repository(Of Product)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of Product)("Product"), Result(Of Product).Ok(entity))
        End Function

        ''' <summary>
        ''' Products at or below their reorder level. Routed through <see cref="Search"/> so a
        ''' caller pinned to a shop is told what *their* shelves are short of, not the company's.
        ''' </summary>
        Public Function LowStock() As Result(Of IReadOnlyList(Of Product))
            Dim found = Search(New QueryOptions With {.PageSize = QueryOptions.MaxPageSize, .SortBy = "name"},
                               activeOnly:=True, lowStockOnly:=True)
            If found.Failed Then Return Result(Of IReadOnlyList(Of Product)).Fail(found.ErrorMessage)
            Return Result(Of IReadOnlyList(Of Product)).Ok(found.Value.Items)
        End Function

        Public Function Create(input As ProductInput) As Result(Of Product)
            If Denied(PermissionCodes.Products.Edit) Then Return Forbidden(Of Product)()
            Dim errors = Validate(input, Nothing)
            If errors.Any() Then Return Result(Of Product).Fail(errors)

            Dim entity As New Product()
            Apply(entity, input)
            entity.QuantityOnHand = 0D
            entity.CreatedAtUtc = Clock.UtcNow
            entity.CreatedByUserId = CurrentUser.UserId
            Uow.Repository(Of Product)().Add(entity)
            _audit.Record(NameOf(Product), entity.Id.ToString(), AuditAction.Create)
            Uow.SaveChanges()
            Return Result(Of Product).Ok(entity)
        End Function

        Public Function Update(id As Integer, input As ProductInput) As Result
            If Denied(PermissionCodes.Products.Edit) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Product)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Product")

            Dim errors = Validate(input, id)
            If errors.Any() Then Return Result.Fail(errors)

            Dim changes As New Dictionary(Of String, FieldChange)()
            If entity.UnitPrice <> input.UnitPrice Then changes(NameOf(entity.UnitPrice)) = New FieldChange(entity.UnitPrice, input.UnitPrice)
            If entity.CostPrice <> input.CostPrice Then changes(NameOf(entity.CostPrice)) = New FieldChange(entity.CostPrice, input.CostPrice)
            If entity.ReorderLevel <> input.ReorderLevel Then changes(NameOf(entity.ReorderLevel)) = New FieldChange(entity.ReorderLevel, input.ReorderLevel)

            Apply(entity, input)
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            _audit.Record(NameOf(Product), entity.Id.ToString(), AuditAction.Update, changes)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function Delete(id As Integer) As Result
            If Denied(PermissionCodes.Products.Delete) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Product)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Product")

            Dim used = Uow.Repository(Of TransactionLine)().Query().Any(Function(l) l.ProductId = id)
            If used Then
                ' Preserve history: deactivate instead of deleting.
                entity.IsActive = False
                entity.UpdatedAtUtc = Clock.UtcNow
                entity.UpdatedByUserId = CurrentUser.UserId
                repo.Update(entity)
                _audit.Record(NameOf(Product), id.ToString(), AuditAction.Update,
                              New Dictionary(Of String, FieldChange) From {{"IsActive", New FieldChange(True, False)}})
                Uow.SaveChanges()
                Return Result.Fail("This product is used by transactions, so it was deactivated rather than deleted.")
            End If

            repo.Remove(entity)
            _audit.Record(NameOf(Product), id.ToString(), AuditAction.Delete)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Private Shared Function SortProducts(q As IQueryable(Of Product), options As QueryOptions) As IQueryable(Of Product)
            Select Case options.SortBy?.ToLowerInvariant()
                Case "sku"
                    Return If(options.SortDescending, q.OrderByDescending(Function(p) p.Sku), q.OrderBy(Function(p) p.Sku))
                Case "quantity", "quantityonhand"
                    Return If(options.SortDescending, q.OrderByDescending(Function(p) p.QuantityOnHand), q.OrderBy(Function(p) p.QuantityOnHand))
                Case "price", "unitprice"
                    Return If(options.SortDescending, q.OrderByDescending(Function(p) p.UnitPrice), q.OrderBy(Function(p) p.UnitPrice))
                Case Else
                    Return If(options.SortDescending, q.OrderByDescending(Function(p) p.Name), q.OrderBy(Function(p) p.Name))
            End Select
        End Function

        Private Function Validate(input As ProductInput, existingId As Integer?) As List(Of String)
            Dim errors As New List(Of String)()
            If input Is Nothing Then
                errors.Add("No data supplied.")
                Return errors
            End If
            If String.IsNullOrWhiteSpace(input.Sku) Then errors.Add("SKU is required.")
            If String.IsNullOrWhiteSpace(input.Name) Then errors.Add("Name is required.")
            If input.UnitPrice < 0D Then errors.Add("Unit price cannot be negative.")
            If input.CostPrice < 0D Then errors.Add("Cost price cannot be negative.")
            If input.ReorderLevel < 0D Then errors.Add("Reorder level cannot be negative.")

            If Not String.IsNullOrWhiteSpace(input.Sku) Then
                Dim sku = input.Sku.Trim().ToLower()
                ' A plain integer, not the Integer? parameter: EF names the SQL parameter after a
                ' captured nullable's VB closure field, and Npgsql rejects the '$' in that name.
                ' See ShopService.Validate.
                Dim excludeId = If(existingId, 0)
                Dim clash = Uow.Repository(Of Product)().Query().
                    Any(Function(p) p.Sku.ToLower() = sku AndAlso p.Id <> excludeId)
                If clash Then errors.Add("Another product already uses that SKU.")
            End If

            If input.CategoryId.HasValue Then
                Dim cid = input.CategoryId.Value
                If Uow.Repository(Of Category)().GetById(cid) Is Nothing Then errors.Add("The selected category was not found.")
            End If
            Return errors
        End Function

        Private Shared Sub Apply(entity As Product, input As ProductInput)
            entity.Sku = input.Sku.Trim()
            entity.Name = input.Name.Trim()
            entity.Description = If(input.Description, String.Empty).Trim()
            entity.CategoryId = input.CategoryId
            entity.UnitPrice = input.UnitPrice
            entity.CostPrice = input.CostPrice
            entity.UnitOfMeasure = If(String.IsNullOrWhiteSpace(input.UnitOfMeasure), "each", input.UnitOfMeasure.Trim())
            entity.ReorderLevel = input.ReorderLevel
            entity.IsActive = input.IsActive
        End Sub
    End Class

End Namespace
