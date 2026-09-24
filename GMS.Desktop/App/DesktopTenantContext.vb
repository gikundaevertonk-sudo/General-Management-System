Imports GMS.Core.Abstractions
Imports GMS.Core.Data

Namespace App

    Public Class DesktopTenantContext
        Implements ITenantContext

        Private ReadOnly _sessionContext As SessionContext

        Public Sub New(sessionContext As SessionContext)
            _sessionContext = sessionContext
        End Sub

        ''' <summary>
        ''' The signed-in user's organization, or the default one before sign-in.
        ''' </summary>
        ''' <remarks>
        ''' Deliberately does not throw when no one is signed in. <see cref="GmsDbContext"/>
        ''' reads this for every tenant-scoped query, and start-up seeding runs before the
        ''' sign-in dialog is ever shown — throwing here would break launch. Until a principal
        ''' is established the session sits on the default organization, which is exactly the
        ''' row <c>DataSeeder.SeedDefaultOrganization</c> creates.
        ''' </remarks>
        Public ReadOnly Property OrganizationId As Integer Implements ITenantContext.OrganizationId
            Get
                Return _sessionContext.TenantId
            End Get
        End Property

        Public ReadOnly Property IsSystemMode As Boolean Implements ITenantContext.IsSystemMode
            Get
                Return _sessionContext.SystemMode
            End Get
        End Property

    End Class

End Namespace
