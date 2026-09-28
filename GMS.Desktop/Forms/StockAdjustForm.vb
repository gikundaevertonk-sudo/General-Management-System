Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Enums
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Manual stock adjustment for one product at one location.</summary>
    Public Class StockAdjustForm

        Private ReadOnly _productId As Integer

        Public Sub New(productId As Integer)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            _productId = productId
            cboDirection.SelectedIndex = 0

            Dim prod = AppHost.Current.Resolve(Of ProductService)().GetById(productId)
            lblProduct.Text = If(prod.Succeeded, prod.Value.Name, $"Product #{productId}")

            LoadLocations()
        End Sub

        ''' <summary>
        ''' Fills the location picker, and hides it when there is nothing to pick.
        ''' </summary>
        ''' <remarks>
        ''' Hidden for an organisation with no shops - everything is central and the choice would
        ''' be a single dead row - and for a pinned attendant, whose adjustment can only ever apply
        ''' to their own shop no matter what this said.
        ''' </remarks>
        Private Sub LoadLocations()
            cboLocation.DisplayMember = "Text"
            cboLocation.ValueMember = "Value"

            Dim pinned = AppHost.Current.Session.Principal.ShopId
            Dim shops = AppHost.Current.Resolve(Of ShopService)().List()
            Dim hasShops = shops.Succeeded AndAlso shops.Value.Count > 0

            If pinned.HasValue OrElse Not hasShops Then
                lblLocation.Visible = False
                cboLocation.Visible = False
                For Each c As Control In New Control() {lblDirection, cboDirection, lblQty, numQty,
                                                        lblNote, txtNote, lblError, btnApply, btnCancel}
                    c.Top -= 36
                Next
                ClientSize = New Size(ClientSize.Width, ClientSize.Height - 36)
                Return
            End If

            ' 0 stands for central, because a ComboBox value cannot be Nothing and round-trip.
            Dim choices As New List(Of Object) From {New With {.Value = 0, .Text = "Central"}}
            choices.AddRange(shops.Value.Select(Function(s) CObj(New With {.Value = s.Id, .Text = s.Name})))
            cboLocation.DataSource = choices
        End Sub

        Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
            lblError.Text = ""
            Dim direction = If(cboDirection.SelectedIndex = 0, StockMovementDirection.In, StockMovementDirection.Out)

            Dim location = 0
            If cboLocation.Visible Then Integer.TryParse(Convert.ToString(cboLocation.SelectedValue), location)
            Dim shopId As Integer? = If(location = 0, CType(Nothing, Integer?), location)

            Dim result = AppHost.Current.Resolve(Of InventoryService)().
                Adjust(_productId, direction, numQty.Value, txtNote.Text, shopId)
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
