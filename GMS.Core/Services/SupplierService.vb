Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class SupplierInput
        Public Property Name As String = String.Empty
        Public Property ContactName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property IsActive As Boolean = True
    End Class

    Public NotInheritable Class SupplierService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
        End Sub

        Public Function Search(options As QueryOptions) As Result(Of PagedResult(Of Supplier))
            If Denied(PermissionCodes.Suppliers.View) Then Return Forbidden(Of PagedResult(Of Supplier))()

            Dim q = Uow.Repository(Of Supplier)().Query()
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(s) s.Name.ToLower().Contains(term) OrElse s.Email.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim items = q.OrderBy(Function(s) s.Name).Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of Supplier)).Ok(
                New PagedResult(Of Supplier)(items, total, options.Page, options.PageSize))
        End Function

        Public Function GetById(id As Integer) As Result(Of Supplier)
            If Denied(PermissionCodes.Suppliers.View) Then Return Forbidden(Of Supplier)()
            Dim entity = Uow.Repository(Of Supplier)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of Supplier)("Supplier"), Result(Of Supplier).Ok(entity))
        End Function

        Public Function Create(input As SupplierInput) As Result(Of Supplier)
            If Denied(PermissionCodes.Suppliers.Edit) Then Return Forbidden(Of Supplier)()
            If input Is Nothing OrElse String.IsNullOrWhiteSpace(input.Name) Then
                Return Result(Of Supplier).Fail("Name is required.")
            End If

            Dim entity As New Supplier()
            Apply(entity, input)
            entity.CreatedAtUtc = Clock.UtcNow
            entity.CreatedByUserId = CurrentUser.UserId
            Uow.Repository(Of Supplier)().Add(entity)
            Uow.SaveChanges()
            Return Result(Of Supplier).Ok(entity)
        End Function

        Public Function Update(id As Integer, input As SupplierInput) As Result
            If Denied(PermissionCodes.Suppliers.Edit) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Supplier)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Supplier")
            If input Is Nothing OrElse String.IsNullOrWhiteSpace(input.Name) Then
                Return Result.Fail("Name is required.")
            End If

            Apply(entity, input)
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function SetActive(id As Integer, isActive As Boolean) As Result
            If Denied(PermissionCodes.Suppliers.Edit) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Supplier)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Supplier")
            entity.IsActive = isActive
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Private Shared Sub Apply(entity As Supplier, input As SupplierInput)
            entity.Name = input.Name.Trim()
            entity.ContactName = If(input.ContactName, String.Empty).Trim()
            entity.Email = If(input.Email, String.Empty).Trim()
            entity.Phone = If(input.Phone, String.Empty).Trim()
            entity.Address = If(input.Address, String.Empty).Trim()
            entity.IsActive = input.IsActive
        End Sub
    End Class

End Namespace
