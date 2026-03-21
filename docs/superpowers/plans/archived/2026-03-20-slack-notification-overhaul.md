# Slack Notification Overhaul — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Slack notification bug (CI fails but Slack says OK), reduce notification noise from ~150 to ~5 messages/day, add branch-conditional CI behavior, and add deploy preview PR comments.

**Architecture:** All 17 workflows use `notify-slack.yml` (reusable). The bug is in the `contains(toJSON(needs), '"result":"failure"')` expression used in all callers — `toJSON()` produces pretty-printed JSON with spaces (`"result": "failure"`) but the pattern searches without spaces. Fix all callers to use `contains(join(needs.*.result, ','), 'failure')`. Then remove Slack from 12 low-value workflows and add failures-only gating + branch-conditional logic to CI.

**Tech Stack:** GitHub Actions YAML, Slack Block Kit webhooks, `gh` CLI

---

## File Structure

| File | Action | Responsibility |
|------|--------|---------------|
| `.github/workflows/ci.yml` | Modify | Fix bug, branch-conditional Slack, deploy preview |
| `.github/workflows/deploy-staging.yml` | Modify | Fix bug in notify-end |
| `.github/workflows/rollback.yml` | Modify | Fix bug in notify-end |
| `.github/workflows/security-scan.yml` | Modify | Fix bug, failures-only |
| `.github/workflows/backend-e2e-tests.yml` | Modify | Fix bug, failures-only |
| `.github/workflows/runner-health-check.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/runner-maintenance.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/auto-branch-policy.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/auto-dependabot.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/auto-validate.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/generate-diagrams.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/generate-operations-pdf.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/test-e2e.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/test-visual.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/test-performance.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/security-pentest.yml` | Modify | Remove ALL Slack notifications |
| `.github/workflows/security-review.yml` | Modify | Remove ALL Slack notifications |

### Notification Tier Summary

| Tier | Workflows | Slack Behavior |
|------|-----------|---------------|
| **CRITICAL** | deploy-staging, rollback | Start + End (both channels) |
| **IMPORTANT** | ci (main-staging/main PRs only), backend-e2e, security-scan | Failures only |
| **SILENT** | All other 12 workflows | No Slack at all |

---

### Task 1: Fix the Bug — `contains(toJSON())` in the 5 Remaining Workflows

**Files** (only the 5 workflows that keep Slack after Task 2):
- Modify: `.github/workflows/ci.yml:649`
- Modify: `.github/workflows/deploy-staging.yml:697`
- Modify: `.github/workflows/rollback.yml` (notify-end)
- Modify: `.github/workflows/security-scan.yml:170`
- Modify: `.github/workflows/backend-e2e-tests.yml:177`

> **Note**: Task 2 (remove Slack from 12 workflows) should be done FIRST. That removes the broken pattern from 12 files. This task only fixes the remaining 5.

The root cause: `toJSON(needs)` may produce `"result": "failure"` (with space) but the `contains()` searches for `"result":"failure"` (no space). Fix by using `join(needs.*.result, ',')` which produces a flat comma-separated string like `success,failure,skipped`.

- [ ] **Step 1: Replace the broken pattern in ci.yml (line 649)**

Replace:
```yaml
event: ${{ contains(toJSON(needs), '"result":"failure"') && 'failed' || contains(toJSON(needs), '"result":"cancelled"') && 'cancelled' || 'completed' }}
```
With:
```yaml
event: ${{ contains(join(needs.*.result, ','), 'failure') && 'failed' || contains(join(needs.*.result, ','), 'cancelled') && 'cancelled' || 'completed' }}
```

- [ ] **Step 2: Apply same fix to deploy-staging.yml (line 697)**

Same replacement as Step 1.

- [ ] **Step 3: Apply same fix to rollback.yml notify-end**

Same replacement.

- [ ] **Step 4: Apply same fix to security-scan.yml and backend-e2e-tests.yml**

Same replacement in both files.

- [ ] **Step 5: Verify no toJSON(needs) pattern remains**

Run: `grep -r "toJSON(needs)" .github/workflows/*.yml`
Expected: **Zero matches** (note: `*.yml` excludes `deploy-production.yml.disabled`)

- [ ] **Step 6: Verify new pattern present in remaining 5 workflows**

Run: `grep -c "join(needs" .github/workflows/*.yml | grep -v ":0"`
Expected: 5 files with matches (ci, deploy-staging, rollback, security-scan, backend-e2e-tests)

- [ ] **Step 7: Commit**

```bash
git add .github/workflows/
git commit -m "fix(workflows): fix Slack status detection in 5 remaining workflows

Replace contains(toJSON(needs)) with contains(join(needs.*.result))
in ci, deploy-staging, rollback, security-scan, backend-e2e-tests.
The toJSON pattern fails because pretty-printed JSON may have spaces
that break substring matching."
```

