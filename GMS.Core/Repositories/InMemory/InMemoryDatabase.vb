Imports System.Collections.Concurrent
Imports System.Threading
Imports GMS.Core.Models

Namespace Repositories.InMemory

    ''' <summary>
    ''' Process-wide store that stands in for the real database until the Entity
    ''' Framework Core layer is added. Register it as a singleton. It keeps one
    ''' <see cref="List(Of Object)"/> per entity type and hands out identity values.
    ''' </summary>
    Public NotInheritable Class InMemoryDatabase

        Private ReadOnly _sets As New ConcurrentDictionary(Of Type, System.Collections.IList)()
        Private ReadOnly _sync As New Object()
        Private _nextId As Integer = 0

        ''' <summary>The backing list for <typeparamref name="T"/>, created on first use.</summary>
        Public Function [Set](Of T As Class)() As List(Of T)
            Return DirectCast(_sets.GetOrAdd(GetType(T), Function(keyType) New List(Of T)()), List(Of T))
        End Function

        Public Function NextId() As Integer
            Return Interlocked.Increment(_nextId)
        End Function

        Public ReadOnly Property SyncRoot As Object
            Get
                Return _sync
            End Get
        End Property

        Public Sub Clear()
            SyncLock _sync
                _sets.Clear()
                _nextId = 0
            End SyncLock
        End Sub

    End Class

End Namespace
