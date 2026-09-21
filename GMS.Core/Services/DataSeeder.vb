Imports System.Linq
Imports GMS.Core.Abstractions
Imports GMS.Core.Enums
Imports GMS.Core.Models
Imports GMS.Core.Security

Namespace Services

    ''' <summary>
    ''' Populates the store with the rows the system needs to run (permissions,
    ''' system roles, one administrator, default settings) and, optionally, a small
    ''' set of demo records. Storage-agnostic: it works against the in-memory unit
    ''' of work now and the Entity Framework one later, unchanged.
    ''' </summary>
    Public NotInheritable Class DataSeeder

        Public Const DefaultAdminUserName As String = "admin"
        Public Const DefaultAdminPassword As String = "ChangeMe#2026"

        Private ReadOnly _uow As IUnitOfWork
        Private ReadOnly _hasher As IPasswordHasher
        Private ReadOnly _clock As IClock

        Public Sub New(uow As IUnitOfWork, hasher As IPasswordHasher, clock As IClock)
            _uow = uow
            _hasher = hasher
            _clock = clock
        End Sub

        ''' <summary>Idempotent: safe to call on every startup.</summary>
        Public Sub SeedBaseline()
            SeedBaseline(organizationId:=Nothing)
        End Sub

        ''' <summary>Idempotent: safe to call for a new organization.</summary>
        Public Sub SeedBaseline(organizationId As Integer?)
            SeedPermissions()
            Dim roles = SeedRoles()
            If organizationId.HasValue Then
                SeedAdminUser(roles.admin, organizationId.Value)
                SeedSettings(organizationId.Value)
            Else
                SeedAdminUser(roles.admin)
                SeedSettings()
            End If
            _uow.SaveChanges()
        End Sub

        Private Sub SeedPermissions()
            Dim repo = _uow.Repository(Of Permission)()
            Dim existing = repo.Query().Select(Function(p) p.Code).ToHashSet(StringComparer.OrdinalIgnoreCase)
            For Each def In PermissionCodes.Catalog
                If Not existing.Contains(def.Code) Then
                    repo.Add(New Permission With {.Code = def.Code, .Category = def.Category, .Description = def.Description})
                End If
            Next
        End Sub

        Private Function SeedRoles() As (admin As Role, manager As Role, staff As Role)
            Dim roleRepo = _uow.Repository(Of Role)()

            Dim admin = EnsureRole(roleRepo, "Admin", "Full access to every module.")
            Dim manager = EnsureRole(roleRepo, "Manager", "Day-to-day operations and reporting.")
            Dim staff = EnsureRole(roleRepo, "Staff", "Create transactions and view catalogue.")

            SetRolePermissions(admin, PermissionCodes.AllCodes())

            SetRolePermissions(manager, {
                PermissionCodes.Products.View, PermissionCodes.Products.Edit,
                PermissionCodes.Categories.View, PermissionCodes.Categories.Edit,
                PermissionCodes.Customers.View, PermissionCodes.Customers.Edit,
                PermissionCodes.Suppliers.View, PermissionCodes.Suppliers.Edit,
                PermissionCodes.Transactions.View, PermissionCodes.Transactions.Create,
                PermissionCodes.Transactions.Confirm, PermissionCodes.Transactions.Cancel,
                PermissionCodes.Inventory.View, PermissionCodes.Inventory.Adjust,
                PermissionCodes.Reports.View, PermissionCodes.Audit.View
            })

            SetRolePermissions(staff, {
                PermissionCodes.Products.View, PermissionCodes.Categories.View,
                PermissionCodes.Customers.View, PermissionCodes.Customers.Edit,
                PermissionCodes.Suppliers.View,
                PermissionCodes.Transactions.View, PermissionCodes.Transactions.Create,
                PermissionCodes.Inventory.View
            })

            Return (admin, manager, staff)
        End Function

        Private Function EnsureRole(repo As IRepository(Of Role), name As String, description As String) As Role
            Dim role = repo.Query().FirstOrDefault(Function(r) r.Name.ToLower() = name.ToLower())
            If role Is Nothing Then
                role = New Role With {.Name = name, .Description = description, .IsSystem = True}
                repo.Add(role)
            End If
            Return role
        End Function

        Private Sub SetRolePermissions(role As Role, codes As IEnumerable(Of String))
            Dim permRepo = _uow.Repository(Of Permission)()
            Dim rpRepo = _uow.Repository(Of RolePermission)()
            Dim wanted = codes.ToHashSet(StringComparer.OrdinalIgnoreCase)
            Dim existing = rpRepo.Query().Where(Function(rp) rp.RoleId = role.Id).Select(Function(rp) rp.PermissionId).ToHashSet()

            For Each perm In permRepo.Query().Where(Function(p) wanted.Contains(p.Code))
                If Not existing.Contains(perm.Id) Then
                    rpRepo.Add(New RolePermission With {.RoleId = role.Id, .PermissionId = perm.Id})
                End If
            Next
        End Sub

        Private Sub SeedAdminUser(adminRole As Role)
            SeedAdminUser(adminRole, organizationId:=1)
        End Sub

        Private Sub SeedAdminUser(adminRole As Role, organizationId As Integer)
            Dim repo = _uow.Repository(Of User)()
            ' For org-specific seeding, ensure no existing admin in that org
            If organizationId > 1 Then
                If repo.Query().Any(Function(u) u.UserName.ToLower() = DefaultAdminUserName AndAlso u.OrganizationId = organizationId) Then Return
            Else
                If repo.Query().Any(Function(u) u.UserName.ToLower() = DefaultAdminUserName) Then Return
            End If

            repo.Add(New User With {
                .UserName = DefaultAdminUserName,
                .Email = "admin@example.com",
                .FullName = "System Administrator",
                .RoleId = adminRole.Id,
                .OrganizationId = organizationId,
                .IsActive = True,
                .MustChangePassword = True,
                .PasswordHash = _hasher.Hash(DefaultAdminPassword),
                .CreatedAtUtc = _clock.UtcNow
            })
        End Sub

        Private Sub SeedSettings()
            SeedSettings(organizationId:=1)
        End Sub

        Private Sub SeedSettings(organizationId As Integer)
            Dim repo = _uow.Repository(Of AppSetting)()
            Dim defaults As New Dictionary(Of String, String) From {
                {SettingKeys.CompanyName, "My Business"},
                {SettingKeys.CurrencyCode, "KES"},
                {SettingKeys.DefaultTaxRatePercent, "0"},
                {SettingKeys.LowStockScanEnabled, "true"},
                {SettingKeys.TimeZone, "Africa/Nairobi"}
            }
            For Each kv In defaults
                Dim checkQuery = If(organizationId > 1,
                    repo.Query().Any(Function(s) s.Key = kv.Key AndAlso s.OrganizationId = organizationId),
                    repo.Query().Any(Function(s) s.Key = kv.Key))

                If Not checkQuery Then
                    repo.Add(New AppSetting With {
                        .Key = kv.Key,
                        .Value = kv.Value,
                        .OrganizationId = organizationId
                    })
                End If
            Next
        End Sub

        ''' <summary>
        ''' Adds a handful of demo records if the catalogue is empty. Uses the real
        ''' services so stock movements and totals are produced the normal way.
        ''' </summary>
        Public Sub SeedDemo(categories As CategoryService, products As ProductService, transactions As TransactionService,
                            customers As CustomerService, suppliers As SupplierService)
            If _uow.Repository(Of Product)().Query().Any() Then Return

            Dim general = categories.Create("General", "Uncategorised items", Nothing)
            Dim office = categories.Create("Office Supplies", "Stationery and consumables", Nothing)

            Dim supplier = suppliers.Create(New SupplierInput With {.Name = "Acme Wholesale", .Email = "sales@acme.example"})
            Dim customer = customers.Create(New CustomerInput With {.Name = "Walk-in Customer", .Code = "WALKIN"})

            Dim p1 = products.Create(New ProductInput With {
                .Sku = "PEN-BLUE", .Name = "Ballpoint Pen (Blue)", .CategoryId = office.Value.Id,
                .UnitPrice = 1.2D, .CostPrice = 0.4D, .ReorderLevel = 50D})
            Dim p2 = products.Create(New ProductInput With {
                .Sku = "PAD-A4", .Name = "A4 Notepad", .CategoryId = office.Value.Id,
                .UnitPrice = 3.5D, .CostPrice = 1.5D, .ReorderLevel = 20D})
            Dim p3 = products.Create(New ProductInput With {
                .Sku = "BOX-SM", .Name = "Small Storage Box", .CategoryId = general.Value.Id,
                .UnitPrice = 6D, .CostPrice = 3D, .ReorderLevel = 10D})

            ' Opening stock via a confirmed purchase.
            Dim po = transactions.CreateDraft(TransactionType.Purchase, supplier.Value.Id, _clock.UtcNow, "Opening stock")
            transactions.AddLine(po.Value.Id, New TransactionLineInput With {.ProductId = p1.Value.Id, .Quantity = 200D})
            transactions.AddLine(po.Value.Id, New TransactionLineInput With {.ProductId = p2.Value.Id, .Quantity = 40D})
            transactions.AddLine(po.Value.Id, New TransactionLineInput With {.ProductId = p3.Value.Id, .Quantity = 25D})
            transactions.Confirm(po.Value.Id)

            ' One confirmed sale.
            Dim so = transactions.CreateDraft(TransactionType.Sale, customer.Value.Id, _clock.UtcNow, "First sale")
            transactions.AddLine(so.Value.Id, New TransactionLineInput With {.ProductId = p1.Value.Id, .Quantity = 12D})
            transactions.AddLine(so.Value.Id, New TransactionLineInput With {.ProductId = p3.Value.Id, .Quantity = 2D})
            transactions.Confirm(so.Value.Id)
        End Sub
    End Class

End Namespace