---

### Task 2: Remove Slack from Tier 3 Workflows (12 Silent Workflows)

**Files to modify** (delete `notify-start` and `notify-end` jobs entirely):
- `.github/workflows/runner-health-check.yml`
- `.github/workflows/runner-maintenance.yml`
- `.github/workflows/auto-branch-policy.yml`
- `.github/workflows/auto-dependabot.yml`
- `.github/workflows/auto-validate.yml`
- `.github/workflows/generate-diagrams.yml`
- `.github/workflows/generate-operations-pdf.yml`
- `.github/workflows/test-e2e.yml`
- `.github/workflows/test-visual.yml`
- `.github/workflows/test-performance.yml`
- `.github/workflows/security-pentest.yml`
- `.github/workflows/security-review.yml`

For each file:
1. Delete the entire `notify-start:` job block (all lines including `uses:`, `with:`, `secrets:`)
2. Delete the entire `notify-end:` job block
3. Remove `notify-start` from any other job's `needs:` array if referenced
4. Remove `secrets:` pass-through for `SLACK_GITNOTIFY_WEBHOOK_URL` and `SLACK_CRITICAL_WEBHOOK_URL`
5. Remove `actions: read` from `permissions:` ONLY if no other job in the workflow needs it (verify by checking if any step uses `github.token` for Actions API calls — e.g., `gh api repos/.../actions/...`)

- [ ] **Step 1: Remove Slack from runner-health-check.yml**

Delete `notify-start` job (lines 20-34) and `notify-end` job (lines 178-194). The `runner-health` and `alert-on-failure` jobs don't depend on `notify-start`. Also remove the standalone `actions: read` permission line (line 18) since notify-slack was the only consumer.

- [ ] **Step 2: Remove Slack from runner-maintenance.yml**

Delete `notify-start` job and `notify-end` job. Check if any other job has `needs: [notify-start]` — if so, remove that dependency.

- [ ] **Step 3: Remove Slack from auto-branch-policy.yml**

Delete `notify-start` job (lines 11-25) and `notify-end` job (lines 100-114).

- [ ] **Step 4: Remove Slack from auto-dependabot.yml**

Delete `notify-start` job (lines 18-32) and `notify-end` job (lines 163-177). Keep `actions: read` permission line ONLY if needed by other jobs.

- [ ] **Step 5: Remove Slack from auto-validate.yml**

Delete `notify-start` job (lines 19-33) and `notify-end` job (lines 50-64).

- [ ] **Step 6: Remove Slack from generate-diagrams.yml**

Delete `notify-start` job (lines 11-25) and `notify-end` job (lines 142-156).

- [ ] **Step 7: Remove Slack from generate-operations-pdf.yml**

Delete `notify-start` job (lines 12-26) and `notify-end` job (lines 70-84).

- [ ] **Step 8: Remove Slack from test-e2e.yml**

Delete `notify-start` job and `notify-end` job.

- [ ] **Step 9: Remove Slack from test-visual.yml**

Delete `notify-start` job and `notify-end` job.

- [ ] **Step 10: Remove Slack from test-performance.yml**

Delete `notify-start` job and `notify-end` job.

- [ ] **Step 11: Remove Slack from security-pentest.yml**

Delete `notify-start` job and `notify-end` job.

- [ ] **Step 12: Remove Slack from security-review.yml**

Delete `notify-start` job and `notify-end` job.

- [ ] **Step 13: Verify only 5 workflows still reference notify-slack**

Run: `grep -rl "notify-slack" .github/workflows/ | grep -v notify-slack.yml | grep -v README`
Expected: ci.yml, deploy-staging.yml, rollback.yml, security-scan.yml, backend-e2e-tests.yml (5 files)

- [ ] **Step 14: Validate YAML for all modified files**

Run: `for f in .github/workflows/*.yml; do python3 -c "import yaml; yaml.safe_load(open('$f'))" && echo "OK: $f" || echo "FAIL: $f"; done`
Expected: All OK

- [ ] **Step 15: Commit**

```bash
git add .github/workflows/
git commit -m "chore(workflows): remove Slack notifications from 12 low-value workflows

Remove notify-start and notify-end jobs from:
- runner-health-check (was ~96 messages/day alone!)
- runner-maintenance, auto-branch-policy, auto-dependabot
- auto-validate, generate-diagrams, generate-operations-pdf
- test-e2e, test-visual, test-performance
- security-pentest, security-review

These workflows still report status in GitHub Actions UI.
Slack reserved for: deploy-staging, rollback, CI failures,
backend-e2e failures, security-scan failures."
```

---

### Task 3: Failures-Only for Tier 2 Workflows

