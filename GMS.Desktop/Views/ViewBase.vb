Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Desktop.App

Namespace Views

    ''' <summary>
    ''' Common page chrome for the content views: a title row with a right-aligned
    ''' action strip, and a white card body that fills the rest of the area.
    ''' </summary>
    Public MustInherit Class ViewBase
        Inherits UserControl

        Private ReadOnly _title As Label
        Protected ReadOnly Actions As FlowLayoutPanel
        Protected ReadOnly Body As Panel

        Protected Sub New(title As String)
            BackColor = UiKit.PageBack
            Dim layout As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 1, .RowCount = 2}
            layout.RowStyles.Add(New RowStyle(SizeType.Absolute, 52))
            layout.RowStyles.Add(New RowStyle(SizeType.Percent, 100))

            Dim head As New Panel With {.Dock = DockStyle.Fill}
            _title = New Label With {
                .Text = title, .Font = New Font("Segoe UI Semibold", 15.0F), .AutoSize = True,
                .Location = New Point(2, 8)}
            Actions = New FlowLayoutPanel With {
                .Dock = DockStyle.Right, .FlowDirection = FlowDirection.RightToLeft,
                .WrapContents = False, .AutoSize = True, .Padding = New Padding(0, 8, 0, 0)}
            head.Controls.Add(_title)
            head.Controls.Add(Actions)

            Body = New Panel With {.Dock = DockStyle.Fill, .BackColor = UiKit.CardBack, .Padding = New Padding(1)}
            AddHandler Body.Paint,
                Sub(s, e)
                    Using p As New Pen(Color.FromArgb(229, 231, 235))
                        e.Graphics.DrawRectangle(p, 0, 0, Body.Width - 1, Body.Height - 1)
                    End Using
                End Sub

            layout.Controls.Add(head, 0, 0)
            layout.Controls.Add(Body, 0, 1)
            Controls.Add(layout)
        End Sub

        Protected Sub SetTitle(text As String)
            _title.Text = text
        End Sub

        ''' <summary>Add a right-aligned action button (added right-to-left, so call primary last).</summary>
        Protected Function AddAction(button As Button) As Button
            button.Margin = New Padding(8, 0, 0, 0)
            Actions.Controls.Add(button)
            Return button
        End Function

        ''' <summary>Runs <paramref name="work"/>, showing any thrown message as a dialog.</summary>
        Protected Sub Guarded(work As Action)
            Try
                work()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Sub
    End Class

End Namespace
