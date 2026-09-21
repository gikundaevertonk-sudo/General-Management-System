Imports System.Text
Imports Microsoft.EntityFrameworkCore
Imports GMS.Core.Models

Namespace Data

    ''' <summary>
    ''' EF Core context for the PostgreSQL / Supabase database. The schema is owned
    ''' by <c>db/supabase/schema.sql</c>; this context maps to those existing tables
    ''' (snake_case) and never runs migrations.
    ''' </summary>
    Public NotInheritable Class GmsDbContext
        Inherits DbContext

        Public Sub New(options As DbContextOptions(Of GmsDbContext))
            MyBase.New(options)
        End Sub

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

            ' StockMovement.UserId, AuditEntry.UserId and Notification.TargetUserId have no
            ' navigation property, so EF leaves them as plain integer columns (no FK) — matching
            ' the schema, where audit/notification rows must outlive a deleted user.

            ApplySnakeCaseNames(b)
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
