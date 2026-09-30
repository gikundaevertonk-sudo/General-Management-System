# General Management System (GMS)

A general-purpose management system for **small businesses** — records, users, inventory,
transactions and reports — with a native VB.NET desktop client and a browser-accessible
web client sharing one business-logic library.

## Solution layout

| Project | Language | Type | Purpose |
|---|---|---|---|
| `GMS.Core` | VB.NET | Class library (`net10.0`) | All models, business rules, services. Every front end depends on this. |
| `GMS.Desktop` | VB.NET | Windows Forms (`net10.0-windows`) | Native desktop client for a tenant. References `GMS.Core`. |
| `GMS.Web` | C# | ASP.NET Core Razor Pages (`net10.0`) | Browser client for a tenant (ASP.NET Core has no VB template). References `GMS.Core`. |
| `GMS.Operator` | C# | ASP.NET Core Razor Pages (`net10.0`) | The system owner's console: create, edit, suspend and delete organisations, set subscriptions and user limits, reset any tenant user's password. **A separate application on purpose** — see [`GMS.Operator/README.md`](GMS.Operator/README.md). |
| `GMS.Tests` | C# | xUnit (`net10.0`) | Business rules in `GMS.Core`: who may call what, trial and subscription arithmetic, stock movement, user limits. |

`GMS.Web` and `GMS.Desktop` are what a customer gets. `GMS.Operator` is what you get, and it is
deployed on its own host with its own credential — the console can read across every tenant and
delete an organisation, so it is not a route inside the product tenants use.

Open `GeneralManagementSystem.slnx` in Visual Studio 2022 (17.10+) or build from the CLI.

```bash
dotnet build GeneralManagementSystem.slnx
dotnet test GMS.Tests/GMS.Tests.csproj
```

The tests run against the in-memory store, so read them narrowly. It stamps the current
organisation on new rows exactly as `GmsDbContext` does, so writes land in the right tenant and
counts per organisation are honest — but it does **not** filter reads or enforce foreign keys. A
plain `Query()` there returns every tenant's rows, so nothing in the suite can prove one tenant's
data is hidden from another. That has to be exercised against PostgreSQL.

## GMS.Core structure

```
Models/          Domain entities (EntityBase / AuditableEntity + 17 entities)
Enums/           TransactionType, TransactionStatus, StockMovement*, Notification*, AuditAction
Common/          Result / Result(Of T), PagedResult(Of T), QueryOptions, Guard, ServiceException
Abstractions/    IRepository(Of T), IUnitOfWork, ICurrentUser, IPasswordHasher, IClock
Contracts/       DTOs returned to the UI (AuthenticatedUser, DashboardSummary, report rows)
Security/        Pbkdf2PasswordHasher, PermissionCodes catalogue, SystemClock, system/anon principals
Data/            GmsDbContext (EF Core, maps to the existing Postgres schema, no migrations)
Repositories/    InMemory/ (desktop/demo fallback) and Ef/ (PostgreSQL, backs GMS.Web)
Services/        AuthService, UserService, RoleService, CategoryService, ProductService,
                 CustomerService, SupplierService, TransactionService, InventoryService,
                 ShopService, NotificationService, ReportService, DashboardService,
                 SettingsService, AuditService, DataSeeder, OrganizationService,
                 SubscriptionService
DependencyInjection/  AddGmsCore() (in-memory), AddGmsCorePostgres(), AddGmsCoreForTooling()
```

### Design decisions

- **Domain:** general small-business. Main entity is `Product` (inventory); `Customer` and
  `Supplier` are first-class. One unified `Transaction` document (`Type` = Sale / Purchase /
  AdjustmentIn / AdjustmentOut) with `TransactionLine` rows.
- **Stock is a ledger.** `StockMovement` is the immutable source of truth; `Product.QuantityOnHand`
  is a cache maintained **only** by `InventoryService`. `Reconcile` rebuilds the cache from the ledger.
- **Stock lives at a location.** An organisation can run many `Shop`s from one account. A movement
  with no shop belongs to the *central pool*; one with a shop belongs to that branch, and
  `ShopStock` caches the per-shop balance. Central is never stored — it is
  `Product.QuantityOnHand` minus everything the shops hold, so the totals cannot drift apart.
  An organisation with no shops behaves exactly as it did before shops existed. **Availability is
  always checked at the location stock is leaving, never company-wide:** a branch that is short
  must fail even when another branch has plenty.
