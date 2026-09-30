Imports System.Threading.Tasks
Imports GMS.Core.Contracts
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Username / password sign-in. Exposes <see cref="Principal"/> on success.</summary>
    Public Class LoginForm

        Public Property Principal As AuthenticatedUser

        Public Sub New()
            InitializeComponent()
            DesktopTheme.Attach(Me)
            Icon = UiKit.AppIcon
            txtUser.Text = "admin"
        End Sub

        ''' <remarks>
        ''' Runs off the UI thread: signing in online also brings this computer's copy of the
        ''' organization up to date, which on the first sign-in is a full download.
        ''' </remarks>
        Private Async Sub btnSignIn_Click(sender As Object, e As EventArgs) Handles btnSignIn.Click
            lblError.Text = ""
            btnSignIn.Enabled = False
            Dim caption = btnSignIn.Text
            btnSignIn.Text = "Signing in..."
            UseWaitCursor = True
            Try
                Dim userName = txtUser.Text.Trim()
                Dim password = txtPass.Text
                Dim organization = txtOrg.Text.Trim()
                Dim result = Await Task.Run(Function() AppHost.Current.SignIn(userName, password, organization))
                If result.Failed Then
                    lblError.Text = result.ErrorMessage
                    txtPass.SelectAll()
                    txtPass.Focus()
                    Return
                End If

                Principal = result.Value
                DialogResult = DialogResult.OK
                Close()
            Catch ex As Exception
                lblError.Text = ex.Message
            Finally
                UseWaitCursor = False
                btnSignIn.Text = caption
                btnSignIn.Enabled = True
            End Try
        End Sub
    End Class

End Namespace
