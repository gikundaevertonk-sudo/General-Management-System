Imports GMS.Core.Abstractions
Imports GMS.Core.DependencyInjection
Imports GMS.Core.Services
Imports Microsoft.Extensions.Configuration
Imports Microsoft.Extensions.DependencyInjection

Namespace App

    ''' <summary>
    ''' Owns the dependency-injection container for the desktop client. The whole
    ''' app runs inside one DI scope (single user, single process). Persistence is
    ''' PostgreSQL (Supabase) when a connection string is configured
    ''' (ConnectionStrings:Gms via user-secrets/env var), otherwise the in-memory
    ''' store, which lives in the root provider so data survives for the session.
    ''' </summary>
    Public NotInheritable Class AppHost
        Implements IDisposable

        Private Shared _instance As AppHost

        Private ReadOnly _root As ServiceProvider
        Private ReadOnly _scope As IServiceScope
        Private ReadOnly _usingDatabase As Boolean

        Public ReadOnly Property Session As SessionContext

        Private Sub New()
            Dim config = New ConfigurationBuilder().
                AddUserSecrets(Of AppHost)(optional:=True).
                AddEnvironmentVariables().
                Build()
            Dim connectionString = config.GetConnectionString("Gms")
            _usingDatabase = Not String.IsNullOrWhiteSpace(connectionString)

            Dim services As New ServiceCollection()
            If _usingDatabase Then
                services.AddGmsCorePostgres(connectionString)
            Else
                services.AddGmsCore()
            End If
            services.AddSingleton(Of SessionContext)()
            services.AddSingleton(Of ICurrentUser, DesktopCurrentUser)()

            _root = services.BuildServiceProvider(validateScopes:=False)
            _scope = _root.CreateScope()
            Session = _root.GetRequiredService(Of SessionContext)()
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

        Public Function Resolve(Of T)() As T
            Return _scope.ServiceProvider.GetRequiredService(Of T)()
        End Function

        ''' <summary>
        ''' Seeds baseline rows on every start-up (idempotent). Demo data is only added
        ''' against the in-memory store — never injected into a shared database.
        ''' </summary>
        Public Sub EnsureSeeded()
            Session.SystemMode = True
            Try
                Dim seeder = Resolve(Of DataSeeder)()
                seeder.SeedBaseline()
                If Not _usingDatabase Then
                    seeder.SeedDemo(
                        Resolve(Of CategoryService)(),
                        Resolve(Of ProductService)(),
                        Resolve(Of TransactionService)(),
                        Resolve(Of CustomerService)(),
                        Resolve(Of SupplierService)())
                End If
            Finally
                Session.SystemMode = False
            End Try
        End Sub

        Public Sub Dispose() Implements IDisposable.Dispose
            _scope.Dispose()
            _root.Dispose()
        End Sub
    End Class

End Namespace
