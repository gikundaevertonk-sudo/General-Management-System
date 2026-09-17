Imports System.Globalization
Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>Well-known application setting keys.</summary>
    Public NotInheritable Class SettingKeys
        Public Const CompanyName As String = "company.name"
        Public Const CurrencyCode As String = "company.currency"
        Public Const DefaultTaxRatePercent As String = "tax.defaultRatePercent"
        Public Const LowStockScanEnabled As String = "inventory.lowStockScanEnabled"

        Private Sub New()
        End Sub
    End Class

    Public NotInheritable Class SettingsService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock)
            MyBase.New(uow, currentUser, clock)
        End Sub

        Public Function GetString(key As String, Optional fallback As String = "") As String
            Dim row = Uow.Repository(Of AppSetting)().Query().FirstOrDefault(Function(s) s.Key = key)
            Return If(row Is Nothing, fallback, row.Value)
        End Function

        Public Function GetDecimal(key As String, Optional fallback As Decimal = 0D) As Decimal
            Dim raw = GetString(key, Nothing)
            Dim parsed As Decimal
            Return If(raw IsNot Nothing AndAlso Decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, parsed),
                      parsed, fallback)
        End Function

        Public Function GetBool(key As String, Optional fallback As Boolean = False) As Boolean
            Dim raw = GetString(key, Nothing)
            Dim parsed As Boolean
            Return If(raw IsNot Nothing AndAlso Boolean.TryParse(raw, parsed), parsed, fallback)
        End Function

        Public Function All() As Result(Of IReadOnlyDictionary(Of String, String))
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden(Of IReadOnlyDictionary(Of String, String))()
            Dim map As IReadOnlyDictionary(Of String, String) =
                Uow.Repository(Of AppSetting)().Query().ToDictionary(Function(s) s.Key, Function(s) s.Value)
            Return Result(Of IReadOnlyDictionary(Of String, String)).Ok(map)
        End Function

        Public Function SetValue(key As String, value As String) As Result
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()
            If String.IsNullOrWhiteSpace(key) Then Return Result.Fail("Key is required.")

            Dim repo = Uow.Repository(Of AppSetting)()
            Dim row = repo.Query().FirstOrDefault(Function(s) s.Key = key)
            If row Is Nothing Then
                repo.Add(New AppSetting With {.Key = key.Trim(), .Value = If(value, String.Empty)})
            Else
                row.Value = If(value, String.Empty)
                repo.Update(row)
            End If
            Uow.SaveChanges()
            Return Result.Ok()
        End Function
    End Class

End Namespace
