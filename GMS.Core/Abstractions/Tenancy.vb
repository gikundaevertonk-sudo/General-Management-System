Namespace Abstractions

    ''' <summary>Provides access to the current tenant context within service operations.</summary>
    Public Interface ITenantContext
        ''' <summary>The ID of the current organization (tenant).</summary>
        ReadOnly Property OrganizationId As Integer

        ''' <summary>Whether this is a system/administrative context (bypasses tenant isolation).</summary>
        ReadOnly Property IsSystemMode As Boolean
    End Interface

End Namespace