- **Allocation moves stock without creating or destroying it.** `InventoryService.Allocate`
  writes a paired Out/In at two locations (`StockMovementReason.Allocation`), leaving
  `Product.QuantityOnHand` untouched. An *adjustment* is the opposite: breakage really is lost,
  so it moves both the location balance and the organisation total.
- **Transactions are immutable once `Confirmed`.** Confirm posts stock at the transaction's own
  `ShopId`; Cancel writes compensating movements there. Multi-line sales are all-or-nothing on
  stock availability, totalled per product so two lines of the same item cannot overdraw together.
- **RBAC:** one `Role` per user, role → many permissions (string codes in `PermissionCodes`).
  Seeded roles: Admin, Manager, Staff, Shop Attendant.
- **Shop confinement is a second axis of scope, not a permission.** `User.ShopId` pins an account
  to one shop; it reaches services through `ICurrentUser.ShopId` and is checked *in addition to* a
  permission code, never instead of one. A shop attendant holds ordinary counter permissions —
  Confirm included, because completing the sale in front of the customer is the job — and simply
  holds them at one location: their lists, ledger, dashboard and sales are all their shop's.
  They cannot allocate, so stock reaches a branch because a manager sent it.
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

`DataSeeder.SeedBaseline()` — permissions, the four system roles, one administrator
(`admin` / `ChangeMe#2026`, must change on first sign-in), default `AppSetting`s. Idempotent.
`DataSeeder.SeedDemo(...)` — a few categories/products, one supplier, one customer, an opening-stock
purchase and one sale, so dashboards and reports are not empty.

## Roadmap

1. **GMS.Core** — models + service layer against `IRepository`/`IUnitOfWork`. ✅ *done, builds, smoke-tested*
2. **GMS.Desktop** — Windows Forms UI. ✅ *done, builds, smoke-tested*
3. **GMS.Web** — Razor Pages UI + cookie authentication, responsive. ✅ *done, builds, HTTP-tested*
4. **Persistence** — EF Core + Npgsql against Supabase Postgres. ✅ *done, HTTP- and DB-verified end to end*
5. **GMS.Operator** — the owner's console, split out of `GMS.Web` so the tenant application has no
   operator surface at all. ✅ *done, DB-verified end to end*
6. **GMS.Tests** — xUnit over the service layer. ✅ *done, 82 cases*
7. **Shops** — many trading locations per organisation, per-shop stock, manager allocation and
   shop-attendant sign-in. ✅ *done in Core, GMS.Web and GMS.Desktop*

Not built, and deliberate for now: there is no payment provider. The operator records a
subscription by hand — a plan, a start date, an end date, or no end at all — and a tenant whose
subscription lapses waits for the operator to renew it rather than paying to restore access
themselves. Their administrators are warned two days before it ends.

## Database (Supabase / PostgreSQL)

Schema lives in **`db/supabase/schema.sql`** (run once in the Supabase SQL Editor; idempotent,
safe to re-run) — 18 snake_case tables, `GmsDbContext` maps to them via a runtime snake-case
rename (no EF migrations; the schema is hand-owned). The file creates structure only; the
25 permissions, 4 system roles, the `admin` account and default settings are seeded by the
application itself on start-up (`DataSeeder.SeedBaseline`, idempotent).

**Upgrading an existing database:** re-run the whole file. `CREATE TABLE IF NOT EXISTS` builds a
new database but does nothing to one that already exists, so columns added after first release
live in an *Upgrades* section of ALTER statements further down. They are nullable with no default,
which is what makes them safe against live data — and for `shop_id`, NULL means "central pool",
which is exactly what every row predating shops actually was.

**Offline sync upgrade (required before deploying that build — web included):** run
`db/supabase/migrations/2026-09-30-offline-sync.sql` once. It adds `sync_id` and `sync_version`
to every table (both mapped by `GmsDbContext`, so the apps fail without them), triggers that
stamp them, and a `sync_tombstones` table that records deletions. See **Working offline** below.

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
                      TransactionEditForm, UserEditForm/SetPasswordForm, StockAdjustForm,
                      ShopEditForm, StockAllocateForm
Views/                ViewBase (page chrome) + DashboardView, ProductsView, CategoriesView,
                      CustomersView, SuppliersView, TransactionsView, InventoryView, ShopsView,
                      ReportsView, UsersView, NotificationsView, SettingsView, AuditView
