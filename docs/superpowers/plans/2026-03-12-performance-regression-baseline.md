# Performance Regression Baseline Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add K6 smoke tests to deploy pipelines with cross-deployment trend comparison via DEPLOYMENT.json on the server.

**Architecture:** New `snapshot-baseline` job reads previous metrics before deploy overwrites `DEPLOYMENT.json`. After deploy, the `validate` job runs K6 smoke tests against live URLs, compares aggregate p95/p99 to the snapshot, and writes the new baseline back. Configurable `warn`/`fail` mode.

**Tech Stack:** K6, bash, GitHub Actions, SSH (appleboy/ssh-action), jq

**Spec:** `docs/superpowers/specs/2026-03-12-performance-regression-baseline-design.md`

---

## File Map

| Action | File | Responsibility |
|--------|------|---------------|
| Create | `tests/k6/scenarios/deploy-smoke.js` | K6 script: hit 3 live endpoints, output summary JSON |
| Create | `tests/k6/utils/deploy-baseline-compare.sh` | Compare K6 summary vs previous baseline, output markdown |
| Modify | `.github/workflows/deploy-staging.yml` | Add snapshot-baseline job, K6 steps in validate |
| Modify | `.github/workflows/deploy-production.yml` | Same changes, different URLs/thresholds |

---

## Chunk 1: K6 Deploy Smoke Script & Baseline Compare

### Task 1: Create `tests/k6/scenarios/deploy-smoke.js`

**Files:**
- Create: `tests/k6/scenarios/deploy-smoke.js`

**Reference:** Existing K6 scenarios at `tests/k6/scenarios/admin-polling.js` for import patterns. Existing shared config at `tests/k6/utils/shared-config.js` — do NOT import it (deploy-smoke is self-contained, no auth needed).

- [ ] **Step 1: Write the K6 deploy smoke script**

```javascript
/**
 * Issue #184: Deploy Smoke Test — Performance Regression Baseline
 *
 * Lightweight K6 script for post-deploy validation against live URLs.
 * Runs against public endpoints only (no auth required).
 * Used by deploy-staging.yml and deploy-production.yml.
 *
 * Environment variables:
 *   BASE_URL      - Web frontend URL (default: https://meepleai.app)
 *   API_BASE_URL  - API URL (default: BASE_URL). Use separate value
 *                   for production where API is on a subdomain.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// URL configuration
const BASE_URL = __ENV.BASE_URL || 'https://meepleai.app';
const API_BASE_URL = __ENV.API_BASE_URL || BASE_URL;

// Custom metrics for readability in summary
const healthLatency = new Trend('health_latency', true);
const webLatency = new Trend('web_latency', true);
const catalogLatency = new Trend('catalog_latency', true);
const smokeErrors = new Rate('smoke_errors');

// Endpoints to test
const ENDPOINTS = [
  { name: 'health',  url: `${API_BASE_URL}/health`,                                    tag: 'health' },
  { name: 'web',     url: `${BASE_URL}/`,                                               tag: 'web' },
  { name: 'catalog', url: `${API_BASE_URL}/api/v1/shared-game-catalog/games?limit=1`,   tag: 'catalog' },
];

// Relaxed thresholds for external network requests
export const options = {
  scenarios: {
    deploy_smoke: {
      executor: 'per-vu-iterations',
      vus: 3,
      iterations: 10,
      maxDuration: '60s',
    },
  },
  thresholds: {
    'http_req_duration': ['p(95)<2000', 'p(99)<3000'],
    'http_req_failed': ['rate<0.1'],
  },
};

export default function () {
  for (const ep of ENDPOINTS) {
    const res = http.get(ep.url, {
      tags: { endpoint: ep.tag },
      timeout: '10s',
    });

    const ok = check(res, {
      [`${ep.name}: status OK`]: (r) => r.status >= 200 && r.status < 400,
      [`${ep.name}: response time < 5s`]: (r) => r.timings.duration < 5000,
    });

    // Track per-endpoint latency
    const latency = res.timings.duration;
    if (ep.tag === 'health') healthLatency.add(latency);
    else if (ep.tag === 'web') webLatency.add(latency);
    else if (ep.tag === 'catalog') catalogLatency.add(latency);

    smokeErrors.add(!ok);

    sleep(0.5);
  }
}
```

