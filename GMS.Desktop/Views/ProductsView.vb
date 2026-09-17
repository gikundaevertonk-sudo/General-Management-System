Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    Public NotInheritable Class ProductsView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _search As New TextBox()
        Private ReadOnly _lowOnly As New CheckBox()
        Private ReadOnly _pager As New Label()
        Private _page As Integer = 1
        Private _totalPages As Integer = 1
        Private ReadOnly _canEdit As Boolean

        Public Sub New()
            MyBase.New("Products")
            _canEdit = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Products.Edit)

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            If _canEdit Then
                Dim add = UiKit.PrimaryButton("New product")
                AddHandler add.Click, Sub() OpenEditor(Nothing)
                AddAction(add)
            End If

            BuildToolbar()
            BuildGrid()
            Reload()
        End Sub

        Private Sub BuildToolbar()
            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(8)}

            _search.PlaceholderText = "Search name or SKU…"
            _search.Location = New Point(8, 9) : _search.Width = 260
            AddHandler _search.KeyDown, Sub(s, e)
                                            If e.KeyCode = Keys.Enter Then GoFirst()
                                        End Sub

            Dim go = UiKit.SecondaryButton("Search")
            go.Location = New Point(276, 6) : go.Height = 28
            AddHandler go.Click, Sub() GoFirst()

            _lowOnly.Text = "Low stock only"
            _lowOnly.Location = New Point(380, 11) : _lowOnly.AutoSize = True
            AddHandler _lowOnly.CheckedChanged, Sub() GoFirst()

            Dim prev = UiKit.SecondaryButton("‹ Prev") : prev.Location = New Point(520, 6) : prev.Height = 28 : prev.Width = 74
            AddHandler prev.Click, Sub() GoPage(-1)
            Dim nxt = UiKit.SecondaryButton("Next ›") : nxt.Location = New Point(600, 6) : nxt.Height = 28 : nxt.Width = 74
            AddHandler nxt.Click, Sub() GoPage(1)
            _pager.Location = New Point(684, 12) : _pager.AutoSize = True : _pager.ForeColor = UiKit.MutedText

            bar.Controls.AddRange({_search, go, _lowOnly, prev, nxt, _pager})

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)
        End Sub

        Private Sub BuildGrid()
            _grid.Columns.Add(UiKit.TextColumn("SKU", "Sku", width:=110))
            _grid.Columns.Add(UiKit.TextColumn("Name", "Name", fill:=100))
            _grid.Columns.Add(UiKit.TextColumn("Category", "Category", width:=140))
            _grid.Columns.Add(UiKit.NumberColumn("Price", "UnitPrice"))
            _grid.Columns.Add(UiKit.NumberColumn("Cost", "CostPrice"))
            _grid.Columns.Add(UiKit.NumberColumn("On hand", "QuantityOnHand", "N3"))
            _grid.Columns.Add(UiKit.NumberColumn("Reorder", "ReorderLevel", "N3"))
            _grid.Columns.Add(UiKit.TextColumn("Active", "Active", width:=64))
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()
            AddHandler _grid.CellFormatting, AddressOf HighlightLowStock
        End Sub

        Private Sub HighlightLowStock(sender As Object, e As DataGridViewCellFormattingEventArgs)
            Dim row = TryCast(_grid.Rows(e.RowIndex).DataBoundItem, Row)
            If row IsNot Nothing AndAlso row.Low Then
                e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9)
            End If
        End Sub

        Private Sub GoFirst()
            _page = 1
            Reload()
        End Sub

        Private Sub GoPage(delta As Integer)
            Dim target = _page + delta
            If target >= 1 AndAlso target <= _totalPages Then
                _page = target
                Reload()
            End If
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim opts As New QueryOptions With {.Search = _search.Text, .Page = _page, .PageSize = 25, .SortBy = "name"}
                        Dim result = AppHost.Current.Resolve(Of ProductService)().
                            Search(opts, activeOnly:=False, lowStockOnly:=_lowOnly.Checked)
                        If result.Failed Then
                            MessageBox.Show(Me, result.ErrorMessage, "Products", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If

                        Dim categories = AppHost.Current.Resolve(Of CategoryService)().List()
                        Dim catName = If(categories.Succeeded,
                            categories.Value.ToDictionary(Function(c) c.Id, Function(c) c.Name),
                            New Dictionary(Of Integer, String)())

                        Dim page = result.Value
                        _totalPages = Math.Max(1, page.TotalPages)
                        _pager.Text = $"Page {page.Page} of {_totalPages}  ·  {page.TotalCount} item(s)"

                        _grid.DataSource = page.Items.Select(Function(p) New Row With {
                            .Id = p.Id, .Sku = p.Sku, .Name = p.Name,
                            .Category = If(p.CategoryId.HasValue, catName.GetValueOrDefault(p.CategoryId.Value, "—"), "—"),
                            .UnitPrice = p.UnitPrice, .CostPrice = p.CostPrice,
                            .QuantityOnHand = p.QuantityOnHand, .ReorderLevel = p.ReorderLevel,
                            .Active = If(p.IsActive, "Yes", "No"),
                            .Low = p.IsActive AndAlso p.QuantityOnHand <= p.ReorderLevel
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub EditSelected()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row IsNot Nothing Then OpenEditor(row.Id)
        End Sub

        Private Sub OpenEditor(productId As Integer?)
            If Not _canEdit Then
                UiKit.Info(Me, "You do not have permission to edit products.")
                Return
            End If
            Using f As New ProductEditForm(productId)
                If f.ShowDialog(Me) = DialogResult.OK Then Reload()
            End Using
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property Sku As String
            Public Property Name As String
            Public Property Category As String
            Public Property UnitPrice As Decimal
            Public Property CostPrice As Decimal
            Public Property QuantityOnHand As Decimal
            Public Property ReorderLevel As Decimal
            Public Property Active As String
            Public Property Low As Boolean
        End Class
    End Class

End Namespace
