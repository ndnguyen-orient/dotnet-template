# Plan: <Issue Title> (#<issue-number>)

> **Issue**: <issue URL>
> **Standalone**: this plan is executable without reading any other file.

## Goal
One sentence: what changes and why.

## Scope & Non-goals
- In scope: <explicit bullets>
- Out of scope: <explicit bullets>

## Requirement Traceability
| Acceptance criterion | Plan section | Verification |
|---|---|---|
| AC-1 … | Changes → <section> | <test name / manual check> |

## Changes

### Domain / Application / Infrastructure / Api / ClientApp
- **File**: `source/…/Foo.cs` — <what to add/modify>
- **New file**: `source/…/Bar.cs` — <purpose>
- **EF migration** (if entities change): `dotnet ef migrations add <Name> --project source/Sample.Infrastructure --startup-project source/Sample.Api`

<Code snippets showing key signatures/contracts only — not full implementation.>

## Key Files
- `source/…/…` — <why touched>
- `tests/…/…` — <new/updated tests>

## Testing & Verification
- `dotnet build -c Release` passes
- `dotnet test -c Release` passes
- New tests: <names + what they cover>
- Manual check: <curl / UI step>

## Template parameterization (if applicable)
- Any code guarded by `#if (UseApiOnly)` or `<!--#if-->` regions and why.

## Branch & PR
- **Branch**: `feat/<short-description>-<issue-number>` (or `fix/…`, `chore/…`)
- **PR title** (Conventional Commits, enforced by `pr-lint.yml`): `<type>(<scope>): <summary> (#<issue-number>)`

## Plan Quality Gate
- [ ] All ACs in the traceability table
- [ ] In-scope / out-of-scope explicit
- [ ] No open questions
- [ ] Commands and pass criteria are concrete
- [ ] Migration step included if entities changed
- [ ] Template parameterization considered

## Additional Ideas (Review Required)

> Two types, both `Pending user decision` until confirmed.
> Type A — from the issue/comments (e.g. safer rollout, observability).
> Type B — *Claude's observations* from codebase scan (reuse opportunity, adjacent smell, coupling risk).

| Idea | Source | Why it helps | Cost | Decision |
|---|---|---|---|---|
| … | Issue / *Claude's observation* | … | Low/Med/High | Pending user decision |

If none: `No additional ideas identified.`
