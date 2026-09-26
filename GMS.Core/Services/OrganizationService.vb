Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

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
        Public Function GetByCode(code As String) As Result(Of Organization)
            If String.IsNullOrWhiteSpace(code) Then Return NotFound(Of Organization)("Organization")

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.Query().FirstOrDefault(Function(o) o.Code.ToLower() = code.ToLower())

            If org Is Nothing Then Return NotFound(Of Organization)("Organization")
            Return Result(Of Organization).Ok(org)
        End Function

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

        ''' <summary>Update organization plan and subscription end date.</summary>
        Public Function UpdatePlan(orgId As Integer, plan As String, endsAt As DateTime) As Result
            If DeniedPlatform() Then Return Forbidden()
            If String.IsNullOrWhiteSpace(plan) Then Return Result.Fail("Plan is required.")

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(orgId)
            If org Is Nothing Then Return NotFound("Organization")

            org.Plan = plan
            org.SubscriptionEndsAtUtc = endsAt
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId

            orgRepo.Update(org)

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = subRepo.Query().FirstOrDefault(Function(s) s.OrganizationId = orgId)
            If subscription IsNot Nothing Then
                subscription.Plan = plan
                subscription.BillingCycleEndAtUtc = endsAt
                subRepo.Update(subscription)
            End If

            Uow.SaveChanges()
            Return Result.Ok()
        End Function

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

    End Class

End Namespace
