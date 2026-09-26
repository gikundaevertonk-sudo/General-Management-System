Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Change the current user's password. Used both on demand and when forced at first sign-in.</summary>
    Public Class ChangePasswordForm

        Private ReadOnly _userId As Integer

        Public Sub New(userId As Integer, Optional forced As Boolean = False)
            InitializeComponent()
            DesktopTheme.Attach(Me)
            Icon = UiKit.AppIcon
            _userId = userId

            If forced Then
                Text = "Set a new password"
                lblInstruction.Text = "Your password must be changed before continuing."
                ControlBox = False
                btnCancel.Visible = False
            End If
        End Sub

        Private Sub btnOk_Click(sender As Object, e As EventArgs) Handles btnOk.Click
            lblError.Text = ""
            If txtNew.Text <> txtConfirm.Text Then
                lblError.Text = "The new password and its confirmation do not match."
                Return
            End If

            Dim auth = AppHost.Current.Resolve(Of AuthService)()
            Dim result = auth.ChangePassword(_userId, txtCurrent.Text, txtNew.Text)
            If result.Failed Then
                lblError.Text = result.ErrorMessage
                Return
            End If

            DialogResult = DialogResult.OK
            Close()
        End Sub

        Private Sub btnCancel_Click(sender As Object, e As EventArgs) Handles btnCancel.Click
            Close()
        End Sub
    End Class

End Namespace
