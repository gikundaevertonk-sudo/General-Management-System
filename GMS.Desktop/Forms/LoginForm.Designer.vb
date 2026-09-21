Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class LoginForm
    Inherits System.Windows.Forms.Form

    'Form overrides dispose to clean up the component list.
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
        Me.lblTitle = New System.Windows.Forms.Label()
        Me.lblSubtitle = New System.Windows.Forms.Label()
        Me.lblOrg = New System.Windows.Forms.Label()
        Me.lblUser = New System.Windows.Forms.Label()
        Me.lblPass = New System.Windows.Forms.Label()
        Me.txtOrg = New System.Windows.Forms.TextBox()
        Me.txtUser = New System.Windows.Forms.TextBox()
        Me.txtPass = New System.Windows.Forms.TextBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnSignIn = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        'lblTitle
        '
        Me.lblTitle.AutoSize = True
        Me.lblTitle.Font = New System.Drawing.Font("Segoe UI Semibold", 18.0!)
        Me.lblTitle.Location = New System.Drawing.Point(24, 22)
        Me.lblTitle.Name = "lblTitle"
        Me.lblTitle.Size = New System.Drawing.Size(84, 32)
        Me.lblTitle.TabIndex = 0
        Me.lblTitle.Text = "Sign in"
        '
        'lblSubtitle
        '
        Me.lblSubtitle.AutoSize = True
        Me.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblSubtitle.Location = New System.Drawing.Point(28, 62)
        Me.lblSubtitle.Name = "lblSubtitle"
        Me.lblSubtitle.Size = New System.Drawing.Size(126, 15)
        Me.lblSubtitle.TabIndex = 1
        Me.lblSubtitle.Text = "Use your GMS account"
        '
        'lblOrg
        '
        Me.lblOrg.AutoSize = True
        Me.lblOrg.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblOrg.Location = New System.Drawing.Point(28, 92)
        Me.lblOrg.Name = "lblOrg"
        Me.lblOrg.Size = New System.Drawing.Size(77, 15)
        Me.lblOrg.TabIndex = 2
        Me.lblOrg.Text = "Organization"
        '
        'txtOrg
        '
        Me.txtOrg.Location = New System.Drawing.Point(28, 110)
        Me.txtOrg.Name = "txtOrg"
        Me.txtOrg.Size = New System.Drawing.Size(320, 23)
        Me.txtOrg.TabIndex = 3
        '
        'lblUser
        '
        Me.lblUser.AutoSize = True
        Me.lblUser.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblUser.Location = New System.Drawing.Point(28, 148)
        Me.lblUser.Name = "lblUser"
        Me.lblUser.Size = New System.Drawing.Size(60, 15)
        Me.lblUser.TabIndex = 4
        Me.lblUser.Text = "Username"
        '
        'lblPass
        '
        Me.lblPass.AutoSize = True
        Me.lblPass.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblPass.Location = New System.Drawing.Point(28, 204)
        Me.lblPass.Name = "lblPass"
        Me.lblPass.Size = New System.Drawing.Size(57, 15)
        Me.lblPass.TabIndex = 6
        Me.lblPass.Text = "Password"
        '
        'txtUser
        '
        Me.txtUser.Location = New System.Drawing.Point(28, 166)
        Me.txtUser.Name = "txtUser"
        Me.txtUser.Size = New System.Drawing.Size(320, 23)
        Me.txtUser.TabIndex = 5
        '
        'txtPass
        '
        Me.txtPass.Location = New System.Drawing.Point(28, 222)
        Me.txtPass.Name = "txtPass"
        Me.txtPass.Size = New System.Drawing.Size(320, 23)
        Me.txtPass.TabIndex = 7
        Me.txtPass.UseSystemPasswordChar = True
        '
        'lblError
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(28, 252)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(320, 34)
        Me.lblError.TabIndex = 8
        '
        'btnSignIn
        '
        Me.btnSignIn.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnSignIn.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSignIn.Font = New System.Drawing.Font("Segoe UI", 9.0!, System.Drawing.FontStyle.Bold)
        Me.btnSignIn.ForeColor = System.Drawing.Color.White
        Me.btnSignIn.Location = New System.Drawing.Point(28, 294)
        Me.btnSignIn.Name = "btnSignIn"
        Me.btnSignIn.Size = New System.Drawing.Size(320, 36)
        Me.btnSignIn.TabIndex = 9
        Me.btnSignIn.Text = "Sign in"
        Me.btnSignIn.UseVisualStyleBackColor = False
        '
        'LoginForm
        '
        Me.AcceptButton = Me.btnSignIn
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.ClientSize = New System.Drawing.Size(380, 356)
        Me.Controls.Add(Me.btnSignIn)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.txtPass)
        Me.Controls.Add(Me.txtUser)
        Me.Controls.Add(Me.txtOrg)
        Me.Controls.Add(Me.lblPass)
        Me.Controls.Add(Me.lblUser)
        Me.Controls.Add(Me.lblOrg)
        Me.Controls.Add(Me.lblSubtitle)
        Me.Controls.Add(Me.lblTitle)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "LoginForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen
        Me.Text = "General Management System — Sign in"
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblTitle As System.Windows.Forms.Label
    Friend WithEvents lblSubtitle As System.Windows.Forms.Label
    Friend WithEvents lblOrg As System.Windows.Forms.Label
    Friend WithEvents lblUser As System.Windows.Forms.Label
    Friend WithEvents lblPass As System.Windows.Forms.Label
    Friend WithEvents txtOrg As System.Windows.Forms.TextBox
    Friend WithEvents txtUser As System.Windows.Forms.TextBox
    Friend WithEvents txtPass As System.Windows.Forms.TextBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnSignIn As System.Windows.Forms.Button
End Class

End Namespace
