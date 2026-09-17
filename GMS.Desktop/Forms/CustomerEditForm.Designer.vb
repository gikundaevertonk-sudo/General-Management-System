Namespace Forms

<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class CustomerEditForm
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
        Me.lblCode = New System.Windows.Forms.Label()
        Me.txtCode = New System.Windows.Forms.TextBox()
        Me.lblName = New System.Windows.Forms.Label()
        Me.txtName = New System.Windows.Forms.TextBox()
        Me.lblContact = New System.Windows.Forms.Label()
        Me.txtContact = New System.Windows.Forms.TextBox()
        Me.lblEmail = New System.Windows.Forms.Label()
        Me.txtEmail = New System.Windows.Forms.TextBox()
        Me.lblPhone = New System.Windows.Forms.Label()
        Me.txtPhone = New System.Windows.Forms.TextBox()
        Me.lblAddress = New System.Windows.Forms.Label()
        Me.txtAddress = New System.Windows.Forms.TextBox()
        Me.lblNotes = New System.Windows.Forms.Label()
        Me.txtNotes = New System.Windows.Forms.TextBox()
        Me.chkActive = New System.Windows.Forms.CheckBox()
        Me.lblError = New System.Windows.Forms.Label()
        Me.btnSave = New System.Windows.Forms.Button()
        Me.btnCancel = New System.Windows.Forms.Button()
        Me.SuspendLayout()
        '
        Me.lblCode.AutoSize = True
        Me.lblCode.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblCode.Location = New System.Drawing.Point(20, 21)
        Me.lblCode.Name = "lblCode"
        Me.lblCode.Size = New System.Drawing.Size(34, 15)
        Me.lblCode.TabIndex = 0
        Me.lblCode.Text = "Code"
        '
        Me.txtCode.Location = New System.Drawing.Point(140, 18)
        Me.txtCode.Name = "txtCode"
        Me.txtCode.Size = New System.Drawing.Size(280, 23)
        Me.txtCode.TabIndex = 1
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
        Me.lblContact.AutoSize = True
        Me.lblContact.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblContact.Location = New System.Drawing.Point(20, 93)
        Me.lblContact.Name = "lblContact"
        Me.lblContact.Size = New System.Drawing.Size(78, 15)
        Me.lblContact.TabIndex = 4
        Me.lblContact.Text = "Contact name"
        '
        Me.txtContact.Location = New System.Drawing.Point(140, 90)
        Me.txtContact.Name = "txtContact"
        Me.txtContact.Size = New System.Drawing.Size(280, 23)
        Me.txtContact.TabIndex = 5
        '
        Me.lblEmail.AutoSize = True
        Me.lblEmail.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblEmail.Location = New System.Drawing.Point(20, 129)
        Me.lblEmail.Name = "lblEmail"
        Me.lblEmail.Size = New System.Drawing.Size(36, 15)
        Me.lblEmail.TabIndex = 6
        Me.lblEmail.Text = "Email"
        '
        Me.txtEmail.Location = New System.Drawing.Point(140, 126)
        Me.txtEmail.Name = "txtEmail"
        Me.txtEmail.Size = New System.Drawing.Size(280, 23)
        Me.txtEmail.TabIndex = 7
        '
        Me.lblPhone.AutoSize = True
        Me.lblPhone.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblPhone.Location = New System.Drawing.Point(20, 165)
        Me.lblPhone.Name = "lblPhone"
        Me.lblPhone.Size = New System.Drawing.Size(39, 15)
        Me.lblPhone.TabIndex = 8
        Me.lblPhone.Text = "Phone"
        '
        Me.txtPhone.Location = New System.Drawing.Point(140, 162)
        Me.txtPhone.Name = "txtPhone"
        Me.txtPhone.Size = New System.Drawing.Size(280, 23)
        Me.txtPhone.TabIndex = 9
        '
        Me.lblAddress.AutoSize = True
        Me.lblAddress.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblAddress.Location = New System.Drawing.Point(20, 201)
        Me.lblAddress.Name = "lblAddress"
        Me.lblAddress.Size = New System.Drawing.Size(88, 15)
        Me.lblAddress.TabIndex = 10
        Me.lblAddress.Text = "Billing address"
        '
        Me.txtAddress.Location = New System.Drawing.Point(140, 198)
        Me.txtAddress.Name = "txtAddress"
        Me.txtAddress.Size = New System.Drawing.Size(280, 23)
        Me.txtAddress.TabIndex = 11
        '
        Me.lblNotes.AutoSize = True
        Me.lblNotes.ForeColor = System.Drawing.Color.FromArgb(107, 114, 128)
        Me.lblNotes.Location = New System.Drawing.Point(20, 237)
        Me.lblNotes.Name = "lblNotes"
        Me.lblNotes.Size = New System.Drawing.Size(38, 15)
        Me.lblNotes.TabIndex = 12
        Me.lblNotes.Text = "Notes"
        '
        Me.txtNotes.Location = New System.Drawing.Point(140, 234)
        Me.txtNotes.Multiline = True
        Me.txtNotes.Name = "txtNotes"
        Me.txtNotes.Size = New System.Drawing.Size(280, 50)
        Me.txtNotes.TabIndex = 13
        '
        Me.chkActive.AutoSize = True
        Me.chkActive.Checked = True
        Me.chkActive.CheckState = System.Windows.Forms.CheckState.Checked
        Me.chkActive.Location = New System.Drawing.Point(140, 294)
        Me.chkActive.Name = "chkActive"
        Me.chkActive.Size = New System.Drawing.Size(59, 19)
        Me.chkActive.TabIndex = 14
        Me.chkActive.Text = "Active"
        '
        Me.lblError.ForeColor = System.Drawing.Color.FromArgb(185, 28, 28)
        Me.lblError.Location = New System.Drawing.Point(20, 324)
        Me.lblError.Name = "lblError"
        Me.lblError.Size = New System.Drawing.Size(400, 32)
        Me.lblError.TabIndex = 15
        '
        Me.btnSave.BackColor = System.Drawing.Color.FromArgb(37, 99, 235)
        Me.btnSave.FlatStyle = System.Windows.Forms.FlatStyle.Flat
        Me.btnSave.ForeColor = System.Drawing.Color.White
        Me.btnSave.Location = New System.Drawing.Point(140, 364)
        Me.btnSave.Name = "btnSave"
        Me.btnSave.Size = New System.Drawing.Size(160, 34)
        Me.btnSave.TabIndex = 16
        Me.btnSave.Text = "Save"
        Me.btnSave.UseVisualStyleBackColor = False
        '
        Me.btnCancel.Location = New System.Drawing.Point(310, 364)
        Me.btnCancel.Name = "btnCancel"
        Me.btnCancel.Size = New System.Drawing.Size(110, 34)
        Me.btnCancel.TabIndex = 17
        Me.btnCancel.Text = "Cancel"
        Me.btnCancel.UseVisualStyleBackColor = True
        '
        'CustomerEditForm
        '
        Me.AcceptButton = Me.btnSave
        Me.AutoScaleDimensions = New System.Drawing.SizeF(7.0!, 15.0!)
        Me.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font
        Me.BackColor = System.Drawing.Color.White
        Me.CancelButton = Me.btnCancel
        Me.ClientSize = New System.Drawing.Size(440, 420)
        Me.Controls.Add(Me.btnCancel)
        Me.Controls.Add(Me.btnSave)
        Me.Controls.Add(Me.lblError)
        Me.Controls.Add(Me.chkActive)
        Me.Controls.Add(Me.txtNotes)
        Me.Controls.Add(Me.lblNotes)
        Me.Controls.Add(Me.txtAddress)
        Me.Controls.Add(Me.lblAddress)
        Me.Controls.Add(Me.txtPhone)
        Me.Controls.Add(Me.lblPhone)
        Me.Controls.Add(Me.txtEmail)
        Me.Controls.Add(Me.lblEmail)
        Me.Controls.Add(Me.txtContact)
        Me.Controls.Add(Me.lblContact)
        Me.Controls.Add(Me.txtName)
        Me.Controls.Add(Me.lblName)
        Me.Controls.Add(Me.txtCode)
        Me.Controls.Add(Me.lblCode)
        Me.Font = New System.Drawing.Font("Segoe UI", 9.0!)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "CustomerEditForm"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Customer"
        Me.ResumeLayout(False)
        Me.PerformLayout()
    End Sub

    Friend WithEvents lblCode As System.Windows.Forms.Label
    Friend WithEvents txtCode As System.Windows.Forms.TextBox
    Friend WithEvents lblName As System.Windows.Forms.Label
    Friend WithEvents txtName As System.Windows.Forms.TextBox
    Friend WithEvents lblContact As System.Windows.Forms.Label
    Friend WithEvents txtContact As System.Windows.Forms.TextBox
    Friend WithEvents lblEmail As System.Windows.Forms.Label
    Friend WithEvents txtEmail As System.Windows.Forms.TextBox
    Friend WithEvents lblPhone As System.Windows.Forms.Label
    Friend WithEvents txtPhone As System.Windows.Forms.TextBox
    Friend WithEvents lblAddress As System.Windows.Forms.Label
    Friend WithEvents txtAddress As System.Windows.Forms.TextBox
    Friend WithEvents lblNotes As System.Windows.Forms.Label
    Friend WithEvents txtNotes As System.Windows.Forms.TextBox
    Friend WithEvents chkActive As System.Windows.Forms.CheckBox
    Friend WithEvents lblError As System.Windows.Forms.Label
    Friend WithEvents btnSave As System.Windows.Forms.Button
    Friend WithEvents btnCancel As System.Windows.Forms.Button
End Class

End Namespace
