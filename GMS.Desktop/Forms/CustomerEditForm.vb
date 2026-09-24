Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a customer.</summary>
    Public Class CustomerEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(customerId As Integer?)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            _id = customerId
            Text = If(customerId Is Nothing, "New customer", "Edit customer")
            If customerId.HasValue Then LoadCustomer(customerId.Value) Else chkActive.Checked = True
        End Sub

        Private Sub LoadCustomer(id As Integer)
            Dim result = AppHost.Current.Resolve(Of CustomerService)().GetById(id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            Dim c = result.Value
            txtCode.Text = c.Code : txtName.Text = c.Name : txtContact.Text = c.ContactName
            txtEmail.Text = c.Email : txtPhone.Text = c.Phone : txtAddress.Text = c.BillingAddress
            txtNotes.Text = c.Notes : chkActive.Checked = c.IsActive
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim input As New CustomerInput With {
                .Code = txtCode.Text, .Name = txtName.Text, .ContactName = txtContact.Text,
                .Email = txtEmail.Text, .Phone = txtPhone.Text, .BillingAddress = txtAddress.Text,
                .Notes = txtNotes.Text, .IsActive = chkActive.Checked}

            Dim svc = AppHost.Current.Resolve(Of CustomerService)()
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
