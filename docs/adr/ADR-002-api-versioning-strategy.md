# ADR-002: API Versioning and Per-Version OpenAPI Strategy

**Date:** 2026-06-24
**Status:** Accepted

---

## Context

MAI Health Coach exposes a single backend API consumed by three clients (web,
iOS, Android). As the product evolves, the API contract will change in
backward-incompatible ways. We need a versioning scheme that:

- Lets clients pin to a specific API version and migrate on their own schedule
- Keeps routing unambiguous and easy to reason about in logs, proxies, and tests
- Produces a discoverable, machine-readable contract (OpenAPI) per version
- Builds reliably on .NET 10 without depending on prerelease packages, which
  the project's tech-stack lock (Issue #1) disallows for foundational pieces

The .NET ecosystem offers several versioning placements (URL segment, query
string, header, media type) via the `Asp.Versioning` family, and several OpenAPI
toolchains (`Microsoft.AspNetCore.OpenApi`, Swashbuckle, NSwag). At the time of
this decision, `Asp.Versioning.OpenApi` (which integrates versioning directly
with the OpenAPI document generator) and the Scalar interactive UI were only
available as prerelease or as additional packages that increase build risk on
.NET 10.

## Decision

> We will use **URL-segment API versioning** with the stable `Asp.Versioning.Http`
> and `Asp.Versioning.Mvc.ApiExplorer` packages (10.0.0), and generate a
> **per-version OpenAPI document** with the existing stable
> `Microsoft.AspNetCore.OpenApi` (10.0.2).

Concretely:

- **Versioning placement:** URL segment. Routes are declared under
  `api/v{version:apiVersion}` and resolved by `UrlSegmentApiVersionReader`, so
  `/api/v1/ping` is the authoritative, literal request path. The default version
  is `1.0`, assumed when unspecified, and `ReportApiVersions` advertises
  supported versions in response headers.

- **API explorer wiring:** `AddApiExplorer` registers the Asp.Versioning API
  description provider so `Microsoft.AspNetCore.OpenApi` can discover versioned
  endpoints. `GroupNameFormat = "'v'VVV"` groups endpoints as `v1`, `v2`, etc.

- **Literal paths in the document:** `SubstituteApiVersionInUrl = true` replaces
  the `{version:apiVersion}` route token with the concrete version number in each
  `ApiDescription.RelativePath`, so the document reports `/api/v1/ping` rather
  than `/api/v{version}/ping`.

- **Per-version OpenAPI document:** `AddOpenApi("v1", ...)` registers a named
  document served at `/openapi/v1.json`. Its `ShouldInclude` predicate admits
  endpoints whose `GroupName` is `v1` plus any future unversioned minimal-API
  endpoint (`GroupName == null`, e.g. a future `/status` route registered outside
  a versioned group). Note that health-check endpoints (`MapHealthChecks`, e.g.
  `/healthz`) are not part of the API description pipeline, so they never appear
  in the OpenAPI document — by design. The document endpoint is gated on the
  Development environment so the production surface stays minimal.

- **Versioned endpoint registration:** Endpoints are registered through
  `NewVersionedApi(...)` and a `MapGroup("api/v{version:apiVersion}")
  .HasApiVersion(new ApiVersion(1, 0))` group, which makes the API explorer aware
  of the version that owns each route.

### Adding a v2 (forward strategy)

When a v2 contract is introduced:

1. Register a second named document: `builder.Services.AddOpenApi("v2", ...)`
   with a `ShouldInclude` predicate admitting `v2` (and, as needed, shared
   unversioned endpoints), served at `/openapi/v2.json`.
2. Add a new versioned group:
   `api.MapGroup("api/v{version:apiVersion}").HasApiVersion(new ApiVersion(2, 0))`
   and map the v2 endpoints onto it.
3. Leave the v1 group and `/openapi/v1.json` **unchanged** so existing clients
   keep working through their migration window.

Each version's contract is therefore additive and independently documented;
removing a version is a deliberate, separate deprecation step.

## Consequences

### Positive

- Unambiguous routing: the version is visible in the URL, in logs, proxies,
  and integration tests — no hidden header/media-type negotiation.
- Stable-only dependency graph: no prerelease packages, satisfying the
  tech-stack lock and reducing .NET 10 build risk.
- Discoverable per-version contract at a predictable URL (`/openapi/v{n}.json`).
- Adding a new version is additive and leaves existing versions untouched.

### Negative / Trade-offs

- URL-segment versioning bakes the version into the path, so a single logical
  resource appears at multiple URLs across versions (less "pure" REST than
  content negotiation).
- Without `Asp.Versioning.OpenApi`, we wire versioning into OpenAPI manually via
  `ShouldInclude` predicates and `SubstituteApiVersionInUrl`; each new document
  must be registered by hand.
- No interactive API UI (Scalar/Swagger UI) yet — consumers read the raw JSON
  document until that is added.

### Neutral

- The OpenAPI document endpoint is Development-only; exposing it in other
  environments is a one-line change if needed later.
- The `GroupName == null` branch of `ShouldInclude` is a forward-looking safety
  net: it would admit a future unversioned minimal-API endpoint (e.g. a `/status`
  route) into the v1 document. Health-check endpoints (`/healthz`) are not surfaced
  through the API description pipeline and therefore never appear in the document.

## Alternatives Considered

| Alternative | Why rejected |
|---|---|
| Header / media-type versioning | Version invisible in URL; harder to test, log, and cache; surprising for client developers |
| Query-string versioning | Easy to omit accidentally; muddies caching and routing semantics |
| `Asp.Versioning.OpenApi` for automatic version↔document integration | Prerelease on .NET 10 at decision time; violates stable-package constraint (see Deferred) |
| Swashbuckle / NSwag for OpenAPI generation | Adds another generator when `Microsoft.AspNetCore.OpenApi` (already referenced, stable) covers the need |
| No versioning (single evolving contract) | No safe path for breaking changes across three independently-shipping clients |

## Deferred

The following were intentionally deferred to avoid prerelease and
separate-package build risk on .NET 10, and may be revisited once stable:

- **Scalar interactive API UI** — a richer, interactive document explorer.
  Deferred; consumers use the raw `/openapi/v{n}.json` documents in the
  meantime.
- **`Asp.Versioning.OpenApi`** — would automate the versioning↔OpenAPI
  integration (removing the manual `ShouldInclude` / `SubstituteApiVersionInUrl`
  wiring). Deferred while it carries prerelease/separate-package risk; the manual
  wiring above is the stable stand-in.

## References

- GitHub Issue #8 (API versioning + per-version OpenAPI)
- ADR-001 (auth strategy)
- [ASP.NET Core API versioning (Asp.Versioning)](https://github.com/dotnet/aspnet-api-versioning)
- [Microsoft.AspNetCore.OpenApi](https://learn.microsoft.com/aspnet/core/fundamentals/openapi/aspnetcore-openapi)
- GitHub Project #11: https://github.com/users/CJFuentes/projects/11
