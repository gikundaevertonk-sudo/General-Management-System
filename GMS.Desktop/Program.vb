Imports System.Windows.Forms
Imports GMS.Core.Data
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Global.GMS.Desktop

    Friend Module Program

        <STAThread>
        Friend Sub Main()
            Application.SetHighDpiMode(HighDpiMode.SystemAware)
            Application.EnableVisualStyles()
            Application.SetCompatibleTextRenderingDefault(False)

            Using host = AppHost.Current
                host.EnsureSeeded()
                WarnIfDemoMode(host)

                Do
                    If Not RunSignIn(host) Then Exit Do

                    Dim main As New MainForm()
                    Application.Run(main)

                    If Not main.SignOutRequested Then Exit Do
                    host.Session.SignOut()
                Loop
            End Using
        End Sub

        ''' <summary>
        ''' Says out loud that nothing will be saved when no database is configured.
        ''' </summary>
        ''' <remarks>
        ''' Without a connection string the app runs against an in-memory store that is thrown
        ''' away on exit. That used to be completely silent, and looked to the user like the
        ''' system "forgetting" its password and resetting to the default on every launch.
        ''' Failing loudly here is the difference between a config mistake and a mystery.
        ''' </remarks>
        Private Sub WarnIfDemoMode(host As AppHost)
            If host.UsingDatabase Then Return

            MessageBox.Show(
                "No database is configured, so this session is running in demo mode." & vbCrLf & vbCrLf &
                "Anything you enter - products, sales, users, even a password change - is held " &
                "in memory only and is permanently lost when you close this window." & vbCrLf & vbCrLf &
                "To connect a database, set the Gms connection string in appsettings.json next " &
                "to GMS.Desktop.exe, or in the ConnectionStrings__Gms environment variable.",
                "GMS - demo mode, nothing will be saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning)
        End Sub

        ''' <summary>Shows the login dialog, handling a forced password change. Returns False if the user gave up.</summary>
        Private Function RunSignIn(host As AppHost) As Boolean
            Do
                Dim principal As Core.Contracts.AuthenticatedUser
                Using login As New LoginForm()
                    If login.ShowDialog() <> DialogResult.OK Then Return False
                    principal = login.Principal
                End Using

                If principal.MustChangePassword Then
                    Using change As New ChangePasswordForm(principal.UserId, forced:=True)
                        If change.ShowDialog() <> DialogResult.OK Then Continue Do
                    End Using
                    principal = host.Resolve(Of AuthService)().GetPrincipal(principal.UserId).Value
                End If

                ' Setting these two is all that scopes the session: GmsDbContext reads the
                ' tenant through ITenantContext -> SessionContext on every query.
                host.Session.Principal = principal
                host.Session.TenantId = principal.OrganizationId
                Return True
            Loop
        End Function

    End Module

End Namespace
