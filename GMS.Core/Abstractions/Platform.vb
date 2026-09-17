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
