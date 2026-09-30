Namespace Models

    ''' <summary>Base type for every persisted entity. Integer identity key.</summary>
    Public MustInherit Class EntityBase
        Public Property Id As Integer

        ''' <summary>
        ''' Identifies the row across every copy of the data, where <see cref="Id"/> does not.
        ''' </summary>
        ''' <remarks>
        ''' The desktop client can create rows while offline, and the id it gives them locally is
        ''' not the id PostgreSQL gives them later. This value is chosen when the object is
        ''' created and never changes, so the sync can tell "already sent" from "new" after a
        ''' crash mid-send, and match a server row to its local copy.
        ''' </remarks>
        Public Property SyncId As Guid = Guid.NewGuid()
    End Class

    ''' <summary>
    ''' Entity that carries create/update stamps. These are populated centrally
    ''' (by the unit of work / interceptor) rather than by individual services.
    ''' </summary>
    Public MustInherit Class AuditableEntity
        Inherits EntityBase

        Public Property CreatedAtUtc As DateTime
        Public Property CreatedByUserId As Integer?
        Public Property UpdatedAtUtc As DateTime?
        Public Property UpdatedByUserId As Integer?
    End Class

End Namespace
