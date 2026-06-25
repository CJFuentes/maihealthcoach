# deploy/prod — Production Quadlet units

[Podman Quadlet](https://docs.podman.io/en/latest/markdown/podman-systemd.unit.5.html)
unit files that run the MAI Health Coach production stack (PostgreSQL 16 + API) as
systemd-managed Podman containers that survive reboot.

> **Full procedure:** see the [Production Deployment Runbook](../../docs/deployment-runbook.md).
> This README is the quick reference; the runbook covers image build/publish,
> migrations, observability, rollback, and backup/restore in detail.

## Files

| File | Type | Purpose |
|------|------|---------|
| `maihealthcoach.network`        | `.network`   | Bridge network shared by the containers (API egress allowed; DB reachable by name) |
| `maihealthcoach-pgdata.volume`  | `.volume`    | Named volume persisting PostgreSQL data |
| `postgres.container`            | `.container` | PostgreSQL 16 service (`maihealthcoach-postgres`) |
| `api.container`                 | `.container` | MAI Health Coach API service (`maihealthcoach-api`) |
| `.env.example`                  | template     | Non-secret prod env vars (copy to an absolute env file) |

There is intentionally **no** `migrate.container`: the runtime image does not embed
an EF migration bundle, so migrations are applied out-of-band before each deploy
(see runbook s4). Do not add a migration unit unless the image ships the bundle.

## Quadlet install paths

| Mode | Unit dir |
|------|----------|
| Rootless (recommended) | `~/.config/containers/systemd/` |
| Rootful | `/etc/containers/systemd/` |

After copying unit files, run `systemctl --user daemon-reload` (rootless) or
`sudo systemctl daemon-reload` (rootful) so Quadlet generates the `.service` units.
Generated service names: `maihealthcoach-network.service`,
`maihealthcoach-pgdata-volume.service`, `maihealthcoach-postgres.service`,
`maihealthcoach-api.service`.

## Quick start (rootless)

```bash
# 1. Create Podman secrets (interactive; never script plaintext)
echo -n 'Host=maihealthcoach-postgres;Port=5432;Database=maihealthcoach;Username=maihc;Password=STRONG_DB_PASSWORD' \
  | podman secret create maihc-connstr -
echo -n 'STRONG_DB_PASSWORD'        | podman secret create maihc-postgres-password -
echo -n 'sk-ant-REAL-ANTHROPIC-KEY' | podman secret create maihc-ai-api-key -

# 2. Non-secret env file (absolute path referenced by api.container)
sudo install -m 600 -D deploy/prod/.env.example /etc/maihealthcoach/prod.env
sudo $EDITOR /etc/maihealthcoach/prod.env

# 3. Pin the image tag in api.container (Image=ghcr.io/CJFuentes/maihealthcoach-api:<tag>)

# 4. Install the units
mkdir -p ~/.config/containers/systemd/
cp deploy/prod/maihealthcoach.network \
   deploy/prod/maihealthcoach-pgdata.volume \
   deploy/prod/postgres.container \
   deploy/prod/api.container \
   ~/.config/containers/systemd/
systemctl --user daemon-reload

# 5. Apply EF Core migrations BEFORE starting the API (runbook s4)

# 6. Start + enable (survive reboot)
loginctl enable-linger "$USER"          # rootless: keep services running without an active login
systemctl --user start maihealthcoach-api.service
systemctl --user enable maihealthcoach-postgres.service maihealthcoach-api.service

# 7. Verify
curl -f http://localhost:8081/healthz/ready
```

## Secrets & .gitignore

- `Ai__ApiKey` and the DB password (inside `ConnectionStrings__Postgres`) are
  injected via **Podman secrets**, never written to any file.
- A real env file lives **outside the repo** at `/etc/maihealthcoach/prod.env`.
- Any `*.env` under `deploy/` is gitignored; only `.env.example` is committed.
  Never commit real credentials.
