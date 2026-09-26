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
            _txnId = transactionId

            LoadProducts()
            If transactionId.HasValue Then LoadExisting(transactionId.Value)
            UpdateEnabled()
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
            If _type = TransactionType.Sale Then
                Dim res = AppHost.Current.Resolve(Of CustomerService)().Search(New QueryOptions With {.PageSize = 1000})
                cboParty.DataSource = If(res.Succeeded,
                    res.Value.Items.Select(Function(c) New With {.Value = c.Id, .Text = c.Name}).ToList(),
                    New List(Of Object)().Select(Function(o) New With {.Value = 0, .Text = ""}).ToList())
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
            If _type = TransactionType.Sale OrElse _type = TransactionType.Purchase Then
                Dim pid = 0
                Integer.TryParse(Convert.ToString(cboParty.SelectedValue), pid)
                If pid = 0 Then
                    lblError.Text = "Choose a party first."
                    Return
                End If
                partyId = pid
            End If

            Dim result = AppHost.Current.Resolve(Of TransactionService)().
                CreateDraft(_type, partyId, dtpDate.Value.Date, txtNotes.Text)
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
            dtpDate.Enabled = Not started
            txtNotes.Enabled = Not started

            For Each c As Control In New Control() {cboProduct, numQty, numPrice, numTax, btnAddLine, btnRemoveLine}
                c.Enabled = started AndAlso isDraft
            Next
            btnConfirm.Enabled = started AndAlso isDraft
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
