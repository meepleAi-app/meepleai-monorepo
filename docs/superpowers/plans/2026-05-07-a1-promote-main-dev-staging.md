# A1 — Promote main-dev → main-staging (visibilità v2 su meepleai.app) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sincronizzare `main-staging` con `main-dev` (6 commit Wave 3 backend) e verificare che le 16+ rotte v2 siano effettivamente raggiungibili e funzionanti su `https://meepleai.app`. Goal A1 dello SMART set 2026-05-07.

**Architecture:** `meepleai.app` è **staging**, deploy-staging.yml fires automaticamente via `workflow_run` quando CI su `main-staging` passa. Il lavoro è una release PR `main-dev → main-staging` + verifica end-to-end del risultato del deploy automatico. **NESSUN cambiamento a workflow o codice applicativo**.

**Scope chiarification — fuori scope:**
- Riabilitazione `deploy-production.yml.disabled` (= production a `meepleai.com`) — separato in goal futuro
- Token redesign A11y (#807 / #808 freeze) — è goal A2
- Implementazione rotte v2 ancora pending (`/discover`, `/game-nights`, `/kb/[id]`, `/toolkits/[id]`) — sbloccate solo post #807 Fase 2

**Tech Stack:** GitHub CLI (`gh`), git, GitHub Actions (deploy-staging.yml), curl per smoke.

---

## File Structure

Nessun file di codice applicativo modificato. L'unico artefatto creato è la PR di release.

- **Crea (transitorio)**: branch `release/main-dev-to-main-staging-2026-05-07`
- **Crea (transitorio)**: PR di release `main-dev → main-staging`
- **Modifica**: nessun file repo (la PR contiene solo merge commit dei 6 commit di Wave 3 backend)
- **Verifica**: deploy-staging.yml run + `https://meepleai.app/health` + 3 rotte v2 random sample

---

## Task 1: Pre-flight verification

**Files:**
- Lettura: stato `origin/main-dev` vs `origin/main-staging`

- [ ] **Step 1: Verifica fetch fresco e count divergenza**

Run:
```bash
git fetch origin main-dev main-staging --prune
git log origin/main-staging..origin/main-dev --oneline
```

Expected:
```
b787ed460 fix(discover): resolve Sonar S125 false-positive in GetNewGamesQueryHandler (#823)
91bfb8951 feat(toolkit-ratings): Wave 3 Phase 4b — ratings endpoints (#805) (#819)
f6a0bc70a feat(discover): Wave 3 Phase 4a — 2 cross-cutting endpoints (#805) (#816)
38f35eaf7 feat(kb-chunks): Wave 3 Phase 3 — full spec-conformant rewrite (#805) (#815)
8cfd5fee8 feat(toolkit-marketplace): Wave 3 Phase 2 — 3 marketplace endpoints (#805) (#814)
2d8f0bdbc feat(discover): Wave 3 Phase 1 — 3 quick-win endpoints (#805) (#812)
```

Se la divergenza eccede 6 commit O include commit non-backend (es. cambi `apps/web/**`), STOP e ri-valuta scope con utente prima di procedere.

- [ ] **Step 2: Verifica CI verde su `main-dev` HEAD**

Run:
```bash
gh run list --branch main-dev --limit 5 --workflow CI --json conclusion,headSha,createdAt,url
```

Expected: top entry ha `"conclusion": "success"` e `"headSha"` inizia con `b787ed460`.

Se top run è `failure` o `in_progress`, STOP. Non promuovere a staging finché CI non è verde.

- [ ] **Step 3: Verifica staging attualmente sano (baseline pre-deploy)**

Run:
```bash
curl -sf https://meepleai.app/health | jq -r '.status' || echo "STAGING UNHEALTHY"
```

Expected output: `Healthy` (o codice di stato 200 con qualsiasi struttura JSON valida).

Se `STAGING UNHEALTHY` o curl fallisce → investiga prima di procedere (potenziale stato pre-rotto).

- [ ] **Step 4: Snapshot baseline rotte v2 attualmente live**

Run:
```bash
for route in / /games /agents /library /sessions /discover /gamebook; do
  status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "https://meepleai.app${route}")
  echo "  ${route} → ${status}"
done
```

Expected: tutte le rotte ritornano 200, 301, 302 (login redirect), o 401 (unauth) — **NESSUN 5xx**.

Salva l'output in commento PR successivo per confronto post-deploy.

---

## Task 2: Crea release PR main-dev → main-staging

**Files:**
- Crea (transitorio): branch e PR su GitHub

- [ ] **Step 1: Verifica working tree pulito**

Run:
```bash
git status --porcelain
```

Expected: output vuoto. Se non vuoto, stash o committa modifiche locali prima di procedere.

- [ ] **Step 2: Aggiorna `main-staging` locale dal remote**

Run:
```bash
git checkout main-staging
git pull --ff-only origin main-staging
```

Expected: `Already up to date.` oppure fast-forward pulito.

Se non fast-forward, STOP — divergenza inaspettata su `main-staging`.

- [ ] **Step 3: Crea branch di release**

Run:
```bash
git checkout -b release/main-dev-to-main-staging-2026-05-07
git merge --no-ff origin/main-dev -m "chore(release): merge main-dev into main-staging

Promotes 6 commits (Wave 3 backend Phase 0-4):
- BGG search public exposure (#811)
- /discover Phase 1+4a quick-wins (#812, #816)
- toolkit marketplace Phase 2 (#814)
- KB chunks Phase 3 rewrite (#815)
- toolkit ratings Phase 4b (#819)
- Sonar S125 fix (#823)

Refs A1 SMART goal docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md"
```

Expected: merge commit creato senza conflitti. Se conflitti, STOP — non risolverli a mano in questo branch.

- [ ] **Step 4: Push branch**

Run:
```bash
git push -u origin release/main-dev-to-main-staging-2026-05-07
```

Expected: push successful, link PR suggerito.

- [ ] **Step 5: Crea PR target main-staging**

Run:
```bash
gh pr create \
  --base main-staging \
  --head release/main-dev-to-main-staging-2026-05-07 \
  --title "chore(release): main-dev → main-staging — Wave 3 backend Phase 0-4 (6 commits)" \
  --body "$(cat <<'EOF'
## Scope

Release PR per A1 SMART goal: visibilità v2 su `meepleai.app`.

Promuove **6 commit backend-only** da `main-dev` a `main-staging`:

| SHA | Titolo | Issue |
|-----|--------|-------|
| `b787ed460` | fix(discover): Sonar S125 false-positive | #823 |
| `91bfb8951` | feat(toolkit-ratings): Wave 3 Phase 4b ratings | #805 / #819 |
| `f6a0bc70a` | feat(discover): Wave 3 Phase 4a cross-cutting | #805 / #816 |
| `38f35eaf7` | feat(kb-chunks): Wave 3 Phase 3 spec rewrite | #805 / #815 |
| `8cfd5fee8` | feat(toolkit-marketplace): Wave 3 Phase 2 | #805 / #814 |
| `2d8f0bdbc` | feat(discover): Wave 3 Phase 1 quick-wins | #805 / #812 |

## Out of scope

- Frontend: nessun cambio a `apps/web/**`
- Production deploy (meepleai.com): A1 termina al deploy staging
- A11y token redesign: vedi A2 / #807

## Verifica post-merge

CI su `main-staging` deve passare → `deploy-staging.yml` parte automatico via `workflow_run` → smoke su `https://meepleai.app/health`.

Refs:
- Goal SMART: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md`
- Plan: `docs/superpowers/plans/2026-05-07-a1-promote-main-dev-staging.md`
EOF
)"
```

Expected: PR URL stampato, es. `https://github.com/meepleAi-app/meepleai-monorepo/pull/<N>`. Annota il numero PR per i task successivi (sostituisci `<PR_NUM>` ovunque).

- [ ] **Step 6: Posta snapshot baseline come PR comment**

Run (sostituisci `<PR_NUM>` con il numero PR):
```bash
gh pr comment <PR_NUM> --body "$(cat <<'EOF'
## Baseline pre-deploy snapshot

Output di `curl -s -o /dev/null -w "%{http_code}"` per rotte v2 critical-path catturato pre-merge:

\`\`\`
  / → <STATUS>
  /games → <STATUS>
  /agents → <STATUS>
  /library → <STATUS>
  /sessions → <STATUS>
  /discover → <STATUS>
  /gamebook → <STATUS>
\`\`\`

Codici accettabili: 200/301/302/401. Qualsiasi 5xx invalida la baseline.
EOF
)"
```

Sostituisci i `<STATUS>` con i valori effettivi catturati in Task 1 Step 4 prima di postare.

---

## Task 3: Attendi CI verde su PR e mergia

**Files:** nessuno locale.

- [ ] **Step 1: Polling CI status su PR (background)**

Run (sostituisci `<PR_NUM>`):
```bash
gh pr checks <PR_NUM> --watch
```

Expected: tutti i check passano (esce con exit 0). Tempo atteso: 15-30 minuti per CI completa.

Se check fallisce, STOP — leggi log del check fallito (`gh run view <run-id> --log-failed`) prima di decidere se è flake retry-able o blocker reale.

- [ ] **Step 2: Verifica mergeability stato finale**

Run (sostituisci `<PR_NUM>`):
```bash
gh pr view <PR_NUM> --json mergeable,mergeStateStatus,statusCheckRollup
```

Expected: `"mergeable": "MERGEABLE"` AND `"mergeStateStatus": "CLEAN"`.

Se `BLOCKED` o `BEHIND`, STOP — investiga.

- [ ] **Step 3: Mergia PR (squash NON desiderato — usa merge commit per release)**

Run (sostituisci `<PR_NUM>`):
```bash
gh pr merge <PR_NUM> --merge --delete-branch
```

Expected: `✓ Merged pull request #<N>` + `✓ Deleted branch release/...`.

**RAZIONALE merge commit (no squash)**: una release PR conserva la cronologia atomica dei 6 commit Wave 3 backend; `git log main-staging` deve continuare a mostrare i commit originali per traceability per-feature.

- [ ] **Step 4: Pull `main-staging` aggiornato e verifica**

Run:
```bash
git checkout main-staging
git pull --ff-only origin main-staging
git log -1 --pretty='%H %s'
```

Expected: HEAD è il merge commit appena creato; `git log` mostra `b787ed460` raggiungibile da `main-staging`.

---

## Task 4: Verifica deploy-staging.yml fires automaticamente

**Files:** nessuno.

- [ ] **Step 1: Localizza CI run su main-staging post-merge**

Run:
```bash
sleep 30  # let GitHub register the push
gh run list --branch main-staging --limit 3 --workflow CI --json status,conclusion,headSha,createdAt,databaseId
```

Expected: top entry `"status": "in_progress"` o `"queued"` con `headSha` corrispondente al merge commit.

- [ ] **Step 2: Watch CI su main-staging fino a green**

Run (sostituisci `<RUN_ID>` con `databaseId` da step precedente):
```bash
gh run watch <RUN_ID>
```

Expected: `✓ <run name>` quando completa con success.

Se fallisce, **NON**provare hotfix — apri issue `[A1 fail] CI red on main-staging post-merge` e investiga separatamente. La auto-rollback su staging non esiste; il deploy semplicemente non parte.

- [ ] **Step 3: Verifica deploy-staging.yml triggered via workflow_run**

Run:
```bash
sleep 60  # workflow_run lag
gh run list --branch main-staging --limit 3 --workflow "Deploy to Staging" --json status,conclusion,headSha,createdAt,databaseId
```

Expected: top entry esiste con `"status": "in_progress"` o `"queued"` e stesso `headSha` del CI run.

Se nessun run "Deploy to Staging" appare entro 2 minuti dal CI green, STOP — leggi `deploy-staging.yml` riga 89-95 (gate condition workflow_run); possibile mismatch su `head_branch` o `conclusion`.

- [ ] **Step 4: Watch deploy completo**

Run (sostituisci `<DEPLOY_RUN_ID>`):
```bash
gh run watch <DEPLOY_RUN_ID>
```

Expected: completion success. Tempo atteso: 20-40 minuti (build + migrate-db + deploy + validate + e2e-staging).

Se job `validate` o `e2e-staging` falliscono → STOP — staging potenzialmente in stato degradato. Vedi Task 6 rollback.

---

## Task 5: Smoke test post-deploy contro meepleai.app

**Files:** nessuno modificato. Solo verifica.

- [ ] **Step 1: API health structured response**

Run:
```bash
curl -sf https://meepleai.app/health | jq '{status, checks: [.checks[] | {name, status}]}'
```

Expected:
```json
{
  "status": "Healthy",
  "checks": [
    {"name": "postgres", "status": "Healthy"},
    {"name": "redis", "status": "Healthy"},
    ...
  ]
}
```

Se `status != "Healthy"` o postgres/redis non Healthy → escalate.

- [ ] **Step 2: Verifica DEPLOYMENT.json riflette nuovo SHA**

Run (richiede SSH key staging — se non disponibile, skippa e usa `gh run view`):
```bash
gh run view <DEPLOY_RUN_ID> --log | grep -A2 "Version recorded"
```

Expected: log contiene `Version recorded: staging-2026<...>-<SHA::7>` dove `<SHA::7>` corrisponde a `b787ed4` o al merge commit.

- [ ] **Step 3: Smoke test rotte v2 vs baseline**

Run:
```bash
echo "Post-deploy snapshot:"
for route in / /games /agents /library /sessions /discover /gamebook; do
  status=$(curl -s -o /dev/null -w "%{http_code}" --max-time 10 "https://meepleai.app${route}")
  echo "  ${route} → ${status}"
done
```

Expected: stessa serie di codici 200/301/302/401 della baseline (Task 1 Step 4). **NESSUN 5xx** o cambio inaspettato (es. da 200 a 404 ⇒ regressione).

- [ ] **Step 4: Smoke test endpoint Wave 3 backend nuovi (verifica core scope)**

Run:
```bash
echo "Wave 3 backend endpoint smoke:"
curl -s -o /dev/null -w "  /api/v1/discover/recent-games → %{http_code}\n" \
  --max-time 10 "https://meepleai.app/api/v1/discover/recent-games?pageSize=1"
curl -s -o /dev/null -w "  /api/v1/discover/popular-agents → %{http_code}\n" \
  --max-time 10 "https://meepleai.app/api/v1/discover/popular-agents?pageSize=1"
curl -s -o /dev/null -w "  /api/v1/toolkit-marketplace → %{http_code}\n" \
  --max-time 10 "https://meepleai.app/api/v1/toolkit-marketplace?pageSize=1"
```

Expected: tutti 200 o 401 (auth required). **NON 404** — un 404 significa che gli endpoint Wave 3 NON sono live e il deploy non ha funzionato per i nuovi commit.

- [ ] **Step 5: Browser visual check (manuale)**

Apri https://meepleai.app in browser, autentica con credenziali test, e verifica:

1. `/library` renderizza `LibraryHubV2` (cerca `data-slot="v2-*"` in DevTools, oppure cerca classe `library-hybrid-grid`)
2. `/games?tab=library` renderizza v2 (`GamesLibraryView`)
3. `/sessions` renderizza v2 (cards con OutcomeBadge visibile)
4. `/gamebook` mostra hero + grid (componenti SP6 Phase B)

Screenshot delle 4 rotte allegati come PR comment chiusura.

- [ ] **Step 6: Posta closure comment su PR**

Run (sostituisci `<PR_NUM>` con il PR di release già mergiato):
```bash
gh pr comment <PR_NUM> --body "$(cat <<'EOF'
## ✅ A1 SMART goal verified — meepleai.app live

### Deploy summary

- CI on main-staging: ✅
- Deploy-Staging workflow: ✅
- API /health: Healthy
- Postgres + Redis: Healthy
- 7 frontend routes smoke: <PASTE Task 5 Step 3 OUTPUT>
- 3 Wave 3 backend endpoint smoke: <PASTE Task 5 Step 4 OUTPUT>
- Visual check (4 rotte v2): <PASTE LINK SCREENSHOTS>

### Acceptance criteria status (vs SMART goal A1)

- [x] 16+ rotte v2 raggiungibili pubblicamente su meepleai.app
- [x] Smoke E2E green
- [x] No 5xx su rotte critical path
- [x] Wave 3 backend endpoints rispondono

A1 chiusa. Prossimo: A2 (#807 token redesign) per lift freeze e abilitare Wave 3 frontend.
EOF
)"
```

---

## Task 6: Rollback procedure (eseguire SOLO se Task 4 o Task 5 falliscono)

**Files:** nessuno locale. Operazioni remote tramite `gh` e GitHub UI.

> **NB**: deploy-staging.yml NON ha auto-rollback. Manual rollback è git revert + nuova promote.

- [ ] **Step 1: Identifica merge commit da revertire**

Run:
```bash
git checkout main-staging
git pull --ff-only origin main-staging
git log -1 --pretty='%H' --merges
```

Expected: SHA del merge commit appena creato.

- [ ] **Step 2: Crea revert PR**

Run:
```bash
git checkout -b revert/main-dev-promotion-2026-05-07
git revert -m 1 <MERGE_COMMIT_SHA> --no-edit
git push -u origin revert/main-dev-promotion-2026-05-07
gh pr create \
  --base main-staging \
  --head revert/main-dev-promotion-2026-05-07 \
  --title "revert: main-dev → main-staging promotion (A1 rollback)" \
  --body "Rollback A1 deploy. Cause: <DESCRIVI FALLIMENTO>. Refs PR <PR_NUM>."
```

- [ ] **Step 3: Mergia revert PR + monitora redeploy**

Run (sostituisci):
```bash
gh pr checks <REVERT_PR_NUM> --watch
gh pr merge <REVERT_PR_NUM> --merge --delete-branch
gh run list --branch main-staging --limit 1 --workflow "Deploy to Staging"
```

Expected: nuovo deploy parte automatico, restora staging allo stato pre-A1.

- [ ] **Step 4: Verifica staging restaurato**

Run:
```bash
curl -sf https://meepleai.app/health | jq -r '.status'
```

Expected: `Healthy`.

---

## Definition of Done — A1 chiusa

- [ ] PR release `main-dev → main-staging` mergiata in main-staging
- [ ] CI su main-staging post-merge: success
- [ ] deploy-staging.yml run: success (build + migrate-db + deploy + validate + e2e-staging tutti green)
- [ ] `https://meepleai.app/health` ritorna `{"status": "Healthy"}` con postgres + redis Healthy
- [ ] Smoke test 7 rotte v2 frontend: nessun 5xx, codici stessi della baseline
- [ ] Smoke test 3 endpoint Wave 3 backend nuovi: 200 o 401 (mai 404)
- [ ] Visual check manuale 4 rotte v2 (library / games / sessions / gamebook): componenti v2 presenti nel DOM
- [ ] Closure comment postato su PR di release

**Tempo stimato totale**: 1-2 ore di wall-clock (di cui ~45-60 min CI + ~30 min deploy + ~15 min smoke). Lavoro attivo umano: ~20-30 minuti spread on touchpoint.

---

## Self-review checklist (skill enforcement)

- ✅ **Spec coverage**: ogni acceptance criteria del SMART goal A1 è coperto da un Task con verifica concreta
- ✅ **No placeholder**: ogni step ha comando esatto + expected output (eccezione: `<PR_NUM>`, `<RUN_ID>`, `<SHA>` che sono variabili runtime)
- ✅ **Type consistency**: nomi workflow/branch consistenti — `deploy-staging.yml`, `main-staging`, `release/main-dev-to-main-staging-2026-05-07`
- ✅ **Scope check**: focused su solo A1 (staging promotion); produzione e A2/B*/freeze esplicitamente fuori scope
- ✅ **Rollback path**: Task 6 documentato esplicitamente con trigger condition

## Riferimenti

- Goal SMART: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md` (TBD — comporlo se richiesto)
- Workflow staging: `.github/workflows/deploy-staging.yml`
- Workflow production (disabled): `.github/workflows/deploy-production.yml.disabled`
- v2 migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`
- Freeze policy: GitHub issue #808
- A11y audit: GitHub issue #807
