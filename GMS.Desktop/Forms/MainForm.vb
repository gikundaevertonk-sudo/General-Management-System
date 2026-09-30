Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Security
Imports GMS.Desktop.App
Imports GMS.Desktop.Views

Namespace Forms

    ''' <summary>Application shell: a permission-filtered sidebar and a swappable content area.</summary>
    Public NotInheritable Class MainForm
        Inherits Form

        Private ReadOnly _nav As New FlowLayoutPanel()
        Private ReadOnly _content As New Panel()
        Private ReadOnly _navButtons As New List(Of Button)()
        Private _currentKey As String

        ' Shared rather than built per navigation: a new Font per button per click was never
        ' released, and a GDI handle leaks with each one.
        Private Shared ReadOnly NavFontInactive As New Font("Segoe UI", 9.5F, FontStyle.Regular)
        Private Shared ReadOnly NavFontActive As New Font("Segoe UI", 9.5F, FontStyle.Bold)

        Public ReadOnly Property SignOutRequested As Boolean

        Public Sub New()
            Dim session = AppHost.Current.Session

            Text = "General Management System"
            Icon = UiKit.AppIcon
            StartPosition = FormStartPosition.CenterScreen
            MinimumSize = New Size(1000, 640)
            WindowState = FormWindowState.Maximized
            Font = New Font("Segoe UI", 9.0F)
            BackColor = UiKit.PageBack

            _content.Dock = DockStyle.Fill
            _content.BackColor = UiKit.PageBack
            _content.Padding = New Padding(24)

            _nav.Dock = DockStyle.Left
            _nav.Width = 220
            _nav.BackColor = UiKit.Sidebar
            _nav.FlowDirection = FlowDirection.TopDown
            _nav.WrapContents = False
            ' Only scrolls when the window is too short for every item, so Sign out is never lost
            ' off the bottom. Everything in it is sized to leave room for the scrollbar.
            _nav.AutoScroll = True
            _nav.Padding = New Padding(0, 12, 0, 12)

            Dim brand As New Label With {
                .Text = "  GMS", .ForeColor = Color.White, .Font = New Font("Segoe UI Semibold", 15.0F),
                .AutoSize = False, .Size = New Size(200, 44), .TextAlign = ContentAlignment.MiddleLeft,
                .Margin = New Padding(0, 0, 0, 12)}
            _nav.Controls.Add(brand)
            AddSyncStatus()

            AddNav("dashboard", "Dashboard", Nothing, Function() New DashboardView(AddressOf Navigate))
            AddNav("products", "Products", PermissionCodes.Products.View, Function() New ProductsView())
            AddNav("categories", "Categories", PermissionCodes.Categories.View, Function() New CategoriesView())
            AddNav("customers", "Customers", PermissionCodes.Customers.View, Function() New CustomersView())
            AddNav("suppliers", "Suppliers", PermissionCodes.Suppliers.View, Function() New SuppliersView())
            AddNav("transactions", "Transactions", PermissionCodes.Transactions.View, Function() New TransactionsView())
            AddNav("inventory", "Inventory", PermissionCodes.Inventory.View, Function() New InventoryView())
            AddNav("shops", "Shops", PermissionCodes.Shops.View, Function() New ShopsView())
            AddNav("reports", "Reports", PermissionCodes.Reports.View, Function() New ReportsView())
            AddNav("users", "Users & Roles", PermissionCodes.Users.View, Function() New UsersView())
            AddNav("notifications", "Notifications", Nothing, Function() New NotificationsView())
            AddNav("audit", "Audit trail", PermissionCodes.Audit.View, Function() New AuditView())
            AddNav("settings", "Settings", PermissionCodes.Settings.Manage, Function() New SettingsView())

            Dim spacer As New Panel With {.Size = New Size(190, 16)}
            _nav.Controls.Add(spacer)

            ' The shop is named here because everything a pinned attendant sees is silently
            ' narrowed to it; without it on screen, an empty list reads as missing data.
            Dim whoLines = $"  {session.Principal.FullName}" & Environment.NewLine & $"  {session.Principal.RoleName}"
            If session.Principal.ShopId.HasValue AndAlso Not String.IsNullOrEmpty(session.Principal.ShopName) Then
                whoLines &= Environment.NewLine & $"  {session.Principal.ShopName}"
            End If
            Dim who As New Label With {
                .Text = whoLines,
                .ForeColor = Color.Gainsboro, .AutoSize = False, .Size = New Size(196, 58),
                .Font = New Font("Segoe UI", 8.5F)}
            _nav.Controls.Add(who)

            ' Labelled with the theme you would switch to, so it reads as an action.
            Dim themeToggle = NavLinkButton(ThemeToggleText())
            AddHandler themeToggle.Click, Sub() DesktopTheme.Toggle()
            AddHandler DesktopTheme.ThemeChanged, Sub() themeToggle.Text = "   " & ThemeToggleText()
            _nav.Controls.Add(themeToggle)

            Dim changePwd = NavLinkButton("Change password")
            AddHandler changePwd.Click, Sub()
                                            Using f As New ChangePasswordForm(session.Principal.UserId)
                                                If f.ShowDialog(Me) = DialogResult.OK Then UiKit.Info(Me, "Password updated.")
                                            End Using
                                        End Sub
            _nav.Controls.Add(changePwd)

            Dim signOut = NavLinkButton("Sign out")
            AddHandler signOut.Click, Sub()
                                          If UiKit.Confirm(Me, "Sign out now?") Then
                                              _SignOutRequested = True
                                              Close()
                                          End If
                                      End Sub
            _nav.Controls.Add(signOut)

            Controls.Add(_content)
            Controls.Add(_nav)

            DesktopTheme.Attach(Me)
            ' Subscribed after Attach so it runs second: the walker resets the nav buttons to
            ' their mapped base colours, then this puts the selection highlight back.
            AddHandler DesktopTheme.ThemeChanged, Sub() RefreshNavColors()

            Navigate("dashboard")
        End Sub

        Private _syncStatus As Button
        Private ReadOnly _syncTips As New ToolTip()

        ''' <summary>
        ''' The line under the logo that says whether this computer is in step with the server.
        ''' Clicking it syncs now, or first lists what could not be sent.
        ''' </summary>
        Private Sub AddSyncStatus()
            Dim sync = AppHost.Current.Sync
            If sync Is Nothing Then Return ' demo mode: nothing to sync

            _syncStatus = NavLinkButton("")
            _syncStatus.Margin = New Padding(6, 0, 6, 10)
            AddHandler _syncStatus.Click, Sub() OnSyncStatusClicked()
            _nav.Controls.Add(_syncStatus)

            AddHandler sync.StatusChanged, AddressOf OnSyncStatusChanged
            AddHandler sync.NegativeStockFound, AddressOf OnNegativeStock
            ' The sync outlives this window (sign out, sign back in), so it must stop calling it.
            AddHandler FormClosed, Sub()
                                       RemoveHandler sync.StatusChanged, AddressOf OnSyncStatusChanged
                                       RemoveHandler sync.NegativeStockFound, AddressOf OnNegativeStock
                                   End Sub
            AddHandler Shown, Sub() sync.Start()
            ' The theme walker resets link colours; this one's colour carries the status.
            AddHandler DesktopTheme.ThemeChanged, Sub() If Not IsDisposed Then OnSyncStatusChanged(sync, EventArgs.Empty)
            OnSyncStatusChanged(sync, EventArgs.Empty)
        End Sub

        Private Sub OnSyncStatusChanged(sender As Object, e As EventArgs)
            Dim sync = AppHost.Current.Sync
            Dim waiting = If(sync.Pending = 1, "1 change waiting", $"{sync.Pending} changes waiting")
            Dim text As String
            Dim colour As Color
            Select Case sync.State
                Case SyncState.Syncing
                    text = "Syncing..."
                    colour = Color.FromArgb(147, 197, 253)
                Case SyncState.UpToDate
                    text = "Online - all saved"
                    colour = Color.FromArgb(134, 239, 172)
                Case SyncState.Offline
                    text = If(sync.Pending = 0, "Offline - working locally", $"Offline - {waiting}")
                    colour = Color.FromArgb(253, 224, 71)
                Case SyncState.NeedsAttention
                    text = $"Not sent - {waiting}"
                    colour = Color.FromArgb(252, 165, 165)
                Case Else
                    text = "Connecting..."
                    colour = Color.Gainsboro
            End Select

            _syncStatus.Text = "   " & ChrW(&H25CF) & " " & text
            _syncStatus.ForeColor = colour
            Dim contact = If(sync.LastContact.HasValue, $"Last reached the server at {sync.LastContact.Value:t}.", "Has not reached the server yet.")
            _syncTips.SetToolTip(_syncStatus, contact & Environment.NewLine & "Click to sync now.")
        End Sub

        Private Sub OnSyncStatusClicked()
            Dim sync = AppHost.Current.Sync
            If sync.State = SyncState.NeedsAttention AndAlso sync.LastReport IsNot Nothing Then
                Dim shown = sync.LastReport.Errors.Take(10).ToList()
                Dim more = sync.LastReport.Errors.Count - shown.Count
                UiKit.Info(Me,
                    "These changes could not be sent to the server. They are kept on this computer " &
                    "and will be tried again:" & Environment.NewLine & Environment.NewLine &
                    String.Join(Environment.NewLine, shown) &
                    If(more > 0, $"{Environment.NewLine}...and {more} more.", ""), "Not sent yet")
            End If
            sync.SyncNow()
        End Sub

        Private Sub OnNegativeStock(sender As Object, items As IReadOnlyList(Of String))
            UiKit.Info(Me,
                "Sales made on this computer while it was offline used stock that had already been " &
                "sold elsewhere. The sales have been kept; please count and adjust:" &
                Environment.NewLine & Environment.NewLine & String.Join(Environment.NewLine, items),
                "Stock below zero")
        End Sub

        Private Shared Function ThemeToggleText() As String
            Return If(DesktopTheme.IsDark, "Light mode", "Dark mode")
        End Function

        Private Sub AddNav(key As String, text As String, requiredPermission As String, factory As Func(Of UserControl))
            Dim session = AppHost.Current.Session
            If requiredPermission IsNot Nothing AndAlso Not session.Principal.HasPermission(requiredPermission) Then Return

            Dim b As New Button With {
                .Text = "   " & text, .Tag = New NavEntry(key, factory),
                .TextAlign = ContentAlignment.MiddleLeft, .FlatStyle = FlatStyle.Flat,
                .ForeColor = Color.Gainsboro, .BackColor = UiKit.Sidebar, .UseMnemonic = False,
                .Size = New Size(190, 36), .Margin = New Padding(6, 1, 6, 1),
                .Font = New Font("Segoe UI", 9.5F), .Cursor = Cursors.Hand}
            b.FlatAppearance.BorderSize = 0
            b.FlatAppearance.MouseOverBackColor = UiKit.SidebarHover
            AddHandler b.Click, Sub() Navigate(key)
            _navButtons.Add(b)
            _nav.Controls.Add(b)
        End Sub

        Private Function NavLinkButton(text As String) As Button
            Dim b As New Button With {
                .Text = "   " & text, .TextAlign = ContentAlignment.MiddleLeft, .FlatStyle = FlatStyle.Flat,
                .ForeColor = Color.FromArgb(147, 197, 253), .BackColor = UiKit.Sidebar,
                .Size = New Size(190, 30), .Margin = New Padding(6, 1, 6, 1), .Cursor = Cursors.Hand, .UseMnemonic = False,
                .Font = New Font("Segoe UI", 8.5F)}
            b.FlatAppearance.BorderSize = 0
            b.FlatAppearance.MouseOverBackColor = UiKit.SidebarHover
            Return b
        End Function

        Private Sub Navigate(key As String)
            If key = _currentKey Then Return
            Dim entry = _navButtons.Select(Function(b) CType(b.Tag, NavEntry)).FirstOrDefault(Function(n) n.Key = key)
            If entry Is Nothing Then Return

            Dim view As UserControl
            Try
                view = entry.Factory.Invoke()
            Catch ex As Exception
                MessageBox.Show(Me, ex.Message, "Could not open view", MessageBoxButtons.OK, MessageBoxIcon.Error)
                Return
            End Try

            ' Snapshot before clearing: Clear() empties the collection, so walking it afterwards
            ' disposed nothing and every screen ever opened - grids, fonts, handles, and the
            ' dashboard's ThemeChanged subscription - stayed alive for the rest of the session.
            Dim previous = _content.Controls.Cast(Of Control)().ToList()
            _content.Controls.Clear()
            For Each old In previous : old.Dispose() : Next
            view.Dock = DockStyle.Fill
            _content.Controls.Add(view)
            _currentKey = key

            ' The view was built after DesktopTheme.Attach walked this form, so it has to be
            ' themed explicitly or it comes up light inside a dark shell.
            DesktopTheme.Apply(view)

            RefreshNavColors()
        End Sub

        ''' <summary>
        ''' Repaints the sidebar's selected-item highlight. Kept separate from the theme walker
        ''' because these colours encode state (which page is open), not just appearance, so
        ''' they have to be recomputed on navigation and again whenever the theme changes.
        ''' </summary>
        Private Sub RefreshNavColors()
            For Each b In _navButtons
                Dim active = CType(b.Tag, NavEntry).Key = _currentKey
                b.BackColor = If(active, DesktopTheme.SidebarHoverBack, DesktopTheme.SidebarBack)
                b.ForeColor = If(active, DesktopTheme.NavTextActive, DesktopTheme.NavTextInactive)
                b.Font = If(active, NavFontActive, NavFontInactive)
            Next
        End Sub

        Private NotInheritable Class NavEntry
            Public ReadOnly Property Key As String
            Public ReadOnly Property Factory As Func(Of UserControl)
            Public Sub New(key As String, factory As Func(Of UserControl))
                Me.Key = key
                Me.Factory = factory
            End Sub
        End Class
    End Class

End Namespace
