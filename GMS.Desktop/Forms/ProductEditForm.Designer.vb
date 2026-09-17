Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ProductEditForm
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
        Me.lblSku = New System.Windows.Forms.Label()
        Me.txtSku = New System.Windows.Forms.TextBox()
        Me.lblName = New System.Windows.Forms.Label()
        Me.txtName = New System.Windows.Forms.TextBox()
        Me.lblDesc = New System.Windows.Forms.Label()
        Me.txtDesc = New System.Windows.Forms.TextBox()
        Me.lblCategory = New System.Windows.Forms.Label()
        Me.cboCategory = New System.Windows.Forms.ComboBox()
        Me.lblPrice = New System.Windows.Forms.Label()
        Me.numPrice = New System.Windows.Forms.NumericUpDown()
        Me.lblCost = New System.Windows.Forms.Label()
        Me.numCost = New System.Windows.Forms.NumericUpDown()
        Me.lblUom = New System.Windows.Forms.Label()
        Me.txtUom = New System.Windows.Forms.TextBox()
        Me.lblReorder = New System.Windows.Forms.Label()
        Me.numReorder = New System.Windows.Forms.NumericUpDown()
        Me.chkActive = New System.Windows.Forms.CheckBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        CType(Me.numPrice, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numCost, System.ComponentModel.ISupportInitialize).BeginInit()
        CType(Me.numReorder, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        Me.lblSku.AutoSize = True
        Me.lblSku.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblSku.Location = New System.Drawing.Point(20, 21)
        Me.lblSku.Name = "lblSku"
        Me.lblSku.Size = New System.Drawing.Size(30, 15)
        Me.lblSku.TabIndex = 0
        Me.lblSku.Text = "SKU"
        '
        Me.txtSku.Location = New System.Drawing.Point(140, 18)
        Me.txtSku.Name = "txtSku"
        Me.txtSku.Size = New System.Drawing.Size(280, 23)
        Me.txtSku.TabIndex = 1
        '
        Me.lblName.AutoSize = True
        Me.lblName.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblName.Location = New System.Drawing.Point(20, 57)
        Me.lblName.Name = "lblName"
        Me.lblName.Size = New System.Drawing.Size(39, 15)
        Me.lblName.TabIndex = 2
        Me.lblName.Text = "Name"
        '
        Me.txtName.Location = New System.Drawing.Point(140, 54)
        Me.txtName.Name = "txtName"
        Me.txtName.Size = New System.Drawing.Size(280, 23)
        Me.txtName.TabIndex = 3
        '
        Me.lblDesc.AutoSize = True
        Me.lblDesc.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblDesc.Location = New System.Drawing.Point(20, 93)
        Me.lblDesc.Name = "lblDesc"
        Me.lblDesc.Size = New System.Drawing.Size(67, 15)
        Me.lblDesc.TabIndex = 4
        Me.lblDesc.Text = "Description"
        '
        Me.txtDesc.Location = New System.Drawing.Point(140, 90)
        Me.txtDesc.Name = "txtDesc"
        Me.txtDesc.Size = New System.Drawing.Size(280, 23)
        Me.txtDesc.TabIndex = 5
        '
        Me.lblCategory.AutoSize = True
        Me.lblCategory.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblCategory.Location = New System.Drawing.Point(20, 129)
        Me.lblCategory.Name = "lblCategory"
        Me.lblCategory.Size = New System.Drawing.Size(55, 15)
        Me.lblCategory.TabIndex = 6
        Me.lblCategory.Text = "Category"
        '
        Me.cboCategory.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboCategory.Location = New System.Drawing.Point(140, 126)
        Me.cboCategory.Name = "cboCategory"
        Me.cboCategory.Size = New System.Drawing.Size(280, 23)
        Me.cboCategory.TabIndex = 7
        '
        Me.lblPrice.AutoSize = True
        Me.lblPrice.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblPrice.Location = New System.Drawing.Point(20, 165)
        Me.lblPrice.Name = "lblPrice"
        Me.lblPrice.Size = New System.Drawing.Size(58, 15)
        Me.lblPrice.TabIndex = 8
        Me.lblPrice.Text = "Unit price"
        '
        Me.numPrice.DecimalPlaces = 2
        Me.numPrice.Location = New System.Drawing.Point(140, 162)
        Me.numPrice.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numPrice.Name = "numPrice"
        Me.numPrice.Size = New System.Drawing.Size(280, 23)
        Me.numPrice.TabIndex = 9
        Me.numPrice.ThousandsSeparator = True
        '
        Me.lblCost.AutoSize = True
        Me.lblCost.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblCost.Location = New System.Drawing.Point(20, 201)
        Me.lblCost.Name = "lblCost"
        Me.lblCost.Size = New System.Drawing.Size(58, 15)
        Me.lblCost.TabIndex = 10
        Me.lblCost.Text = "Cost price"
        '
        Me.numCost.DecimalPlaces = 2
        Me.numCost.Location = New System.Drawing.Point(140, 198)
        Me.numCost.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numCost.Name = "numCost"
        Me.numCost.Size = New System.Drawing.Size(280, 23)
        Me.numCost.TabIndex = 11
        Me.numCost.ThousandsSeparator = True
        '
        Me.lblUom.AutoSize = True
        Me.lblUom.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblUom.Location = New System.Drawing.Point(20, 237)
        Me.lblUom.Name = "lblUom"
        Me.lblUom.Size = New System.Drawing.Size(96, 15)
        Me.lblUom.TabIndex = 12
        Me.lblUom.Text = "Unit of measure"
        '
        Me.txtUom.Location = New System.Drawing.Point(140, 234)
        Me.txtUom.Name = "txtUom"
        Me.txtUom.Size = New System.Drawing.Size(280, 23)
        Me.txtUom.TabIndex = 13
        '
        Me.lblReorder.AutoSize = True
        Me.lblReorder.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblReorder.Location = New System.Drawing.Point(20, 273)
        Me.lblReorder.Name = "lblReorder"
        Me.lblReorder.Size = New System.Drawing.Size(78, 15)
        Me.lblReorder.TabIndex = 14
        Me.lblReorder.Text = "Reorder level"
        '
        Me.numReorder.DecimalPlaces = 3
        Me.numReorder.Location = New System.Drawing.Point(140, 270)
        Me.numReorder.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numReorder.Name = "numReorder"
        Me.numReorder.Size = New System.Drawing.Size(280, 23)
        Me.numReorder.TabIndex = 15
        Me.numReorder.ThousandsSeparator = True
        '
        Me.chkActive.AutoSize = True
        Me.chkActive.Checked = True
        Me.chkActive.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkActive.Location = New System.Drawing.Point(140, 306)
        Me.chkActive.Name = "chkActive"
        Me.chkActive.Size = New System.Drawing.Size(59, 19)
        Me.chkActive.TabIndex = 16
        Me.chkActive.Text = "Active"
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 336)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(400, 32)
        Me.lblError.TabIndex = 17
        '
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSave.ForeColor = System.Drawing.Color.White
        Me.btnSave.Location = New System.Drawing.Point(140, 376)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(160, 34)
        Me.btnSave.TabIndex = 18
        Me.btnSave.Text = "Save"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        Me.btnCancel.Location = New System.Drawing.Point(310, 376)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(110, 34)
        Me.btnCancel.TabIndex = 19
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'ProductEditForm
        '
        Me.AcceptButton = Me.btnSave
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(440, 430)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.chkActive)
        Me.Controls.Add(Me.numReorder)
        Me.Controls.Add(Me.lblReorder)
        Me.Controls.Add(Me.txtUom)
        Me.Controls.Add(Me.lblUom)
        Me.Controls.Add(Me.numCost)
        Me.Controls.Add(Me.lblCost)
        Me.Controls.Add(Me.numPrice)
        Me.Controls.Add(Me.lblPrice)
        Me.Controls.Add(Me.cboCategory)
        Me.Controls.Add(Me.lblCategory)
        Me.Controls.Add(Me.txtDesc)
        Me.Controls.Add(Me.lblDesc)
        Me.Controls.Add(Me.txtName)
        Me.Controls.Add(Me.lblName)
        Me.Controls.Add(Me.txtSku)
        Me.Controls.Add(Me.lblSku)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "ProductEditForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Product"
        CType(Me.numPrice, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numCost, System.ComponentModel.ISupportInitialize).EndInit()
        CType(Me.numReorder, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblSku As System.Windows.Forms.Label
    Friend WithEvents txtSku As System.Windows.Forms.TextBox
    Friend WithEvents lblName As System.Windows.Forms.Label
    Friend WithEvents txtName As System.Windows.Forms.TextBox
    Friend WithEvents lblDesc As System.Windows.Forms.Label
    Friend WithEvents txtDesc As System.Windows.Forms.TextBox
    Friend WithEvents lblCategory As System.Windows.Forms.Label
    Friend WithEvents cboCategory As System.Windows.Forms.ComboBox
    Friend WithEvents lblPrice As System.Windows.Forms.Label
    Friend WithEvents numPrice As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblCost As System.Windows.Forms.Label
    Friend WithEvents numCost As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblUom As System.Windows.Forms.Label
    Friend WithEvents txtUom As System.Windows.Forms.TextBox
    Friend WithEvents lblReorder As System.Windows.Forms.Label
    Friend WithEvents numReorder As System.Windows.Forms.NumericUpDown
    Friend WithEvents chkActive As System.Windows.Forms.CheckBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnSave As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
