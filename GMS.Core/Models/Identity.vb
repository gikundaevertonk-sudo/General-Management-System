Namespace Models

    ''' <summary>A named set of permissions. Seeded: Admin, Manager, Staff.</summary>
    Public Class Role
        Inherits EntityBase

        Public Property Name As String = String.Empty
        Public Property Description As String = String.Empty
        ''' <summary>System roles cannot be renamed or deleted from the UI.</summary>
        Public Property IsSystem As Boolean

        Public Property Permissions As ICollection(Of RolePermission) = New List(Of RolePermission)()
        Public Property Users As ICollection(Of User) = New List(Of User)()
    End Class

    ''' <summary>A single capability, referenced by a stable string code (see <c>PermissionCodes</c>).</summary>
    Public Class Permission
        Inherits EntityBase

        Public Property Code As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property Category As String = String.Empty

        Public Property Roles As ICollection(Of RolePermission) = New List(Of RolePermission)()
    End Class

    ''' <summary>Join row between <c>Role</c> and <c>Permission</c>.</summary>
    Public Class RolePermission
        Public Property RoleId As Integer
        Public Property Role As Role
        Public Property PermissionId As Integer
        Public Property Permission As Permission
    End Class

    ''' <summary>An account that can sign in. Exactly one role per user in v1.</summary>
    Public Class User
        Inherits AuditableEntity

        Public Property UserName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property FullName As String = String.Empty
        Public Property PasswordHash As String = String.Empty
        Public Property RoleId As Integer
        Public Property Role As Role
        Public Property IsActive As Boolean = True
        ''' <summary>Forces a password change on next successful sign-in.</summary>
        Public Property MustChangePassword As Boolean
        Public Property LastLoginUtc As DateTime?
        Public Property FailedLoginCount As Integer
        Public Property LockedOutUntilUtc As DateTime?
    End Class

End Namespace
