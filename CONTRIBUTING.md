# Contributing to MAI Health Coach

Thank you for contributing. This document describes the branch naming convention,
commit style, PR process, and how work ties to the GitHub Project board.

**Project board (GitHub Project #11):**
https://github.com/users/CJFuentes/projects/11

---

## Table of Contents

1. [Getting Started](#getting-started)
2. [Branch Naming Convention](#branch-naming-convention)
3. [Commit Message Convention](#commit-message-convention)
4. [Pull Request Process](#pull-request-process)
5. [Project Board Workflow](#project-board-workflow)
6. [Code Style](#code-style)
7. [Testing Requirements](#testing-requirements)

---

## Getting Started

1. Fork or clone `https://github.com/CJFuentes/maihealthcoach`
2. Create a branch from `main` (see naming convention below)
3. Make your changes with focused commits
4. Open a PR against `main`

---

## Branch Naming Convention

Format: `<type>/<issue-number>-<short-slug>`

The slug is lowercase, hyphen-separated, max ~40 characters.

| Type      | When to use                              | Example                         |
|-----------|------------------------------------------|---------------------------------|
| `feature` | New functionality                        | `feature/1-repo-structure`      |
| `fix`     | Bug fix                                  | `fix/42-barcode-null-crash`     |
| `chore`   | Tooling, deps, config — no runtime change | `chore/7-update-nuget`         |
| `docs`    | Documentation only                       | `docs/3-architecture-adr`       |
| `release` | Release preparation                      | `release/v1.0.0`               |
| `hotfix`  | Critical production fix off a release    | `hotfix/v1.0.1-login-timeout`  |

Issue-linked branches (`feature`, `fix`, `chore`, `docs`) must match:
`^(feature|fix|chore|docs)/[0-9]+-[a-z0-9-]+$`

Version-based branches (`release`, `hotfix`) are not tied to an issue number
and must match:
`^(release|hotfix)/v[0-9]+\.[0-9]+\.[0-9]+(-[a-z0-9-]+)?$`

---

## Commit Message Convention

Use [Conventional Commits](https://www.conventionalcommits.org/) 1.0:

```
<type>(<scope>): <short imperative summary> (#<issue>)

[optional body — wrap at 72 chars]

[optional footer: BREAKING CHANGE: ...]
```

**Types:** `feat`, `fix`, `docs`, `style`, `refactor`, `test`, `chore`, `build`, `ci`, `perf`

**Scopes:** `backend`, `web`, `ios`, `android`, `deploy`, `docs`, `root`

**Examples:**

```
feat(backend): add NutritionLookupService with OFF integration (#15)

fix(web): prevent double-submit on log-meal form (#33)

chore(root): add .editorconfig and .gitattributes (#1)

docs(docs): add ADR-001 for Clerk auth decision (#3)
```

Rules:
- Subject line ≤ 72 characters
- Imperative mood ("add", "fix", "remove" — not "added", "fixes")
- Reference the issue number in the subject
- Use the body to explain *why*, not *what*

---

## Pull Request Process

1. **Title** — follows Conventional Commits format:
   `feat(backend): add barcode scanning endpoint (#12)`

2. **Description** — use the PR template (`.github/pull_request_template.md`).
   Every PR description must contain `Closes #N` so the issue auto-closes on merge.

3. **Reviewers** — request at least one reviewer before marking Ready for Review.

4. **Checks** — all GitHub Actions CI checks must pass (build, lint, test).

5. **Merging** — use **Squash and Merge** to keep `main` history linear. The
   squash commit message must still follow Conventional Commits format.

6. **Branch cleanup** — delete the branch after merge (GitHub does this
   automatically if configured).

---

## Project Board Workflow

All issues are tracked on **GitHub Project #11**:
https://github.com/users/CJFuentes/projects/11

Column progression:

| Column         | Meaning                                              |
|----------------|------------------------------------------------------|
| `Backlog`      | Defined but not yet scheduled                        |
| `Ready`        | Scheduled for current or next sprint, acceptance criteria clear |
| `In Progress`  | Branch created, work underway                        |
| `In Review`    | PR open, awaiting review                             |
| `Done`         | PR merged, issue closed                              |

When you start work:
1. Move the issue from `Ready` → `In Progress` on the board
2. Create your branch immediately

When you open a PR:
1. Move the issue to `In Review`
2. Link the PR to the issue using `Closes #N`

The project board auto-moves the issue to `Done` on merge if linked correctly.

---

## Code Style

- Follow `.editorconfig` rules (enforced by editors; CI will lint where tooling supports it)
- **C#:** follow Microsoft C# coding conventions; use `dotnet format` before committing
- **TypeScript/React:** ESLint + Prettier (config in `web/`); run `npm run lint` before committing
- **Swift:** SwiftLint (config in `ios/.swiftlint.yml`)
- **Kotlin:** ktlint (config in `android/.editorconfig`)

---

## Testing Requirements

- **Backend:** xUnit; minimum coverage enforced by CI (threshold set per milestone)
- **Web:** Vitest for unit tests; Playwright for E2E (added in a later milestone)
- **iOS:** XCTest
- **Android:** JUnit4 + Robolectric / Espresso

No PR that deletes or skips existing tests will be merged without explicit
justification in the PR description.
