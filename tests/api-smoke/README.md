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

Most scenarios use strict `expect(...).to.equal(N)` assertions after Phase A (#943). Three files remain **intentionally tolerant**:

| File | Reason | Status |
|------|--------|--------|
| `private-game/03-create-from-bgg-id-conflict-redirect.bru` | API behaviour policy-dependent (201 if private copy auto-creates, 409 if conflict-redirect). Tolerant until product decision codified. | Phase A.2 candidate |
| `private-game/06-tier-quota-402.bru` | Free-tier `MaxPrivateGames` quota is DB-seeded (`TierLimits`). After the pre-request reset the collection creates 1-2 games before this scenario fires; whether the next create is 201 (within quota) or 402 (over quota) depends on the seeded limit. Tightening requires pre-seeding N games OR looping until 402. | Phase A.2 candidate |
| `private-game/07-bgg-search-throttle.bru` | Depends on external BGG API (`api.geekdo.com`) rate limits — we can't make BGG return 200 deterministically. **Permanently tolerant by design.** | Won't fix |

Phase A.2 (#943 follow-up) addresses the first two. The third is documented and accepted.

## Soft-launch flag

`api-smoke.yml` carries `continue-on-error: true` (the soft-launch flag). It will be flipped to `false` in **Phase B** of #943 after **14 consecutive days of green** smoke runs measured by:

```bash
gh run list --workflow="API Smoke (Bruno)" --status=completed \
   --json conclusion,createdAt --created '>=2026-MM-DD' \
   | jq '[.[] | select(.conclusion == "failure")] | length'
```

The flip is a single-PR change and stays tracked on the original #943 thread.

## Refs

- Issue #943 (Phase A + Phase B)
- EPIC #906 (closed) — original smoke harness
- Triple-gate handler: `apps/api/src/Api/BoundedContexts/Administration/Application/Commands/ResetSmokeAaronCommandHandler.cs`
- Reset endpoint: `apps/api/src/Api/Routing/TestEndpoints.cs` — `POST /api/v1/test/reset-smoke-aaron`
- Smoke persona seed: `tests/fixtures/smoke-test-users.sql`
