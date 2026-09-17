Imports System.Windows.Forms
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

                Do
                    If Not RunSignIn(host) Then Exit Do

                    Dim main As New MainForm()
                    Application.Run(main)

                    If Not main.SignOutRequested Then Exit Do
                    host.Session.SignOut()
                Loop
            End Using
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

                host.Session.Principal = principal
                Return True
            Loop
        End Function

    End Module

End Namespace
