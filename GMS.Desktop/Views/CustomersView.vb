Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    Public NotInheritable Class CustomersView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _search As New TextBox()
        Private ReadOnly _pager As New Label()
        Private ReadOnly _canEdit As Boolean
        Private _page As Integer = 1
        Private _totalPages As Integer = 1

        Public Sub New()
            MyBase.New("Customers")
            _canEdit = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Customers.Edit)

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)
            If _canEdit Then
                Dim add = UiKit.PrimaryButton("New customer")
                AddHandler add.Click, Sub() OpenEditor(Nothing)
                AddAction(add)
            End If

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(8)}
            _search.PlaceholderText = "Search name, code or email…"
            _search.SetBounds(8, 9, 260, 24)
            AddHandler _search.KeyDown, Sub(s, e)
                                            If e.KeyCode = Keys.Enter Then GoFirst()
                                        End Sub
            Dim go = UiKit.SecondaryButton("Search") : go.SetBounds(276, 6, 80, 28)
            AddHandler go.Click, Sub() GoFirst()
            Dim prev = UiKit.SecondaryButton("‹ Prev") : prev.SetBounds(368, 6, 74, 28)
            AddHandler prev.Click, Sub() GoPage(-1)
            Dim nxt = UiKit.SecondaryButton("Next ›") : nxt.SetBounds(448, 6, 74, 28)
            AddHandler nxt.Click, Sub() GoPage(1)
            _pager.SetBounds(532, 12, 260, 20) : _pager.ForeColor = UiKit.MutedText
            bar.Controls.AddRange({_search, go, prev, nxt, _pager})

            _grid.Columns.Add(UiKit.TextColumn("Code", "Code", width:=90))
            _grid.Columns.Add(UiKit.TextColumn("Name", "Name", fill:=100))
            _grid.Columns.Add(UiKit.TextColumn("Contact", "Contact", width:=140))
            _grid.Columns.Add(UiKit.TextColumn("Email", "Email", width:=180))
            _grid.Columns.Add(UiKit.TextColumn("Phone", "Phone", width:=120))
            _grid.Columns.Add(UiKit.TextColumn("Active", "Active", width:=64))
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()

            Dim gridWrap As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)
            Body.Controls.Add(bar)

            Reload()
        End Sub

        Private Sub GoFirst()
            _page = 1
            Reload()
        End Sub

        Private Sub GoPage(delta As Integer)
            Dim target = _page + delta
            If target >= 1 AndAlso target <= _totalPages Then
                _page = target
                Reload()
            End If
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim opts As New QueryOptions With {.Search = _search.Text, .Page = _page, .PageSize = 25}
                        Dim result = AppHost.Current.Resolve(Of CustomerService)().Search(opts)
                        If result.Failed Then
                            MessageBox.Show(Me, result.ErrorMessage, "Customers", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If
                        Dim page = result.Value
                        _totalPages = Math.Max(1, page.TotalPages)
                        _pager.Text = $"Page {page.Page} of {_totalPages}  ·  {page.TotalCount} item(s)"
                        _grid.DataSource = page.Items.Select(Function(c) New Row With {
                            .Id = c.Id, .Code = c.Code, .Name = c.Name, .Contact = c.ContactName,
                            .Email = c.Email, .Phone = c.Phone, .Active = If(c.IsActive, "Yes", "No")
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub EditSelected()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row IsNot Nothing Then OpenEditor(row.Id)
        End Sub

        Private Sub OpenEditor(customerId As Integer?)
            If Not _canEdit Then UiKit.Info(Me, "You do not have permission to edit customers.") : Return
            Using f As New CustomerEditForm(customerId)
                If f.ShowDialog(Me) = DialogResult.OK Then Reload()
            End Using
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property Code As String
            Public Property Name As String
            Public Property Contact As String
            Public Property Email As String
            Public Property Phone As String
            Public Property Active As String
        End Class
    End Class

End Namespace
