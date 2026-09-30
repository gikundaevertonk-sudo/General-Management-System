Imports System.Linq
Imports System.Text.Json
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
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

        ''' <summary>Entity name used for the sign-in rows that head each session.</summary>
        Public Const LoginEntityName As String = "Login"

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
        End Sub

        ''' <summary>
        ''' Records a successful sign-in, which is what turns the audit trail into a list of
        ''' sessions that everything else can be attributed to.
        ''' </summary>
        ''' <remarks>
        ''' Takes the user explicitly instead of reading <c>CurrentUser</c> like <see cref="Record"/>
        ''' does, because at this moment nobody is signed in yet - the front end has not built its
        ''' session or cookie. <paramref name="organizationId"/> is set for the same reason: the
        ''' tenant is not resolvable during sign-in, so leaving the stores to stamp it would write
        ''' a 0 and break the foreign key.
        ''' </remarks>
        Friend Sub RecordLogin(userId As Integer, userName As String, organizationId As Integer)
            Uow.Repository(Of AuditEntry)().Add(New AuditEntry With {
                .OrganizationId = organizationId,
                .EntityName = LoginEntityName,
                .EntityId = userId.ToString(),
                .Action = AuditAction.Login,
                .ChangesJson = "{}",
                .UserId = userId,
                .UserName = If(userName, String.Empty),
                .TimestampUtc = Clock.UtcNow
            })
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

        ''' <summary>
        ''' Who signed in and when, newest first, with a count of what each session went on to do.
        ''' </summary>
        ''' <remarks>
        ''' A session runs from one sign-in to that same account's next sign-in, because nothing
        ''' records a sign-out: closing the browser, the desktop app or the laptop lid all end a
        ''' session without telling the server. That makes the window an upper bound rather than an
        ''' exact one - the last session of the day runs until the next morning's sign-in - which is
        ''' the honest reading of the evidence there is, and still answers the question being asked:
        ''' what was done under this login.
        '''
        ''' The counts are gathered in two queries for the whole page rather than two per row; a
        ''' per-row lookup is what made the operator's organisation list slow enough to need fixing.
        ''' </remarks>
        Public Function LoginHistory(options As QueryOptions) As Result(Of PagedResult(Of LoginSessionRow))
            If Denied(PermissionCodes.Audit.View) Then Return Forbidden(Of PagedResult(Of LoginSessionRow))()

            Dim entries = Uow.Repository(Of AuditEntry)()
            Dim q = entries.Query().Where(Function(a) a.EntityName = LoginEntityName)
            If Not String.IsNullOrWhiteSpace(options.Search) Then
                Dim term = options.Search.Trim().ToLower()
                q = q.Where(Function(a) a.UserName.ToLower().Contains(term))
            End If

            Dim total = q.Count()
            Dim page = q.OrderByDescending(Function(a) a.TimestampUtc).ThenByDescending(Function(a) a.Id).
                Skip(options.Skip).Take(options.PageSize).ToList()
            If page.Count = 0 Then
                Return Result(Of PagedResult(Of LoginSessionRow)).Ok(PagedResult(Of LoginSessionRow).Empty(options))
            End If

            Dim userIds = page.Where(Function(a) a.UserId.HasValue).Select(Function(a) a.UserId.Value).Distinct().ToList()

            ' Every sign-in by the accounts on this page, so each row's session end is the next one
            ' along. Timestamps only - the rows themselves are not needed.
            Dim allLogins = entries.Query().
                Where(Function(a) a.EntityName = LoginEntityName AndAlso a.UserId.HasValue AndAlso userIds.Contains(a.UserId.Value)).
                Select(Function(a) New With {a.UserId, a.TimestampUtc}).ToList()

            Dim windowStart = page.Min(Function(a) a.TimestampUtc)
            Dim activity = entries.Query().
                Where(Function(a) a.EntityName <> LoginEntityName AndAlso
                                  a.UserId.HasValue AndAlso userIds.Contains(a.UserId.Value) AndAlso
                                  a.TimestampUtc >= windowStart).
                Select(Function(a) New With {a.UserId, a.TimestampUtc, a.EntityName, a.Action}).ToList()

            Dim users = Uow.Repository(Of User)().Query().
                Select(Function(u) New With {u.Id, u.FullName, u.ShopId}).ToList().
                ToDictionary(Function(u) u.Id)
            Dim shopNames = Uow.Repository(Of Shop)().Query().
                Select(Function(s) New With {s.Id, s.Name}).ToList().
                ToDictionary(Function(s) s.Id, Function(s) s.Name)

            Dim rows As New List(Of LoginSessionRow)()
            For Each login In page
                ' Unwrapped to a plain Integer before any comparison: two Integer? values compared
                ' with = yield Boolean?, which Option Strict will not accept as a condition.
                Dim uid = If(login.UserId, 0)
                Dim startedAt = login.TimestampUtc

                Dim endedAt As DateTime? = Nothing
                Dim later = allLogins.
                    Where(Function(l) If(l.UserId, 0) = uid AndAlso l.TimestampUtc > startedAt).
                    Select(Function(l) l.TimestampUtc).ToList()
                If later.Any() Then endedAt = later.Min()

                Dim closesAt = endedAt
                Dim during = activity.Where(
                    Function(a) If(a.UserId, 0) = uid AndAlso
                                a.TimestampUtc >= startedAt AndAlso
                                (Not closesAt.HasValue OrElse a.TimestampUtc < closesAt.Value)).ToList()

                Dim profile = If(login.UserId.HasValue, users.GetValueOrDefault(login.UserId.Value), Nothing)
                Dim shopName = String.Empty
                If profile IsNot Nothing AndAlso profile.ShopId.HasValue Then
                    shopName = shopNames.GetValueOrDefault(profile.ShopId.Value, String.Empty)
                End If

                rows.Add(New LoginSessionRow With {
                    .AuditEntryId = login.Id,
                    .UserId = login.UserId,
                    .UserName = login.UserName,
                    .FullName = If(profile?.FullName, String.Empty),
                    .ShopName = shopName,
                    .SignedInAtUtc = startedAt,
                    .EndedAtUtc = endedAt,
                    .ActionCount = during.Count,
                    .TransactionCount = during.Where(Function(a) a.EntityName = NameOf(Transaction) AndAlso
                                                                 a.Action = AuditAction.Create).Count()})
            Next

            Return Result(Of PagedResult(Of LoginSessionRow)).Ok(
                New PagedResult(Of LoginSessionRow)(rows, total, options.Page, options.PageSize))
        End Function

        ''' <summary>
        ''' Everything done during one sign-in, newest first. Transaction rows carry their number
        ''' and total so the caller can show and link to the actual document.
        ''' </summary>
        Public Function ActivityForLogin(auditEntryId As Integer) As Result(Of IReadOnlyList(Of SessionActivityRow))
            If Denied(PermissionCodes.Audit.View) Then Return Forbidden(Of IReadOnlyList(Of SessionActivityRow))()

            Dim entries = Uow.Repository(Of AuditEntry)()
            Dim login = entries.GetById(auditEntryId)
            If login Is Nothing OrElse login.EntityName <> LoginEntityName Then Return NotFound(Of IReadOnlyList(Of SessionActivityRow))("Sign-in")
            If Not login.UserId.HasValue Then
                Return Result(Of IReadOnlyList(Of SessionActivityRow)).Ok(New List(Of SessionActivityRow)())
            End If

            Dim userId = login.UserId.Value
            Dim start = login.TimestampUtc
            Dim nextLogin = entries.Query().
                Where(Function(a) a.EntityName = LoginEntityName AndAlso
                                  a.UserId.HasValue AndAlso a.UserId.Value = userId AndAlso
                                  a.TimestampUtc > start).
                OrderBy(Function(a) a.TimestampUtc).
                Select(Function(a) CType(a.TimestampUtc, DateTime?)).FirstOrDefault()

            Dim during = entries.Query().
                Where(Function(a) a.EntityName <> LoginEntityName AndAlso
                                  a.UserId.HasValue AndAlso a.UserId.Value = userId AndAlso
                                  a.TimestampUtc >= start).
                OrderByDescending(Function(a) a.TimestampUtc).ThenByDescending(Function(a) a.Id).
                ToList()
            If nextLogin.HasValue Then
                Dim ended = nextLogin.Value
                during = during.Where(Function(a) a.TimestampUtc < ended).ToList()
            End If

            ' One lookup for every transaction mentioned, rather than one per row.
            Dim txnIds = during.Where(Function(a) a.EntityName = NameOf(Transaction)).
                Select(Function(a) ParseId(a.EntityId)).
                Where(Function(id) id.HasValue).Select(Function(id) id.Value).Distinct().ToList()
            Dim txns = Uow.Repository(Of Transaction)().Query().
                Where(Function(t) txnIds.Contains(t.Id)).
                Select(Function(t) New With {t.Id, t.TransactionNumber, t.Total, t.Type, t.Status}).ToList().
                ToDictionary(Function(t) t.Id)

            Dim rows = during.Select(
                Function(a)
                    Dim row As New SessionActivityRow With {
                        .WhenUtc = a.TimestampUtc,
                        .Action = a.Action,
                        .EntityName = a.EntityName,
                        .EntityId = a.EntityId,
                        .ChangesJson = If(a.ChangesJson = "{}", String.Empty, a.ChangesJson),
                        .Summary = $"{a.Action.ToString().ToLower()}d {a.EntityName} #{a.EntityId}"}

                    If a.EntityName = NameOf(Transaction) Then
                        Dim id = ParseId(a.EntityId)
                        Dim txn = If(id.HasValue, txns.GetValueOrDefault(id.Value), Nothing)
                        If txn IsNot Nothing Then
                            row.TransactionId = txn.Id
                            row.TransactionNumber = txn.TransactionNumber
                            row.TransactionTotal = txn.Total
                            row.Summary = $"{txn.Type} {txn.TransactionNumber} ({txn.Status})"
                        End If
                    End If
                    Return row
                End Function).ToList()

            Return Result(Of IReadOnlyList(Of SessionActivityRow)).Ok(rows)
        End Function

        Private Shared Function ParseId(value As String) As Integer?
            Dim id As Integer
            Return If(Integer.TryParse(value, id), CType(id, Integer?), Nothing)
        End Function
    End Class

End Namespace
