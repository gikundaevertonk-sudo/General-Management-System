Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class UserInput
        Public Property UserName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property FullName As String = String.Empty
        Public Property RoleId As Integer
        ''' <summary>
        ''' Pin this account to one shop, or leave empty for organization-wide access. This is what
        ''' turns an ordinary staff account into a shop attendant.
        ''' </summary>
        Public Property ShopId As Integer?
        Public Property IsActive As Boolean = True
    End Class

    Public NotInheritable Class UserService
        Inherits ServiceBase

        Private ReadOnly _hasher As IPasswordHasher
        Private ReadOnly _audit As AuditService

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock,
                       hasher As IPasswordHasher, audit As AuditService)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _hasher = Guard.NotNull(hasher)
            _audit = Guard.NotNull(audit)
        End Sub

        Public Function Search(options As QueryOptions) As Result(Of PagedResult(Of User))
            If Denied(PermissionCodes.Users.View) Then Return Forbidden(Of PagedResult(Of User))()

            Dim q = Uow.Repository(Of User)().Query()
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(u) u.UserName.ToLower().Contains(term) _
                                     OrElse u.FullName.ToLower().Contains(term) _
                                     OrElse u.Email.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim items = q.OrderBy(Function(u) u.UserName).Skip(options.Skip).Take(options.PageSize).ToList()
            Return Result(Of PagedResult(Of User)).Ok(
                New PagedResult(Of User)(items, total, options.Page, options.PageSize))
        End Function

        Public Function GetById(id As Integer) As Result(Of User)
            If Denied(PermissionCodes.Users.View) Then Return Forbidden(Of User)()
            Dim entity = Uow.Repository(Of User)().GetById(id)
            Return If(entity Is Nothing, NotFound(Of User)("User"), Result(Of User).Ok(entity))
        End Function

        ''' <summary>Creates a user with a temporary password that must be changed on first sign-in.</summary>
        Public Function Create(input As UserInput, temporaryPassword As String) As Result(Of User)
            If Denied(PermissionCodes.Users.Manage) Then Return Forbidden(Of User)()

            Dim errors = Validate(input, Nothing)
            errors.AddRange(AuthService.ValidateNewPassword(temporaryPassword))
            If errors.Any() Then Return Result(Of User).Fail(errors)

            ' The organization's user cap, enforced. It was stored, displayed on the operator
            ' console and editable there, but nothing ever read it - an organization capped at five
            ' could create fifty, and the console showed "7/5 users" without complaint.
            ' Counted with an explicit OrganizationId match rather than relying on the query
            ' filter, so the figure is right under the in-memory store too, which has no filters.
            Dim orgId = TenantContext.OrganizationId
            Dim org = Uow.Repository(Of Organization)().GetById(orgId)
            If org IsNot Nothing AndAlso org.MaxUsers > 0 Then
                Dim existing = Uow.Repository(Of User)().Query().Count(Function(u) u.OrganizationId = orgId)
                If existing >= org.MaxUsers Then
                    Return Result(Of User).Fail(
                        $"This organization is limited to {org.MaxUsers} users and already has {existing}. " &
                        "Deactivating a user does not free a place - remove one, or ask for the limit to be raised.")
                End If
            End If

            ' OrganizationId is set here rather than left to GmsDbContext.SaveChanges to stamp.
            ' That stamp only exists on the EF path, so under the in-memory store the row would
            ' keep OrganizationId 0 - and the cap counted above, which matches on OrganizationId,
            ' would never see any of these users and so would never trip.
            Dim entity As New User With {
                .OrganizationId = orgId,
                .UserName = input.UserName.Trim(),
                .Email = If(input.Email, String.Empty).Trim(),
                .FullName = If(input.FullName, String.Empty).Trim(),
                .RoleId = input.RoleId,
                .ShopId = input.ShopId,
                .IsActive = input.IsActive,
                .PasswordHash = _hasher.Hash(temporaryPassword),
                .MustChangePassword = True,
                .CreatedAtUtc = Clock.UtcNow,
                .CreatedByUserId = CurrentUser.UserId
            }
            Uow.Repository(Of User)().Add(entity)
            _audit.Record(NameOf(User), entity.Id.ToString(), AuditAction.Create)
            Uow.SaveChanges()
            Return Result(Of User).Ok(entity)
        End Function

        Public Function Update(id As Integer, input As UserInput) As Result
            If Denied(PermissionCodes.Users.Manage) Then Return Forbidden()
            Dim repo = Uow.Repository(Of User)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("User")

            Dim errors = Validate(input, id)
            If errors.Any() Then Return Result.Fail(errors)

            If entity.RoleId <> input.RoleId AndAlso Not WouldLeaveAnAdmin(entity, input.RoleId) Then
                Return Result.Fail("This is the last active administrator; assign another before changing this role.")
            End If

            entity.UserName = input.UserName.Trim()
            entity.Email = If(input.Email, String.Empty).Trim()
            entity.FullName = If(input.FullName, String.Empty).Trim()
            entity.RoleId = input.RoleId
            entity.ShopId = input.ShopId
            entity.IsActive = input.IsActive
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            _audit.Record(NameOf(User), entity.Id.ToString(), AuditAction.Update)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function SetActive(id As Integer, isActive As Boolean) As Result
            If Denied(PermissionCodes.Users.Manage) Then Return Forbidden()
            Dim repo = Uow.Repository(Of User)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("User")

            If Not isActive AndAlso Not WouldLeaveAnAdmin(entity, entity.RoleId) Then
                Return Result.Fail("This is the last active administrator and cannot be deactivated.")
            End If
            If Not isActive AndAlso CurrentUser.UserId = id Then
                Return Result.Fail("You cannot deactivate your own account.")
            End If

            entity.IsActive = isActive
            entity.UpdatedAtUtc = Clock.UtcNow
            entity.UpdatedByUserId = CurrentUser.UserId
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>Admin reset: sets a new temporary password and forces a change at next sign-in.</summary>
        Public Function ResetPassword(id As Integer, temporaryPassword As String) As Result
            If Denied(PermissionCodes.Users.Manage) Then Return Forbidden()
            Dim errors = AuthService.ValidateNewPassword(temporaryPassword)
            If errors.Any() Then Return Result.Fail(errors)

            Dim repo = Uow.Repository(Of User)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("User")

            entity.PasswordHash = _hasher.Hash(temporaryPassword)
            entity.MustChangePassword = True
            entity.FailedLoginCount = 0
            entity.LockedOutUntilUtc = Nothing
            repo.Update(entity)
            _audit.Record(NameOf(User), entity.Id.ToString(), AuditAction.Update,
                          New Dictionary(Of String, FieldChange) From {{"PasswordHash", New FieldChange("(hidden)", "(reset)")}})
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Private Function WouldLeaveAnAdmin(changing As User, newRoleId As Integer) As Boolean
            Dim adminRole = Uow.Repository(Of Role)().Query().
                FirstOrDefault(Function(r) r.Name.ToLower() = "admin")
            If adminRole Is Nothing Then Return True

            Dim isCurrentlyAdmin = changing.RoleId = adminRole.Id AndAlso changing.IsActive
            If Not isCurrentlyAdmin Then Return True
            If newRoleId = adminRole.Id Then Return True

            Dim otherAdmins = Uow.Repository(Of User)().Query().
                Count(Function(u) u.Id <> changing.Id AndAlso u.RoleId = adminRole.Id AndAlso u.IsActive)
            Return otherAdmins > 0
        End Function

        Private Function Validate(input As UserInput, existingId As Integer?) As List(Of String)
            Dim errors As New List(Of String)()
            If input Is Nothing Then
                errors.Add("No data supplied.")
                Return errors
            End If
            If String.IsNullOrWhiteSpace(input.UserName) Then errors.Add("Username is required.")
            If String.IsNullOrWhiteSpace(input.Email) OrElse Not input.Email.Contains("@"c) Then
                errors.Add("A valid email address is required.")
            End If
            If Uow.Repository(Of Role)().GetById(input.RoleId) Is Nothing Then errors.Add("The selected role was not found.")

            If input.ShopId.HasValue Then
                Dim shop = Uow.Repository(Of Shop)().GetById(input.ShopId.Value)
                If shop Is Nothing Then
                    errors.Add("The selected shop was not found.")
                ElseIf Not shop.IsActive Then
                    ' Pinning someone to a closed shop would leave them signed in with nothing to
                    ' sell from and no stock to see.
                    errors.Add($"'{shop.Name}' is closed; choose an open shop or leave the shop empty.")
                End If
            End If

            If Not String.IsNullOrWhiteSpace(input.UserName) Then
                Dim name = input.UserName.Trim().ToLower()
                ' Plain integer rather than the Integer?; see ProductService.Validate.
                Dim excludeId = If(existingId, 0)
                Dim clash = Uow.Repository(Of User)().Query().
                    Any(Function(u) u.UserName.ToLower() = name AndAlso u.Id <> excludeId)
                If clash Then errors.Add("That username is already taken.")
            End If
            Return errors
        End Function
    End Class

End Namespace
