Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a single product.</summary>
    Public Class ProductEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(productId As Integer?)
            InitializeComponent()
            _id = productId
            Text = If(productId Is Nothing, "New product", "Edit product")

            LoadCategories()
            If productId.HasValue Then
                LoadProduct(productId.Value)
            Else
                txtUom.Text = "each"
            End If
        End Sub

        Private Sub LoadCategories()
            cboCategory.DisplayMember = "Text"
            cboCategory.ValueMember = "Value"
            Dim items As New List(Of KeyValuePair(Of Integer, String)) From {
                New KeyValuePair(Of Integer, String)(0, "— none —")}
            Dim cats = AppHost.Current.Resolve(Of CategoryService)().List()
            If cats.Succeeded Then
                For Each c In cats.Value
                    items.Add(New KeyValuePair(Of Integer, String)(c.Id, c.Name))
                Next
            End If
            cboCategory.DataSource = items.Select(Function(kv) New With {.Value = kv.Key, .Text = kv.Value}).ToList()
        End Sub

        Private Sub LoadProduct(id As Integer)
            Dim result = AppHost.Current.Resolve(Of ProductService)().GetById(id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            Dim p = result.Value
            txtSku.Text = p.Sku
            txtName.Text = p.Name
            txtDesc.Text = p.Description
            txtUom.Text = p.UnitOfMeasure
            numPrice.Value = p.UnitPrice
            numCost.Value = p.CostPrice
            numReorder.Value = p.ReorderLevel
            chkActive.Checked = p.IsActive
            cboCategory.SelectedValue = If(p.CategoryId.HasValue, p.CategoryId.Value, 0)
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim selectedCat = 0
            Integer.TryParse(Convert.ToString(cboCategory.SelectedValue), selectedCat)

            Dim input As New ProductInput With {
                .Sku = txtSku.Text, .Name = txtName.Text, .Description = txtDesc.Text,
                .CategoryId = If(selectedCat = 0, CType(Nothing, Integer?), selectedCat),
                .UnitPrice = numPrice.Value, .CostPrice = numCost.Value,
                .UnitOfMeasure = txtUom.Text, .ReorderLevel = numReorder.Value,
                .IsActive = chkActive.Checked}

            Dim svc = AppHost.Current.Resolve(Of ProductService)()
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
