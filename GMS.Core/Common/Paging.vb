Namespace Common

    ''' <summary>Standard paging / search / sort request passed to list endpoints.</summary>
    Public Class QueryOptions
        Private _page As Integer = 1
        Private _pageSize As Integer = 25

        ''' <summary>1-based page number. Values below 1 are clamped to 1.</summary>
        Public Property Page As Integer
            Get
                Return _page
            End Get
            Set(value As Integer)
                _page = Math.Max(1, value)
            End Set
        End Property

        ''' <summary>Rows per page. Clamped to 1..MaxPageSize.</summary>
        Public Property PageSize As Integer
            Get
                Return _pageSize
            End Get
            Set(value As Integer)
                _pageSize = Math.Min(MaxPageSize, Math.Max(1, value))
            End Set
        End Property

        Public Const MaxPageSize As Integer = 200

        ''' <summary>Free-text term; each service decides which fields it matches.</summary>
        Public Property Search As String = String.Empty
        Public Property SortBy As String = String.Empty
        Public Property SortDescending As Boolean

        Public ReadOnly Property Skip As Integer
            Get
                Return (Page - 1) * PageSize
            End Get
        End Property
    End Class

    ''' <summary>A single page of results plus the totals needed to render a pager.</summary>
    Public NotInheritable Class PagedResult(Of T)

        Public ReadOnly Property Items As IReadOnlyList(Of T)
        Public ReadOnly Property TotalCount As Integer
        Public ReadOnly Property Page As Integer
        Public ReadOnly Property PageSize As Integer

        Public Sub New(items As IEnumerable(Of T), totalCount As Integer, page As Integer, pageSize As Integer)
            Me.Items = If(items, Enumerable.Empty(Of T)()).ToList()
            Me.TotalCount = totalCount
            Me.Page = page
            Me.PageSize = pageSize
        End Sub

        Public ReadOnly Property TotalPages As Integer
            Get
                Return If(PageSize <= 0, 0, CInt(Math.Ceiling(TotalCount / CDbl(PageSize))))
            End Get
        End Property

        Public ReadOnly Property HasPrevious As Boolean
            Get
                Return Page > 1
            End Get
        End Property

        Public ReadOnly Property HasNext As Boolean
            Get
                Return Page < TotalPages
            End Get
        End Property

        Public Shared Function Empty(options As QueryOptions) As PagedResult(Of T)
            Return New PagedResult(Of T)(Enumerable.Empty(Of T)(), 0, options.Page, options.PageSize)
        End Function
    End Class

End Namespace
