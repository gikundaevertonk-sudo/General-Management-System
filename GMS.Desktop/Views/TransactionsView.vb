Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    Public NotInheritable Class TransactionsView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _type As New ComboBox()
        Private ReadOnly _status As New ComboBox()
        Private ReadOnly _search As New TextBox()
        Private ReadOnly _canCreate As Boolean
        Private ReadOnly _canConfirm As Boolean
        Private ReadOnly _canCancel As Boolean

        Public Sub New()
            MyBase.New("Transactions")
            Dim pr = AppHost.Current.Session.Principal
            _canCreate = pr.HasPermission(PermissionCodes.Transactions.Create)
            _canConfirm = pr.HasPermission(PermissionCodes.Transactions.Confirm)
            _canCancel = pr.HasPermission(PermissionCodes.Transactions.Cancel)

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            If _canCreate Then
                Dim newPurchase = UiKit.SecondaryButton("New purchase")
                AddHandler newPurchase.Click, Sub() OpenNew(TransactionType.Purchase)
                AddAction(newPurchase)
                Dim newSale = UiKit.PrimaryButton("New sale")
                AddHandler newSale.Click, Sub() OpenNew(TransactionType.Sale)
                AddAction(newSale)
            End If

            BuildToolbar()
            BuildGrid()
            Reload()
        End Sub

        Private Sub BuildToolbar()
            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(8)}

            _type.DropDownStyle = ComboBoxStyle.DropDownList
            _type.Items.AddRange({"All types", "Sales", "Purchases", "Adjustments in", "Adjustments out"})
            _type.SelectedIndex = 0
            _type.Location = New Point(8, 8) : _type.Width = 140
            AddHandler _type.SelectedIndexChanged, Sub() Reload()

            _status.DropDownStyle = ComboBoxStyle.DropDownList
            _status.Items.AddRange({"All statuses", "Draft", "Confirmed", "Cancelled"})
            _status.SelectedIndex = 0
            _status.Location = New Point(158, 8) : _status.Width = 140
            AddHandler _status.SelectedIndexChanged, Sub() Reload()

            _search.PlaceholderText = "Search number or notes…"
            _search.Location = New Point(308, 9) : _search.Width = 220
            AddHandler _search.KeyDown, Sub(s, e) If e.KeyCode = Keys.Enter Then Reload()

            Dim confirmBtn = UiKit.SecondaryButton("Confirm") : confirmBtn.Location = New Point(540, 6) : confirmBtn.Height = 28
            AddHandler confirmBtn.Click, Sub() ConfirmSelected()
            Dim cancelBtn = UiKit.SecondaryButton("Cancel") : cancelBtn.Location = New Point(624, 6) : cancelBtn.Height = 28
            AddHandler cancelBtn.Click, Sub() CancelSelected()

            bar.Controls.AddRange({_type, _status, _search, confirmBtn, cancelBtn})

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)
        End Sub

        Private Sub BuildGrid()
            _grid.Columns.Add(UiKit.TextColumn("Number", "Number", width:=130))
            _grid.Columns.Add(UiKit.TextColumn("Date", "Date", width:=100))
            _grid.Columns.Add(UiKit.TextColumn("Type", "Type", width:=110))
            _grid.Columns.Add(UiKit.TextColumn("Status", "Status", width:=90))
            _grid.Columns.Add(UiKit.TextColumn("Party", "Party", fill:=100))
            _grid.Columns.Add(UiKit.NumberColumn("Total", "Total"))
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then OpenSelected()
        End Sub

        Private Function SelectedType() As TransactionType?
            Select Case _type.SelectedIndex
                Case 1 : Return TransactionType.Sale
                Case 2 : Return TransactionType.Purchase
                Case 3 : Return TransactionType.AdjustmentIn
                Case 4 : Return TransactionType.AdjustmentOut
                Case Else : Return Nothing
            End Select
        End Function

        Private Function SelectedStatus() As TransactionStatus?
            Select Case _status.SelectedIndex
                Case 1 : Return TransactionStatus.Draft
                Case 2 : Return TransactionStatus.Confirmed
                Case 3 : Return TransactionStatus.Cancelled
                Case Else : Return Nothing
            End Select
        End Function

        Public Sub Reload()
            Guarded(Sub()
                        Dim opts As New QueryOptions With {.Search = _search.Text, .PageSize = 100}
                        Dim result = AppHost.Current.Resolve(Of TransactionService)().Search(opts, SelectedType(), SelectedStatus())
                        If result.Failed Then
                            MessageBox.Show(Me, result.ErrorMessage, "Transactions", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If

                        Dim customers = AppHost.Current.Resolve(Of CustomerService)().Search(New QueryOptions With {.PageSize = 1000})
                        Dim suppliers = AppHost.Current.Resolve(Of SupplierService)().Search(New QueryOptions With {.PageSize = 1000})
                        Dim custName = If(customers.Succeeded, customers.Value.Items.ToDictionary(Function(c) c.Id, Function(c) c.Name), New Dictionary(Of Integer, String))
                        Dim suppName = If(suppliers.Succeeded, suppliers.Value.Items.ToDictionary(Function(s) s.Id, Function(s) s.Name), New Dictionary(Of Integer, String))

                        _grid.DataSource = result.Value.Items.Select(Function(t) New Row With {
                            .Id = t.Id, .Number = t.TransactionNumber,
                            .Date = t.TransactionDate.ToString("yyyy-MM-dd"),
                            .Type = t.Type.ToString(), .Status = t.Status.ToString(),
                            .Party = PartyName(t, custName, suppName),
                            .Total = t.Total
                        }).ToList()
                    End Sub)
        End Sub

        Private Shared Function PartyName(t As Transaction,
                                          custName As Dictionary(Of Integer, String),
                                          suppName As Dictionary(Of Integer, String)) As String
            If t.CustomerId.HasValue Then Return custName.GetValueOrDefault(t.CustomerId.Value, "—")
            If t.SupplierId.HasValue Then Return suppName.GetValueOrDefault(t.SupplierId.Value, "—")
            ' A sale with nobody attached is a counter sale, not missing data.
            If t.Type = TransactionType.Sale Then Return "One-off sale"
            Return "—"
        End Function

        Private Function SelectedId() As Integer?
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            Return If(row Is Nothing, CType(Nothing, Integer?), row.Id)
        End Function

        Private Sub OpenSelected()
            Dim id = SelectedId()
            If id Is Nothing Then Return
            Using f As New TransactionEditForm(id.Value)
                f.ShowDialog(Me)
            End Using
            Reload()
        End Sub

        Private Sub OpenNew(type As TransactionType)
            Using f As New TransactionEditForm(type)
                f.ShowDialog(Me)
            End Using
            Reload()
        End Sub

        Private Sub ConfirmSelected()
            Dim id = SelectedId()
            If id Is Nothing Then Return
            If Not _canConfirm Then UiKit.Info(Me, "You do not have permission to confirm transactions.") : Return
            If Not UiKit.Confirm(Me, "Confirm this transaction? Stock will be posted.") Then Return
            AppHost.Current.Resolve(Of TransactionService)().Confirm(id.Value).ShowIfFailed(Me)
            Reload()
        End Sub

        Private Sub CancelSelected()
            Dim id = SelectedId()
            If id Is Nothing Then Return
            If Not _canCancel Then UiKit.Info(Me, "You do not have permission to cancel transactions.") : Return
            If Not UiKit.Confirm(Me, "Cancel this transaction? Any posted stock will be reversed.") Then Return
            AppHost.Current.Resolve(Of TransactionService)().Cancel(id.Value, "Cancelled from desktop").ShowIfFailed(Me)
            Reload()
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property Number As String
            Public Property [Date] As String
            Public Property Type As String
            Public Property Status As String
            Public Property Party As String
            Public Property Total As Decimal
        End Class
    End Class

End Namespace
