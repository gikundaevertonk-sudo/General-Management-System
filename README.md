# General Management System (GMS)

A general-purpose management system for **small businesses** — records, users, inventory,
transactions and reports — with a native VB.NET desktop client and a browser-accessible
web client sharing one business-logic library.

## Solution layout

| Project | Language | Type | Purpose |
|---|---|---|---|
| `GMS.Core` | VB.NET | Class library (`net10.0`) | All models, business rules, services. Both front ends depend on this. |
| `GMS.Desktop` | VB.NET | Windows Forms (`net10.0-windows`) | Native desktop client. References `GMS.Core`. |
| `GMS.Web` | C# | ASP.NET Core Razor Pages (`net10.0`) | Browser client (ASP.NET Core has no VB template). References `GMS.Core`. |

Open `GeneralManagementSystem.slnx` in Visual Studio 2022 (17.10+) or build from the CLI.

```bash
dotnet build GeneralManagementSystem.slnx
```

## GMS.Core structure

```
Models/          Domain entities (EntityBase / AuditableEntity + 15 entities)
Enums/           TransactionType, TransactionStatus, StockMovement*, Notification*, AuditAction
Common/          Result / Result(Of T), PagedResult(Of T), QueryOptions, Guard, ServiceException
Abstractions/    IRepository(Of T), IUnitOfWork, ICurrentUser, IPasswordHasher, IClock
Contracts/       DTOs returned to the UI (AuthenticatedUser, DashboardSummary, report rows)
Security/        Pbkdf2PasswordHasher, PermissionCodes catalogue, SystemClock, system/anon principals
Data/            GmsDbContext (EF Core, maps to the existing Postgres schema, no migrations)
Repositories/    InMemory/ (desktop/demo fallback) and Ef/ (PostgreSQL, backs GMS.Web)
Services/        AuthService, UserService, RoleService, CategoryService, ProductService,
                 CustomerService, SupplierService, TransactionService, InventoryService,
                 NotificationService, ReportService, DashboardService, SettingsService,
                 AuditService, DataSeeder
DependencyInjection/  AddGmsCore(), AddGmsCoreForTooling()
```

### Design decisions

- **Domain:** general small-business. Main entity is `Product` (inventory); `Customer` and
  `Supplier` are first-class. One unified `Transaction` document (`Type` = Sale / Purchase /
  AdjustmentIn / AdjustmentOut) with `TransactionLine` rows.
- **Stock is a ledger.** `StockMovement` is the immutable source of truth; `Product.QuantityOnHand`
  is a cache maintained **only** by `InventoryService`. `Reconcile` rebuilds the cache from the ledger.
- **Transactions are immutable once `Confirmed`.** Confirm posts stock; Cancel writes compensating
  movements. Multi-line sales are all-or-nothing on stock availability.
- **RBAC:** one `Role` per user, role → many permissions (string codes in `PermissionCodes`).
  Seeded roles: Admin, Manager, Staff.
- **Passwords:** PBKDF2-HMAC-SHA256, parameters stored with the hash. Never stored in plain text.
- **Results:** services return `Result` / `Result(Of T)` (no exceptions for expected validation
  failures) and `PagedResult(Of T)` for lists.
- **Auditing:** `AuditService.Record` is called explicitly by mutating services (no `SaveChanges`
  interceptor).
