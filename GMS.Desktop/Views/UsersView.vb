Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    Public NotInheritable Class UsersView
        Inherits ViewBase

        Private ReadOnly _tabUsers As Button
        Private ReadOnly _tabRoles As Button
        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _canManageUsers As Boolean
        Private _mode As String = "users"

        Public Sub New()
            MyBase.New("Users & Roles")
            _canManageUsers = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Users.Manage)

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            If _canManageUsers Then
                Dim reset = UiKit.SecondaryButton("Reset password")
                AddHandler reset.Click, Sub() ResetPassword()
                AddAction(reset)
                Dim toggle = UiKit.SecondaryButton("Activate / Deactivate")
                AddHandler toggle.Click, Sub() ToggleActive()
                AddAction(toggle)
                Dim add = UiKit.PrimaryButton("New user")
                AddHandler add.Click, Sub() OpenEditor(Nothing)
                AddAction(add)
            End If

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 40, .Padding = New Padding(8, 6, 8, 6)}
            _tabUsers = UiKit.SecondaryButton("Users") : _tabUsers.SetBounds(8, 4, 90, 28)
            _tabRoles = UiKit.SecondaryButton("Roles") : _tabRoles.SetBounds(104, 4, 90, 28)
            AddHandler _tabUsers.Click, Sub() SwitchTo("users")
            AddHandler _tabRoles.Click, Sub() SwitchTo("roles")
            bar.Controls.AddRange({_tabUsers, _tabRoles})

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 AndAlso _mode = "users" Then EditSelected()
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)

            SwitchTo("users")
        End Sub

        Private Sub SwitchTo(mode As String)
            _mode = mode
            _tabUsers.BackColor = If(mode = "users", UiKit.Accent, Color.White)
            _tabUsers.ForeColor = If(mode = "users", Color.White, Color.Black)
            _tabRoles.BackColor = If(mode = "roles", UiKit.Accent, Color.White)
            _tabRoles.ForeColor = If(mode = "roles", Color.White, Color.Black)
            Reload()
        End Sub

        Public Sub Reload()
            If _mode = "users" Then ReloadUsers() Else ReloadRoles()
        End Sub

        Private Sub ReloadUsers()
            Guarded(Sub()
                        _grid.Columns.Clear()
                        _grid.DataSource = Nothing
                        Dim shops = AppHost.Current.Resolve(Of ShopService)().List(activeOnly:=False)
                        Dim shopName = If(shops.Succeeded,
                            shops.Value.ToDictionary(Function(s) s.Id, Function(s) s.Name),
                            New Dictionary(Of Integer, String))

                        _grid.Columns.AddRange(UiKit.TextColumn("Username", "UserName", width:=140),
                                               UiKit.TextColumn("Full name", "FullName", fill:=100),
                                               UiKit.TextColumn("Email", "Email", fill:=100),
                                               UiKit.TextColumn("Role", "Role", width:=120))
                        ' Only worth a column once the organisation actually has shops.
                        If shopName.Count > 0 Then _grid.Columns.Add(UiKit.TextColumn("Shop", "Shop", width:=130))
                        _grid.Columns.AddRange(UiKit.TextColumn("Active", "Active", width:=64),
                                               UiKit.TextColumn("Last sign-in (UTC)", "LastLogin", width:=150))

                        Dim res = AppHost.Current.Resolve(Of UserService)().Search(New QueryOptions With {.PageSize = 500})
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return
                        Dim roles = AppHost.Current.Resolve(Of RoleService)().List()
                        Dim roleName = If(roles.Succeeded, roles.Value.ToDictionary(Function(r) r.Role.Id, Function(r) r.Role.Name), New Dictionary(Of Integer, String))

                        _grid.DataSource = res.Value.Items.Select(Function(u) New Row With {
                            .Id = u.Id, .UserName = u.UserName, .FullName = u.FullName, .Email = u.Email,
                            .Role = roleName.GetValueOrDefault(u.RoleId, "—"),
                            .Shop = If(u.ShopId.HasValue, shopName.GetValueOrDefault(u.ShopId.Value, "—"), "All shops"),
                            .Active = If(u.IsActive, "Yes", "No"),
                            .LastLogin = If(u.LastLoginUtc.HasValue, u.LastLoginUtc.Value.ToString("yyyy-MM-dd HH:mm"), "—")
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub ReloadRoles()
            Guarded(Sub()
                        _grid.Columns.Clear()
                        _grid.DataSource = Nothing
                        _grid.Columns.AddRange(UiKit.TextColumn("Role", "Name", width:=160),
                                               UiKit.TextColumn("Description", "Description", fill:=100),
                                               UiKit.NumberColumn("Permissions", "Permissions", "N0", 100),
                                               UiKit.NumberColumn("Users", "Users", "N0", 80),
                                               UiKit.TextColumn("System", "System", width:=70))

                        Dim res = AppHost.Current.Resolve(Of RoleService)().List()
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return
                        _grid.DataSource = res.Value.Select(Function(r) New With {
                            .Name = r.Role.Name, .Description = r.Role.Description,
                            .Permissions = r.PermissionCodes.Count, .Users = r.UserCount,
                            .System = If(r.Role.IsSystem, "Yes", "No")
                        }).ToList()
                    End Sub)
        End Sub

        Private Function SelectedUserId() As Integer?
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            Return If(row Is Nothing, CType(Nothing, Integer?), row.Id)
        End Function

        Private Sub EditSelected()
            Dim id = SelectedUserId()
            If id IsNot Nothing Then OpenEditor(id)
        End Sub

        Private Sub OpenEditor(userId As Integer?)
            If Not _canManageUsers Then UiKit.Info(Me, "You do not have permission to manage users.") : Return
            Using f As New UserEditForm(userId)
                If f.ShowDialog(Me) = DialogResult.OK Then ReloadUsers()
            End Using
        End Sub

        Private Sub ResetPassword()
            Dim id = SelectedUserId()
            If id Is Nothing Then Return
            Using f As New SetPasswordForm()
                If f.ShowDialog(Me) <> DialogResult.OK Then Return
                AppHost.Current.Resolve(Of UserService)().ResetPassword(id.Value, f.NewPassword).ShowIfFailed(Me)
            End Using
        End Sub

        Private Sub ToggleActive()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row Is Nothing Then Return
            Dim makeActive = row.Active <> "Yes"
            AppHost.Current.Resolve(Of UserService)().SetActive(row.Id, makeActive).ShowIfFailed(Me)
            ReloadUsers()
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property UserName As String
            Public Property FullName As String
            Public Property Email As String
            Public Property Role As String
            Public Property Shop As String
            Public Property Active As String
            Public Property LastLogin As String
        End Class
    End Class

End Namespace
