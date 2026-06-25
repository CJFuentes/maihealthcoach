# Docs

Canonical documentation for MAI Health Coach — architecture diagrams,
Architecture Decision Records (ADRs), and API documentation.

## Contents

| File / Folder       | Purpose                                               |
|---------------------|-------------------------------------------------------|
| `architecture.md`   | Canonical system architecture diagram (mirrored in root README) |
| `deployment-runbook.md` | Production deployment runbook (Podman Quadlet + systemd) — companion to `deploy/prod/` |
| `coaching-safety-guardrails.md` | AI coaching safety policy: prohibited topics, disclaimers, risk categories, red-team prompts |
| `adr/`              | Architecture Decision Records                         |
| `adr/template.md`   | ADR template                                          |
| `adr/ADR-001-auth-clerk.md` | First ADR (auth approach — Clerk)             |
| `adr/ADR-003-coaching-safety-guardrails.md` | ADR: coaching guardrail architecture (spike #41) |

## Architecture Diagram

`architecture.md` is the **single source of truth** for the system diagram.
The root `README.md` mirrors it. When the architecture changes, update
`docs/architecture.md` first, then sync the root README.

## ADR Process

An ADR is written for every significant, hard-to-reverse technical decision.
The locked stack decisions (Clerk, Claude API, Podman, PostgreSQL, native
mobile over cross-platform) should each have an ADR for future-reference and
onboarding clarity.

See `adr/template.md` for the format.
