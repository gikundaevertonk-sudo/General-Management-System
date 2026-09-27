Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Models

Namespace Services

    ''' <summary>Sign-in and password changes.</summary>
    Public NotInheritable Class AuthService
        Inherits ServiceBase

        Private ReadOnly _hasher As IPasswordHasher

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock, hasher As IPasswordHasher)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _hasher = Guard.NotNull(hasher)
        End Sub

        Public Function SignIn(userName As String, password As String) As Result(Of AuthenticatedUser)
            If String.IsNullOrWhiteSpace(userName) OrElse String.IsNullOrEmpty(password) Then
                Return Result(Of AuthenticatedUser).Fail("Enter a username and password.")
            End If

            ' Pre-tenant by definition: nobody is signed in yet, so there is no organization to
            ' scope to. See IRepository.QueryAcrossTenants.
            Dim users = Uow.Repository(Of User)()
            Dim user = users.QueryAcrossTenants().FirstOrDefault(
                Function(u) u.UserName.ToLower() = userName.Trim().ToLower())

            ' Same message whether the user is missing or the password is wrong.
            Dim invalid = Result(Of AuthenticatedUser).Fail("Invalid username or password.")
            If user Is Nothing Then Return invalid

            If Not user.IsActive Then
                Return Result(Of AuthenticatedUser).Fail("This account has been deactivated.")
            End If

            If Not _hasher.Verify(password, user.PasswordHash) Then Return invalid

            ' Nothing sets these any more, but rows predating the change can still carry a
            ' lock, and the columns are part of the schema. Clearing on success wipes a stale
            ' one rather than leaving it to sit there forever.
            user.FailedLoginCount = 0
            user.LockedOutUntilUtc = Nothing
            user.LastLoginUtc = Clock.UtcNow
            users.Update(user)
            Uow.SaveChanges()

            Return Result(Of AuthenticatedUser).Ok(Project(user))
        End Function

        Public Function SignInWithTenant(userName As String, password As String, organizationCode As String) As Result(Of AuthenticatedUser)
            If String.IsNullOrWhiteSpace(userName) OrElse String.IsNullOrEmpty(password) OrElse String.IsNullOrWhiteSpace(organizationCode) Then
                Return Result(Of AuthenticatedUser).Fail("Enter organization code, username and password.")
            End If

            ' Find organization by code
            Dim orgs = Uow.Repository(Of Organization)()
            Dim organization = orgs.Query().FirstOrDefault(
                Function(o) o.Code.ToLower() = organizationCode.Trim().ToLower())
            If organization Is Nothing Then
                Return Result(Of AuthenticatedUser).Fail("Organization not found.")
            End If

            If Not organization.IsActive Then
                Return Result(Of AuthenticatedUser).Fail("This organization has been deactivated.")
            End If

            ' Find user by username and organization. Pre-tenant by definition - the caller is
            ' establishing which organization it belongs to, so the tenant filter cannot be
            ' applied yet (it would have nothing to filter on and would throw). Scope is not
            ' lost: the organization resolved from organizationCode above is matched explicitly
            ' here, so this can only ever return a user of that one organization.
            Dim users = Uow.Repository(Of User)()
            Dim user = users.QueryAcrossTenants().FirstOrDefault(
                Function(u) u.UserName.ToLower() = userName.Trim().ToLower() AndAlso u.OrganizationId = organization.Id)

            ' Same message whether the user is missing or the password is wrong.
            Dim invalid = Result(Of AuthenticatedUser).Fail("Invalid username or password.")
            If user Is Nothing Then Return invalid

            If Not user.IsActive Then
                Return Result(Of AuthenticatedUser).Fail("This account has been deactivated.")
            End If

            If Not _hasher.Verify(password, user.PasswordHash) Then Return invalid

            ' Nothing sets these any more, but rows predating the change can still carry a
            ' lock, and the columns are part of the schema. Clearing on success wipes a stale
            ' one rather than leaving it to sit there forever.
            user.FailedLoginCount = 0
            user.LockedOutUntilUtc = Nothing
            user.LastLoginUtc = Clock.UtcNow
            users.Update(user)
            Uow.SaveChanges()

            WarnAboutExpiry(organization.Id)

            Return Result(Of AuthenticatedUser).Ok(Project(user))
        End Function

        ''' <summary>
        ''' Raises the "your subscription is about to end" notification if it is due.
        ''' </summary>
        ''' <remarks>
        ''' Sign-in is the trigger because there is no scheduler here, and it is the moment the
        ''' warning can actually be acted on. Swallowing the failure is deliberate: a reminder is
        ''' worth strictly less than being able to sign in, so nothing it does may turn a valid
        ''' sign-in into a failed one.
        ''' </remarks>
        Private Sub WarnAboutExpiry(organizationId As Integer)
            Try
                Dim subscriptions = New SubscriptionService(Uow, CurrentUser, TenantContext, Clock)
                subscriptions.WarnIfExpiringSoon(organizationId)
            Catch
                ' Ignored on purpose. See above.
            End Try
        End Sub

        Public Function ChangePassword(userId As Integer, currentPassword As String, newPassword As String) As Result
            Dim errors = ValidateNewPassword(newPassword)
            If errors.Any() Then Return Result.Fail(errors)

            Dim users = Uow.Repository(Of User)()
            Dim user = users.GetById(userId)
            If user Is Nothing Then Return NotFound("User")

            If Not _hasher.Verify(currentPassword, user.PasswordHash) Then
                Return Result.Fail("The current password is incorrect.")
            End If

            user.PasswordHash = _hasher.Hash(newPassword)
            user.MustChangePassword = False
            users.Update(user)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>Load a user's permission set (e.g. to refresh a live session).</summary>
        Public Function GetPrincipal(userId As Integer) As Result(Of AuthenticatedUser)
            Dim user = Uow.Repository(Of User)().GetById(userId)
            If user Is Nothing Then Return NotFound(Of AuthenticatedUser)("User")
            Return Result(Of AuthenticatedUser).Ok(Project(user))
        End Function

        Friend Shared Function ValidateNewPassword(password As String) As List(Of String)
            Dim errors As New List(Of String)()
            If String.IsNullOrWhiteSpace(password) OrElse password.Length < 8 Then
                errors.Add("Password must be at least 8 characters.")
            End If
            If password IsNot Nothing AndAlso Not password.Any(AddressOf Char.IsDigit) Then
                errors.Add("Password must contain at least one digit.")
            End If
            If password IsNot Nothing AndAlso Not password.Any(AddressOf Char.IsLetter) Then
                errors.Add("Password must contain at least one letter.")
            End If
            Return errors
        End Function

        Private Function Project(user As User) As AuthenticatedUser
            Dim role = Uow.Repository(Of Role)().GetById(user.RoleId)
            Dim permissions = Uow.Repository(Of RolePermission)().Query().
                Where(Function(rp) rp.RoleId = user.RoleId).
                Join(Uow.Repository(Of Permission)().Query(),
                     Function(rp) rp.PermissionId, Function(p) p.Id, Function(rp, p) p.Code).
                ToList()

            Return New AuthenticatedUser With {
                .UserId = user.Id,
                .UserName = user.UserName,
                .FullName = user.FullName,
                .Email = user.Email,
                .RoleId = user.RoleId,
                .RoleName = If(role?.Name, String.Empty),
                .OrganizationId = user.OrganizationId,
                .MustChangePassword = user.MustChangePassword,
                .Permissions = permissions
            }
        End Function
    End Class

End Namespace
