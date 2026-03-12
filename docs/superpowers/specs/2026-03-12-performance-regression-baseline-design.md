# Performance Regression Baseline in Deploy Pipeline

**Issue**: #184 | **Epic**: #171 (CI/CD Pipeline Hardening)
**Date**: 2026-03-12 | **Status**: Approved

## Problem

Production and staging deploy pipelines have basic curl-based response time checks with static thresholds. No baseline tracking, no trend detection across deployments, no K6 integration in deploy pipelines.

## Solution

Add K6 smoke tests to deploy pipelines with cross-deployment trend comparison using the existing `DEPLOYMENT.json` on the server.

## Architecture

### Data Flow

```
BEFORE deploy (new job: snapshot-baseline)
  |
  v
[SSH] Read previous DEPLOYMENT.json → extract performance field → job output
  |
  v
Deploy runs (overwrites DEPLOYMENT.json without performance field)
  |
  v
AFTER deploy (validate job)
  |
  v
[Runner] Checkout repo (required for K6 scripts)
  |
  v
[Runner] Install K6 → run deploy-smoke.js against live URL
  |
  v
[Runner] deploy-baseline-compare.sh:
  - Parse K6 summary JSON (current, aggregate p95/p99/error_rate)
  - Parse previous baseline JSON (from snapshot-baseline job output)
  - Compare aggregate metrics (20% regression threshold)
  - Write comparison table → $GITHUB_STEP_SUMMARY
  - Respect PERF_REGRESSION_MODE (warn|fail, default warn on push triggers)
  |
  v
[SSH] Update DEPLOYMENT.json: merge new performance metrics into existing file
```

### Critical: Baseline Snapshot Timing

The `deploy` job writes a fresh `DEPLOYMENT.json` without a `performance` field. The previous baseline **must be read before deploy runs**, not after. This is achieved via a new `snapshot-baseline` job that runs before `deploy` and passes the previous metrics as a job output to `validate`.

### DEPLOYMENT.json Schema Extension

The existing `DEPLOYMENT.json` on the server gains a `performance` field:

```json
{
  "version": "staging-20260312-abc1234",
  "api_image": "ghcr.io/.../api:staging-latest",
  "web_image": "ghcr.io/.../web:staging-latest",
  "environment": "staging",
  "deployed_at": "2026-03-12T10:00:00Z",
  "deployed_by": "github-actions",
  "commit": "abc1234...",
  "workflow_run": "12345",
  "performance": {
    "measured_at": "2026-03-12T10:05:00Z",
    "aggregate": {
      "avg_ms": 132,
      "p95_ms": 450,
      "p99_ms": 600,
      "error_rate": 0.0
    }
  }
}
```

**Note**: K6's `--summary-export` produces aggregate metrics only (not per-endpoint). The schema uses aggregate values to match what K6 actually outputs. Per-endpoint breakdown would require `handleSummary()` — deferred to a future enhancement if needed.

## New Files

### `tests/k6/scenarios/deploy-smoke.js`

Lightweight K6 script for post-deploy validation against live URLs.

- Accepts `BASE_URL` env var (defaults to `https://meepleai.app`)
- Accepts `API_BASE_URL` env var (defaults to `BASE_URL`). Required for production where the API lives on a separate subdomain (`https://api.meepleai.com`)
- 10 iterations per endpoint across 3 VUs (~30 seconds total)
- Measures aggregate p95, p99, avg, error rate across all endpoints
- Uses K6 URL tags to label each endpoint in the summary for readability
- Outputs K6 JSON summary via `--summary-export` to a dedicated directory (`reports/deploy/`) to avoid conflicts with the existing `baseline-compare.sh` which scans `reports/*-summary.json`
- No authentication required — public endpoints only
- Relaxed thresholds for external network (p95 < 2s, not 500ms)

Endpoints tested:
1. `{API_BASE_URL}/health`
2. `{BASE_URL}/`
3. `{API_BASE_URL}/api/v1/shared-game-catalog/games?limit=1`

Usage:
```bash
# Staging (unified reverse proxy)
k6 run --env BASE_URL=https://meepleai.app \
       --summary-export=reports/deploy/deploy-smoke-summary.json \
       tests/k6/scenarios/deploy-smoke.js

# Production (separate API subdomain)
k6 run --env BASE_URL=https://meepleai.com \
       --env API_BASE_URL=https://api.meepleai.com \
       --summary-export=reports/deploy/deploy-smoke-summary.json \
       tests/k6/scenarios/deploy-smoke.js
```

