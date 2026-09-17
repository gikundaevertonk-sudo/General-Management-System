Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a supplier.</summary>
    Public Class SupplierEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(supplierId As Integer?)
            InitializeComponent()
            _id = supplierId
            Text = If(supplierId Is Nothing, "New supplier", "Edit supplier")
            If supplierId.HasValue Then LoadSupplier(supplierId.Value) Else chkActive.Checked = True
        End Sub

        Private Sub LoadSupplier(id As Integer)
            Dim result = AppHost.Current.Resolve(Of SupplierService)().GetById(id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            Dim s = result.Value
            txtName.Text = s.Name : txtContact.Text = s.ContactName : txtEmail.Text = s.Email
            txtPhone.Text = s.Phone : txtAddress.Text = s.Address : chkActive.Checked = s.IsActive
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim input As New SupplierInput With {
                .Name = txtName.Text, .ContactName = txtContact.Text, .Email = txtEmail.Text,
                .Phone = txtPhone.Text, .Address = txtAddress.Text, .IsActive = chkActive.Checked}

            Dim svc = AppHost.Current.Resolve(Of SupplierService)()
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
