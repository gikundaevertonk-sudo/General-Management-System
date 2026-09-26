Imports GMS.Core.Contracts
Imports GMS.Core.Services
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

        Private Sub btnSignIn_Click(sender As Object, e As EventArgs) Handles btnSignIn.Click
            lblError.Text = ""
            btnSignIn.Enabled = False
            Try
                Dim auth = AppHost.Current.Resolve(Of AuthService)()
                Dim result = auth.SignInWithTenant(txtUser.Text.Trim(), txtPass.Text, txtOrg.Text.Trim())
                If result.Failed Then
                    lblError.Text = result.ErrorMessage
                    txtPass.SelectAll()
                    txtPass.Focus()
                    Return
                End If

                Principal = result.Value
                DialogResult = DialogResult.OK
                Close()
            Finally
                btnSignIn.Enabled = True
            End Try
        End Sub
    End Class

End Namespace
