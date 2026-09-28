Imports GMS.Core.Enums

Namespace Models

    ''' <summary>
    ''' A unified stock/money document. <c>Type</c> decides whether it moves stock
    ''' in or out and which party it involves. Immutable once <c>Confirmed</c>.
    ''' </summary>
    Public Class Transaction
        Inherits AuditableEntity

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        ''' <summary>Human reference, e.g. SAL-2026-0001 / PUR-2026-0007.</summary>
        Public Property TransactionNumber As String = String.Empty
        Public Property Type As TransactionType
        Public Property Status As TransactionStatus = TransactionStatus.Draft
        Public Property TransactionDate As DateTime

        ''' <summary>
        ''' The shop this document belongs to, or <c>Nothing</c> for the central pool.
        ''' </summary>
        ''' <remarks>
        ''' Decided when the draft is created and fixed from then on, because it is the location
        ''' whose stock <c>Confirm</c> will move: a sale raised at a shop must fail when that shop
        ''' is short, even if another shop has plenty. Changing it after lines exist would silently
        ''' re-point those lines at a different balance, so it is not editable.
        ''' </remarks>
        Public Property ShopId As Integer?
        Public Property Shop As Shop

        Public Property CustomerId As Integer?
        Public Property Customer As Customer
        Public Property SupplierId As Integer?
        Public Property Supplier As Supplier

        Public Property Subtotal As Decimal
        Public Property TaxTotal As Decimal
        Public Property Total As Decimal
        Public Property Notes As String = String.Empty
        Public Property ConfirmedAtUtc As DateTime?
        Public Property CancelledAtUtc As DateTime?

        Public Property Lines As ICollection(Of TransactionLine) = New List(Of TransactionLine)()
    End Class

    ''' <summary>One product row on a <c>Transaction</c>.</summary>
    Public Class TransactionLine
        Inherits EntityBase

        Public Property TransactionId As Integer
        Public Property Transaction As Transaction
        Public Property ProductId As Integer
        Public Property Product As Product

        Public Property Description As String = String.Empty
        Public Property Quantity As Decimal
        Public Property UnitPrice As Decimal
        ''' <summary>Percentage, e.g. 20 for 20%. Defaults from settings at line creation.</summary>
        Public Property TaxRate As Decimal
        Public Property LineSubtotal As Decimal
        Public Property LineTax As Decimal
        Public Property LineTotal As Decimal
    End Class

    ''' <summary>
    ''' Immutable ledger of every change to a product's quantity on hand.
    ''' The sum of <c>SignedQuantity</c> for a product equals its <c>QuantityOnHand</c>.
    ''' </summary>
    Public Class StockMovement
        Inherits EntityBase

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        Public Property ProductId As Integer
        Public Property Product As Product
        Public Property TransactionLineId As Integer?
        Public Property TransactionLine As TransactionLine

        ''' <summary>The location this movement happened at; <c>Nothing</c> is the central pool.</summary>
        ''' <remarks>
        ''' Null for every row written before shops existed, which is correct rather than merely
        ''' convenient: an organization that never had a shop kept all of its stock centrally.
        ''' </remarks>
        Public Property ShopId As Integer?
        Public Property Shop As Shop

        Public Property Direction As StockMovementDirection
        Public Property Reason As StockMovementReason
        ''' <summary>Always positive; combine with <c>Direction</c> for the signed effect.</summary>
        Public Property Quantity As Decimal
        ''' <summary>
        ''' Balance at <see cref="ShopId"/> immediately after this movement was applied - the
        ''' shop's own quantity, or the central remainder when no shop is named.
        ''' </summary>
        Public Property QuantityAfter As Decimal
        Public Property Note As String = String.Empty
        Public Property MovedAtUtc As DateTime
        Public Property UserId As Integer?

        Public ReadOnly Property SignedQuantity As Decimal
            Get
                Return If(Direction = StockMovementDirection.In, Quantity, -Quantity)
            End Get
        End Property
    End Class

    ''' <summary>Row written by the audit interceptor for every create/update/delete.</summary>
    Public Class AuditEntry
        Inherits EntityBase

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        Public Property EntityName As String = String.Empty
        Public Property EntityId As String = String.Empty
        Public Property Action As AuditAction
        ''' <summary>JSON object of changed fields: { field: { old, new } }.</summary>
        Public Property ChangesJson As String = String.Empty
        Public Property UserId As Integer?
        Public Property UserName As String = String.Empty
        Public Property TimestampUtc As DateTime
    End Class

    ''' <summary>An alert shown in-app. A null <c>TargetUserId</c> means broadcast to all.</summary>
    Public Class Notification
        Inherits EntityBase

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        Public Property Type As NotificationType
        Public Property Severity As NotificationSeverity = NotificationSeverity.Info
        Public Property Title As String = String.Empty
        Public Property Message As String = String.Empty
        Public Property TargetUserId As Integer?
        Public Property RelatedEntityName As String = String.Empty
        Public Property RelatedEntityId As String = String.Empty
        ''' <summary>De-dupe key so the same condition is not raised repeatedly.</summary>
        Public Property DedupeKey As String = String.Empty
        Public Property IsRead As Boolean
        Public Property CreatedAtUtc As DateTime
        Public Property ReadAtUtc As DateTime?
    End Class

    ''' <summary>Key/value application configuration (company name, currency, tax rate…). Tenant-scoped.</summary>
    Public Class AppSetting
        Inherits EntityBase

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        Public Property Key As String = String.Empty
        Public Property Value As String = String.Empty
    End Class

End Namespace
