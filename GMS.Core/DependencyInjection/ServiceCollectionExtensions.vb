Imports System.Runtime.CompilerServices
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Caching.Memory
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.DependencyInjection.Extensions
Imports GMS.Core.Abstractions
Imports GMS.Core.Data
Imports GMS.Core.Repositories.Ef
Imports GMS.Core.Repositories.InMemory
Imports GMS.Core.Security
Imports GMS.Core.Services

Namespace DependencyInjection

    Public Module ServiceCollectionExtensions

        ''' <summary>
        ''' Registers GMS.Core against the in-memory store: the service layer, password
        ''' hashing, the clock, and an <see cref="IUnitOfWork"/> that keeps data only for
        ''' the life of the process. Used by GMS.Desktop and for local, database-free runs.
        ''' Each front end must also register its own <see cref="ICurrentUser"/>.
        ''' </summary>
        <Extension>
        Public Function AddGmsCore(services As IServiceCollection) As IServiceCollection
            AddCommon(services)
            services.TryAddSingleton(Of InMemoryDatabase)()
            ' Built by hand rather than by type so ITenantContext can be optional: GetService
            ' returns Nothing when a caller has not registered one, and the store then leaves
            ' OrganizationId alone instead of failing to resolve a dependency it can live without.
            services.TryAddScoped(Of IUnitOfWork)(
                Function(sp) New InMemoryUnitOfWork(
                    sp.GetRequiredService(Of InMemoryDatabase)(),
                    sp.GetService(Of ITenantContext)()))
            Return services
        End Function

        ''' <summary>
        ''' Registers GMS.Core against PostgreSQL (Supabase or any Postgres server) via
        ''' EF Core. The schema is owned by <c>db/supabase/schema.sql</c> — this does not
        ''' run migrations, it only maps to the tables that already exist.
        ''' Each front end must also register its own <see cref="ICurrentUser"/>.
        ''' </summary>
        <Extension>
        Public Function AddGmsCorePostgres(services As IServiceCollection, connectionString As String) As IServiceCollection
            AddCommon(services)

            ' Do NOT set Npgsql.EnableLegacyTimestampBehavior: it doesn't just relax the
            ' Kind=Utc requirement for 'timestamp with time zone' columns, it also converts
            ' the value using the machine's LOCAL time zone on write — silently shifting every
            ' stored timestamp by the local UTC offset. The correct fix, already applied at the
            ' source (DateRange's constructor, TransactionService.CreateDraft), is to ensure
            ' every DateTime reaching EF is genuinely DateTime.SpecifyKind(_, DateTimeKind.Utc).

            services.AddDbContext(Of GmsDbContext)(
                Sub(options)
                    options.UseNpgsql(connectionString)
                    ' Every service reads an entity, mutates it, then calls Repository.Update
                    ' (which explicitly attaches + marks Modified) — never relies on EF's change
                    ' tracker to notice edits. Defaulting to no-tracking means a second read in
                    ' the same DbContext always reflects the database, not a stale first read.
                    ' That matters once more than one process (desktop + web) writes the same rows.
                    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                End Sub)
            services.TryAddScoped(Of IUnitOfWork, EfUnitOfWork)()
            Return services
        End Function

        ''' <summary>
        ''' <see cref="AddGmsCore"/> (in-memory) plus a non-interactive <see cref="ICurrentUser"/>
        ''' that passes every permission check. For console tools, seeding and tests.
        ''' </summary>
        <Extension>
        Public Function AddGmsCoreForTooling(services As IServiceCollection) As IServiceCollection
            services.AddGmsCore()
            services.AddSingleton(Of ICurrentUser, SystemCurrentUser)()
            Return services
        End Function

        Private Sub AddCommon(services As IServiceCollection)
            services.TryAddSingleton(Of IClock, SystemClock)()
            services.TryAddSingleton(Of IPasswordHasher)(Function(sp) New Pbkdf2PasswordHasher())
            services.AddMemoryCache()

            services.AddScoped(Of AuditService)()
            services.AddScoped(Of AuthService)()
            services.AddScoped(Of SettingsService)()
            services.AddScoped(Of UserService)()
            services.AddScoped(Of RoleService)()
            services.AddScoped(Of CategoryService)()
            services.AddScoped(Of ProductService)()
            services.AddScoped(Of CustomerService)()
            services.AddScoped(Of SupplierService)()
            services.AddScoped(Of InventoryService)()
            services.AddScoped(Of NotificationService)()
            services.AddScoped(Of TransactionService)()
            services.AddScoped(Of ReportService)()
            services.AddScoped(Of DashboardService)()
            services.AddScoped(Of DataSeeder)()
            services.AddScoped(Of OrganizationService)()
            services.AddScoped(Of SubscriptionService)()
        End Sub
    End Module

End Namespace
