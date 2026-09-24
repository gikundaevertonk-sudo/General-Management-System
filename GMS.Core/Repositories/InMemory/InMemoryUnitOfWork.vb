Imports System.Collections.Concurrent
Imports System.Linq
Imports GMS.Core.Abstractions

Namespace Repositories.InMemory

    ''' <summary>
    ''' In-memory <see cref="IUnitOfWork"/>. Changes are applied to the shared store
    ''' immediately, so <see cref="SaveChanges"/> only reports how many mutating
    ''' calls have happened since it was last called. Register as scoped.
    ''' </summary>
    Public NotInheritable Class InMemoryUnitOfWork
        Implements IUnitOfWork

        Private ReadOnly _db As InMemoryDatabase
        Private ReadOnly _repositories As New ConcurrentDictionary(Of Type, Object)()
        Private _pendingChangeCount As Integer

        Public Sub New(db As InMemoryDatabase)
            _db = db
        End Sub

        Public Function Repository(Of T As Class)() As IRepository(Of T) Implements IUnitOfWork.Repository
            Return DirectCast(
                _repositories.GetOrAdd(GetType(T), Function(keyType) New TrackingRepository(Of T)(_db, AddressOf NoteChange)),
                IRepository(Of T))
        End Function

        Public Function SaveChanges() As Integer Implements IUnitOfWork.SaveChanges
            Dim count = _pendingChangeCount
            _pendingChangeCount = 0
            Return count
        End Function

        Private Sub NoteChange()
            _pendingChangeCount += 1
        End Sub

        ''' <summary>Wraps <see cref="InMemoryRepository(Of T)"/> to count mutations for <c>SaveChanges</c>.</summary>
        Private NotInheritable Class TrackingRepository(Of T As Class)
            Implements IRepository(Of T)

            Private ReadOnly _inner As InMemoryRepository(Of T)
            Private ReadOnly _onChange As Action

            Public Sub New(db As InMemoryDatabase, onChange As Action)
                _inner = New InMemoryRepository(Of T)(db)
                _onChange = onChange
            End Sub

            Public Function GetById(id As Integer) As T Implements IRepository(Of T).GetById
                Return _inner.GetById(id)
            End Function

            Public Function Query() As IQueryable(Of T) Implements IRepository(Of T).Query
                Return _inner.Query()
            End Function

            Public Function QueryAcrossTenants() As IQueryable(Of T) Implements IRepository(Of T).QueryAcrossTenants
                Return _inner.QueryAcrossTenants()
            End Function

            Public Function List() As IReadOnlyList(Of T) Implements IRepository(Of T).List
                Return _inner.List()
            End Function

            Public Sub Add(entity As T) Implements IRepository(Of T).Add
                _inner.Add(entity) : _onChange()
            End Sub

            Public Sub AddRange(entities As IEnumerable(Of T)) Implements IRepository(Of T).AddRange
                Dim list = entities.ToList()
                _inner.AddRange(list)
                For i = 1 To list.Count : _onChange() : Next
            End Sub

            Public Sub Update(entity As T) Implements IRepository(Of T).Update
                _inner.Update(entity) : _onChange()
            End Sub

            Public Sub Remove(entity As T) Implements IRepository(Of T).Remove
                _inner.Remove(entity) : _onChange()
            End Sub
        End Class
    End Class

End Namespace
