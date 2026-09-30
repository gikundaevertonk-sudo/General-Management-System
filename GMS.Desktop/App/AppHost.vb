Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Contracts
Imports GMS.Core.Data
Imports GMS.Core.DependencyInjection
Imports GMS.Core.Services
Imports GMS.Core.Sync
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection
Imports Npgsql

Namespace App

    ''' <summary>
    ''' Owns the dependency-injection container for the desktop client. The whole
    ''' app runs inside one DI scope (single user, single process).
    ''' </summary>
    ''' <remarks>
    ''' With a connection string configured (ConnectionStrings:Gms via appsettings.json,
    ''' user-secrets or an environment variable) every screen works against a SQLite copy on this
    ''' computer (<see cref="LocalStore"/>), and <see cref="DesktopSync"/> keeps that copy and
    ''' PostgreSQL in step whenever the server can be reached. That is what lets a shop keep
    ''' selling through an internet outage. Without one, the in-memory store is used and nothing
    ''' outlives the process.
    ''' </remarks>
    Public NotInheritable Class AppHost
        Implements IDisposable

        Private Shared _instance As AppHost

        Private ReadOnly _root As ServiceProvider
        Private ReadOnly _scope As IServiceScope
        Private ReadOnly _serverConnectionString As String
        Private _server As ServiceProvider

        Public ReadOnly Property Session As SessionContext

        ''' <summary>The background sync, or Nothing in demo mode.</summary>
        Public ReadOnly Property Sync As DesktopSync

        ''' <summary>
        ''' Set when an update to the application had to rebuild the local copy while it still
        ''' held changes that had not been sent; Program shows it once at start-up.
        ''' </summary>
        Public ReadOnly Property StartupNotice As String

        Private Sub New()
            ' Lowest priority first. appsettings.json is the only one of the three that ships
            ' with an installed copy: user-secrets live in the developer's own profile and are
            ' never published, so without the JSON file a packaged build had no way at all to
            ' be given a connection string and always fell back to the throwaway in-memory
            ' store. Base path is the executable's folder, not the working directory, so a
            ' desktop shortcut finds the file.
            Dim config = New ConfigurationBuilder().
                SetBasePath(AppContext.BaseDirectory).
                AddJsonFile("appsettings.json", optional:=True, reloadOnChange:=False).
                AddUserSecrets(Of AppHost)(optional:=True).
                AddEnvironmentVariables().
                Build()
            _serverConnectionString = config.GetConnectionString("Gms")
            Dim usingDatabase = Not String.IsNullOrWhiteSpace(_serverConnectionString)

            Session = New SessionContext()
            Dim device As New DesktopDevice()
            ' LocalStore:Path (LocalStore__Path in the environment) moves the file, e.g. off a
            ' small system drive. Otherwise it lives in the Windows user's local app data.
            Dim localPath = config("LocalStore:Path")
            If String.IsNullOrWhiteSpace(localPath) AndAlso usingDatabase Then
                localPath = LocalStore.DefaultPath(_serverConnectionString)
            End If

            Dim services As New ServiceCollection()
            If usingDatabase Then
                services.AddGmsCoreSqlite(localPath)
                services.AddSingleton(Of IDeviceIdentity)(device)
            Else
                services.AddGmsCore()
            End If
            AddSession(services)

            _root = services.BuildServiceProvider(validateScopes:=False)
            _scope = _root.CreateScope()

            If usingDatabase Then
                Dim local = _scope.ServiceProvider.GetRequiredService(Of GmsDbContext)()
                _StartupNotice = LocalStore.Open(local, localPath)
                device.Code = LocalStore.DeviceCode(local)
                _Sync = New DesktopSync(New SyncEngine(localPath, _serverConnectionString), Session)
            End If
        End Sub

        Private Sub AddSession(services As IServiceCollection)
            services.AddSingleton(Session)
            services.AddSingleton(Of ICurrentUser, DesktopCurrentUser)()
            services.AddScoped(Of ITenantContext, DesktopTenantContext)()
        End Sub

        Public Shared ReadOnly Property Current As AppHost
            Get
                If _instance Is Nothing Then _instance = New AppHost()
                Return _instance
            End Get
        End Property

        Public ReadOnly Property Services As IServiceProvider
            Get
                Return _scope.ServiceProvider
            End Get
        End Property

        ''' <summary>
        ''' False when no connection string was found and the app is running entirely in
        ''' memory, where everything entered is discarded when the process exits.
        ''' </summary>
        Public ReadOnly Property UsingDatabase As Boolean
            Get
                Return _Sync IsNot Nothing
            End Get
        End Property

        Public Function Resolve(Of T)() As T
            Return _scope.ServiceProvider.GetRequiredService(Of T)()
        End Function

        ''' <summary>
        ''' Forgets every row the screens' context is holding, so the next read comes from the
        ''' file. Called after a sync, which writes to the file through a context of its own.
        ''' </summary>
        Public Sub ResetTracking()
            If UsingDatabase Then Resolve(Of GmsDbContext)().ChangeTracker.Clear()
        End Sub

        ''' <summary>
        ''' Services wired straight to PostgreSQL, for the two things that must happen there
        ''' rather than in the local copy: seeding, and checking a password online.
        ''' </summary>
        Private Function ServerServices() As IServiceProvider
            If _server Is Nothing Then
                Dim services As New ServiceCollection()
                services.AddGmsCorePostgres(_serverConnectionString)
                AddSession(services)
                _server = services.BuildServiceProvider(validateScopes:=False)
            End If
            Return _server
        End Function

        ''' <summary>
        ''' Seeds baseline rows (idempotent). Demo data is only added against the in-memory
        ''' store - never injected into a shared database.
        ''' </summary>
        ''' <remarks>
        ''' With a database, seeding happens on PostgreSQL and the result arrives here with the
        ''' first sync; seeding the local copy directly would create a second set of roles and
        ''' an admin that the server has never heard of. Offline, there is nothing to do: a copy
        ''' that has synced before already has its baseline.
        ''' </remarks>
        Public Sub EnsureSeeded()
            Session.SystemMode = True
            Try
                If UsingDatabase Then
                    If Not Sync.Engine.IsServerReachable(timeoutSeconds:=3) Then Return
                    Using scope = ServerServices().CreateScope()
                        scope.ServiceProvider.GetRequiredService(Of DataSeeder)().SeedBaseline()
                    End Using
                    Return
                End If

                Dim seeder = Resolve(Of DataSeeder)()
                seeder.SeedBaseline()
                seeder.SeedDemo(
                    Resolve(Of CategoryService)(),
                    Resolve(Of ProductService)(),
                    Resolve(Of TransactionService)(),
                    Resolve(Of CustomerService)(),
                    Resolve(Of SupplierService)())
            Finally
                Session.SystemMode = False
            End Try
        End Sub

        ''' <summary>
        ''' Signs in online when the server can be reached - and brings this computer's copy of
        ''' the organization up to date before returning - otherwise against the local copy.
        ''' </summary>
        ''' <remarks>
        ''' Online is preferred because the server has the current password and the current
        ''' state of the account (deactivated, locked, subscription ended). A wrong password
        ''' online is final; only a failure to reach or query the server falls back to offline.
        ''' Runs on a background thread from the sign-in form, while nothing else is using the
        ''' screens' context.
        ''' </remarks>
        Public Function SignIn(userName As String, password As String, organizationCode As String) As Result(Of AuthenticatedUser)
            If Not UsingDatabase Then Return Resolve(Of AuthService)().SignInWithTenant(userName, password, organizationCode)

            If Sync.Engine.IsServerReachable() Then
                Try
                    Dim online As Result(Of AuthenticatedUser)
                    Using scope = ServerServices().CreateScope()
                        online = scope.ServiceProvider.GetRequiredService(Of AuthService)().
                            SignInWithTenant(userName, password, organizationCode)
                    End Using
                    If online.Failed Then Return online

                    Dim report = Sync.RunBlocking(online.Value.OrganizationId)
                    ' GetPrincipal reads through the tenant filter, which still points at the
                    ' pre-sign-in default until the session is scoped.
                    Session.TenantId = online.Value.OrganizationId
                    ' The screens read the local copy, so it must have this account in it. It will
                    ' unless this was the computer's first sign-in and the download broke off.
                    If Resolve(Of AuthService)().GetPrincipal(online.Value.UserId).Failed Then
                        Return Result(Of AuthenticatedUser).Fail(
                            "Signed in, but this computer's copy of your organization could not be " &
                            "downloaded: " & String.Join(" ", report.Errors) & " Try again.")
                    End If
                    Return online
                Catch ex As PostgresException When ex.SqlState = PostgresErrorCodes.UndefinedColumn
                    Return Result(Of AuthenticatedUser).Fail(
                        "The server database has not been upgraded for offline use yet. Run " &
                        "db/supabase/migrations/2026-09-30-offline-sync.sql in Supabase, then try again.")
                Catch ex As Exception When SyncEngine.IsConnectionFailure(ex)
                    ' Lost the connection mid-way: fall through and try the local copy.
                End Try
            End If

            Dim offline = Resolve(Of AuthService)().SignInWithTenant(userName, password, organizationCode)
            If offline.Failed AndAlso offline.ErrorMessage = "Organization not found." Then
                Return Result(Of AuthenticatedUser).Fail(
                    "Can't reach the server, and this computer has no copy of that organization yet. " &
                    "The first sign-in on a computer needs an internet connection.")
            End If
            Return offline
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            _Sync?.Dispose()
            _scope.Dispose()
            _root.Dispose()
            _server?.Dispose()
        End Sub

        ''' <summary>This installation's device code, known once the local copy is open.</summary>
        Private NotInheritable Class DesktopDevice
            Implements IDeviceIdentity

            Public Property Code As String

            Public ReadOnly Property DeviceCode As String Implements IDeviceIdentity.DeviceCode
                Get
                    Return Code
                End Get
            End Property
        End Class
    End Class

End Namespace