Save to `tests/k6/scenarios/deploy-smoke.js`.

- [ ] **Step 2: Verify K6 script syntax locally (if K6 installed)**

Run: `k6 inspect tests/k6/scenarios/deploy-smoke.js` (or skip if K6 not installed locally — CI will validate)

- [ ] **Step 3: Commit**

```bash
git add tests/k6/scenarios/deploy-smoke.js
git commit -m "feat(k6): add deploy smoke test script (#184)"
```

---

### Task 2: Create `tests/k6/utils/deploy-baseline-compare.sh`

**Files:**
- Create: `tests/k6/utils/deploy-baseline-compare.sh`

**Reference:** Existing `tests/k6/utils/baseline-compare.sh` for bash patterns (float comparison with awk, report generation). Do NOT modify it.

- [ ] **Step 1: Write the deploy baseline comparison script**

```bash
#!/bin/bash
# Issue #184: Deploy Performance Regression Comparison
#
# Compares current K6 deploy-smoke results against previous deployment baseline.
# Uses aggregate http_req_duration p95/p99 and http_req_failed rate.
#
# Usage:
#   ./deploy-baseline-compare.sh <current-k6-summary.json> [previous-baseline.json]
#
# Environment variables:
#   PERF_REGRESSION_MODE     - "warn" (default) or "fail"
#   REGRESSION_THRESHOLD_PCT - percentage increase to flag (default: 20)
#
# Exit codes:
#   0 = OK (or warn mode with regression)
#   1 = Regression detected in fail mode

set -euo pipefail

CURRENT_SUMMARY="${1:?Usage: deploy-baseline-compare.sh <current.json> [previous.json]}"
PREVIOUS_BASELINE="${2:-}"
MODE="${PERF_REGRESSION_MODE:-warn}"
THRESHOLD="${REGRESSION_THRESHOLD_PCT:-20}"

# Portable float comparison using awk
float_gt() { awk "BEGIN { exit !($1 > $2) }"; }
float_pct_change() { awk "BEGIN { printf \"%.1f\", (($1 - $2) / $2) * 100 }"; }
float_fmt() { awk "BEGIN { printf \"%.1f\", $1 }"; }

# Extract metrics from K6 summary JSON
extract_metric() {
  local file="$1" path="$2"
  jq -r "$path // empty" "$file" 2>/dev/null || echo ""
}

# ============================================
# EXTRACT CURRENT METRICS
# ============================================

if [ ! -f "$CURRENT_SUMMARY" ]; then
  echo "::error::K6 summary file not found: $CURRENT_SUMMARY"
  exit 1
fi

CURR_P95=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values["p(95)"]')
CURR_P99=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values["p(99)"]')
CURR_AVG=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_duration.values.avg')
CURR_ERR=$(extract_metric "$CURRENT_SUMMARY" '.metrics.http_req_failed.values.rate')

if [ -z "$CURR_P95" ]; then
  echo "::error::Could not extract p95 from K6 summary"
  exit 1
fi

CURR_P95_FMT=$(float_fmt "$CURR_P95")
CURR_P99_FMT=$(float_fmt "$CURR_P99")
CURR_AVG_FMT=$(float_fmt "$CURR_AVG")
CURR_ERR_PCT=$(awk "BEGIN { printf \"%.2f\", ${CURR_ERR:-0} * 100 }")

# ============================================
# BUILD REPORT
# ============================================

REPORT=""
add() { REPORT="${REPORT}${1}\n"; }

add "## ⚡ Deploy Performance Baseline"
add ""
add "| Metric | Current | Previous | Change | Status |"
add "|--------|---------|----------|--------|--------|"

REGRESSION_FOUND=0

if [ -z "$PREVIOUS_BASELINE" ] || [ ! -f "$PREVIOUS_BASELINE" ] || [ ! -s "$PREVIOUS_BASELINE" ]; then
  # First deploy — no comparison, just record
  add "| p95 | ${CURR_P95_FMT}ms | _first deploy_ | — | 📊 Baseline |"
  add "| p99 | ${CURR_P99_FMT}ms | _first deploy_ | — | 📊 Baseline |"
  add "| avg | ${CURR_AVG_FMT}ms | _first deploy_ | — | 📊 Baseline |"
  add "| error rate | ${CURR_ERR_PCT}% | _first deploy_ | — | 📊 Baseline |"
  add ""
  add "**First deployment — baseline recorded for future comparisons.**"
else
  # Compare against previous
  PREV_P95=$(jq -r '.aggregate.p95_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_P99=$(jq -r '.aggregate.p99_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_AVG=$(jq -r '.aggregate.avg_ms // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")
  PREV_ERR=$(jq -r '.aggregate.error_rate // empty' "$PREVIOUS_BASELINE" 2>/dev/null || echo "")

  compare_metric() {
    local name="$1" curr="$2" prev="$3" unit="$4"
    if [ -z "$prev" ] || [ "$prev" = "null" ] || [ "$prev" = "0" ]; then
      add "| $name | ${curr}${unit} | — | — | 📊 New |"
      return
    fi

    local change
    change=$(float_pct_change "$curr" "$prev")
    local status="✅ OK"

    if float_gt "$change" "$THRESHOLD"; then
      status="⚠️ Regression (+${change}%)"
      REGRESSION_FOUND=1
    elif float_gt "$change" "0"; then
      status="📈 +${change}%"
    else
      status="✅ ${change}%"
    fi

    add "| $name | ${curr}${unit} | ${prev}${unit} | ${change}% | $status |"
  }

  compare_metric "p95" "$CURR_P95_FMT" "$PREV_P95" "ms"
  compare_metric "p99" "$CURR_P99_FMT" "$PREV_P99" "ms"
  compare_metric "avg" "$CURR_AVG_FMT" "$PREV_AVG" "ms"
  compare_metric "error rate" "$CURR_ERR_PCT" "$(awk "BEGIN { printf \"%.2f\", ${PREV_ERR:-0} * 100 }")" "%"
fi

add ""
add "---"
add ""
add "**Mode**: \`$MODE\` | **Regression threshold**: ${THRESHOLD}%"

if [ "$REGRESSION_FOUND" -eq 1 ]; then
  add ""
  if [ "$MODE" = "fail" ]; then
    add "**❌ REGRESSION DETECTED** — deploy pipeline will fail."
  else
    add "**⚠️ REGRESSION DETECTED** — warning only (mode=warn)."
  fi
fi

# Output report
echo -e "$REPORT"

# Write to GitHub step summary if available
if [ -n "${GITHUB_STEP_SUMMARY:-}" ]; then
  echo -e "$REPORT" >> "$GITHUB_STEP_SUMMARY"
fi

# Output current metrics as JSON for the update step to consume
METRICS_JSON=$(cat <<METRICSEOF
{
  "measured_at": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "aggregate": {
    "avg_ms": $CURR_AVG_FMT,
    "p95_ms": $CURR_P95_FMT,
    "p99_ms": $CURR_P99_FMT,
    "error_rate": ${CURR_ERR:-0}
  }
}
METRICSEOF
)
echo "$METRICS_JSON" > "${CURRENT_SUMMARY%.json}-metrics.json"
echo "Metrics JSON saved to: ${CURRENT_SUMMARY%.json}-metrics.json"

# Exit based on mode
if [ "$REGRESSION_FOUND" -eq 1 ] && [ "$MODE" = "fail" ]; then
  echo "::error::Performance regression detected (p95/p99 increased >${THRESHOLD}%)"
  exit 1
fi

if [ "$REGRESSION_FOUND" -eq 1 ]; then
  echo "::warning::Performance regression detected (p95/p99 increased >${THRESHOLD}%) — mode=$MODE, continuing"
fi

exit 0
```

