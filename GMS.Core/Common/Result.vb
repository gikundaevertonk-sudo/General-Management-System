Namespace Common

    ''' <summary>Outcome of a service operation that returns no value.</summary>
    Public Class Result

        Public ReadOnly Property Succeeded As Boolean
        Public ReadOnly Property Errors As IReadOnlyList(Of String)

        Public ReadOnly Property Failed As Boolean
            Get
                Return Not Succeeded
            End Get
        End Property

        ''' <summary>All errors joined with "; ", or empty when succeeded.</summary>
        Public ReadOnly Property ErrorMessage As String
            Get
                Return String.Join("; ", Errors)
            End Get
        End Property

        Protected Sub New(succeeded As Boolean, errors As IEnumerable(Of String))
            Me.Succeeded = succeeded
            Me.Errors = If(errors, Enumerable.Empty(Of String)()).ToList()
        End Sub

        Public Shared Function Ok() As Result
            Return New Result(True, Nothing)
        End Function

        Public Shared Function Fail(ParamArray errors As String()) As Result
            Return New Result(False, errors)
        End Function

        Public Shared Function Fail(errors As IEnumerable(Of String)) As Result
            Return New Result(False, errors)
        End Function
    End Class

    ''' <summary>Outcome of a service operation that returns a value on success.</summary>
    Public NotInheritable Class Result(Of T)
        Inherits Result

        Public ReadOnly Property Value As T

        Private Sub New(succeeded As Boolean, value As T, errors As IEnumerable(Of String))
            MyBase.New(succeeded, errors)
            Me.Value = value
        End Sub

        Public Overloads Shared Function Ok(value As T) As Result(Of T)
            Return New Result(Of T)(True, value, Nothing)
        End Function

        Public Overloads Shared Function Fail(ParamArray errors As String()) As Result(Of T)
            Return New Result(Of T)(False, Nothing, errors)
        End Function

        Public Overloads Shared Function Fail(errors As IEnumerable(Of String)) As Result(Of T)
            Return New Result(Of T)(False, Nothing, errors)
        End Function

        ''' <summary>Re-wrap a failed non-generic result as <c>Result(Of T)</c>.</summary>
        Public Shared Function FromError(source As Result) As Result(Of T)
            Return New Result(Of T)(False, Nothing, source.Errors)
        End Function
    End Class

End Namespace
