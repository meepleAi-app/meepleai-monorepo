# API Smoke Tests (Bruno)

Bruno-based smoke collection that runs against a real running API. Validates the post-merge / pre-prod release surface using the **smoke-aaron** persona (`smoke-aaron@meepleai.test`, free-tier).

Triggered by `.github/workflows/api-smoke.yml` on PRs `main-staging → main`, weekly on `main-dev`, and on PRs to `main-dev` that change the smoke itself (#4978). The nightly cron of `e2e-smoke-real-backend.yml` that this README used to cite was removed on 2026-06-08.

## Quick start

```bash
# API on localhost:8080 with the fixture applied (tests/fixtures/smoke-test-users.sql)
tests/api-smoke/run-smoke.sh --env local                         # the whole collection
tests/api-smoke/run-smoke.sh --env local --collection kb          # one sub-collection
SMOKE_API_BASE=http://127.0.0.1:18080 tests/api-smoke/run-smoke.sh --env local   # another port
```

The script runs Bruno from the collection root (the folder with `bruno.json`) with `-r`, and fails if Bruno ran 0 requests. The JSON report goes to `tests/api-smoke/reports/bruno-report.json`.

What the API needs, as the workflow sets it up (#4978):

- **Development** environment, with `TestEndpoints__Enabled=true`: the reset route exists only in Development and answers only with the flag.
- A seeded shared catalog (`SEED_PROFILE=Staging`), since `kb` and `sessions` work on the first shared game.
- No placeholder `storage.secret`: its seed-bucket endpoint makes the catalog seed fail.

The scenarios authenticate with the `meepleai_session` cookie, which Bruno's cookie jar carries between requests. The API returns no token. The scripts that call the API themselves (the reset, the quota seed of `private-game/06`, the ownership steps) use `axios` and the cookie captured by the `script:post-response` of `collection.bru`: the script sandbox has no `fetch`.

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
| `private-game/06-tier-quota-402.bru` | `[201, 402]` | **strict `402`** | `TierLimits.FreeTier.MaxPrivateGames = 3`. After reset + scenario 04 (1 game), this scenario's `pre-request` seeds 2 more games via direct API calls; the main POST is therefore the 4th attempt and must `402`. *#4978: until then `POST /private-games` did not check the limit at all, and the scenario had never reached the API; the check and its `PRIVATE_GAME_QUOTA_EXCEEDED` code came with #4978.* |

## Gating status — Phase B (#943)

**Phase B FLIPPED**: `continue-on-error: true` removed from `api-smoke.yml`. The release-gate registry (`.github/release-gates.yml`) still classifies `smoke (ubuntu-latest)` and `smoke (windows-latest)` as **`warning`** (#4978): until #4978 the job never reached a request, so the streak below was never measured. They become `blocker` after two consecutive green release rounds.

### Stability evidence at flip time

Workflow event cadence is low (release cycle ~1 PR/month). Streak verification uses **last-N-runs conclusion check** (avoids vacuous day-count streak inflation when no PRs are firing):

```bash
# Verify last N=10 completed workflow_runs ALL have conclusion = success
gh run list --workflow="api-smoke.yml" --status=completed \
   --json conclusion --limit 10 \
   | jq '[.[] | select(.conclusion == "failure")] | length'   # MUST be 0
```

For deeper scenario-level inspection (only when a failure occurs), use the structured jobs API:

```bash
gh run view <runId> --json jobs \
   | jq '[.jobs[].steps[] | select(.conclusion=="failure")] | .[].name'
```

### Rollback criteria (post-flip monitoring)

Re-flip to `continue-on-error: true` if **any** of:
- 2 consecutive workflow failures on `main-staging → main` PRs (excluding cancelled), OR
- Single failure with root cause = flaky infra (not a real regression)

Document the rollback PR + root cause in this README before re-flip.

### Permanently-tolerant scope

`private-game/07-bgg-search-throttle.bru` stays tolerant `[200, 429, 503]` (BGG external API rate-limits). Out of scope for Phase B gate-flip — a BGG outage produces a `success` workflow conclusion (the assertion is tolerant by design) and therefore **cannot** trigger a rollback criterion.

## Scenarios removed in #4978

They called routes the API no longer has, so they had never passed:

- `agents/02-create-user-agent`, `03-start-testing`, `04-publish`, `05-unpublish`, `06-soft-delete`, `07-restore`, `09-tier-quota-402`: the user-agent lifecycle (`POST /agents/user`, `/agents/{id}/start-testing|publish|unpublish|restore`, `DELETE /agents/{id}`). `AgentsEndpoints.cs` maps only `GET /agents` and `GET /agents/{id}`. `08-builder-filters-status` became `02-list-my-library-agents`, because `GET /agents` has no `status` filter (it takes `activeOnly`, `type` and `scope`).
- `sessions/game-session/01-create-track-finalize`, `02-finalize-soft-delete`, `03-fk-constraint-cascade`: `POST /sessions` and `POST /sessions/{id}/complete` do not exist. Game sessions are created with `POST /game-sessions`.

## Refs

- Issue #943 (Phase A + Phase B)
- EPIC #906 (closed) — original smoke harness
- Triple-gate handler: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ResetSmokeAaronCommandHandler.cs`
- Reset endpoint: `apps/api/src/Api/Routing/TestEndpoints.cs` — `POST /api/v1/test/reset-smoke-aaron`
- Smoke persona seed: `tests/fixtures/smoke-test-users.sql`
