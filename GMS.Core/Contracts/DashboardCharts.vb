Namespace Contracts

    ''' <summary>
    ''' The two charts on the dashboard: how takings are moving, and what is driving them. Both
    ''' cover the same trailing window so they can be read together.
    ''' </summary>
    Public NotInheritable Class DashboardCharts
        ''' <summary>Length of the window both charts cover, in days, ending today.</summary>
        Public Property Days As Integer

        ''' <summary>One point per day, oldest first, with days that had no sales present as zero.</summary>
        Public Property DailySales As IReadOnlyList(Of DailySalesPoint) = New List(Of DailySalesPoint)()

        ''' <summary>Best sellers by revenue over the window, largest first.</summary>
        Public Property TopProducts As IReadOnlyList(Of TopProductPoint) = New List(Of TopProductPoint)()
    End Class

    Public NotInheritable Class DailySalesPoint
        ''' <summary>The UTC day, at midnight.</summary>
        Public Property Day As DateTime
        Public Property Total As Decimal
        Public Property TransactionCount As Integer
    End Class

    Public NotInheritable Class TopProductPoint
        Public Property ProductId As Integer
        Public Property ProductName As String = String.Empty
        Public Property QuantitySold As Decimal
        Public Property Revenue As Decimal
    End Class

End Namespace
