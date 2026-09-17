Imports System.Windows.Forms
Imports GMS.Core.Security
Imports GMS.Core.Services
Imports GMS.Desktop.App
Imports GMS.Desktop.Forms

Namespace Views

    Public NotInheritable Class CategoriesView
        Inherits ViewBase

        Private ReadOnly _grid As DataGridView = UiKit.MakeGrid()
        Private ReadOnly _canEdit As Boolean

        Public Sub New()
            MyBase.New("Categories")
            _canEdit = AppHost.Current.Session.Principal.HasPermission(PermissionCodes.Categories.Edit)

            Dim refresh = UiKit.SecondaryButton("Refresh")
            AddHandler refresh.Click, Sub() Reload()
            AddAction(refresh)
            If _canEdit Then
                Dim add = UiKit.PrimaryButton("New category")
                AddHandler add.Click, Sub() OpenEditor(Nothing)
                AddAction(add)
                Dim del = UiKit.SecondaryButton("Delete")
                AddHandler del.Click, Sub() DeleteSelected()
                AddAction(del)
            End If

            _grid.Columns.Add(UiKit.TextColumn("Name", "Name", width:=180))
            _grid.Columns.Add(UiKit.TextColumn("Description", "Description", fill:=100))
            _grid.Columns.Add(UiKit.TextColumn("Parent", "Parent", width:=160))
            AddHandler _grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()

            Dim gridWrap As New System.Windows.Forms.Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
            gridWrap.Controls.Add(_grid)
            Body.Controls.Add(gridWrap)

            Reload()
        End Sub

        Public Sub Reload()
            Guarded(Sub()
                        Dim result = AppHost.Current.Resolve(Of CategoryService)().List()
                        If result.Failed Then
                            MessageBox.Show(Me, result.ErrorMessage, "Categories", MessageBoxButtons.OK, MessageBoxIcon.Warning)
                            Return
                        End If
                        Dim byId = result.Value.ToDictionary(Function(c) c.Id, Function(c) c.Name)
                        _grid.DataSource = result.Value.Select(Function(c) New Row With {
                            .Id = c.Id, .Name = c.Name, .Description = c.Description,
                            .Parent = If(c.ParentCategoryId.HasValue, byId.GetValueOrDefault(c.ParentCategoryId.Value, "—"), "—")
                        }).ToList()
                    End Sub)
        End Sub

        Private Sub EditSelected()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row IsNot Nothing Then OpenEditor(row.Id)
        End Sub

        Private Sub OpenEditor(categoryId As Integer?)
            If Not _canEdit Then UiKit.Info(Me, "You do not have permission to edit categories.") : Return
            Using f As New CategoryEditForm(categoryId)
                If f.ShowDialog(Me) = DialogResult.OK Then Reload()
            End Using
        End Sub

        Private Sub DeleteSelected()
            Dim row = TryCast(_grid.CurrentRow?.DataBoundItem, Row)
            If row Is Nothing Then Return
            If Not UiKit.Confirm(Me, $"Delete category '{row.Name}'?") Then Return
            AppHost.Current.Resolve(Of CategoryService)().Delete(row.Id).ShowIfFailed(Me)
            Reload()
        End Sub

        Private NotInheritable Class Row
            Public Property Id As Integer
            Public Property Name As String
            Public Property Description As String
            Public Property Parent As String
        End Class
    End Class

End Namespace
