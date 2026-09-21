Namespace Models

    ''' <summary>Represents a customer organization (tenant) in the multi-tenant system.</summary>
    Public Class Organization
        Inherits AuditableEntity

        Public Property Name As String
        Public Property Code As String ' Unique identifier for the organization (used in URLs/login)
        Public Property Email As String
        Public Property Plan As String ' trial | basic | professional | enterprise
        Public Property TrialEndsAtUtc As DateTime?
        Public Property SubscriptionEndsAtUtc As DateTime?
        Public Property IsActive As Boolean = True
        Public Property MaxUsers As Integer = 5
        Public Property Features As String ' JSON: {"api_access": true, "custom_reports": false}

        ' Navigation property
        Public Property Subscription As Subscription
        Public Property Users As ICollection(Of User) = New List(Of User)()
        Public Property Products As ICollection(Of Product) = New List(Of Product)()
        Public Property Categories As ICollection(Of Category) = New List(Of Category)()
        Public Property Suppliers As ICollection(Of Supplier) = New List(Of Supplier)()
        Public Property Customers As ICollection(Of Customer) = New List(Of Customer)()
        Public Property Transactions As ICollection(Of Transaction) = New List(Of Transaction)()
        Public Property StockMovements As ICollection(Of StockMovement) = New List(Of StockMovement)()
        Public Property Notifications As ICollection(Of Notification) = New List(Of Notification)()
        Public Property AppSettings As ICollection(Of AppSetting) = New List(Of AppSetting)()
        Public Property AuditEntries As ICollection(Of AuditEntry) = New List(Of AuditEntry)()

    End Class

    ''' <summary>Tracks subscription/billing for an organization.</summary>
    Public Class Subscription
        Inherits AuditableEntity

        Public Property OrganizationId As Integer
        Public Property Plan As String
        Public Property PlanName As String
        Public Property PricePerMonth As Decimal
        Public Property AutoRenew As Boolean = True
        Public Property BillingCycleStartAtUtc As DateTime
        Public Property BillingCycleEndAtUtc As DateTime
        Public Property StripeSubscriptionId As String ' Nullable for manual renewals
        Public Property PaymentStatus As String ' active | expired | failed
        Public Property CancelledAtUtc As DateTime?

        ' Navigation property
        Public Property Organization As Organization

    End Class

End Namespace
