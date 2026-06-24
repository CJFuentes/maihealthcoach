# MAIHealthCoach.Api — Configuration & Secrets

This document is the authoritative **config schema** for the API. It lists every
configuration key, where each value should come from, and how to provide secrets
**without committing them**.

## Configuration sources & precedence

The API uses the standard ASP.NET Core configuration stack. Later sources win:

1. `appsettings.json` — committed, **non-secret defaults / placeholders only**.
2. `appsettings.Development.json` — local, **gitignored** (copy from
   `appsettings.Development.json.example`). Still prefer user-secrets for secrets.
3. **.NET user-secrets** — local-dev secrets, stored outside the repo. The Api
   project has a `UserSecretsId`, so `dotnet user-secrets` works out of the box.
4. **Environment variables** — CI and production. Nested keys use the
   double-underscore convention, e.g. `Ai__ApiKey` → `Ai:ApiKey`.

> No real secrets are ever committed. `appsettings.json` ships empty/dummy values;
> `.env`, `appsettings.Development.json`, and `appsettings.Local.json` are gitignored.
> Only `*.example` files and placeholders are tracked.

## Config schema (every key)

| Key (config path)            | Env var                       | Secret? | Default / placeholder              | Purpose |
|------------------------------|-------------------------------|---------|------------------------------------|---------|
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | **Yes** (password) | placeholder host in appsettings | Postgres connection string |
| `Clerk:Authority`            | `Clerk__Authority`            | No      | `""`                               | Clerk OIDC authority / Frontend API origin |
| `Clerk:Issuer`               | `Clerk__Issuer`               | No      | `""` (falls back to Authority)     | Expected JWT `iss` claim |
| `Clerk:JwksUrl`              | `Clerk__JwksUrl`              | No      | `""` (derived from Authority)      | JWKS endpoint for signing keys |
| `Clerk:Audience`             | `Clerk__Audience`             | No      | `""`                               | Expected JWT `aud` claim (optional) |
| `Ai:ApiKey`                  | `Ai__ApiKey`                  | **Yes — server-side** | `""`                  | Anthropic/Claude API key |
| `Ai:DefaultModel`            | `Ai__DefaultModel`            | No      | `claude-sonnet-4-6`                | Default coaching model |
| `Ai:EscalationModel`         | `Ai__EscalationModel`         | No      | `claude-opus-4-8`                  | Escalation model |
| `Ai:BaseUrl`                 | `Ai__BaseUrl`                 | No      | `https://api.anthropic.com`        | Anthropic API base URL |
| `Ai:TimeoutSeconds`          | `Ai__TimeoutSeconds`          | No      | `100`                              | AI HTTP timeout (seconds) |

The Clerk **secret key** is intentionally **not** part of this schema — Clerk JWT
validation only needs the public authority/JWKS metadata above.

## Providing secrets locally (user-secrets)

```bash
cd backend/src/Api

# Claude API key (server-side secret)
dotnet user-secrets set "Ai:ApiKey" "sk-ant-..."

# Postgres password / full connection string
dotnet user-secrets set "ConnectionStrings:Postgres" "Host=localhost;Port=5432;Database=maihealthcoach_dev;Username=postgres;Password=..."

# Clerk (public, but convenient to set locally)
dotnet user-secrets set "Clerk:Authority" "https://your-instance.clerk.accounts.dev"
```

## Providing config via environment variables (CI / prod)

Use the double-underscore convention. See `deploy/.env.example` for the full list:

```bash
export Ai__ApiKey="sk-ant-..."
export Clerk__Authority="https://your-instance.clerk.accounts.dev"
export ConnectionStrings__Postgres="Host=...;Database=...;Username=...;Password=..."
```

## Important: the app starts without secrets

Clerk auth (#12) and the Claude `CoachService` (#36) are **not wired up yet**.
The options validators only check the **format** of values that are actually
supplied — they do **not** require Clerk/Claude keys to be present, and validation
is **not** run eagerly at startup. So the API builds, starts, and serves `/healthz`
green with **no** Clerk/Claude secrets configured.
