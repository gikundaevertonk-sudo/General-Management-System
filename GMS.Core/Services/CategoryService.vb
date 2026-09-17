Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class CategoryService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock)
            MyBase.New(uow, currentUser, clock)
        End Sub

        Public Function List() As Result(Of IReadOnlyList(Of Category))
            If Denied(PermissionCodes.Categories.View) Then Return Forbidden(Of IReadOnlyList(Of Category))()
            Return Result(Of IReadOnlyList(Of Category)).Ok(
                Uow.Repository(Of Category)().Query().OrderBy(Function(c) c.Name).ToList())
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
            Return Result.Ok()
        End Function
    End Class

End Namespace
