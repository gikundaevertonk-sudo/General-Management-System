Imports GMS.Core.Abstractions

Namespace Security

    ''' <summary>
    ''' A non-interactive principal that passes every permission check. For seeding,
    ''' background jobs and tests only — never register this in a front end.
    ''' </summary>
    Public NotInheritable Class SystemCurrentUser
        Implements ICurrentUser

        Public ReadOnly Property UserId As Integer? Implements ICurrentUser.UserId
            Get
                Return Nothing
            End Get
        End Property

        Public ReadOnly Property UserName As String Implements ICurrentUser.UserName
            Get
                Return "system"
            End Get
        End Property

        Public ReadOnly Property IsAuthenticated As Boolean Implements ICurrentUser.IsAuthenticated
            Get
                Return True
            End Get
        End Property

        Public Function HasPermission(permissionCode As String) As Boolean Implements ICurrentUser.HasPermission
            Return True
        End Function

        Public Function IsInRole(roleName As String) As Boolean Implements ICurrentUser.IsInRole
            Return True
        End Function

        ''' <summary>Nothing: a system principal is confined to no one shop.</summary>
        Public ReadOnly Property ShopId As Integer? Implements ICurrentUser.ShopId
            Get
                Return Nothing
            End Get
        End Property

        Public ReadOnly Property IsPlatformOperator As Boolean Implements ICurrentUser.IsPlatformOperator
            Get
                Return True
            End Get
        End Property
    End Class

    ''' <summary>The principal used before anyone has signed in. Grants nothing.</summary>
    Public NotInheritable Class AnonymousCurrentUser
        Implements ICurrentUser

        Public ReadOnly Property UserId As Integer? Implements ICurrentUser.UserId
            Get
                Return Nothing
            End Get
        End Property

        Public ReadOnly Property UserName As String Implements ICurrentUser.UserName
            Get
                Return String.Empty
            End Get
        End Property

        Public ReadOnly Property IsAuthenticated As Boolean Implements ICurrentUser.IsAuthenticated
            Get
                Return False
            End Get
        End Property

        Public Function HasPermission(permissionCode As String) As Boolean Implements ICurrentUser.HasPermission
            Return False
        End Function

        Public Function IsInRole(roleName As String) As Boolean Implements ICurrentUser.IsInRole
            Return False
        End Function

        ''' <summary>
        ''' Nothing. Not a widening: nobody who is signed out reaches a service that reads this,
        ''' because every one of them refuses on a permission check first.
        ''' </summary>
        Public ReadOnly Property ShopId As Integer? Implements ICurrentUser.ShopId
            Get
                Return Nothing
            End Get
        End Property

        Public ReadOnly Property IsPlatformOperator As Boolean Implements ICurrentUser.IsPlatformOperator
            Get
                Return False
            End Get
        End Property
    End Class

End Namespace
