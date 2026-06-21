# Deploy — Containers & CI/CD

This directory contains all containerization and deployment configuration for
MAI Health Coach.

## Stack

| Concern              | Technology                                   |
|----------------------|----------------------------------------------|
| Container runtime    | Podman                                       |
| Container definition | `Containerfile` (OCI-compatible, not Dockerfile) |
| Local orchestration  | Podman Compose (`compose.local.yaml`)        |
| Production           | Podman pods + systemd unit files             |
| CI/CD                | GitHub Actions (workflows in `.github/workflows/`) |

## Planned Structure

```
deploy/
├── backend/
│   └── Containerfile       # Backend API image
├── web/
│   └── Containerfile       # Web front-end image (nginx)
├── compose.local.yaml      # Full local stack (api + postgres + web)
├── compose.ci.yaml         # Minimal stack for CI integration tests
└── systemd/                # Prod: quadlet/systemd unit files
```

## Secrets Management

- Local: `.env` files (gitignored) passed to Podman Compose
- CI: GitHub Actions secrets
- Production: systemd `EnvironmentFile` or a secrets manager (TBD)

Never commit secrets. All `.env` files are gitignored at the root.

## Coming in Later Tickets

- M1: `Containerfile` for backend + Postgres; `compose.local.yaml` for walking skeleton
- M2: Web front-end `Containerfile`
- M3: GitHub Actions CI workflow (build + test)
- M4: Production systemd/quadlet units

## Local Run

See the root [README.md](../README.md#full-local-stack-with-podman-compose).
