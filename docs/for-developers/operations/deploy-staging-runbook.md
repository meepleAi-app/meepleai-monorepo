# Deploy to Staging — Runbook

> Ultima revisione: 2026-05-07 (P1 fix — pipeline trustworthy minimal viable)

## Modello deploy

Il workflow `.github/workflows/deploy-staging.yml` deploya `https://meepleai.app` (staging che funge da unico ambiente pubblico al momento — produzione `meepleai.com` non provisionata).

**Trigger**:

- `push: branches: [main-staging]` — modello canonico: merge da `main-dev → main-staging` via release PR triggera deploy
- `workflow_dispatch` — escape hatch manuale (emergency / retry / hotfix)

**Pipeline jobs** (in ordine):

1. `gate` — CI Quality Gate (sempre passa post-P1; observability-only)
2. `notify-start` — Slack notification
3. `detect-changes` — paths-filter su API / Web / infra
4. `pre-deploy-check` — backend build + frontend build verification
5. `build` — Docker images push to GHCR (ARM64)
6. `migrate-db` — idempotent SQL migration via dotnet ef + pre-migration backup
7. `snapshot-baseline` — read previous DEPLOYMENT.json performance metrics
8. `deploy` — SSH-based blue-green deploy
9. `validate` — Deep Health Check + Web Health Check + Smoke Tests + K6 smoke + perf regression
10. `e2e-staging` — Playwright smoke spec
11. `notify-end` — Slack completion

## Promote main-dev → main-staging

```bash
# 1. Verify CI is green on main-dev (manual check)
gh pr checks <latest-merged-pr-number>

# 2. Create release PR
git checkout main-staging
git pull --ff-only origin main-staging
git checkout -b release/main-dev-YYYY-MM-DD
git merge --no-ff origin/main-dev -m "chore(release): merge main-dev into main-staging"
git push -u origin release/main-dev-YYYY-MM-DD

# 3. Create + merge PR
gh pr create --base main-staging --head release/main-dev-YYYY-MM-DD \
  --title "chore(release): main-dev → main-staging" \
  --body "Promote latest main-dev for staging deployment"
gh pr merge <PR_NUM> --merge --delete-branch

# 4. Watch deploy
gh run watch $(gh run list --workflow "Deploy to Staging" --limit 1 --json databaseId --jq '.[0].databaseId')
```

## Smoke testing meepleai.app dietro CF Access

`meepleai.app` è dietro Cloudflare Access. Curl pubblici ricevono `HTTP/1.1 302 Found` redirect a login.

**Per smoke locale o ad-hoc**:

Il service token è salvato in `infra/secrets/cf-access.secret` (gitignored). Template documentale a `infra/secrets/cf-access.secret.example`.

```bash
# Carica credentials da file gitignored
set -a; source infra/secrets/cf-access.secret; set +a

# Smoke health
curl -sf -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
        -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
        https://meepleai.app/health | jq

# Smoke API endpoint
curl -sf -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
        -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
        "https://meepleai.app/api/v1/discover/recent-games?pageSize=1" | jq

# Cleanup env vars
unset CF_ACCESS_CLIENT_ID CF_ACCESS_CLIENT_SECRET
```

**Per CI runner**: i secrets `CF_ACCESS_CLIENT_ID` e `CF_ACCESS_CLIENT_SECRET` sono iniettati nei step `validate` job di `deploy-staging.yml` via `env:` block. Non serve azione manuale.

**Storage del token**: il token è salvato in CF Zero Trust dashboard. Rotazione raccomandata annuale. Per emettere nuovo token:

1. CF dashboard → Access → Service Auth → Create Service Token (TTL ≥ 1 anno)
2. Aggiungi nuovo token alla policy "Service auth for staging smoke" già attaccata all'application meepleai.app
3. Aggiorna `infra/secrets/cf-access.secret` con nuovi valori
4. Re-popola GitHub secrets:
   ```bash
   set -a; source infra/secrets/cf-access.secret; set +a
   printf '%s' "$CF_ACCESS_CLIENT_ID"    | gh secret set CF_ACCESS_CLIENT_ID
   printf '%s' "$CF_ACCESS_CLIENT_SECRET" | gh secret set CF_ACCESS_CLIENT_SECRET
   unset CF_ACCESS_CLIENT_ID CF_ACCESS_CLIENT_SECRET
   ```
