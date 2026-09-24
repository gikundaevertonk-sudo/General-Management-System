Imports System.Collections.Generic
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore.ChangeTracking
Imports GMS.Core.Abstractions
Imports GMS.Core.Data

Namespace Repositories.Ef

    ''' <summary>EF Core-backed <see cref="IRepository(Of T)"/>. One instance per <see cref="GmsDbContext"/>.</summary>
    Friend NotInheritable Class EfRepository(Of T As Class)
        Implements IRepository(Of T)

        Private ReadOnly _context As GmsDbContext
        Private ReadOnly _set As DbSet(Of T)

        Public Sub New(context As GmsDbContext)
            _context = context
            _set = context.Set(Of T)()
        End Sub

        Public Function GetById(id As Integer) As T Implements IRepository(Of T).GetById
            Return _set.Find(id)
        End Function

        Public Function Query() As IQueryable(Of T) Implements IRepository(Of T).Query
            Return _set
        End Function

        Public Function QueryAcrossTenants() As IQueryable(Of T) Implements IRepository(Of T).QueryAcrossTenants
            ' Drops the global query filters declared in GmsDbContext.OnModelCreating, and with
            ' them the need for a resolvable tenant at all.
            Return _set.IgnoreQueryFilters()
        End Function

        Public Function List() As IReadOnlyList(Of T) Implements IRepository(Of T).List
            Return _set.ToList()
        End Function

        Public Sub Add(entity As T) Implements IRepository(Of T).Add
            _set.Add(entity)
        End Sub

        Public Sub AddRange(entities As IEnumerable(Of T)) Implements IRepository(Of T).AddRange
            _set.AddRange(entities)
        End Sub

        ''' <summary>
        ''' Marks <paramref name="entity"/> modified, tolerating a change tracker that already
        ''' holds a different instance of the same row.
        ''' </summary>
        ''' <remarks>
        ''' Queries default to <see cref="QueryTrackingBehavior.NoTracking"/>, so every read
        ''' hands back a fresh, untracked instance — but <c>Add</c> and <c>Attach</c> do track.
        ''' GMS.Desktop runs the whole application in a single DI scope, so one
        ''' <see cref="GmsDbContext"/> lives for the entire session and those tracked instances
        ''' accumulate: the user seeded at start-up, or any row edited earlier in the session.
        ''' Blindly attaching a second instance with the same primary key throws
        ''' "another instance with the same key value is already being tracked", so when the
        ''' tracker already knows the row we copy onto that instance instead of attaching.
        ''' </remarks>
        Public Sub Update(entity As T) Implements IRepository(Of T).Update
            Dim tracked = FindTracked(entity)
            If tracked IsNot Nothing Then
                If Not Object.ReferenceEquals(tracked.Entity, entity) Then
                    tracked.CurrentValues.SetValues(entity)
                End If
                tracked.State = EntityState.Modified
                Return
            End If

            _set.Attach(entity)
            _context.Entry(entity).State = EntityState.Modified
        End Sub

        Public Sub Remove(entity As T) Implements IRepository(Of T).Remove
            Dim tracked = FindTracked(entity)
            If tracked IsNot Nothing Then
                tracked.State = EntityState.Deleted
                Return
            End If

            _set.Attach(entity)
            _set.Remove(entity)
        End Sub

        ''' <summary>
        ''' The already-tracked entry for the same row as <paramref name="entity"/>, or Nothing.
        ''' Added entries are skipped: their keys are still store-generated placeholders, so two
        ''' unsaved rows would both compare equal on a default key.
        ''' </summary>
        Private Function FindTracked(entity As T) As EntityEntry(Of T)
            Dim key = KeyOf(entity)
            If key Is Nothing Then Return Nothing

            For Each entry In _context.ChangeTracker.Entries(Of T)()
                If entry.State = EntityState.Detached OrElse entry.State = EntityState.Added Then Continue For
                Dim other = KeyOf(entry.Entity)
                If other IsNot Nothing AndAlso String.Equals(other, key, StringComparison.Ordinal) Then
                    Return entry
                End If
            Next
            Return Nothing
        End Function

        ''' <summary>
        ''' The entity's primary key rendered as a comparable string, or Nothing when the key
        ''' cannot be read without the change tracker (shadow properties) or is unset.
        ''' </summary>
        Private Function KeyOf(entity As T) As String
            Dim entityType = _context.Model.FindEntityType(GetType(T))
            If entityType Is Nothing Then Return Nothing

            Dim primaryKey = entityType.FindPrimaryKey()
            If primaryKey Is Nothing Then Return Nothing

            Dim parts As New List(Of String)()
            For Each prop In primaryKey.Properties
                ' Reading through PropertyInfo rather than _context.Entry(entity) keeps this
                ' side-effect free: Entry() on a detached instance can begin tracking it, which
                ' is the very conflict this helper exists to avoid.
                If prop.PropertyInfo Is Nothing Then Return Nothing
                Dim value = prop.PropertyInfo.GetValue(entity)
                If value Is Nothing Then Return Nothing
                parts.Add(Convert.ToString(value, Globalization.CultureInfo.InvariantCulture))
            Next

            Return String.Join(ChrW(31), parts)
        End Function
    End Class

End Namespace
