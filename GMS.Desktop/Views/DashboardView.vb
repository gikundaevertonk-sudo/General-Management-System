Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class DashboardView
        Inherits ViewBase

        Private Const GridHeaderHeight As Integer = 34
        Private Const GridRowHeight As Integer = 30
        Private Const SectionHeadingHeight As Integer = 26

        Private ReadOnly _onNavigate As Action(Of String)
        Private ReadOnly _page As New ScrollHost()
        Private ReadOnly _launcher As New FlowLayoutPanel()
        Private ReadOnly _cards As New FlowLayoutPanel()
        Private ReadOnly _activity As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _activityWrap As New Panel()
        Private ReadOnly _chartRow As New TableLayoutPanel()
        Private ReadOnly _trend As New SalesTrendChart()
        Private ReadOnly _top As New TopProductsChart()

        Public Sub New(onNavigate As Action(Of String))
            MyBase.New("Dashboard")
            _onNavigate = onNavigate

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            ' Everything below is a section that docks to the top at its natural height and never
            ' scrolls on its own. The page has exactly one scrollbar - the host's, on the right.
            _page.BackColor = UiKit.CardBack

            _launcher.Dock = DockStyle.Top
            _launcher.AutoSize = True
            _launcher.AutoSizeMode = AutoSizeMode.GrowAndShrink
            _launcher.Padding = New Padding(16, 16, 16, 4)
            BuildLauncher()

            _cards.Dock = DockStyle.Top
            _cards.AutoSize = True
            _cards.AutoSizeMode = AutoSizeMode.GrowAndShrink
            _cards.Padding = New Padding(16, 8, 16, 8)

            ' Two charts side by side, sales trend taking the larger share. Hidden until the caller is
            ' known to hold Reports.View, because they are the sales report in another shape.
            _chartRow.Dock = DockStyle.Top
            _chartRow.Height = 280
            _chartRow.Padding = New Padding(16, 0, 16, 8)
            _chartRow.ColumnCount = 2
            _chartRow.RowCount = 1
            _chartRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 60.0F))
            _chartRow.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 40.0F))
            _trend.Dock = DockStyle.Fill
            _trend.Margin = New Padding(0, 0, 8, 0)
            _top.Dock = DockStyle.Fill
            _top.Margin = New Padding(8, 0, 0, 0)
            _chartRow.Controls.Add(_trend, 0, 0)
            _chartRow.Controls.Add(_top, 1, 0)
            _chartRow.Visible = False

            ' The grid is sized to its rows (see FitActivityGrid) so it has no scrollbar of its own.
            _activity.ScrollBars = ScrollBars.None
            _activity.Dock = DockStyle.Fill
            _activityWrap.Dock = DockStyle.Top
            _activityWrap.Padding = New Padding(16, 8, 16, 16)
            _activity.Columns.Add(UiKit.TextColumn("When (UTC)", "WhenUtc", width:=150))
            _activity.Columns.Add(UiKit.TextColumn("Activity", "Summary", fill:=100))
            _activityWrap.Controls.Add(_activity)
            _activityWrap.Controls.Add(New Label With {.Text = "Recent activity", .Dock = DockStyle.Top,
                                                       .Font = New Font("Segoe UI Semibold", 10.0F),
                                                       .Height = SectionHeadingHeight})
            FitActivityGrid(0)

            ' The charts paint from the theme at draw time, so a toggle only needs a repaint.
            Dim onTheme As EventHandler = Sub() If Not IsDisposed Then _page.Invalidate(True)
            AddHandler DesktopTheme.ThemeChanged, onTheme
            AddHandler Disposed, Sub() RemoveHandler DesktopTheme.ThemeChanged, onTheme

            ' Docked to the top, so the last one added sits highest.
            _page.Controls.Add(_activityWrap)
            _page.Controls.Add(_chartRow)
            _page.Controls.Add(_cards)
            _page.Controls.Add(_launcher)
            _page.ForwardWheel(_activity)
            Body.Controls.Add(_page)

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
                .BackColor = UiKit.ButtonTint, .ForeColor = UiKit.AccentDark, .Cursor = Cursors.Hand,
                .Font = New Font("Segoe UI Semibold", 9.5F), .AccessibleName = title}
            b.FlatAppearance.BorderColor = UiKit.ButtonEdge
            b.FlatAppearance.MouseOverBackColor = UiKit.ButtonTintHover
            AddHandler b.Click, Sub() _onNavigate?.Invoke(key)
            ' Drawn here, not set as the button's Text: a Text with "&" in it ("Users & Roles",
            ' "Catalogue & pricing") loses the ampersand to the mnemonic, and a two-line Text
            ' cannot give the description its own, quieter colour.
            AddHandler b.Paint,
                Sub(s, e)
                    Dim flags = TextFormatFlags.Left Or TextFormatFlags.Top Or TextFormatFlags.NoPrefix Or
                                TextFormatFlags.EndEllipsis Or TextFormatFlags.WordBreak
                    TextRenderer.DrawText(e.Graphics, title, b.Font, New Rectangle(12, 12, b.Width - 20, 20), b.ForeColor, flags)
                    Dim quiet = If(DesktopTheme.IsDark, Color.FromArgb(154, 166, 184), UiKit.MutedText)
                    Using small As New Font("Segoe UI", 8.5F)
                        TextRenderer.DrawText(e.Graphics, subtitle, small, New Rectangle(12, 38, b.Width - 20, 40), quiet, flags)
                    End Using
                End Sub
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

                        LoadCharts()

                        Dim rows = d.RecentActivity.
                            Select(Function(a) New With {.WhenUtc = a.WhenUtc.ToString("yyyy-MM-dd HH:mm"), a.Summary}).ToList()
                        _activity.DataSource = rows
                        FitActivityGrid(rows.Count)
                    End Sub)

            ' The cards are rebuilt from scratch here, so the ones the theme walker already knows
            ' about are gone and their replacements have never been through it.
            DesktopTheme.Apply(_cards)
        End Sub

        ''' <summary>
        ''' Sizes the grid to hold every row, so the page scrolls rather than the grid. A grid with
        ''' its own scrollbar next to the page's would be two bars to reach the bottom of one screen.
        ''' </summary>
        Private Sub FitActivityGrid(rowCount As Integer)
            Dim gridHeight = GridHeaderHeight + GridRowHeight * Math.Max(rowCount, 1) + 2
            _activityWrap.Height = gridHeight + SectionHeadingHeight + _activityWrap.Padding.Vertical
        End Sub

        Private Sub LoadCharts()
            If Not AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Reports.View) Then
                _chartRow.Visible = False
                Return
            End If
            Dim charts = AppHost.Current.Resolve(Of DashboardService)().GetCharts()
            If charts.Failed Then
                _chartRow.Visible = False
                Return
            End If
            _trend.SetData(charts.Value)
            _top.SetData(charts.Value)
            _chartRow.Visible = True
        End Sub

        Private Shared Function Card(caption As String, value As String, accent As Color) As Control
            ' Both colours are set explicitly. The theme walker restores light mode from the
            ' colours a control had when it was first seen, and an unset ForeColor reports
            ' whatever the parent currently has - so a card rebuilt by Reload while dark mode
            ' is on would capture dark grey as its "original" and keep it on the way back.
            Dim p As New Panel With {.Size = New Size(210, 92), .Margin = New Padding(8),
                                     .BackColor = Color.White, .ForeColor = SystemColors.ControlText}
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