```

`ShopsView` is the desktop's central point for multiple locations: every location down the top
grid (central first, then each shop) and what the selected one holds down the bottom, with
**Allocate stock…** sending it somewhere else.

The **dialog forms** (`LoginForm`, `ChangePasswordForm`, `ProductEditForm`, `UserEditForm`,
`SetPasswordForm`, `StockAdjustForm`, `TransactionEditForm`, `ShopEditForm`,
`StockAllocateForm`) use the standard `*.vb` +
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
  Inventory/, Shops/, Reports/, Users/, Notifications/, Settings/, Audit/
  Shared/_Layout.cshtml  responsive Bootstrap nav, permission-filtered links, unread-alerts badge
```

`Shops/Index` is the central point — every shop with its staff count, items, units and stock
value. `Shops/Stock` opens one location (no `shopId` is the central pool) and carries the
allocation form. The sidebar names the signed-in user's shop when they are pinned to one, because
everything they see is silently narrowed to it and an empty list should not read as missing data.

Every folder requires authentication (`AuthorizeFolder("/")`); pages add
`[Authorize("perm:<code>")]` for finer control, and services enforce permissions again server-side.

## Working offline (desktop)

With a database configured, **GMS.Desktop works against a SQLite copy on the PC** and keeps it in
step with PostgreSQL in the background (`GMS.Core/Sync`). When the internet drops, the shop keeps
selling; changes queue up and are sent when the connection returns. GMS.Web stays online-only.

- **Where:** `%LOCALAPPDATA%\GMS\gms-local-<server>.db`, one file per server (its ids belong to
  that server). `LocalStore:Path` / `LocalStore__Path` moves it.
- **Sign-in:** online when the server answers (and the organization is downloaded before the first
  screen opens); otherwise against the local copy. A computer's **first** sign-in needs internet.
- **Sync:** every 30 s and on demand. The sidebar shows *Online / Offline – N changes waiting /
  Not sent*; click it to sync now or see what was refused.
- **Ids:** rows created offline take negative local ids that never change; the server's id is
  recorded beside them (`IdMapEntry`) and references are translated both ways. `SyncId` (a uuid on
  every row) makes a resend after a dropped connection idempotent.
- **Receipt numbers:** each install numbers in its own series, e.g. `SAL-2026-K7Q2-0001`, so two
  offline tills can never issue the same number. The web keeps `SAL-2026-0001`.
- **Conflicts:** an edit sends only the fields it changed, so edits to different fields of one
  record both survive; the same field is last-write-wins. A deletion on the server wins.
- **Stock:** totals are never sent; stock movements are, and the server adds their effect to its
  own totals. If two tills sold the same last units offline, **both sales are kept**, stock goes
  below zero, and a *Critical* "Stock below zero after offline sales" notification is raised.
- **Not synced from the desktop:** organizations, subscriptions, roles and permissions (copied
  down, never sent).
- **Upgrades:** the local file is rebuilt when a new version changes the data model. If it still
  holds unsent changes it is kept aside as `*.bak` and the user is told; sync before upgrading.

**Testing sync:** `GMS.Tests/SyncTests.cs` and `PostgresQueryTests.cs` run only when
`GMS_TEST_POSTGRES` names a disposable PostgreSQL server (each test creates and drops its own
database — never point it at Supabase). `GMS_TEST_STORE=sqlite` runs the whole suite on the local
store instead of the in-memory one.

## Current limitations

- The local copy on each desktop is not encrypted: it holds the organization's data, including
  users' password hashes, readable by anyone who can read that Windows profile.
- A pull re-reads a margin of recent changes to catch slow transactions; one that stays open
  longer than ~100 other writes can still be missed until that row changes again.

- No UI to create custom roles or edit a role's permission set — `RoleService.Create`/
  `SetPermissions` exist and both UIs show roles read-only (name, description, permission
  count, user count). The 4 seeded roles (Admin/Manager/Staff/Shop Attendant) cover typical use;
  add a permission-checkbox editor if a custom role is ever needed.
- A user belongs to **one** shop or to all of them; there is no "these three branches". A regional
  manager is modelled as unpinned (whole organisation) today.
- Low-stock notifications are raised against the organisation's total, not per shop. A branch
  running out while another is overstocked shows on the Shops screen (its *Low* count) but does
  not raise an alert.
- Audit rows record who changed what, not where, so a shop attendant's dashboard shows no recent
  activity at all rather than the whole organisation's.
- Report export is CSV only (`ReportService.ToCsv`); PDF/Excel rendering is a front-end concern.
- No audit `SaveChanges` interceptor (services call `AuditService.Record` explicitly) and no
  optimistic-concurrency handling.
- Transaction numbers are assigned by counting existing rows per year (`TransactionService.NextNumber`)
  rather than a DB sequence — fine at small-business volume, but not collision-proof under heavy
  concurrent writes.
