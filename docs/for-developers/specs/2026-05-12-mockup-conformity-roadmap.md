# Mockup Conformity Roadmap & Visual Fidelity Restoration

| Field | Value |
|---|---|
| **Status** | draft |
| **Date** | 2026-05-12 |
| **Author** | spec-panel (Wiegers·Cockburn·Fowler·Adzic·Crispin·Nygard) |
| **Upstream dependency** | [`2026-05-11-design-system-deversioning.md`](./2026-05-11-design-system-deversioning.md) — Stage 2 path-migration ✅ chiuso 2026-05-11 (#1025), WS-A unblocked |
| **Related** | [`2026-04-26-v2-design-migration.md`](./2026-04-26-v2-design-migration.md), [`v2-token-system.md`](../frontend/v2-token-system.md), [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md) |
| **Branch convention** | `feature/issue-{N}-mockup-conformity-{ws}` (ws ∈ {tokens, nanolith, gate, states, linter, ownership}) |

## 1. Problem statement

Un audit visivo del 2026-05-12 (spec-panel critique mode, 4 route campionate) ha quantificato un gap medio **75–85%** tra screenshot live e mockup canonici in `admin-mockups/design_files/`. Il giudizio dell'utente «siamo molto distanti dai mock» è accurato e ripetibile.

### 1.1 Audit findings recap

| Route | Screenshot evidenza | Mockup di riferimento | Gap stimato |
|---|---|---|---|
| `/library` | grid cards vuote, hero minimal, 4 stat basic | `sp4-library-desktop.html` (hero ricco, cover-art, microgrid) | **~70%** |
| `/sessions/[id]/live` | timer + 5 tool button + classifica vuota | `sp4-session-live.html` (multi-panel: events/players/dice/scoring/agent/phase) | **~85%** |
| `/sessions/[id]/chat-ai` | bubble user+agent, fallback message, **zero citation/confidence/prompts** | `sp4-game-chat-tab.html` (citation-chip kb-tinted, confidence badge, suggested prompts A·B·C·E·F, sidebar Tutor↔Arbitro) | **~80%** |
| `/library/[gameId]` (nanolith) | error state «Gioco non trovato» | `nanolith-runthrough-game-detail.html` (hero card + 4-stat + connection-bar 5-pip + CTA «Avvia libro game») | **~95% (funzionalmente broken)** |

### 1.2 Cause sistemiche (panel consensus)

1. **Doppio design system in convivenza** — `admin-mockups/design_files/tokens.css` (canonical, 9 entity HSL triplets + spacing 4px grid + Quicksand/Nunito/JetBrains Mono) vs il frontend che usa palette frammentate in `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` + shadow palette `apps/web/src/lib/sessions-summary/entity-text-tokens.ts` (Wave D.3 hotfix #756). Risultato: shadow, border-radius, font-family, color scale, glass-blur, gradient hero divergenti.

2. **Freeze attivo** ~~(umbrella #1023, audit #1024) — la causa originaria è riconosciuta dal de-versioning spec ma in attesa di Stage 2 codemod~~ → **Aggiornamento 2026-05-12**: Stage 2 di-versioning chiuso 2026-05-11 (#1025 / PR #1032), Wave A-D `v2/` paths rinominati a `components/features/**` + `components/ui/**`. Freeze resta solo per nuovi file sotto `v2/**` (cleanup di legacy code coexistence). WS-A non è più bloccato.

3. **Spec-as-comment non spec-as-test** — i mockup contengono vincoli SMART nei commenti HTML (es. `sp4-game-chat-tab.html:32-37`: «90% citation explicit», «confidence <70% marker», «latency P95 ≤ 10s»). Nessun gate CI estrae quei vincoli per validarli runtime.

### 1.3 Existing infrastructure assessment

| Asset | Funzione attuale | Gap |
|---|---|---|
| `.github/workflows/visual-regression-mockups.yml` (issue #571) | mockup HTML → baseline PNG committed (verifica stabilità mockup) | NON confronta route live |
| `.github/workflows/visual-regression-migrated.yml` (Wave A/B) | route live → baseline PNG committed (verifica stabilità implementazione) | NON confronta col mockup |
| `apps/web/scripts/serve-mockups.cjs` | static server porta 5174 per mockup HTML | Riusabile per CI gate WS-C |
| `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/` | PNG canonical Linux x86-64 dei mockup | Riusabile come reference per WS-C |
| `admin-mockups/design_files/tokens.css` + `components.css` | design system canonical | NON importato nel build FE |
| Sentinel pattern `?state=...` URL override (Wave B.1/B.2/B.3) | visual state coverage per 4-5 stati di route migrate | Non uniformato cross-route, mancano stati dichiarati in `<!-- Stati: -->` mockup |

> 🤔 **Insight**: due workflow visual-regression esistono in parallelo ma misurano cose diverse. Nessuno dei due risponde alla domanda «la route implementata corrisponde al mockup?». WS-C colma esattamente questo buco.

## 2. Goals & Non-goals

### Goals

- **G1** — Unificare design tokens: `tokens.css` mockup importato in `apps/web/src/styles/`; eliminare shadow palette (`meeple-card/tokens.ts`, `sessions-summary/entity-text-tokens.ts`).
- **G2** — Risolvere bug funzionale `/library/[gameId]` per Nanolith (default state al posto di not-found).
- **G3** — Aggiungere gate CI mockup-to-route (estensione dei due workflow esistenti, non sostituzione).
- **G4** — Garantire coverage visual per ogni stato dichiarato nel commento mockup (`<!-- Stati: default · loading · error · ... -->`).
- **G5** — Estrarre vincoli SMART dai commenti HTML mockup come acceptance criteria validabili runtime + GitHub issue auto-generate.
- **G6** — Standardizzare mockup ownership: ogni HTML ha metadata `@route` + `@last-verified` + `@status`; CI rileva drift.

### Non-goals

- **NG1** — Re-implementare Stage 2 codemod di-versioning (cross-ref [spec di-versioning](./2026-05-11-design-system-deversioning.md#stage-2--path-migration-rename-atomico--import-fix)).
- **NG2** — Re-implementare Stage 3 cluster-by-cluster fixes (cross-ref [stesso spec](./2026-05-11-design-system-deversioning.md#stage-3--conformity-fixes-cluster-by-cluster)).
- **NG3** — Modificare i mockup HTML canonici sotto `admin-mockups/design_files/` (single source of truth, read-only per il roadmap).
- **NG4** — Toccare backend, AI Python services, n8n.
- **NG5** — Introdurre integrazione esterna (Figma/Penpot) per mockup ownership: il sistema resta markdown + HTML driven, no SaaS dependency.

## 3. Workstreams

Sei workstream (A–F) con dipendenze esplicitate in §4 sequencing.

### WS-A — Token unification (single source of truth)

**Owner**: frontend-architect · **Status**: ✅ ready (Stage 2 di-versioning chiuso 2026-05-11 via #1025 / PR #1032) · **Effort**: 3–5 giorni

**Problem**: tre palette entity colors in convivenza (canonical mockup, shadow MeepleCard, shadow Wave D.3) con valori HSL divergenti. Esempio entity=game:
- mockup `tokens.css:7` → `--c-game: 25 95% 45%`
- shadow MeepleCard (PR #542) → `entityColors.game = {h:25,s:'95%',l:'45%'}` — ALLINEATO
- shadow text-on-light Wave D.3 → `ENTITY_TEXT_HSL.game = 'hsl(25, 95%, 32%)'` — DARKER per WCAG AA su light-bg
- frontend dark mode entity nel page (#1035) → ulteriore variante

**Goal**: collassare le tre palette in un unico `tokens.css` importato. Le varianti text-on-light vengono espresse come **token derivati** (`--c-game-text: hsl(25 95% 32%)`) nella stessa fonte, non come file separato.

**Deliverable**:
1. Copy `admin-mockups/design_files/tokens.css` → `apps/web/src/styles/tokens.css`. Strategia: **copy non symlink** (Windows compat), automation di sync via `scripts/sync-mockup-tokens.ts` con CI check.
2. Estendere `tokens.css` con sezione `Entity text-on-light` (8 colori darker, contrast table commentata).
3. Import in `apps/web/src/app/globals.css` come root layer.
4. Update `apps/web/tailwind.config.ts` per leggere CSS vars (`colors: { game: 'hsl(var(--c-game))', ... }`).
5. Rimuovere `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` (shadow).
6. Rimuovere `apps/web/src/lib/sessions-summary/entity-text-tokens.ts` (shadow Wave D.3 #756).
7. Update tutti i consumer `import { entityColors } from ...` → CSS vars o Tailwind classes (`text-game`, `bg-game/10`, ecc.).

**Acceptance criteria**:
- **AC-A.1**: `tokens.css` è l'unico file che dichiara `--c-{entity}` (verifica via `grep --c-game apps/web/src | wc -l == 1`).
- **AC-A.2**: contrast WCAG AA verificato per ogni entity colour in entrambe le direzioni (text-on-white ≥ 4.5:1; white-text-on-bg ≥ 4.5:1). Tabella commentata in `tokens.css`.
- **AC-A.3**: `pnpm test:visual:migrated` e `pnpm test:visual:mockups` invariati (baselines stable).
- **AC-A.4**: bundle size delta `pnpm build` < 2%.
- **AC-A.5**: CI check `scripts/sync-mockup-tokens.ts --verify` blocca PR se `apps/web/src/styles/tokens.css` diverge da `admin-mockups/design_files/tokens.css`.

**Failure modes**:
- shadcn/Tailwind theme collision con CSS vars → mitigation: namespace `--c-*` per entity, `--theme-*` per shadcn primitives.
- Wave D.3 a11y regression su light-mode → mitigation: token text-on-light derivati nello stesso file, contrast table committata.

**Rollback**: revert PR — codebase ritorna a triple-palette stato (regressione visiva minore, no break runtime).

---

### WS-B — Nanolith detail page bugfix

**Owner**: frontend-dev · **Status**: hot (independent, no upstream) · **Effort**: 1–2 giorni

**Problem**: lo screenshot `library-post-pull-2026-05-12.png` mostra Nanolith in collection ("Nanolith 7.5", row 4). Lo screenshot `nanolith-detail.png` su `/library/{gameId}` mostra «Gioco non trovato». Bug funzionale documentato: `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx:76` collassa `!gameDetail` in not-found senza distinguere «assente» da «errore lookup».

**Hypothesis**: probabile mismatch tra `gameId` (BGG id, shared catalog id, library entry id, personal library id). Conferma: esiste screenshot `nanolith-detail-shared-id.png` come variante separata, suggerendo che lo stesso gioco ha più id traversale.

**Goal**: con persona Aaron (badsworm@gmail.com, superadmin, Nanolith in collection, KB indicizzato — vedi commento `nanolith-runthrough-game-detail.html:11-14`), apertura `/library/nanolith` mostra **default state** (hero-card + 4-stat + connection-bar 5-pip + CTA primary «Avvia libro game»).

**Deliverable**:
1. Investigation: trace `useLibraryGameDetail(gameId)` (vedi `page.tsx:35`) → endpoint backend → id resolution.
2. Documentare id taxonomy in `docs/for-developers/architecture/game-id-taxonomy.md` (likely: BGG id ↔ SharedGameCatalog id ↔ UserLibrary entry id).
3. Fix: hook normalize, route param coercion, OR endpoint correction.
4. Distinguere visualmente `error` (lookup failed, retry CTA) da `not-found` (verified absent, suggest add CTA) — separazione conforme al mockup (`Stati: default · loading · error · not-found`).
5. Test: integration test cross-id resolution + E2E Playwright con fixture Aaron persona.

**Acceptance criteria**:
- **AC-B.1**: Aaron persona + `/library/nanolith` → default state visibile (hero + CTA).
- **AC-B.2**: stato `error` distinto da `not-found` nell'UI (testo, icona, CTA diversi).
- **AC-B.3**: E2E test `e2e/regression/library-game-detail-nanolith.spec.ts` blocca regressione.
- **AC-B.4**: Game id taxonomy doc esiste e cross-linka almeno BGG/Shared/Library schemi.

**Failure modes**:
- Bug è backend-side (id stripping in API) → escalation a backend-dev, parallel issue su BC `GameManagement` o `UserLibrary`.
- Fix tocca soft-delete query filter (Nanolith potrebbe essere soft-deleted in SharedGameCatalog) → review pattern in CLAUDE.md "Soft Delete".

**Rollback**: revert PR — torna al «not-found» univoco (regressione UX minore vs status quo, no break).

---

### WS-C — Mockup-to-route CI gate

**Owner**: cicd-engineer · **Status**: depends on WS-A · **Effort**: 2–3 giorni

**Problem**: due workflow visual-regression esistenti misurano `mockup→baseline` e `route→baseline`, ma nessuno misura **`route→mockup`**. Il gap quantificato al §1.1 esiste perché non c'è gate che lo prevenga.

**Goal**: nuovo workflow `.github/workflows/visual-regression-conformity.yml` che, per ogni route registrata in mockup-ownership map (vedi WS-F):
1. Renderizza mockup HTML via `serve-mockups.cjs` (porta 5174) + screenshot Playwright Chromium headless (1280×720 + 375×740).
2. Renderizza route live via Next.js dev server + screenshot stessi viewport.
3. Diff con `pixelmatch` (threshold 5% default, configurabile per-route).
4. Pubblica diff artifacts + commento su PR.

**Deliverable**:
1. `.github/workflows/visual-regression-conformity.yml` (NEW). Pattern: estende `visual-regression-mockups.yml` (vedi `lines 1-150`).
2. `apps/web/e2e/visual-conformity/{route}.spec.ts` per ciascuna route mappata.
3. `apps/web/playwright.config.ts`: nuovo project `conformity-desktop` + `conformity-mobile`.
4. `apps/web/scripts/conformity-runner.ts` orchestra mockup-render + route-render + diff.
5. `docs/for-developers/testing/frontend/mockup-conformity.md` documenta workflow + come aggiornare threshold.

**Acceptance criteria**:
- **AC-C.1**: gate triggera su PR il cui changeset interseca path mappati in `mockup-ownership.bootstrap.json` (vedi AC-C.7) **oppure** tocca `apps/web/src/styles/design-tokens-canonical.css`, `apps/web/tailwind.config.*`, o `admin-mockups/design_files/**`. Esplicito **exclude** per `apps/web/src/components/ui/**` (primitive system-wide, non bound a singola route).
- **AC-C.2**: diff calcolato via `pixelmatch` con parametri espliciti:
  - per-pixel sensitivity: `pixelmatch.threshold = 0.1` (YIQ delta, hardcoded in `conformity-runner.ts`)
  - aggregato pass criterion: `mismatchedPixels / totalPixels < ratioPerRoute` (default `0.05`, override per-route in `mockup-ownership.bootstrap.json` campo `conformityRatio`)
  - formula referenziata nel doc `mockup-conformity.md` con esempio numerico.
- **AC-C.3**: failure produce artifact `conformity-diffs-{run}` con triplet (mockup.png, route.png, diff.png) per ciascuna route fallita. **Retention**: 14 giorni per PR runs, 90 giorni per `main-dev` runs (configurato in workflow `actions/upload-artifact retention-days`).
- **AC-C.4**: commento PR include link artifact + screenshot inline + percentuale diff per route (formula AC-C.2). Commento è **sticky** (replace su re-run, no spam).
- **AC-C.5**: waiver system — il PR gate è saltabile tramite label `conformity-waiver`, con i seguenti sub-criteri concreti:
  - **AC-C.5.1 (rationale enforcement)**: workflow separato `conformity-waiver-validator.yml` è **required check** quando il PR ha la label `conformity-waiver`. Cerca nei commenti del PR un blocco marcatore strutturato:
    ```
    > Conformity waiver rationale:
    > Reason: <free text, min 40 char>
    > Expiry: <YYYY-MM-DD, optional, default = now+30d, max = now+90d>
    > Routes: <space-separated route ids from mockup-ownership.bootstrap.json>
    ```
    Il check fallisce (red, blocking) se il blocco è assente, malformato, o `Expiry` fuori range. Autore del commento deve essere uno tra: PR author, repo maintainer, member di `area/frontend`.
  - **AC-C.5.2 (debt-issue dedup)**: workflow `conformity-debt-issue.yml` (triggered su label-add) calcola dedup key `sha256(PR_number || sorted(route_ids) || iso_week(now))`. Query issue aperte con quella key in body header marker `<!-- waiver-key: {key} -->`. Se trovata, comment-update invece di create. Se PR è chiusa unmerged, hook chiude la debt-issue (`closed_by: auto-cancelled, PR not merged`).
  - **AC-C.5.3 (expiry semantics)**: ogni debt-issue ha campo `Expiry` nel body (parsed dal rationale, default 30gg). Issue body include header machine-readable:
    ```
    <!-- conformity-debt: routes=library,library-game-detail; expiry=2026-06-13; waiver-key=ABC123 -->
    ```
  - **AC-C.5.4 (cross-branch gate)**: workflow `conformity-debt-gate.yml` è **required check** su PR `main-dev → main-staging` e `main-staging → main`. Enumera issue aperte con label `conformity-debt`, parsing del marker `expiry=`, fail se almeno una ha `expiry < now`. Output console + sticky PR comment elenca le issue scadute per remediation manuale.
  - **AC-C.5.5 (audit log, git-based)**: workflow waiver crea/aggiorna `docs/for-developers/audits/conformity-waivers.md` via auto-PR (peter-evans/create-pull-request@SHA-pinned). Aggiunta è in fondo, sezione per anno-mese, formato tabellare. **"Append-only"** è inteso come **convention** (git history mostra ogni rimozione, code review detecta tampering). Non è enforced cryptographically nello scope WS-C; sufficiente come audit trail per il contesto del progetto.
  - **AC-C.5.6 (closing conditions)**: debt-issue si chiude in tre modi:
    1. **Auto-close su PR remediation**: PR che include `Closes #<debt-issue-number>` e supera conformity gate green → workflow chiude issue con commento `closed_by: remediated by PR #N`.
    2. **Manual re-validate**: maintainer applica label `waiver-revalidated` su una PR che dimostra che il routes mappati ora conformano alla baseline. Workflow chiude debt-issue.
    3. **Expiry passed**: se `expiry < now` senza chiusura → no auto-close, ma AC-C.5.4 fa fail su merge a main-staging. Reviewer deve decidere manualmente: estendere expiry (nuovo waiver) oppure remediare.
  - **AC-C.5.7 (observability)** (opzionale, Phase 4c): workflow weekly `conformity-debt-summary.yml` publishes `docs/for-developers/audits/conformity-waivers-summary.md` con top routes by debt count, oldest expiring, active count. Costo basso, segnale operativo.
- **AC-C.6**: mockup baselines committed sotto `apps/web/e2e/visual-conformity/__mockup__/{route-slug}.{viewport}.png`. Rigenerati **solo** via workflow dedicato `bootstrap-mockup-baselines.yml` (manual `workflow_dispatch` + auto-trigger su `admin-mockups/design_files/**` change con PR auto-create). Gate `visual-regression-conformity.yml` **non** rigenera mai baselines: confronta solo route live vs PNG committed.
- **AC-C.7**: WS-C ships con bootstrap minimal `apps/web/e2e/visual-conformity/mockup-ownership.bootstrap.json` hardcoded a **2 route**: `/library` (mockup `sp4-library-desktop.html`, Wave B.3 stable) e `/library/[gameId]` (mockup `nanolith-runthrough-game-detail.html`, sinergico con WS-B/WS-D Exemplar). Schema completo + auto-discovery sono scope WS-F. JSON schema sidecar `mockup-ownership.schema.json` versionato in repo.
- **AC-C.8**: determinismo cross-environment garantito da:
  - workflow runner pinned `ubuntu-22.04` (x86-64), NO matrix multi-OS
  - mockup baselines bootstrap MUST eseguire su stesso runner (CI-only generation, no local commit di baseline)
  - `serve-mockups.cjs` inietta font stack canonico `'Inter', -apple-system, system-ui, sans-serif` + preload Inter via `<link rel="preload">` con SHA-384 integrity
  - React UMD pinned a `18.3.1` con SRI hash in `serve-mockups.cjs`
- **AC-C.9**: workflow concurrency control: `concurrency: { group: conformity-${{ github.ref }}, cancel-in-progress: true }`. Flake tolerance: retry automatico 1× su failure (Playwright `retries: 1` solo per project `conformity-*`).

**Failure modes**:
- false positive da anti-alias / font rendering → mitigation: AC-C.8 font lock + per-route `conformityRatio` override.
- Tempo CI eccessivo (>10min) → mitigation: parallelizzazione per-route project Playwright + cache mockup screenshots invariati (mockup HTML hash → cache key).
- Mockup HTML usa React 18 UMD (vedi `sp4-library-desktop.html:16-17`) → rendering CI consistente? mitigation: AC-C.8 React version + SRI pin in `serve-mockups.cjs`.
- Waiver back-door permanente → mitigation: AC-C.5 expiration 30gg (override max 90gg) + AC-C.5.4 cross-branch gate su PR a main-staging/main.
- Waiver applicato senza rationale → mitigation: AC-C.5.1 required check `conformity-waiver-validator` parsifica marker strutturato.
- Duplicate debt-issue se label flap → mitigation: AC-C.5.2 dedup key `sha256(PR, routes, iso_week)` + comment-update fallback.
- "Append-only" audit log mutable via git → mitigation: AC-C.5.5 accetta git-as-audit-log; tampering visibile in code review.
- Baseline drift silenzioso post-mockup-change → mitigation: AC-C.6 auto-PR su `admin-mockups/design_files/**` change forza review esplicita.

**Rollback**: workflow disable via PR — gate scompare ma legacy gates restano. Baselines + bootstrap JSON retained come documentation reference.

---

### WS-D — Visual state coverage matrix

**Owner**: tester · **Status**: independent, parallel-safe con WS-A/B/C · **Effort**: 2–3 giorni (scaffold + 1 route exemplar) + ongoing per altre route

**Problem**: ogni mockup dichiara stati canonici nel commento header (es. `nanolith-runthrough-game-detail.html:14` → `Stati: default · loading · error · not-found`; `sp4-game-chat-tab.html:23-31` → `default · confidence-bassa · out-of-context · loading`). Il pattern sentinel `?state=...` Wave B.1/B.2/B.3 copre 4-5 stati per route migrate, ma:
- Non uniformato cross-route (Wave A non lo usa).
- Non lega gli stati alle dichiarazioni del mockup (drift possibile).

**Goal**: ogni stato dichiarato in `<!-- Stati: ... -->` ha una baseline PNG e un test Playwright. Generator script enforce mapping bidirezionale.

**Deliverable**:
1. Standard parser `scripts/extract-mockup-states.ts`: regex/AST estrae `Stati:` list da commenti HTML.
2. Output `apps/web/e2e/state-coverage/state-matrix.json`: `{ mockup_path, route, declared_states[], covered_states[], missing[] }`.
3. CI workflow `state-coverage-check.yml` fallisce se `missing.length > 0` per route in mockup-ownership map.
4. Refactor 1 route exemplar (proposta: `/library/[gameId]`) come canonical pattern, doc in `docs/for-developers/testing/frontend/visual-state-coverage.md`.
5. `?state=...` URL override unificato in `apps/web/src/lib/visual-test/state-override.ts` (gated da `NODE_ENV !== 'production' || IS_VISUAL_TEST_BUILD`).

**Acceptance criteria**:
- **AC-D.1**: 100% degli stati dichiarati nel mockup di ciascuna route registrata sono coperti (`missing.length === 0` in `state-matrix.json`).
- **AC-D.2**: generator output deterministico (run idempotente, ordering stabile).
- **AC-D.3**: fixture pattern uniformato (`apps/web/src/lib/{feature}/visual-test-fixture.ts`).
- **AC-D.4**: route exemplar (`/library/[gameId]`) cita lo stato `error ≠ not-found` (sinergico WS-B AC-B.2).

**Failure modes**:
- State proliferation (>20 stati per route → maintenance burden) → cap @ canonical 4-6, escalation per eccezioni motivate.
- Stati non visual-test-friendly (es. timing-dependent SSE) → exception list documentata (es. Wave D.3 `error` state escluso visual, coperto unit).

**Rollback**: workflow disable; matrix.json retained come documentation snapshot.

---

### WS-E — Spec linter (extract SMART constraints)

**Owner**: backend-dev (script side) + technical-writer (taxonomy) · **Status**: depends on WS-D parser infra · **Effort**: 2–3 giorni

**Problem**: i mockup contengono vincoli SMART nei commenti, esempio `sp4-game-chat-tab.html:33-37`:
```
Vincoli SMART (da spec G1+G5):
  - 90% query con citazione esplicita (pagina+sezione)
  - confidence < 70% → marker incertezza obbligatorio
  - latency P95 ≤ 10s con spinner accettabile
  - chat handoff <3s (G2 — non coperto qui ma compatibile con UX)
```
Lo screenshot `chat-ai-after-fix.png` non mostra **nessuna** citation chip né confidence indicator. Nessun gate CI estrae quei vincoli per validarli.

**Goal**: estrarre vincoli SMART da tutti i ~60 mockup HTML, generare automaticamente issue GitHub per AC mancanti, mantenere sync con mockup changes.

**Deliverable**:
1. `scripts/extract-mockup-smart-constraints.ts`: parser cerca pattern `Vincoli SMART:` / `Acceptance:` / `AC-*:` in commenti HTML, normalizza in struttura JSON.
2. Schema output: `{ mockup, constraints: [{ id, text, metric_type, target_value, applies_to }] }`.
3. `scripts/check-constraint-implementation.ts`: per ogni `constraint.metric_type`, controlla se esiste implementation marker nel codice (telemetria, UI element, test).
4. GitHub Action `mockup-spec-sync.yml`: weekly cron, diff `previous-extraction.json` vs `current-extraction.json`, crea issue per AC mancanti con label `mockup-spec-debt`.
5. Tassonomia metric_type documentata in `docs/for-developers/specs/mockup-smart-taxonomy.md` (citation, confidence, latency, accessibility, ...).

**Acceptance criteria**:
- **AC-E.1**: estrazione corretta su tutti i mockup attuali (~60 file), output JSON validato contro schema.
- **AC-E.2**: zero false-positive nelle issue auto-create (calibrazione su 5 mockup campione + manual review).
- **AC-E.3**: sync mantenuta via cron weekly + trigger PR su `admin-mockups/design_files/**` change.
- **AC-E.4**: tassonomia copre almeno: `citation`, `confidence`, `latency`, `accessibility`, `coverage`, `performance`.

**Failure modes**:
- Regex extract fail su commento HTML edge-case (multi-line, nested) → mitigation: HTML parser (parse5) invece di regex.
- Issue spam (>50 issue auto-create in primo run) → mitigation: dry-run mode default, manual approval batch-create.

**Rollback**: disable cron, retain JSON snapshot come reference documentation.

---

### WS-F — Mockup ownership metadata + change tracking

**Owner**: technical-writer + cicd-engineer · **Status**: depends on WS-C + WS-E (steady-state) · **Effort**: 2–3 giorni

**Problem**: nessun sistema tracking persistente collega mockup HTML → route implementata → last-verified timestamp. Quando un mockup cambia (designer update), nessun automatic trigger forza la re-verifica della route.

**Goal**: ogni mockup HTML ha metadata standard. CI rileva drift e flagga PR. Mapping bidirezionale mockup ↔ route.

**Deliverable**:
1. Header standard per ogni mockup HTML:
   ```html
   <!--
     @route /library/{gameId}
     @route-pattern dynamic
     @last-verified 2026-05-12
     @verified-by spec-panel
     @status canonical
     @conformity-threshold 5
   -->
   ```
2. `admin-mockups/mockup-ownership.json`: registro centralizzato (bidirezionale) auto-generato da header metadata.
3. `scripts/mockup-ownership-check.ts`: CI validation che metadata sono presenti + consistenti.
4. CI workflow `mockup-drift-detect.yml`: trigger su `admin-mockups/design_files/**` PR, se modifica un mockup → genera companion issue per route owned.
5. Dashboard `docs/for-developers/audits/mockup-ownership-status.md` auto-generato weekly: list di stato `canonical|partial|diverge|missing-route` per ogni mockup.

**Acceptance criteria**:
- **AC-F.1**: 100% dei mockup in `admin-mockups/design_files/*.html` hanno header metadata valido.
- **AC-F.2**: CI fail se metadata missing/malformed su PR che tocca `admin-mockups/design_files/**`.
- **AC-F.3**: mapping bidirezionale (`mockup → route` e `route → mockup`) consultabile via `mockup-ownership.json`.
- **AC-F.4**: drift detection produce issue companion entro 1 ora dalla PR mockup change.

**Failure modes**:
- Mockup orfani (no route ancora implementata) → status `pending-implementation`, no fail CI ma flag dashboard.
- Route multipli implementano stesso mockup → ammesso, `@route` accetta array.

**Rollback**: workflow disable; metadata header rimangono come documentazione passiva.

## 4. Sequencing & dependency graph

```
                                                      ┌──────────────┐
                                                      │  Stage 2     │
                                                      │  di-vers.    │  (UPSTREAM)
                                                      │  codemod     │
                                                      └──────┬───────┘
                                                             │
                       ┌─────────────────────────────────────┘
                       │
                       ▼
                ┌─────────────┐
                │   WS-A      │   tokens unification
                │  (blocker)  │   ~3-5d
                └──────┬──────┘
                       │
       ┌───────────────┴───────────────┐
       ▼                               ▼
┌─────────────┐                ┌─────────────┐
│   WS-C      │                │   WS-E      │
│  CI gate    │                │  linter     │
│  ~2-3d      │                │  ~2-3d      │
└──────┬──────┘                └──────┬──────┘
       │                              │
       └──────────────┬───────────────┘
                      ▼
               ┌─────────────┐
               │   WS-F      │   ownership tracking
               │  ~2-3d      │   (steady-state)
               └─────────────┘

INDEPENDENT (parallel-safe, can start NOW):
  ┌─────────────┐
  │   WS-B      │   nanolith bugfix (~1-2d, hot)
  └─────────────┘
  
  ┌─────────────┐
  │   WS-D      │   state coverage scaffold (~2-3d)
  │             │   feeds WS-E parser
  └─────────────┘
```

### Phase plan

| Phase | Workstream(s) | Start condition | Effort | Parallel-safe |
|---|---|---|---|---|
| **0 (NOW)** | WS-B, WS-D scaffold | none | 3-5d totale | YES (different files) |
| **1** | Stage 2 di-vers. codemod | umbrella #1023 unblocked | (per spec di-vers.) | NO (atomic) |
| **2** | WS-A | Phase 1 ✓ | 3-5d | NO (atomic, blocker for 3) |
| **3** | WS-C, WS-E | WS-A ✓ | 4-6d totale | YES (different workflows) |
| **4** | WS-F | WS-C ✓ + WS-E ✓ | 2-3d | N/A (final) |

**Total**: ~2.5 sprint con 1 dev FTE; ~1.5 sprint con 2 dev paralleli su Phase 0+3.

## 5. Failure matrix (cross-workstream)

| WS | Failure mode | Detection | Mitigation |
|---|---|---|---|
| WS-A | shadcn theme collision con CSS vars | `pnpm typecheck` fail | namespace `--c-*`/`--theme-*` |
| WS-A | Wave D.3 a11y regression light-mode | `axe-core` E2E | derived `--c-{entity}-text` tokens con contrast table |
| WS-A | Bundle size +>2% | `pnpm build` size diff | investigation, possibilmente split chunk |
| WS-B | Fix richiede backend change | API trace inconclusivo | escalation backend-dev, parallel issue su BC |
| WS-B | Test E2E flaky (id-based fixture) | CI retry count | stable fixture con DB seed deterministico |
| WS-C | False positive >5% (anti-alias) | Diff artifact review | per-region threshold, font-rendering lock |
| WS-C | CI tempo >10min | workflow timing | parallel project, cache mockup screenshots |
| WS-D | State proliferation (>20) | matrix.json count | cap canonical 4-6 stati, exception list documentata |
| WS-E | Regex extract miss | Manual audit campione | parse5 HTML parser invece di regex |
| WS-E | Issue spam (>50 in first run) | Issue count alert | dry-run default + manual batch approval |
| WS-F | Mockup orfani | Dashboard status | flag `pending-implementation`, no CI fail |
| WS-F | Drift detection slow | webhook latency | weekly cron fallback |

## 6. Out-of-scope / future work

- **Visual diff tooling avanzato** (Chromatic, Percy, Reg-cli): valutare post WS-C se threshold tuning insufficiente.
- **Design tokens v2** (CSS @layer, container queries): post-roadmap, separato spec.
- **Mockup interaction recording** (Playwright codegen → mockup HTML auto-update): future, separato spec.
- **Backend-driven UI conformity** (server-rendered HTML diff): non incluso, frontend-only.
- **Mobile native conformity** (React Native): non incluso, web-only.

## 7. Open questions for implementation

- **Q1**: WS-A — strategia sync `tokens.css` mockup → frontend: copy + CI verify vs git submodule vs build-time inline? Proposta: copy + CI verify (Windows compat, no submodule complexity).
- **Q2**: WS-B — fix Nanolith è frontend-only o richiede backend change? Investigation prima del PR kickoff.
- **Q3**: WS-C — quale route mappare per primo gate? Proposta: `/library/[gameId]` (sinergico WS-B fix) + `/library` (Wave B.3 stable baseline).
- **Q4**: WS-D — esempi route candidato per scaffold pattern? Proposta: `/library/[gameId]` (4 stati canonici già nel mockup).
- **Q5**: WS-E — soglia issue auto-create al primo full run? Proposta: dry-run + manual review prima della prima massa.
- **Q6**: WS-F — `@last-verified` aggiornato manualmente o auto-update post WS-C green? Proposta: auto-update.
- **Q7**: Umbrella issue tracking: aprire una umbrella issue (es. #1100) con 6 sotto-issue (WS-A..F)? Convenzione `feature/issue-{N}` lo richiede. Decidere al kickoff.

## 8. References

- **Roadmap origin**: spec-panel critique mode 2026-05-12 (questa conversazione) — 4 route campionate, gap 75-85% quantificato.
- **Mockup canonical source**: [`admin-mockups/design_files/`](../../../admin-mockups/design_files/) (~60 file HTML/JSX).
- **Upstream spec**: [`2026-05-11-design-system-deversioning.md`](./2026-05-11-design-system-deversioning.md) — Stage 2 ✅ chiuso 2026-05-11 #1025, WS-A unblocked.
- **Existing CI workflows**:
  - [`visual-regression-mockups.yml`](../../../.github/workflows/visual-regression-mockups.yml) (issue #571, mockup-to-baseline).
  - [`visual-regression-migrated.yml`](../../../.github/workflows/visual-regression-migrated.yml) (Wave A/B, route-to-baseline).
- **Token system existing docs**: [`v2-token-system.md`](../frontend/v2-token-system.md), [`v2-a11y-token-audit.md`](../frontend/v2-a11y-token-audit.md).
- **Shadow palettes da rimuovere**:
  - `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` (PR #542).
  - `apps/web/src/lib/sessions-summary/entity-text-tokens.ts` (Wave D.3 hotfix #756).
- **Nanolith bug evidence**: `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx:76` (`!gameDetail` → not-found).
- **Active freezes**: umbrella #1023, audit #1024 (CLAUDE.md → "🔒 Active Freezes").

---

**Sign-off required from**:
- [ ] Project owner — conferma priorità Phase 0 (WS-B hot, WS-D scaffold).
- [ ] Frontend architecture review — token strategy WS-A (copy vs submodule, Q1).
- [ ] CI/CD review — workflow extension WS-C (threshold calibration, Q3).
- [ ] PM — apertura umbrella issue + scheduling sprint (Q7).
