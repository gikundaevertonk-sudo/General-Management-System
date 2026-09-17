Imports System.IO
Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Contracts
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class ReportsView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _from As New DateTimePicker()
        Private ReadOnly _to As New DateTimePicker()
        Private ReadOnly _summary As New Label()
        Private _currentRows As Object

        Public Sub New()
            MyBase.New("Reports")

            Dim exportBtn = UiKit.SecondaryButton("Export CSV")
            AddHandler exportBtn.Click, Sub() ExportCsv()
            AddAction(exportBtn)

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 80, .Padding = New Padding(8)}
            _from.Format = DateTimePickerFormat.Short : _from.SetBounds(8, 10, 130, 24)
            _from.Value = DateTime.Today.AddDays(-30)
            _to.Format = DateTimePickerFormat.Short : _to.SetBounds(150, 10, 130, 24)
            _to.Value = DateTime.Today
            bar.Controls.Add(New Label With {.Text = "From / To", .ForeColor = UiKit.MutedText, .AutoSize = True, .Location = New Point(8, -8 + 12), .Visible = False})

            Dim salesRange = UiKit.SecondaryButton("Sales (range)") : salesRange.SetBounds(292, 8, 110, 28)
            AddHandler salesRange.Click, Sub() ShowSales(New DateRange(_from.Value.Date.ToUniversalTime(), _to.Value.Date.AddDays(1).ToUniversalTime()))
            Dim salesMonth = UiKit.SecondaryButton("Sales (MTD)") : salesMonth.SetBounds(410, 8, 100, 28)
            AddHandler salesMonth.Click, Sub() ShowSales(DateRange.MonthToDate(DateTime.UtcNow))
            Dim valuation = UiKit.SecondaryButton("Inventory valuation") : valuation.SetBounds(292, 42, 140, 28)
            AddHandler valuation.Click, Sub() ShowValuation()
            Dim lowStock = UiKit.SecondaryButton("Low stock") : lowStock.SetBounds(440, 42, 100, 28)
            AddHandler lowStock.Click, Sub() ShowLowStock()

            _summary.SetBounds(560, 12, 320, 56) : _summary.ForeColor = UiKit.MutedText
            bar.Controls.AddRange({_from, _to, salesRange, salesMonth, valuation, lowStock, _summary})

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)

            ShowSales(DateRange.MonthToDate(DateTime.UtcNow))
        End Sub

        Private Sub ResetColumns(ParamArray cols As DataGridViewColumn())
            _grid.DataSource = Nothing
            _grid.Columns.Clear()
            _grid.Columns.AddRange(cols)
        End Sub

        Private Sub ShowSales(range As DateRange)
            Guarded(Sub()
                        Dim res = AppHost.Current.Resolve(Of ReportService)().SalesSummary(range)
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return
                        SetTitle("Reports — sales summary")
                        ResetColumns(UiKit.TextColumn("SKU", "Sku", width:=120),
                                     UiKit.TextColumn("Product", "ProductName", fill:=100),
                                     UiKit.NumberColumn("Qty sold", "QuantitySold", "N3", 100),
                                     UiKit.NumberColumn("Revenue", "Revenue", "N2", 120))
                        Dim rows = res.Value.Lines.ToList()
                        _grid.DataSource = rows
                        _currentRows = rows
                        _summary.Text = $"{res.Value.Range.FromUtc:yyyy-MM-dd} → {res.Value.Range.ToUtc:yyyy-MM-dd}" & Environment.NewLine &
                                        $"{res.Value.TransactionCount} sale(s)   Subtotal {res.Value.Subtotal:N2}   Tax {res.Value.Tax:N2}   Total {res.Value.Total:N2}"
                    End Sub)
        End Sub

        Private Sub ShowValuation()
            Guarded(Sub()
                        Dim res = AppHost.Current.Resolve(Of ReportService)().InventoryValuation()
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return
                        SetTitle("Reports — inventory valuation")
                        ResetColumns(UiKit.TextColumn("SKU", "Sku", width:=120),
                                     UiKit.TextColumn("Product", "ProductName", fill:=100),
                                     UiKit.NumberColumn("On hand", "QuantityOnHand", "N3", 100),
                                     UiKit.NumberColumn("Unit cost", "UnitCost", "N2", 100),
                                     UiKit.NumberColumn("Value", "ValueAtCost", "N2", 120))
                        Dim rows = res.Value.ToList()
                        _grid.DataSource = rows
                        _currentRows = rows
                        _summary.Text = $"{rows.Count} product(s)   Total value {rows.Sum(Function(r) r.ValueAtCost):N2}"
                    End Sub)
        End Sub

        Private Sub ShowLowStock()
            Guarded(Sub()
                        Dim res = AppHost.Current.Resolve(Of ReportService)().LowStock()
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return
                        SetTitle("Reports — low stock")
                        ResetColumns(UiKit.TextColumn("SKU", "Sku", width:=120),
                                     UiKit.TextColumn("Product", "Name", fill:=100),
                                     UiKit.NumberColumn("On hand", "QuantityOnHand", "N3", 100),
                                     UiKit.NumberColumn("Reorder level", "ReorderLevel", "N3", 110))
                        Dim rows = res.Value.Select(Function(p) New With {p.Sku, p.Name, p.QuantityOnHand, p.ReorderLevel}).ToList()
                        _grid.DataSource = rows
                        _currentRows = rows
                        _summary.Text = $"{rows.Count} product(s) at or below reorder level"
                    End Sub)
        End Sub

        Private Sub ExportCsv()
            If _currentRows Is Nothing Then UiKit.Info(Me, "Run a report first.") : Return
            Using dlg As New SaveFileDialog With {.Filter = "CSV file (*.csv)|*.csv", .FileName = "report.csv"}
                If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
                File.WriteAllText(dlg.FileName, ReportService.ToCsv(CType(_currentRows, IEnumerable)))
                UiKit.Info(Me, "Saved " & dlg.FileName)
            End Using
        End Sub
    End Class

End Namespace