- **Every `DateTime` written to the database must be `DateTime.Kind = Utc`.** `timestamptz`
  columns reject `Unspecified`. Do **not** enable `Npgsql.EnableLegacyTimestampBehavior` — it
  doesn't just relax that check, it also converts using the *local machine's* time zone on
  write, silently shifting every stored timestamp by the local UTC offset. Fix at the source
  instead (`DateRange`'s constructor and `TransactionService.CreateDraft` already do this).
- **Service methods avoid VB/C# reserved words as names** — `GetById` (not `Get`), `SetValue`
  (not `Set`) — so both `GMS.Desktop` (VB) and `GMS.Web` (C#) can call them without bracket
  escaping (`.[Get](...)`).
- **EF query results are `NoTracking` by default** (`GmsDbContext`/`AddGmsCorePostgres`). Every
  service already follows read → mutate → `Repository.Update(entity)` → `Uow.SaveChanges()`,
  which explicitly attaches and marks the entity Modified — it never relies on EF's change
  tracker noticing edits. NoTracking-by-default means a second read always reflects the
  database instead of a stale first read, which matters once more than one process (desktop +
  web) can write the same rows.

### Seed data

`DataSeeder.SeedBaseline()` — permissions, the three system roles, one administrator
(`admin` / `ChangeMe#2026`, must change on first sign-in), default `AppSetting`s. Idempotent.
`DataSeeder.SeedDemo(...)` — a few categories/products, one supplier, one customer, an opening-stock
purchase and one sale, so dashboards and reports are not empty.

## Roadmap

1. **GMS.Core** — models + service layer against `IRepository`/`IUnitOfWork`. ✅ *done, builds, smoke-tested*
2. **GMS.Desktop** — Windows Forms UI. ✅ *done, builds, smoke-tested*
3. **GMS.Web** — Razor Pages UI + cookie authentication, responsive. ✅ *done, builds, HTTP-tested*
4. **Persistence** — EF Core + Npgsql against Supabase Postgres. ✅ *done, HTTP- and DB-verified end to end*

## Database (Supabase / PostgreSQL)

Schema lives in **`db/supabase/schema.sql`** (run once in the Supabase SQL Editor; idempotent,
safe to re-run) — 14 snake_case tables, `GmsDbContext` maps to them via a runtime snake-case
rename (no EF migrations; the schema is hand-owned). The optional seed block in that same file
creates the 22 permissions, 3 system roles, the `admin` account and default settings; the app
also seeds baseline data itself on start-up (idempotent either way).

**Both** `GMS.Web` and `GMS.Desktop` connect to the same Supabase database via the
`ConnectionStrings:Gms` config key. Sources, lowest priority to highest:

| Source | Used by |
|---|---|
| `appsettings.json` beside the executable | an **installed** copy of GMS.Desktop |
| user-secrets | developer machines only — never published |
| `ConnectionStrings__Gms` environment variable | deployment, CI |

```bash
dotnet user-secrets set "ConnectionStrings:Gms" "Host=<pooler-host>;Port=5432;Database=postgres;Username=postgres.<project-ref>;Password=<password>;SSL Mode=Require" --project GMS.Web
dotnet user-secrets set "ConnectionStrings:Gms" "<same string>" --project GMS.Desktop
```

Use the **session pooler** host (`aws-<n>-<region>.pooler.supabase.com:5432`, user
`postgres.<project-ref>`) — Supabase's direct `db.<ref>.supabase.co` host is IPv6-only on most
plans, and Npgsql cannot parse the `postgresql://…` URI form the dashboard displays. A quick way
to find the right region: the correct pooler answers a bad password with `28P01`, while every
other region answers `XX000 … tenant or user not found`.

When `ConnectionStrings:Gms` is absent, both apps fall back to the in-memory store (and, only in
that fallback, seed demo data) — useful for a database-free local run. `GMS.Desktop` now says so
with a warning dialog at start-up, because a silent fallback looks exactly like the app losing
its data and resetting the admin password on every launch.

**Testing against this database:** both apps pick up `ConnectionStrings:Gms` automatically the
moment it's configured in user-secrets — including a quick "smoke test" console that merely
references `GMS.Desktop.dll`. There is no separate opt-in for "real database" vs "throwaway
run." Before running anything experimental against a configured project, either remove the
secret temporarily or treat every write as real and clean up afterwards.

## Running the desktop client

```bash
dotnet run --project GMS.Desktop
```

Sign in with **`admin` / `ChangeMe#2026`** — you are prompted to set a new password on first
sign-in. Connects to Supabase Postgres when `ConnectionStrings:Gms` is configured (see
**Database** below); otherwise falls back to an in-memory store seeded with demo data.

### GMS.Desktop structure

```
Program.vb            Entry point: DI, seeding, sign-in loop (forced password change), sign-out
App/
  AppHost.vb          Owns the DI container; the whole app runs in one scope
  SessionContext.vb   Holds the signed-in principal; DesktopCurrentUser : ICurrentUser
  UiKit.vb            Shared control factories, colours, grid helpers, Result -> dialog
Forms/                LoginForm, ChangePasswordForm, MainForm (sidebar shell),
                      ProductEditForm, CategoryEditForm, CustomerEditForm, SupplierEditForm,
                      TransactionEditForm, UserEditForm/SetPasswordForm, StockAdjustForm
Views/                ViewBase (page chrome) + DashboardView, ProductsView, CategoriesView,
                      CustomersView, SuppliersView, TransactionsView, InventoryView, ReportsView,
                      UsersView, NotificationsView, SettingsView, AuditView
```

The **dialog forms** (`LoginForm`, `ChangePasswordForm`, `ProductEditForm`, `UserEditForm`,
`SetPasswordForm`, `StockAdjustForm`, `TransactionEditForm`) use the standard `*.vb` +
`*.Designer.vb` / `InitializeComponent()` pattern, so they **open in the Visual Studio Windows
Forms designer**. The shell (`MainForm`) and the seven data-bound `Views/*` are built in code
(runtime-dynamic layout) and are edited as code, not in the designer.

Each view calls the matching GMS.Core service and shows `Result` failures as dialogs; the
sidebar hides sections the signed-in role cannot use.

## Running the web client

```bash
dotnet run --project GMS.Web
```

Browse to the URL Kestrel prints, sign in with **`admin` / `ChangeMe#2026`** (forced password
change on first sign-in). Connects to Supabase Postgres when `ConnectionStrings:Gms` is
configured (see **Database** below); otherwise falls back to an in-memory store seeded with
demo data. Demo data is never injected into a real database unless `Seed:Demo=true` is set.

### GMS.Web structure

```
Program.cs           Razor Pages + AddGmsCore + cookie auth + permission policy; seeds on start-up
Auth/
  WebCurrentUser.cs      ICurrentUser from cookie claims (system principal when no HttpContext)
  PermissionPolicy.cs    [Authorize("perm:<code>")] policy provider + handler;
                         MustChangePasswordMiddleware forces the password-change page
Pages/
  Account/  Login, ChangePassword, Logout
  Index (dashboard), Products/, Categories/, Customers/, Suppliers/, Transactions/,
  Inventory/, Reports/, Users/, Notifications/, Settings/, Audit/
  Shared/_Layout.cshtml  responsive Bootstrap nav, permission-filtered links, unread-alerts badge
```

Every folder requires authentication (`AuthorizeFolder("/")`); pages add
`[Authorize("perm:<code>")]` for finer control, and services enforce permissions again server-side.

## Current limitations

- No UI to create custom roles or edit a role's permission set — `RoleService.Create`/
  `SetPermissions` exist and both UIs show roles read-only (name, description, permission
  count, user count). The 3 seeded roles (Admin/Manager/Staff) cover typical use; add a
  permission-checkbox editor if a custom role is ever needed.
- Report export is CSV only (`ReportService.ToCsv`); PDF/Excel rendering is a front-end concern.
- No audit `SaveChanges` interceptor (services call `AuditService.Record` explicitly) and no
  optimistic-concurrency handling.
- Transaction numbers are assigned by counting existing rows per year (`TransactionService.NextNumber`)
  rather than a DB sequence — fine at small-business volume, but not collision-proof under heavy
  concurrent writes.
