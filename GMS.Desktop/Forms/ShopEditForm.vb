Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit one shop.</summary>
    Public Class ShopEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(shopId As Integer?)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            Icon = UiKit.AppIcon
            _id = shopId
            Text = If(shopId Is Nothing, "New shop", "Edit shop")

            If shopId.HasValue Then LoadShop(shopId.Value)
        End Sub

        Private Sub LoadShop(id As Integer)
            Dim res = AppHost.Current.Resolve(Of ShopService)().GetById(id)
            If res.Failed Then
                lblError.Text = res.ErrorMessage
                btnSave.Enabled = False
                Return
            End If
            Dim s = res.Value
            txtName.Text = s.Name
            txtCode.Text = s.Code
            txtAddress.Text = s.Address
            txtPhone.Text = s.Phone
            chkActive.Checked = s.IsActive
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""

            Dim input As New ShopInput With {
                .Name = txtName.Text, .Code = txtCode.Text, .Address = txtAddress.Text,
                .Phone = txtPhone.Text, .IsActive = chkActive.Checked}

            Dim svc = AppHost.Current.Resolve(Of ShopService)()
            Dim result As Result = If(_id Is Nothing, svc.Create(input), svc.Update(_id.Value, input))
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
