Namespace Common

    ''' <summary>Thrown for programming errors (null args, invalid state) — not for
    ''' expected validation failures, which are returned as <c>Result.Fail</c>.</summary>
    Public Class ServiceException
        Inherits Exception

        Public Sub New(message As String)
            MyBase.New(message)
        End Sub

        Public Sub New(message As String, inner As Exception)
            MyBase.New(message, inner)
        End Sub
    End Class

    ''' <summary>Terse argument guards for use at the top of public methods.</summary>
    Public Module Guard

        Public Function NotNull(Of T As Class)(value As T, Optional name As String = "value") As T
            If value Is Nothing Then Throw New ArgumentNullException(name)
            Return value
        End Function

        Public Function NotNullOrWhiteSpace(value As String, Optional name As String = "value") As String
            If String.IsNullOrWhiteSpace(value) Then
                Throw New ArgumentException("Value cannot be null or whitespace.", name)
            End If
            Return value
        End Function

        Public Function Positive(value As Decimal, Optional name As String = "value") As Decimal
            If value <= 0D Then Throw New ArgumentOutOfRangeException(name, "Value must be greater than zero.")
            Return value
        End Function

        Public Function NotNegative(value As Decimal, Optional name As String = "value") As Decimal
            If value < 0D Then Throw New ArgumentOutOfRangeException(name, "Value cannot be negative.")
            Return value
        End Function

    End Module

End Namespace
