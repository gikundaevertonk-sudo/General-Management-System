Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class InventoryView
        Inherits ViewBase

        Private ReadOnly _product As New ComboBox()
        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _onHand As New Label()
        Private ReadOnly _canAdjust As Boolean
        Private ReadOnly _hasShops As Boolean

        Public Sub New()
            MyBase.New("Inventory")
            _canAdjust = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Inventory.Adjust)

            ' The ledger only needs a Location column once stock can be in more than one place.
            Dim shops = AppHost.Current.Resolve(Of ShopService)().List()
            _hasShops = shops.Succeeded AndAlso shops.Value.Count > 0

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() LoadLedger()
            AddAction(refresh)

            If _canAdjust Then
                Dim adjust = UiKit.PrimaryButton("Adjust stock")
                AddHandler adjust.Click, Sub() OpenAdjust()
                AddAction(adjust)
            End If

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(8)}
            _product.DropDownStyle = ComboBoxStyle.DropDownList
            _product.SetBounds(8, 8, 320, 24)
            _product.DisplayMember = "Text" : _product.ValueMember = "Value"
            AddHandler _product.SelectedIndexChanged, Sub() LoadLedger()
            _onHand.SetBounds(344, 12, 900, 20)
            _onHand.ForeColor = UiKit.MutedText
            bar.Controls.AddRange({_product, _onHand})

            _grid.Columns.Add(UiKit.TextColumn("When (UTC)", "When", width:=150))
            If _hasShops Then _grid.Columns.Add(UiKit.TextColumn("Location", "Location", width:=130))
            _grid.Columns.Add(UiKit.TextColumn("Direction", "Direction", width:=90))
            _grid.Columns.Add(UiKit.TextColumn("Reason", "Reason", width:=140))
            _grid.Columns.Add(UiKit.NumberColumn("Qty", "Quantity", "N3", 90))
            _grid.Columns.Add(UiKit.NumberColumn("Balance", "QuantityAfter", "N3", 90))
            _grid.Columns.Add(UiKit.TextColumn("Reference", "Reference", width:=130))
            _grid.Columns.Add(UiKit.TextColumn("Note", "Note", fill:=100))

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)

            LoadProducts()
        End Sub

        Private Sub LoadProducts()
            Dim res = AppHost.Current.Resolve(Of ProductService)().Search(New QueryOptions With {.PageSize = 1000, .SortBy = "name"})
            If res.Succeeded Then
                _product.DataSource = res.Value.Items.Select(Function(p) New With {.Value = p.Id, .Text = $"{p.Sku} — {p.Name}"}).ToList()
            End If
        End Sub

        Private Function SelectedProductId() As Integer
            Dim id = 0
            Integer.TryParse(Convert.ToString(_product.SelectedValue), id)
            Return id
        End Function

        Private Sub LoadLedger()
            Guarded(Sub()
                        Dim pid = SelectedProductId()
                        If pid = 0 Then Return

                        Dim inventory = AppHost.Current.Resolve(Of InventoryService)()
                        Dim prod = AppHost.Current.Resolve(Of ProductService)().GetById(pid)
                        _onHand.Text = If(prod.Succeeded,
                            $"{If(_hasShops, "Across the organisation", "On hand")}: " &
                            $"{prod.Value.QuantityOnHand:N3} {prod.Value.UnitOfMeasure}{WhereItIs(inventory, pid)}",
                            "")

                        Dim res = inventory.GetLedger(pid, New QueryOptions With {.PageSize = 200})
                        If res.Failed Then
                            MessageBox.Show(Me, res.ErrorMessage, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If
                        _grid.DataSource = res.Value.Items.Select(Function(r) New With {
                            .When = r.MovedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                            r.Location,
                            .Direction = r.Direction.ToString(),
                            .Reason = r.Reason.ToString(),
                            r.Quantity, r.QuantityAfter,
                            .Reference = r.TransactionNumber,
                            r.Note
                        }).ToList()
                    End Sub)
        End Sub

        ''' <summary>
        ''' A one-line breakdown of where a product's stock is sitting, for the header. Empty when
        ''' the organisation has no shops, where the only answer would be "all of it, here".
        ''' </summary>
        Private Function WhereItIs(inventory As InventoryService, productId As Integer) As String
            If Not _hasShops Then Return ""
            Dim spread = inventory.GetDistribution(productId)
            If spread.Failed Then Return ""
            Dim parts = spread.Value.Where(Function(r) r.QuantityOnHand <> 0D).
                Select(Function(r) $"{r.Location} {r.QuantityOnHand:N3}").ToList()
            If Not parts.Any() Then Return ""
            Return "   ·   " & String.Join("   ", parts)
        End Function

        Private Sub OpenAdjust()
            Dim pid = SelectedProductId()
            If pid = 0 Then UiKit.Info(Me, "Select a product first.") : Return
            Using f As New Forms.StockAdjustForm(pid)
                If f.ShowDialog(Me) = DialogResult.OK Then LoadLedger()
            End Using
        End Sub
    End Class

End Namespace
