Imports GMS.Core.Abstractions
Imports GMS.Core.Contracts

Namespace App

    ''' <summary>
    ''' Holds the signed-in user for the life of the process. A single instance is
    ''' registered in DI and read by <see cref="DesktopCurrentUser"/>.
    ''' </summary>
    Public NotInheritable Class SessionContext

        ''' <summary>The signed-in principal, or <c>Nothing</c> before sign-in.</summary>
        Public Property Principal As AuthenticatedUser

        ''' <summary>
        ''' The organization (tenant) ID for the current session. Defaults to the default
        ''' organization so that start-up seeding, which runs before sign-in, is scoped
        ''' somewhere valid rather than to organization 0.
        ''' </summary>
        Public Property TenantId As Integer = DefaultOrganizationId

        ''' <summary>The organization <c>DataSeeder</c> creates for a fresh installation.</summary>
        Public Const DefaultOrganizationId As Integer = 1

        ''' <summary>
        ''' When true every permission check passes and no principal is required.
        ''' Used only while seeding at start-up.
        ''' </summary>
        Public Property SystemMode As Boolean

        Public ReadOnly Property IsSignedIn As Boolean
            Get
                Return Principal IsNot Nothing
            End Get
        End Property

        Public Sub SignOut()
            Principal = Nothing
        End Sub
    End Class

    ''' <summary>Desktop <see cref="ICurrentUser"/> backed by <see cref="SessionContext"/>.</summary>
    Public NotInheritable Class DesktopCurrentUser
        Implements ICurrentUser

        Private ReadOnly _session As SessionContext

        Public Sub New(session As SessionContext)
            _session = session
        End Sub

        Public ReadOnly Property UserId As Integer? Implements ICurrentUser.UserId
            Get
                Return _session.Principal?.UserId
            End Get
        End Property

        Public ReadOnly Property UserName As String Implements ICurrentUser.UserName
            Get
                If _session.SystemMode Then Return "system"
                Return If(_session.Principal?.UserName, String.Empty)
            End Get
        End Property

        Public ReadOnly Property IsAuthenticated As Boolean Implements ICurrentUser.IsAuthenticated
            Get
                Return _session.SystemMode OrElse _session.Principal IsNot Nothing
            End Get
        End Property

        Public Function HasPermission(permissionCode As String) As Boolean Implements ICurrentUser.HasPermission
            If _session.SystemMode Then Return True
            Return _session.Principal IsNot Nothing AndAlso _session.Principal.HasPermission(permissionCode)
        End Function

        Public Function IsInRole(roleName As String) As Boolean Implements ICurrentUser.IsInRole
            If _session.SystemMode Then Return True
            Return String.Equals(_session.Principal?.RoleName, roleName, StringComparison.OrdinalIgnoreCase)
        End Function

        ''' <summary>
        ''' The shop the signed-in user is pinned to. Nothing while seeding, which runs before
        ''' anyone has signed in and must not be confined to a shop.
        ''' </summary>
        Public ReadOnly Property ShopId As Integer? Implements ICurrentUser.ShopId
            Get
                If _session.SystemMode Then Return Nothing
                Return _session.Principal?.ShopId
            End Get
        End Property

        ''' <summary>
        ''' Always false. The operator console is web-only; the desktop client signs in as a
        ''' user of one organization and has no way to act on any other.
        ''' </summary>
        Public ReadOnly Property IsPlatformOperator As Boolean Implements ICurrentUser.IsPlatformOperator
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace
