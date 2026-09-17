Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class DashboardView
        Inherits ViewBase

        Private ReadOnly _cards As New FlowLayoutPanel()
        Private ReadOnly _activity As DataGridView = UiKit.MakeGrid()

        Public Sub New()
            MyBase.New("Dashboard")

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            _cards.Dock = DockStyle.Top
            _cards.Height = 220
            _cards.Padding = New Padding(16)
            _cards.AutoScroll = True

            Dim activityWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(16)}
            _activity.Columns.Add(UiKit.TextColumn("When (UTC)", "WhenUtc", width:=150))
            _activity.Columns.Add(UiKit.TextColumn("Activity", "Summary", fill:=100))
            activityWrap.Controls.Add(_activity)
            activityWrap.Controls.Add(New Label With {.Text = "Recent activity", .Dock = DockStyle.Top,
                                                      .Font = New Font("Segoe UI Semibold", 10.0F), .Height = 24})

            Body.Controls.Add(activityWrap)
            Body.Controls.Add(_cards)

            Reload()
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim result = AppHost.Current.Resolve(Of DashboardService)().GetSummary()
                        _cards.Controls.Clear()
                        If result.Failed Then
                            _cards.Controls.Add(UiKit.Muted(result.ErrorMessage))
                            Return
                        End If

                        Dim d = result.Value
                        _cards.Controls.Add(Card("Products", d.ProductCount.ToString("N0"), UiKit.Accent))
                        _cards.Controls.Add(Card("Low stock", d.LowStockCount.ToString("N0"),
                                                 If(d.LowStockCount > 0, Color.FromArgb(202, 138, 4), UiKit.Accent)))
                        _cards.Controls.Add(Card("Inventory value (cost)", d.InventoryValueAtCost.ToString("N2"), UiKit.Accent))
                        _cards.Controls.Add(Card("Sales today", d.SalesTodayTotal.ToString("N2"), UiKit.Accent))
                        _cards.Controls.Add(Card("Sales this month", d.SalesMonthToDateTotal.ToString("N2"), UiKit.Accent))
                        _cards.Controls.Add(Card("Open drafts", d.OpenDraftCount.ToString("N0"), UiKit.Accent))
                        _cards.Controls.Add(Card("Customers", d.CustomerCount.ToString("N0"), UiKit.Accent))
                        _cards.Controls.Add(Card("Unread alerts", d.UnreadNotificationCount.ToString("N0"),
                                                 If(d.UnreadNotificationCount > 0, Color.FromArgb(202, 138, 4), UiKit.Accent)))

                        _activity.DataSource = d.RecentActivity.
                            Select(Function(a) New With {.WhenUtc = a.WhenUtc.ToString("yyyy-MM-dd HH:mm"), a.Summary}).ToList()
                    End Sub)
        End Sub

        Private Shared Function Card(caption As String, value As String, accent As Color) As Control
            Dim p As New Panel With {.Size = New Size(210, 92), .Margin = New Padding(8), .BackColor = Color.White}
            AddHandler p.Paint, Sub(s, e)
                                    Using pen As New Pen(Color.FromArgb(229, 231, 235))
                                        e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1)
                                    End Using
                                    Using b As New SolidBrush(accent)
                                        e.Graphics.FillRectangle(b, 0, 0, 4, p.Height)
                                    End Using
                                End Sub
            p.Controls.Add(New Label With {.Text = caption, .ForeColor = UiKit.MutedText, .AutoSize = True, .Location = New Point(16, 14)})
            p.Controls.Add(New Label With {.Text = value, .Font = New Font("Segoe UI Semibold", 18.0F), .AutoSize = True, .Location = New Point(14, 38)})
            Return p
        End Function
    End Class

End Namespace