**Files:**
- Modify: `.github/workflows/ci.yml`
- Modify: `.github/workflows/backend-e2e-tests.yml`
- Modify: `.github/workflows/security-scan.yml`

- [ ] **Step 1: CI — remove notify-start job**

Delete the entire `notify-start` job block in `ci.yml` (lines 22-38).

- [ ] **Step 2: CI — gate notify-end to failures only + branch restriction**

Change `notify-end` `if:` from:
```yaml
if: always() && github.event_name != 'workflow_call'
```
To:
```yaml
if: >-
  always()
  && github.event_name != 'workflow_call'
  && contains(join(needs.*.result, ','), 'failure')
  && (
    github.event_name == 'workflow_dispatch'
    || github.base_ref == 'main-staging'
    || github.base_ref == 'main'
  )
```

**Important**: `github.base_ref` is only populated for `pull_request` events. Without the `workflow_dispatch` fallback, manual dispatch failures would be silently swallowed.

And hardcode the event since we know it's always a failure:
```yaml
event: failed
```

This means:
- PRs to `main-dev`, `frontend-dev`, `backend-dev` → NO Slack (still run CI, visible in GitHub UI)
- PRs to `main-staging` or `main` → Slack ONLY on failure
- `workflow_dispatch` (manual) → Slack on failure (always notified)

- [ ] **Step 3: Backend E2E — remove notify-start, failures-only notify-end**

In `backend-e2e-tests.yml`:
1. Delete `notify-start` job (lines 24-37+)
2. Change `notify-end` `if:` to:
```yaml
if: always() && contains(join(needs.*.result, ','), 'failure')
```
3. Set `event: failed`

- [ ] **Step 4: Security Scan — remove notify-start, failures-only notify-end**

In `security-scan.yml`:
1. Delete `notify-start` job (lines 20-33+)
2. Change `notify-end` `if:` to:
```yaml
if: always() && contains(join(needs.*.result, ','), 'failure')
```
3. Set `event: failed`
4. Keep `is_critical: true` (security failures should hit critical channel)

- [ ] **Step 5: Verify only 2 workflows still have notify-start**

Run: `grep -rl "notify-start:" .github/workflows/ | grep -v notify-slack.yml`
Expected: Only `deploy-staging.yml` and `rollback.yml`

- [ ] **Step 6: Commit**

```bash
git add .github/workflows/ci.yml .github/workflows/backend-e2e-tests.yml .github/workflows/security-scan.yml
git commit -m "feat(workflows): Slack failures-only for CI, backend-e2e, security-scan

- Remove notify-start (no more 'Started' noise)
- Gate notify-end to fire only on failure
- CI Slack restricted to PRs targeting main-staging/main only
- PRs to main-dev/frontend-dev/backend-dev: silent (GitHub UI only)

deploy-staging and rollback keep full start+end notifications."
```

---

### Task 4: Deploy Preview PR Comment

**Files:**
- Modify: `.github/workflows/ci.yml` — add `deploy-preview` job

Add a new job that runs ONLY on PRs targeting `main-staging`, posts a summary comment showing what will be deployed.

- [ ] **Step 1: Add deploy-preview job to ci.yml**

Add after the `ci-success` job, before `notify-end`. This job uses `actions/github-script@v8`:

```yaml
  # Deploy preview comment for PRs targeting staging
  deploy-preview:
    name: Deploy Preview
    runs-on: ubuntu-latest
    needs: [changes, ci-success]
    if: github.base_ref == 'main-staging' && needs.ci-success.result == 'success'
    permissions:
      pull-requests: write
    steps:
      - name: Checkout
        uses: actions/checkout@v6
        with:
          fetch-depth: 0

      - name: Generate Preview Comment
        uses: actions/github-script@v8
        env:
          FRONTEND_CHANGED: ${{ needs.changes.outputs.frontend }}
          BACKEND_CHANGED: ${{ needs.changes.outputs.backend }}
          INFRA_CHANGED: ${{ needs.changes.outputs.infra }}
        with:
          script: |
            const frontendChanged = process.env.FRONTEND_CHANGED === 'true';
            const backendChanged = process.env.BACKEND_CHANGED === 'true';
            const infraChanged = process.env.INFRA_CHANGED === 'true';

            // Build services list
            const services = [];
            if (backendChanged) services.push('🔧 **API** — will be rebuilt and redeployed');
            if (frontendChanged) services.push('🎨 **Web** — will be rebuilt and redeployed');
            if (infraChanged) services.push('🏗️ **Infra** — configuration changes detected');
            if (services.length === 0) services.push('ℹ️ No service changes detected');

            const body = [
              '## 🚀 Deploy Preview — Staging',
              '',
              '> This PR targets `main-staging`. Merging will trigger an automatic deployment to [meepleai.app](https://meepleai.app).',
              '',
              '### Services Affected',
              services.join('\n'),
              '',
              '### CI Status',
              '✅ All checks passed — ready to deploy',
              '',
              '---',
              '*🤖 Generated by CI — Deploy Preview*'
            ].join('\n');

            // Find existing preview comment to update (avoid duplicates)
            const { data: comments } = await github.rest.issues.listComments({
              owner: context.repo.owner,
              repo: context.repo.repo,
              issue_number: context.issue.number,
            });

            const existing = comments.find(c =>
              c.body && c.body.includes('Generated by CI — Deploy Preview')
            );

            if (existing) {
              await github.rest.issues.updateComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                comment_id: existing.id,
                body
              });
              console.log('Updated existing deploy preview comment');
            } else {
              await github.rest.issues.createComment({
                owner: context.repo.owner,
                repo: context.repo.repo,
                issue_number: context.issue.number,
                body
              });
              console.log('Created new deploy preview comment');
            }
```

