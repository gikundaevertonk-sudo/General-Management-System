Namespace Models

    ''' <summary>Product grouping. Optional single-level parent for sub-categories.</summary>
    Public Class Category
        Inherits AuditableEntity

        Public Property Name As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property ParentCategoryId As Integer?
        Public Property ParentCategory As Category
        Public Property Children As ICollection(Of Category) = New List(Of Category)()
        Public Property Products As ICollection(Of Product) = New List(Of Product)()
    End Class

    ''' <summary>A stock-keeping item that can be bought and sold.</summary>
    Public Class Product
        Inherits AuditableEntity

        Public Property Sku As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property Description As String = String.Empty
        Public Property CategoryId As Integer?
        Public Property Category As Category

        ''' <summary>Default selling price, exclusive of tax.</summary>
        Public Property UnitPrice As Decimal
        ''' <summary>Most recent purchase/landed cost, used for valuation and margin.</summary>
        Public Property CostPrice As Decimal
        Public Property UnitOfMeasure As String = "each"

        ''' <summary>Cached running total maintained only by <c>InventoryService</c>.</summary>
        Public Property QuantityOnHand As Decimal
        ''' <summary>At or below this level the low-stock scan raises a notification.</summary>
        Public Property ReorderLevel As Decimal
        Public Property IsActive As Boolean = True

        Public Property Movements As ICollection(Of StockMovement) = New List(Of StockMovement)()
    End Class

    ''' <summary>A party goods are purchased from.</summary>
    Public Class Supplier
        Inherits AuditableEntity

        Public Property Name As String = String.Empty
        Public Property ContactName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property IsActive As Boolean = True
    End Class

    ''' <summary>A party goods are sold to.</summary>
    Public Class Customer
        Inherits AuditableEntity

        Public Property Code As String = String.Empty
        Public Property Name As String = String.Empty
        Public Property ContactName As String = String.Empty
        Public Property Email As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property BillingAddress As String = String.Empty
        Public Property Notes As String = String.Empty
        Public Property IsActive As Boolean = True
    End Class

End Namespace
