Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class ChangePasswordForm
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
        Me.lblInstruction = New System.Windows.Forms.Label()
        Me.lblCurrent = New System.Windows.Forms.Label()
        Me.txtCurrent = New System.Windows.Forms.TextBox()
        Me.lblNew = New System.Windows.Forms.Label()
        Me.txtNew = New System.Windows.Forms.TextBox()
        Me.lblConfirm = New System.Windows.Forms.Label()
        Me.txtConfirm = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnOk = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblInstruction
        '
        Me.lblInstruction.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblInstruction.Location = New System.Drawing.Point(20, 20)
        Me.lblInstruction.Name = "lblInstruction"
        Me.lblInstruction.Size = New System.Drawing.Size(360, 34)
        Me.lblInstruction.TabIndex = 0
        Me.lblInstruction.Text = "Enter your current and new password."
        '
        'lblCurrent
        '
        Me.lblCurrent.AutoSize = True
        Me.lblCurrent.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblCurrent.Location = New System.Drawing.Point(20, 64)
        Me.lblCurrent.Name = "lblCurrent"
        Me.lblCurrent.Size = New System.Drawing.Size(101, 15)
        Me.lblCurrent.TabIndex = 1
        Me.lblCurrent.Text = "Current password"
        '
        'txtCurrent
        '
        Me.txtCurrent.Location = New System.Drawing.Point(20, 82)
        Me.txtCurrent.Name = "txtCurrent"
        Me.txtCurrent.Size = New System.Drawing.Size(360, 23)
        Me.txtCurrent.TabIndex = 2
        Me.txtCurrent.UseSystemPasswordChar = True
        '
        'lblNew
        '
        Me.lblNew.AutoSize = True
        Me.lblNew.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNew.Location = New System.Drawing.Point(20, 118)
        Me.lblNew.Name = "lblNew"
        Me.lblNew.Size = New System.Drawing.Size(85, 15)
        Me.lblNew.TabIndex = 3
        Me.lblNew.Text = "New password"
        '
        'txtNew
        '
        Me.txtNew.Location = New System.Drawing.Point(20, 136)
        Me.txtNew.Name = "txtNew"
        Me.txtNew.Size = New System.Drawing.Size(360, 23)
        Me.txtNew.TabIndex = 4
        Me.txtNew.UseSystemPasswordChar = True
        '
        'lblConfirm
        '
        Me.lblConfirm.AutoSize = True
        Me.lblConfirm.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblConfirm.Location = New System.Drawing.Point(20, 172)
        Me.lblConfirm.Name = "lblConfirm"
        Me.lblConfirm.Size = New System.Drawing.Size(139, 15)
        Me.lblConfirm.TabIndex = 5
        Me.lblConfirm.Text = "Confirm new password"
        '
        'txtConfirm
        '
        Me.txtConfirm.Location = New System.Drawing.Point(20, 190)
        Me.txtConfirm.Name = "txtConfirm"
        Me.txtConfirm.Size = New System.Drawing.Size(360, 23)
        Me.txtConfirm.TabIndex = 6
        Me.txtConfirm.UseSystemPasswordChar = True
        '
        'lblError
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 220)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(360, 16)
        Me.lblError.TabIndex = 7
        '
        'btnOk
        '
        Me.btnOk.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnOk.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnOk.ForeColor = System.Drawing.Color.White
        Me.btnOk.Location = New System.Drawing.Point(20, 244)
        Me.btnOk.Name = "btnOk"
        Me.btnOk.Size = New System.Drawing.Size(230, 34)
        Me.btnOk.TabIndex = 8
        Me.btnOk.Text = "Update password"
        Me.btnOk.UseVisualStyleBackColor = False
        '
        'btnCancel
        '
        Me.btnCancel.Location = New System.Drawing.Point(260, 244)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(120, 34)
        Me.btnCancel.TabIndex = 9
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'ChangePasswordForm
        '
        Me.AcceptButton = Me.btnOk
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(400, 300)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnOk)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.txtConfirm)
        Me.Controls.Add(Me.lblConfirm)
        Me.Controls.Add(Me.txtNew)
        Me.Controls.Add(Me.lblNew)
        Me.Controls.Add(Me.txtCurrent)
        Me.Controls.Add(Me.lblCurrent)
        Me.Controls.Add(Me.lblInstruction)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "ChangePasswordForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Change password"
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblInstruction As System.Windows.Forms.Label
    Friend WithEvents lblCurrent As System.Windows.Forms.Label
    Friend WithEvents txtCurrent As System.Windows.Forms.TextBox
    Friend WithEvents lblNew As System.Windows.Forms.Label
    Friend WithEvents txtNew As System.Windows.Forms.TextBox
    Friend WithEvents lblConfirm As System.Windows.Forms.Label
    Friend WithEvents txtConfirm As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnOk As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
