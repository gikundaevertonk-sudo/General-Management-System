Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    ''' <summary>
    ''' The central point for running more than one shop: every location down the top grid, and
    ''' what the selected one is holding down the bottom.
    ''' </summary>
    ''' <remarks>
    ''' Central is listed as a location alongside the shops rather than given a screen of its own.
    ''' It is where stock sits before anyone sends it anywhere, so reading it and allocating out of
    ''' it work exactly as they do for a branch.
    ''' </remarks>
    Public NotInheritable Class ShopsView
        Inherits ViewBase

        Private ReadOnly _locations As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _stock As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _stockHeading As New Label()
        Private ReadOnly _canManage As Boolean
        Private ReadOnly _canAllocate As Boolean
        Private ReadOnly _pinnedShopId As Integer?

        Public Sub New()
            MyBase.New("Shops")
            Dim principal = AppHost.Current.Session.Principal
            _canManage = principal.HasPermission(PermissionCodes.Shops.Manage)
            _canAllocate = principal.HasPermission(PermissionCodes.Shops.Allocate)
            _pinnedShopId = principal.ShopId

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            If _canManage Then
                Dim toggle = UiKit.SecondaryButton("Open / Close")
                AddHandler toggle.Click, Sub() ToggleSelected()
                AddAction(toggle)
                Dim edit = UiKit.SecondaryButton("Edit shop")
                AddHandler edit.Click, Sub() EditSelected()
                AddAction(edit)
            End If

            If _canAllocate Then
                Dim allocate = UiKit.SecondaryButton("Allocate stock…")
                AddHandler allocate.Click, Sub() OpenAllocate()
                AddAction(allocate)
            End If

            If _canManage Then
                Dim add = UiKit.PrimaryButton("New shop")
                AddHandler add.Click, Sub() OpenEditor(Nothing)
                AddAction(add)
            End If

            BuildGrids()
            Reload()
        End Sub

        Private Sub BuildGrids()
            _locations.Columns.Add(UiKit.TextColumn("Location", "Name", fill:=100))
            _locations.Columns.Add(UiKit.TextColumn("Code", "Code", width:=90))
            _locations.Columns.Add(UiKit.NumberColumn("Staff", "StaffCount", "N0", 70))
            _locations.Columns.Add(UiKit.NumberColumn("Items", "SkuCount", "N0", 70))
            _locations.Columns.Add(UiKit.NumberColumn("Units", "QuantityOnHand", "N3"))
            _locations.Columns.Add(UiKit.NumberColumn("Value at cost", "StockValueAtCost", "N2", 110))
            _locations.Columns.Add(UiKit.NumberColumn("Low", "LowStockCount", "N0", 60))
            _locations.Columns.Add(UiKit.TextColumn("Open", "OpenText", width:=64))
            AddHandler _locations.SelectionChanged, Sub() LoadStockForSelection()
            AddHandler _locations.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()

            _stock.Columns.Add(UiKit.TextColumn("SKU", "Sku", width:=120))
            _stock.Columns.Add(UiKit.TextColumn("Product", "ProductName", fill:=100))
            _stock.Columns.Add(UiKit.NumberColumn("On hand", "QuantityOnHand", "N3"))
            _stock.Columns.Add(UiKit.NumberColumn("Reorder at", "ReorderLevel", "N3"))
            _stock.Columns.Add(UiKit.NumberColumn("Value at cost", "ValueAtCost", "N2", 110))
            AddHandler _stock.CellFormatting, AddressOf HighlightLowStock

            Dim split As New SplitContainer With {
                .Dock = DockStyle.Fill, .Orientation = Orientation.Horizontal,
                .SplitterDistance = 220, .Panel1MinSize = 120, .Panel2MinSize = 120}

            Dim top As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            top.Controls.Add(_locations)

            Dim bottom As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8, 0, 8, 8)}
            _stockHeading.Dock = DockStyle.Top
            _stockHeading.Height = 26
            _stockHeading.Font = New Font("Segoe UI Semibold", 9.5F)
            bottom.Controls.Add(_stock)
            bottom.Controls.Add(_stockHeading)

            split.Panel1.Controls.Add(top)
            split.Panel2.Controls.Add(bottom)
            Body.Controls.Add(split)
        End Sub

        Private Sub HighlightLowStock(sender As Object, e As DataGridViewCellFormattingEventArgs)
            Dim row = TryCast(_stock.Rows(e.RowIndex).DataBoundItem, StockRow)
            If row IsNot Nothing AndAlso row.Low Then
                e.CellStyle.ForeColor = Color.FromArgb(180, 83, 9)
            End If
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim rows As New List(Of LocationRow)()

                        ' Only offered to someone who can see past their own counter: a pinned
                        ' attendant has no view of the central pool at all.
                        If Not _pinnedShopId.HasValue Then
                            Dim central = AppHost.Current.Resolve(Of InventoryService)().GetSummaryAt(Nothing)
                            If central.Succeeded Then rows.Add(ToRow(central.Value, Nothing, "—", "—"))
                        End If

                        Dim overview = AppHost.Current.Resolve(Of ShopService)().GetOverview()
                        If overview.Failed Then
                            UiKit.Info(Me, overview.ErrorMessage, "Shops")
                        Else
                            For Each shop In overview.Value
                                rows.Add(ToRow(shop, shop.ShopId, shop.Code, If(shop.IsActive, "Yes", "No")))
                            Next
                        End If

                        _locations.DataSource = rows
                        LoadStockForSelection()
                    End Sub)
        End Sub

        Private Shared Function ToRow(summary As Core.Contracts.ShopSummaryRow, shopId As Integer?,
                                      code As String, openText As String) As LocationRow
            Return New LocationRow With {
                .ShopId = shopId,
                .Name = summary.Name,
                .Code = code,
                .StaffCount = summary.StaffCount,
                .SkuCount = summary.SkuCount,
                .QuantityOnHand = summary.QuantityOnHand,
                .StockValueAtCost = summary.StockValueAtCost,
                .LowStockCount = summary.LowStockCount,
                .OpenText = openText,
                .IsActive = summary.IsActive}
        End Function

        Private Function Selected() As LocationRow
            Return TryCast(_locations.CurrentRow?.DataBoundItem, LocationRow)
        End Function

        Private Sub LoadStockForSelection()
            Dim row = Selected()
            If row Is Nothing Then
                _stockHeading.Text = "Stock"
                _stock.DataSource = Nothing
                Return
            End If

            Guarded(Sub()
                        _stockHeading.Text = $"Stock at {row.Name}"
                        Dim held = AppHost.Current.Resolve(Of InventoryService)().
                            GetStockAt(row.ShopId, New QueryOptions With {.PageSize = QueryOptions.MaxPageSize})
                        If held.Failed Then
                            _stock.DataSource = Nothing
                            Return
                        End If
                        _stock.DataSource = held.Value.Items.Select(Function(r) New StockRow With {
                            .Sku = r.Sku, .ProductName = r.ProductName,
                            .QuantityOnHand = r.QuantityOnHand, .ReorderLevel = r.ReorderLevel,
                            .ValueAtCost = r.ValueAtCost, .Low = r.BelowReorderLevel}).ToList()
                    End Sub)
        End Sub

        Private Sub EditSelected()
            Dim row = Selected()
            If row Is Nothing OrElse Not row.ShopId.HasValue Then
                UiKit.Info(Me, "Select a shop first. Central is not a shop and has nothing to edit.")
                Return
            End If
            OpenEditor(row.ShopId.Value)
        End Sub

        Private Sub OpenEditor(shopId As Integer?)
            If Not _canManage Then
                UiKit.Info(Me, "You do not have permission to manage shops.")
                Return
            End If
            Using f As New ShopEditForm(shopId)
                If f.ShowDialog(Me) = DialogResult.OK Then Reload()
            End Using
        End Sub

        Private Sub ToggleSelected()
            Dim row = Selected()
            If row Is Nothing OrElse Not row.ShopId.HasValue Then
                UiKit.Info(Me, "Select a shop first.")
                Return
            End If

            Dim closing = row.IsActive
            If closing AndAlso Not UiKit.Confirm(Me, $"Close '{row.Name}'? Staff will no longer be able to sell from it.") Then Return

            AppHost.Current.Resolve(Of ShopService)().SetActive(row.ShopId.Value, Not closing).ShowIfFailed(Me)
            Reload()
        End Sub

        Private Sub OpenAllocate()
            Dim row = Selected()
            If row Is Nothing Then
                UiKit.Info(Me, "Select the location the stock is coming from.")
                Return
            End If
            Using f As New StockAllocateForm(row.ShopId, row.Name)
                If f.ShowDialog(Me) = DialogResult.OK Then Reload()
            End Using
        End Sub

        Private NotInheritable Class LocationRow
            ''' <summary>Nothing for the central pool, which has no shop row of its own.</summary>
            Public Property ShopId As Integer?
            Public Property Name As String
            Public Property Code As String
            Public Property StaffCount As Integer
            Public Property SkuCount As Integer
            Public Property QuantityOnHand As Decimal
            Public Property StockValueAtCost As Decimal
            Public Property LowStockCount As Integer
            Public Property OpenText As String
            Public Property IsActive As Boolean
        End Class

        Private NotInheritable Class StockRow
            Public Property Sku As String
            Public Property ProductName As String
            Public Property QuantityOnHand As Decimal
            Public Property ReorderLevel As Decimal
            Public Property ValueAtCost As Decimal
            Public Property Low As Boolean
        End Class
    End Class

End Namespace
