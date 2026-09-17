Namespace Security

    ''' <summary>One row of the permission catalogue, used to seed the store.</summary>
    Public Structure PermissionDefinition
        Public ReadOnly Property Code As String
        Public ReadOnly Property Category As String
        Public ReadOnly Property Description As String

        Public Sub New(code As String, category As String, description As String)
            Me.Code = code
            Me.Category = category
            Me.Description = description
        End Sub
    End Structure

    ''' <summary>
    ''' Stable string identifiers for every capability the system checks. Services
    ''' and UI call <c>ICurrentUser.HasPermission(PermissionCodes.Products.Edit)</c>.
    ''' </summary>
    Public NotInheritable Class PermissionCodes

        Private Sub New()
        End Sub

        Public NotInheritable Class Users
            Public Const View As String = "users.view"
            Public Const Manage As String = "users.manage"
        End Class

        Public NotInheritable Class Roles
            Public Const View As String = "roles.view"
            Public Const Manage As String = "roles.manage"
        End Class

        Public NotInheritable Class Products
            Public Const View As String = "products.view"
            Public Const Edit As String = "products.edit"
            Public Const Delete As String = "products.delete"
        End Class

        Public NotInheritable Class Categories
            Public Const View As String = "categories.view"
            Public Const Edit As String = "categories.edit"
        End Class

        Public NotInheritable Class Customers
            Public Const View As String = "customers.view"
            Public Const Edit As String = "customers.edit"
        End Class

        Public NotInheritable Class Suppliers
            Public Const View As String = "suppliers.view"
            Public Const Edit As String = "suppliers.edit"
        End Class

        Public NotInheritable Class Transactions
            Public Const View As String = "transactions.view"
            Public Const Create As String = "transactions.create"
            Public Const Confirm As String = "transactions.confirm"
            Public Const Cancel As String = "transactions.cancel"
        End Class

        Public NotInheritable Class Inventory
            Public Const View As String = "inventory.view"
            Public Const Adjust As String = "inventory.adjust"
        End Class

        Public NotInheritable Class Reports
            Public Const View As String = "reports.view"
        End Class

        Public NotInheritable Class Audit
            Public Const View As String = "audit.view"
        End Class

        Public NotInheritable Class Settings
            Public Const Manage As String = "settings.manage"
        End Class

        ''' <summary>Full catalogue for seeding the Permission table.</summary>
        Public Shared ReadOnly Property Catalog As IReadOnlyList(Of PermissionDefinition) = New List(Of PermissionDefinition) From {
            New PermissionDefinition(Users.View, "Users", "View user accounts"),
            New PermissionDefinition(Users.Manage, "Users", "Create, edit and deactivate users"),
            New PermissionDefinition(Roles.View, "Roles", "View roles and permissions"),
            New PermissionDefinition(Roles.Manage, "Roles", "Create roles and assign permissions"),
            New PermissionDefinition(Products.View, "Products", "View products and stock levels"),
            New PermissionDefinition(Products.Edit, "Products", "Create and edit products"),
            New PermissionDefinition(Products.Delete, "Products", "Delete products"),
            New PermissionDefinition(Categories.View, "Categories", "View categories"),
            New PermissionDefinition(Categories.Edit, "Categories", "Create and edit categories"),
            New PermissionDefinition(Customers.View, "Customers", "View customers"),
            New PermissionDefinition(Customers.Edit, "Customers", "Create and edit customers"),
            New PermissionDefinition(Suppliers.View, "Suppliers", "View suppliers"),
            New PermissionDefinition(Suppliers.Edit, "Suppliers", "Create and edit suppliers"),
            New PermissionDefinition(Transactions.View, "Transactions", "View transactions"),
            New PermissionDefinition(Transactions.Create, "Transactions", "Create draft transactions"),
            New PermissionDefinition(Transactions.Confirm, "Transactions", "Confirm transactions and post stock"),
            New PermissionDefinition(Transactions.Cancel, "Transactions", "Cancel confirmed transactions"),
            New PermissionDefinition(Inventory.View, "Inventory", "View stock ledger and valuation"),
            New PermissionDefinition(Inventory.Adjust, "Inventory", "Make manual stock adjustments"),
            New PermissionDefinition(Reports.View, "Reports", "Run and export reports"),
            New PermissionDefinition(Audit.View, "Audit", "View the audit trail"),
            New PermissionDefinition(Settings.Manage, "Settings", "Change application settings")
        }

        Public Shared Iterator Function AllCodes() As IEnumerable(Of String)
            For Each item In Catalog
                Yield item.Code
            Next
        End Function
    End Class

End Namespace
