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
        Public Property MustChangePassword As Boolean
        Public Property Permissions As IReadOnlyCollection(Of String) = New List(Of String)()

        Public Function HasPermission(code As String) As Boolean
            Return Permissions.Contains(code)
        End Function
    End Class

End Namespace
