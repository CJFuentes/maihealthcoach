# MAI Health Coach — Production Deployment Runbook

> **Target:** a Linux host with **systemd** and **Podman 4.4+** (Quadlet support;
> verified against Podman 5.7.1). Covers the backend **API + PostgreSQL 16** only —
> the web front-end (nginx) is a separate ticket.
>
> **Companion artifacts:** the production Quadlet units and env template live in
> [`deploy/prod/`](../deploy/prod/README.md). The image is built from
> [`deploy/backend/Containerfile`](../deploy/backend/Containerfile). This runbook
> is the production counterpart to the local [`deploy/compose.yml`](../deploy/compose.yml).

## Contents

1. [Prerequisites](#1-prerequisites)
2. [Build & publish the image](#2-build--publish-the-image)
3. [Secrets & environment configuration](#3-secrets--environment-configuration)
4. [Database migrations on deploy](#4-database-migrations-on-deploy)
5. [Install & start the stack (Quadlet + systemd)](#5-install--start-the-stack-quadlet--systemd)
6. [Verify health](#6-verify-health)
7. [Observability & metrics](#7-observability--metrics)
8. [Logs](#8-logs)
9. [Rollback](#9-rollback)
10. [Routine ops (restart / update / backup / restore)](#10-routine-ops)
11. [Acceptance-criteria mapping](#11-acceptance-criteria-mapping)
12. [Reverse proxy, TLS & operational notes](#12-reverse-proxy-tls--operational-notes)

---

## 1. Prerequisites

**Production host**

- Linux with systemd ≥ 252 (Quadlet is bundled with Podman; older systemd works but
  ≥ 252 is recommended for stable Quadlet behaviour).
- Podman ≥ 4.4 — verify: `podman --version`.
- `curl` on the host (manual health checks).
- `postgresql-client` (`pg_dump` / `pg_restore`) for backups, if running them from the host.
- **Rootless is recommended.** All commands below assume rootless Podman
  (`systemctl --user …`, unit dir `~/.config/containers/systemd/`). For rootful, drop
  `--user`, use `sudo`, and the unit dir `/etc/containers/systemd/`.

**Build host** (can be the CI runner or any workstation; does not have to be the prod host)

- .NET 10 SDK — only needed to build the image and/or run EF Core migrations.
- Podman or Docker to build the OCI image.

---

## 2. Build & publish the image

The `Containerfile` is multi-stage (`sdk:10.0` build → `aspnet:10.0` runtime, non-root
`app` user). **The build context must be the repo root** so the `COPY backend/src/...`
paths resolve.

```bash
# From the repo root:
podman build -f deploy/backend/Containerfile -t maihealthcoach-api:1.0.0 .

# Tag for your registry (example: GitHub Container Registry)
podman tag maihealthcoach-api:1.0.0 ghcr.io/CJFuentes/maihealthcoach-api:1.0.0

# Authenticate & push
podman login ghcr.io
podman push ghcr.io/CJFuentes/maihealthcoach-api:1.0.0
```

**Tagging policy:** always deploy an **immutable, explicit tag** (e.g. `1.0.0`) or a
digest — never `:latest`. The `Image=` line in `deploy/prod/api.container` must name the
exact tag so rollbacks (s9) are deterministic.

**Pull on the prod host:**

```bash
podman login ghcr.io                       # private registry only
podman pull ghcr.io/CJFuentes/maihealthcoach-api:1.0.0
```

---

## 3. Secrets & environment configuration

The API reads configuration from environment variables using the ASP.NET Core `__`
(double-underscore) convention, which maps to nested config keys
(`Clerk__Authority` → `Clerk:Authority`).

### Secret strategy (one mechanism — Podman secrets)

Two things are secret and are injected via **Podman secrets** (never written to a file):

| Secret name | Injected as env | Used by |
|-------------|-----------------|---------|
| `maihc-connstr` | `ConnectionStrings__Postgres` (full string incl. password) | API |
| `maihc-ai-api-key` | `Ai__ApiKey` | API |
| `maihc-postgres-password` | `POSTGRES_PASSWORD` | Postgres container |

```bash
# Full connection string as ONE secret -> the DB password never lands in any file.
# Host MUST be the Postgres container name (the API reaches it over the Podman network).
echo -n 'Host=maihealthcoach-postgres;Port=5432;Database=maihealthcoach;Username=maihc;Password=STRONG_DB_PASSWORD' \
  | podman secret create maihc-connstr -

echo -n 'STRONG_DB_PASSWORD'        | podman secret create maihc-postgres-password -
echo -n 'sk-ant-REAL-ANTHROPIC-KEY' | podman secret create maihc-ai-api-key -

podman secret ls
```

### Non-secret env file

Everything non-secret goes in an **absolute** env file referenced by
`api.container` (`EnvironmentFile=/etc/maihealthcoach/prod.env`). Do not rely on
home-relative paths.

```bash
sudo install -m 600 -D deploy/prod/.env.example /etc/maihealthcoach/prod.env
sudo $EDITOR /etc/maihealthcoach/prod.env
```

### Full variable reference

**Required (API throws on startup if absent)**

| Variable | Source | Notes |
|----------|--------|-------|
| `ConnectionStrings__Postgres` | `maihc-connstr` secret | `Host=maihealthcoach-postgres;Port=5432;Database=maihealthcoach;Username=maihc;Password=…` |

**Set directly in `api.container` (not the env file)**

| Variable | Value | Why |
|----------|-------|-----|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Disables the dev-only auto-migrate path; enables `UseHttpsRedirection` (see s12); compact JSON logs |
| `ASPNETCORE_HTTP_PORTS` | `8080` | In-container listen port (high port → safe with dropped caps) |
| `Database__AutoMigrate` | `false` | Auto-migrate is dev-only; prod migrates out-of-band (s4) |

**Postgres container** — `POSTGRES_USER=maihc`, `POSTGRES_DB=maihealthcoach` (in
`postgres.container`), `POSTGRES_PASSWORD` from the `maihc-postgres-password` secret.

**Clerk (public metadata, non-secret)** — `Clerk__Authority`, `Clerk__Issuer`,
`Clerk__JwksUrl`, `Clerk__Audience`.

**Claude / Anthropic** — `Ai__ApiKey` (**secret**, via `maihc-ai-api-key`),
`Ai__DefaultModel=claude-sonnet-4-6`, `Ai__EscalationModel=claude-opus-4-8`,
`Ai__BaseUrl=https://api.anthropic.com`, `Ai__TimeoutSeconds=100`.

**Open Food Facts (non-secret)** — `OpenFoodFacts__BaseUrl=https://world.openfoodfacts.org`,
`OpenFoodFacts__UserAgent`, `OpenFoodFacts__TimeoutSeconds`, `OpenFoodFacts__CacheTtlDays`,
`OpenFoodFacts__SearchPageSize`.

**OpenTelemetry (non-secret)** — `OTEL_EXPORTER_OTLP_ENDPOINT` (native OTel var) **or**
`Telemetry__Otlp__Endpoint` (absolute `http(s)://` collector URL; empty = OTLP off);
`Telemetry__Prometheus__Enabled=true` exposes `/metrics`.

**Rate limiting (non-secret)** — `RateLimiting__Enabled`, `RateLimiting__GlobalPermitLimit`,
`RateLimiting__GlobalWindowSeconds`; coach-LLM per-user limiter via `CoachChat__PermitLimit`,
`CoachChat__WindowSeconds`, `CoachChat__MaxMessageLength`, `CoachChat__HistoryTurnLimit`.

See [`deploy/prod/.env.example`](../deploy/prod/.env.example) for the full template with
defaults.

---

## 4. Database migrations on deploy

**The API does NOT auto-migrate in production.** `Program.cs` guards migration with
`app.Environment.IsDevelopment() && Database:AutoMigrate`. In Production `IsDevelopment()`
is false and **short-circuits the whole condition** — auto-migration is off regardless of
`Database__AutoMigrate`. (`Database__AutoMigrate=false` is set in `api.container` as
belt-and-suspenders; the environment check alone already blocks it.) Migrations are
therefore applied **out-of-band, before** starting or updating the API. Always
[back up](#backup) first.

EF facts: DbContext `AppDbContext` (in `Infrastructure`), startup project = the `Api`
project, provider `Npgsql.EntityFrameworkCore.PostgreSQL`, migrations in
`backend/src/Infrastructure/Migrations/`.

### Option A — `dotnet ef database update` (simplest)

Run from the build host (.NET 10 SDK) with network access to the prod DB (SSH tunnel/VPN).
Point the connection string at the prod database — and at the host/port you can actually
reach it on (e.g. `Host=127.0.0.1;Port=5432` through a tunnel):

```bash
cd backend
export ConnectionStrings__Postgres='Host=127.0.0.1;Port=5432;Database=maihealthcoach;Username=maihc;Password=STRONG_DB_PASSWORD'

# Preview pending migrations
dotnet ef migrations list \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --context AppDbContext

# Apply
dotnet ef database update \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --context AppDbContext
```

### Option B — EF migration bundle (no SDK on the prod host)

Build a self-contained bundle on the build host, copy it over, run it:

```bash
cd backend
dotnet ef migrations bundle \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --context AppDbContext \
  --runtime linux-x64 --self-contained \
  --output efbundle

scp efbundle prod-host:/opt/maihealthcoach/efbundle
ssh prod-host
export ConnectionStrings__Postgres='Host=127.0.0.1;Port=5432;Database=maihealthcoach;Username=maihc;Password=STRONG_DB_PASSWORD'
/opt/maihealthcoach/efbundle
```

> **Why no `migrate.container`?** A Quadlet oneshot migration unit would need the EF bundle
> baked into the runtime image; the current `Containerfile` does not ship it. Until it does,
> use Option A/B as a manual deploy step. Do **not** add a non-functional `migrate.container`
> to `deploy/prod/`.

### Per-deploy migration sequence

1. [Back up the database](#backup).
2. Apply migrations (Option A or B).
3. Start / restart the API.
4. Verify [`/healthz/ready`](#6-verify-health).

---

## 5. Install & start the stack (Quadlet + systemd)

Copy the unit files into the Quadlet search directory and reload systemd so it generates
the backing `.service` units.

```bash
# Rootless (recommended)
mkdir -p ~/.config/containers/systemd/
cp deploy/prod/maihealthcoach.network \
   deploy/prod/maihealthcoach-pgdata.volume \
   deploy/prod/postgres.container \
   deploy/prod/api.container \
   ~/.config/containers/systemd/
systemctl --user daemon-reload

# Rootful: use /etc/containers/systemd/ and `sudo systemctl daemon-reload`
```

Generated service units: `maihealthcoach-network.service`,
`maihealthcoach-pgdata-volume.service`, `maihealthcoach-postgres.service`,
`maihealthcoach-api.service`. The network/volume dependencies are auto-wired by Quadlet
from the `Network=`/`Volume=` keys; the only hand-written ordering is api→postgres.

**Apply migrations now** (s4) — before the first API start.

**Start & enable (survive reboot):**

```bash
loginctl enable-linger "$USER"     # rootless: required so user services run without a login session
systemctl --user start maihealthcoach-api.service     # pulls in postgres via ordering
systemctl --user enable maihealthcoach-postgres.service maihealthcoach-api.service
```

> `loginctl enable-linger` is **load-bearing for reboot survival** under rootless Podman —
> without it the services stop when the session ends. Rootful uses `WantedBy=default.target`
> + `systemctl enable` and needs no linger.

**Validate unit syntax on the prod host** (the Windows dev host cannot — see s12):

```bash
systemd-analyze --user verify ~/.config/containers/systemd/api.container 2>&1 || true
podman ps --format '{{.Names}}\t{{.Status}}'
```

---

## 6. Verify health

The API exposes three endpoints (`Program.cs`):

| Endpoint | Meaning |
|----------|---------|
| `/healthz` | Liveness (excludes DB-tagged checks) |
| `/healthz/live` | Liveness (same set as `/healthz`) |
| `/healthz/ready` | Readiness — **includes the Postgres connectivity check** |

```bash
# API is published on host loopback 127.0.0.1:8081 -> container :8080
curl -f http://localhost:8081/healthz        # 200 Healthy = process is up
curl -f http://localhost:8081/healthz/ready   # 200 Healthy = DB reachable too (503 if not)

# Container health (from the Quadlet HealthCmd)
podman ps --format '{{.Names}}\t{{.Status}}'
#   maihealthcoach-postgres  Up X (healthy)
#   maihealthcoach-api       Up X (healthy)
```

---

## 7. Observability & metrics

**Prometheus** — set `Telemetry__Prometheus__Enabled=true`, restart the API, then scrape
`/metrics` (exempt from rate limiting):

```bash
curl http://localhost:8081/metrics | head
```

```yaml
# prometheus.yml
scrape_configs:
  - job_name: maihealthcoach-api
    metrics_path: /metrics
    static_configs:
      - targets: ['127.0.0.1:8081']
```

**OTLP** — set `OTEL_EXPORTER_OTLP_ENDPOINT` (or `Telemetry__Otlp__Endpoint`) to an
absolute `http(s)://` collector URL (e.g. `http://otel-collector:4318`). If empty/invalid,
OTLP export is silently disabled and the app boots normally. The `/metrics` and `/healthz*`
paths are excluded from self-tracing.

---

## 8. Logs

The API logs structured JSON to stdout, captured by journald.

```bash
# Rootless
journalctl --user -u maihealthcoach-api.service -f          # follow
journalctl --user -u maihealthcoach-api.service -n 200       # last 200 lines
journalctl --user -u maihealthcoach-postgres.service -n 100

# Direct container output (either mode)
podman logs -f maihealthcoach-api
podman logs -f maihealthcoach-postgres

# Rootful: drop --user
```

---

## 9. Rollback

### API rollback (no schema change)

```bash
# Pin the previous immutable tag in the unit, reload, restart
sed -i 's#maihealthcoach-api:1.0.1#maihealthcoach-api:1.0.0#' \
  ~/.config/containers/systemd/api.container
systemctl --user daemon-reload
systemctl --user restart maihealthcoach-api.service
curl -f http://localhost:8081/healthz/ready
```

### Migration rollback (DESTRUCTIVE — down-migrations can drop data)

Always [back up](#backup) first, then revert to a known-good migration on the build host:

```bash
cd backend
export ConnectionStrings__Postgres='Host=127.0.0.1;Port=5432;Database=maihealthcoach;Username=maihc;Password=STRONG_DB_PASSWORD'
dotnet ef database update 20260625073647_AddExerciseCatalog \
  --project src/Infrastructure/MAIHealthCoach.Infrastructure.csproj \
  --startup-project src/Api/MAIHealthCoach.Api.csproj \
  --context AppDbContext
systemctl --user restart maihealthcoach-api.service
```

Migration order (latest last): `InitialCreate` → `AddUserEntity` →
`AddUserProfileAndWeightMeasurements` → `AddUserGoalTargets` → `AddFoodDomain` →
`AddFoodDiary` → `AddCustomFoodsFavoritesAndRecents` → `AddCoachConversations` →
`AddWaterLog` → `AddExerciseCatalog` → `AddExerciseLog`. Use
`dotnet ef migrations list` to confirm the applied set before reverting.

> Prefer a **forward-fix** migration over a down-migration whenever data loss is possible.
> When in doubt, restore from a backup (s10) instead of running `down`.

---

## 10. Routine ops

### Restart

```bash
systemctl --user restart maihealthcoach-api.service
systemctl --user restart maihealthcoach-postgres.service
```

### Update to a new image

```bash
podman pull ghcr.io/CJFuentes/maihealthcoach-api:1.1.0      # 1. pull
# 2. back up (below)
# 3. apply migrations (s4)
sed -i 's#maihealthcoach-api:1.0.0#maihealthcoach-api:1.1.0#' \
  ~/.config/containers/systemd/api.container                  # 4. pin new tag
systemctl --user daemon-reload                                # 5. reload + restart
systemctl --user restart maihealthcoach-api.service
curl -f http://localhost:8081/healthz/ready                   # 6. verify
```

<a id="backup"></a>
### Backup PostgreSQL

```bash
mkdir -p /opt/maihealthcoach/backups
podman exec maihealthcoach-postgres \
  pg_dump -U maihc -Fc maihealthcoach \
  > /opt/maihealthcoach/backups/maihealthcoach-$(date +%Y%m%d%H%M%S).dump
```

### Restore PostgreSQL

```bash
systemctl --user stop maihealthcoach-api.service             # stop the API first
podman exec maihealthcoach-postgres psql -U maihc -d postgres \
  -c "DROP DATABASE maihealthcoach;" -c "CREATE DATABASE maihealthcoach;"
podman exec -i maihealthcoach-postgres \
  pg_restore -U maihc -d maihealthcoach \
  < /opt/maihealthcoach/backups/maihealthcoach-<timestamp>.dump
systemctl --user start maihealthcoach-api.service
curl -f http://localhost:8081/healthz/ready
```

The data volume (`maihealthcoach-pgdata`) persists independently of the containers; inspect
it with `podman volume ls` / `podman volume inspect`.

---

## 11. Acceptance-criteria mapping

| Acceptance criterion | Where it's satisfied |
|----------------------|----------------------|
| API + Postgres run as managed Podman services that survive reboot | Quadlet units in `deploy/prod/` + `[Install] WantedBy=default.target` + `systemctl enable` + `loginctl enable-linger` (s5) |
| DB migrations run safely on deploy; backup/restore documented | s4 (out-of-band migrations) + s10 (pg_dump / pg_restore) |
| Secrets injection covered | s3 — Podman secrets for connstr + Ai key; non-secret env file |
| TLS / reverse proxy covered | s12 |
| Rollback covered | s9 (image tag + migration revert) |

---

## 12. Reverse proxy, TLS & operational notes

**TLS terminates at a reverse proxy.** The API container publishes **only** on
`127.0.0.1:8081`; never expose port 8080/8081 to the internet directly. Put nginx / Caddy /
Traefik in front to terminate TLS and forward HTTP to `127.0.0.1:8081`.

**⚠ HTTPS redirect-loop hazard.** In Production `Program.cs` runs `app.UseHttpsRedirection()`,
and the app currently registers **no** `ForwardedHeaders` middleware. So Kestrel never learns
the original request was HTTPS and will 307-redirect proxied plaintext requests — a loop — if
the proxy and app are misconfigured. Mitigations, in order of preference:

1. Keep the loopback-only `PublishPort` and have the proxy terminate TLS and forward to
   `127.0.0.1:8081`. Because the proxy is the only client and serves the public HTTPS URL,
   browsers see HTTPS end-to-end; the in-container redirect only fires on the rare direct-HTTP
   hit, which is loopback-only and harmless.
2. Configure the proxy to send `X-Forwarded-Proto: https` **and** (future app change, out of
   scope for this ticket) add `app.UseForwardedHeaders()` so `UseHttpsRedirection` becomes a
   no-op for already-secure requests. Track this as a follow-up backend issue.

Example nginx server block:

```nginx
server {
    listen 443 ssl;
    server_name api.maihealthcoach.example.com;
    ssl_certificate     /etc/ssl/certs/maihealthcoach.crt;
    ssl_certificate_key /etc/ssl/private/maihealthcoach.key;
    location / {
        proxy_pass http://127.0.0.1:8081;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto https;
    }
}
server { listen 80; server_name api.maihealthcoach.example.com; return 301 https://$host$request_uri; }
```

**Other notes**

- **Capabilities.** `api.container` sets `DropCapability=all` + `NoNewPrivileges=true`. The
  API binds high port 8080 as the non-root `app` user (uid 1654), so no `NET_BIND_SERVICE`
  is required. If the in-container port is ever changed to < 1024 this must be revisited.
- **Unit validation on Windows.** Quadlet/systemd unit syntax cannot be validated on a
  Windows dev host (no `systemd-analyze`/Quadlet generator). Validate on the Linux prod host
  after `daemon-reload` with `systemd-analyze --user verify`.
- **Postgres has no published port** by default — the API reaches it over the Podman network
  by name. Add `PublishPort=127.0.0.1:5432:5432` to `postgres.container` only for temporary
  host-side `psql` debugging.
