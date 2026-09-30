Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>Fields accepted when creating or updating a customer.</summary>
    Public NotInheritable Class CustomerInput
        Public Property Code As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property ContactName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property BillingAddress As String = String.Empty
        Public Property Notes As String = String.Empty
        Public Property IsActive As Boolean = True
    End Class

    Public NotInheritable Class CustomerService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
        End Sub

        Public Function Search(options As QueryOptions) As Result(Of PagedResult(Of Customer))
            If Denied(PermissionCodes.Customers.View) Then Return Forbidden(Of PagedResult(Of Customer))()

            Dim q = Uow.Repository(Of Customer)().Query()
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(c) c.Name.ToLower().Contains(term) _
                                     OrElse c.Code.ToLower().Contains(term) _
                                     OrElse c.Email.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim items = q.OrderBy(Function(c) c.Name).Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of Customer)).Ok(
                New PagedResult(Of Customer)(items, total, options.Page, options.PageSize))
        End Function

        Public Function GetById(id As Integer) As Result(Of Customer)
            If Denied(PermissionCodes.Customers.View) Then Return Forbidden(Of Customer)()
            Dim entity = Uow.Repository(Of Customer)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of Customer)("Customer"), Result(Of Customer).Ok(entity))
        End Function

        Public Function Create(input As CustomerInput) As Result(Of Customer)
            If Denied(PermissionCodes.Customers.Edit) Then Return Forbidden(Of Customer)()
            Dim errors = Validate(input, Nothing)
            If errors.Any() Then Return Result(Of Customer).Fail(errors)

            Dim entity As New Customer()
            Apply(entity, input)
            entity.CreatedAtUtc = Clock.UtcNow
            entity.CreatedByUserId = CurrentUser.UserId
            Uow.Repository(Of Customer)().Add(entity)
            Uow.SaveChanges()
            Return Result(Of Customer).Ok(entity)
        End Function

        Public Function Update(id As Integer, input As CustomerInput) As Result
            If Denied(PermissionCodes.Customers.Edit) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Customer)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Customer")

            Dim errors = Validate(input, id)
            If errors.Any() Then Return Result.Fail(errors)

            Apply(entity, input)
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function SetActive(id As Integer, isActive As Boolean) As Result
            If Denied(PermissionCodes.Customers.Edit) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Customer)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Customer")
            entity.IsActive = isActive
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Private Function Validate(input As CustomerInput, existingId As Integer?) As List(Of String)
            Dim errors As New List(Of String)()
            If input Is Nothing Then
                errors.Add("No data supplied.")
                Return errors
            End If
            If String.IsNullOrWhiteSpace(input.Name) Then errors.Add("Name is required.")
            If Not String.IsNullOrWhiteSpace(input.Email) AndAlso Not input.Email.Contains("@"c) Then
                errors.Add("Email address is not valid.")
            End If
            If Not String.IsNullOrWhiteSpace(input.Code) Then
                Dim code = input.Code.Trim().ToLower()
                ' Plain integer rather than the Integer?; see ProductService.Validate.
                Dim excludeId = If(existingId, 0)
                Dim clash = Uow.Repository(Of Customer)().Query().
                    Any(Function(c) c.Code.ToLower() = code AndAlso c.Id <> excludeId)
                If clash Then errors.Add("Another customer already uses that code.")
            End If
            Return errors
        End Function

        Private Shared Sub Apply(entity As Customer, input As CustomerInput)
            entity.Code = If(input.Code, String.Empty).Trim()
            entity.Name = input.Name.Trim()
            entity.ContactName = If(input.ContactName, String.Empty).Trim()
            entity.Email = If(input.Email, String.Empty).Trim()
            entity.Phone = If(input.Phone, String.Empty).Trim()
            entity.BillingAddress = If(input.BillingAddress, String.Empty).Trim()
            entity.Notes = If(input.Notes, String.Empty).Trim()
            entity.IsActive = input.IsActive
        End Sub
    End Class

End Namespace