Save to `tests/k6/utils/deploy-baseline-compare.sh`.

- [ ] **Step 2: Make the script executable**

```bash
chmod +x tests/k6/utils/deploy-baseline-compare.sh
```

- [ ] **Step 3: Commit**

```bash
git add tests/k6/utils/deploy-baseline-compare.sh
git commit -m "feat(k6): add deploy baseline comparison script (#184)"
```

---

## Chunk 2: Staging Workflow Changes

### Task 3: Add `snapshot-baseline` job to `deploy-staging.yml`

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

**Context:**
- The `deploy` job (line 299) writes `DEPLOYMENT.json` without a `performance` field.
- The new `snapshot-baseline` job must run before `deploy` to capture previous metrics.
- It uses SSH to read the file, same secrets as the deploy job (`STAGING_HOST`, `STAGING_USER`, `STAGING_SSH_KEY`).
- The `deploy` job currently depends on `[build, migrate-db]`. The `snapshot-baseline` must run in parallel with `migrate-db` (both after `build`), then `deploy` needs both.

- [ ] **Step 1: Add `perf_regression_mode` workflow input**

In `deploy-staging.yml`, add to the `workflow_dispatch.inputs` section (after line 17):

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

- [ ] **Step 2: Add `snapshot-baseline` job**

Insert after the `migrate-db` job (after line 297) and before the `deploy` job (line 299):

