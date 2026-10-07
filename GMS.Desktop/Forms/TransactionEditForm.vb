Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>
    ''' Header + lines editor for one transaction. Draft transactions are editable;
    ''' confirmed/cancelled ones are shown read-only.
    ''' </summary>
    Public Class TransactionEditForm

        Private ReadOnly _newType As TransactionType?
        Private _txnId As Integer?
        Private _type As TransactionType
        Private _status As TransactionStatus = TransactionStatus.Draft

        Public Sub New(newType As TransactionType)
            Me.New(CType(Nothing, Integer?))
            _newType = newType
            _type = newType
            Text = $"New {newType.ToString().ToLower()}"
            LoadPartyChoices()
            UpdateEnabled()
        End Sub

        Public Sub New(transactionId As Integer?)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            Icon = UiKit.AppIcon
            grdLines.AutoGenerateColumns = False
            ' Without a format each decimal shows its stored scale ("3.5", "35.0", "24"), which
            ' disagrees with the inputs above and the totals below.
            FormatNumberColumn(colQuantity, "N3")
            FormatNumberColumn(colUnitPrice, "N2")
            FormatNumberColumn(colTaxRate, "N2")
            FormatNumberColumn(colLineTotal, "N2")
            _txnId = transactionId

            LoadProducts()
            LoadShops()
            If transactionId.HasValue Then LoadExisting(transactionId.Value)
            UpdateEnabled()
        End Sub

        Private Shared Sub FormatNumberColumn(col As DataGridViewColumn, format As String)
            col.DefaultCellStyle.Format = format
            col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            col.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight
        End Sub

        ''' <summary>
        ''' Fills the shop picker, hiding it when there is no choice to make: an organisation with
        ''' no shops, or an attendant whose document is always their own shop's whatever is picked.
        ''' </summary>
        Private Sub LoadShops()
            cboShop.DisplayMember = "Text"
            cboShop.ValueMember = "Value"

            Dim pinned = AppHost.Current.Session.Principal.ShopId
            Dim res = AppHost.Current.Resolve(Of ShopService)().List()
            Dim shops As IReadOnlyList(Of Shop) = If(res.Succeeded, res.Value, New List(Of Shop)())

            If pinned.HasValue OrElse shops.Count = 0 Then
                lblShop.Visible = False
                cboShop.Visible = False
                Return
            End If

            ' 0 stands for central, because a ComboBox value cannot be Nothing and round-trip.
            Dim choices As New List(Of Object) From {New With {.Value = 0, .Text = "Central"}}
            choices.AddRange(shops.Select(Function(s) CObj(New With {.Value = s.Id, .Text = s.Name})))
            cboShop.DataSource = choices
        End Sub

        Private Sub LoadProducts()
            Dim res = AppHost.Current.Resolve(Of ProductService)().
                Search(New QueryOptions With {.PageSize = 1000, .SortBy = "name"}, activeOnly:=True)
            cboProduct.DisplayMember = "Text"
            cboProduct.ValueMember = "Value"
            cboProduct.DataSource = If(res.Succeeded,
                res.Value.Items.Select(Function(p) New With {.Value = p.Id, .Text = $"{p.Sku} — {p.Name}"}).ToList(),
                New List(Of Object)().Select(Function(o) New With {.Value = 0, .Text = ""}).ToList())
        End Sub

        Private Sub LoadPartyChoices()
            cboParty.DisplayMember = "Text"
            cboParty.ValueMember = "Value"
            ' Only a sale can be to someone who is not on the list; a purchase needs a real
            ' supplier, and an adjustment has no party at all.
            txtWalkIn.Visible = _type = TransactionType.Sale
            If _type = TransactionType.Sale Then
                Dim res = AppHost.Current.Resolve(Of CustomerService)().Search(New QueryOptions With {.PageSize = 1000})
                ' Value 0 is "nobody", and it leads so a counter sale is the default rather
                ' than something you have to go looking for.
                Dim choices As New List(Of Object) From {New With {.Value = 0, .Text = "(one-off sale - no customer)"}}
                If res.Succeeded Then
                    choices.AddRange(res.Value.Items.Select(Function(c) CObj(New With {.Value = c.Id, .Text = c.Name})))
                End If
                cboParty.DataSource = choices
            ElseIf _type = TransactionType.Purchase Then
                Dim res = AppHost.Current.Resolve(Of SupplierService)().Search(New QueryOptions With {.PageSize = 1000})
                cboParty.DataSource = If(res.Succeeded,
                    res.Value.Items.Select(Function(s) New With {.Value = s.Id, .Text = s.Name}).ToList(),
                    New List(Of Object)().Select(Function(o) New With {.Value = 0, .Text = ""}).ToList())
            Else
                cboParty.DataSource = New List(Of Object)().Select(Function(o) New With {.Value = 0, .Text = "(no party)"}).ToList()
                cboParty.Enabled = False
            End If
        End Sub

        Private Sub btnStart_Click(sender As Object, e As EventArgs) Handles btnStart.Click
            lblError.Text = ""
            Dim partyId As Integer? = Nothing
            Dim walkIn = txtWalkIn.Text.Trim()

            If _type = TransactionType.Sale OrElse _type = TransactionType.Purchase Then
                Dim pid = 0
                Integer.TryParse(Convert.ToString(cboParty.SelectedValue), pid)
                If pid > 0 Then
                    partyId = pid
                ElseIf _type = TransactionType.Sale Then
                    ' A counter sale: recorded with nobody attached. Any typed name rides
                    ' along in the notes rather than creating a customer account.
                    partyId = Nothing
                Else
                    lblError.Text = "Choose a supplier first."
                    Return
                End If
            End If

            Dim shopChoice = 0
            If cboShop.Visible Then Integer.TryParse(Convert.ToString(cboShop.SelectedValue), shopChoice)
            Dim shopId As Integer? = If(shopChoice = 0, CType(Nothing, Integer?), shopChoice)

            Dim result = AppHost.Current.Resolve(Of TransactionService)().
                CreateDraft(_type, partyId, dtpDate.Value.Date, txtNotes.Text, walkIn, shopId)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If

            _txnId = result.Value.Id
            _status = TransactionStatus.Draft
            Text = $"{result.Value.TransactionNumber} (draft)"
            RefreshLines(result.Value)
            UpdateEnabled()
        End Sub

        Private Sub LoadExisting(id As Integer)
            Dim result = AppHost.Current.Resolve(Of TransactionService)().GetById(id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            Dim t = result.Value
            _txnId = t.Id
            _type = t.Type
            _status = t.Status
            Text = $"{t.TransactionNumber} ({t.Status.ToString().ToLower()})"
            LoadPartyChoices()
            cboParty.SelectedValue = If(t.CustomerId, If(t.SupplierId, 0))
            If cboShop.Visible Then cboShop.SelectedValue = If(t.ShopId, 0)
            dtpDate.Value = t.TransactionDate.Date
            txtNotes.Text = t.Notes
            RefreshLines(t)
        End Sub

        Private Sub btnAddLine_Click(sender As Object, e As EventArgs) Handles btnAddLine.Click
            lblError.Text = ""
            If _txnId Is Nothing Then Return
            Dim pid = 0
            Integer.TryParse(Convert.ToString(cboProduct.SelectedValue), pid)
            If pid = 0 Then
                lblError.Text = "Choose a product."
                Return
            End If

            Dim input As New TransactionLineInput With {
                .ProductId = pid, .Quantity = numQty.Value,
                .UnitPrice = If(numPrice.Value > 0, CType(numPrice.Value, Decimal?), Nothing),
                .TaxRatePercent = If(numTax.Value > 0, CType(numTax.Value, Decimal?), Nothing)}

            Dim result = AppHost.Current.Resolve(Of TransactionService)().AddLine(_txnId.Value, input)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            RefreshLines(result.Value)
        End Sub

        Private Sub btnRemoveLine_Click(sender As Object, e As EventArgs) Handles btnRemoveLine.Click
            lblError.Text = ""
            If _txnId Is Nothing Then Return
            Dim row = TryCast(grdLines.CurrentRow?.DataBoundItem, LineRow)
            If row Is Nothing Then Return
            Dim result = AppHost.Current.Resolve(Of TransactionService)().RemoveLine(_txnId.Value, row.Id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            ReloadFromServer()
        End Sub

        Private Sub btnConfirm_Click(sender As Object, e As EventArgs) Handles btnConfirm.Click
            lblError.Text = ""
            If _txnId Is Nothing Then Return
            If Not UiKit.Confirm(Me, "Confirm this transaction? Stock will be posted and it can no longer be edited.") Then Return
            Dim result = AppHost.Current.Resolve(Of TransactionService)().Confirm(_txnId.Value)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            DialogResult = DialogResult.OK
            Close()
        End Sub

        Private Sub btnCancelTxn_Click(sender As Object, e As EventArgs) Handles btnCancelTxn.Click
            lblError.Text = ""
            If _txnId Is Nothing Then Return

            ' A confirmed transaction has already moved stock, so cancelling it puts that
            ' stock back. Worth saying out loud before it happens.
            Dim prompt = If(_status = TransactionStatus.Draft,
                            "Cancel this transaction? It will be kept as a cancelled record.",
                            "Cancel this transaction? The stock it posted will be put back.")
            If Not UiKit.Confirm(Me, prompt) Then Return

            Dim result = AppHost.Current.Resolve(Of TransactionService)().
                Cancel(_txnId.Value, "Cancelled from desktop")
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If

            DialogResult = DialogResult.OK
            Close()
        End Sub

        Private Sub btnClose_Click(sender As Object, e As EventArgs) Handles btnClose.Click
            Close()
        End Sub

        Private Sub ReloadFromServer()
            If _txnId Is Nothing Then Return
            Dim result = AppHost.Current.Resolve(Of TransactionService)().GetById(_txnId.Value)
            If result.Succeeded Then RefreshLines(result.Value)
        End Sub

        Private Sub RefreshLines(t As Transaction)
            Dim products = AppHost.Current.Resolve(Of ProductService)().Search(New QueryOptions With {.PageSize = 1000})
            Dim name = If(products.Succeeded,
                products.Value.Items.ToDictionary(Function(p) p.Id, Function(p) p.Name),
                New Dictionary(Of Integer, String))

            grdLines.DataSource = t.Lines.Select(Function(l) New LineRow With {
                .Id = l.Id, .Product = name.GetValueOrDefault(l.ProductId, l.Description),
                .Quantity = l.Quantity, .UnitPrice = l.UnitPrice, .TaxRate = l.TaxRate, .LineTotal = l.LineTotal
            }).ToList()

            lblTotals.Text = $"Subtotal {t.Subtotal:N2}     Tax {t.TaxTotal:N2}     Total {t.Total:N2}"
            _status = t.Status
        End Sub

        Private Sub UpdateEnabled()
            Dim isDraft = _status = TransactionStatus.Draft
            Dim started = _txnId IsNot Nothing

            btnStart.Visible = Not started
            cboParty.Enabled = Not started AndAlso cboParty.Enabled
            txtWalkIn.Enabled = Not started
            ' Fixed once the draft exists: it is the location Confirm will move stock at, and the
            ' lines already added were priced and checked against it.
            cboShop.Enabled = Not started
            dtpDate.Enabled = Not started
            txtNotes.Enabled = Not started

            For Each c As Control In New Control() {cboProduct, numQty, numPrice, numTax, btnAddLine, btnRemoveLine}
                c.Enabled = started AndAlso isDraft
            Next
            btnConfirm.Enabled = started AndAlso isDraft
            ' Cancellable while it is a draft and after it is confirmed - only an already
            ' cancelled one has nothing left to do.
            btnCancelTxn.Enabled = started AndAlso _status <> TransactionStatus.Cancelled
        End Sub

        Private NotInheritable Class LineRow
            Public Property Id As Integer
            Public Property Product As String
            Public Property Quantity As Decimal
            Public Property UnitPrice As Decimal
            Public Property TaxRate As Decimal
            Public Property LineTotal As Decimal
        End Class
    End Class

End Namespace
