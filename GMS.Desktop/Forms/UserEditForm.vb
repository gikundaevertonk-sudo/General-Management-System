Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a user account and its role.</summary>
    Public Class UserEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(userId As Integer?)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            _id = userId
            Text = If(userId Is Nothing, "New user", "Edit user")

            LoadRoles()
            Dim shopCount = LoadShops()

            ' An organisation with no shops has no attendants to create, so the picker would only
            ' be a row of dead space. Hidden rather than disabled, and the rows below pull up.
            If shopCount = 0 Then
                lblShop.Visible = False
                cboShop.Visible = False
                lblShopHint.Visible = False
                ShiftUp(60)
            End If

            If userId IsNot Nothing Then
                ' No temp-password field when editing; pull the rows below it up too.
                lblTempPassword.Visible = False
                txtTempPassword.Visible = False
                ShiftUp(36)
            End If

            If userId.HasValue Then LoadUser(userId.Value)
        End Sub

        ''' <summary>
        ''' Closes the gap a hidden row leaves behind, and shrinks the dialog to match. Rows are
        ''' moved whether or not they are themselves visible, so two hidden rows compose.
        ''' </summary>
        Private Sub ShiftUp(by As Integer)
            For Each c As Control In New Control() {lblTempPassword, txtTempPassword, chkActive, lblError, btnSave, btnCancel}
                c.Top -= by
            Next
            ClientSize = New Size(ClientSize.Width, ClientSize.Height - by)
        End Sub

        Private Sub LoadRoles()
            cboRole.DisplayMember = "Text"
            cboRole.ValueMember = "Value"
            Dim res = AppHost.Current.Resolve(Of RoleService)().List()
            If res.Succeeded Then
                cboRole.DataSource = res.Value.Select(Function(r) New With {.Value = r.Role.Id, .Text = r.Role.Name}).ToList()
            End If
        End Sub

        ''' <summary>
        ''' Fills the shop picker and returns how many shops there are (the "all shops" entry
        ''' excluded, since it is always offered).
        ''' </summary>
        Private Function LoadShops() As Integer
            cboShop.DisplayMember = "Text"
            cboShop.ValueMember = "Value"

            ' 0 stands for "no restriction": a ComboBox value cannot be Nothing and still
            ' round-trip, so it is turned back into Nothing when the input is built.
            Dim choices As New List(Of Object) From {New With {.Value = 0, .Text = "All shops (no restriction)"}}

            Dim res = AppHost.Current.Resolve(Of ShopService)().List()
            If res.Succeeded Then
                choices.AddRange(res.Value.Select(Function(s) CObj(New With {.Value = s.Id, .Text = s.Name})))
            End If

            cboShop.DataSource = choices
            Return choices.Count - 1
        End Function

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
            cboShop.SelectedValue = If(u.ShopId, 0)
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim roleId = 0
            Integer.TryParse(Convert.ToString(cboRole.SelectedValue), roleId)
            Dim shopId = 0
            Integer.TryParse(Convert.ToString(cboShop.SelectedValue), shopId)

            Dim input As New UserInput With {
                .UserName = txtUserName.Text, .FullName = txtFullName.Text, .Email = txtEmail.Text,
                .RoleId = roleId,
                .ShopId = If(shopId = 0, CType(Nothing, Integer?), shopId),
                .IsActive = chkActive.Checked}

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