```yaml
  # Snapshot previous performance baseline before deploy overwrites DEPLOYMENT.json
  # Issue #184: Performance regression baseline
  snapshot-baseline:
    name: Snapshot Performance Baseline
    runs-on: ubuntu-latest
    needs: [build]
    outputs:
      previous_baseline: ${{ steps.read-baseline.outputs.baseline }}
    steps:
      - name: Read Previous Baseline via SSH
        id: read-baseline
        if: vars.DEPLOY_METHOD == 'ssh'
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.STAGING_HOST }}
          username: ${{ secrets.STAGING_USER }}
          key: ${{ secrets.STAGING_SSH_KEY }}
          script: |
            DEPLOY_FILE="/opt/meepleai/repo/infra/DEPLOYMENT.json"
            if [ -f "$DEPLOY_FILE" ]; then
              PERF=$(jq -r '.performance // empty' "$DEPLOY_FILE" 2>/dev/null || echo "")
              if [ -n "$PERF" ] && [ "$PERF" != "null" ]; then
                echo "$PERF"
              else
                echo ""
              fi
            else
              echo ""
            fi

      - name: Capture Baseline Output
        id: capture
        run: |
          # The SSH action output is in the step's stdout
          # For appleboy/ssh-action, we need to use a different approach:
          # Pass through environment or use the script output
          echo "baseline=${{ steps.read-baseline.outputs.stdout || '' }}" >> $GITHUB_OUTPUT
```

**Note on appleboy/ssh-action:** This action does not natively support capturing script stdout as a step output. The implementation must handle this — options include: (a) write to a file on the server and fetch it, (b) use a raw SSH command via bash instead of the action. The implementer should use the simplest approach that works. A practical approach:

```yaml
      - name: Read Previous Baseline via SSH
        id: read-baseline
        run: |
          BASELINE=$(ssh -o StrictHostKeyChecking=no \
            -i <(echo "${{ secrets.STAGING_SSH_KEY }}") \
            ${{ secrets.STAGING_USER }}@${{ secrets.STAGING_HOST }} \
            'jq -c ".performance // {}" /opt/meepleai/repo/infra/DEPLOYMENT.json 2>/dev/null || echo "{}"')
          echo "baseline=$BASELINE" >> $GITHUB_OUTPUT
```

However, this requires the SSH key to be available inline. The recommended approach is to use `appleboy/ssh-action` to write the baseline to a known path, then fetch with `scp` or another step. The implementer should choose whichever pattern works cleanly.

- [ ] **Step 3: Update `deploy` job dependencies**

Change the `deploy` job's `needs` from:
```yaml
    needs: [build, migrate-db]
```
to:
```yaml
    needs: [build, migrate-db, snapshot-baseline]
```

And update the `if` condition to also allow `snapshot-baseline` to be skipped:
```yaml
    if: always() && needs.build.result == 'success' && (needs.migrate-db.result == 'success' || needs.migrate-db.result == 'skipped') && (needs.snapshot-baseline.result == 'success' || needs.snapshot-baseline.result == 'skipped')
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "feat(cicd): add snapshot-baseline job to staging deploy (#184)"
```

---

### Task 4: Replace curl Performance Baseline with K6 in staging `validate` job

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`

**Context:**
- The `validate` job (line 387) currently has no `actions/checkout@v4` — must add one.
- The "Performance Baseline" step (lines 502-531) uses curl — replace with K6.
- The `validate` job needs to depend on `snapshot-baseline` for the previous metrics.

- [ ] **Step 1: Update `validate` job dependencies and add checkout**

Change the `validate` job's `needs` from:
```yaml
    needs: [deploy]
```
to:
```yaml
    needs: [deploy, snapshot-baseline]
