Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public Class SubscriptionService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock)
            MyBase.New(uow, currentUser, clock)
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
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()

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
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()

            Dim subRepo = Uow.Repository(Of Subscription)()
            Dim subscription = subRepo.Query().FirstOrDefault(Function(s) s.OrganizationId = organizationId)

            If subscription Is Nothing Then Return Result.Fail("Subscription not found.")

            subscription.PaymentStatus = "expired"
            subscription.BillingCycleEndAtUtc = Clock.UtcNow

            subRepo.Update(subscription)
            Uow.SaveChanges()

            Return Result.Ok()
        End Function

    End Class

End Namespace
