Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security
Imports Microsoft.Extensions.Caching.Memory

Namespace Services

    Public NotInheritable Class CategoryService
        Inherits ServiceBase

        Private ReadOnly _cache As IMemoryCache

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, cache As IMemoryCache)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _cache = Guard.NotNull(cache)
        End Sub

        Public Function List() As Result(Of IReadOnlyList(Of Category))
            If Denied(PermissionCodes.Categories.View) Then Return Forbidden(Of IReadOnlyList(Of Category))()

            Dim cacheKey = $"categories_org_{TenantContext.OrganizationId}"
            Dim cachedValue As Object = Nothing
            If _cache.TryGetValue(cacheKey, cachedValue) Then
                Return Result(Of IReadOnlyList(Of Category)).Ok(CType(cachedValue, IReadOnlyList(Of Category)))
            End If

            Dim categories As IReadOnlyList(Of Category) = Uow.Repository(Of Category)().Query().OrderBy(Function(c) c.Name).ToList()
            _cache.Set(cacheKey, categories, TimeSpan.FromMinutes(10))
            Return Result(Of IReadOnlyList(Of Category)).Ok(categories)
        End Function

        Public Function GetById(id As Integer) As Result(Of Category)
            If Denied(PermissionCodes.Categories.View) Then Return Forbidden(Of Category)()
            Dim entity = Uow.Repository(Of Category)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of Category)("Category"), Result(Of Category).Ok(entity))
        End Function

        Public Function Create(name As String, description As String, parentCategoryId As Integer?) As Result(Of Category)
            If Denied(PermissionCodes.Categories.Edit) Then Return Forbidden(Of Category)()

            Dim repo = Uow.Repository(Of Category)()
            If String.IsNullOrWhiteSpace(name) Then Return Result(Of Category).Fail("Name is required.")
            If repo.Query().Any(Function(c) c.Name.ToLower() = name.Trim().ToLower()) Then
                Return Result(Of Category).Fail("A category with that name already exists.")
            End If
            If parentCategoryId.HasValue AndAlso repo.GetById(parentCategoryId.Value) Is Nothing Then
                Return Result(Of Category).Fail("The parent category was not found.")
            End If

            Dim entity As New Category With {
                .Name = name.Trim(),
                .Description = If(description, String.Empty).Trim(),
                .ParentCategoryId = parentCategoryId,
                .CreatedAtUtc = Clock.UtcNow,
                .CreatedByUserId = CurrentUser.UserId
            }
            repo.Add(entity)
            Uow.SaveChanges()
            InvalidateCache()
            Return Result(Of Category).Ok(entity)
        End Function

        Public Function Update(id As Integer, name As String, description As String, parentCategoryId As Integer?) As Result
            If Denied(PermissionCodes.Categories.Edit) Then Return Forbidden()

            Dim repo = Uow.Repository(Of Category)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Category")
            If String.IsNullOrWhiteSpace(name) Then Return Result.Fail("Name is required.")
            If parentCategoryId.HasValue AndAlso parentCategoryId.Value = id Then
                Return Result.Fail("A category cannot be its own parent.")
            End If
            If repo.Query().Any(Function(c) c.Id <> id AndAlso c.Name.ToLower() = name.Trim().ToLower()) Then
                Return Result.Fail("A category with that name already exists.")
            End If

            entity.Name = name.Trim()
            entity.Description = If(description, String.Empty).Trim()
            entity.ParentCategoryId = parentCategoryId
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            InvalidateCache()
            Return Result.Ok()
        End Function

        Public Function Delete(id As Integer) As Result
            If Denied(PermissionCodes.Categories.Edit) Then Return Forbidden()

            Dim repo = Uow.Repository(Of Category)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Category")
            If Uow.Repository(Of Product)().Query().Any(Function(p) p.CategoryId.HasValue AndAlso p.CategoryId.Value = id) Then
                Return Result.Fail("This category still has products assigned to it.")
            End If
            If repo.Query().Any(Function(c) c.ParentCategoryId.HasValue AndAlso c.ParentCategoryId.Value = id) Then
                Return Result.Fail("This category has sub-categories.")
            End If

            repo.Remove(entity)
            Uow.SaveChanges()
            InvalidateCache()
            Return Result.Ok()
        End Function

        Private Sub InvalidateCache()
            _cache.Remove($"categories_org_{TenantContext.OrganizationId}")
        End Sub
    End Class

End Namespace
