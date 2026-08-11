# Audit — Inventario procedure & piano di cleanup

**Data**: 2026-07-31 · **Branch**: `main-dev` · **Metodo**: workflow multi-agente (9 agenti di discovery in parallelo → sintesi con scoring normalizzato → critico verificatore) · **Procedure catalogate**: 258 · **Verdetto verifica**: PASS, 0 falsi-delete.

> **Scopo**: catalogare ogni "procedura" eseguibile del monorepo (target `make`, script shell/PowerShell/Python/Node, workflow CI, comandi dev, runbook documentati), assegnare **utilità (1-10)** e **frequenza d'uso (1-10)**, e derivarne un piano di riduzione di file duplicati/morti e di LOC.

---

## 1. Executive summary

Il grosso del repository è sano e cablato: **171 procedure su 258 (~66%) sono `keep`**. Il debito è concentrato in tre nuclei:

1. **Codice morto archiviato** — `tools/archive/**` (59 script one-off per issue chiuse) + `.claude/helpers/**` (20 script di scaffolding claude-flow mai agganciati agli hook di progetto). Da soli valgono **~13.700 LOC** eliminabili a rischio nullo.
2. **One-off consumati** — diagnostici chess/RAG con credenziali hardcoded, seed dashboard archiviato, `rebucket-pdfs`, codemod Jest→Vitest, ecc.
3. **Duplicazione cross-platform** `.sh`/`.ps1` (10 coppie twin, di cui 2 legittime) + **percorsi paralleli legacy** sotto `tools/{backup,deployment,secrets}` che duplicano i gemelli canonici già cablati in `infra/`.

| Metrica | Valore |
|---|---|
| Procedure catalogate | 258 (1° giro) + 34 (2° giro chiusura lacune) |
| `keep` (core, non toccare) | ~180 |
| `delete` (rimozione a rischio nullo) | **~115 file** |
| `consolidate` (duplicati/alias) | 14 gruppi (10 coppie cross-platform `.sh`/`.ps1`) |
| `review` (decisione umana) | ~69 |
| **LOC eliminabili stimate** | **~19.823** (~18.323 delete + ~1.500 meta-consolidate) |

> **Nota metrica**: la sintesi del 1° giro contava `deleteCount = 16` **gruppi/famiglie**; espansi + il 2° giro di chiusura lacune (§8-bis), i file fisici da rimuovere sono **~115** (59 in `tools/archive/**` + 20 in `.claude/helpers/**` + ~36 sciolti). La cifra LOC si riferisce ai file, non ai gruppi.
>
> **Aggiornamento 2° giro**: le 4 cartelle non coperte dal critico sono state analizzate (34 record) e piegate nei bucket. Netto: **+10 file DELETE (~3.297 LOC)** — il singolo file più pesante dell'intero audit è `tools/setup/.dotnet-install.sh`, **1888 LOC** di installer Microsoft vendored e mai invocato. Confermati invece `keep` (CI-wired) `lint-cross-lang-constants.sh`, `scrub_bgg_manifest.py`, `bootstrap_wikidata_qid.py`: sarebbero stati **falsi-delete** se rimossi alla cieca.

---

## 2. Metodologia & normalizzazione scoring

- **utilità (1-10)**: valore per il progetto *oggi*. 10 = critica/insostituibile (build, test, deploy, dev-up); 7-9 = operativa importante; 4-6 = utile occasionale; 2-3 = marginale/rimpiazzabile; 1 = obsoleta/morta.
- **frequenza (1-10)**: uso reale. 10 = ogni giorno / ogni PR / ogni CI run; 7-9 = settimanale; 4-6 = mensile / per-release; 2-3 = una-tantum ricorrente / rara; 1 = mai da tempo o one-off già consumato.
- **Evidenza**: referenze del basename in `infra/Makefile`, `.github/workflows/`, `**/package.json`, `.claude/settings*.json`, `.husky/`, `docs/`. Referenziato attivamente → punteggio alto + `keep`; in `archive/` o legato a issue chiusa → `1/1` + `delete`; gemello cross-platform → `consolidate`.
- **Normalizzazione**: scala comune applicata tra le 9 aree; nessun outlier alto da correggere sugli archive (correttamente a utilità = 1); i core loop restano a 9-10. `make game-reset` (punteggio grezzo basso ma in Quick Reference) trattato come `keep`; i twin cross-platform legittimi (`run-with-docker`, `setup-test-env`) hanno `duplicate_of` valorizzato ma **non** sono `consolidate`.

