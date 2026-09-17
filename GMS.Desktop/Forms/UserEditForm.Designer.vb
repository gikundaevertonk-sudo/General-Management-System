Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class UserEditForm
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
        Me.lblUserName = New System.Windows.Forms.Label()
        Me.txtUserName = New System.Windows.Forms.TextBox()
        Me.lblFullName = New System.Windows.Forms.Label()
        Me.txtFullName = New System.Windows.Forms.TextBox()
        Me.lblEmail = New System.Windows.Forms.Label()
        Me.txtEmail = New System.Windows.Forms.TextBox()
        Me.lblRole = New System.Windows.Forms.Label()
        Me.cboRole = New System.Windows.Forms.ComboBox()
        Me.lblTempPassword = New System.Windows.Forms.Label()
        Me.txtTempPassword = New System.Windows.Forms.TextBox()
        Me.chkActive = New System.Windows.Forms.CheckBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        Me.lblUserName.AutoSize = True
        Me.lblUserName.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblUserName.Location = New System.Drawing.Point(20, 21)
        Me.lblUserName.Name = "lblUserName"
        Me.lblUserName.Size = New System.Drawing.Size(60, 15)
        Me.lblUserName.TabIndex = 0
        Me.lblUserName.Text = "Username"
        '
        Me.txtUserName.Location = New System.Drawing.Point(140, 18)
        Me.txtUserName.Name = "txtUserName"
        Me.txtUserName.Size = New System.Drawing.Size(280, 23)
        Me.txtUserName.TabIndex = 1
        '
        Me.lblFullName.AutoSize = True
        Me.lblFullName.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblFullName.Location = New System.Drawing.Point(20, 57)
        Me.lblFullName.Name = "lblFullName"
        Me.lblFullName.Size = New System.Drawing.Size(61, 15)
        Me.lblFullName.TabIndex = 2
        Me.lblFullName.Text = "Full name"
        '
        Me.txtFullName.Location = New System.Drawing.Point(140, 54)
        Me.txtFullName.Name = "txtFullName"
        Me.txtFullName.Size = New System.Drawing.Size(280, 23)
        Me.txtFullName.TabIndex = 3
        '
        Me.lblEmail.AutoSize = True
        Me.lblEmail.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblEmail.Location = New System.Drawing.Point(20, 93)
        Me.lblEmail.Name = "lblEmail"
        Me.lblEmail.Size = New System.Drawing.Size(36, 15)
        Me.lblEmail.TabIndex = 4
        Me.lblEmail.Text = "Email"
        '
        Me.txtEmail.Location = New System.Drawing.Point(140, 90)
        Me.txtEmail.Name = "txtEmail"
        Me.txtEmail.Size = New System.Drawing.Size(280, 23)
        Me.txtEmail.TabIndex = 5
        '
        Me.lblRole.AutoSize = True
        Me.lblRole.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblRole.Location = New System.Drawing.Point(20, 129)
        Me.lblRole.Name = "lblRole"
        Me.lblRole.Size = New System.Drawing.Size(30, 15)
        Me.lblRole.TabIndex = 6
        Me.lblRole.Text = "Role"
        '
        Me.cboRole.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList
        Me.cboRole.Location = New System.Drawing.Point(140, 126)
        Me.cboRole.Name = "cboRole"
        Me.cboRole.Size = New System.Drawing.Size(280, 23)
        Me.cboRole.TabIndex = 7
        '
        Me.lblTempPassword.AutoSize = True
        Me.lblTempPassword.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblTempPassword.Location = New System.Drawing.Point(20, 165)
        Me.lblTempPassword.Name = "lblTempPassword"
        Me.lblTempPassword.Size = New System.Drawing.Size(96, 15)
        Me.lblTempPassword.TabIndex = 8
        Me.lblTempPassword.Text = "Temp. password"
        '
        Me.txtTempPassword.Location = New System.Drawing.Point(140, 162)
        Me.txtTempPassword.Name = "txtTempPassword"
        Me.txtTempPassword.Size = New System.Drawing.Size(280, 23)
        Me.txtTempPassword.TabIndex = 9
        Me.txtTempPassword.UseSystemPasswordChar = True
        '
        Me.chkActive.AutoSize = True
        Me.chkActive.Checked = True
        Me.chkActive.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkActive.Location = New System.Drawing.Point(140, 198)
        Me.chkActive.Name = "chkActive"
        Me.chkActive.Size = New System.Drawing.Size(59, 19)
        Me.chkActive.TabIndex = 10
        Me.chkActive.Text = "Active"
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 228)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(400, 32)
        Me.lblError.TabIndex = 11
        '
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSave.ForeColor = System.Drawing.Color.White
        Me.btnSave.Location = New System.Drawing.Point(140, 268)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(160, 34)
        Me.btnSave.TabIndex = 12
        Me.btnSave.Text = "Save"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        Me.btnCancel.Location = New System.Drawing.Point(310, 268)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(110, 34)
        Me.btnCancel.TabIndex = 13
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'UserEditForm
        '
        Me.AcceptButton = Me.btnSave
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(440, 330)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.chkActive)
        Me.Controls.Add(Me.txtTempPassword)
        Me.Controls.Add(Me.lblTempPassword)
        Me.Controls.Add(Me.cboRole)
        Me.Controls.Add(Me.lblRole)
        Me.Controls.Add(Me.txtEmail)
        Me.Controls.Add(Me.lblEmail)
        Me.Controls.Add(Me.txtFullName)
        Me.Controls.Add(Me.lblFullName)
        Me.Controls.Add(Me.txtUserName)
        Me.Controls.Add(Me.lblUserName)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "UserEditForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "User"
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblUserName As System.Windows.Forms.Label
    Friend WithEvents txtUserName As System.Windows.Forms.TextBox
    Friend WithEvents lblFullName As System.Windows.Forms.Label
    Friend WithEvents txtFullName As System.Windows.Forms.TextBox
    Friend WithEvents lblEmail As System.Windows.Forms.Label
    Friend WithEvents txtEmail As System.Windows.Forms.TextBox
    Friend WithEvents lblRole As System.Windows.Forms.Label
    Friend WithEvents cboRole As System.Windows.Forms.ComboBox
    Friend WithEvents lblTempPassword As System.Windows.Forms.Label
    Friend WithEvents txtTempPassword As System.Windows.Forms.TextBox
    Friend WithEvents chkActive As System.Windows.Forms.CheckBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnSave As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