```

Add `actions/checkout@v4` as the first step in the `validate` job (before "Deep Health Check"):

```yaml
      - name: Checkout
        uses: actions/checkout@v4
```

- [ ] **Step 2: Replace "Performance Baseline" step with K6 steps**

Remove the existing "Performance Baseline" step (lines 502-531). Replace with these steps (insert after the "Smoke Tests" step):

```yaml
      - name: Install K6
        run: |
          ARCH=$(dpkg --print-architecture)
          if [ "$ARCH" = "arm64" ] || [ "$ARCH" = "aarch64" ]; then
            K6_VERSION=$(curl -s https://api.github.com/repos/grafana/k6/releases/latest | grep tag_name | cut -d'"' -f4)
            curl -sL "https://github.com/grafana/k6/releases/download/${K6_VERSION}/k6-${K6_VERSION}-linux-arm64.tar.gz" | tar xz
            sudo mv k6-${K6_VERSION}-linux-arm64/k6 /usr/local/bin/k6
          else
            sudo gpg -k
            sudo gpg --no-default-keyring --keyring /usr/share/keyrings/k6-archive-keyring.gpg --keyserver hkp://keyserver.ubuntu.com:80 --recv-keys C5AD17C747E3415A3642D57D77C6C491D6AC1D69
            echo "deb [signed-by=/usr/share/keyrings/k6-archive-keyring.gpg] https://dl.k6.io/deb stable main" | sudo tee /etc/apt/sources.list.d/k6.list
            sudo apt-get update
            sudo apt-get install -y k6
          fi

      - name: Run K6 Deploy Smoke Test
        run: |
          mkdir -p tests/k6/reports/deploy
          k6 run --env BASE_URL=https://meepleai.app \
                 --summary-export=tests/k6/reports/deploy/deploy-smoke-summary.json \
                 tests/k6/scenarios/deploy-smoke.js
          echo "✅ K6 deploy smoke test completed"

      - name: Write Previous Baseline
        run: |
          BASELINE='${{ needs.snapshot-baseline.outputs.previous_baseline }}'
          if [ -n "$BASELINE" ] && [ "$BASELINE" != "{}" ]; then
            echo "$BASELINE" > /tmp/previous-baseline.json
            echo "📊 Previous baseline loaded"
          else
            echo "{}" > /tmp/previous-baseline.json
            echo "📊 No previous baseline (first deploy)"
          fi

      - name: Compare Performance Baseline
        env:
          PERF_REGRESSION_MODE: ${{ inputs.perf_regression_mode || 'warn' }}
          REGRESSION_THRESHOLD_PCT: '20'
        run: |
          chmod +x tests/k6/utils/deploy-baseline-compare.sh
          tests/k6/utils/deploy-baseline-compare.sh \
            tests/k6/reports/deploy/deploy-smoke-summary.json \
            /tmp/previous-baseline.json

      - name: Update Server Baseline
        if: always() && vars.DEPLOY_METHOD == 'ssh'
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.STAGING_HOST }}
          username: ${{ secrets.STAGING_USER }}
          key: ${{ secrets.STAGING_SSH_KEY }}
          envs: METRICS_JSON
          script: |
            DEPLOY_FILE="/opt/meepleai/repo/infra/DEPLOYMENT.json"
            if [ -f "$DEPLOY_FILE" ] && command -v jq &>/dev/null; then
              # Read metrics from the compare step output
              METRICS_FILE="tests/k6/reports/deploy/deploy-smoke-summary-metrics.json"
              if [ -f "$METRICS_FILE" ]; then
                jq --argjson perf "$(cat $METRICS_FILE)" '.performance = $perf' "$DEPLOY_FILE" > "${DEPLOY_FILE}.tmp" \
                  && mv "${DEPLOY_FILE}.tmp" "$DEPLOY_FILE"
                echo "✅ Performance baseline updated in DEPLOYMENT.json"
              else
                echo "⚠️ Metrics file not found — skipping baseline update"
              fi
            else
              echo "⚠️ DEPLOYMENT.json or jq not available — skipping baseline update"
            fi