### `tests/k6/utils/deploy-baseline-compare.sh`

Cross-deployment trend comparison script.

- Reads previous baseline JSON (from snapshot-baseline job output)
- Reads current K6 summary JSON (aggregate `http_req_duration` p95/p99 and `http_req_failed` rate)
- Compares aggregate p95/p99 values
- Regression threshold: 20% increase (configurable via `REGRESSION_THRESHOLD_PCT` env var)
- Respects `PERF_REGRESSION_MODE`:
  - `warn` (default): `::warning::` annotation, exit 0
  - `fail`: exit 1 on regression detection
- Writes markdown comparison table to stdout and `$GITHUB_STEP_SUMMARY`
- Handles first-deploy case (no previous baseline) gracefully — records baseline only, no comparison

Usage:
```bash
PERF_REGRESSION_MODE=warn \
REGRESSION_THRESHOLD_PCT=20 \
  ./deploy-baseline-compare.sh <k6-summary.json> <previous-baseline.json>
```

**Output path**: K6 summary is written to `reports/deploy/deploy-smoke-summary.json` (NOT to `reports/` root). This prevents the existing `baseline-compare.sh` from picking it up and comparing live-site latency against the internal 500ms SLO threshold.

## Modified Files

### `.github/workflows/deploy-staging.yml`

**New job: `snapshot-baseline`** — runs after `build`, before `deploy`:
- SSH to server, read `DEPLOYMENT.json`, extract `performance` field
- Output as job output (`previous_baseline`) for `validate` to consume
- Gracefully handles missing file (first deploy)

**Modified job: `validate`**:
- **Add** `actions/checkout@v4` step at the start (currently missing in staging)
- **Replace** the "Performance Baseline" curl step with:
  1. **Install K6** — Same install logic as `test-performance.yml` (with ARM64 support)
  2. **Run K6 Deploy Smoke** — Execute `deploy-smoke.js` against `https://meepleai.app`
  3. **Compare & Report** — Run `deploy-baseline-compare.sh` with previous baseline from `snapshot-baseline` job output
  4. **Update Server Baseline** — SSH to server, merge new performance metrics into `DEPLOYMENT.json` using `jq`

**Modified job dependencies**: `validate` gains `needs: [deploy, snapshot-baseline]`

**New workflow input**:
```yaml
perf_regression_mode:
  description: 'Performance regression behavior (warn|fail)'
  required: false
  default: 'warn'
  type: choice
  options:
    - warn
    - fail
```

**Default handling on push triggers**: Workflow steps must use `${{ inputs.perf_regression_mode || 'warn' }}` to ensure the default applies when the workflow is triggered by `push` (where `inputs.*` is undefined), not just on `workflow_dispatch`.

Default threshold: 3.0s (staging).

### `.github/workflows/deploy-production.yml`

Same structural changes as staging with:
- Same `snapshot-baseline` job added
- `validate` job gets checkout (already present in production)
- Target URLs: `BASE_URL=https://meepleai.com`, `API_BASE_URL=https://api.meepleai.com`
- Default threshold: 2.0s (production)
- Same `perf_regression_mode` input defaulting to `warn` with `|| 'warn'` fallback

## Acceptance Criteria Mapping

| Criteria | Implementation |
|----------|---------------|
| Response times for 3+ critical endpoints | K6 smoke tests `/health`, `/`, `/api/v1/shared-game-catalog/games?limit=1` |
| Results visible in GitHub deployment summary | Comparison table in `$GITHUB_STEP_SUMMARY` |
| Warning if P95 exceeds threshold (configurable) | `PERF_REGRESSION_MODE` workflow input (`warn`/`fail`) |
| Trend data accessible across deployments | `DEPLOYMENT.json` on server stores metrics, compared each deploy |

## Out of Scope

- Grafana/dashboard integration
- Historical trend beyond previous deploy (N vs N-1 only)
- Per-endpoint breakdown in baseline (uses K6 aggregate; per-endpoint requires `handleSummary()`)
- Changes to `test-performance.yml` or existing K6 scenarios
- Changes to `baseline-compare.sh` (SLO-based, stays as-is)
- New secrets or infrastructure
- Authenticated endpoint testing in deploy smoke
