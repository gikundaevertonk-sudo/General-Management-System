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

            Dim seeder = New DataSeeder(Uow, New Pbkdf2PasswordHasher(), Clock)
            seeder.SeedBaseline(org.Id)

            Uow.SaveChanges()

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

        ''' <summary>Get organization by ID (admin only).</summary>
        Public Function GetById(id As Integer) As Result(Of Organization)
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden(Of Organization)()

            Dim org = Uow.Repository(Of Organization)().GetById(id)
            If org Is Nothing Then Return NotFound(Of Organization)("Organization")
            Return Result(Of Organization).Ok(org)
        End Function

        ''' <summary>List all organizations (admin only).</summary>
        Public Function ListAll() As Result(Of List(Of Organization))
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden(Of List(Of Organization))()

            Dim orgs = Uow.Repository(Of Organization)().Query().OrderBy(Function(o) o.Name).ToList()
            Return Result(Of List(Of Organization)).Ok(orgs)
        End Function

        ''' <summary>Update organization plan and subscription end date.</summary>
        Public Function UpdatePlan(orgId As Integer, plan As String, endsAt As DateTime) As Result
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()
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
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()

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
            If Denied(PermissionCodes.Settings.Manage) Then Return Forbidden()

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
