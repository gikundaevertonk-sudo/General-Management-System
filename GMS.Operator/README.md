# GMS.Operator — the system owner's console

Create, rename, suspend, renew and delete organisations; see what each one contains; reset any
tenant user's password when they have locked themselves out.

## Why this is a separate application

It used to be a `/Platform` folder inside `GMS.Web`, and that was wrong. The console can read
across every tenant, reset anyone's password and delete an organisation and all its data. Living
inside the product tenants use meant:

- its sign-in page sat on the same public host every customer browses;
- it shared a process, a DI container, a DataProtection keyring and a database connection with
  the 25 tenant-facing pages, so any bug in one of those pages sat next to operator authority;
- the operator identity had to be told apart from tenant identities by authentication scheme,
  inside a single `ClaimsPrincipal`. That coupling was fragile in practice — it made the
  console's own **Sign out** button return 400 and do nothing for four hours.

Split out, the tenant application has no operator surface at all: every `/Platform` route
returns 404, and `WebCurrentUser.IsPlatformOperator` is a hard `false`, so
`ServiceBase.DeniedPlatform` refuses every cross-tenant call regardless of what a bug in a
tenant page might attempt. Here, there is one cookie scheme and one kind of visitor, so the
whole class of scheme-confusion bug is gone.

What is **not** separated is the data: the console points at the same database, because managing
those tenants is its job.

## Configuration

Nothing sensitive is committed. Both values come from user-secrets or the environment.

```bash
# The database the tenants live in - same connection string as GMS.Web
dotnet user-secrets set "ConnectionStrings:Gms" "<same string as GMS.Web>" --project GMS.Operator

# The operator credential. Not in the database: the console manages the tenants, so its
# credential must not live in the same table as theirs, and it belongs to no organisation.
dotnet user-secrets set "Platform:Operator:UserName" "owner" --project GMS.Operator
dotnet user-secrets set "Platform:Operator:PasswordHash" "<hash>" --project GMS.Operator
```

The hash format is `PBKDF2-SHA256.<iterations>.<saltBase64>.<keyBase64>`; generate one with
`PlatformOperatorOptions.HashFor("<password>")`. Verification reads the iteration count back out
of the stored string, so raising it later does not invalidate existing hashes.

**If either value is missing the console switches itself off** — `/Login` returns 404 and no
sign-in succeeds. A deployment that has not been configured exposes no operator surface rather
than falling back to a default.

## Running it

```bash
dotnet run --project GMS.Operator
```

Use a **real database**. Without a connection string `GMS.Core` falls back to its in-memory
store, and because the console is now its own process it gets its own empty copy of that store —
it cannot see anything `GMS.Web` created, and there is no baseline seed here. Creating an
organisation still works (onboarding seeds its roles and first administrator itself), but you are
looking at a private, throwaway world, not your tenants.

## Deploying it

Deploy it as its own site, not as a path on the tenant application's host. It needs no public
reachability at all — bind it to a private interface, put it behind a VPN, or restrict it by IP.
Nobody but you should be able to reach its login page.

It is also the only component that never ships to a customer: `GMS-Setup.exe` contains
`GMS.Desktop`, and `GMS.Web` is the hosted tenant site. This console stays with you.
