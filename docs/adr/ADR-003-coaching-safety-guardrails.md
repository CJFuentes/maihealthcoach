# ADR-003: MAI Coaching Safety Guardrail Architecture

**Date:** 2026-06-24
**Status:** Accepted

---

## Context

MAI Health Coach uses Claude (Anthropic) as its coaching AI. Any LLM-based coaching tool carries
liability and harm risk if it responds to inputs involving eating disorders, self-harm, medical
emergencies, dangerous diet advice, or medical diagnosis. Issue #41 mandates that before any
downstream coaching feature ships (#37 meal suggestions, #38 nudges, #39 chat history), the
coaching pipeline must have:

1. A refined system prompt with explicit, enumerated guardrails.
2. A reusable client-facing disclaimer carried on every coaching response.
3. A deterministic, testable pre-screen for red-flag inputs that does not depend on the LLM.
4. Documented redirect language and crisis-resource references.

The architecture question is **where these components live, how they wire into `CoachService`,
and how the disclaimer reaches clients** — all without touching the Api or web layers, which the
issue scope excludes.

## Decision

> We will implement a layered defence: a refined system prompt in
> `CoachPromptBuilder.SystemPrompt`, a deterministic `CoachInputRiskClassifier` that pre-screens
> user input before the LLM is called, a `CoachSafetyResponder` that owns the redirect and note
> copy, and a `CoachResult.Disclaimer` property that carries a reusable disclaimer on every
> successful response without any Api-layer change.

Concretely:

- **`CoachInputRiskClassifier`** (Application/Coaching): an `internal sealed partial` class using
  `[GeneratedRegex]` source-generated patterns for `High` and `Elevated` risk. `High` matches
  **unambiguous harm intent only** (purging, induced vomiting, self-harm, suicidal ideation,
  pro-eating-disorder content, overdose); `Elevated` matches extreme-diet phrasing and first-person
  medical signals. The patterns are flat, word-boundary-anchored alternations with no nested
  quantifiers, so they cannot exhibit catastrophic backtracking. `Classify(string?)` is pure,
  synchronous, null-safe, and has no I/O. It is constructed directly in `CoachService` — no DI.

- **`CoachSafetyResponder`** (Application/Coaching): a static class owning the
  `GuardrailModelSentinel`, `HighRiskRedirectText`, and `ElevatedRiskSafetyNote` constants. It is
  the single place to revise crisis copy. The redirect language is region-generic (recommends a
  doctor / registered dietitian / mental-health professional, references 988 for US/Canada, and
  local emergency services).

- **`CoachResult.Disclaimer`** (Application/Coaching): a new nullable property added as the last
  positional parameter of the `CoachResult` record (defaulted to `null`). `CoachService` populates
  it with `CoachPromptBuilder.SafetyDisclaimer` on every successful response. No Api-layer change
  is required — the property rides through the existing result object.

- **`CoachService`** (Infrastructure/Coaching): after the existing API-key guard and before the
  prompt is built, it classifies `request.UserMessage`. `High` short-circuits to the redirect
  **without calling the model** (logging the interception with no message content) and tags
  `ModelUsed` with the sentinel. `Elevated` proceeds normally and appends the safety note to the
  reply text (a single channel). All successful results carry the disclaimer; failures do not.

- **System prompt** (`CoachPromptBuilder.SystemPrompt`): substantially expanded with MAI's scope,
  enumerated hard limits, hard allergen/dietary-restriction enforcement, sensitive-situation
  handling, tone guidance, and an in-prompt disclaimer paragraph.

## Consequences

### Positive

- The deterministic pre-screen catches the highest-risk inputs before any LLM cost is incurred and
  before the model can inadvertently assist harmful behaviour.
- All safety text and classification logic live in the Application layer (pure, no I/O) and are
  fully testable without infrastructure mocks.
- Disclaimer delivery is guaranteed at the result level — no reliance on client-side logic to
  attach it.
- `CoachSafetyResponder` centralises crisis-resource copy for one-place updates.
- No Api, web, DI, or EF changes; the change set is small and contained.

### Negative / Trade-offs

- The keyword/regex classifier has both false positives (over-steering legitimate queries that
  mention flagged words) and false negatives (novel phrasings, obfuscation, non-English input). It
  is a conservative first pass, not a complete solution.
- `CoachResult` gains a positional parameter; any future positional deconstruction must account for
  it. Today the only caller is `CoachService`, so the blast radius is minimal.
- The `ModelUsed = "guardrail-safety-sentinel"` value for high-risk intercepts overloads the
  `ModelUsed` field; billing/analytics consumers must exclude the sentinel.

### Neutral

- `Disclaimer` is nullable: it is populated on every successful result — including the
  high-risk guardrail redirect — and is `null` only on failures. Callers should check
  `IsSuccess` before reading `Disclaimer`.

## Alternatives Considered

| Alternative | Why rejected |
|---|---|
| Disclaimer as a global static constant on a separate `CoachingConstants` class | Requires the Api layer to actively look up and attach it; not guaranteed on every response |
| ML-based risk classifier | No external calls allowed; adds a model artefact to maintain; overkill for MVP |
| `RegexOptions.Compiled` static `Regex` fields | `[GeneratedRegex]` produces the same compiled matcher at build time with zero startup JIT cost and analyzer-verified patterns |
| System-prompt-only guardrails (no pre-screen classifier) | The LLM can be manipulated; a deterministic layer is needed as a hard stop for the highest-risk categories |
| Separate `SafetyResult` type for high-risk inputs | Breaks the `ICoachService` contract and forces all downstream callers to handle a new type |
| New `CoachErrorCategory.Intercepted` for high-risk | Forces clients to render a non-error message from an error code; the redirect IS a successful response |

## References

- GitHub Issue #41 (this spike)
- GitHub Issue #36 (CoachService — merged)
- `docs/coaching-safety-guardrails.md` (full policy)
- ADR-002 (API versioning strategy)
- GitHub Project #11: https://github.com/users/CJFuentes/projects/11
