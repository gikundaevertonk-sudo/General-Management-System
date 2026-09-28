Imports System.Linq
Imports System.Linq.Expressions
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>What an organization contains, for the operator console's overview.</summary>
    Public NotInheritable Class TenantStats
        Public Property UserCount As Integer
        Public Property ProductCount As Integer
        Public Property TransactionCount As Integer
        Public Property CustomerCount As Integer
        Public Property LastSignInUtc As DateTime?
    End Class

    ''' <summary>A tenant's user as the system owner sees it. Never carries the password hash.</summary>
    Public NotInheritable Class TenantUser
        Public Property Id As Integer
        Public Property UserName As String = String.Empty
        Public Property FullName As String = String.Empty
        Public Property RoleName As String = String.Empty
        Public Property IsActive As Boolean
        Public Property MustChangePassword As Boolean
        Public Property LastLoginUtc As DateTime?
    End Class

    Public Class OrganizationService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
            _tenantContext = tenantContext
        End Sub

        Private ReadOnly _tenantContext As ITenantContext

        ''' <summary>Create a new trial organization.</summary>
        Public Function CreateTrial(name As String, code As String, email As String) As Result(Of Organization)
            If DeniedPlatform() Then Return Forbidden(Of Organization)()
            If String.IsNullOrWhiteSpace(name) Then Return Result(Of Organization).Fail("Organization name is required.")
            If String.IsNullOrWhiteSpace(code) Then Return Result(Of Organization).Fail("Organization code is required.")

            Dim orgRepo = Uow.Repository(Of Organization)()

            If orgRepo.Query().Any(Function(o) o.Code.ToLower() = code.ToLower()) Then
                Return Result(Of Organization).Fail("Organization code already exists.")
            End If

            Dim trialEndsAt = Clock.UtcNow.AddDays(30)
            Dim org = New Organization With {
                .Name = name,
                .Code = code.ToLower(),
                .Email = email,
                .Plan = "trial",
                .TrialEndsAtUtc = trialEndsAt,
                .IsActive = True,
                .MaxUsers = 5,
                .CreatedAtUtc = Clock.UtcNow
            }

            orgRepo.Add(org)
            Uow.SaveChanges()

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = New Subscription With {
                .OrganizationId = org.Id,
                .Plan = "trial",
                .PlanName = "Trial",
                .PricePerMonth = 0,
                .AutoRenew = False,
                .BillingCycleStartAtUtc = Clock.UtcNow,
                .BillingCycleEndAtUtc = trialEndsAt,
                .PaymentStatus = "active",
                .CreatedAtUtc = Clock.UtcNow
            }

            subRepo.Add(subscription)

            ' The organization row is committed by the SaveChanges above, because the store has
            ' to assign its id before a subscription or a user can reference it. There is no
            ' transaction spanning the two, so a failure here leaves an organization that exists
            ' but has no administrator and no settings - one nobody can ever sign in to. That is
            ' exactly what happened when this ran from the operator console, which belongs to no
            ' tenant: the seeder's tenant-filtered reads tried to resolve a current tenant and
            ' threw. Say so plainly rather than letting it surface as a 500 that looks like the
            ' organization was never created at all.
            Try
                Dim seeder = New DataSeeder(Uow, New Pbkdf2PasswordHasher(), Clock)
                seeder.SeedBaseline(org.Id)
                Uow.SaveChanges()
            Catch ex As Exception
                Return Result(Of Organization).Fail(
                    $"'{org.Code}' was created but could not be set up, so nobody can sign in to it yet. " &
                    $"It needs removing from the database by hand. Cause: {ex.Message}")
            End Try

            Return Result(Of Organization).Ok(org)
        End Function

        ''' <summary>Get organization by code.</summary>
        ' GetByCode was removed. Nothing called it - sign-in resolves the organization itself, from
        ' inside AuthService, where it can explain why a code was rejected. What was left was an
        ' ungated lookup that would hand any organization to any caller who found it, which is not
        ' something to leave lying around for someone to wire up later.

        ''' <summary>Get organization by ID (system owner only).</summary>
        Public Function GetById(id As Integer) As Result(Of Organization)
            If DeniedPlatform() Then Return Forbidden(Of Organization)()

            Dim org = Uow.Repository(Of Organization)().GetById(id)
            If org Is Nothing Then Return NotFound(Of Organization)("Organization")
            Return Result(Of Organization).Ok(org)
        End Function

        ''' <summary>List all organizations (system owner only).</summary>
        Public Function ListAll() As Result(Of List(Of Organization))
            If DeniedPlatform() Then Return Forbidden(Of List(Of Organization))()

            Dim orgs = Uow.Repository(Of Organization)().Query().OrderBy(Function(o) o.Name).ToList()
            Return Result(Of List(Of Organization)).Ok(orgs)
        End Function

        ''' <summary>
        ''' The caller's own organization, with no permission check at all.
        ''' </summary>
        ''' <remarks>
        ''' Reading your own tenant's trial and suspension state is not an administrative act -
        ''' it is what decides whether you are allowed in at all, so it has to work for every
        ''' user of the tenant. TrialExpiryMiddleware used to call <see cref="GetById"/> for
        ''' this and swallow the Forbidden it got back for anyone without settings.manage,
        ''' which quietly let ordinary staff of a suspended or lapsed organization straight
        ''' through. There is nothing to leak: the id comes from the caller's own tenant
        ''' context, so this can only ever return the organization they already belong to.
        ''' </remarks>
        Public Function GetOwnOrganization() As Result(Of Organization)
            Dim org = Uow.Repository(Of Organization)().GetById(TenantContext.OrganizationId)
            If org Is Nothing Then Return NotFound(Of Organization)("Organization")
            Return Result(Of Organization).Ok(org)
        End Function

        ''' <summary>
        ''' Whether an organization has run out of paid-up time and should be locked out.
        ''' </summary>
        ''' <remarks>
        ''' The single definition, so the operator console and TrialExpiryMiddleware cannot
        ''' disagree about who is cut off. An organization with no dates at all is never
        ''' lapsed; otherwise at least one of its two end dates has to still be in the future.
        '''
        ''' The old test required a trial end date in the past, which meant a paid customer
        ''' whose TrialEndsAtUtc was never set could not be locked out however long their
        ''' subscription had been expired.
        ''' </remarks>
        Public Shared Function IsLapsed(org As Organization, asOfUtc As DateTime) As Boolean
            If org Is Nothing Then Return False

            Dim hasAnyDate = org.TrialEndsAtUtc.HasValue OrElse org.SubscriptionEndsAtUtc.HasValue
            If Not hasAnyDate Then Return False

            Dim trialStillValid = org.TrialEndsAtUtc.HasValue AndAlso org.TrialEndsAtUtc.Value >= asOfUtc
            Dim subscriptionStillValid = org.SubscriptionEndsAtUtc.HasValue AndAlso org.SubscriptionEndsAtUtc.Value >= asOfUtc
            Return Not trialStillValid AndAlso Not subscriptionStillValid
        End Function

        ' UpdatePlan was removed. It set a plan and an end date and had never had a single caller;
        ' SubscriptionService.SetSubscription does the same thing properly - it also keeps the
        ' subscription row's start date, clears the stale trial date that would otherwise override
        ' the expiry, and can record no end date at all. Two ways to write the same two columns, one
        ' of them unreachable, is how they drift apart.

        ''' <summary>Suspend an organization (blocks all access).</summary>
        Public Function Suspend(orgId As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim org = Uow.Repository(Of Organization)().GetById(orgId)
            If org Is Nothing Then Return NotFound("Organization")

            org.IsActive = False
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId

            Uow.Repository(Of Organization)().Update(org)
            Uow.SaveChanges()

            Return Result.Ok()
        End Function

        ''' <summary>Activate a suspended organization.</summary>
        Public Function Activate(orgId As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim org = Uow.Repository(Of Organization)().GetById(orgId)
            If org Is Nothing Then Return NotFound("Organization")

            org.IsActive = True
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId

            Uow.Repository(Of Organization)().Update(org)
            Uow.SaveChanges()

            Return Result.Ok()
        End Function

        ''' <summary>
        ''' What an organization contains. Counts only - never its rows.
        ''' </summary>
        ''' <remarks>
        ''' QueryAcrossTenants throughout: the system owner is looking at a tenant they do not
        ''' belong to, which the tenant filter would otherwise scope away to nothing. Every
        ''' query matches orgId explicitly, so one tenant's figures can never include another's.
        ''' Counts and a sign-in date are deliberately all it returns; running the business is
        ''' the tenant's job, and the owner has no reason to read their customers or takings.
        '''
        ''' Everything but UserCount reads zero against the in-memory store, and that is not a
        ''' fault here. Services never set OrganizationId themselves - GmsDbContext stamps it on
        ''' new rows in SaveChanges - and the in-memory store has no equivalent, so its products,
        ''' customers and transactions all sit on organization 0. Users are counted correctly
        ''' because onboarding assigns their organization explicitly. Judge these figures against
        ''' PostgreSQL.
        ''' </remarks>
        Public Function GetStats(orgId As Integer) As Result(Of TenantStats)
            If DeniedPlatform() Then Return Forbidden(Of TenantStats)()

            Dim users = Uow.Repository(Of User)().QueryAcrossTenants().Where(Function(u) u.OrganizationId = orgId)
            Dim stats As New TenantStats With {
                .UserCount = users.Count(),
                .ProductCount = Uow.Repository(Of Product)().QueryAcrossTenants().Count(Function(p) p.OrganizationId = orgId),
                .TransactionCount = Uow.Repository(Of Transaction)().QueryAcrossTenants().Count(Function(t) t.OrganizationId = orgId),
                .CustomerCount = Uow.Repository(Of Customer)().QueryAcrossTenants().Count(Function(c) c.OrganizationId = orgId),
                .LastSignInUtc = users.Where(Function(u) u.LastLoginUtc.HasValue).
                                       OrderByDescending(Function(u) u.LastLoginUtc).
                                       Select(Function(u) u.LastLoginUtc).FirstOrDefault()
            }
            Return Result(Of TenantStats).Ok(stats)
        End Function

        ''' <summary>
        ''' The same figures as <see cref="GetStats"/>, for every organization at once, keyed by id.
        ''' </summary>
        ''' <remarks>
        ''' Five grouped queries for the whole list, however many tenants there are, rather than
        ''' <see cref="GetStats"/> once per row - which was five queries per tenant, so fifty
        ''' customers meant two hundred and fifty round trips to build one page. Over a connection
        ''' pooler that is the difference between a page that opens and one that hangs.
        '''
        ''' An organization with nothing in it simply has no group to appear in, so callers must
        ''' treat a missing key as zero rather than as unknown.
        ''' </remarks>
        Public Function ListStats() As Result(Of Dictionary(Of Integer, TenantStats))
            If DeniedPlatform() Then Return Forbidden(Of Dictionary(Of Integer, TenantStats))()

            Dim stats As New Dictionary(Of Integer, TenantStats)()
            Dim entry = Function(orgId As Integer) As TenantStats
                            Dim s As TenantStats = Nothing
                            If Not stats.TryGetValue(orgId, s) Then
                                s = New TenantStats()
                                stats(orgId) = s
                            End If
                            Return s
                        End Function

            Dim users = Uow.Repository(Of User)().QueryAcrossTenants().
                GroupBy(Function(u) u.OrganizationId).
                Select(Function(g) New With {
                    .OrgId = g.Key,
                    .Total = g.Count(),
                    .LastSignIn = g.Max(Function(u) u.LastLoginUtc)
                }).ToList()
            For Each row In users
                Dim s = entry(row.OrgId)
                s.UserCount = row.Total
                s.LastSignInUtc = row.LastSignIn
            Next

            For Each row In CountBy(Of Product)(Function(p) p.OrganizationId)
                entry(row.Key).ProductCount = row.Value
            Next
            For Each row In CountBy(Of Transaction)(Function(t) t.OrganizationId)
                entry(row.Key).TransactionCount = row.Value
            Next
            For Each row In CountBy(Of Customer)(Function(c) c.OrganizationId)
                entry(row.Key).CustomerCount = row.Value
            Next

            Return Result(Of Dictionary(Of Integer, TenantStats)).Ok(stats)
        End Function

        ''' <summary>
        ''' Row counts per organization for one tenant-owned entity type.
        ''' </summary>
        ''' <remarks>
        ''' The organization id is passed as an expression rather than reached through a shared
        ''' interface, because the entities do not have one - OrganizationId is simply a property
        ''' each declares for itself. An expression keeps the grouping on the database.
        ''' </remarks>
        Private Function CountBy(Of T As Class)(orgIdOf As Expression(Of Func(Of T, Integer))) _
                                              As Dictionary(Of Integer, Integer)
            Return Uow.Repository(Of T)().QueryAcrossTenants().
                GroupBy(orgIdOf).
                Select(Function(g) New With {.OrgId = g.Key, .Total = g.Count()}).
                ToList().
                ToDictionary(Function(r) r.OrgId, Function(r) r.Total)
        End Function

        ''' <summary>A tenant's users, so the owner can see who to reset when nobody can get in.</summary>
        Public Function ListUsers(orgId As Integer) As Result(Of List(Of TenantUser))
            If DeniedPlatform() Then Return Forbidden(Of List(Of TenantUser))()

            Dim roles = Uow.Repository(Of Role)().Query().ToDictionary(Function(r) r.Id, Function(r) r.Name)
            Dim list = Uow.Repository(Of User)().QueryAcrossTenants().
                Where(Function(u) u.OrganizationId = orgId).
                OrderBy(Function(u) u.UserName).
                ToList().
                Select(Function(u) New TenantUser With {
                    .Id = u.Id,
                    .UserName = u.UserName,
                    .FullName = u.FullName,
                    .RoleName = roles.GetValueOrDefault(u.RoleId, "-"),
                    .IsActive = u.IsActive,
                    .MustChangePassword = u.MustChangePassword,
                    .LastLoginUtc = u.LastLoginUtc
                }).ToList()

            Return Result(Of List(Of TenantUser)).Ok(list)
        End Function

        ''' <summary>
        ''' Sets a temporary password for one of a tenant's users and forces a change at their
        ''' next sign-in.
        ''' </summary>
        ''' <remarks>
        ''' There is no self-service reset, and a tenant's administrator cannot reset the very
        ''' account they are locked out of - so without this the only way back in is writing a
        ''' hash into the database by hand. MustChangePassword is not optional here: the
        ''' temporary password passes through the owner, so it must not stay usable afterwards.
        ''' </remarks>
        Public Function ResetUserPassword(orgId As Integer, userId As Integer, temporaryPassword As String) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim errors = AuthService.ValidateNewPassword(temporaryPassword)
            If errors.Any() Then Return Result.Fail(errors)

            Dim repo = Uow.Repository(Of User)()
            ' Matched on both ids, so naming a user from another tenant finds nothing rather
            ' than resetting somebody else's password.
            Dim user = repo.QueryAcrossTenants().
                FirstOrDefault(Function(u) u.Id = userId AndAlso u.OrganizationId = orgId)
            If user Is Nothing Then Return NotFound("User")

            user.PasswordHash = New Pbkdf2PasswordHasher().Hash(temporaryPassword)
            user.MustChangePassword = True
            user.FailedLoginCount = 0
            user.LockedOutUntilUtc = Nothing
            user.UpdatedAtUtc = Clock.UtcNow
            repo.Update(user)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Sets how many users an organization is allowed. Returns how many it currently has, so
        ''' the caller can say whether the new limit is already exceeded.
        ''' </summary>
        ''' <remarks>
        ''' Its own method rather than part of <see cref="UpdateDetails"/>, because the two are
        ''' answers to different questions - what the organization is called, and what it is paying
        ''' for - and having one form post both is how a stale field silently overwrites the other.
        '''
        ''' A limit below the current head count is allowed, deliberately. A customer dropping from
        ''' seven seats to five is a real thing to record, and refusing would leave the operator
        ''' unable to write down what was actually agreed. The consequence is only that nobody new
        ''' can be added until they are back under it - UserService.Create counts before it creates -
        ''' so no existing user is locked out by this.
        ''' </remarks>
        Public Function SetUserLimit(orgId As Integer, maxUsers As Integer) As Result(Of Integer)
            If DeniedPlatform() Then Return Forbidden(Of Integer)()
            If maxUsers < 1 Then Return Result(Of Integer).Fail("The user limit must be at least 1.")

            Dim repo = Uow.Repository(Of Organization)()
            Dim org = repo.GetById(orgId)
            If org Is Nothing Then Return NotFound(Of Integer)("Organization")

            org.MaxUsers = maxUsers
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId
            repo.Update(org)
            Uow.SaveChanges()

            Dim inUse = Uow.Repository(Of User)().QueryAcrossTenants().
                Count(Function(u) u.OrganizationId = orgId)
            Return Result(Of Integer).Ok(inUse)
        End Function

        ''' <summary>Rename an organization or change its contact address and user cap.</summary>
        Public Function UpdateDetails(orgId As Integer, name As String, email As String, maxUsers As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()
            If String.IsNullOrWhiteSpace(name) Then Return Result.Fail("Organization name is required.")
            If maxUsers < 1 Then Return Result.Fail("The user cap must be at least 1.")

            Dim repo = Uow.Repository(Of Organization)()
            Dim org = repo.GetById(orgId)
            If org Is Nothing Then Return NotFound("Organization")

            ' Code is deliberately not editable: it is what a tenant's users type to sign in,
            ' so changing it would lock every one of them out with no warning.
            org.Name = name.Trim()
            org.Email = If(email, String.Empty).Trim()
            org.MaxUsers = maxUsers
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId

            repo.Update(org)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Removes an organization and everything belonging to it.
        ''' </summary>
        ''' <remarks>
        ''' Every child row is deleted here by hand rather than left to ON DELETE CASCADE.
        ''' Cascades would cover most of it under PostgreSQL, but not all: transaction_lines
        ''' has no organization_id, so the cascade from organizations never reaches it. And the
        ''' in-memory store has no foreign keys at all, so a cascade-only delete would strand
        ''' that tenant's users there while reporting success.
        '''
        ''' One SaveChanges at the end, so a failure part-way cannot leave a half-deleted
        ''' tenant behind: EF Core wraps the batch in a transaction and orders the deletes from
        ''' the relationships in the model.
        '''
        ''' Irreversible, and the caller is expected to have confirmed. The default
        ''' organization is refused outright: it is the one the system itself was seeded
        ''' around, and deleting it is not a recoverable mistake.
        ''' </remarks>
        Public Function DeleteOrganization(orgId As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(orgId)
            If org Is Nothing Then Return NotFound("Organization")
            If String.Equals(org.Code, DataSeeder.DefaultOrganizationCode, StringComparison.OrdinalIgnoreCase) Then
                Return Result.Fail("The default organization cannot be deleted.")
            End If

            ' transaction_lines is reached through its parent transactions, being the one table
            ' belonging to a tenant that does not say so itself.
            Dim txnIds = Uow.Repository(Of Transaction)().QueryAcrossTenants().
                Where(Function(t) t.OrganizationId = orgId).Select(Function(t) t.Id).ToList()
            Purge(Of TransactionLine)(Function(x) txnIds.Contains(x.TransactionId))

            Purge(Of ShopStock)(Function(x) x.OrganizationId = orgId)
            Purge(Of StockMovement)(Function(x) x.OrganizationId = orgId)
            Purge(Of Transaction)(Function(x) x.OrganizationId = orgId)
            Purge(Of Product)(Function(x) x.OrganizationId = orgId)
            Purge(Of Category)(Function(x) x.OrganizationId = orgId)
            Purge(Of Customer)(Function(x) x.OrganizationId = orgId)
            Purge(Of Supplier)(Function(x) x.OrganizationId = orgId)
            Purge(Of AuditEntry)(Function(x) x.OrganizationId = orgId)
            Purge(Of Notification)(Function(x) x.OrganizationId = orgId)
            Purge(Of AppSetting)(Function(x) x.OrganizationId = orgId)
            Purge(Of Subscription)(Function(x) x.OrganizationId = orgId)
            Purge(Of User)(Function(x) x.OrganizationId = orgId)
            ' Shops last of the children: users reference them with ON DELETE RESTRICT, so the
            ' accounts have to be gone first.
            Purge(Of Shop)(Function(x) x.OrganizationId = orgId)

            orgRepo.Remove(org)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Marks every row matching <paramref name="match"/> for deletion, without saving.
        ''' </summary>
        ''' <remarks>
        ''' The predicate is an expression so EF Core turns it into a WHERE clause; passing a
        ''' compiled delegate instead would pull every tenant's rows into memory to filter them
        ''' here. QueryAcrossTenants because the operator does not belong to the tenant being
        ''' deleted, and each predicate re-establishes that scope itself.
        ''' </remarks>
        Private Sub Purge(Of T As Class)(match As Expression(Of Func(Of T, Boolean)))
            Dim repo = Uow.Repository(Of T)()
            For Each row In repo.QueryAcrossTenants().Where(match).ToList()
                repo.Remove(row)
            Next
        End Sub

    End Class

End Namespace
