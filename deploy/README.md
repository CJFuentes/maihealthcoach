# Deploy — Containers & CI/CD

This directory contains all containerization and deployment configuration for
MAI Health Coach.

## Stack

| Concern              | Technology                                   |
|----------------------|----------------------------------------------|
| Container runtime    | Podman (Docker also works)                   |
| Container definition | `Containerfile` (OCI-compatible, not Dockerfile) |
| Local orchestration  | Podman Compose / Docker Compose (`compose.yml`) |
| Production           | Podman pods + systemd unit files             |
| CI/CD                | GitHub Actions (workflows in `.github/workflows/`) |

## Structure

```
deploy/
├── backend/
│   ├── Containerfile       # Backend API image (multi-stage: sdk:10.0 → aspnet:10.0)
│   └── (.containerignore / .dockerignore live at the repo root — see below)
├── web/
│   └── Containerfile       # Web front-end image (nginx) — later ticket
├── compose.yml             # Full local stack (api + postgres)
├── compose.ci.yaml         # Minimal stack for CI integration tests — later ticket
├── .env.example            # Template for local env vars (copy to .env)
└── systemd/                # Prod: quadlet/systemd unit files — later ticket
```

> The container-ignore files (`.containerignore` for Podman, `.dockerignore` for
> Docker) live at the **repo root**, not in `deploy/`, because the build context
> is the repo root (so the `Containerfile` can `COPY backend/src/...`). Ignore
> files are read from the context root.

## Quick Start (Local)

```bash
cd deploy
cp .env.example .env          # first time only; defaults are fine for local dev
podman compose up             # or: docker compose up
```

- API: <http://localhost:8080>
- Health check: <http://localhost:8080/healthz>
- PostgreSQL on the host (for psql/debugging): `localhost:5432`

On Windows, ensure the Podman machine is running first: `podman machine start`.

## Build the API image manually

The `Containerfile` lives at `deploy/backend/Containerfile` but needs source from
the repo root (`backend/src/`). Build from the **repo root** so the context is
correct:

```bash
# from the repo root:
podman build -f deploy/backend/Containerfile -t maihealthcoach-api:local .
# or:
docker build -f deploy/backend/Containerfile -t maihealthcoach-api:local .
```

The image is multi-stage (SDK build → slim ASP.NET runtime) and runs as the
non-root `app` user shipped by the .NET base image.

## Secrets Management

- Local: `.env` file (gitignored) next to `compose.yml`, loaded automatically by Compose
- CI: GitHub Actions secrets
- Production: systemd `EnvironmentFile` or a secrets manager (TBD)

Never commit secrets. `.env` is gitignored; `.env.example` is committed and
documents every required variable with safe local-dev defaults.

## Coming in Later Tickets

- F5: GitHub Actions CI workflow (build + test image)
- Web front-end `Containerfile` (nginx)
- Production systemd/quadlet units

## Local Run

See the root [README.md](../README.md#full-local-stack-with-podman-compose).
