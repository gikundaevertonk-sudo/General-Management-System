Imports System.Linq
Imports System.Text.Json
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>Before/after pair for one field, used when recording an update.</summary>
    Public Structure FieldChange
        Public ReadOnly Property OldValue As Object
        Public ReadOnly Property NewValue As Object

        Public Sub New(oldValue As Object, newValue As Object)
            Me.OldValue = oldValue
            Me.NewValue = newValue
        End Sub
    End Structure

    ''' <summary>
    ''' Writes and queries the audit trail. Until the EF Core layer adds a
    ''' <c>SaveChanges</c> interceptor, mutating services call <see cref="Record"/>
    ''' explicitly after a successful change.
    ''' </summary>
    Public NotInheritable Class AuditService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
        End Sub

        ''' <summary>Record one change. <paramref name="changes"/> maps field name to its before/after values.</summary>
        Public Sub Record(entityName As String, entityId As String, action As AuditAction,
                          Optional changes As IReadOnlyDictionary(Of String, FieldChange) = Nothing)

            Dim payload = If(changes Is Nothing, "{}",
                JsonSerializer.Serialize(
                    changes.ToDictionary(Function(kv) kv.Key,
                                         Function(kv) New With {.old = kv.Value.OldValue, .[new] = kv.Value.NewValue})))

            Uow.Repository(Of AuditEntry)().Add(New AuditEntry With {
                .EntityName = entityName,
                .EntityId = entityId,
                .Action = action,
                .ChangesJson = payload,
                .UserId = CurrentUser.UserId,
                .UserName = If(CurrentUser.UserName, "system"),
                .TimestampUtc = Clock.UtcNow
            })
        End Sub

        Public Function Query(options As QueryOptions,
                              Optional entityName As String = Nothing) As Result(Of PagedResult(Of AuditEntry))

            If Denied(PermissionCodes.Audit.View) Then Return Forbidden(Of PagedResult(Of AuditEntry))()

            Dim q = Uow.Repository(Of AuditEntry)().Query()

            If Not String.IsNullOrWhiteSpace(entityName) Then
                q = q.Where(Function(a) a.EntityName = entityName)
            End If

            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim()
                q = q.Where(Function(a) a.UserName.Contains(term) OrElse a.EntityName.Contains(term) OrElse a.EntityId.Contains(term))
            End If

            Dim total = q.Count()
            Dim page = q.OrderByDescending(Function(a) a.TimestampUtc).
                         Skip(options.Skip).Take(options.PageSize).ToList()

            Return Result(Of PagedResult(Of AuditEntry)).Ok(
                New PagedResult(Of AuditEntry)(page, total, options.Page, options.PageSize))
        End Function
    End Class

End Namespace
