Namespace Models

    ''' <summary>Base type for every persisted entity. Integer identity key.</summary>
    Public MustInherit Class EntityBase
        Public Property Id As Integer
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