- [ ] **Step 2: Add deploy-preview to notify-end needs**

Update the `notify-end` job `needs:` to include `deploy-preview`:
```yaml
needs: [changes, frontend, backend-unit, backend-integration, python-tests, e2e, ci-success, deploy-preview]
```

- [ ] **Step 3: Validate YAML**

Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))"`
Expected: No errors

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/ci.yml
git commit -m "feat(ci): add deploy preview comment for PRs to main-staging

When CI passes on a PR targeting main-staging, posts a PR comment
showing: services affected (API/Web/Infra), CI status, and staging
environment link. Updates existing comment on re-push to avoid spam."
```

---

### Task 5: Final Validation & Documentation

**Files:**
- Modify: `.github/workflows/README.md`

- [ ] **Step 1: Verify the complete notification matrix**

Run:
```bash
echo "=== Workflows WITH Slack ==="
grep -rl "notify-slack" .github/workflows/ | grep -v notify-slack.yml | grep -v README

echo "=== Workflows with notify-start ==="
grep -rl "notify-start:" .github/workflows/ | grep -v notify-slack.yml

echo "=== Workflows with notify-end ==="
grep -rl "notify-end:" .github/workflows/ | grep -v notify-slack.yml
```

Expected:
- **With Slack**: ci.yml, deploy-staging.yml, rollback.yml, security-scan.yml, backend-e2e-tests.yml (5 files)
- **With notify-start**: deploy-staging.yml, rollback.yml (2 files only)
- **With notify-end**: ci.yml, deploy-staging.yml, rollback.yml, security-scan.yml, backend-e2e-tests.yml (5 files)

- [ ] **Step 2: Verify no toJSON(needs) pattern remains**

Run: `grep -r "toJSON(needs)" .github/workflows/*.yml`
Expected: **Zero matches** (note: `*.yml` excludes `deploy-production.yml.disabled`)

- [ ] **Step 3: Update README.md notification section**

Find the notification section in `.github/workflows/README.md` and update it to document the new tier system:

```markdown
## Slack Notification Tiers

| Tier | Workflows | Behavior |
|------|-----------|----------|
| **CRITICAL** | deploy-staging, rollback | Start + End (both channels) |
| **IMPORTANT** | ci (main-staging/main PRs), backend-e2e, security-scan | Failures only |
| **SILENT** | All others (12 workflows) | GitHub Actions UI only |

**Estimated messages/day:** 2-5 (down from ~150)

### Deploy Preview
PRs targeting `main-staging` receive an automated comment showing:
- Services affected (API/Web/Infra)
- CI status
- Link to staging environment
```

- [ ] **Step 4: Commit**

```bash
git add .github/workflows/README.md
git commit -m "docs(workflows): update README with notification tiers

Document the 3-tier Slack notification strategy:
- CRITICAL: deploy-staging, rollback (start+end, both channels)
- IMPORTANT: ci, backend-e2e, security-scan (failures only)
- SILENT: 12 other workflows (GitHub UI only)"
```

---

## Verification Checklist

After all tasks complete:

| Check | Command | Expected |
|-------|---------|----------|
| No `toJSON(needs)` | `grep -r "toJSON(needs)" .github/workflows/*.yml` | 0 matches |
| Only 5 Slack workflows | `grep -rl "notify-slack" .github/workflows/ \| grep -v notify-slack.yml \| grep -v README \| wc -l` | 5 |
| Only 2 with start notifications | `grep -rl "notify-start:" .github/workflows/ \| grep -v notify-slack.yml \| wc -l` | 2 |
| YAML valid | Validate each file | All pass |
| Deploy preview exists | `grep "deploy-preview" .github/workflows/ci.yml` | Present |
| CI branch gate | `grep "main-staging" .github/workflows/ci.yml \| grep "base_ref"` | Present |