---

## 3. Tabella procedure

Ordine: DELETE → CONSOLIDATE → REVIEW → KEEP; entro gruppo per utilità ascendente. Famiglie raggruppate.

| Procedura | Cat. | Path | Util. | Freq. | Cleanup | Note |
|---|---|---|---|---|---|---|
| Archive 2025-11 closed-issues (43) | ps1 | `tools/archive/2025-11-closed-issues/` | 1 | 1 | delete | ~6000 LOC, consumati, zero ref |
| Archive 2025-11 guid-fixes (9) | ps1 | `tools/archive/2025-11-guid-fixes/` | 1 | 1 | delete | migrazione GUID chiusa |
| Archive migration-scripts (7) | sh/js/py | `tools/archive/migration-scripts/` | 1 | 1 | delete | migrazioni per-fase applicate |
| `.claude/helpers` quick-start banner | sh | `.claude/helpers/quick-start.sh` | 1 | 1 | delete | echo claude-flow non installato |
| `.claude/helpers` swarm-* (3) | sh | `.claude/helpers/swarm-*.sh` | 1 | 1 | delete | scaffolding claude-flow, no wiring |
| `.claude/helpers` v3-* (5) | sh | `.claude/helpers/v3*.sh` | 1 | 1 | delete | tracking v3 del tool, non MeepleAI |
| `.claude/helpers` learning/guidance/pattern (5) | sh | `.claude/helpers/{learning,guidance,pattern}*` | 1 | 1 | delete | hooks mai registrati |
| `.claude/helpers` checkpoint/daemon/worker (6) | sh | `.claude/helpers/{checkpoint,daemon,worker,perf,health}*` | 1 | 1 | delete | runtime statusline claude-flow |
| create-issues.sh (epic #4068) | sh | `create-issues.sh` | 1 | 1 | delete | batch gh issue create consumato |
| rebucket-pdfs (twin) | ps1/sh | `scripts/rebucket-pdfs.ps1` + `-s3.sh` | 1 | 1 | delete | migrazione #480 consumata |
| Chess/RAG diagnostic one-off (10) | ps1 | `tools/{check,fix,upload}-chess*`, `tools/test-*` | 2 | 1 | delete | cred+GameId hardcoded, AGT-018 |
| fix-test-trait-constants v1+v2 | ps1 | `tools/fix-test-trait-constants*.ps1` | 2 | 1 | delete | coppia v1/v2 dup, applicata |
| Consumed test/epic one-off (5) | ps1/sh | `tools/{add-test-traits,migrate-*,remove-regions,update-epic-3167}` | 2 | 1 | delete | issue #2031/#1679/#3167 chiuse |
| issue-triage-analysis.md | doc | `tools/issue-triage-analysis.md` | 1 | 1 | delete | snapshot triage storico |
| seed-dashboard-data (ps1+sql+README) | ps1 | `scripts/seed-dashboard-data.*` | 2 | 1 | delete | epic Gaming Hub archiviato |
| fix-vitest-imports + migrate-jest-to-vitest | sh | `apps/web/scripts/{fix-vitest-imports,migrate-jest-to-vitest}.sh` | 1 | 1 | delete | codemod #1503 completato |
| pnpm chromatic + test:visual:ci alias | pnpm | `apps/web/package.json` | 6 | 4 | consolidate | `test:visual:ci` === `chromatic:ci` |
| pnpm scraper:* passthrough | pnpm | `apps/web/package.json` | 4 | 3 | consolidate | mere deleghe a game-scraper |
| auto-validate.yml (name collision) | ci | `.github/workflows/auto-validate.yml` | 6 | 5 | consolidate | display name === validate-workflows.yml |
| security-audit.ps1 | ps1 | `infra/scripts/security-audit.ps1` | 3 | 2 | consolidate | twin di security-audit.sh (wired) |
| mvp-smoke-test.sh | sh | `infra/scripts/mvp-smoke-test.sh` | 3 | 2 | consolidate | overlap smoke-set.sh |
| start-dev.ps1 | ps1 | `infra/scripts/start-dev.ps1` | 3 | 2 | consolidate | overlap `make dev` |
| validate-doc-links.ps1 | ps1 | `scripts/quality/validate-doc-links.ps1` | 3 | 2 | consolidate | dup cross-lang di scan-broken-links.py |
| open-dual-vscode (twin) | ps1/sh | `tools/development/open-dual-vscode.*` | 3 | 2 | consolidate | coppia + alternativa nativa VSCode |
| tools/secrets/* (init/list/rotate) | sh | `tools/secrets/` | 3 | 2 | consolidate | dup flusso `make secrets-*` |
| cleanup-testcontainers (twin) | ps1/sh | `tools/cleanup/cleanup-testcontainers.*` | 4 | 2 | consolidate | coppia + overlap cleanup-test-processes |
| cleanup-caches (twin) | ps1/sh | `tools/cleanup/cleanup-caches.*` | 4 | 2 | consolidate | coppia cross-platform |
| Seed blob publish legacy (3) | py/sh | `infra/scripts/{snapshot-publish.py,upload-seed-pdfs.*}` | 3 | 1 | consolidate | superati da seed-index-publish.sh |
| coverage-trends + measure + refactor | ps1/sh | `tools/coverage/{coverage-trends.*,measure-coverage.ps1,refactor-test-isolation.sh}` | 3 | 2 | consolidate | coppia + one-off consumato |
| Legacy seed/db ps1 (5) | ps1 | `infra/scripts/{seed-dump,seed-pull,seed-restore,db-dump,db-restore}.ps1` | 3 | 1 | consolidate | overlap snapshot/restore .sh canonici |
| tools/backup/* (10) | sh | `tools/backup/` | 3 | 1 | review | ~2500 LOC parallele; canonico è infra/scripts/backup.sh; ref Qdrant morto |
| tools/deployment/* (7) | sh | `tools/deployment/` | 2 | 1 | review | deploy legacy, host obsoleto |
| Staging diagnostic/remediation one-off (6) | ci | `.github/workflows/{check-api-logs,check-role-case,diagnose-admin,test-login,fix-db-password,fix-line-endings}.yml` | 2 | 1 | review | residuo debug admin-login; SSH ora diretto |
| Manifest seed tooling (7) | py/sh | `infra/scripts/{detect,diff,fill,fix,patch}-manifest*`, `add-placeholder-*` | 3 | 1 | review | ban BGG #2123; twin patch .py/.sh |
| DB mirror one-off (2) | py | `infra/scripts/{mirror-staging-to-dev-prod,rebuild-dev-from-staging}.py` | 3 | 1 | review | superati da snapshot flow |
| infra one-off vari | sh | `infra/scripts/{reset-staging-beta0,benchmark-pdf-pipeline,evaluate-kb-coverage,lint-deps-imagesharp,query-pipeline-metrics,seed-test-game,reembed-vectors}.sh` | 3-4 | 2 | review | non wired Makefile/CI; occhio umano |
| make staging-core / make test-visual | make | `infra/Makefile` | 2 | 1 | review | staging-core deprecated; test-visual gate rimosso 2026-05-20 |
| test-visual.yml + pnpm test:e2e:visual | ci/pnpm | `.github/workflows/test-visual.yml` | 2 | 2 | review | visual gate 'green theatre' superato |
| tools/docs/* + tools/setup one-off | js/sh | `tools/docs/`, `tools/setup/` | 3 | 1-2 | review | non wired; verificare allineamento Scalar |
| .claude/helpers misc + statusline.sh | sh | `.claude/helpers/{adr,ddd,security,auto-commit,github,mcp}*`, `.claude/statusline.sh` | 2 | 1 | review | vicini alle convenzioni ma non wired |
| RAG/QA runner attivi | ps1 | `tools/run-rag-validation-20q.ps1`, `tools/migrate-local-to-s3.ps1` | 6-7 | 3 | keep | referenziati in runbook attivi |
| release-gate bot | node | `scripts/release-gate/` | 9 | 8 | keep | 5 workflow + suite vitest |
| Core make dev loop | make | `infra/Makefile` | 10 | 9 | keep | Quick Reference |
| Core CI pipeline | ci | `.github/workflows/{ci,dev-fast,deploy-staging}.yml` | 10 | 10 | keep | release/feature gate |
| Core dev commands | pnpm/dotnet | `apps/web/package.json`, `apps/api/` | 10 | 10 | keep | dev/test/build quotidiano |
| Operations Manual + runbook attivi | doc | `docs/for-developers/operations/` | 8-9 | 6 | keep | source of truth ops |

---

## 4. Cleanup — Wave 1 · DELETE NOW (rischio nullo)

Rimozione a rischio nullo (nessun wiring Makefile/CI/`package.json`/hook; one-off consumati o scaffolding esterno). **~15.026 LOC · ~105 file**:

- `tools/archive/2025-11-closed-issues/` — **~6000 LOC** — 43 script per issue chiuse Nov 2025, cartella archive esplicita.
- `tools/archive/2025-11-guid-fixes/` — **~1400 LOC** — 9 `fix-*.ps1`, migrazione GUID completata.
- `tools/archive/migration-scripts/` — **~1000 LOC** — 7 migrazioni FE/BE per-fase applicate (README storico).
- `.claude/helpers/swarm-*.sh` (3) — **~1324 LOC** — scaffolding claude-flow, nessun `.claude/hooks` né blocco hooks in `settings.json`.
- `.claude/helpers/{checkpoint,daemon,worker,perf,health}*` (6) — **~1130 LOC** — runtime statusline claude-flow non lanciato.
- `.claude/helpers/v3*.sh` (5) — **~792 LOC** — tracking epic v3 del tool claude-flow, non MeepleAI.
- `.claude/helpers/{learning,guidance,pattern}*` (5) — **~657 LOC** — hooks neural mai registrati.
- `.claude/helpers/quick-start.sh` — **~19 LOC** — solo echo comandi claude-flow.
- `tools/{check,fix}-chess-game.ps1`, `upload-chess-rulebook.ps1`, `check-doc-status.ps1`, `find-valid-game-for-validation.ps1`, `upload-and-validate-rag.ps1`, `test-{kb-ask,qa-endpoint,rag-simple,single-question}.ps1` — **~690 LOC** — diagnostici AGT-018 con credenziali admin + GameId Chess hardcoded (**la rimozione elimina anche password in chiaro**).
- `tools/{add-test-traits,migrate-to-shared-testcontainers,remove-regions,update-epic-3167}.ps1` + `tools/migrate-admin-api-calls.sh` — **~680 LOC** — one-off issue #2031/#1679/#3167 chiuse.
- `scripts/seed-dashboard-data.{ps1,sql}` + `scripts/README-seed-dashboard.md` — **~376 LOC** — epic Gaming Hub Dashboard archiviato.
- `tools/fix-test-trait-constants.ps1` + `-v2.ps1` — **~300 LOC** — coppia v1/v2 duplicata, migrazione trait applicata.
- `tools/issue-triage-analysis.md` — **~262 LOC** — snapshot triage storico.
- `create-issues.sh` — **~195 LOC** — batch `gh issue create` epic #4068 consumato.
- `apps/web/scripts/{fix-vitest-imports,migrate-jest-to-vitest}.sh` — **~134 LOC** — codemod Jest→Vitest #1503 completato.
- `scripts/rebucket-pdfs.ps1` + `scripts/rebucket-pdfs-s3.sh` — **~67 LOC** — twin migrazione storage #480 consumata.

**Aggiunte 2° giro (chiusura lacune) — +10 file, ~3.297 LOC:**

- `tools/setup/.dotnet-install.sh` — **~1888 LOC** — copia vendored dell'installer ufficiale Microsoft, **zero invocazioni**: il provisioning runner scarica lo script fresco da `https://dot.net/v1/dotnet-install.sh` (`infra/runner/setup-vm.sh:45`), non da qui. Singolo file più pesante dell'audit.
- `tools/setup/setup-github-labels.sh` — **~47 LOC** — bootstrap label/milestone MVP (Sprint 1-5, due 2025-02/03) consumato; milestone già scadute.
- `scripts/testing/{test-oauth-health,test-runbooks,test-services}.ps1` — **~564 LOC** — diagnostici standalone (test-runbooks legato a issue #2004 chiusa, credenziali admin hardcoded); superati da `GET /api/v1/health` + `make logs`.
- `scripts/development/e2e-demo-setup.ps1` — **~380 LOC** — setup demo v1.0.0 superato da `make dev` / `dev-from-snapshot` / `seed-index`; include import BGG user-side in tensione col freeze #2123.
- `infra/scripts/{fill-skeleton-entries,fix-manifest-mismatches,add-placeholder-fallback-images,rebuild-dev-from-staging}.py` — **~418 LOC** — migrazioni seed-catalog blob (plan archiviato 2026-04-08) e fix puntuali (3 bggId hardcoded) già applicati ai manifest committati.

### Riferimenti "dangling" da ripulire nello stesso commit
Il critico ha confermato **zero referenze runtime attive**; restano solo 3 riferimenti solo-doc da aggiornare contestualmente:
- `.claude/statusline.sh:164` cita `swarm-comms.sh` (guardato da `[ -x ]`; lo statusline non è comunque agganciato ad alcuna `statusLine` key) → rimuovere il blocco o l'intero `statusline.sh`.
- `scripts/README.md:70` cita `seed-dashboard-data.ps1`.
- `apps/web/docs/vitest-migration-guide.md` cita i 2 script di migrazione.

---

## 5. Cleanup — Wave 2 · CONSOLIDA

Duplicati cross-platform / logica ripetuta / alias. **10 coppie cross-platform `.sh`/`.ps1`** identificate (2 — `run-with-docker`, `setup-test-env` — sono twin **legittimi** Windows+Linux da NON toccare). Risparmio stimato **~1.500 LOC**:

> **Esito verifica Wave 2 (2026-07-31, worktree — 6 agenti per-item)**: eseguiti **7 delete confermati (~963 LOC)** + rimozione alias `test:visual:ci` + `refactor-test-isolation.sh` + rename `auto-validate.yml`. La stima "~1.500 LOC / 10 coppie" era **ottimistica**: molti twin sono risultati **mantenuti** e sono stati **tenuti** — modulo `db-snapshot` PS1 (`db-save-state`/`db-restore-state`/`db-snapshot-common.psm1`) wired in Makefile + coperto da Pester; `tools/secrets/*` (schema `.txt` Docker-Secrets, nessun equivalente `make`); `cleanup-testcontainers.ps1` (superset non equivalente al `.sh`); `measure-coverage.ps1` (senza twin); i `db-*/seed-*.ps1` (workflow SSH-staging distinti dai `.sh` bucket-based). Premesse audit **corrette in verifica**: `validate-doc-links.ps1` NON è coperto da `scan-broken-links.py` (il gate `docs-linkcheck.yml` usa **lychee**) → tenuto; `start-dev.ps1` è wired in `tasks.json`/ops-manual → tenuto. **Delete eseguiti**: `cleanup-caches.ps1`, `coverage-trends.ps1`, `open-dual-vscode.ps1`, `security-audit.ps1`, `patch-manifest-from-hashes.py`, `snapshot-publish.py`, `upload-seed-pdfs.py`.

- **security-audit** — `infra/scripts/security-audit.ps1` (120 LOC) duplica `security-audit.sh` (wired in Makefile). Unico entrypoint o rimuovi la variante non-wired. ~−60 LOC.
- **cleanup-caches / cleanup-testcontainers** — coppie `.ps1`/`.sh` in `tools/cleanup/` (687 LOC comb.) senza wiring; collassa su un entrypoint. ~−430 LOC.
- **coverage-trends** — `tools/coverage/{coverage-trends.ps1,.sh}` coppia + `refactor-test-isolation.sh` (23 LOC one-off delete). ~−300 LOC.
- **open-dual-vscode** — `tools/development/{.ps1,.sh}`; il README suggerisce l'alternativa nativa VSCode workspaces. ~−67 LOC.
- **Legacy seed/db ps1** — `infra/scripts/{seed-dump,seed-pull,seed-restore,db-dump,db-restore}.ps1` (500 LOC) sovrappone `snapshot-staging.sh`/`restore-*.sh` canonici. ~−250 LOC.
- **Seed blob publish legacy** — `infra/scripts/{snapshot-publish.py,upload-seed-pdfs.py,.sh}` superati da `seed-index-publish.sh`; twin `upload-seed-pdfs.py/.sh`. ~−125 LOC.
- **tools/secrets/*** (init/list/rotate, 393 LOC) — duplica `make secrets-setup`/`make secrets-sync` (`infra/secrets/setup-secrets.ps1` + `infra/scripts/sync-secrets.sh`); il doc security che li cita è stale (referenzia `validate-secrets.sh` inesistente).
- **mvp-smoke-test.sh / start-dev.ps1** — dup funzionali di `smoke-set.sh` e `make dev`; nome legacy, non wired.
- **validate-doc-links.ps1** — dup cross-lang di `scan-broken-links.py` (già gate in `docs-linkcheck.yml`); consolida sul Python.
- **auto-validate.yml** — display name "Validate Workflows" identico a `validate-workflows.yml` (job/scopo diversi): rinomina o unifica per non confondere la checks UI.
- **pnpm chromatic** — `test:visual:ci` è comando identico a `chromatic:ci`; collassa gli alias.
- **pnpm scraper:*** — passthrough a `tools/game-scraper` con `scraper:validate` mancante: allinea o invoca il tool direttamente.
- **patch-manifest-from-hashes** (2° giro) — `infra/scripts/patch-manifest-from-hashes.py` (166 LOC) è il gemello cross-platform dichiarato di `patch-manifest-from-hashes.sh` (docstring: *"Python equivalent of …"*); nessun consumatore invoca né `.sh` né `.py`. Consolidare su un unico artefatto o eliminare entrambi col cluster seed-blob legacy.

---

## 6. Cleanup — Wave 3 · REVIEW (decisione umana)

~69 procedure che richiedono decisione umana prima del taglio. Cluster principali:

1. **Percorsi backup/deploy paralleli** (`tools/backup/*` ~2500 LOC, `tools/deployment/*` ~837 LOC): il canonico è `infra/scripts/backup*.sh` + `deploy-staging.sh` (wired Makefile/cron/GHA). `tools/backup` referenzia **Qdrant** (non più usato, si è su pgvector) e usa host obsoleto. Forte sospetto di codice morto, ma **~3300 LOC** meritano conferma esplicita prima del delete.
2. **Workflow diagnostici/remediation one-off** (`check-api-logs`, `check-role-case`, `diagnose-admin`, `test-login`, `fix-db-password`, `fix-line-endings`.yml): residuo di un incidente admin-login; l'accesso SSH diretto a staging li supera. Candidati delete.
3. **One-off infra non wired** (`reset-staging-beta0`, `benchmark-pdf-pipeline`, `evaluate-kb-coverage`, `lint-deps-imagesharp`, `query-pipeline-metrics`, `seed-test-game`, `reembed-vectors`, Manifest seed tooling, DB mirror): decidere se promuovere a gate CI (es. `lint-deps-imagesharp`) o archiviare. `reembed-vectors` potenzialmente superato da `reindex-corpus`.
4. **Superficie visual-gate** (`make test-visual`, `test-visual.yml`, `pnpm test:e2e:visual`, `compare:mockups`, `visual-docs`/`docs:*`, `audit-mockups:*`): il visual gate è stato **rimosso il 2026-05-20** ma i target sopravvivono ambigui — riconciliare (probabile delete di gran parte).
5. **.claude/helpers misc + statusline** (`adr/ddd/security/auto-commit/github/mcp`, `statusline.sh` vs `statusline.mjs`): tema vicino alle convenzioni MeepleAI ma non wired; valutare riuso mirato vs delete in blocco col resto claude-flow.
6. **Runbook one-off consumati** (`2026-05-19-r2-orphan-cleanup`, `storage-layout-migration`, `2026-06-11-issue-2116-post-deploy-smoke`, `infrastructure-deployment-checklist`): spostare in `.docs-archive/` una volta confermato che le migrazioni sono chiuse.
7. **Tooling QA/dev standalone** (`run-golden-dataset-evaluation`, `generate-component-tests`, `dismiss-codeql-false-positives`, `migrate-to-private`, dev diagnostic helpers, wiki publishing): keep-as-tool vs delete secondo se il flusso a valle è ancora vivo.

---

## 7. Core da mantenere (non toccare)

Top procedure per utilità+frequenza:

- **Dev loop**: `make dev`/`dev-core`/`dev-down` (10/9), `pnpm dev` `build` `typecheck` `lint` `test` (10/10), `dotnet test` `run` `restore` `format` (10/10), `pnpm prepare` husky (7/10).
- **CI/CD**: `ci.yml` (10/10), `dev-fast.yml` (10/10), `deploy-staging.yml` (10/7), `dev-async.yml` (8/8), `security-scan.yml` (9/8), `notify-slack.yml` reusable (7/7), `auto-branch-policy.yml` (7/8), `dev-auto-revert.yml` (7/6), `auto-dependabot.yml` (7/6).
- **Release automation**: `scripts/release-gate/` bot (9/8) + trio reporting.
- **Ops critici**: `load-secrets-env.sh` entrypoint compose (8/8), `sync-secrets.sh`/`secrets-setup.ps1` (8-9/4-5), `backup.sh`+verify+restore-test (8/5) + cron, `infra/hetzner/backup.{sh,cron}` (7/8), `docker-proxy-watchdog` (5/6), `daily-disk-prune.sh` (6/6).
- **Quality gate**: `check-migration-safety.py` (7/6), `validate-workflows.js` (7/6), `scan-broken-links.py` (7/6), `secrets/validate.sh` (6/7), `lint:tokens`/`lint:bgg`/`test:a11y:e2e` (8-9/7-8).
- **Snapshot/RAG lifecycle**: pipeline `seed-index-*.sh` + `snapshot-{fetch,verify,restore}.sh` (7/3-4), `rag-smoke-assert.sh` (7/4), `title-health-assert.sh` (7/4), `k6 shared-config.js`+`baseline-compare` (8/4).
- **Doc source-of-truth**: Operations Manual (9/6), Rollback Runbook (8/3), Snapshot Seed Workflow (9/5), Git/Branch Workflow (7/4).

---

## 8. Verifica del critico & copertura

**Verdetto**: PASS con riserve minori. **Nessun falso-delete**: nessun path della Wave 1 ha riferimenti attivi. Verificati `.github/workflows/` (0 match), `.husky/` (0), `infra/Makefile` (0 overlap), `apps/web/package.json` (0 — l'unico `cleanup-*.ps1` attivo, `tools/cleanup/cleanup-test-processes.ps1`, **non** è in lista). Cluster `.claude/helpers/*` confermato dead (unwired in `settings.json`, solo `enabledPlugins`).

**Riserve non bloccanti**:
1. **Riferimenti solo-doc dangling** (3) — vedi §4; da ripulire nello stesso commit del delete.
2. **Discrepanza metrica** — `deleteCount = 16` conta gruppi/directory, non file (~115 reali); riconciliata in §1.
3. **Cartelle non coperte dalla `deleteList`** → **CHIUSE nel 2° giro** (§8-bis).

---

## 8-bis. Chiusura lacune (2° giro, 2026-07-31)

Le 4 cartelle segnalate dal critico sono state analizzate (4 agenti, 34 record, ognuno con auto-verifica delle referenze via Grep). Esito per cluster:

| Cluster | File | Esito |
|---|---|---|
| `infra/scripts/*.py` (manifest/mirror) | 8 nuovi | **4 delete** (`fill-skeleton-entries` 201, `fix-manifest-mismatches` 103, `add-placeholder-fallback-images` 61, `rebuild-dev-from-staging` 53 LOC — migrazioni consumate) · **3 review** (`detect-manifest-mismatches`, `diff-manifests`, `mirror-staging-to-dev-prod` — diagnostici read-only riutilizzabili) · **1 consolidate** (`patch-manifest-from-hashes.py` = twin del `.sh`) |
| `tools/setup/` | 6 | **2 delete** (`.dotnet-install.sh` **1888**, `setup-github-labels.sh` 47) · **1 keep** (`setup-test-environment.sh` — invocato da `quick-start.sh:16`) · **3 review** (`setup-n8n-service-account.ps1`, `setup-ollama.ps1` probabile legacy Ollama, `README.md` da sincronizzare) |
| `scripts/testing/` + `scripts/development/` | 4 | **4 delete** (`test-oauth-health`, `test-runbooks` #2004, `test-services`, `e2e-demo-setup` — diagnostici/setup superati) |
| `scripts/` root residui | 14 | **9 keep** (di cui **3 CI-wired confermati**: `lint-cross-lang-constants.sh` `ci.yml:394`, `scrub_bgg_manifest.py` `ci.yml:856`, `bootstrap_wikidata_qid.py` `ci.yml:860`; + `git-workflow.sh`, `audit-pdf-storage.sh`, `cleanup-orphan-pdfs.sh`, `setup-branch-protection.sh`, `build-claude-design-bundle.sh`) · **5 review** (`rewrite-docs-links.sh`, `enqueue/reverse-storage-migration.sh`, `initialize-kb.ps1`, `security-check-local.ps1`, `seed-default-agent.ps1+.sql`) |

**Lezione**: il 2° giro ha evitato **falsi-delete** su `scripts/` root — 3 script che "sembravano" residui sono in realtà gate CI attivi. La verifica-referenze per-file prima di ogni delete è non-negoziabile.

**Nota di correzione**: la premessa "README `tools/setup` cita un `quick-start.sh` inesistente" era **errata** — `quick-start.sh` esiste alla root del repo e invoca `setup-test-environment.sh` (che quindi resta `keep`).

**Copertura residua** (fuori scope dei 2 giri, per completezza futura): `apps/api/tests/setup-test-env.*`, `apps/web/e2e/admin-first-time-setup/*`, `tests/k6/utils/*`, `infra/scripts/tests/*` — già toccati dagli agenti del 1° giro come ancillari; nessun candidato delete emerso.

---

## 9. Piano d'esecuzione consigliato

Da eseguire su un feature branch dedicato (parent `main-dev`), a ondate separate per mantenere la revisione semplice:

1. **Wave 1 — DELETE NOW** (rischio nullo): rimozione dei ~115 file di §4 (1° giro + aggiunte 2° giro) + fix dei 3 dangling ref. `git rm` in un unico commit `chore(cleanup): remove archived/dead scripts (~18k LOC)`. Verifica post-delete: `pnpm typecheck`, `pnpm lint`, `make dev-core` smoke, grep di sicurezza sui basename rimossi.
2. **Wave 2 — CONSOLIDA** (basso rischio): collasso delle 8 coppie cross-platform non-legittime + alias pnpm + rinomina `auto-validate.yml` + twin `patch-manifest-from-hashes.py/.sh`. Un commit per famiglia.
3. **Wave 3 — REVIEW** (richiede decisione umana): triage dei 7 cluster del §6 + i ~12 residui `review` emersi dal 2° giro (§8-bis), preferibilmente aprendo una issue di tracking cleanup.

**Note operative**:
- Tutte le rimozioni sono reversibili via git finché il branch non è merge-ato.
- Non toccare i twin legittimi `run-with-docker.{sh,ps1}` e `setup-test-env.{sh,ps1}` (Windows+Linux entrambi necessari).
- Aggiornare la baseline "Known Flaky Tests"/CLAUDE.md non è richiesto: nessun test dipende dagli script rimossi.
