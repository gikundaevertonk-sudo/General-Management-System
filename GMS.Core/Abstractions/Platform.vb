Namespace Abstractions

    ''' <summary>
    ''' The identity acting in the current call. Each front end supplies its own
    ''' implementation: the desktop app from the signed-in session it holds in
    ''' memory, the web app from the authentication cookie's claims.
    ''' </summary>
    Public Interface ICurrentUser

        ReadOnly Property UserId As Integer?
        ReadOnly Property UserName As String
        ReadOnly Property IsAuthenticated As Boolean

        ''' <summary>True if the current user's role grants <paramref name="permissionCode"/>.</summary>
        Function HasPermission(permissionCode As String) As Boolean

        Function IsInRole(roleName As String) As Boolean

        ''' <summary>
        ''' The one shop this caller is confined to, or <c>Nothing</c> for the whole organization.
        ''' </summary>
        ''' <remarks>
        ''' A second axis of scope, inside the tenant rather than across it: the tenant decides
        ''' which rows exist for this caller at all, this decides which of them they may see and
        ''' touch. It is not a permission and cannot be granted - a shop attendant holds the same
        ''' sale and stock permissions as anyone else, they simply hold them at one location. So a
        ''' service must check it in addition to a permission code, never instead of one.
        ''' </remarks>
        ReadOnly Property ShopId As Integer?

        ''' <summary>
        ''' True for the system owner operating the platform itself, rather than a user of
        ''' any one tenant.
        ''' </summary>
        ''' <remarks>
        ''' Deliberately separate from <see cref="HasPermission"/>. Permissions are granted by
        ''' a role inside an organization, so every tenant's own administrator holds them -
        ''' gating "suspend an organization" on one would let any tenant's admin suspend every
        ''' other tenant. This is not a permission a tenant can ever be given; it is only true
        ''' for a caller authenticated outside the tenant model altogether.
        ''' </remarks>
        ReadOnly Property IsPlatformOperator As Boolean
    End Interface

    ''' <summary>One-way password hashing. Implemented by <c>Pbkdf2PasswordHasher</c>.</summary>
    Public Interface IPasswordHasher

        Function Hash(password As String) As String

        ''' <summary>Constant-time verification of <paramref name="password"/> against a stored hash.</summary>
        Function Verify(password As String, hash As String) As Boolean
    End Interface

    ''' <summary>Abstraction over the system clock so time-based logic is testable.</summary>
    Public Interface IClock
        ReadOnly Property UtcNow As DateTime
    End Interface

End Namespace
