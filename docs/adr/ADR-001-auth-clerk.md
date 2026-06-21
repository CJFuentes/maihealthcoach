# ADR-001: Use Clerk for Authentication and Identity

**Date:** 2026-06-21
**Status:** Accepted

---

## Context

MAI Health Coach requires user authentication across three clients (web,
iOS, Android) with a shared backend API. The options are: build custom
auth (sessions or JWTs), use an identity provider (Clerk, Auth0, Supabase
Auth, Firebase Auth), or use ASP.NET Core Identity with a Postgres backing
store.

Requirements:
- Single identity across web and both mobile clients
- Social login (Google, Apple) likely needed in a future milestone
- Minimal custom auth code — auth is not a differentiator for this product
- Backend must validate tokens without storing credentials

## Decision

We will use **Clerk** for identity management. The backend validates Clerk
JWTs by fetching the JWKS endpoint (`/api/.well-known/jwks.json`).
Clients obtain a Clerk session token and pass it as a Bearer token on every
API request. The backend never sees or stores passwords.

## Consequences

### Positive
- Zero custom auth code on the backend beyond JWT middleware
- Social login, MFA, and device management are built-in
- Native Clerk SDKs for React, iOS, and Android match our client stack
- JWKS-based validation is stateless and scales horizontally

### Negative / Trade-offs
- Dependency on a third-party SaaS; outage = login outage
- Clerk pricing applies beyond the free tier
- Clerk user IDs become a foreign key in our domain — migration away would
  require a data backfill

### Neutral
- JWT validation is standard middleware; swapping the issuer is feasible if
  we ever migrate away

## Alternatives Considered

| Alternative | Why rejected |
|---|---|
| ASP.NET Core Identity + Postgres | More code, more maintenance, custom session management across 3 clients |
| Auth0 | More expensive at scale; Clerk has better React/mobile DX |
| Supabase Auth | Ties auth to Supabase DB; we are using Postgres directly with EF Core |
| Firebase Auth | Google ecosystem lock-in; weaker .NET SDK support |

## References

- [Clerk documentation](https://clerk.com/docs)
- GitHub Issue #1 (repo bootstrap — tech stack lock)
- GitHub Project #11: https://github.com/users/CJFuentes/projects/11
