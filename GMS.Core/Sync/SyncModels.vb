Imports Microsoft.EntityFrameworkCore
Imports GMS.Core.Data

Namespace Sync

    ''' <summary>What happened to a row, as far as the sync is concerned.</summary>
    Public Enum OutboxOperation
        Insert = 1
        Update = 2
        Delete = 3
    End Enum

    ''' <summary>
    ''' One change made on this computer that PostgreSQL has not seen yet. Local SQLite only.
    ''' </summary>
    ''' <remarks>
    ''' Written by <see cref="GmsDbContext"/> in the same SaveChanges as the change itself, so a
    ''' change and its record of needing to be sent are committed together or not at all.
    ''' </remarks>
    Public Class OutboxEntry
        ''' <summary>Increasing; the order changes were made in.</summary>
        Public Property Seq As Long
        ''' <summary>
        ''' The organization signed in when the change was made. One computer can be used by more
        ''' than one organization, and each sync sends only its own organization's changes.
        ''' </summary>
        Public Property OrganizationId As Integer
        ''' <summary>The CLR name of the entity, e.g. "Product".</summary>
        Public Property EntityName As String = String.Empty
        ''' <summary>The row's id in this local file (negative when created here).</summary>
        Public Property LocalId As Integer
        Public Property RowSyncId As Guid
        Public Property Operation As OutboxOperation
        ''' <summary>
        ''' Comma-separated property names this change touched. Only these are sent on an update,
        ''' so two people editing different fields of the same record offline both keep their edit.
        ''' Empty for inserts and deletes.
        ''' </summary>
        Public Property ChangedProperties As String = String.Empty
        Public Property CreatedAtUtc As DateTime
        ''' <summary>Why the last attempt to send this failed; empty while it has not failed.</summary>
        Public Property LastError As String = String.Empty
        Public Property Attempts As Integer
    End Class

    ''' <summary>
    ''' A row created on this computer (negative local id) and the id PostgreSQL gave it once sent.
    ''' Local SQLite only.
    ''' </summary>
    ''' <remarks>
    ''' Local ids are never rewritten after a row is sent. A screen may be holding that id in an
    ''' open edit form at the very moment the sync runs, and changing a primary key underneath it
    ''' would turn the user's save into an update of nothing. Translating at the boundary instead
    ''' keeps every id the screens know about valid forever.
    ''' </remarks>
    Public Class IdMapEntry
        Public Property EntityName As String = String.Empty
        Public Property LocalId As Integer
        Public Property ServerId As Integer
    End Class

    ''' <summary>Small key/value table for the sync's own bookkeeping. Local SQLite only.</summary>
    Public Class LocalSyncState
        Public Property Key As String = String.Empty
        Public Property Value As String = String.Empty

        Friend Shared Function Read(context As GmsDbContext, key As String) As String
            Return context.Set(Of LocalSyncState)().AsNoTracking().
                Where(Function(s) s.Key = key).Select(Function(s) s.Value).FirstOrDefault()
        End Function

        Friend Shared Sub Write(context As GmsDbContext, key As String, value As String)
            Dim updated = context.Set(Of LocalSyncState)().
                Where(Function(s) s.Key = key).
                ExecuteUpdate(Sub(setters) setters.SetProperty(Function(s) s.Value, value))
            If updated = 0 Then
                context.Database.ExecuteSqlInterpolated(
                    $"INSERT INTO local_sync_state (key, value) VALUES ({key}, {value})")
            End If
        End Sub
    End Class

End Namespace
