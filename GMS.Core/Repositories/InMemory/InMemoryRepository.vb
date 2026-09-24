Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Models

Namespace Repositories.InMemory

    ''' <summary>In-memory <see cref="IRepository(Of T)"/> backed by <see cref="InMemoryDatabase"/>.</summary>
    Friend NotInheritable Class InMemoryRepository(Of T As Class)
        Implements IRepository(Of T)

        Private ReadOnly _db As InMemoryDatabase
        Private ReadOnly _items As List(Of T)

        Public Sub New(db As InMemoryDatabase)
            _db = db
            _items = db.Set(Of T)()
        End Sub

        Public Function GetById(id As Integer) As T Implements IRepository(Of T).GetById
            SyncLock _db.SyncRoot
                Return _items.FirstOrDefault(Function(e) EntityId(e) = id)
            End SyncLock
        End Function

        Public Function Query() As IQueryable(Of T) Implements IRepository(Of T).Query
            ' Snapshot so callers can enumerate without holding the lock.
            SyncLock _db.SyncRoot
                Return _items.ToList().AsQueryable()
            End SyncLock
        End Function

        ''' <summary>
        ''' Identical to <see cref="Query"/>: this store applies no tenant filter in the first
        ''' place, which is exactly why it cannot be trusted to catch multi-tenancy mistakes.
        ''' </summary>
        Public Function QueryAcrossTenants() As IQueryable(Of T) Implements IRepository(Of T).QueryAcrossTenants
            Return Query()
        End Function

        Public Function List() As IReadOnlyList(Of T) Implements IRepository(Of T).List
            SyncLock _db.SyncRoot
                Return _items.ToList()
            End SyncLock
        End Function

        Public Sub Add(entity As T) Implements IRepository(Of T).Add
            SyncLock _db.SyncRoot
                AssignId(entity)
                _items.Add(entity)
            End SyncLock
        End Sub

        Public Sub AddRange(entities As IEnumerable(Of T)) Implements IRepository(Of T).AddRange
            SyncLock _db.SyncRoot
                For Each entity In entities
                    AssignId(entity)
                    _items.Add(entity)
                Next
            End SyncLock
        End Sub

        Public Sub Update(entity As T) Implements IRepository(Of T).Update
            ' Reference semantics: the stored instance is the same object. Nothing to do
            ' beyond confirming it is tracked. EF Core's implementation will differ.
            SyncLock _db.SyncRoot
                If Not _items.Contains(entity) Then
                    Dim id = EntityId(entity)
                    Dim existing = _items.FirstOrDefault(Function(e) EntityId(e) = id)
                    If existing IsNot Nothing Then
                        _items(_items.IndexOf(existing)) = entity
                    End If
                End If
            End SyncLock
        End Sub

        Public Sub Remove(entity As T) Implements IRepository(Of T).Remove
            SyncLock _db.SyncRoot
                _items.Remove(entity)
            End SyncLock
        End Sub

        Private Sub AssignId(entity As T)
            Dim eb = TryCast(entity, EntityBase)
            If eb IsNot Nothing AndAlso eb.Id = 0 Then
                eb.Id = _db.NextId()
            End If
        End Sub

        Private Shared Function EntityId(entity As T) As Integer
            Dim eb = TryCast(entity, EntityBase)
            Return If(eb IsNot Nothing, eb.Id, 0)
        End Function
    End Class

End Namespace
