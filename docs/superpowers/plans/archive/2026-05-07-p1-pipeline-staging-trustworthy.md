# P1 — Pipeline staging trustworthy Implementation Plan

**Status**: ✅ COMPLETED (PR #824 — CF Access bypass + runbook)
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rendere `deploy-staging.yml` end-to-end fidato: trigger fires consistentemente, tutti job critici eseguono (no vacuum success da gate skip), smoke test job verifica HTTP 200 reale dietro CF Access. Goal P1 dello SMART set 2026-05-07.

**Architecture:** Tre fix indipendenti su `.github/workflows/deploy-staging.yml`: (1) trigger semplificato a `push: [main-staging]` (rimuove workflow_run + gate complessità); (2) smoke test passa header CF Access service token via secret per bypass 302; (3) runbook documenta il modello "promote PR main-dev → main-staging quando ready to deploy" + come usare il bypass. Verifica via 3 deploy consecutivi success-no-skip.

**Tech Stack:** GitHub Actions (workflow YAML), Bash (smoke test functions), curl con CF-Access-Client-Id/Secret headers, Markdown runbook.

---

## Diagnosi pre-plan (osservata 2026-05-07)

Stato attuale verificato:

```
event=workflow_run, headBranch=main-dev, conclusion=success → tutti i 9 job CRITICI in stato "skipped", solo notify-end gira (= vacuum success)
event=workflow_dispatch, headBranch=main-staging, conclusion=failure → tutti job girano fino a Post-deploy Validation che FALLISCE (smoke test riceve 302 da CF Access invece di 200)
```

Root causes:
1. **Trigger `workflow_run` + gate `head_branch=='main-staging'` non si parlano**: GitHub fires workflow_run su main-dev ma gate skippa, lasciando `notify-end` (senza `needs: gate`) come unico job → "success" vacuamente
2. **Smoke test `curl -sf https://meepleai.app/health` riceve 302** da Cloudflare Access redirect invece del 200 atteso → `validate` job fail
3. **CF Access service token non disponibile** ai GitHub Actions runner (secrets `CF_ACCESS_CLIENT_ID`/`CF_ACCESS_CLIENT_SECRET` non esistono nei repo/env secrets di GitHub — verificato via `gh secret list`)

Fix scope: 1 file YAML modificato (`deploy-staging.yml`) + 1 markdown runbook nuovo + 2 GitHub secrets aggiunti dall'utente owner.

---

## File Structure

- **Modify**: `.github/workflows/deploy-staging.yml` (~10-15 righe modificate, suddivise in 2 commit logici: trigger fix + smoke fix)
- **Create**: `docs/for-developers/operations/deploy-staging-runbook.md` (~150 righe nuovo doc)
- **Modify (out-of-tree, manual via GitHub UI)**: GitHub repository secrets — aggiungere `CF_ACCESS_CLIENT_ID` + `CF_ACCESS_CLIENT_SECRET`

Nessun file applicativo (`apps/api/**`, `apps/web/**`) modificato.

---

## Task 1: CF Access service token discovery + GitHub secrets setup

**Files:**
- Read: Cloudflare Zero Trust dashboard (esterno al repo)
- Modify: GitHub repository secrets (manual via UI o `gh secret set`)
- No code/file commits in repo

> **Razionale**: il servizio Cloudflare Access wrappa `meepleai.app/*`. Il GitHub Actions runner ha bisogno di credenziali di service-token per bypass HTTP 302. User indica che "abbiamo già lavorato sui token di accesso" — Task 1 è appurare DOVE sono e renderli disponibili al runner.

- [ ] **Step 1: Owner verifica esistenza service token Cloudflare Access**

Apri https://one.dash.cloudflare.com → Access → Service Auth → Service Tokens.

Cerca un token già emesso per `meepleai.app` con scope che include `/health` o `/*` (lista tutti).

Output atteso: almeno un token esistente con CLIENT_ID + CLIENT_SECRET. Se NON esiste, crea uno nuovo nominato `github-actions-staging-smoke` con TTL ≥ 1 anno.

- [ ] **Step 2: Verifica policy CF Access permette il service token**

Dashboard CF → Access → Applications → seleziona l'app coprente `meepleai.app` → Policies.

Conferma esiste policy "Service Auth" che include il service token. Se non esiste, crea con: action=Allow, include=Service Auth + nome token sopra.

Test from local:
```bash
curl -H "CF-Access-Client-Id: <CLIENT_ID>.access" \
     -H "CF-Access-Client-Secret: <CLIENT_SECRET>" \
     -sf -o /dev/null -w "%{http_code}\n" \
     https://meepleai.app/health
```

Expected: `200` (non 302).

Se 302 ancora → policy non abbinata correttamente, ferma e correggi policy in CF dashboard.

- [ ] **Step 3: Aggiungi service token come GitHub repository secrets**

Run (sostituisci `<CLIENT_ID>` e `<CLIENT_SECRET>` con valori reali, mai committarli):
```bash
gh secret set CF_ACCESS_CLIENT_ID --body "<CLIENT_ID>.access"
gh secret set CF_ACCESS_CLIENT_SECRET --body "<CLIENT_SECRET>"
```

Verify:
```bash
gh secret list --json name | jq -r '.[] | select(.name | test("CF_ACCESS")) | .name'
```

Expected: 2 righe `CF_ACCESS_CLIENT_ID` e `CF_ACCESS_CLIENT_SECRET`.

- [ ] **Step 4: Commit "preparazione" doc nel repo**

Crea file segnaposto che traccia il setup avvenuto:
```bash
echo "# CF Access secrets setup — Task 1 P1 (2026-05-07)
- CF_ACCESS_CLIENT_ID added to GitHub repo secrets
- CF_ACCESS_CLIENT_SECRET added to GitHub repo secrets
- Service token configured in CF Zero Trust for meepleai.app /* with policy 'Service Auth'
" > /tmp/cf-access-setup-note.md
```

NB: Non committare il /tmp file. Questa è solo una traccia per Task 4 runbook.

---

## Task 2: Smoke test usa CF Access bypass headers

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`:797-833 (job `validate`, sezione "Smoke Tests")
- Modify: `.github/workflows/deploy-staging.yml`:745-786 (job `validate`, sezione "Deep Health Check")

> **Razionale**: tutti i `curl` contro `https://meepleai.app/*` nel job `validate` ricevono 302 da CF Access. Aggiungiamo CF Access service token headers a ogni curl, passando i secrets via env.

- [ ] **Step 1: Controlla baseline corrente del job `validate`**

Run:
```bash
sed -n '745,855p' .github/workflows/deploy-staging.yml
```

Verifica che esistano:
- "Deep Health Check" step con `HEALTH=$(curl -sf https://api.meepleai.app/health ...)`
- "Web Health Check" step con `curl ... https://meepleai.app`
- "Smoke Tests" step con funzione `smoke_test()` che fa curl

- [ ] **Step 2: Modifica step "Deep Health Check" — passa CF Access headers**

Edit `.github/workflows/deploy-staging.yml`. Trova la riga ~745 con step `name: Deep Health Check` e modifica come segue:

Sostituisci:
```yaml
      - name: Deep Health Check
        run: |
          echo "🔍 Running deep health checks..."

          # Wait for API with structured health response
          HEALTH=""
          for i in {1..30}; do
            HEALTH=$(curl -sf https://api.meepleai.app/health 2>/dev/null || true)
            if [ -n "$HEALTH" ]; then
```

Con:
```yaml
      - name: Deep Health Check
        env:
          CF_ACCESS_CLIENT_ID: ${{ secrets.CF_ACCESS_CLIENT_ID }}
          CF_ACCESS_CLIENT_SECRET: ${{ secrets.CF_ACCESS_CLIENT_SECRET }}
        run: |
          echo "🔍 Running deep health checks..."

          # CF Access bypass headers reused for all curls in this step
          CF_HEADERS=(-H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}")

          # Wait for API with structured health response (CF Access bypass)
          HEALTH=""
          for i in {1..30}; do
            HEALTH=$(curl -sf "${CF_HEADERS[@]}" https://api.meepleai.app/health 2>/dev/null || true)
            if [ -n "$HEALTH" ]; then
```

- [ ] **Step 3: Modifica step "Web Health Check" — passa CF Access headers**

Sostituisci:
```yaml
      - name: Web Health Check
        run: |
          HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" https://meepleai.app)
          if [ "$HTTP_STATUS" != "200" ]; then
            echo "❌ Web returned status $HTTP_STATUS"
            exit 1
          fi
          echo "✅ Web healthy (HTTP $HTTP_STATUS)"
```

Con:
```yaml
      - name: Web Health Check
        env:
          CF_ACCESS_CLIENT_ID: ${{ secrets.CF_ACCESS_CLIENT_ID }}
          CF_ACCESS_CLIENT_SECRET: ${{ secrets.CF_ACCESS_CLIENT_SECRET }}
        run: |
          HTTP_STATUS=$(curl -s -o /dev/null -w "%{http_code}" \
            -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
            -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
            https://meepleai.app)
          if [ "$HTTP_STATUS" != "200" ]; then
            echo "❌ Web returned status $HTTP_STATUS"
            exit 1
          fi
          echo "✅ Web healthy (HTTP $HTTP_STATUS)"
```

- [ ] **Step 4: Modifica step "Smoke Tests" — funzione smoke_test riceve CF headers**

Sostituisci:
```yaml
      - name: Smoke Tests
        run: |
          echo "🧪 Running staging smoke tests..."
          FAIL=0
          RESULTS=""

          smoke_test() {
            local name="$1" url="$2" expect="$3"
            local status
            status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "$url" 2>/dev/null || echo "000")
```

Con:
```yaml
      - name: Smoke Tests
        env:
          CF_ACCESS_CLIENT_ID: ${{ secrets.CF_ACCESS_CLIENT_ID }}
          CF_ACCESS_CLIENT_SECRET: ${{ secrets.CF_ACCESS_CLIENT_SECRET }}
        run: |
          echo "🧪 Running staging smoke tests..."
          FAIL=0
          RESULTS=""

          smoke_test() {
            local name="$1" url="$2" expect="$3"
            local status
            status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 \
              -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
              -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
              "$url" 2>/dev/null || echo "000")
```

E nello stesso step più sotto, modifica anche la chiamata `HEALTH_JSON=$(curl ...)`:

Sostituisci:
```bash
          HEALTH_JSON=$(curl -sf --max-time 10 https://meepleai.app/health 2>/dev/null || true)
```

Con:
```bash
          HEALTH_JSON=$(curl -sf --max-time 10 \
            -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
            -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
            https://meepleai.app/health 2>/dev/null || true)
```

E ancora la response time check:
Sostituisci:
```bash
          RT=$(curl -sf -o /dev/null -w "%{time_total}" --max-time 10 https://meepleai.app 2>/dev/null || echo "9.999")
```

Con:
```bash
          RT=$(curl -sf -o /dev/null -w "%{time_total}" --max-time 10 \
            -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
            -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
            https://meepleai.app 2>/dev/null || echo "9.999")
```

- [ ] **Step 5: Verifica YAML lint del workflow**

Run:
```bash
gh workflow view deploy-staging.yml --yaml > /tmp/deploy-staging-current.yml 2>&1
# Local YAML syntax check (using actionlint if available, else python yaml)
python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml'))" && echo "YAML OK"
```

Expected: `YAML OK`. Se errore, leggi il messaggio (probabilmente indentazione `env:` block) e correggi.

- [ ] **Step 6: Commit Task 2**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "fix(deploy-staging): pass CF Access service token headers to all smoke curls

Smoke test job validate failed because curl received 302 from Cloudflare
Access redirect instead of 200. Add CF-Access-Client-Id and
CF-Access-Client-Secret headers to:
- Deep Health Check step
- Web Health Check step
- Smoke Tests step (smoke_test function + HEALTH_JSON + RT timer)

Secrets CF_ACCESS_CLIENT_ID and CF_ACCESS_CLIENT_SECRET must be populated
in repo secrets per Task 1 of P1 plan.

Refs docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md (Goal P1)
Refs docs/superpowers/plans/2026-05-07-p1-pipeline-staging-trustworthy.md"
```

---

## Task 3: Trigger semplificato a `push: [main-staging]`

**Files:**
- Modify: `.github/workflows/deploy-staging.yml`:25-49 (top-level `on:` block)
- Modify: `.github/workflows/deploy-staging.yml`:78-105 (job `gate` — possibilmente eliminato)

> **Razionale**: rimuovere workflow_run + gate accoppiato. Trigger diventa: push a `main-staging` (manuale via release PR) OPPURE workflow_dispatch (escape hatch). Il modello "deploy quando merge a main-staging" è esplicito e deterministico. Future CI policy hardening (out of scope P1) può ripristinare un gate strutturato.

- [ ] **Step 1: Sostituisci il blocco `on:` (righe 25-49 attuali)**

Edit `.github/workflows/deploy-staging.yml`. Trova:
```yaml
on:
  workflow_run:
    workflows: ["CI"]
    types: [completed]
    branches: [main-staging]
  workflow_dispatch:
    inputs:
      skip_tests:
        description: 'Skip tests (emergency deploy only)'
        required: false
        default: 'false'
        type: boolean
      force_full_deploy:
        description: 'Force rebuild and deploy of both API and Web'
        required: false
        default: 'false'
        type: boolean
      perf_regression_mode:
        description: 'Performance regression behavior (warn|fail)'
        required: false
        default: 'warn'
        type: choice
        options:
          - warn
          - fail
```

Sostituisci con:
```yaml
on:
  push:
    branches: [main-staging]
  workflow_dispatch:
    inputs:
      skip_tests:
        description: 'Skip tests (emergency deploy only)'
        required: false
        default: 'false'
        type: boolean
      force_full_deploy:
        description: 'Force rebuild and deploy of both API and Web'
        required: false
        default: 'false'
        type: boolean
      perf_regression_mode:
        description: 'Performance regression behavior (warn|fail)'
        required: false
        default: 'warn'
        type: choice
        options:
          - warn
          - fail
```

- [ ] **Step 2: Aggiorna env DEPLOY_SHA / DEPLOY_BRANCH (righe 67-77)**

Trova:
```yaml
  # Spec R2 — Deploy SHA/branch routing
  # Under workflow_run trigger, github.sha and github.ref point to the
  # default branch, NOT the commit that triggered CI. Use the workflow_run
  # context to recover the actual deploy SHA, with a fallback to github.sha
  # for workflow_dispatch and any future trigger that doesn't expose
  # workflow_run context. Every checkout `ref:` and every shell reference
  # to the deploy SHA must use these env vars instead of github.sha.
  DEPLOY_SHA: ${{ github.event.workflow_run.head_sha || github.sha }}
  DEPLOY_BRANCH: ${{ github.event.workflow_run.head_branch || github.head_ref || github.ref_name }}
```

Sostituisci con:
```yaml
  # Deploy SHA/branch routing
  # Under push trigger, github.sha is the actual commit pushed.
  # Under workflow_dispatch, github.sha is HEAD of the ref input.
  # workflow_run path retained as defensive fallback (currently unused but
  # cheap). Every checkout `ref:` should route through these.
  DEPLOY_SHA: ${{ github.sha }}
  DEPLOY_BRANCH: ${{ github.ref_name }}
```

- [ ] **Step 3: Sostituisci il job `gate` con un placeholder no-op (per minimizzare blast radius del cambio)**

Trova:
```yaml
  # Spec R2 — Deploy quality gate
  # All deployment jobs depend on this gate. The gate passes only if:
  #   - The workflow was triggered manually (workflow_dispatch escape hatch), OR
  #   - The workflow was triggered by a successful CI run on main-staging
  #
  # When ci.yml on main-staging fails, this gate stays in `skipped` state
  # and no downstream jobs run. The result: zero deploy attempts on red CI.
  gate:
    name: CI Quality Gate
    runs-on: ubuntu-latest
    if: |
      github.event_name == 'workflow_dispatch'
      || (
        github.event_name == 'workflow_run'
        && github.event.workflow_run.conclusion == 'success'
        && github.event.workflow_run.head_branch == 'main-staging'
      )
    steps:
      - name: Confirm gate inputs
        run: |
          echo "Trigger:           ${{ github.event_name }}"
          echo "Source CI status:  ${{ github.event.workflow_run.conclusion || 'n/a (manual)' }}"
          echo "Source CI branch:  ${{ github.event.workflow_run.head_branch || 'n/a (manual)' }}"
          echo "Source CI run id:  ${{ github.event.workflow_run.id || 'n/a (manual)' }}"
          echo "Deploy SHA:        ${{ env.DEPLOY_SHA }}"
          echo "Deploy branch:     ${{ env.DEPLOY_BRANCH }}"
          echo "Gate passed - proceeding with deployment pipeline"
```

Sostituisci con:
```yaml
  # CI Quality Gate
  # Trigger now is push to main-staging or workflow_dispatch — both are
  # explicit operator-controlled actions. The gate confirms inputs for
  # observability but always passes (no skip path). Future CI policy
  # hardening (out of scope P1) may re-introduce a CI Success branch
  # protection check or workflow_run trigger if desired.
  gate:
    name: CI Quality Gate
    runs-on: ubuntu-latest
    steps:
      - name: Confirm gate inputs
        run: |
          echo "Trigger:           ${{ github.event_name }}"
          echo "Deploy SHA:        ${{ env.DEPLOY_SHA }}"
          echo "Deploy branch:     ${{ env.DEPLOY_BRANCH }}"
          echo "Gate passed - proceeding with deployment pipeline"
```

- [ ] **Step 4: Verifica YAML lint dopo edit**

Run:
```bash
python -c "import yaml; yaml.safe_load(open('.github/workflows/deploy-staging.yml'))" && echo "YAML OK"
```

Expected: `YAML OK`.

- [ ] **Step 5: Verifica visivamente che jobs downstream `needs: gate` ancora funzionano**

Run:
```bash
grep -n "needs.*gate" .github/workflows/deploy-staging.yml
```

Expected: alcune righe `needs: gate` o `needs: [gate, ...]`. Confermati ancora referenziati. Il gate ora passa SEMPRE quindi non bloccherà i downstream jobs.

- [ ] **Step 6: Commit Task 3**

```bash
git add .github/workflows/deploy-staging.yml
git commit -m "fix(deploy-staging): trigger su push:main-staging, rimuove workflow_run + gate skip vacuo

Sostituisce 'workflow_run from CI on main-staging' con 'push to main-staging'.
Risolve il problema dove workflow_run firava ma il gate head_branch=='main-staging'
skippava tutti i job lasciando notify-end come solo job ad eseguire (vacuum success).

Modello deploy ora: merge a main-staging → push trigger → tutti job eseguono.
workflow_dispatch resta come escape hatch.

Future CI policy hardening (out of scope P1) può re-introdurre un gate
strutturato basato su branch protection o workflow_run con filter affidabile.

Refs docs/superpowers/plans/2026-05-07-p1-pipeline-staging-trustworthy.md (Task 3)"
```

---

## Task 4: Runbook deploy-staging

**Files:**
- Create: `docs/for-developers/operations/deploy-staging-runbook.md`

> **Razionale**: nuovo developer/operator deve sapere (1) come funziona il pipeline post-fix, (2) come usare CF Access service token per smoke locale, (3) come distinguere skip vacuo da real run.

- [ ] **Step 1: Crea il runbook con sezioni essenziali**

Crea il file:
```bash
mkdir -p docs/for-developers/operations
cat > docs/for-developers/operations/deploy-staging-runbook.md << 'RUNBOOK_EOF'
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

```bash
# Recupera service token da CF Zero Trust dashboard:
# https://one.dash.cloudflare.com → Access → Service Auth → Service Tokens
# Token GitHub Actions: github-actions-staging-smoke
export CF_ACCESS_CLIENT_ID="<id>.access"
export CF_ACCESS_CLIENT_SECRET="<secret>"

# Smoke health
curl -sf -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
        -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
        https://meepleai.app/health | jq

# Smoke API endpoint
curl -sf -H "CF-Access-Client-Id: ${CF_ACCESS_CLIENT_ID}" \
        -H "CF-Access-Client-Secret: ${CF_ACCESS_CLIENT_SECRET}" \
        "https://meepleai.app/api/v1/discover/recent-games?pageSize=1" | jq
```

**Per CI runner**: i secrets `CF_ACCESS_CLIENT_ID` e `CF_ACCESS_CLIENT_SECRET` sono iniettati nei step `validate` job di `deploy-staging.yml` via `env:` block. Non serve azione manuale.

**Storage del token**: il token è salvato in CF Zero Trust dashboard. Rotazione raccomandata annuale. Per emettere nuovo token:
1. CF dashboard → Access → Service Auth → Create Service Token
2. Aggiorna repo secrets: `gh secret set CF_ACCESS_CLIENT_ID --body "<new>.access"`, `gh secret set CF_ACCESS_CLIENT_SECRET --body "<new>"`
3. Disabilita il token vecchio dopo verifica next deploy success

## Distinguere skip vacuo da real success run

Pre-P1, deploy-staging.yml mostrava `conclusion=success` su run dove TUTTI i job critici erano skippati (gate condition non soddisfatta). Solo `notify-end` (senza `needs: gate`) eseguiva.

**Per verificare un run è realmente eseguito**:
```bash
gh run view <RUN_ID> --json jobs | \
  jq '[.jobs[] | select(.name=="Deploy to Staging" or .name=="Build Images" or .name=="Database Migration") | .conclusion]'
```

Expected per run reale: `["success", "success", "success"]` (no `"skipped"`).
Expected per run vacuo (pre-P1): `["skipped", "skipped", "skipped"]` con run-level conclusion=success → BUG.

Post-P1 il gate passa sempre (observability-only), quindi questo problema non si ripresenta.

## Rollback procedure

`deploy-staging.yml` non ha auto-rollback. Manual rollback richiede:

```bash
# 1. Identifica merge commit responsabile
git checkout main-staging
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

## Observability

- Slack: `#deploy-notifications` (via `notify-start` + `notify-end` jobs)
- Run history: `gh run list --workflow "Deploy to Staging" --limit 20`
- DEPLOYMENT.json on staging server: `/opt/meepleai/repo/infra/DEPLOYMENT.json` — contiene version + commit + perf baseline ultimo deploy success

## Known limitations (P1 minimal)

- Trigger `push: [main-staging]` accetta qualsiasi push, anche con CI rosso. Affidamento al disciplinare merge process: chi mergia main-dev → main-staging deve aver verificato CI green pre-merge.
- Smoke test K6 + perf regression `continue-on-error: true` — non bloccanti.
- Auto-rollback assente — richiede manual revert PR.

Future hardening (out of scope P1):
- Reintroduzione branch protection rule che richiede CI Success su main-staging
- Auto-rollback workflow su validate failure
- RUM Web Vitals per p75 reali post-deploy
RUNBOOK_EOF
```

- [ ] **Step 2: Verifica markdown valido**

Run:
```bash
ls -lh docs/for-developers/operations/deploy-staging-runbook.md
head -5 docs/for-developers/operations/deploy-staging-runbook.md
```

Expected: file esiste, ~150 linee, primi 5 righe mostrano titolo + revisione.

- [ ] **Step 3: Commit Task 4**

```bash
git add docs/for-developers/operations/deploy-staging-runbook.md
git commit -m "docs(deploy-staging): add runbook for trusted pipeline (P1)

Documenta:
- Modello deploy post-P1 (push main-staging → trigger automatico)
- Procedura promote main-dev → main-staging via release PR
- Smoke testing meepleai.app dietro CF Access (service token usage)
- Come distinguere skip vacuo (pre-P1) da real success run
- Rollback manuale procedure
- Known limitations P1 minimal + future hardening notes

Refs docs/superpowers/plans/2026-05-07-p1-pipeline-staging-trustworthy.md (Task 4)"
```

---

## Task 5: Apri PR e attendi prima deploy reale

**Files:** nessuno modificato locale.

> **Razionale**: tutti i fix sono ora committati su un branch feature. Apriamo PR target main-dev (perché Task 2+3 modificano il workflow file e devono essere validati via CI), poi merge a main-dev, e da lì owner può fare release PR main-dev → main-staging (che triggera il primo deploy POST-fix).

- [ ] **Step 1: Determina parent branch e crea feature branch se non già su uno**

Run:
```bash
git status
git branch --show-current
```

Se HEAD è già su `feature/p1-pipeline-staging-trustworthy` o simile (creato durante Task 2-4), skippa al Step 2.

Se HEAD è su `main-dev` con i commit Task 2-4 sopra, sposta su feature branch:
```bash
git log --oneline -5  # verifica i 3 commit di P1 sono in cima
LAST_PRE_P1=$(git log --oneline | sed -n '4p' | awk '{print $1}')
git checkout -b feature/p1-pipeline-staging-trustworthy
git checkout main-dev
git reset --hard $LAST_PRE_P1
git checkout feature/p1-pipeline-staging-trustworthy
```

(NB: questo script serve solo se accidentalmente i commit erano stati fatti direttamente su main-dev. Best practice: lavora su feature branch da Task 2.)

- [ ] **Step 2: Push feature branch**

Run:
```bash
git push -u origin feature/p1-pipeline-staging-trustworthy
```

Expected: `Branch 'feature/p1-pipeline-staging-trustworthy' set up to track remote ...`.

- [ ] **Step 3: Crea PR target main-dev**

Run:
```bash
gh pr create \
  --base main-dev \
  --head feature/p1-pipeline-staging-trustworthy \
  --title "fix(deploy-staging): P1 pipeline trustworthy — CF Access bypass + trigger semplificato + runbook" \
  --body "$(cat <<'EOF'
## P1 Implementation

Implementa Goal P1 dello SMART set: deploy-staging.yml end-to-end fidato.

## Diagnosi pre-fix
- workflow_run + gate `head_branch=='main-staging'` skippava 9/10 job → vacuum success
- Smoke test riceveva 302 da CF Access invece di 200

## Fix
- **Task 2**: CF Access service token headers su Deep Health Check + Web Health Check + Smoke Tests step
- **Task 3**: Trigger semplificato a `push: [main-staging]` + workflow_dispatch (rimosso workflow_run + gate skip)
- **Task 4**: Runbook `docs/for-developers/operations/deploy-staging-runbook.md`

## Pre-req merge
Repo secrets devono esistere (verificati in Task 1):
- ✅ \`CF_ACCESS_CLIENT_ID\`
- ✅ \`CF_ACCESS_CLIENT_SECRET\`

## Acceptance
Post-merge a main-dev → owner crea release PR main-dev → main-staging → trigger \`push: main-staging\` → primo deploy POST-fix gira tutti i job. Verifica Task 6.

Refs spec: \`docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md\`
Refs plan: \`docs/superpowers/plans/2026-05-07-p1-pipeline-staging-trustworthy.md\`
EOF
)"
```

Annota PR_NUM stampato per i passaggi successivi.

- [ ] **Step 4: Watch CI checks su PR**

Run (sostituisci `<PR_NUM>`):
```bash
gh pr checks <PR_NUM> --watch
```

Expected: tutti i check passano. Se YAML lint fail o altro, fix sul branch e push (Task 2-3 step 5 dovrebbe averlo prevenuto).

- [ ] **Step 5: Verifica mergeability**

Run:
```bash
gh pr view <PR_NUM> --json mergeable,mergeStateStatus
```

Expected: `MERGEABLE` + `CLEAN`.

- [ ] **Step 6: Mergia a main-dev**

Run:
```bash
gh pr merge <PR_NUM> --squash --delete-branch
```

Squash perché è una "feature" P1 conceptualmente atomic (anche se 3 commit logici).

---

## Task 6: Acceptance verification — 3 deploy consecutivi success no-skip

**Files:** nessuno modificato.

> **Razionale**: SMART criterion P1 richiede 3 deploy consecutivi con conclusion=success E nessun job critico in stato skipped. Eseguiamo 3 trigger (mix di push reali main-dev → main-staging release PR + workflow_dispatch ammessi come surrogato) e ispezioniamo job results.

- [ ] **Step 1: Trigger deploy #1 — release PR main-dev → main-staging**

Run:
```bash
git checkout main-staging
git pull --ff-only origin main-staging
git checkout -b release/p1-acceptance-deploy-1-2026-05-07
git merge --no-ff origin/main-dev -m "chore(release): P1 acceptance deploy #1 — main-dev → main-staging"
git push -u origin release/p1-acceptance-deploy-1-2026-05-07
gh pr create --base main-staging --head release/p1-acceptance-deploy-1-2026-05-07 \
  --title "chore(release): P1 acceptance deploy #1" \
  --body "First post-P1 deploy verification. Refs P1 plan Task 6 step 1."
gh pr checks --watch  # attendi CI verde su release PR (su main-dev side)
gh pr merge --merge --delete-branch
```

- [ ] **Step 2: Watch deploy #1 fino a completion**

Run:
```bash
sleep 30
RUN_ID=$(gh run list --workflow "Deploy to Staging" --limit 1 --branch main-staging --json databaseId --jq '.[0].databaseId')
echo "Watching run $RUN_ID"
gh run watch $RUN_ID
```

Expected: completa con conclusion=success in ~30-40 minuti.

- [ ] **Step 3: Verifica deploy #1 NO skipped critical jobs**

Run:
```bash
gh run view $RUN_ID --json jobs | \
  jq '[.jobs[] | select(.name | test("Build Images|Database Migration|Deploy to Staging|Post-deploy Validation")) | {name, conclusion}]'
```

Expected output (4 entries, tutti conclusion="success"):
```json
[
  {"name": "Build Images", "conclusion": "success"},
  {"name": "Database Migration", "conclusion": "success"},
  {"name": "Deploy to Staging", "conclusion": "success"},
  {"name": "Post-deploy Validation", "conclusion": "success"}
]
```

Se qualche conclusion="skipped", P1 acceptance fallisce — investiga (gate ancora skippante? trigger non firato?).

Se qualche conclusion="failure" su Post-deploy Validation, smoke test ancora fail — verifica secrets popolati e curl headers corretti.

- [ ] **Step 4: Trigger deploy #2 — workflow_dispatch (surrogato manuale)**

Run:
```bash
gh workflow run "Deploy to Staging" --ref main-staging
sleep 30
RUN_ID2=$(gh run list --workflow "Deploy to Staging" --limit 1 --branch main-staging --json databaseId --jq '.[0].databaseId')
echo "Watching run $RUN_ID2"
gh run watch $RUN_ID2
```

Expected: completion success entro ~30 min. Detect-changes potrebbe forzare full deploy via workflow_dispatch input default.

- [ ] **Step 5: Verifica deploy #2 NO skipped critical jobs**

Run (sostituisci $RUN_ID2):
```bash
gh run view $RUN_ID2 --json jobs | \
  jq '[.jobs[] | select(.name | test("Build Images|Database Migration|Deploy to Staging|Post-deploy Validation")) | {name, conclusion}]'
```

Expected: 4 entries tutti success.

- [ ] **Step 6: Trigger deploy #3 — workflow_dispatch ripetuto**

Run:
```bash
gh workflow run "Deploy to Staging" --ref main-staging
sleep 30
RUN_ID3=$(gh run list --workflow "Deploy to Staging" --limit 1 --branch main-staging --json databaseId --jq '.[0].databaseId')
echo "Watching run $RUN_ID3"
gh run watch $RUN_ID3
```

- [ ] **Step 7: Verifica deploy #3 e finale**

Run (sostituisci $RUN_ID3):
```bash
gh run view $RUN_ID3 --json jobs | \
  jq '[.jobs[] | select(.name | test("Build Images|Database Migration|Deploy to Staging|Post-deploy Validation")) | {name, conclusion}]'
```

Expected: 4 entries tutti success.

- [ ] **Step 8: Smoke test post-deploy con CF Access (verifica indipendente)**

Run (sostituisci `<CLIENT_ID>` `<SECRET>` con i valori GitHub secrets):
```bash
curl -sf -H "CF-Access-Client-Id: <CLIENT_ID>" \
         -H "CF-Access-Client-Secret: <SECRET>" \
         https://meepleai.app/health | jq -r '.status'
```

Expected: `Healthy`.

- [ ] **Step 9: Posta closure comment su PR P1**

Run (sostituisci `<P1_PR_NUM>` = il PR aperto in Task 5):
```bash
gh pr comment <P1_PR_NUM> --body "$(cat <<EOF
## ✅ P1 acceptance verification — 3 deploy consecutivi green

| Deploy | Run ID | Trigger | Build Images | DB Migration | Deploy | Validate |
|--------|--------|---------|--------------|--------------|--------|----------|
| #1 | $RUN_ID | release PR push:main-staging | success | success | success | success |
| #2 | $RUN_ID2 | workflow_dispatch | success | success | success | success |
| #3 | $RUN_ID3 | workflow_dispatch | success | success | success | success |

Smoke independent: \`curl https://meepleai.app/health\` con CF Access headers ritorna \`{"status": "Healthy"}\`.

P1 SMART Goal acceptance:
- [x] 3 consecutive Deploy to Staging runs con conclusion=success
- [x] Nessun job critico (Build/Migration/Deploy/Validate) in stato skipped
- [x] Smoke test verifica HTTP 200 reale (non 302)
- [x] Runbook \`docs/for-developers/operations/deploy-staging-runbook.md\` esistente

P1 chiusa. Prossimo: P2 (deploy v2 + #807 token redesign + #808 freeze lift).
EOF
)"
```

---

## Definition of Done — P1 chiusa

- [ ] Repo secrets `CF_ACCESS_CLIENT_ID` + `CF_ACCESS_CLIENT_SECRET` popolati e funzionanti (Task 1)
- [ ] `.github/workflows/deploy-staging.yml` modificato: smoke test usa CF Access headers (Task 2) + trigger semplificato push:main-staging (Task 3)
- [ ] `docs/for-developers/operations/deploy-staging-runbook.md` creato e committato (Task 4)
- [ ] PR P1 mergiata a main-dev (Task 5)
- [ ] 3 deploy consecutivi su main-staging completano success con NESSUN job critico skipped (Task 6)
- [ ] Smoke test indipendente con CF Access headers ritorna `{"status": "Healthy"}`

**Tempo stimato totale**: 1 settimana wall-clock owner-paced. Active work ~4-6 ore (Task 1-5) + ~2 ore observation (Task 6 wait + verify).

---

## Self-review checklist

- ✅ **Spec coverage**: ogni Gherkin scenario di P1 (deploy fires + smoke real 200 + 3 consecutive + runbook) è implementato da una Task con verifica concreta
- ✅ **No placeholder**: ogni step ha comando esatto + expected output. Variabili runtime (`<PR_NUM>`, `<CLIENT_ID>`, `$RUN_ID`) sono chiaramente marcate come sostituibili
- ✅ **Type consistency**: nomi consistenti — `CF_ACCESS_CLIENT_ID`/`CF_ACCESS_CLIENT_SECRET` ovunque, branch naming `feature/p1-...` e `release/p1-acceptance-deploy-N-...`
- ✅ **Scope check**: focused su solo P1 (pipeline trustworthy minimal). #807 / #808 / Wave 3 frontend / production stand-up esplicitamente fuori scope (richiamati out-of-scope nello spec sopra)

## Riferimenti

- Spec parent: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md`
- Workflow modificato: `.github/workflows/deploy-staging.yml`
- Plan A1 invalidato (storico): `docs/superpowers/plans/2026-05-07-a1-promote-main-dev-staging.md`