```

**Important implementation note:** The "Update Server Baseline" step cannot directly access runner files via SSH action. The metrics JSON must be passed as an environment variable or the step must use raw SSH/SCP from the runner. The implementer should:
1. Read the metrics JSON file content into a variable in a prior step
2. Pass it to the SSH action via `envs` parameter
3. Write it on the server using `echo "$METRICS_JSON" | jq ...`

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "feat(cicd): replace curl perf check with K6 smoke in staging (#184)"
```

---

## Chunk 3: Production Workflow Changes

### Task 5: Add `snapshot-baseline` job to `deploy-production.yml`

**Files:**
- Modify: `.github/workflows/deploy-production.yml`

**Context:**
- Production has a different job chain: `staging-check` → `pre-production-check` → `build` → `approve` → `migrate-db` → `deploy` → `validate`
- The `snapshot-baseline` job should run after `build` and in parallel with `approve` and `migrate-db`
- Production uses different SSH secrets: `PRODUCTION_HOST`, `PRODUCTION_USER`, `PRODUCTION_SSH_KEY`
- DEPLOYMENT.json path on production: `/opt/meepleai/DEPLOYMENT.json` (NOT `/opt/meepleai/repo/infra/DEPLOYMENT.json`)

- [ ] **Step 1: Add `perf_regression_mode` workflow input**

In `deploy-production.yml`, add to the `workflow_dispatch.inputs` section (after `skip_staging_check`):

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

- [ ] **Step 2: Add `snapshot-baseline` job**

Insert after the `approve` job and before `migrate-db`. Same pattern as staging but with production secrets and path:

```yaml
  # Snapshot previous performance baseline before deploy overwrites DEPLOYMENT.json
  # Issue #184: Performance regression baseline
  snapshot-baseline:
    name: Snapshot Performance Baseline
    runs-on: ubuntu-latest
    needs: [build]
    outputs:
      previous_baseline: ${{ steps.read-baseline.outputs.baseline }}
    steps:
      - name: Read Previous Baseline via SSH
        id: read-baseline
        run: |
          # Same SSH approach as staging but with production paths
          BASELINE=$(ssh -o StrictHostKeyChecking=no \
            -i <(echo "${{ secrets.PRODUCTION_SSH_KEY }}") \
            ${{ secrets.PRODUCTION_USER }}@${{ secrets.PRODUCTION_HOST }} \
            'jq -c ".performance // {}" /opt/meepleai/DEPLOYMENT.json 2>/dev/null || echo "{}"')
          echo "baseline=$BASELINE" >> $GITHUB_OUTPUT
```

- [ ] **Step 3: Update `deploy` job dependencies**

Add `snapshot-baseline` to the `deploy` job's `needs` array and update its `if` condition accordingly.

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/deploy-production.yml
git commit -m "feat(cicd): add snapshot-baseline job to production deploy (#184)"
```

---

### Task 6: Replace curl Performance Baseline with K6 in production `validate` job

**Files:**
- Modify: `.github/workflows/deploy-production.yml`

**Context:**
- Production `validate` job already has `actions/checkout@v4` (line 363).
- The "Performance Baseline" step (lines 468-505) uses curl — replace with K6.
- Production uses **two hostnames**: `https://meepleai.com` (web) and `https://api.meepleai.com` (API).
- DEPLOYMENT.json path: `/opt/meepleai/DEPLOYMENT.json`
- Default threshold: 2.0s

- [ ] **Step 1: Update `validate` job dependencies**

Add `snapshot-baseline` to `validate` needs:
```yaml
    needs: [deploy, snapshot-baseline]
```

- [ ] **Step 2: Replace "Performance Baseline" step with K6 steps**

Remove lines 468-505. Replace with same K6 steps as staging but with production URLs:

