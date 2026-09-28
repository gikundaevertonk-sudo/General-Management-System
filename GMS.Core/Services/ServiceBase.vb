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

        ''' <summary>
        ''' True when the caller is not the system owner. Guards anything that reaches across
        ''' organizations; see <see cref="ICurrentUser.IsPlatformOperator"/> for why a
        ''' permission code cannot do this job.
        ''' </summary>
        Protected Function DeniedPlatform() As Boolean
            Return Not CurrentUser.IsPlatformOperator
        End Function

        ''' <summary>The shop the caller is confined to, or Nothing for organization-wide access.</summary>
        Protected ReadOnly Property PinnedShopId As Integer?
            Get
                Return CurrentUser.ShopId
            End Get
        End Property

        ''' <summary>
        ''' The shop a call should act on: the caller's own when they are pinned to one,
        ''' otherwise whatever they asked for.
        ''' </summary>
        ''' <remarks>
        ''' A pinned caller's request is overridden rather than rejected, so a shared page that
        ''' offers a shop picker to a manager simply loses the choice for an attendant instead of
        ''' erroring. Anything that must refuse a mismatch outright - reading back a document that
        ''' already names a different shop - calls <see cref="OutsideShopScope"/> instead.
        ''' </remarks>
        Protected Function ResolveShopScope(requested As Integer?) As Integer?
            Dim pinned = PinnedShopId
            Return If(pinned.HasValue, pinned, requested)
        End Function

        ''' <summary>
        ''' True when <paramref name="shopId"/> is not the caller's shop. Always false for a
        ''' caller who is not pinned to one.
        ''' </summary>
        Protected Function OutsideShopScope(shopId As Integer?) As Boolean
            Dim pinned = PinnedShopId
            If Not pinned.HasValue Then Return False
            Return Not shopId.HasValue OrElse shopId.Value <> pinned.Value
        End Function

        Protected Shared Function ForbiddenShop() As Result
            Return Result.Fail("That belongs to another shop.")
        End Function

        Protected Shared Function ForbiddenShop(Of T)() As Result(Of T)
            Return Result(Of T).Fail("That belongs to another shop.")
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
