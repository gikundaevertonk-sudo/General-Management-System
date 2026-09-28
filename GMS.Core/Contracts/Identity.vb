Namespace Contracts

    ''' <summary>
    ''' The result of a successful sign-in. Front ends turn this into their own
    ''' session (desktop) or authentication cookie claims (web).
    ''' </summary>
    Public NotInheritable Class AuthenticatedUser
        Public Property UserId As Integer
        Public Property UserName As String = String.Empty
        Public Property FullName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property RoleId As Integer
        Public Property RoleName As String = String.Empty
        Public Property OrganizationId As Integer
        ''' <summary>The shop this account is confined to, or Nothing for the whole organization.</summary>
        Public Property ShopId As Integer?
        ''' <summary>Display name of <see cref="ShopId"/>, so a front end need not look it up to show it.</summary>
        Public Property ShopName As String = String.Empty
        Public Property MustChangePassword As Boolean
        Public Property Permissions As IReadOnlyCollection(Of String) = New List(Of String)()

        Public Function HasPermission(code As String) As Boolean
            Return Permissions.Contains(code)
        End Function
    End Class

End Namespace