```yaml
      - name: Install K6
        run: |
          # Same K6 install logic as staging (copy from Task 4)
          ...

      - name: Run K6 Deploy Smoke Test
        run: |
          mkdir -p tests/k6/reports/deploy
          k6 run --env BASE_URL=https://meepleai.com \
                 --env API_BASE_URL=https://api.meepleai.com \
                 --summary-export=tests/k6/reports/deploy/deploy-smoke-summary.json \
                 tests/k6/scenarios/deploy-smoke.js
          echo "✅ K6 deploy smoke test completed"

      - name: Write Previous Baseline
        run: |
          # Same as staging (copy from Task 4)
          ...

      - name: Compare Performance Baseline
        env:
          PERF_REGRESSION_MODE: ${{ inputs.perf_regression_mode || 'warn' }}
          REGRESSION_THRESHOLD_PCT: '20'
        run: |
          chmod +x tests/k6/utils/deploy-baseline-compare.sh
          tests/k6/utils/deploy-baseline-compare.sh \
            tests/k6/reports/deploy/deploy-smoke-summary.json \
            /tmp/previous-baseline.json

      - name: Update Server Baseline
        if: always() && vars.DEPLOY_METHOD == 'ssh'
        uses: appleboy/ssh-action@v1.0.3
        with:
          host: ${{ secrets.PRODUCTION_HOST }}
          username: ${{ secrets.PRODUCTION_USER }}
          key: ${{ secrets.PRODUCTION_SSH_KEY }}
          script: |
            DEPLOY_FILE="/opt/meepleai/DEPLOYMENT.json"
            # Same jq merge logic as staging (copy from Task 4)
            ...
```

- [ ] **Step 3: Commit**

```bash
git add .github/workflows/deploy-production.yml
git commit -m "feat(cicd): replace curl perf check with K6 smoke in production (#184)"
```

---

## Chunk 4: Final Validation & Cleanup

### Task 7: Validate workflow YAML syntax

**Files:**
- Validate: `.github/workflows/deploy-staging.yml`
- Validate: `.github/workflows/deploy-production.yml`

- [ ] **Step 1: Validate YAML syntax**

```bash
# Check YAML is valid
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml'))" && echo "Staging: valid YAML"
python3 -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-production.yml'))" && echo "Production: valid YAML"
```

- [ ] **Step 2: Verify job dependency chain is correct**

Staging chain:
```
detect-changes → pre-deploy-check → build → snapshot-baseline ─┐
                                         → migrate-db ────────┤
                                                               → deploy → validate → e2e-staging → notify
```

Production chain:
```
staging-check ───────┐
pre-production-check → build → approve ──────────┐
                            → snapshot-baseline ──┤
                                                  → migrate-db → deploy → validate → rollback/notify
```

- [ ] **Step 3: Verify no references to removed curl steps remain**

```bash
# Should NOT find the old curl-based "Performance Baseline" pattern in deploy workflows
grep -n "for ENDPOINT in" .github/workflows/deploy-staging.yml .github/workflows/deploy-production.yml || echo "✅ Old curl pattern removed"
grep -n "bc -l" .github/workflows/deploy-staging.yml .github/workflows/deploy-production.yml || echo "✅ Old bc pattern removed"
```

- [ ] **Step 4: Final commit with all cleanup**

```bash
git add -A
git commit -m "chore(cicd): validate and clean up perf baseline implementation (#184)"
```

---

### Task 8: Create PR and update issue

- [ ] **Step 1: Push branch**

```bash
git push -u origin feature/issue-184-perf-regression-baseline
```

- [ ] **Step 2: Create PR to `main-dev`**

```bash
gh pr create \
  --base main-dev \
  --title "feat(cicd): performance regression baseline in deploy pipeline (#184)" \
  --body "$(cat <<'EOF'
## Summary
- Add K6 smoke tests to staging and production deploy pipelines
- Cross-deployment trend comparison via DEPLOYMENT.json on server
- Configurable warn/fail mode for regression detection
- New `snapshot-baseline` job captures previous metrics before deploy

## Files Changed
- **New**: `tests/k6/scenarios/deploy-smoke.js` — K6 smoke script for live endpoints
- **New**: `tests/k6/utils/deploy-baseline-compare.sh` — Trend comparison script
- **Modified**: `.github/workflows/deploy-staging.yml` — K6 integration + snapshot job
- **Modified**: `.github/workflows/deploy-production.yml` — Same changes for production

## Test plan
- [ ] Trigger staging deploy manually with `perf_regression_mode: warn`
- [ ] Verify K6 smoke test runs and results appear in GitHub summary
- [ ] Verify DEPLOYMENT.json on server gets `performance` field after deploy
- [ ] Trigger second deploy and verify trend comparison table appears
- [ ] Test `perf_regression_mode: fail` with manual dispatch

Closes #184

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: Update issue #184 on GitHub**

```bash
gh issue edit 184 --repo MeepleAi-Dev/meepleai-monorepo --add-label "in-progress"
```
