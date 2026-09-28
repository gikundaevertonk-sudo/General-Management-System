Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class TransactionEditForm
    Inherits System.Windows.Forms.Form

    <System.Diagnostics.DebuggerNonUserCode()>
    Protected Overrides Sub Dispose(disposing As Boolean)
        Try
            If disposing AndAlso components IsNot Nothing Then
                components.Dispose()
            End If
        Finally
            MyBase.Dispose(disposing)
        End Try
    End Sub

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.lblParty = New System.Windows.Forms.Label()
        Me.cboParty = New System.Windows.Forms.ComboBox()
        Me.txtWalkIn = New System.Windows.Forms.TextBox()
        Me.lblDate = New System.Windows.Forms.Label()
        Me.dtpDate = New System.Windows.Forms.DateTimePicker()
        Me.lblShop = New System.Windows.Forms.Label()
        Me.cboShop = New System.Windows.Forms.ComboBox()
        Me.lblNotes = New System.Windows.Forms.Label()
        Me.txtNotes = New System.Windows.Forms.TextBox()
        Me.btnStart = New System.Windows.Forms.Button()
        Me.cboProduct = New System.Windows.Forms.ComboBox()
        Me.numQty = New System.Windows.Forms.NumericUpDown()
        Me.numPrice = New System.Windows.Forms.NumericUpDown()
        Me.numTax = New System.Windows.Forms.NumericUpDown()
        Me.lblLineHint = New System.Windows.Forms.Label()
        Me.btnAddLine = New System.Windows.Forms.Button()
        Me.btnRemoveLine = New System.Windows.Forms.Button()
        Me.grdLines = New System.Windows.Forms.DataGridView()
        Me.colProduct = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colQuantity = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colUnitPrice = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colTaxRate = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.colLineTotal = New System.Windows.Forms.DataGridViewTextBoxColumn()
        Me.lblTotals = New System.Windows.Forms.Label()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnConfirm = New System.Windows.Forms.Button()
        Me.btnCancelTxn = New System.Windows.Forms.Button()
        Me.btnClose = New System.Windows.Forms.Button()
        CType(Me.numQty, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numPrice, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numTax, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.grdLines, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        Me.lblParty.AutoSize = True
        Me.lblParty.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblParty.Location = New System.Drawing.Point(16, 18)
        Me.lblParty.Name = "lblParty"
        Me.lblParty.Size = New System.Drawing.Size(35, 15)
        Me.lblParty.TabIndex = 0
        Me.lblParty.Text = "Party"
        '
        Me.cboParty.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboParty.Location = New System.Drawing.Point(120, 15)
        Me.cboParty.Name = "cboParty"
        Me.cboParty.Size = New System.Drawing.Size(320, 23)
        Me.cboParty.TabIndex = 1
        '
        ' Sits beside the party list rather than on its own row: the form is fixed-layout and
        ' a new row would mean shifting every control below it.
        Me.txtWalkIn.Location = New System.Drawing.Point(450, 15)
        Me.txtWalkIn.Name = "txtWalkIn"
        Me.txtWalkIn.Size = New System.Drawing.Size(150, 23)
        Me.txtWalkIn.TabIndex = 2
        Me.txtWalkIn.PlaceholderText = "name (optional)"
        '
        Me.lblDate.AutoSize = True
        Me.lblDate.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblDate.Location = New System.Drawing.Point(16, 52)
        Me.lblDate.Name = "lblDate"
        Me.lblDate.Size = New System.Drawing.Size(31, 15)
        Me.lblDate.TabIndex = 2
        Me.lblDate.Text = "Date"
        '
        Me.dtpDate.Format = System.Windows.Forms.DateTimePickerFormat.Short
        Me.dtpDate.Location = New System.Drawing.Point(120, 49)
        Me.dtpDate.Name = "dtpDate"
        Me.dtpDate.Size = New System.Drawing.Size(160, 23)
        Me.dtpDate.TabIndex = 3
        '
        ' Shares the date's row for the same reason txtWalkIn shares the party's: this form is
        ' fixed-layout, so a row of its own would mean moving everything below it.
        Me.lblShop.AutoSize = True
        Me.lblShop.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblShop.Location = New System.Drawing.Point(300, 52)
        Me.lblShop.Name = "lblShop"
        Me.lblShop.Size = New System.Drawing.Size(34, 15)
        Me.lblShop.TabIndex = 20
        Me.lblShop.Text = "Shop"
        '
        Me.cboShop.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboShop.Location = New System.Drawing.Point(350, 49)
        Me.cboShop.Name = "cboShop"
        Me.cboShop.Size = New System.Drawing.Size(250, 23)
        Me.cboShop.TabIndex = 21
        '
        Me.lblNotes.AutoSize = True
        Me.lblNotes.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNotes.Location = New System.Drawing.Point(16, 86)
        Me.lblNotes.Name = "lblNotes"
        Me.lblNotes.Size = New System.Drawing.Size(38, 15)
        Me.lblNotes.TabIndex = 4
        Me.lblNotes.Text = "Notes"
        '
        Me.txtNotes.Location = New System.Drawing.Point(120, 83)
        Me.txtNotes.Name = "txtNotes"
        Me.txtNotes.Size = New System.Drawing.Size(480, 23)
        Me.txtNotes.TabIndex = 5
        '
        Me.btnStart.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnStart.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnStart.ForeColor = System.Drawing.Color.White
        Me.btnStart.Location = New System.Drawing.Point(620, 13)
        Me.btnStart.Name = "btnStart"
        Me.btnStart.Size = New System.Drawing.Size(140, 28)
        Me.btnStart.TabIndex = 6
        Me.btnStart.Text = "Start transaction"
        Me.btnStart.UseVisualStyleBackColor = False
        '
        Me.cboProduct.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboProduct.Location = New System.Drawing.Point(16, 130)
        Me.cboProduct.Name = "cboProduct"
        Me.cboProduct.Size = New System.Drawing.Size(260, 23)
        Me.cboProduct.TabIndex = 7
        '
        Me.numQty.DecimalPlaces = 3
        Me.numQty.Location = New System.Drawing.Point(284, 130)
        Me.numQty.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numQty.Name = "numQty"
        Me.numQty.Size = New System.Drawing.Size(80, 23)
        Me.numQty.TabIndex = 8
        Me.numQty.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        Me.numPrice.DecimalPlaces = 2
        Me.numPrice.Location = New System.Drawing.Point(372, 130)
        Me.numPrice.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numPrice.Name = "numPrice"
        Me.numPrice.Size = New System.Drawing.Size(90, 23)
        Me.numPrice.TabIndex = 9
        '
        Me.numTax.DecimalPlaces = 2
        Me.numTax.Location = New System.Drawing.Point(470, 130)
        Me.numTax.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numTax.Name = "numTax"
        Me.numTax.Size = New System.Drawing.Size(70, 23)
        Me.numTax.TabIndex = 10
        '
        Me.lblLineHint.AutoSize = True
        Me.lblLineHint.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblLineHint.Location = New System.Drawing.Point(284, 112)
        Me.lblLineHint.Name = "lblLineHint"
        Me.lblLineHint.Size = New System.Drawing.Size(120, 15)
        Me.lblLineHint.TabIndex = 11
        Me.lblLineHint.Text = "qty  /  price  /  tax %"
        '
        Me.btnAddLine.Location = New System.Drawing.Point(550, 128)
        Me.btnAddLine.Name = "btnAddLine"
        Me.btnAddLine.Size = New System.Drawing.Size(90, 27)
        Me.btnAddLine.TabIndex = 12
        Me.btnAddLine.Text = "Add line"
        Me.btnAddLine.UseVisualStyleBackColor = True
        '
        Me.btnRemoveLine.Location = New System.Drawing.Point(646, 128)
        Me.btnRemoveLine.Name = "btnRemoveLine"
        Me.btnRemoveLine.Size = New System.Drawing.Size(110, 27)
        Me.btnRemoveLine.TabIndex = 13
        Me.btnRemoveLine.Text = "Remove line"
        Me.btnRemoveLine.UseVisualStyleBackColor = True
        '
        Me.grdLines.AllowUserToAddRows = False
        Me.grdLines.AllowUserToDeleteRows = False
        Me.grdLines.Anchor = CType((((System.Windows.Forms.AnchorStyles.Top Or System.Windows.Forms.AnchorStyles.Bottom) _
            Or System.Windows.Forms.AnchorStyles.Left) Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.grdLines.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill
        Me.grdLines.BackgroundColor = System.Drawing.Color.White
        Me.grdLines.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize
        Me.grdLines.Columns.AddRange(New System.Windows.Forms.DataGridViewColumn() {Me.colProduct, Me.colQuantity, Me.colUnitPrice, Me.colTaxRate, Me.colLineTotal})
        Me.grdLines.Location = New System.Drawing.Point(16, 168)
        Me.grdLines.MultiSelect = False
        Me.grdLines.Name = "grdLines"
        Me.grdLines.ReadOnly = True
        Me.grdLines.RowHeadersVisible = False
        Me.grdLines.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect
        Me.grdLines.Size = New System.Drawing.Size(748, 372)
        Me.grdLines.TabIndex = 14
        '
        Me.colProduct.DataPropertyName = "Product"
        Me.colProduct.HeaderText = "Product"
        Me.colProduct.Name = "colProduct"
        Me.colProduct.ReadOnly = True
        '
        Me.colQuantity.DataPropertyName = "Quantity"
        Me.colQuantity.HeaderText = "Qty"
        Me.colQuantity.Name = "colQuantity"
        Me.colQuantity.ReadOnly = True
        '
        Me.colUnitPrice.DataPropertyName = "UnitPrice"
        Me.colUnitPrice.HeaderText = "Unit price"
        Me.colUnitPrice.Name = "colUnitPrice"
        Me.colUnitPrice.ReadOnly = True
        '
        Me.colTaxRate.DataPropertyName = "TaxRate"
        Me.colTaxRate.HeaderText = "Tax %"
        Me.colTaxRate.Name = "colTaxRate"
        Me.colTaxRate.ReadOnly = True
        '
        Me.colLineTotal.DataPropertyName = "LineTotal"
        Me.colLineTotal.HeaderText = "Line total"
        Me.colLineTotal.Name = "colLineTotal"
        Me.colLineTotal.ReadOnly = True
        '
        Me.lblTotals.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lblTotals.Font = New System.Drawing.Font("Segoe UI Semibold", 10.0!)
        Me.lblTotals.Location = New System.Drawing.Point(16, 552)
        Me.lblTotals.Name = "lblTotals"
        Me.lblTotals.Size = New System.Drawing.Size(500, 24)
        Me.lblTotals.TabIndex = 15
        '
        Me.lblError.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Left), System.Windows.Forms.AnchorStyles)
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(16, 580)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(520, 20)
        Me.lblError.TabIndex = 16
        '
        Me.btnConfirm.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnConfirm.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnConfirm.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnConfirm.ForeColor = System.Drawing.Color.White
        Me.btnConfirm.Location = New System.Drawing.Point(560, 576)
        Me.btnConfirm.Name = "btnConfirm"
        Me.btnConfirm.Size = New System.Drawing.Size(100, 34)
        Me.btnConfirm.TabIndex = 17
        Me.btnConfirm.Text = "Confirm"
        Me.btnConfirm.UseVisualStyleBackColor = False
        '
        Me.btnCancelTxn.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnCancelTxn.Location = New System.Drawing.Point(446, 576)
        Me.btnCancelTxn.Name = "btnCancelTxn"
        Me.btnCancelTxn.Size = New System.Drawing.Size(100, 34)
        Me.btnCancelTxn.TabIndex = 19
        Me.btnCancelTxn.Text = "Cancel sale"
        Me.btnCancelTxn.UseVisualStyleBackColor = True
        '
        Me.btnClose.Anchor = CType((System.Windows.Forms.AnchorStyles.Bottom Or System.Windows.Forms.AnchorStyles.Right), System.Windows.Forms.AnchorStyles)
        Me.btnClose.Location = New System.Drawing.Point(668, 576)
        Me.btnClose.Name = "btnClose"
        Me.btnClose.Size = New System.Drawing.Size(90, 34)
        Me.btnClose.TabIndex = 18
        Me.btnClose.Text = "Close"
        Me.btnClose.UseVisualStyleBackColor = True
        '
        'TransactionEditForm
        '
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(780, 624)
        Me.Controls.Add(Me.btnClose)
        Me.Controls.Add(Me.btnCancelTxn)
        Me.Controls.Add(Me.btnConfirm)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.lblTotals)
        Me.Controls.Add(Me.grdLines)
        Me.Controls.Add(Me.btnRemoveLine)
        Me.Controls.Add(Me.btnAddLine)
        Me.Controls.Add(Me.lblLineHint)
        Me.Controls.Add(Me.numTax)
        Me.Controls.Add(Me.numPrice)
        Me.Controls.Add(Me.numQty)
        Me.Controls.Add(Me.cboProduct)
        Me.Controls.Add(Me.btnStart)
        Me.Controls.Add(Me.txtNotes)
        Me.Controls.Add(Me.lblNotes)
        Me.Controls.Add(Me.cboShop)
        Me.Controls.Add(Me.lblShop)
        Me.Controls.Add(Me.dtpDate)
        Me.Controls.Add(Me.lblDate)
        Me.Controls.Add(Me.txtWalkIn)
        Me.Controls.Add(Me.cboParty)
        Me.Controls.Add(Me.lblParty)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.MinimumSize = New System.Drawing.Size(720, 560)
        Me.Name = "TransactionEditForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Transaction"
        CType(Me.numQty, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numPrice, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numTax, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.grdLines, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblParty As System.Windows.Forms.Label
    Friend WithEvents cboParty As System.Windows.Forms.ComboBox
    Friend WithEvents txtWalkIn As System.Windows.Forms.TextBox
    Friend WithEvents lblDate As System.Windows.Forms.Label
    Friend WithEvents dtpDate As System.Windows.Forms.DateTimePicker
    Friend WithEvents lblShop As System.Windows.Forms.Label
    Friend WithEvents cboShop As System.Windows.Forms.ComboBox
    Friend WithEvents lblNotes As System.Windows.Forms.Label
    Friend WithEvents txtNotes As System.Windows.Forms.TextBox
    Friend WithEvents btnStart As System.Windows.Forms.Button
    Friend WithEvents cboProduct As System.Windows.Forms.ComboBox
    Friend WithEvents numQty As System.Windows.Forms.NumericUpDown
    Friend WithEvents numPrice As System.Windows.Forms.NumericUpDown
    Friend WithEvents numTax As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblLineHint As System.Windows.Forms.Label
    Friend WithEvents btnAddLine As System.Windows.Forms.Button
    Friend WithEvents btnRemoveLine As System.Windows.Forms.Button
    Friend WithEvents grdLines As System.Windows.Forms.DataGridView
    Friend WithEvents colProduct As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents colQuantity As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents colUnitPrice As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents colTaxRate As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents colLineTotal As System.Windows.Forms.DataGridViewTextBoxColumn
    Friend WithEvents lblTotals As System.Windows.Forms.Label
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnConfirm As System.Windows.Forms.Button
    ''' <summary>Cancels the transaction itself. Not to be confused with btnClose, which
    ''' only shuts the window - the two sat one word apart and needed different names.</summary>
    Friend WithEvents btnCancelTxn As System.Windows.Forms.Button
    Friend WithEvents btnClose As System.Windows.Forms.Button
End Class

End Namespace
