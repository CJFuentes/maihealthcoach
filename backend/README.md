# Backend — ASP.NET Core Web API

This directory contains the server-side application for MAI Health Coach.

## Stack

| Concern           | Technology                          |
|-------------------|-------------------------------------|
| Framework         | ASP.NET Core Web API (.NET 9)       |
| Architecture      | Layered: Api / Application / Domain / Infrastructure |
| Database          | PostgreSQL 16 via Entity Framework Core |
| Auth              | Clerk JWT validation (JWKS endpoint) |
| AI Coach          | Anthropic Claude API (`CoachService`) — `claude-sonnet-4-6` default, `claude-opus-4-8` for escalated requests |
| Food Data         | Open Food Facts via `NutritionLookupService` (cached in Postgres) |
| Testing           | xUnit                               |
| Containerization  | Podman (`Containerfile` in `/deploy`) |

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
