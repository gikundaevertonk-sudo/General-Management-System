Imports System.Linq
Imports System.Reflection
Imports GMS.Core.Abstractions
Imports GMS.Core.Models

Namespace Repositories.InMemory

    ''' <summary>In-memory <see cref="IRepository(Of T)"/> backed by <see cref="InMemoryDatabase"/>.</summary>
    Friend NotInheritable Class InMemoryRepository(Of T As Class)
        Implements IRepository(Of T)

        Private ReadOnly _db As InMemoryDatabase
        Private ReadOnly _items As List(Of T)
        Private ReadOnly _tenant As ITenantContext

        ''' <summary>
        ''' The entity's OrganizationId property, or Nothing for types that have none. Resolved
        ''' once per T rather than per row.
        ''' </summary>
        Private Shared ReadOnly OrganizationIdProperty As PropertyInfo =
            GetType(T).GetProperty("OrganizationId", GetType(Integer))

        Public Sub New(db As InMemoryDatabase, Optional tenant As ITenantContext = Nothing)
            _db = db
            _items = db.Set(Of T)()
            _tenant = tenant
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
                StampTenant(entity)
                _items.Add(entity)
            End SyncLock
        End Sub

        Public Sub AddRange(entities As IEnumerable(Of T)) Implements IRepository(Of T).AddRange
            SyncLock _db.SyncRoot
                For Each entity In entities
                    AssignId(entity)
                    StampTenant(entity)
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

        ''' <summary>
        ''' Puts the current organization on a new row that does not name one, mirroring
        ''' <c>GmsDbContext.StampTenantOnNewRows</c>.
        ''' </summary>
        ''' <remarks>
        ''' Without this the two stores disagree about something fundamental. Most services never
        ''' set OrganizationId themselves - they rely on that stamp, which only exists on the EF
        ''' path - so in this store their rows sat on organization 0 forever. Nothing complained,
        ''' because this store applies no tenant filter either, so each tenant's own pages still
        ''' listed them. But anything grouping or counting *by* organization read zero, and no test
        ''' written against this store could show that one tenant's rows stay out of another's.
        '''
        ''' Only when the value is 0, exactly as EF does, so an explicit assignment always wins -
        ''' onboarding sets it deliberately for a tenant that is not yet the current one.
        '''
        ''' A tenant that cannot be resolved leaves the value alone rather than throwing. EF would
        ''' throw here, but there are no foreign keys in this store to be violated, and a
        ''' database-free local run should not be brought down by a reminder or an audit row raised
        ''' from outside any tenant.
        ''' </remarks>
        Private Sub StampTenant(entity As T)
            If OrganizationIdProperty Is Nothing OrElse _tenant Is Nothing Then Return
            If CInt(OrganizationIdProperty.GetValue(entity)) <> 0 Then Return

            Try
                OrganizationIdProperty.SetValue(entity, _tenant.OrganizationId)
            Catch
                ' No resolvable tenant. See above.
            End Try
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
