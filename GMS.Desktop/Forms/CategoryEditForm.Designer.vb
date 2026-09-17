Namespace Forms

    <Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
    Partial Class CategoryEditForm
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
            Me.lblName = New System.Windows.Forms.Label()
            Me.txtName = New System.Windows.Forms.TextBox()
            Me.lblDesc = New System.Windows.Forms.Label()
            Me.txtDesc = New System.Windows.Forms.TextBox()
            Me.lblParent = New System.Windows.Forms.Label()
            Me.cboParent = New System.Windows.Forms.ComboBox()
            Me.lblError = New System.Windows.Forms.Label()
            Me.btnSave = New System.Windows.Forms.Button()
            Me.btnCancel = New System.Windows.Forms.Button()
            Me.SuspendLayout()
            '
            Me.lblName.AutoSize = True
            Me.lblName.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
            Me.lblName.Location = New System.Drawing.Point(20, 21)
            Me.lblName.Name = "lblName"
            Me.lblName.Size = New System.Drawing.Size(39, 15)
            Me.lblName.TabIndex = 0
            Me.lblName.Text = "Name"
            '
            Me.txtName.Location = New System.Drawing.Point(140, 18)
            Me.txtName.Name = "txtName"
            Me.txtName.Size = New System.Drawing.Size(260, 23)
            Me.txtName.TabIndex = 1
            '
            Me.lblDesc.AutoSize = True
            Me.lblDesc.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
            Me.lblDesc.Location = New System.Drawing.Point(20, 57)
            Me.lblDesc.Name = "lblDesc"
            Me.lblDesc.Size = New System.Drawing.Size(67, 15)
            Me.lblDesc.TabIndex = 2
            Me.lblDesc.Text = "Description"
            '
            Me.txtDesc.Location = New System.Drawing.Point(140, 54)
            Me.txtDesc.Name = "txtDesc"
            Me.txtDesc.Size = New System.Drawing.Size(260, 23)
            Me.txtDesc.TabIndex = 3
            '
            Me.lblParent.AutoSize = True
            Me.lblParent.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
            Me.lblParent.Location = New System.Drawing.Point(20, 93)
            Me.lblParent.Name = "lblParent"
            Me.lblParent.Size = New System.Drawing.Size(90, 15)
            Me.lblParent.TabIndex = 4
            Me.lblParent.Text = "Parent category"
            '
            Me.cboParent.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
            Me.cboParent.Location = New System.Drawing.Point(140, 90)
            Me.cboParent.Name = "cboParent"
            Me.cboParent.Size = New System.Drawing.Size(260, 23)
            Me.cboParent.TabIndex = 5
            '
            Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
            Me.lblError.Location = New System.Drawing.Point(20, 124)
            Me.lblError.Name = "lblError"
            Me.lblError.Size = New System.Drawing.Size(380, 32)
            Me.lblError.TabIndex = 6
            '
            Me.btnSave.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
            Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
            Me.btnSave.ForeColor = System.Drawing.Color.White
            Me.btnSave.Location = New System.Drawing.Point(140, 164)
            Me.btnSave.Name = "btnSave"
            Me.btnSave.Size = New System.Drawing.Size(140, 34)
            Me.btnSave.TabIndex = 7
            Me.btnSave.Text = "Save"
            Me.btnSave.UseVisualStyleBackColor = False
            '
            Me.btnCancel.Location = New System.Drawing.Point(290, 164)
            Me.btnCancel.Name = "btnCancel"
            Me.btnCancel.Size = New System.Drawing.Size(110, 34)
            Me.btnCancel.TabIndex = 8
            Me.btnCancel.Text = "Cancel"
            Me.btnCancel.UseVisualStyleBackColor = True
            '
            'CategoryEditForm
            '
            Me.AcceptButton = Me.btnSave
            Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
            Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
            Me.BackColor = System.Drawing.Color.White
            Me.CancelButton = Me.btnCancel
            Me.ClientSize = New System.Drawing.Size(420, 220)
            Me.Controls.Add(Me.btnCancel)
            Me.Controls.Add(Me.btnSave)
            Me.Controls.Add(Me.lblError)
            Me.Controls.Add(Me.cboParent)
            Me.Controls.Add(Me.lblParent)
            Me.Controls.Add(Me.txtDesc)
            Me.Controls.Add(Me.lblDesc)
            Me.Controls.Add(Me.txtName)
            Me.Controls.Add(Me.lblName)
            Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
            Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
            Me.MaximizeBox = False
            Me.MinimizeBox = False
            Me.Name = "CategoryEditForm"
            Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
            Me.Text = "Category"
            Me.ResumeLayout(False)
            Me.PerformLayout()
        End Sub

        Friend WithEvents lblName As System.Windows.Forms.Label
        Friend WithEvents txtName As System.Windows.Forms.TextBox
        Friend WithEvents lblDesc As System.Windows.Forms.Label
        Friend WithEvents txtDesc As System.Windows.Forms.TextBox
        Friend WithEvents lblParent As System.Windows.Forms.Label
        Friend WithEvents cboParent As System.Windows.Forms.ComboBox
        Friend WithEvents lblError As System.Windows.Forms.Label
        Friend WithEvents btnSave As System.Windows.Forms.Button
        Friend WithEvents btnCancel As System.Windows.Forms.Button
    End Class

End Namespace
