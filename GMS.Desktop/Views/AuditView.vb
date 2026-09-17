Imports System.Drawing
Imports System.Windows.Forms
Imports GMS.Core.Common
Imports GMS.Core.Services
Imports GMS.Desktop.App

Namespace Views

    Public NotInheritable Class AuditView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _search As New TextBox()
        Private ReadOnly _pager As New Label()
        Private _page As Integer = 1
        Private _totalPages As Integer = 1

        Public Sub New()
            MyBase.New("Audit trail")

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)

            Dim bar As New Panel With {.Dock = DockStyle.Top, .Height = 44, .Padding = New Padding(8)}
            _search.PlaceholderText = "Filter by user, entity or id…"
            _search.SetBounds(8, 9, 280, 24)
            AddHandler _search.KeyDown, Sub(s, e)
                                            If e.KeyCode = Keys.Enter Then GoFirst()
                                        End Sub
            Dim go = UiKit.SecondaryButton("Search") : go.SetBounds(296, 6, 80, 28)
            AddHandler go.Click, Sub() GoFirst()
            Dim prev = UiKit.SecondaryButton("‹ Prev") : prev.SetBounds(392, 6, 74, 28)
            AddHandler prev.Click, Sub() GoPage(-1)
            Dim nxt = UiKit.SecondaryButton("Next ›") : nxt.SetBounds(472, 6, 74, 28)
            AddHandler nxt.Click, Sub() GoPage(1)
            _pager.SetBounds(556, 12, 300, 20) : _pager.ForeColor = UiKit.MutedText
            bar.Controls.AddRange({_search, go, prev, nxt, _pager})

            _grid.Columns.Add(UiKit.TextColumn("When (UTC)", "When", width:=150))
            _grid.Columns.Add(UiKit.TextColumn("User", "User", width:=140))
            _grid.Columns.Add(UiKit.TextColumn("Action", "Action", width:=90))
            _grid.Columns.Add(UiKit.TextColumn("Entity", "Entity", width:=140))
            _grid.Columns.Add(UiKit.TextColumn("Id", "EntityId", width:=70))
            _grid.Columns.Add(UiKit.TextColumn("Changes", "Changes", fill:=100))

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
                        Dim res = AppHost.Current.Resolve(Of AuditService)().
                            Query(New QueryOptions With {.Search = _search.Text, .Page = _page, .PageSize = 50})
                        If res.Failed Then UiKit.Info(Me, res.ErrorMessage) : Return

                        Dim page = res.Value
                        _totalPages = Math.Max(1, page.TotalPages)
                        _pager.Text = $"Page {page.Page} of {_totalPages}  ·  {page.TotalCount} entr(y/ies)"
                        _grid.DataSource = page.Items.Select(Function(a) New With {
                            .When = a.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                            .User = a.UserName,
                            .Action = a.Action.ToString(),
                            .Entity = a.EntityName,
                            a.EntityId,
                            .Changes = If(a.ChangesJson = "{}", "", a.ChangesJson)
                        }).ToList()
                    End Sub)
        End Sub
    End Class

End Namespace
