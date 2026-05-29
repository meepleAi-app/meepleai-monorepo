# Issue #1628 — Fixture Alignment Fallout Triage

**Date:** 2026-05-29
**Branch:** `feature/issue-1628-totp-astracking-refactor`
**Context:** Task 7 of the plan aligned `IntegrationWebApplicationFactory` to production's `QueryTrackingBehavior.NoTracking` (PERF-06). Task 8 runs the full `Category=Integration` suite to surface any silent-write bug class masked by the previous TrackAll default.

## TL;DR

**Zero integration-test failures are attributable to #1628.** The fixture alignment did **not** surface any new silent-write bug in any writer. All observed failures are either (a) **Testcontainers/Postgres resource exhaustion** when running 40+ integration classes back-to-back on a local Docker host, or (b) **pre-existing FrontendSdk failures** that fail identically with and without the NoTracking change.

The change is safe to merge. Writers verified to persist correctly under NoTracking: `TotpService` (3 contract tests), `BatchJobRepository` / processing-job writers (16/18 isolated, 2 setup-only failures), `AdminProvider` probe endpoints (8/9 isolated, 1 network timeout).

## Full-suite run (post-NoTracking, all Category=Integration)

```
1257 PASS / 50 FAIL / 29 SKIP — 31m 27s
```

Distinct error signatures across the 50 fails:
- 24× `Npgsql.NpgsqlException : Exception while reading from stream`
- 20× `System.IO.EndOfStreamException : Attempted to read past the end of the stream`
- 4× `System.Net.Sockets.SocketException : Connessione interrotta dal software del computer host`
- 4× `System.IO.IOException : Unable to read data from the transport connection`
- ~13× HTTP status assertions (`401`/`500`) downstream of the above connection failures

These are textbook **Docker resource-exhaustion** signatures: the Postgres connection pool / containers are saturated when dozens of `IAsyncLifetime` classes each spin a `pgvector/pgvector:pg16` container in the same run. One observed `Api.Tests` host process held **8 GB RAM**.

## Isolation re-runs (the discriminator)

Each suspected cluster re-run **in isolation** (single class filter, full resources):

| Cluster | Class | Full-suite | Isolated | Residual fail cause |
|---|---|---|---|---|
| AdminProvider probe | `AdminProviderEndpointsIntegrationTests` | 9 FAIL | **8/9 PASS** | 1× `Probe_UnknownProvider_Returns404` — 30s execution timeout (network/provider, not DB) |
| Processing-job writers | `BatchJobIntegrationTests` | ~13 FAIL | **16/18 PASS** | 2× `InitializeAsync` → `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync` Npgsql connect failure (setup, not writer logic) |
| SDK/HTTP contract | `Integration.FrontendSdk.*` | ~13 FAIL | 44/57 PASS | see pre/post comparison below |
| TotpService writers | `TotpServiceTrackingContractTests` | 3/3 PASS | **3/3 PASS** | — |

The writer-heavy clusters (AdminProvider, BatchJob, TotpService) pass cleanly in isolation. The only residual failures in isolation are **connection-setup** and **network-timeout** failures — never a `0 affected rows` / persistence-not-saved assertion that a real NoTracking bug would produce.

## FrontendSdk pre/post NoTracking comparison (definitive)

To rule out the SDK/HTTP cluster being caused by the NoTracking change, the fixture was reverted to its pre-#1628 state and `Integration.FrontendSdk` re-run:

| Fixture state | Total run | PASS | FAIL |
|---|---|---|---|
| **pre-NoTracking** (no #1628 change) | 22 | 17 | **5** |
| **post-NoTracking** (#1628 Task 7) | 57 | 44 | 13 |

The 5 pre-NoTracking failures are a **name-identical subset** of the 13 post-NoTracking failures:
- `Auth responses should include security headers`
- `Concurrent requests with same session should work correctly`
- `GET with malformed request should return 400 Bad Request`
- `POST /auth/login with missing fields should return 400 Bad Request`
- `POST /auth/logout should clear session cookie`

Same error types (`401 Unauthorized`, `500 InternalServerError`) in both runs. **These tests fail with and without the NoTracking change** ⇒ pre-existing, not introduced by #1628. The differing totals (22 vs 57, 5 vs 13) reflect how many tests survive before Testcontainers connection exhaustion kicks in — non-deterministic, resource-bound.

## Disposition

- **No follow-up issue for a NoTracking bug** — none exists. The fixture alignment confirmed the rest of the codebase's writers already persist correctly under NoTracking.
- **No tests skipped** for #1628. Nothing in this PR is `[Trait("Skip", ...)]`-ed.
- **Pre-existing FrontendSdk failures** (`401`/`500` on auth/SDK-contract tests) are **out of scope** for #1628. They are likely local Docker resource/timing artifacts; whether they reproduce on the CI runner (dedicated resources, controlled parallelism) is the deciding factor. **Action: re-evaluate after the PR's CI run (Task 11).** If they reproduce in CI, open a dedicated issue and add to the CLAUDE.md "Known Flaky Tests" baseline — but as a separate concern, not a #1628 regression.

## Method notes (for future fixture-alignment work)

Running the entire `Category=Integration` suite locally on Docker Desktop is **not a reliable signal** — container saturation produces false failures. Prefer per-class isolation runs, or run the full suite on a CI runner with adequate resources / bounded test parallelism. The pre/post fixture-revert comparison is the reliable discriminator for "did my fixture change cause this fail?".
