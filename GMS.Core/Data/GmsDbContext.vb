Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports Microsoft.EntityFrameworkCore
Imports GMS.Core.Abstractions
Imports GMS.Core.Common
Imports GMS.Core.Models

Namespace Data

    ''' <summary>
    ''' EF Core context for the PostgreSQL / Supabase database. The schema is owned
    ''' by <c>db/supabase/schema.sql</c>; this context maps to those existing tables
    ''' (snake_case) and never runs migrations.
    ''' </summary>
    Public NotInheritable Class GmsDbContext
        Inherits DbContext

        Private ReadOnly _tenantContext As ITenantContext

        Public Sub New(options As DbContextOptions(Of GmsDbContext), tenantContext As ITenantContext)
            MyBase.New(options)
            _tenantContext = Guard.NotNull(tenantContext)
        End Sub

        ''' <summary>
        ''' The organization every tenant-scoped query and insert is confined to.
        ''' </summary>
        ''' <remarks>
        ''' Read through <see cref="ITenantContext"/> on each access rather than captured once.
        ''' It must not be cached in a field, and above all must not be Shared: this used to be
        ''' a static set per request by GMS.Web's TenantContextMiddleware, so two concurrent
        ''' requests from different organizations raced - one could overwrite the other's tenant
        ''' mid-request and serve it the wrong organization's rows. ITenantContext is registered
        ''' scoped in both front ends, so reading it here keeps the value per-request on the web
        ''' and per-session on the desktop. Reading per access (not in the constructor) also
        ''' matters for GMS.Desktop, which builds one context for the whole process: the tenant
        ''' is not known until the user signs in, well after the context is created.
        ''' </remarks>
        Private ReadOnly Property CurrentTenantId As Integer
            Get
                Return _tenantContext.OrganizationId
            End Get
        End Property

        Public Property Organizations As DbSet(Of Organization)
        Public Property Subscriptions As DbSet(Of Subscription)
        Public Property Roles As DbSet(Of Role)
        Public Property Permissions As DbSet(Of Permission)
        Public Property RolePermissions As DbSet(Of RolePermission)
        Public Property Users As DbSet(Of User)
        Public Property Categories As DbSet(Of Category)
        Public Property Products As DbSet(Of Product)
        Public Property Suppliers As DbSet(Of Supplier)
        Public Property Customers As DbSet(Of Customer)
        Public Property Transactions As DbSet(Of Transaction)
        Public Property TransactionLines As DbSet(Of TransactionLine)
        Public Property StockMovements As DbSet(Of StockMovement)
        Public Property AuditEntries As DbSet(Of AuditEntry)
        Public Property Notifications As DbSet(Of Notification)
        Public Property AppSettings As DbSet(Of AppSetting)

        Protected Overrides Sub ConfigureConventions(builder As ModelConfigurationBuilder)
            ' One scale wide enough for money (2), quantities (3) and tax rates (4);
            ' the PostgreSQL columns still enforce their own precision.
            builder.Properties(Of Decimal)().HavePrecision(18, 6)
        End Sub

        Protected Overrides Sub OnModelCreating(b As ModelBuilder)
            ' Organization relationships
            b.Entity(Of Organization)().HasMany(Function(o) o.Users).WithOne(Function(u) u.Organization).HasForeignKey(Function(u) u.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Products).WithOne(Function(p) p.Organization).HasForeignKey(Function(p) p.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Categories).WithOne(Function(c) c.Organization).HasForeignKey(Function(c) c.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Suppliers).WithOne(Function(s) s.Organization).HasForeignKey(Function(s) s.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Customers).WithOne(Function(c) c.Organization).HasForeignKey(Function(c) c.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Transactions).WithOne(Function(t) t.Organization).HasForeignKey(Function(t) t.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.StockMovements).WithOne(Function(m) m.Organization).HasForeignKey(Function(m) m.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.Notifications).WithOne(Function(n) n.Organization).HasForeignKey(Function(n) n.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.AppSettings).WithOne(Function(s) s.Organization).HasForeignKey(Function(s) s.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasMany(Function(o) o.AuditEntries).WithOne(Function(a) a.Organization).HasForeignKey(Function(a) a.OrganizationId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of Organization)().HasOne(Function(o) o.Subscription).WithOne(Function(s) s.Organization).HasForeignKey(Of Subscription)(Function(s) s.OrganizationId).OnDelete(DeleteBehavior.Cascade)

            b.Entity(Of RolePermission)().HasKey(Function(rp) New With {rp.RoleId, rp.PermissionId})

            b.Entity(Of RolePermission)().
                HasOne(Function(rp) rp.Role).WithMany(Function(r) r.Permissions).
                HasForeignKey(Function(rp) rp.RoleId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of RolePermission)().
                HasOne(Function(rp) rp.Permission).WithMany(Function(p) p.Roles).
                HasForeignKey(Function(rp) rp.PermissionId).OnDelete(DeleteBehavior.Cascade)

            b.Entity(Of User)().
                HasOne(Function(u) u.Role).WithMany(Function(r) r.Users).
                HasForeignKey(Function(u) u.RoleId).OnDelete(DeleteBehavior.Restrict)
            b.Entity(Of User)().
                HasOne(Function(u) u.Organization).WithMany(Function(o) o.Users).
                HasForeignKey(Function(u) u.OrganizationId).OnDelete(DeleteBehavior.Cascade)

            b.Entity(Of Category)().
                HasOne(Function(c) c.ParentCategory).WithMany(Function(c) c.Children).
                HasForeignKey(Function(c) c.ParentCategoryId).OnDelete(DeleteBehavior.SetNull)

            b.Entity(Of Product)().
                HasOne(Function(p) p.Category).WithMany(Function(c) c.Products).
                HasForeignKey(Function(p) p.CategoryId).OnDelete(DeleteBehavior.SetNull)

            b.Entity(Of Transaction)().
                HasOne(Function(t) t.Customer).WithMany().
                HasForeignKey(Function(t) t.CustomerId).OnDelete(DeleteBehavior.Restrict)
            b.Entity(Of Transaction)().
                HasOne(Function(t) t.Supplier).WithMany().
                HasForeignKey(Function(t) t.SupplierId).OnDelete(DeleteBehavior.Restrict)

            b.Entity(Of TransactionLine)().
                HasOne(Function(l) l.Transaction).WithMany(Function(t) t.Lines).
                HasForeignKey(Function(l) l.TransactionId).OnDelete(DeleteBehavior.Cascade)
            b.Entity(Of TransactionLine)().
                HasOne(Function(l) l.Product).WithMany().
                HasForeignKey(Function(l) l.ProductId).OnDelete(DeleteBehavior.Restrict)

            b.Entity(Of StockMovement)().
                HasOne(Function(m) m.Product).WithMany(Function(p) p.Movements).
                HasForeignKey(Function(m) m.ProductId).OnDelete(DeleteBehavior.Restrict)
            b.Entity(Of StockMovement)().
                HasOne(Function(m) m.TransactionLine).WithMany().
                HasForeignKey(Function(m) m.TransactionLineId).OnDelete(DeleteBehavior.SetNull)
            b.Entity(Of StockMovement)().Ignore(Function(m) m.SignedQuantity)

            b.Entity(Of AuditEntry)().Property(Function(a) a.ChangesJson).HasColumnType("jsonb")

            ' Global query filters for multi-tenancy: automatically filter all tenant-scoped entities
            b.Entity(Of User)().HasQueryFilter(Function(u) u.OrganizationId = CurrentTenantId)
            b.Entity(Of Category)().HasQueryFilter(Function(c) c.OrganizationId = CurrentTenantId)
            b.Entity(Of Product)().HasQueryFilter(Function(p) p.OrganizationId = CurrentTenantId)
            b.Entity(Of Supplier)().HasQueryFilter(Function(s) s.OrganizationId = CurrentTenantId)
            b.Entity(Of Customer)().HasQueryFilter(Function(c) c.OrganizationId = CurrentTenantId)
            b.Entity(Of Transaction)().HasQueryFilter(Function(t) t.OrganizationId = CurrentTenantId)
            b.Entity(Of StockMovement)().HasQueryFilter(Function(m) m.OrganizationId = CurrentTenantId)
            b.Entity(Of Notification)().HasQueryFilter(Function(n) n.OrganizationId = CurrentTenantId)
            b.Entity(Of AppSetting)().HasQueryFilter(Function(s) s.OrganizationId = CurrentTenantId)
            b.Entity(Of AuditEntry)().HasQueryFilter(Function(a) a.OrganizationId = CurrentTenantId)

            ' TransactionLine is the one tenant-scoped table with no organization_id of its own;
            ' it belongs to a tenant only through its parent transaction, so it filters through
            ' that. Without this, Repository(Of TransactionLine).Query() returns every
            ' organization's line items. It also silences EF's warning that Product (filtered)
            ' is the required end of a relationship with TransactionLine (unfiltered), where a
            ' filtered-out product can make the owning line behave unpredictably.
            b.Entity(Of TransactionLine)().HasQueryFilter(
                Function(l) l.Transaction.OrganizationId = CurrentTenantId)

            ' StockMovement.UserId, AuditEntry.UserId and Notification.TargetUserId have no
            ' navigation property, so EF leaves them as plain integer columns (no FK) â€” matching
            ' the schema, where audit/notification rows must outlive a deleted user.

            ApplySnakeCaseNames(b)
        End Sub

        Public Const OrganizationIdProperty As String = "OrganizationId"

        ''' <summary>
        ''' Stamps the current tenant onto new rows that did not set it themselves, then saves.
        ''' </summary>
        ''' <remarks>
        ''' Every tenant-scoped entity declares its own <c>OrganizationId</c>, and the query
        ''' filters above are keyed on it â€” but the services that create catalogue, transaction,
        ''' stock, notification and audit rows never populate it, so it arrives as 0. Against
        ''' PostgreSQL that breaks the foreign key to <c>organizations</c>; against the in-memory
        ''' store, which enforces neither foreign keys nor the query filters, it goes unnoticed.
        ''' Filling it here rather than in each service means a new service cannot forget it.
        ''' Only an unset (0) value is filled, so a caller that targets a specific organization â€”
        ''' <c>DataSeeder.SeedBaseline(organizationId)</c> onboarding a new tenant - still wins.
        ''' </remarks>
        Public Overrides Function SaveChanges(acceptAllChangesOnSuccess As Boolean) As Integer
            StampTenantOnNewRows()
            Return MyBase.SaveChanges(acceptAllChangesOnSuccess)
        End Function

        Public Overrides Function SaveChangesAsync(acceptAllChangesOnSuccess As Boolean,
                                                   Optional cancellationToken As CancellationToken = Nothing) As Task(Of Integer)
            StampTenantOnNewRows()
            Return MyBase.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken)
        End Function

        Private Sub StampTenantOnNewRows()
            ' Named 'tracked', not 'entry': VB is case-insensitive, so a local called 'entry'
            ' would shadow DbContext.Entry() and fail to compile.
            For Each tracked In ChangeTracker.Entries()
                If tracked.State <> EntityState.Added Then Continue For

                Dim prop = tracked.Metadata.FindProperty(OrganizationIdProperty)
                If prop Is Nothing OrElse prop.ClrType IsNot GetType(Integer) Then Continue For

                Dim current = tracked.Property(OrganizationIdProperty)
                If CInt(If(current.CurrentValue, 0)) = 0 Then current.CurrentValue = CurrentTenantId
            Next
        End Sub

        ''' <summary>Renames every table and column to snake_case to match the hand-written schema.</summary>
        Private Shared Sub ApplySnakeCaseNames(b As ModelBuilder)
            For Each entity In b.Model.GetEntityTypes()
                Dim table = entity.GetTableName()
                If table IsNot Nothing Then entity.SetTableName(ToSnake(table))
                For Each prop In entity.GetProperties()
                    prop.SetColumnName(ToSnake(prop.GetColumnName()))
                Next
            Next
        End Sub

        Private Shared Function ToSnake(name As String) As String
            If String.IsNullOrEmpty(name) Then Return name
            Dim sb As New StringBuilder(name.Length + 8)
            For i = 0 To name.Length - 1
                Dim ch = name(i)
                If Char.IsUpper(ch) Then
                    If i > 0 Then sb.Append("_"c)
                    sb.Append(Char.ToLowerInvariant(ch))
                Else
                    sb.Append(ch)
                End If
            Next
            Return sb.ToString()
        End Function
    End Class

End Namespace
