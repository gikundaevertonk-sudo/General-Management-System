Imports GMS.Core.Enums
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Manual stock adjustment for one product.</summary>
    Public Class StockAdjustForm

        Private ReadOnly _productId As Integer

        Public Sub New(productId As Integer)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            _productId = productId
            cboDirection.SelectedIndex = 0

            Dim prod = AppHost.Current.Resolve(Of ProductService)().GetById(productId)
            lblProduct.Text = If(prod.Succeeded, prod.Value.Name, $"Product #{productId}")
        End Sub

        Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
            lblError.Text = ""
            Dim direction = If(cboDirection.SelectedIndex = 0, StockMovementDirection.In, StockMovementDirection.Out)
            Dim result = AppHost.Current.Resolve(Of InventoryService)().Adjust(_productId, direction, numQty.Value, txtNote.Text)
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
