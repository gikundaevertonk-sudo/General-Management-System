Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class StockAllocateForm
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
        Me.lblFrom = New System.Windows.Forms.Label()
        Me.lblProduct = New System.Windows.Forms.Label()
        Me.cboProduct = New System.Windows.Forms.ComboBox()
        Me.lblAvailable = New System.Windows.Forms.Label()
        Me.lblTo = New System.Windows.Forms.Label()
        Me.cboTo = New System.Windows.Forms.ComboBox()
        Me.lblQty = New System.Windows.Forms.Label()
        Me.numQty = New System.Windows.Forms.NumericUpDown()
        Me.lblNote = New System.Windows.Forms.Label()
        Me.txtNote = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnApply = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        CType(Me.numQty, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        Me.lblFrom.AutoSize = True
        Me.lblFrom.Font = New System.Drawing.Font("Segoe UI Semibold", 11.0!)
        Me.lblFrom.Location = New System.Drawing.Point(20, 16)
        Me.lblFrom.Name = "lblFrom"
        Me.lblFrom.Size = New System.Drawing.Size(62, 20)
        Me.lblFrom.TabIndex = 0
        Me.lblFrom.Text = "From"
        '
        Me.lblProduct.AutoSize = True
        Me.lblProduct.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblProduct.Location = New System.Drawing.Point(20, 58)
        Me.lblProduct.Name = "lblProduct"
        Me.lblProduct.Size = New System.Drawing.Size(48, 15)
        Me.lblProduct.TabIndex = 1
        Me.lblProduct.Text = "Product"
        '
        Me.cboProduct.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboProduct.Location = New System.Drawing.Point(140, 54)
        Me.cboProduct.Name = "cboProduct"
        Me.cboProduct.Size = New System.Drawing.Size(300, 23)
        Me.cboProduct.TabIndex = 2
        '
        Me.lblAvailable.AutoSize = True
        Me.lblAvailable.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblAvailable.Location = New System.Drawing.Point(140, 82)
        Me.lblAvailable.Name = "lblAvailable"
        Me.lblAvailable.Size = New System.Drawing.Size(300, 15)
        Me.lblAvailable.TabIndex = 3
        '
        Me.lblTo.AutoSize = True
        Me.lblTo.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblTo.Location = New System.Drawing.Point(20, 110)
        Me.lblTo.Name = "lblTo"
        Me.lblTo.Size = New System.Drawing.Size(48, 15)
        Me.lblTo.TabIndex = 4
        Me.lblTo.Text = "Send to"
        '
        Me.cboTo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboTo.Location = New System.Drawing.Point(140, 106)
        Me.cboTo.Name = "cboTo"
        Me.cboTo.Size = New System.Drawing.Size(300, 23)
        Me.cboTo.TabIndex = 5
        '
        Me.lblQty.AutoSize = True
        Me.lblQty.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblQty.Location = New System.Drawing.Point(20, 144)
        Me.lblQty.Name = "lblQty"
        Me.lblQty.Size = New System.Drawing.Size(55, 15)
        Me.lblQty.TabIndex = 6
        Me.lblQty.Text = "Quantity"
        '
        Me.numQty.DecimalPlaces = 3
        Me.numQty.Location = New System.Drawing.Point(140, 140)
        Me.numQty.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numQty.Name = "numQty"
        Me.numQty.Size = New System.Drawing.Size(120, 23)
        Me.numQty.TabIndex = 7
        Me.numQty.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        Me.lblNote.AutoSize = True
        Me.lblNote.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNote.Location = New System.Drawing.Point(20, 178)
        Me.lblNote.Name = "lblNote"
        Me.lblNote.Size = New System.Drawing.Size(32, 15)
        Me.lblNote.TabIndex = 8
        Me.lblNote.Text = "Note"
        '
        Me.txtNote.Location = New System.Drawing.Point(140, 174)
        Me.txtNote.Name = "txtNote"
        Me.txtNote.Size = New System.Drawing.Size(300, 23)
        Me.txtNote.TabIndex = 9
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 206)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(420, 34)
        Me.lblError.TabIndex = 10
        '
        Me.btnApply.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnApply.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnApply.ForeColor = System.Drawing.Color.White
        Me.btnApply.Location = New System.Drawing.Point(140, 248)
        Me.btnApply.Name = "btnApply"
        Me.btnApply.Size = New System.Drawing.Size(180, 34)
        Me.btnApply.TabIndex = 11
        Me.btnApply.Text = "Allocate"
        Me.btnApply.UseVisualStyleBackColor = False
        '
        Me.btnCancel.Location = New System.Drawing.Point(330, 248)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(110, 34)
        Me.btnCancel.TabIndex = 12
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'StockAllocateForm
        '
        Me.AcceptButton = Me.btnApply
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(464, 306)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnApply)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.txtNote)
        Me.Controls.Add(Me.lblNote)
        Me.Controls.Add(Me.numQty)
        Me.Controls.Add(Me.lblQty)
        Me.Controls.Add(Me.cboTo)
        Me.Controls.Add(Me.lblTo)
        Me.Controls.Add(Me.lblAvailable)
        Me.Controls.Add(Me.cboProduct)
        Me.Controls.Add(Me.lblProduct)
        Me.Controls.Add(Me.lblFrom)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "StockAllocateForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Allocate stock"
        CType(Me.numQty, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblFrom As System.Windows.Forms.Label
    Friend WithEvents lblProduct As System.Windows.Forms.Label
    Friend WithEvents cboProduct As System.Windows.Forms.ComboBox
    Friend WithEvents lblAvailable As System.Windows.Forms.Label
    Friend WithEvents lblTo As System.Windows.Forms.Label
    Friend WithEvents cboTo As System.Windows.Forms.ComboBox
    Friend WithEvents lblQty As System.Windows.Forms.Label
    Friend WithEvents numQty As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblNote As System.Windows.Forms.Label
    Friend WithEvents txtNote As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnApply As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
