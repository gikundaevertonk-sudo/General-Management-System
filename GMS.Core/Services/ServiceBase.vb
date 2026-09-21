Imports GMS.Core.Abstractions
Imports GMS.Core.Common

Namespace Services

    ''' <summary>Shared dependencies and permission helpers for the service layer.</summary>
    Public MustInherit Class ServiceBase

        Protected ReadOnly Uow As IUnitOfWork
        Protected ReadOnly CurrentUser As ICurrentUser
        Protected ReadOnly TenantContext As ITenantContext
        Protected ReadOnly Clock As IClock

        Protected Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            Me.Uow = Guard.NotNull(uow)
            Me.CurrentUser = Guard.NotNull(currentUser)
            Me.TenantContext = Guard.NotNull(tenantContext)
            Me.Clock = Guard.NotNull(clock)
        End Sub

        ''' <summary>True when the caller lacks <paramref name="permissionCode"/>.</summary>
        Protected Function Denied(permissionCode As String) As Boolean
            Return Not CurrentUser.HasPermission(permissionCode)
        End Function

        Protected Shared Function Forbidden() As Result
            Return Result.Fail("You do not have permission to perform this action.")
        End Function

        Protected Shared Function Forbidden(Of T)() As Result(Of T)
            Return Result(Of T).Fail("You do not have permission to perform this action.")
        End Function

        Protected Shared Function NotFound(entity As String) As Result
            Return Result.Fail($"{entity} was not found.")
        End Function

        Protected Shared Function NotFound(Of T)(entity As String) As Result(Of T)
            Return Result(Of T).Fail($"{entity} was not found.")
        End Function
    End Class

End Namespace
