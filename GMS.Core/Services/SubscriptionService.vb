Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public Class SubscriptionService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, tenantContext As ITenantContext, clock As IClock)
            MyBase.New(uow, currentUser, tenantContext, clock)
        End Sub

        ''' <summary>Check if organization's subscription is active.</summary>
        Public Function IsActive(organizationId As Integer) As Boolean
            Dim subscription = Uow.Repository(Of Subscription)().Query() _
                .FirstOrDefault(Function(s) s.OrganizationId = organizationId)

            If subscription Is Nothing Then Return False
            If subscription.PaymentStatus <> "active" Then Return False
            If subscription.BillingCycleEndAtUtc < Clock.UtcNow Then Return False

            Return True
        End Function

        ''' <summary>Get subscription for organization.</summary>
        Public Function GetByOrganizationId(organizationId As Integer) As Result(Of Subscription)
            Dim subscription = Uow.Repository(Of Subscription)().Query() _
                .FirstOrDefault(Function(s) s.OrganizationId = organizationId)

            If subscription Is Nothing Then Return Result(Of Subscription).Fail("Subscription not found.")
            Return Result(Of Subscription).Ok(subscription)
        End Function

        ''' <summary>
        ''' Sets an organization's subscription by hand: its plan, the day it runs from, and the
        ''' day it runs to - or no end at all.
        ''' </summary>
        ''' <remarks>
        ''' There is no payment provider wired in, so billing happens outside the system and the
        ''' operator records the result here. That makes this the honest shape: two dates the
        ''' operator chooses, rather than a machine pretending to know when money arrived. If a
        ''' provider is added later it can call this same method.
        '''
        ''' <paramref name="endsAtUtc"/> as Nothing means it never expires, and is stored as
        ''' Nothing on the organization rather than as a far-future date, because that is what
        ''' <see cref="OrganizationService.IsLapsed"/> reads as "nothing has been promised, so
        ''' nothing has run out". The subscription row cannot hold a null cycle end, so it gets a
        ''' date a century out purely to keep that column sane.
        '''
        ''' TrialEndsAtUtc is always cleared. Entitlement is "either date still in the future", so
        ''' leaving an old trial date alongside a deliberate end date gives two answers to one
        ''' question - and a stale future trial date would silently override an expiry the operator
        ''' had just set.
        ''' </remarks>
        Public Function SetSubscription(organizationId As Integer, plan As String,
                                        startsAtUtc As DateTime, endsAtUtc As DateTime?) As Result
            If DeniedPlatform() Then Return Forbidden()
            If String.IsNullOrWhiteSpace(plan) Then Return Result.Fail("A plan name is required.")
            If endsAtUtc.HasValue AndAlso endsAtUtc.Value <= startsAtUtc Then
                Return Result.Fail("The end date must be after the start date.")
            End If

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(organizationId)
            If org Is Nothing Then Return NotFound("Organization")

            Dim starts = DateTime.SpecifyKind(startsAtUtc, DateTimeKind.Utc)
            Dim ends As DateTime? = If(endsAtUtc.HasValue,
                                       DateTime.SpecifyKind(endsAtUtc.Value, DateTimeKind.Utc),
                                       CType(Nothing, DateTime?))

            org.Plan = plan.Trim().ToLowerInvariant()
            org.SubscriptionEndsAtUtc = ends
            org.TrialEndsAtUtc = Nothing
            org.UpdatedAtUtc = Clock.UtcNow
            org.UpdatedByUserId = CurrentUser.UserId
            orgRepo.Update(org)

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = subRepo.Query().FirstOrDefault(Function(s) s.OrganizationId = organizationId)
            If subscription Is Nothing Then
                subscription = New Subscription With {
                    .OrganizationId = organizationId,
                    .CreatedAtUtc = Clock.UtcNow
                }
                subRepo.Add(subscription)
            End If

            subscription.Plan = org.Plan
            subscription.PlanName = plan.Trim()
            subscription.BillingCycleStartAtUtc = starts
            subscription.BillingCycleEndAtUtc = If(ends.HasValue, ends.Value, starts.AddYears(100))
            subscription.PaymentStatus = "active"
            ' Nothing renews this on a timer - the operator does, by hand.
            subscription.AutoRenew = False
            subscription.CancelledAtUtc = Nothing
            subscription.UpdatedAtUtc = Clock.UtcNow
            subRepo.Update(subscription)

            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>
        ''' Pushes the end date out by <paramref name="days"/>.
        ''' </summary>
        ''' <remarks>
        ''' Counted from today when the subscription has already run out, not from the old end
        ''' date: adding thirty days to a customer who lapsed two months ago should give them
        ''' thirty days, not leave them still expired. An organization set never to expire is
        ''' refused rather than quietly given an end date it did not have.
        ''' </remarks>
        Public Function ExtendBy(organizationId As Integer, days As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()
            If days = 0 Then Return Result.Fail("Enter a number of days to add.")

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(organizationId)
            If org Is Nothing Then Return NotFound("Organization")

            Dim current = If(org.SubscriptionEndsAtUtc, org.TrialEndsAtUtc)
            If Not current.HasValue Then
                Return Result.Fail("This organization is set never to expire, so there is nothing to extend.")
            End If

            Dim from = If(current.Value > Clock.UtcNow, current.Value, Clock.UtcNow)
            Return SetSubscription(organizationId, org.Plan, org.CreatedAtUtc, from.AddDays(days))
        End Function

        ''' <summary>How much notice an organization gets that its subscription is ending.</summary>
        Public Const ExpiryWarningDays As Integer = 2

        ''' <summary>
        ''' Raises an in-app notification for a tenant's administrators when its subscription is
        ''' within <see cref="ExpiryWarningDays"/> days of running out. Returns how many were raised.
        ''' </summary>
        ''' <remarks>
        ''' Called on sign-in rather than from a timer, because there is no scheduler in this system
        ''' and inventing one for a single reminder would be a lot of moving parts to go wrong.
        ''' Sign-in is also the moment it is worth saying: someone is here and can act on it.
        '''
        ''' DedupeKey carries the end date, so the same expiry is only ever raised once per
        ''' administrator - but moving the end date raises a fresh one, which is right, because it
        ''' is then a different deadline.
        '''
        ''' Deliberately silent in three cases: no end date at all (nothing to warn about), an end
        ''' date already past (they are locked out and being told it is "about to end" would be
        ''' absurd), and an organization with no administrators (nobody to tell).
        ''' </remarks>
        Public Function WarnIfExpiringSoon(organizationId As Integer) As Integer
            Dim org = Uow.Repository(Of Organization)().GetById(organizationId)
            If org Is Nothing Then Return 0

            Dim endsAt = If(org.SubscriptionEndsAtUtc, org.TrialEndsAtUtc)
            If Not endsAt.HasValue Then Return 0

            Dim remaining = endsAt.Value - Clock.UtcNow
            If remaining <= TimeSpan.Zero Then Return 0
            If remaining > TimeSpan.FromDays(ExpiryWarningDays) Then Return 0

            ' QueryAcrossTenants with an explicit organization match: this runs from sign-in, before
            ' any tenant is established, so a filtered read would have nothing to filter on.
            Dim adminRole = Uow.Repository(Of Role)().Query().
                FirstOrDefault(Function(r) r.Name.ToLower() = "admin")
            If adminRole Is Nothing Then Return 0

            Dim admins = Uow.Repository(Of User)().QueryAcrossTenants().
                Where(Function(u) u.OrganizationId = organizationId AndAlso
                                  u.RoleId = adminRole.Id AndAlso u.IsActive).ToList()
            If admins.Count = 0 Then Return 0

            Dim notifications = Uow.Repository(Of Notification)()
            Dim key = $"subscription-expiry:{endsAt.Value:yyyy-MM-dd}"
            Dim existing = notifications.QueryAcrossTenants().
                Where(Function(n) n.OrganizationId = organizationId AndAlso n.DedupeKey = key).
                Select(Function(n) n.TargetUserId).ToList()

            Dim days = CInt(Math.Ceiling(remaining.TotalDays))
            Dim wording = If(days <= 1, "tomorrow", $"in {days} days")
            Dim raised = 0

            For Each admin In admins
                If existing.Contains(admin.Id) Then Continue For

                notifications.Add(New Notification With {
                    .OrganizationId = organizationId,
                    .Type = NotificationType.System,
                    .Severity = NotificationSeverity.Warning,
                    .Title = "Your subscription is about to end",
                    .Message = $"Access ends {wording}, on {endsAt.Value:d MMMM yyyy}. " &
                               "Renew it to avoid your staff being locked out.",
                    .TargetUserId = admin.Id,
                    .RelatedEntityName = NameOf(Organization),
                    .RelatedEntityId = organizationId.ToString(),
                    .DedupeKey = key,
                    .CreatedAtUtc = Clock.UtcNow
                })
                raised += 1
            Next

            If raised > 0 Then Uow.SaveChanges()
            Return raised
        End Function

        ''' <summary>Renew a subscription (extend billing cycle).</summary>
        Public Function Renew(organizationId As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = subRepo.Query().FirstOrDefault(Function(s) s.OrganizationId = organizationId)

            If subscription Is Nothing Then Return Result.Fail("Subscription not found.")

            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(organizationId)
            If org Is Nothing Then Return Result.Fail("Organization not found.")

            Dim daysToAdd = If(org.Plan = "trial", 30, 30)
            subscription.BillingCycleEndAtUtc = Clock.UtcNow.AddDays(daysToAdd)
            subscription.PaymentStatus = "active"

            subRepo.Update(subscription)

            org.SubscriptionEndsAtUtc = subscription.BillingCycleEndAtUtc
            If org.Plan = "trial" Then
                org.TrialEndsAtUtc = subscription.BillingCycleEndAtUtc
            End If

            orgRepo.Update(org)
            Uow.SaveChanges()

            Return Result.Ok()
        End Function

        ''' <summary>Expire a subscription (trial or paid).</summary>
        Public Function ExpireSubscription(organizationId As Integer) As Result
            If DeniedPlatform() Then Return Forbidden()

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = subRepo.Query().FirstOrDefault(Function(s) s.OrganizationId = organizationId)

            If subscription Is Nothing Then Return Result.Fail("Subscription not found.")

            subscription.PaymentStatus = "expired"
            subscription.BillingCycleEndAtUtc = Clock.UtcNow

            subRepo.Update(subscription)

            ' Pulling the organization's own dates back is what actually locks anyone out.
            ' Access is decided from these two columns, not from the subscription row, so
            ' expiring the subscription alone showed the tenant as unpaid on the console
            ' while its users carried on working as though nothing had happened.
            Dim orgRepo = Uow.Repository(Of Organization)()
            Dim org = orgRepo.GetById(organizationId)
            If org IsNot Nothing Then
                ' A second before now, not now. IsLapsed counts an end date equal to the moment
                ' asked about as still valid, so setting these to Clock.UtcNow left the tenant
                ' entitled for the rest of that instant - "mark unpaid" did not take effect at the
                ' moment it was applied. Milliseconds in practice, but the operator pressed a
                ' button that says it locks them out, so it should.
                Dim endedAt = Clock.UtcNow.AddSeconds(-1)
                org.SubscriptionEndsAtUtc = endedAt
                ' Only when it was set: leaving a future trial date in place would keep the
                ' organization inside its trial and undo the expiry.
                If org.TrialEndsAtUtc.HasValue Then org.TrialEndsAtUtc = endedAt
                orgRepo.Update(org)
            End If

            Uow.SaveChanges()

            Return Result.Ok()
        End Function

    End Class

End Namespace