5. Verifica deploy success al prossimo run, poi revoca il token vecchio in CF dashboard

## Distinguere skip vacuo da real success run

Pre-P1, deploy-staging.yml mostrava `conclusion=success` su run dove TUTTI i job critici erano skippati (gate condition `workflow_run.head_branch=='main-staging'` non soddisfatta da CI completion su main-dev). Solo `notify-end` (senza `needs: gate`) eseguiva.

**Per verificare un run è realmente eseguito**:

```bash
gh run view <RUN_ID> --json jobs | \
  jq '[.jobs[] | select(.name | test("Build Images|Database Migration|Deploy to Staging|Post-deploy Validation")) | {name: .name, conclusion: .conclusion}]'
```

Expected per run reale: 4 entries con `conclusion="success"` (no `"skipped"`).
Expected per run vacuo (pre-P1): tutti `"skipped"` con run-level conclusion=success → BUG.

Post-P1 il trigger è `push: [main-staging]` quindi non si verificano più run vacui.

## Rollback procedure

Two paths depending on incident urgency:

### Fast path — image-tag revert (≤ 10 min RTO)

For P1 incidents where forward-fix is too slow, use the **image-tag rollback**:

```bash
bash infra/scripts/rollback-trigger.sh staging staging-YYYYMMDD-<sha7>
```

Full procedure with safety checks, 3 scenarios, and post-rollback validation:
[**rollback-runbook.md**](./rollback-runbook.md).

### Slow path — revert PR (forward-fix friendly)

When the broken deploy is non-critical and you want the rollback to flow through CI:

```bash
# 1. Identifica merge commit responsabile
git checkout main-staging
git pull --ff-only origin main-staging
git log -1 --pretty='%H' --merges

# 2. Crea revert PR
git checkout -b revert/staging-deploy-YYYY-MM-DD
git revert -m 1 <MERGE_SHA> --no-edit
git push -u origin revert/staging-deploy-YYYY-MM-DD
gh pr create --base main-staging --head revert/staging-deploy-YYYY-MM-DD \
  --title "revert: staging deploy <MERGE_SHA>" \
  --body "Rollback. Cause: <DESCRIBE_FAILURE>"

# 3. Merge → triggera nuovo deploy con codice precedente
gh pr merge <REVERT_PR_NUM> --merge --delete-branch
gh run watch $(gh run list --workflow "Deploy to Staging" --limit 1 --json databaseId --jq '.[0].databaseId')
```

The slow path takes ~15-25 min (CI rebuild + deploy) vs ~10 min for the image-tag path,
but produces a clean git history. Use slow path for P2/P3, fast path for P1.

## Observability

- Slack: notifiche via `notify-start` + `notify-end` jobs (channel configurato in repo secrets `SLACK_GITNOTIFY_WEBHOOK_URL` + `SLACK_CRITICAL_WEBHOOK_URL`)
- Run history: `gh run list --workflow "Deploy to Staging" --limit 20`
- DEPLOYMENT.json on staging server: `/opt/meepleai/repo/infra/DEPLOYMENT.json` — contiene version + commit + perf baseline ultimo deploy success

## Known limitations (P1 minimal)

- Trigger `push: [main-staging]` accetta qualsiasi push, anche con CI rosso. Affidamento al disciplinare merge process: chi mergia main-dev → main-staging deve aver verificato CI green pre-merge.
- Smoke test K6 + perf regression sono `continue-on-error: true` — non bloccanti.
- Auto-rollback assente — manuale via `rollback-runbook.md` (fast path, image-tag) o revert PR (slow path).

Future hardening (out of scope P1):

- Reintroduzione branch protection rule che richiede CI Success su main-staging
- Auto-rollback workflow su validate failure
- RUM Web Vitals per p75 reali post-deploy
- CF Access bypass per `/health` endpoint (consentirebbe smoke pubblico senza service token, trade-off security)
