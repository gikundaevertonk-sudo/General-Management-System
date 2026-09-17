Imports System.Windows.Forms
Imports GMS.Core.Enums
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class NotificationsView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _unreadOnly As New CheckBox()

        Public Sub New()
            MyBase.New("Notifications")

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            Dim markAll = UiKit.SecondaryButton("Mark all read")
            AddHandler markAll.Click, Sub()
                                          AppHost.Current.Resolve(Of NotificationService)().MarkAllReadForCurrentUser()
                                          Reload()
                                      End Sub
            AddAction(markAll)

            If AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Inventory.View) Then
                Dim scan = UiKit.PrimaryButton("Run low-stock scan")
                AddHandler scan.Click, Sub()
                                           Dim result = AppHost.Current.Resolve(Of NotificationService)().RunLowStockScan()
                                           UiKit.Info(Me, If(result.Succeeded, $"{result.Value} new alert(s).", result.ErrorMessage))
                                           Reload()
                                       End Sub
                AddAction(scan)
            End If

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 36, .Padding = New Padding(8, 8, 8, 0)}
            _unreadOnly.Text = "Unread only"
            _unreadOnly.AutoSize = True
            AddHandler _unreadOnly.CheckedChanged, Sub() Reload()
            bar.Controls.Add(_unreadOnly)

            _grid.Columns.Add(UiKit.TextColumn("When (UTC)", "When", width:=150))
            _grid.Columns.Add(UiKit.TextColumn("Severity", "Severity", width:=90))
            _grid.Columns.Add(UiKit.TextColumn("Title", "Title", width:=220))
            _grid.Columns.Add(UiKit.TextColumn("Message", "Message", fill:=100))
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then MarkSelectedRead()

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)

            Reload()
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim items = AppHost.Current.Resolve(Of NotificationService)().
                            ListForCurrentUser(_unreadOnly.Checked, take:=200)
                        _grid.DataSource = items.Select(Function(n) New Row With {
                            .Id = n.Id, .When = n.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                            .Severity = n.Severity.ToString(), .Title = n.Title, .Message = n.Message,
                            .IsRead = n.IsRead
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub MarkSelectedRead()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row Is Nothing OrElse row.IsRead Then Return
            AppHost.Current.Resolve(Of NotificationService)().MarkRead(row.Id)
            Reload()
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property [When] As String
            Public Property Severity As String
            Public Property Title As String
            Public Property Message As String
            Public Property IsRead As Boolean
        End Class
    End Class

End Namespace
