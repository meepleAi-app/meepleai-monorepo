# API Smoke Tests (Bruno)

Bruno-based smoke collection that runs against a real running API. Validates the post-merge / pre-prod release surface using the **smoke-aaron** persona (`smoke-aaron@meepleai.test`, free-tier).

Triggered by `.github/workflows/api-smoke.yml` on PRs `main-staging → main`. Nightly cron coverage on `main-dev` lives in `.github/workflows/e2e-smoke-real-backend.yml`.

## Quick start

```bash
cd tests/api-smoke/bruno-collection
bru run --env local --collection .
```

## State management — Phase A reset hook (#943)

Each collection invocation calls `POST /api/v1/test/reset-smoke-aaron` from the collection-level `pre-request` script. This wipes smoke-aaron's transactional rows (private games, chat threads, KB reindex jobs, PDF documents) before scenarios begin.

The endpoint is triple-gated (`TestEndpoints:Enabled=true` + `IsProduction()=false` + hardcoded target UUID). In production it returns 403 and the smoke run continues with whatever state existed.

## Assertion strictness

After Phase A + Phase A.2 (#943), 5 of 6 originally tolerant assertions are now strict. One file remains **intentionally tolerant**:

| File | Reason | Status |
|------|--------|--------|
| `private-game/07-bgg-search-throttle.bru` | Depends on external BGG API (`api.geekdo.com`) rate limits — we can't make BGG return 200 deterministically. **Permanently tolerant by design.** | Won't fix |

### Phase A.2 conversions (2026-05-13)

| File | Before | After | Mechanism |
|------|--------|-------|-----------|
| `private-game/03-create-from-bgg-id-conflict-redirect.bru` | `[201, 409]` | **strict `409`** (with skip-on-missing-precondition for empty catalog) | API confirmed to throw `ConflictException` when BGG ID is already in SharedGameCatalog (`AddPrivateGameCommandHandler.cs`). Scenario 01 fetches a real catalog BGG ID. |
| `private-game/06-tier-quota-402.bru` | `[201, 402]` | **strict `402`** | `TierLimits.FreeTier.MaxPrivateGames = 3`. After reset + scenario 04 (1 game), this scenario's `pre-request` seeds 2 more games via direct API calls; the main POST is therefore the 4th attempt and must `402`. |

## Gating status — Phase B (#943)

**Phase B FLIPPED**: `continue-on-error: true` removed. `api-smoke.yml` is now **gating-blocking** on `main-staging → main` PRs.

### Stability evidence at flip time

Workflow event cadence is low (release cycle ~1 PR/month). Streak verification uses **last-run-success + scenario-level pass-rate** instead of pure day-count to avoid vacuous-streak inflation:

```bash
# 1) Verify last completed workflow_run = success (most recent N=10 runs)
gh run list --workflow="api-smoke.yml" --status=completed \
   --json conclusion,createdAt --limit 10 \
   | jq '[.[] | select(.conclusion == "failure")] | length'   # MUST be 0

# 2) Verify scenario-level pass-rate ≥ 95% (47/48 minimum) in the same N runs
gh run list --workflow="api-smoke.yml" --status=completed \
   --json databaseId --limit 10 \
   | jq -r '.[].databaseId' \
   | xargs -I {} gh run view {} --log 2>/dev/null \
   | grep -E "(passed|failed)" | sort | uniq -c
```

### Rollback criteria (post-flip monitoring)

Re-flip to `continue-on-error: true` if **any** of:
- 2 consecutive workflow failures on `main-staging → main` PRs (excluding cancelled), OR
- Single failure with root cause = flaky infra (not real regression), OR
- BGG external dependency outage exceeding 1h SLO

Document the rollback PR + root cause in this README before re-flip.

### Permanently-tolerant scope

`private-game/07-bgg-search-throttle.bru` stays tolerant `[200, 429, 503]` (BGG external API rate-limits). Out of scope for Phase B gate-flip.

## Refs

- Issue #943 (Phase A + Phase B)
- EPIC #906 (closed) — original smoke harness
- Triple-gate handler: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ResetSmokeAaronCommandHandler.cs`
- Reset endpoint: `apps/api/src/Api/Routing/TestEndpoints.cs` — `POST /api/v1/test/reset-smoke-aaron`
- Smoke persona seed: `tests/fixtures/smoke-test-users.sql`
