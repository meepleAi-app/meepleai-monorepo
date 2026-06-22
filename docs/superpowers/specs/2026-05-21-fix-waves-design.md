# V2 Audit Fix Waves — Design

**Date:** 2026-05-21
**Status:** draft (pending user review)
**Scope:** Close the 10 `audit-finding` GitHub issues (#1363–#1372) opened by Phase 2 sub-project A. Each issue gets one PR, executed sequentially, with re-audit between merges.

**Predecessors:**
- Phase 2 sub-project A (PR #1373) — produced the audit toolchain (`scripts/v2_audit/`), the `audit-report.md`, and the 10 GitHub issues.
- Phase 1 (PR #1356) — provided `nav-map.md` (533 navigation destinations) and the renamed librogame mockups.

---

## 1. Goal

Close all 10 `audit-finding` issues by applying one PR per route. Each PR contains explicit per-finding decisions (FIX, SKIP_FP, DEFER) with rationale. Re-audit after each merge catches drift; the global audit count drops from 109 main findings to ≤20.

## 2. Non-goals

- ❌ New audit findings discovered during fix waves (out of scope; tracked for next cycle).
- ❌ Migration of the 48 pending components.
- ❌ Significant structural refactors (go to DEFER → sub-issue).
- ❌ Backend / API / data-model changes.
- ❌ Modifications to `scripts/v2_audit/` itself (toolchain frozen).
- ❌ Phase 1 follow-up issues (classify_todos dead branch, jsx 3-deep brace handling — separate cleanup).

## 3. Architecture

### 3.1 One PR per route, sequential execution

Ten branches, ten PRs, ten merges. Execution order ranks by impact:

| # | Issue | Route | Findings | Subagent model |
|---|---|---|---|---|
| 1 | #1372 | `/sessions/[id]/live` | 26 | Sonnet |
| 2 | #1363 | `/agents/[id]` | 21 | Sonnet |
| 3 | #1371 | `/sessions/[id]` | 19 | Sonnet |
| 4 | #1366 | `/games/[id]` | 15 | Sonnet |
| 5 | #1370 | `/sessions` | 3 | Haiku |
| 6 | #1367 | `/join/event/[code]` | 3 | Haiku |
| 7 | #1364 | `/game-nights` | 3 | Haiku |
| 8 | #1369 | `/players` | 1 | Haiku |
| 9 | #1368 | `/library` | 1 | Haiku |
| 10 | #1365 | `/game-nights/[id]` | 1 | Haiku |

Branches: `feature/audit-fix-<route-slug>` from fresh `main-dev`. Each PR targets `main-dev` (parent branch rule per CLAUDE.md).

### 3.2 Per-route subagent workflow

**Stage A — Implementer subagent** (Sonnet for large, Haiku for small):

1. `git checkout main-dev && git pull --ff-only`
2. `git checkout -b feature/audit-fix-<route-slug>`
3. Read issue body: `gh issue view <num> --json title,body`
4. For each finding in body:
   - Identify target file (nav-finding: pick layout/header/sidebar component from aggregated list; structural/tokens/props: single component named in finding)
   - Decide: FIX, SKIP_FP, or DEFER
   - Apply fix pattern (below)
5. Local verification:
   - `cd apps/web && pnpm lint <modified-files>`
   - `pnpm typecheck`
   - `pnpm test --run --related <modified-files>` if tests exist
   - `python -m scripts.v2_audit.run` partial — confirm target findings disappear
6. Commit, push, open PR: `gh pr create --base main-dev --title "Audit fix: route /xxx (closes #NNNN)"`
7. Report: stats, PR URL

**Stage B — Spec compliance reviewer** (Haiku):
- Confirm PR closes the findings it claims.
- Spot-check 3 random findings: is fix coherent with mockup intent?
- Verify `Closes #NNNN` present.
- Verify no out-of-scope file modifications.

**Stage C — Code quality reviewer** (Haiku):
- ESLint: no new token violations, no `eslint-disable`.
- TypeScript: type safety preserved.
- No dead code introduced.
- Follows existing patterns.

**Stage D — Controller post-merge cycle** (no subagent):
- Wait CI green (~10-15min).
- Merge (squash, delete branch).
- Re-run audit: `python -m scripts.v2_audit.run` + `python -m scripts.v2_audit.create_issues` (idempotent — updates body of remaining issues).
- If issue findings count = 0: close manually with `gh issue close <num> --comment "All findings resolved by PR #X"`.
- Cleanup local branch.

### 3.3 Fix patterns by dimension

| Finding type | Pattern |
|---|---|
| **nav** (Missing Link to /X) | Add `<Link href="/X">` in most appropriate component (typically root layout, header, sidebar, or breadcrumb) |
| **structural** (Missing `<header>`/`<main>`/`<section>`) | Wrap content in semantic element with `role` attribute if needed |
| **tokens** (Hardcoded color utility) | Replace per CLAUDE.md DS-15: `bg-white` → `bg-card`, `text-gray-700` → `text-foreground`, `border-zinc-300` → `border-border`, etc. |
| **tokens** (file-level eslint-disable) | Refactor file to remove directive; if impossible, DEFER + sub-issue |
| **props** (Missing field) | Extend `interface XxxProps` adding field with TypeScript type matching mockup data semantics |

### 3.4 Per-finding decision rule

Each finding receives ONE decision (no silent skips):

| Decision | When | Issue effect |
|---|---|---|
| **FIX** | Modification applied | Checkbox `[x]` |
| **SKIP_FP** | False positive confirmed via inspection | Checkbox `[ ]` + comment `[FP: <reason>]` |
| **DEFER** | Requires separate design / large refactor | Checkbox `[ ]` + comment `[DEFER: sub-issue #XXX]` + new sub-issue created |

### 3.5 Quality bars per dimension

If a PR exceeds these bounds, escalate to controller (possible tool bug, not component bug):

| Dimension | Target FIX rate | SKIP_FP tolerance | DEFER tolerance |
|---|---|---|---|
| Tokens | ≥95% | <5% | 0% |
| Nav | ≥70% | ≤25% | ≤5% |
| Structural | ≥60% | ≤30% | ≤10% |
| Props | ≥50% | ≤30% | ≤20% |

## 4. Acceptance criteria

### Per-PR

1. ✅ CI green (frontend tests, lint, typecheck, codeql).
2. ✅ Body contains `finding | decision | rationale` table for every finding in the issue.
3. ✅ Body contains `Closes #NNNN`.
4. ✅ Stage B + C reviews PASS.
5. ✅ Every finding has explicit decision (no silent skips).
6. ✅ Per-dimension decision rates within bounds defined in §3.5 (otherwise escalate to controller before merge).

### Per-issue closure

- **100% FIX**: `Closes #NNNN` auto-closes on merge.
- **Mixed FIX + SKIP_FP**: controller closes manually with summary comment.
- **Contains DEFER**: issue stays open with `audit-deferred` label + sub-issues created for each DEFER.

### Global sub-project

1. ✅ All 10 PRs merged to `main-dev`.
2. ✅ Final audit re-run: `main_report_count` ≤ 20 (was 109).
3. ✅ No open `audit-finding` issue without explanation (open issues require `audit-deferred` label + tracking sub-issue).
4. ✅ Final stats committed to `docs/superpowers/specs/audit-report-final.md` for historical record.

## 5. Execution model

### Default: sequential

PRs 1 → 10 in strict serial order. Each cycle:
1. Implementer subagent (~30-60min for large, ~10-20min for small)
2. Stage B + C reviews (~5-10min)
3. Wait CI (~10-15min)
4. Merge + cleanup + re-audit (~5-10min)
5. Next cycle

Total: ~8h elapsed.

### Optional: parallel after PR 2

After PRs 1-2 validate the workflow, PR 3-10 can pipeline:
- Subagent N+1 starts implementation as soon as subagent N finishes Stage A (doesn't wait for CI/merge of N).
- Branch isolation prevents conflicts.
- Merge still sequential to maintain ordered history.

Reduces wall-clock to ~4h. Opt-in after PR 2.

## 6. Risks

| Risk | Mitigation |
|---|---|
| CI flakiness | Parallel-mode opt-in after PR 2 absorbs wait time |
| Token violations from nav fixes | Pre-commit `pnpm lint` mandatory; ESLint DS-15 rule catches |
| Conflict between consecutive PRs | Sequential merge + `git rebase main-dev` before push of next branch |
| Over-aggressive FIX (out-of-scope edits) | Stage B verifies modified files = those in finding evidence |
| Over-cautious DEFER (defer everything) | Per-dimension DEFER tolerance bars; escalate if exceeded |
| Re-audit introduces new findings (cascade) | Accept: this sub-project closes ONLY the 10 initial issues |
| Issue body stale during fix | Subagent reads body at setup; re-audit between merges catches drift |
| Mockup itself wrong (audit reveals UI bug) | DEFER finding to mockup-fix sub-issue, separate from component-fix sub-issue |

## 7. Deliverables

| Deliverable | Path / Location |
|---|---|
| Spec | `docs/superpowers/specs/2026-05-21-fix-waves-design.md` |
| Plan | `docs/superpowers/plans/2026-05-21-fix-waves.md` |
| 10 PRs | Tracked via `Closes #NNNN` |
| Per-merge audit report updates | `docs/superpowers/specs/audit-report.md` regenerated each cycle |
| Final audit snapshot | `docs/superpowers/specs/audit-report-final.md` |
| Tracked deferrals | GitHub sub-issues labeled `audit-deferred` |

## 8. Estimate

- **Spec + plan**: ~1h (this brainstorm + plan writing).
- **Implementation sequential**: ~5-8h tool work + ~2.5h CI wait wall-clock.
- **Implementation parallel (after PR 2)**: ~3-4h tool work + ~1.5h CI wait.
- **Manual review + merge**: ~10min × 10 = ~2h (user).

**Total: ~10-12h elapsed sequential, ~6-8h parallel.**

## 9. Out-of-scope follow-ups

After this sub-project completes, separate specs/plans will address:

- Re-audit cycle if new findings emerge after merges.
- Sub-project B: audit the 48 pending components once implemented.
- Sub-project D: gap analysis of missing routes (nav-map destinations without app routes).
- Phase 1 toolchain follow-ups (classify_todos dead branch, jsx 3-deep brace handling, missing unit tests for apply_map.py + classify_todos.py).
