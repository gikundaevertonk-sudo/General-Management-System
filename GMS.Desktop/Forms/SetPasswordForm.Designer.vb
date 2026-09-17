Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class SetPasswordForm
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
        Me.lblNew = New System.Windows.Forms.Label()
        Me.txtNew = New System.Windows.Forms.TextBox()
        Me.lblConfirm = New System.Windows.Forms.Label()
        Me.txtConfirm = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnOk = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        Me.lblNew.AutoSize = True
        Me.lblNew.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNew.Location = New System.Drawing.Point(20, 24)
        Me.lblNew.Name = "lblNew"
        Me.lblNew.Size = New System.Drawing.Size(85, 15)
        Me.lblNew.TabIndex = 0
        Me.lblNew.Text = "New password"
        '
        Me.txtNew.Location = New System.Drawing.Point(150, 20)
        Me.txtNew.Name = "txtNew"
        Me.txtNew.Size = New System.Drawing.Size(210, 23)
        Me.txtNew.TabIndex = 1
        Me.txtNew.UseSystemPasswordChar = True
        '
        Me.lblConfirm.AutoSize = True
        Me.lblConfirm.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblConfirm.Location = New System.Drawing.Point(20, 60)
        Me.lblConfirm.Name = "lblConfirm"
        Me.lblConfirm.Size = New System.Drawing.Size(49, 15)
        Me.lblConfirm.TabIndex = 2
        Me.lblConfirm.Text = "Confirm"
        '
        Me.txtConfirm.Location = New System.Drawing.Point(150, 56)
        Me.txtConfirm.Name = "txtConfirm"
        Me.txtConfirm.Size = New System.Drawing.Size(210, 23)
        Me.txtConfirm.TabIndex = 3
        Me.txtConfirm.UseSystemPasswordChar = True
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 92)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(340, 20)
        Me.lblError.TabIndex = 4
        '
        Me.btnOk.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnOk.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOk.ForeColor = System.Drawing.Color.White
        Me.btnOk.Location = New System.Drawing.Point(150, 130)
        Me.btnOk.Name = "btnOk"
        Me.btnOk.Size = New System.Drawing.Size(110, 34)
        Me.btnOk.TabIndex = 5
        Me.btnOk.Text = "OK"
        Me.btnOk.UseVisualStyleBackColor = False
        '
        Me.btnCancel.Location = New System.Drawing.Point(270, 130)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(90, 34)
        Me.btnCancel.TabIndex = 6
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'SetPasswordForm
        '
        Me.AcceptButton = Me.btnOk
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(380, 190)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnOk)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.txtConfirm)
        Me.Controls.Add(Me.lblConfirm)
        Me.Controls.Add(Me.txtNew)
        Me.Controls.Add(Me.lblNew)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "SetPasswordForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Set temporary password"
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblNew As System.Windows.Forms.Label
    Friend WithEvents txtNew As System.Windows.Forms.TextBox
    Friend WithEvents lblConfirm As System.Windows.Forms.Label
    Friend WithEvents txtConfirm As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnOk As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
