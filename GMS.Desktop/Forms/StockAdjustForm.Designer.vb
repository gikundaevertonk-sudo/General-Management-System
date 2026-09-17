Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class StockAdjustForm
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
        Me.lblProduct = New System.Windows.Forms.Label()
        Me.lblDirection = New System.Windows.Forms.Label()
        Me.cboDirection = New System.Windows.Forms.ComboBox()
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
        'lblProduct
        '
        Me.lblProduct.AutoSize = True
        Me.lblProduct.Font = New System.Drawing.Font("Segoe UI Semibold", 11.0!)
        Me.lblProduct.Location = New System.Drawing.Point(20, 16)
        Me.lblProduct.Name = "lblProduct"
        Me.lblProduct.Size = New System.Drawing.Size(62, 20)
        Me.lblProduct.TabIndex = 0
        Me.lblProduct.Text = "Product"
        '
        'lblDirection
        '
        Me.lblDirection.AutoSize = True
        Me.lblDirection.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblDirection.Location = New System.Drawing.Point(20, 58)
        Me.lblDirection.Name = "lblDirection"
        Me.lblDirection.Size = New System.Drawing.Size(58, 15)
        Me.lblDirection.TabIndex = 1
        Me.lblDirection.Text = "Direction"
        '
        'cboDirection
        '
        Me.cboDirection.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboDirection.Items.AddRange(New Object() {"Increase (stock in)", "Decrease (stock out)"})
        Me.cboDirection.Location = New System.Drawing.Point(140, 54)
        Me.cboDirection.Name = "cboDirection"
        Me.cboDirection.Size = New System.Drawing.Size(230, 23)
        Me.cboDirection.TabIndex = 2
        '
        'lblQty
        '
        Me.lblQty.AutoSize = True
        Me.lblQty.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblQty.Location = New System.Drawing.Point(20, 92)
        Me.lblQty.Name = "lblQty"
        Me.lblQty.Size = New System.Drawing.Size(55, 15)
        Me.lblQty.TabIndex = 3
        Me.lblQty.Text = "Quantity"
        '
        'numQty
        '
        Me.numQty.DecimalPlaces = 3
        Me.numQty.Location = New System.Drawing.Point(140, 88)
        Me.numQty.Maximum = New Decimal(New Integer() {1000000, 0, 0, 0})
        Me.numQty.Name = "numQty"
        Me.numQty.Size = New System.Drawing.Size(120, 23)
        Me.numQty.TabIndex = 4
        Me.numQty.Value = New Decimal(New Integer() {1, 0, 0, 0})
        '
        'lblNote
        '
        Me.lblNote.AutoSize = True
        Me.lblNote.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNote.Location = New System.Drawing.Point(20, 126)
        Me.lblNote.Name = "lblNote"
        Me.lblNote.Size = New System.Drawing.Size(82, 15)
        Me.lblNote.TabIndex = 5
        Me.lblNote.Text = "Reason / note"
        '
        'txtNote
        '
        Me.txtNote.Location = New System.Drawing.Point(140, 122)
        Me.txtNote.Name = "txtNote"
        Me.txtNote.Size = New System.Drawing.Size(230, 23)
        Me.txtNote.TabIndex = 6
        '
        'lblError
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 156)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(360, 20)
        Me.lblError.TabIndex = 7
        '
        'btnApply
        '
        Me.btnApply.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnApply.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnApply.ForeColor = System.Drawing.Color.White
        Me.btnApply.Location = New System.Drawing.Point(140, 186)
        Me.btnApply.Name = "btnApply"
        Me.btnApply.Size = New System.Drawing.Size(120, 34)
        Me.btnApply.TabIndex = 8
        Me.btnApply.Text = "Apply"
        Me.btnApply.UseVisualStyleBackColor = False
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(270, 186)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(100, 34)
        Me.btnCancel.TabIndex = 9
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'StockAdjustForm
        '
        Me.AcceptButton = Me.btnApply
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(400, 250)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnApply)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.txtNote)
        Me.Controls.Add(Me.lblNote)
        Me.Controls.Add(Me.numQty)
        Me.Controls.Add(Me.lblQty)
        Me.Controls.Add(Me.cboDirection)
        Me.Controls.Add(Me.lblDirection)
        Me.Controls.Add(Me.lblProduct)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "StockAdjustForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Adjust stock"
        CType(Me.numQty, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblProduct As System.Windows.Forms.Label
    Friend WithEvents lblDirection As System.Windows.Forms.Label
    Friend WithEvents cboDirection As System.Windows.Forms.ComboBox
    Friend WithEvents lblQty As System.Windows.Forms.Label
    Friend WithEvents numQty As System.Windows.Forms.NumericUpDown
    Friend WithEvents lblNote As System.Windows.Forms.Label
    Friend WithEvents txtNote As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnApply As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
