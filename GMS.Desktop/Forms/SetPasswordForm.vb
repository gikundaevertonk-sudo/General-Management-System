Imports GMS.Desktop.App

Namespace Forms

    ''' <summary>Prompts for a new password (used for admin password resets).</summary>
    Public Class SetPasswordForm

        Public ReadOnly Property NewPassword As String
            Get
                Return txtNew.Text
            End Get
        End Property

        Public Sub New()
            InitializeComponent()
            DesktopTheme.Attach(Me)
        End Sub

        Private Sub btnOk_Click(sender As Object, e As EventArgs) Handles btnOk.Click
            If txtNew.Text <> txtConfirm.Text Then
                lblError.Text = "Passwords do not match."
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
