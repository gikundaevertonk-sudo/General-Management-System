Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class DashboardView
        Inherits ViewBase

        Private ReadOnly _onNavigate As Action(Of String)
        Private ReadOnly _launcher As New FlowLayoutPanel()
        Private ReadOnly _cards As New FlowLayoutPanel()
        Private ReadOnly _activity As DataGridView = UiKit.MakeGrid()

        Public Sub New(onNavigate As Action(Of String))
            MyBase.New("Dashboard")
            _onNavigate = onNavigate

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            _launcher.Dock = DockStyle.Top
            _launcher.Height = 160
            _launcher.Padding = New Padding(16, 16, 16, 0)
            _launcher.AutoScroll = True
            BuildLauncher()

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
            Body.Controls.Add(_launcher)

            Reload()
        End Sub

        Private Sub BuildLauncher()
            Dim session = AppHost.Current.Session
            Dim modules = {
                ("products", "Products", "Catalogue & pricing", PermissionCodes.Products.View),
                ("categories", "Categories", "Organise the catalogue", PermissionCodes.Categories.View),
                ("customers", "Customers", "Accounts & contacts", PermissionCodes.Customers.View),
                ("suppliers", "Suppliers", "Vendors you buy from", PermissionCodes.Suppliers.View),
                ("transactions", "Transactions", "Sales & purchases", PermissionCodes.Transactions.View),
                ("inventory", "Inventory", "Stock levels & adjustments", PermissionCodes.Inventory.View),
                ("reports", "Reports", "Summaries & exports", PermissionCodes.Reports.View),
                ("users", "Users & Roles", "Access control", PermissionCodes.Users.View),
                ("notifications", "Notifications", "Alerts & reminders", CType(Nothing, String)),
                ("audit", "Audit trail", "Who did what, when", PermissionCodes.Audit.View),
                ("settings", "Settings", "Company & system setup", PermissionCodes.Settings.Manage)
            }

            For Each m In modules
                If m.Item4 IsNot Nothing AndAlso Not session.Principal.HasPermission(m.Item4) Then Continue For
                _launcher.Controls.Add(LaunchButton(m.Item1, m.Item2, m.Item3))
            Next
        End Sub

        Private Function LaunchButton(key As String, title As String, subtitle As String) As Control
            Dim b As New Button With {
                .Size = New Size(150, 90), .Margin = New Padding(0, 0, 12, 12), .FlatStyle = FlatStyle.Flat,
                .BackColor = Color.White, .ForeColor = Color.FromArgb(17, 24, 39), .Cursor = Cursors.Hand,
                .TextAlign = ContentAlignment.TopLeft, .Padding = New Padding(12, 10, 8, 8),
                .Text = title & Environment.NewLine & Environment.NewLine & subtitle,
                .Font = New Font("Segoe UI Semibold", 9.5F)}
            b.FlatAppearance.BorderColor = Color.FromArgb(229, 231, 235)
            b.FlatAppearance.MouseOverBackColor = Color.FromArgb(239, 246, 255)
            AddHandler b.Click, Sub() _onNavigate?.Invoke(key)
            Return b
        End Function

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

            ' The cards are rebuilt from scratch here, so the ones the theme walker already knows
            ' about are gone and their replacements have never been through it.
            DesktopTheme.Apply(_cards)
        End Sub

        Private Shared Function Card(caption As String, value As String, accent As Color) As Control
            Dim p As New Panel With {.Size = New Size(210, 92), .Margin = New Padding(8), .BackColor = Color.White}
            AddHandler p.Paint, Sub(s, e)
                                    Using pen As New Pen(DesktopTheme.CardBorder)
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
