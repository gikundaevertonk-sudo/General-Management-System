Imports System.Collections.Concurrent
Imports System.Runtime.CompilerServices
Imports System.Threading
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.ChangeTracking
Imports Microsoft.EntityFrameworkCore.ValueGeneration

Namespace Sync

    ''' <summary>
    ''' Hands out ids for rows created on this computer: -1, -2, -3...
    ''' </summary>
    ''' <remarks>
    ''' Every positive id in the local file came from PostgreSQL, so a row made here must never
    ''' take one - the server will sooner or later issue that same number to someone else's row,
    ''' and the pull would then collide with it. Negative ids cannot collide with anything the
    ''' server issues. The generator is only consulted for a new row whose Id is still 0, so rows
    ''' the sync copies down keep the server's id.
    ''' </remarks>
    Friend NotInheritable Class LocalIdGenerator
        Inherits ValueGenerator(Of Integer)

        ''' <summary>Last id issued, per database file and table.</summary>
        Private Shared ReadOnly _last As New ConcurrentDictionary(Of String, StrongBox(Of Integer))()

        Public Overrides ReadOnly Property GeneratesTemporaryValues As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides Function [Next](entry As EntityEntry) As Integer
            Dim table = entry.Metadata.GetTableName()
            Dim connection = entry.Context.Database.GetDbConnection()
            Dim key = connection.DataSource & "|" & table
            Dim box = _last.GetOrAdd(key, Function(k) New StrongBox(Of Integer)(LowestIn(entry.Context, table)))
            Return Interlocked.Decrement(box.Value)
        End Function

        ''' <summary>The smallest id already in the table, or 0 when none is negative.</summary>
        Private Shared Function LowestIn(context As DbContext, table As String) As Integer
            Dim connection = context.Database.GetDbConnection()
            Dim opened = connection.State <> System.Data.ConnectionState.Open
            If opened Then connection.Open()
            Try
                Using command = connection.CreateCommand()
                    command.CommandText = $"SELECT MIN(MIN(id), 0) FROM ""{table}"""
                    Dim value = command.ExecuteScalar()
                    If value Is Nothing OrElse TypeOf value Is DBNull Then Return 0
                    Return Convert.ToInt32(value, Globalization.CultureInfo.InvariantCulture)
                End Using
            Finally
                If opened Then connection.Close()
            End Try
        End Function

        ''' <summary>Forgets cached counters, for when a local file is deleted and recreated.</summary>
        Friend Shared Sub Reset()
            _last.Clear()
        End Sub
    End Class

End Namespace
