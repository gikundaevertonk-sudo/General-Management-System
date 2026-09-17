Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common

Namespace App

    ''' <summary>Small helpers to keep the forms terse and visually consistent.</summary>
    Public Module UiKit

        Public ReadOnly Accent As Color = Color.FromArgb(37, 99, 235)
        Public ReadOnly AccentDark As Color = Color.FromArgb(30, 64, 175)
        Public ReadOnly Sidebar As Color = Color.FromArgb(17, 24, 39)
        Public ReadOnly SidebarHover As Color = Color.FromArgb(31, 41, 55)
        Public ReadOnly PageBack As Color = Color.FromArgb(243, 244, 246)
        Public ReadOnly CardBack As Color = Color.White
        Public ReadOnly MutedText As Color = Color.FromArgb(107, 114, 128)

        Public Function H1(text As String) As Label
            Return New Label With {
                .Text = text, .AutoSize = True, .Font = New Font("Segoe UI Semibold", 16.0F),
                .Margin = New Padding(0, 0, 0, 8)}
        End Function

        Public Function Muted(text As String) As Label
            Return New Label With {.Text = text, .AutoSize = True, .ForeColor = MutedText}
        End Function

        Public Function PrimaryButton(text As String) As Button
            Dim b As New Button With {
                .Text = text, .AutoSize = False, .Height = 34, .MinimumSize = New Size(96, 34),
                .FlatStyle = FlatStyle.Flat, .BackColor = Accent, .ForeColor = Color.White,
                .Font = New Font("Segoe UI", 9.0F, FontStyle.Bold), .Cursor = Cursors.Hand,
                .Padding = New Padding(10, 0, 10, 0)}
            b.FlatAppearance.BorderSize = 0
            b.FlatAppearance.MouseOverBackColor = AccentDark
            Return b
        End Function

        Public Function SecondaryButton(text As String) As Button
            Dim b As New Button With {
                .Text = text, .AutoSize = False, .Height = 34, .MinimumSize = New Size(90, 34),
                .FlatStyle = FlatStyle.Flat, .BackColor = Color.White, .ForeColor = Color.FromArgb(31, 41, 55),
                .Cursor = Cursors.Hand, .Padding = New Padding(10, 0, 10, 0)}
            b.FlatAppearance.BorderColor = Color.FromArgb(209, 213, 219)
            Return b
        End Function

        Public Function MakeGrid() As DataGridView
            Dim g As New DataGridView With {
                .Dock = DockStyle.Fill, .AllowUserToAddRows = False, .AllowUserToDeleteRows = False,
                .ReadOnly = True, .RowHeadersVisible = False, .MultiSelect = False,
                .SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                .AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                .BackgroundColor = Color.White, .BorderStyle = BorderStyle.None,
                .EnableHeadersVisualStyles = False, .AllowUserToResizeRows = False,
                .Font = New Font("Segoe UI", 9.0F), .AutoGenerateColumns = False}
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(249, 250, 251)
            g.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI Semibold", 9.0F)
            g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
            g.ColumnHeadersHeight = 34
            g.RowTemplate.Height = 30
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(219, 234, 254)
            g.DefaultCellStyle.SelectionForeColor = Color.Black
            Return g
        End Function

        Public Function TextColumn(header As String, prop As String,
                                   Optional fill As Integer = 0, Optional width As Integer = 0) As DataGridViewTextBoxColumn
            Dim c As New DataGridViewTextBoxColumn With {.HeaderText = header, .DataPropertyName = prop, .Name = prop}
            If fill > 0 Then
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                c.FillWeight = fill
            ElseIf width > 0 Then
                c.AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                c.Width = width
            End If
            Return c
        End Function

        Public Function NumberColumn(header As String, prop As String, Optional format As String = "N2",
                                     Optional width As Integer = 90) As DataGridViewTextBoxColumn
            Dim c = TextColumn(header, prop, width:=width)
            c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
            c.DefaultCellStyle.Format = format
            Return c
        End Function

        <Runtime.CompilerServices.Extension>
        Public Sub ShowIfFailed(result As Result, owner As IWin32Window)
            If result.Failed Then
                MessageBox.Show(owner, result.ErrorMessage, "Cannot complete",
                                MessageBoxButtons.OK, MessageBoxIcon.Warning)
            End If
        End Sub

        Public Function Confirm(owner As IWin32Window, text As String, Optional caption As String = "Please confirm") As Boolean
            Return MessageBox.Show(owner, text, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
        End Function

        Public Sub Info(owner As IWin32Window, text As String, Optional caption As String = "Done")
            MessageBox.Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information)
        End Sub

    End Module

End Namespace
