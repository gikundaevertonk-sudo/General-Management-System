Imports GMS.Core.Enums

Namespace Contracts

    ''' <summary>Inclusive-from / exclusive-to UTC window for reports and dashboards.</summary>
    Public Structure DateRange
        Public ReadOnly Property FromUtc As DateTime
        Public ReadOnly Property ToUtc As DateTime

        Public Sub New(fromUtc As DateTime, toUtc As DateTime)
            If toUtc < fromUtc Then Throw New ArgumentException("'to' must not precede 'from'.")
            ' Force UTC kind: these values are used as SQL parameters against
            ' 'timestamp with time zone' columns, which reject Unspecified kind.
            Me.FromUtc = DateTime.SpecifyKind(fromUtc, DateTimeKind.Utc)
            Me.ToUtc = DateTime.SpecifyKind(toUtc, DateTimeKind.Utc)
        End Sub

        Public Shared Function Today(nowUtc As DateTime) As DateRange
            Dim start = DateTime.SpecifyKind(nowUtc.Date, DateTimeKind.Utc)
            Return New DateRange(start, start.AddDays(1))
        End Function

        Public Shared Function MonthToDate(nowUtc As DateTime) As DateRange
            Dim start = New DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc)
            Return New DateRange(start, DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc))
        End Function
    End Structure

    Public NotInheritable Class DashboardSummary
        Public Property ProductCount As Integer
        Public Property CustomerCount As Integer
        Public Property LowStockCount As Integer
        Public Property InventoryValueAtCost As Decimal
        Public Property SalesTodayTotal As Decimal
        Public Property SalesMonthToDateTotal As Decimal
        Public Property OpenDraftCount As Integer
        Public Property UnreadNotificationCount As Integer
        Public Property RecentActivity As IReadOnlyList(Of ActivityItem) = New List(Of ActivityItem)()
    End Class

    Public NotInheritable Class ActivityItem
        Public Property WhenUtc As DateTime
        Public Property Summary As String = String.Empty
        Public Property EntityName As String = String.Empty
        Public Property EntityId As String = String.Empty
    End Class

    Public NotInheritable Class SalesSummaryReport
        Public Property Range As DateRange
        Public Property TransactionCount As Integer
        Public Property Subtotal As Decimal
        Public Property Tax As Decimal
        Public Property Total As Decimal
        Public Property Lines As IReadOnlyList(Of SalesSummaryLine) = New List(Of SalesSummaryLine)()
    End Class

    Public NotInheritable Class SalesSummaryLine
        Public Property ProductId As Integer
        Public Property Sku As String = String.Empty
        Public Property ProductName As String = String.Empty
        Public Property QuantitySold As Decimal
        Public Property Revenue As Decimal
    End Class

    Public NotInheritable Class InventoryValuationRow
        Public Property ProductId As Integer
        Public Property Sku As String = String.Empty
        Public Property ProductName As String = String.Empty
        Public Property QuantityOnHand As Decimal
        Public Property UnitCost As Decimal
        Public Property ValueAtCost As Decimal
        Public Property BelowReorderLevel As Boolean
    End Class

    Public NotInheritable Class StockLedgerRow
        Public Property MovedAtUtc As DateTime
        Public Property Direction As StockMovementDirection
        Public Property Reason As StockMovementReason
        Public Property Quantity As Decimal
        Public Property QuantityAfter As Decimal
        Public Property Note As String = String.Empty
        Public Property TransactionNumber As String = String.Empty
        Public Property ShopId As Integer?
        ''' <summary>Shop the movement happened at, or "Central" when it did not happen at one.</summary>
        Public Property Location As String = String.Empty
    End Class

    ''' <summary>
    ''' How much of one product sits at one location. Used both for a shop's own stock list and,
    ''' with <c>ShopId</c> null, for the central row of a product's distribution.
    ''' </summary>
    Public NotInheritable Class ShopStockRow
        Public Property ShopId As Integer?
        Public Property Location As String = String.Empty
        Public Property ProductId As Integer
        Public Property Sku As String = String.Empty
        Public Property ProductName As String = String.Empty
        Public Property UnitOfMeasure As String = String.Empty
        Public Property QuantityOnHand As Decimal
        Public Property ReorderLevel As Decimal
        Public Property UnitCost As Decimal
        Public Property ValueAtCost As Decimal
        Public Property BelowReorderLevel As Boolean
    End Class

    ''' <summary>One shop as it appears on the organization's shop list.</summary>
    Public NotInheritable Class ShopSummaryRow
        Public Property ShopId As Integer
        Public Property Name As String = String.Empty
        Public Property Code As String = String.Empty
        Public Property IsActive As Boolean
        ''' <summary>Accounts pinned to this shop.</summary>
        Public Property StaffCount As Integer
        ''' <summary>Products with a non-zero balance here.</summary>
        Public Property SkuCount As Integer
        Public Property QuantityOnHand As Decimal
        Public Property StockValueAtCost As Decimal
        Public Property LowStockCount As Integer
    End Class

End Namespace
