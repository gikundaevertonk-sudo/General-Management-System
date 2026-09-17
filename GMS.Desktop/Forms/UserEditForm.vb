Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a user account and its role.</summary>
    Public Class UserEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(userId As Integer?)
            InitializeComponent()
            _id = userId
            Text = If(userId Is Nothing, "New user", "Edit user")

            If userId IsNot Nothing Then
                ' No temp-password field when editing; pull the rows below it up.
                lblTempPassword.Visible = False
                txtTempPassword.Visible = False
                chkActive.Top -= 36
                lblError.Top -= 36
                btnSave.Top -= 36
                btnCancel.Top -= 36
                ClientSize = New Size(ClientSize.Width, ClientSize.Height - 36)
            End If

            LoadRoles()
            If userId.HasValue Then LoadUser(userId.Value)
        End Sub

        Private Sub LoadRoles()
            cboRole.DisplayMember = "Text"
            cboRole.ValueMember = "Value"
            Dim res = AppHost.Current.Resolve(Of RoleService)().List()
            If res.Succeeded Then
                cboRole.DataSource = res.Value.Select(Function(r) New With {.Value = r.Role.Id, .Text = r.Role.Name}).ToList()
            End If
        End Sub

        Private Sub LoadUser(id As Integer)
            Dim res = AppHost.Current.Resolve(Of UserService)().GetById(id)
            If res.Failed Then
                lblError.Text = res.ErrorMessage
                Return
            End If
            Dim u = res.Value
            txtUserName.Text = u.UserName
            txtFullName.Text = u.FullName
            txtEmail.Text = u.Email
            chkActive.Checked = u.IsActive
            cboRole.SelectedValue = u.RoleId
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim roleId = 0
            Integer.TryParse(Convert.ToString(cboRole.SelectedValue), roleId)

            Dim input As New UserInput With {
                .UserName = txtUserName.Text, .FullName = txtFullName.Text, .Email = txtEmail.Text,
                .RoleId = roleId, .IsActive = chkActive.Checked}

            Dim svc = AppHost.Current.Resolve(Of UserService)()
            Dim result As Result = If(_id Is Nothing, svc.Create(input, txtTempPassword.Text), svc.Update(_id.Value, input))
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If

            DialogResult = DialogResult.OK
            Close()
        End Sub

        Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
            Close()
        End Sub
    End Class

End Namespace
