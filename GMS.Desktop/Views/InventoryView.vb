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

        Public Sub New()
            MyBase.New("Inventory")
            _canAdjust = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Inventory.Adjust)

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
            _onHand.SetBounds(344, 12, 300, 20)
            _onHand.ForeColor = UiKit.MutedText
            bar.Controls.AddRange({_product, _onHand})

            _grid.Columns.Add(UiKit.TextColumn("When (UTC)", "When", width:=150))
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

                        Dim prod = AppHost.Current.Resolve(Of ProductService)().GetById(pid)
                        _onHand.Text = If(prod.Succeeded, $"On hand: {prod.Value.QuantityOnHand:N3} {prod.Value.UnitOfMeasure}", "")

                        Dim res = AppHost.Current.Resolve(Of InventoryService)().
                            GetLedger(pid, New QueryOptions With {.PageSize = 200})
                        If res.Failed Then
                            MessageBox.Show(Me, res.ErrorMessage, "Inventory", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If
                        _grid.DataSource = res.Value.Items.Select(Function(r) New With {
                            .When = r.MovedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                            .Direction = r.Direction.ToString(),
                            .Reason = r.Reason.ToString(),
                            r.Quantity, r.QuantityAfter,
                            .Reference = r.TransactionNumber,
                            r.Note
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub OpenAdjust()
            Dim pid = SelectedProductId()
            If pid = 0 Then UiKit.Info(Me, "Select a product first.") : Return
            Using f As New Forms.StockAdjustForm(pid)
                If f.ShowDialog(Me) = DialogResult.OK Then LoadLedger()
            End Using
        End Sub
    End Class

End Namespace
