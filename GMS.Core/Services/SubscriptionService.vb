Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
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
                org.SubscriptionEndsAtUtc = Clock.UtcNow
                ' Only when it was set: leaving a future trial date in place would keep the
                ' organization inside its trial and undo the expiry.
                If org.TrialEndsAtUtc.HasValue Then org.TrialEndsAtUtc = Clock.UtcNow
                orgRepo.Update(org)
            End If

            Uow.SaveChanges()

            Return Result.Ok()
        End Function

    End Class

End Namespace
