# Backend — ASP.NET Core Web API

This directory contains the server-side application for MAI Health Coach.

## Stack

| Concern           | Technology                          |
|-------------------|-------------------------------------|
| Framework         | ASP.NET Core Web API (.NET 10)      |
| Architecture      | Layered: Api / Application / Domain / Infrastructure |
| Database          | PostgreSQL 16 via Entity Framework Core |
| Auth              | Clerk JWT validation (JWKS endpoint) |
| AI Coach          | Anthropic Claude API (`CoachService`) — `claude-sonnet-4-6` default, `claude-opus-4-8` for escalated requests |
| Food Data         | Open Food Facts via `NutritionLookupService` (cached in Postgres) |
| Testing           | xUnit                               |
| Containerization  | Podman (`Containerfile` in `/deploy`) |

## Database & Migrations

The backend uses **EF Core** with the **Npgsql** provider against **PostgreSQL**.
`AppDbContext` (`src/Infrastructure/Persistence/AppDbContext.cs`) is the single
unit-of-work for the application.

### Connection string configuration

The connection string is named **`Postgres`** and is read from configuration. It is
**never hard-coded** for a real database. Configure it locally using any of these
(highest precedence last); none of these sources are committed:

- **User secrets** (recommended for local dev), run from `src/Api`:
  ```bash
  dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=maihealthcoach_dev;Username=postgres;Password=..."
  ```
- **`appsettings.Development.json`** — copy `appsettings.Development.json.example`
  to `appsettings.Development.json` and fill in the password.
- **Environment variable** `ConnectionStrings__Postgres` (double underscore).

> The committed `appsettings.json` ships a **placeholder**
> `Host=localhost;Database=mai_placeholder`. It is intentionally non-functional: it
> lets DI succeed and integration tests run without a real database (EF Core opens no
> connection until a query executes, and `Database:AutoMigrate` defaults to `false`).
> Override it locally — do not point it at a real database in source control.

### Running migrations

Apply migrations against the configured database:

```bash
dotnet ef database update \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --context AppDbContext
```

The design-time factory (`AppDbContextFactory`) resolves the connection string from the
same configuration the app uses, so the tooling targets the same database. To point it at
a specific database, set the standard ASP.NET Core override **`ConnectionStrings__Postgres`**
(double underscore) — for example:

```bash
ConnectionStrings__Postgres="Host=localhost;Port=5432;Database=maihealthcoach_dev;Username=postgres;Password=…" \
  dotnet ef database update --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj --context AppDbContext
```

Full resolution order: `ConnectionStrings:Postgres` (config / `ConnectionStrings__Postgres`
env var) → `EFCORE_CONNECTION_STRING` (last-resort fallback, only reached when the
`ConnectionStrings:Postgres` key is absent from all config sources — note `appsettings.json`
always supplies a placeholder for it) → a localhost fallback for offline `migrations add`.

### Adding migrations

```bash
dotnet ef migrations add <Name> \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --output-dir Migrations \
  --context AppDbContext
```

Migration files live in `src/Infrastructure/Migrations/` and **are source-controlled**.

### Auto-migrate on startup (dev only)

Set `Database:AutoMigrate: true` in `appsettings.Development.json` to apply pending
migrations automatically when the API starts in the Development environment. The
default is `false`, which is why CI and integration tests never touch a database.
Production applies migrations via `dotnet ef database update` in the deploy pipeline,
not on startup.

### DbContext access pattern (unit-of-work)

Inject `AppDbContext` **directly** as the unit-of-work; each `DbSet<T>` is the
repository for its aggregate, and `SaveChangesAsync` commits the unit-of-work. There is
deliberately **no generic `IRepository<T>` wrapper** — it would add indirection without
value and hide EF Core's querying capabilities.

```csharp
public sealed class ExampleService
{
    private readonly AppDbContext _db;

    public ExampleService(AppDbContext db) => _db = db;

    public async Task<int> CountAsync(CancellationToken ct) =>
        await _db.Set<SomeEntity>().CountAsync(ct);
}
```

## Planned Layer Structure

```
backend/
├── src/
│   ├── Api/               # Controllers, middleware, program entry point
│   ├── Application/       # CQRS commands/queries, service interfaces
│   ├── Domain/            # Entities, value objects, domain rules
│   └── Infrastructure/    # EF Core DbContext, external HTTP clients, repos
├── tests/
│   ├── Api.Tests/
│   ├── Application.Tests/
│   └── Domain.Tests/
└── MAIHealthCoach.sln
```

## Coming in Later Tickets

- M1: Walking skeleton — health-check endpoint, EF Core + Postgres wiring, Clerk JWT middleware
- M2: User profile and daily log domain entities
- M3: `NutritionLookupService` and barcode endpoint
- M4: `CoachService` (Claude API integration)

## Local Run

See the root [README.md](../README.md#backend-net) for prerequisites and setup steps.
