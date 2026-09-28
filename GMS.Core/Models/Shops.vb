Namespace Models

    ''' <summary>
    ''' A trading location belonging to one organization: a branch, outlet or counter.
    ''' </summary>
    ''' <remarks>
    ''' Shops divide an organization's stock without dividing the organization itself. The
    ''' catalogue, customers, suppliers and settings stay shared - a product is defined once and
    ''' sold from anywhere - while the *quantity* of that product is held per location, in
    ''' <see cref="ShopStock"/>. An organization with no shops behaves exactly as it did before
    ''' shops existed: everything sits in the central pool.
    ''' </remarks>
    Public Class Shop
        Inherits AuditableEntity

        Public Property OrganizationId As Integer
        Public Property Organization As Organization

        Public Property Name As String = String.Empty
        ''' <summary>Short reference shown on lists and in transaction notes. Unique per organization.</summary>
        Public Property Code As String = String.Empty
        Public Property Address As String = String.Empty
        Public Property Phone As String = String.Empty
        Public Property IsActive As Boolean = True

        Public Property Stock As ICollection(Of ShopStock) = New List(Of ShopStock)()
    End Class

    ''' <summary>
    ''' How much of one product is held at one shop. Maintained only by <c>InventoryService</c>,
    ''' alongside the <see cref="StockMovement"/> ledger, exactly as <c>Product.QuantityOnHand</c> is.
    ''' </summary>
    ''' <remarks>
    ''' There is deliberately no row for the central pool. Central is the remainder -
    ''' <c>Product.QuantityOnHand</c> minus the sum of these rows - so the two can never disagree
    ''' about how much the organization holds in total. A second cached figure for central would
    ''' be one more number to drift.
    '''
    ''' A missing row means zero, not "unknown": rows are created the first time stock actually
    ''' reaches a shop, so a new shop does not need a row per product in the catalogue.
    ''' </remarks>
    Public Class ShopStock
        Inherits EntityBase

        Public Property OrganizationId As Integer
        Public Property Organization As Organization
        Public Property ShopId As Integer
        Public Property Shop As Shop
        Public Property ProductId As Integer
        Public Property Product As Product

        Public Property QuantityOnHand As Decimal
    End Class

End Namespace
