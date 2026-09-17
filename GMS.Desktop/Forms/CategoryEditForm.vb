Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Create or edit a category.</summary>
    Public Class CategoryEditForm

        Private ReadOnly _id As Integer?

        Public Sub New(categoryId As Integer?)
            InitializeComponent()
            _id = categoryId
            Text = If(categoryId Is Nothing, "New category", "Edit category")
            LoadParents()
            If categoryId.HasValue Then LoadCategory(categoryId.Value)
        End Sub

        Private Sub LoadParents()
            cboParent.DisplayMember = "Text"
            cboParent.ValueMember = "Value"
            Dim items As New List(Of KeyValuePair(Of Integer, String)) From {
                New KeyValuePair(Of Integer, String)(0, "— none —")}
            Dim cats = AppHost.Current.Resolve(Of CategoryService)().List()
            If cats.Succeeded Then
                For Each c In cats.Value
                    If c.Id <> _id Then items.Add(New KeyValuePair(Of Integer, String)(c.Id, c.Name)) ' can't be its own parent
                Next
            End If
            cboParent.DataSource = items.Select(Function(kv) New With {.Value = kv.Key, .Text = kv.Value}).ToList()
        End Sub

        Private Sub LoadCategory(id As Integer)
            Dim result = AppHost.Current.Resolve(Of CategoryService)().GetById(id)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If
            Dim c = result.Value
            txtName.Text = c.Name
            txtDesc.Text = c.Description
            cboParent.SelectedValue = If(c.ParentCategoryId.HasValue, c.ParentCategoryId.Value, 0)
        End Sub

        Private Sub btnSave_Click(sender As Object, e As EventArgs) Handles btnSave.Click
            lblError.Text = ""
            Dim parentId = 0
            Integer.TryParse(Convert.ToString(cboParent.SelectedValue), parentId)
            Dim parent As Integer? = If(parentId = 0, CType(Nothing, Integer?), parentId)

            Dim svc = AppHost.Current.Resolve(Of CategoryService)()
            Dim result As Result = If(_id Is Nothing,
                CType(svc.Create(txtName.Text, txtDesc.Text, parent), Result),
                svc.Update(_id.Value, txtName.Text, txtDesc.Text, parent))
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
