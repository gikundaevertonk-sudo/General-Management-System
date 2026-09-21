Imports GMS.Core.Abstractions

Namespace App

    Public Class DesktopTenantContext
        Implements ITenantContext

        Private ReadOnly _sessionContext As SessionContext

        Public Sub New(sessionContext As SessionContext)
            _sessionContext = sessionContext
        End Sub

        Public ReadOnly Property OrganizationId As Integer Implements ITenantContext.OrganizationId
            Get
                If _sessionContext.Principal Is Nothing Then
                    Throw New InvalidOperationException("No principal in session context")
                End If
                Return _sessionContext.TenantId
            End Get
        End Property

        Public ReadOnly Property IsSystemMode As Boolean Implements ITenantContext.IsSystemMode
            Get
                Return False
            End Get
        End Property

    End Class

End Namespace
