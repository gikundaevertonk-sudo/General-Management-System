Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    Public NotInheritable Class RoleDetail
        Public Property Role As Role
        Public Property PermissionCodes As IReadOnlyCollection(Of String) = New List(Of String)()
        Public Property UserCount As Integer
    End Class

    Public NotInheritable Class RoleService
        Inherits ServiceBase

        Public Sub New(uow As IUnitOfWork, currentUser As ICurrentUser, clock As IClock)
            MyBase.New(uow, currentUser, clock)
        End Sub

        Public Function List() As Result(Of IReadOnlyList(Of RoleDetail))
            If Denied(PermissionCodes.Roles.View) Then Return Forbidden(Of IReadOnlyList(Of RoleDetail))()

            ' Materialise everything first: the RoleDetail projection below combines rows from
            ' four tables in memory, which a SQL provider cannot translate as one query.
            Dim rolePerms = Uow.Repository(Of RolePermission)().Query().ToList()
            Dim permsById = Uow.Repository(Of Permission)().Query().ToList().ToDictionary(Function(p) p.Id, Function(p) p.Code)
            Dim userCounts = Uow.Repository(Of User)().Query().ToList().
                GroupBy(Function(u) u.RoleId).ToDictionary(Function(g) g.Key, Function(g) g.Count())

            Dim details = Uow.Repository(Of Role)().Query().OrderBy(Function(r) r.Name).ToList().
                Select(Function(r) New RoleDetail With {
                    .Role = r,
                    .PermissionCodes = rolePerms.Where(Function(rp) rp.RoleId = r.Id).
                        Select(Function(rp) permsById.GetValueOrDefault(rp.PermissionId, String.Empty)).
                        Where(Function(c) c <> String.Empty).ToList(),
                    .UserCount = userCounts.GetValueOrDefault(r.Id, 0)
                }).ToList()

            Return Result(Of IReadOnlyList(Of RoleDetail)).Ok(details)
        End Function

        Public Function Create(name As String, description As String) As Result(Of Role)
            If Denied(PermissionCodes.Roles.Manage) Then Return Forbidden(Of Role)()
            If String.IsNullOrWhiteSpace(name) Then Return Result(Of Role).Fail("Name is required.")

            Dim repo = Uow.Repository(Of Role)()
            If repo.Query().Any(Function(r) r.Name.ToLower() = name.Trim().ToLower()) Then
                Return Result(Of Role).Fail("A role with that name already exists.")
            End If

            Dim entity As New Role With {
                .Name = name.Trim(),
                .Description = If(description, String.Empty).Trim(),
                .IsSystem = False
            }
            repo.Add(entity)
            Uow.SaveChanges()
            Return Result(Of Role).Ok(entity)
        End Function

        Public Function Rename(id As Integer, name As String, description As String) As Result
            If Denied(PermissionCodes.Roles.Manage) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Role)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Role")
            If entity.IsSystem Then Return Result.Fail("System roles cannot be renamed.")
            If String.IsNullOrWhiteSpace(name) Then Return Result.Fail("Name is required.")

            entity.Name = name.Trim()
            entity.Description = If(description, String.Empty).Trim()
            repo.Update(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        Public Function Delete(id As Integer) As Result
            If Denied(PermissionCodes.Roles.Manage) Then Return Forbidden()
            Dim repo = Uow.Repository(Of Role)()
            Dim entity = repo.GetById(id)
            If entity Is Nothing Then Return NotFound("Role")
            If entity.IsSystem Then Return Result.Fail("System roles cannot be deleted.")
            If Uow.Repository(Of User)().Query().Any(Function(u) u.RoleId = id) Then
                Return Result.Fail("This role is still assigned to one or more users.")
            End If

            For Each rp In Uow.Repository(Of RolePermission)().Query().Where(Function(x) x.RoleId = id).ToList()
                Uow.Repository(Of RolePermission)().Remove(rp)
            Next
            repo.Remove(entity)
            Uow.SaveChanges()
            Return Result.Ok()
        End Function

        ''' <summary>Replaces the role's permission set with exactly <paramref name="codes"/>.</summary>
        Public Function SetPermissions(roleId As Integer, codes As IEnumerable(Of String)) As Result
            If Denied(PermissionCodes.Roles.Manage) Then Return Forbidden()
            Dim role = Uow.Repository(Of Role)().GetById(roleId)
            If role Is Nothing Then Return NotFound("Role")

            Dim wanted = New HashSet(Of String)(
                If(codes, Enumerable.Empty(Of String)()), StringComparer.OrdinalIgnoreCase)

            Dim allPerms = Uow.Repository(Of Permission)().Query().ToList()
            Dim unknown = wanted.Where(Function(c) Not allPerms.Any(Function(p) p.Code.Equals(c, StringComparison.OrdinalIgnoreCase))).ToList()
            If unknown.Any() Then Return Result.Fail($"Unknown permission code(s): {String.Join(", ", unknown)}")

            Dim rpRepo = Uow.Repository(Of RolePermission)()
            Dim current = rpRepo.Query().Where(Function(rp) rp.RoleId = roleId).ToList()

            For Each rp In current
                Dim code = allPerms.First(Function(p) p.Id = rp.PermissionId).Code
                If Not wanted.Contains(code) Then rpRepo.Remove(rp)
            Next

            For Each perm In allPerms.Where(Function(p) wanted.Contains(p.Code))
                If Not current.Any(Function(rp) rp.PermissionId = perm.Id) Then
                    rpRepo.Add(New RolePermission With {.RoleId = roleId, .PermissionId = perm.Id})
                End If
            Next

            Uow.SaveChanges()
            Return Result.Ok()
        End Function
    End Class

End Namespace
