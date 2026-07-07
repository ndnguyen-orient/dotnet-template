---
name: plan-issue
description: Turn a GitHub issue into a single-file implementation plan for this repo, then publish it as a PR on a `plan/{issue}` branch for team review. Use when the user wants to plan (not implement) work described by an issue URL or number.
argument-hint: "<issue-url-or-number>"
---

# /plan-issue

Produce a **standalone implementation plan** for one GitHub issue in this repo. The plan is a single markdown file, opened as a PR so the team can review before anyone writes code.

Do **not** implement. Stop at the plan PR. Implementation is a separate step (see `tdd`).

---

## Step 0: Preflight

Run in parallel:

- `gh auth status` — must be authenticated.
- `git status` — must be clean; if dirty, ask the user to stash/commit first.
- `git rev-parse --abbrev-ref HEAD` — record current branch to return to later.

If any check fails, stop and ask the user to fix it.

---

## Step 1: Resolve the issue

Accept either a full URL or a bare number. Fetch the issue with:

```bash
gh issue view <number> --json number,title,body,labels,assignees,comments,url
```

Extract: title, body (acceptance criteria if present), labels, all comments (decisions often live there), linked PRs.

If the issue is missing acceptance criteria or is too vague to plan, stop and ask the user to clarify **in the issue itself** (so the plan stays traceable) — do not proceed on assumptions.

---

## Step 1b: Read the required input file

Look for `docs/inputs/<issue-number>-*.md`. **This file is required** — it holds the design links, brainstorming, and constraints the GitHub issue alone doesn't capture.

- If **none** matches: stop and tell the user:
  > No input file found at `docs/inputs/<issue-number>-*.md`. Create one using [docs/inputs/TEMPLATE.md](docs/inputs/TEMPLATE.md), then re-run `/plan-issue <issue-number>`.
- If **multiple** match: ask the user which to use via `AskUserQuestion`.
- If **exactly one** matches: parse it for:
  - **Design / Reference Links** — authoritative for UI/UX shape.
  - **Brainstorming** — hints for technical approach.
  - **Constraints & Non-goals** — hard constraints (deadlines, compat) unless the issue contradicts them.

Validate the file has non-empty **Issue**, **Design / Reference Links**, and **Constraints & Non-goals** sections. If any required section is empty, stop and ask the user to fill it in before re-running. (Brainstorming may be empty.)

**Conflict order (highest wins)** when the issue, input file, and repo docs disagree:
1. Issue acceptance criteria + decisions in issue comments
2. Input file's design/reference links (for UI/UX shape)
3. Input file's brainstorming notes (for technical approach)
4. Existing repo patterns

If a conflict can't be resolved by this order, stop and ask via `AskUserQuestion` — don't guess.

---

## Step 2: Read repo context

- Read [CLAUDE.md](CLAUDE.md) — this repo's conventions are load-bearing (Clean Architecture layout under `source/`, EF migrations, JWT-cookie auth, `dotnet new` template parameterization, Conventional Commits PR titles).
- Read any nearby docs the issue references (e.g. `docs/authentication.md` if auth-adjacent).

---

## Step 3: Check for existing work

Before planning, check whether the work is partly done:

```bash
gh pr list --search "<issue-number> in:title,body" --state all
git branch -a | grep -iE "(feat|fix|chore)/.*<issue-number>"
```

Also grep the codebase for obvious markers (issue number in comments, TODOs).

**If any existing work is found — stop and ask** whether to plan from scratch or continue from what exists. Do not assume.

---

## Step 4: Explore the codebase

Use `Explore` agents (up to 3 in parallel) scoped to the affected areas. For each:

- Find files that will need to change.
- Identify existing patterns, utilities, and abstractions to reuse.
- Note the layer (`Domain` / `Application` / `Infrastructure` / `Api` / `ClientApp`) each change belongs in.
- If auth-related: check `AuthCookies`, `AuthEndpoints`, `docs/authentication.md`.
- If schema-related: note that EF migrations are schema-of-record (`source/Sample.Infrastructure/Migrations/`) — a plan touching entities must include a migration step.
- If touching template-varying code: note the `#if (UseApiOnly)` / `<!--#if-->` regions that must stay parameterized.

Record **Claude's own observations** — reuse opportunities, hidden coupling, adjacent smells — separately from what the issue asked for. These go in the "Additional Ideas" section, clearly labelled.

---

## Step 5: Resolve ambiguities before writing

If the plan requires a judgement call the issue doesn't resolve (naming, layer boundary, migration strategy, breaking-change tolerance), use `AskUserQuestion` with 2–4 concrete options. Do not write the plan with open questions in it.

---

## Step 6: Write the plan file

Location: `docs/plans/<issue-number>-<short-slug>.md` (create `docs/plans/` if it doesn't exist).

Use this template:

```markdown
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
```

---

## Step 7: Publish as a PR

**Ask the user explicitly before creating the PR.**

```bash
git checkout -b plan/<issue-number>-<short-slug>
git add docs/plans/
git commit -m "docs(plan): implementation plan for #<issue-number>"
git push -u origin plan/<issue-number>-<short-slug>
gh pr create \
  --title "docs(plan): <Issue Title> (#<issue-number>)" \
  --body "Implementation plan for #<issue-number>. Please review before implementation starts."
```

PR title uses Conventional Commits so `pr-lint.yml` passes.

Return the PR URL to the user.

---

## Step 8: Iterate on feedback

When the user asks to process PR comments:

```bash
gh pr view <pr-number> --comments
gh api repos/{owner}/{repo}/pulls/<pr-number>/comments
```

For each actionable comment: clarify if ambiguous (`AskUserQuestion`), apply to the plan file, commit + push to the same branch, then leave a summary comment on the PR listing what changed per reviewer.

Repeat until approved/merged. Do not start implementation until the user explicitly says so.

---

## Final validation before handing back

- Every AC is in the traceability table.
- Plan file lives under `docs/plans/` and is self-contained.
- Migration step included iff entities changed.
- PR title follows Conventional Commits.
- Additional Ideas section present, with Type A vs Type B clearly labelled.
- No implementation done.
