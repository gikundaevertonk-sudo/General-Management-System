Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class SettingsView
        Inherits ViewBase

        Private ReadOnly _companyName As New TextBox()
        Private ReadOnly _currency As New TextBox()
        Private ReadOnly _taxRate As New NumericUpDown()
        Private ReadOnly _lowStockScan As New CheckBox()
        Private ReadOnly _error As New Label()

        Public Sub New()
            MyBase.New("Settings")

            Dim panel As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(24)}

            _companyName.SetBounds(180, 16, 300, 24)
            _currency.SetBounds(180, 52, 100, 24)
            _taxRate.DecimalPlaces = 2 : _taxRate.Maximum = 100 : _taxRate.Minimum = 0
            _taxRate.SetBounds(180, 88, 100, 24)
            _lowStockScan.Text = "Raise low-stock alerts automatically"
            _lowStockScan.AutoSize = True
            _lowStockScan.Location = New Point(180, 124)

            panel.Controls.Add(New Label With {.Text = "Company name", .ForeColor = UiKit.MutedText, .AutoSize = True, .Location = New Point(20, 20)})
            panel.Controls.Add(New Label With {.Text = "Currency code", .ForeColor = UiKit.MutedText, .AutoSize = True, .Location = New Point(20, 56)})
            panel.Controls.Add(New Label With {.Text = "Default tax rate (%)", .ForeColor = UiKit.MutedText, .AutoSize = True, .Location = New Point(20, 92)})
            panel.Controls.AddRange({_companyName, _currency, _taxRate, _lowStockScan})

            _error.SetBounds(20, 156, 400, 20)
            _error.ForeColor = Color.FromArgb(185, 28, 28)
            panel.Controls.Add(_error)

            Dim save = UiKit.PrimaryButton("Save")
            save.SetBounds(180, 186, 120, 34)
            AddHandler save.Click, AddressOf OnSave
            panel.Controls.Add(save)

            Body.Controls.Add(panel)
            LoadValues()
        End Sub

        Private Sub LoadValues()
            Dim svc = AppHost.Current.Resolve(Of SettingsService)()
            _companyName.Text = svc.GetString(SettingKeys.CompanyName, "My Business")
            _currency.Text = svc.GetString(SettingKeys.CurrencyCode, "USD")
            _taxRate.Value = svc.GetDecimal(SettingKeys.DefaultTaxRatePercent, 0D)
            _lowStockScan.Checked = svc.GetBool(SettingKeys.LowStockScanEnabled, True)
        End Sub

        Private Sub OnSave(sender As Object, e As EventArgs)
            _error.Text = ""
            Dim svc = AppHost.Current.Resolve(Of SettingsService)()
            Dim results = {
                svc.SetValue(SettingKeys.CompanyName, _companyName.Text),
                svc.SetValue(SettingKeys.CurrencyCode, _currency.Text),
                svc.SetValue(SettingKeys.DefaultTaxRatePercent, _taxRate.Value.ToString("0.####")),
                svc.SetValue(SettingKeys.LowStockScanEnabled, If(_lowStockScan.Checked, "true", "false"))
            }
            Dim failed = results.FirstOrDefault(Function(r) r.Failed)
            If failed IsNot Nothing Then
                _error.Text = failed.ErrorMessage
                Return
            End If
            UiKit.Info(Me, "Settings saved.")
        End Sub
    End Class

End Namespace
