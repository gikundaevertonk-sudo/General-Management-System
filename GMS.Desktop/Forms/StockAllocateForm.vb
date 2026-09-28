Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>
    ''' Sends stock out of one location. The source is fixed by whatever the caller had selected,
    ''' so the only decisions here are what, where to, and how much.
    ''' </summary>
    Public Class StockAllocateForm

        Private ReadOnly _fromShopId As Integer?
        Private ReadOnly _fromName As String

        ''' <param name="fromShopId">Nothing for the central pool.</param>
        Public Sub New(fromShopId As Integer?, fromName As String)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            Icon = UiKit.AppIcon
            _fromShopId = fromShopId
            _fromName = fromName

            lblFrom.Text = $"From {fromName}"
            LoadProducts()
            LoadDestinations()
            ShowAvailable()
            AddHandler cboProduct.SelectedIndexChanged, Sub() ShowAvailable()
        End Sub

        Private Sub LoadProducts()
            cboProduct.DisplayMember = "Text"
            cboProduct.ValueMember = "Value"
            Dim res = AppHost.Current.Resolve(Of ProductService)().
                Search(New QueryOptions With {.PageSize = QueryOptions.MaxPageSize, .SortBy = "name"}, activeOnly:=True)
            If res.Succeeded Then
                cboProduct.DataSource = res.Value.Items.
                    Select(Function(p) New With {.Value = p.Id, .Text = $"{p.Sku} — {p.Name}"}).ToList()
            End If
        End Sub

        Private Sub LoadDestinations()
            cboTo.DisplayMember = "Text"
            cboTo.ValueMember = "Value"

            ' 0 stands for central in the combo, because a ComboBox value cannot be Nothing and
            ' still round-trip. Turned back into Nothing in btnApply_Click.
            Dim choices As New List(Of Object)()
            If _fromShopId.HasValue Then choices.Add(New With {.Value = 0, .Text = "Central"})

            Dim res = AppHost.Current.Resolve(Of ShopService)().List()
            If res.Succeeded Then
                For Each shop In res.Value
                    If _fromShopId.HasValue AndAlso shop.Id = _fromShopId.Value Then Continue For
                    choices.Add(New With {.Value = shop.Id, .Text = shop.Name})
                Next
            End If

            cboTo.DataSource = choices
            If choices.Count = 0 Then
                lblError.Text = "There is nowhere to send stock to yet. Add another shop first."
                btnApply.Enabled = False
            End If
        End Sub

        ''' <summary>Shows how much of the chosen product is actually here, before it is asked for.</summary>
        Private Sub ShowAvailable()
            lblAvailable.Text = ""
            Dim productId = SelectedProductId()
            If productId = 0 Then Return

            Dim spread = AppHost.Current.Resolve(Of InventoryService)().GetDistribution(productId)
            If spread.Failed Then Return

            Dim here = spread.Value.FirstOrDefault(
                Function(r) If(r.ShopId, 0) = If(_fromShopId, 0))
            If here Is Nothing Then Return
            lblAvailable.Text = $"{here.QuantityOnHand:N3} {here.UnitOfMeasure} at {_fromName}"
        End Sub

        Private Function SelectedProductId() As Integer
            Dim id = 0
            Integer.TryParse(Convert.ToString(cboProduct.SelectedValue), id)
            Return id
        End Function

        Private Sub btnApply_Click(sender As Object, e As EventArgs) Handles btnApply.Click
            lblError.Text = ""

            Dim productId = SelectedProductId()
            If productId = 0 Then
                lblError.Text = "Choose a product."
                Return
            End If

            Dim destination = 0
            Integer.TryParse(Convert.ToString(cboTo.SelectedValue), destination)
            Dim toShopId As Integer? = If(destination = 0, CType(Nothing, Integer?), destination)

            Dim result = AppHost.Current.Resolve(Of InventoryService)().
                Allocate(productId, _fromShopId, toShopId, numQty.Value, txtNote.Text)
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
