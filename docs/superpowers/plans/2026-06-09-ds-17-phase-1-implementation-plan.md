# DS-17 Phase 1 — Implementation Plan

**Data**: 2026-06-09
**Umbrella**: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063) — DS-17 Mockup-to-App Fidelity
**Phase**: Phase 1 (Quick wins, foundation)
**Sub-issues**: [#2069](https://github.com/meepleAi-app/meepleai-monorepo/issues/2069) DS-17-1 · [#2070](https://github.com/meepleAi-app/meepleai-monorepo/issues/2070) DS-17-2 · [#2071](https://github.com/meepleAi-app/meepleai-monorepo/issues/2071) DS-17-3 · [#2072](https://github.com/meepleAi-app/meepleai-monorepo/issues/2072) DS-17-4
**Origine**: `/sc:pm` planning request post `/sc:spec-panel` consolidation (vedi `docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md` — PR #2068)
**Status**: ⏳ AWAITING USER DECISION POINTS (vedi Sezione 5)

---

## 1. Goal

Implementare le 4 sub-issue Phase 1 in modo ordinato e parallelizzabile dove possibile, minimizzando il rework con un sequencing che rispetta le dipendenze logiche identificate dalla discovery.

**Successo = 4 PR shipped (~3.5-4.5gg single dev) che sbloccano Phase 2 (Storybook setup)**.

---

## 2. Pre-conditions

| Pre-cond | Status | Blocker? |
|----------|--------|----------|
| Spec doc PR #2068 merged | ⏳ in review | NO per DS-17-2/DS-17-4 (ortogonali). SÌ raccomandato per DS-17-1/DS-17-3 (referenziano spec) |
| Umbrella #2063 body 5 sub-issue linkati | ✅ done sess.46h | — |
| Discovery completata (workflow attuale) | ✅ done sess.46h (vedi §3) | — |
| User-locked decisioni Phase 1 (vedi §5) | ⏳ pending | **HARD BLOCKER** — start work solo dopo lock |

---

## 3. Discovery findings (workflow attuale rilevante)

### 3.1 `pnpm lint:tokens` (esistente — DS-2 era)

**File**: `apps/web/scripts/lint-tokens.mjs` (95+ LOC)

**Approccio attuale**:
- Usa `ESLint` API con flat config esistente
- Extract violazioni di `local/no-hardcoded-color-utility`
- Output: `audits/2026-05-12-token-violations.{json,md}`
- Cluster-aggregated markdown per readability

**Scope attuale**: `apps/web/src/**/*.{ts,tsx,jsx,js}` (no HTML, no CSS files standalone)

**Implicazione DS-17-2**: estensione richiede aggiungere un parser per HTML+CSS. ESLint nativo non parsa HTML. Due approcci:
- **Opt A**: aggiungere `@html-eslint/parser` + estendere flat config → uniforme, costoso (~0.5gg setup parser)
- **Opt B**: nuovo script parallelo `lint-tokens-mockups.mjs` che fa grep semantic con regex su `**/*.{html,css}` → veloce, meno robusto (~0.2gg setup)

Decisione tooling = **DP-2** in §5.

### 3.2 ESLint custom rules (esistenti)

**Path**: `apps/web/eslint-rules/`

Lista rules custom:
- `no-hardcoded-color-utility.js` ← rule chiave per token canonicalization
- `no-hardcoded-hex.js`
- `no-inline-hsl-v2.js`
- `no-incomplete-sanitization.js`
- `api-client-v1-prefix.js`

**Implicazione**: pattern "custom local rule" già consolidato. Estensione naturale per DS-17-2.

### 3.3 `scripts/mockup_demo/` (esistente — pattern di riferimento per DS-17-1)

**Path**: `scripts/mockup_demo/` (Python 3.11+ stdlib, no deps)

**File chiave**:
- `build_map.py` — scan mockup → emit nav-map.md
- `classify_todos.py` — heuristic batch classification
- `apply_map.py` — patch HTML/JSX in place con marker `/* DEMO-NAV */`
- `validate.py` — BFS reachability + broken-target report
- `data/canonical_nav.json` — input rules

**Implicazione DS-17-1**: il pattern `apply_map.py` idempotente con guard marker è esattamente quello da replicare per `inject-annotations.mjs`. Posso scegliere se:
- **Opt A**: estendere `scripts/mockup_demo/` (Python stdlib) → consistency col precedente
- **Opt B**: nuovo `scripts/mockup-annotations/` (Node.js mjs, allineato a `lint-tokens.mjs`) → consistency col tooling FE moderno

Decisione tooling = **DP-1** in §5.

### 3.4 `audits/2026-05-12-token-violations.{json,md}` (esistenti)

**Path**: `audits/` (repo root)

**Status**: artifacts del DS-12 ultimo run. Da rigenerare in DS-17-2 includendo scope esteso.

### 3.5 Mockup state files (NON esistenti)

**Glob `admin-mockups/design_files/*state*`**: zero file con naming `-state-NN-` o `-state-`.

**Implicazione DS-17-3**: pattern naming è completamente nuovo. Nessuna migrazione retroattiva di file esistenti — solo creazione di nuovi file `*-state-NN-<label>.html`. `state-matrix.html` referenziato in MOCKUPS_INDEX.md è una **dev-fixture** standalone (matrice 8 route × 5 stati = 40 cell), non un page-mock multi-stato.

### 3.6 Mockup count (per scoping DS-17-3)

Da MOCKUPS_INDEX.md:
- 67 page-mock totali
- Top 30 per traffico stimato (parte sotto-set DS-17-1)
- Top 10 canonical per DS-17-3 (selezione spec doc Sezione 3)

### 3.7 Storybook (NON installato)

**Verificato**: `apps/web/.storybook/` non esiste; `package.json` non ha `@storybook/*` deps.

**Implicazione**: DS-17-4 template `mockup.fidelity.yml` può procedere SENZA Storybook (template è standalone YAML schema). Storybook setup è in Phase 2 (DS-17-5).

---

## 4. Per sub-issue plan

### 4.1 DS-17-2 (#2070) — Estendi `lint:tokens` a admin-mockups + blocca legacy token names

**Effort**: ~0.5-1gg. **Wave**: 1 (parallel con DS-17-4).

#### Scope review

3 deliverable concreti:
1. Lint extension copre `admin-mockups/**/*.{html,jsx,css}`
2. Legacy token names bloccati: `--bg-base`, `--gaming-*`, `--nh-*`, `--e-*`
3. CI gate + inventory regenerated

#### Tasks (TDD-style)

- **T1**: Decisione tooling (DP-2) → Opt A (`@html-eslint/parser`) vs Opt B (custom mjs grep). Raccomandazione panel: **Opt B** (più cheap, no parser dep, allineato a `lint-tokens.mjs`).
- **T2** (RED): scrivere test fixture HTML con `var(--bg-base)` literal in `apps/web/scripts/__tests__/lint-tokens-mockups.test.mjs` → assert script returns exit 1 + error message specifico.
- **T3** (GREEN): implementare `apps/web/scripts/lint-tokens-mockups.mjs` (basato su Opt B) — regex pattern matching su file glob `admin-mockups/**/*.{html,jsx,css}`.
- **T4**: rigenerare inventory esistente includendo nuove violazioni (run `pnpm lint:tokens` + `lint-tokens-mockups` → merge JSON output → markdown summary aggiornato).
- **T5**: CI workflow update — `.github/workflows/lint-tokens.yml` (verificare esistente, altrimenti aggiungere step in `ci.yml`).
- **T6**: Docs update — CLAUDE.md § "🔒 Active Freezes" → nuova lista violazioni bloccate + comando.
- **T7**: smoke test E2E manuale — aggiungere mockup test con violazione + verificare CI rosso; rimuoverla + verificare verde.

#### Test strategy

- Unit: 5-8 test su `lint-tokens-mockups.mjs` (regex coverage, file glob, exit code, JSON output schema)
- Integration: CI workflow dry-run su PR fake

#### Risk

- **R**: parser HTML scope creep (Opt A) → mitigato da DP-2 lock su Opt B
- **R**: violazioni esistenti in `admin-mockups/` esplodono lo script → mitigato whitelisting incrementale (vedi §7 anti-patterns)

---

### 4.2 DS-17-4 (#2072) — Acceptance criteria template `mockup.fidelity.yml` + validator

**Effort**: ~0.5gg. **Wave**: 1 (parallel con DS-17-2).

#### Scope review

4 deliverable:
1. Template YAML schema (`mockup.fidelity.yml.template`)
2. Validator script (`scripts/mockup-annotations/validate-fidelity.mjs`)
3. Docs (`docs/for-developers/frontend/mockup-fidelity-acceptance.md`)
4. Esempio compilato per `sp4-dashboard` pilot

#### Tasks (TDD-style)

- **T1**: Decidere lib YAML parser (`yaml` npm package è già in monorepo? Check) + schema validator (`zod` o `ajv` — `zod` già presente in FE).
- **T2** (RED): scrivere test fixture YAML con campo mancante → assert validator exit 1.
- **T3** (GREEN): implementare `scripts/mockup-annotations/validate-fidelity.mjs` (zod schema parse + cross-reference check su mockup.source path exists).
- **T4**: scrivere template `apps/web/.storybook/templates/mockup.fidelity.yml.template` (anche se .storybook/ non esiste, path forward-compatible; storage temporaneo `docs/for-developers/frontend/templates/mockup.fidelity.yml.template`).
- **T5**: compilare esempio `sp4-dashboard.fidelity.yml` con valori reali (visual_diff_max_px=5, color_delta_e_max=3, tokens_used=canonical_only, states=[default,empty,loading,error], a11y_axe=AA, breakpoints=[375,768,1024,1440]).
- **T6**: docs `mockup-fidelity-acceptance.md` (schema reference + when-to-use guidelines).
- **T7**: CI workflow opzionale `validate-mockup-fidelity.yml` (non blocking inizial — dry-run).

#### Test strategy

- Unit: 4-6 test su `validate-fidelity.mjs` (schema valid + missing field + missing source file + cross-reference path)
- Manual: esempio sp4-dashboard parse pass

#### Risk

- **R**: zod schema vs ajv decisione tooling → mitigato zod (già in repo)
- **R**: path canonico template instabile (Storybook .storybook/ non esiste) → mitigato storage temporaneo `docs/for-developers/frontend/templates/`, move in Phase 2

---

### 4.3 DS-17-1 (#2069) — `@mockup` annotation injection script + grep audit

**Effort**: ~1gg. **Wave**: 2 (dopo DS-17-2/DS-17-4 done).

#### Scope review

5 deliverable:
1. Script inject (`scripts/mockup-annotations/inject-annotations.mjs`)
2. Script audit (`scripts/mockup-annotations/audit-coverage.mjs`)
3. Docs pattern (`docs/for-developers/frontend/mockup-annotation-pattern.md`)
4. Top 30 route page.tsx con annotation applicata
5. CI job opzionale `mockup-annotation-coverage.yml`

#### Dipendenze

- **Depends on**: DS-17-4 (annotation referenzia path `fixtures` e `story` definite dal template)
- **Coordina con**: DS-17-2 (script abita stesso folder `scripts/mockup-annotations/`)

#### Tasks (TDD-style)

- **T1**: Decisione tooling (DP-1) → Opt A Python (estensione `scripts/mockup_demo/`) vs Opt B Node.js (nuovo `scripts/mockup-annotations/`). Raccomandazione panel: **Opt B** (allineato a tooling FE, no Python skill richiesto a contributor FE).
- **T2** (RED): scrivere test fixture su `apps/web/src/app/dashboard/page.tsx` (esempio) — assert script idempotente, run 2x = stesso output, marker guard `/* MOCKUP-ANNOTATION */` rispettato.
- **T3** (GREEN): implementare `inject-annotations.mjs` — parse `admin-mockups/MOCKUPS_INDEX.md` mapping table → match route file → inject JSDoc block.
- **T4** (RED): scrivere test su `audit-coverage.mjs` — fixture route con/senza annotation → assert report markdown corretto + exit code 1 sotto threshold.
- **T5** (GREEN): implementare `audit-coverage.mjs` con threshold default 80%, override via `--threshold N`.
- **T6**: dry-run su 30 route pilota → review output → apply.
- **T7**: docs pattern + CI workflow.

#### Test strategy

- Unit: 8-10 test su inject + audit scripts (parse INDEX, match route, inject idempotente, marker guard, threshold check)
- Integration: dry-run su repo pulito → no diff su 2a run

#### Risk

- **R**: MOCKUPS_INDEX.md format change rompe parser → mitigato schema check al parse + version pinning
- **R**: 30 route troppe in 1 PR → split in 3 PR sub-cluster (10 route ciascuno) se diff >500 LOC
- **R**: annotation in conflict con altri JSDoc esistenti → mitigato T2 test fixture coverage

---

### 4.4 DS-17-3 (#2071) — Standardizza naming stati + migra 10 mockup canonici a multi-stato

**Effort**: ~1.5-2gg. **Wave**: 3 (può procedere parallelo a Wave 2 se designer disponibile).

#### Scope review

4 deliverable:
1. Pattern naming documentato (README + MOCKUPS_INDEX.md)
2. 10 mockup canonici migrati (~30 nuovi file `*-state-NN-*.html`)
3. MOCKUPS_INDEX.md aggiornato con stati per mockup
4. Lint script `validate-state-naming.mjs`

#### Dipendenze

- **Designer-bound**: contenuto visivo dei nuovi stati (empty illustrations, loading skeletons, error messages) richiede designer input
- **Indipendente da**: DS-17-1/DS-17-2/DS-17-4 (lavoro su file diversi)

#### Tasks (TDD-style)

- **T1**: Pattern naming spec — update `admin-mockups/README.md` + create `admin-mockups/STATE_PATTERN.md` (riferimento esplicito).
- **T2**: lint script `scripts/mockup-annotations/validate-state-naming.mjs` (regex check su `*-state-*.html` → naming canonico `-state-NN-<label>.html`).
- **T3**: Coordinamento designer — review 10 mockup canonici, raccolta requisiti stati (empty per dashboard? loading per game-detail? sse per chat?).
- **T4**: Migrazione mockup #1 (`sp4-dashboard.html`) → +`sp4-dashboard-state-02-empty.html`, +`sp4-dashboard-state-03-loading.html`. Validation: lint pass + smoke test browser.
- **T5**: Migrazione mockup #2-5 (`sp4-library-desktop`, `sp4-game-detail`, `sp4-chat`, `sp4-session-skeleton-live`) — pattern consolidato.
- **T6**: Migrazione mockup #6-10 (`sp7-game-night-detail-rsvp`, `sp3-shared-games`, `sp4-discover`, `sp4-kb-hub`, `librogame-runthrough-play-session`).
- **T7**: MOCKUPS_INDEX.md aggiornato — colonna "Stati pubblicati" o sezione separata.
- **T8**: CI workflow `validate-state-naming.yml` (blocking).

#### Test strategy

- Unit: 3-5 test su `validate-state-naming.mjs` (regex coverage)
- Manual: smoke test browser `python -m http.server` su `admin-mockups/design_files/` post-migration
- Designer review per ogni mockup migrato (DEC-3 spec doc Sezione 2)

#### Risk

- **R**: designer non disponibile → mitigato split sub-issue in `DS-17-3a` (naming + script lint, no designer-bound) e `DS-17-3b` (10 mockup migration, designer-bound, può attendere)
- **R**: scope creep su contenuto stati (designer aggiunge feature) → mitigato locking spec doc Sezione 2 DEC-3 "Designer-led review checklist" come anti-bikeshed gate
- **R**: 30 nuovi file in 1 PR → split per mockup (10 PR piccole) o per cluster (3 PR da 3-4 mockup)

---

## 5. Decision points (USER-LOCKED prima di start)

| ID | Decisione | Opzioni | Raccomandazione panel | User-locked? |
|----|-----------|---------|----------------------|--------------|
| **DP-1** | Tooling annotation script (DS-17-1) | A: estendere `scripts/mockup_demo/` (Python) · B: nuovo `scripts/mockup-annotations/` (Node mjs) | **Opt B** — allineato a tooling FE, no Python skill required | ⏳ |
| **DP-2** | Tooling lint extension HTML/CSS (DS-17-2) | A: `@html-eslint/parser` + flat config · B: custom `lint-tokens-mockups.mjs` grep | **Opt B** — più cheap, no dep, fast iteration | ⏳ |
| **DP-3** | YAML lib (DS-17-4) | A: `yaml` npm · B: `js-yaml` · C: `zod` only (no YAML, JSON instead) | **Opt A `yaml`** — leggero, attivamente maintained | ⏳ |
| **DP-4** | Sequencing strategy | A: full sequential (17-2 → 17-4 → 17-1 → 17-3) · B: parallel waves (vedi §6) | **Opt B** — risparmia ~30% wall-clock | ⏳ |
| **DP-5** | DS-17-3 designer dependency | A: aspetta designer disponibilità · B: split `DS-17-3a` (lint+naming) ship subito + `DS-17-3b` (migration) attende designer | **Opt B** — sblocca progress, designer non bottleneck | ⏳ |
| **DP-6** | Aspetta PR #2068 merged? | A: aspetta merge prima di iniziare Phase 1 · B: parte DS-17-2/DS-17-4 in parallelo a #2068 review (ortogonali al spec) | **Opt B** — work parallelo a basso rework risk | ⏳ |

Senza lock di queste decisioni l'implementazione può divergere → blocco hard pre-start.

---

## 6. Sequencing strategy raccomandato (assumendo DP-4=B, DP-5=B, DP-6=B)

```
WAVE 1 (parallel, ~0.5-1gg)
├─ DS-17-2 (lint tokens extension)     [dev A]
└─ DS-17-4 (acceptance template)        [dev B o stessa dev sequenziale]
   ↓ (DS-17-4 done unlocks DS-17-1 annotations syntax)

WAVE 2 (sequential, ~1gg)
└─ DS-17-1 (mockup annotations)         [dev A]
   ↓ (annotations applied unlock DS-17-3 lint check su path coerenza)

WAVE 3 (split a/b)
├─ DS-17-3a (naming + lint script, ~0.5gg)    [dev A o B]
└─ DS-17-3b (10 mockup migration, ~1-1.5gg)   [designer-led, can defer]
```

**Wall-clock totale**:
- Single dev: ~3.5-4.5gg (Wave 1 + 2 + 3a sequential, 3b deferred)
- 2 dev parallel: ~2-3gg (Wave 1 parallel, Wave 2 single, Wave 3a+b parallel-ish)

**Stato pre-mortem**: se Wave 1 take >1.5gg, escalation → re-evaluate scope DS-17-2 (riduci legacy names blocked iniziali da 4 a 2 famiglie).

---

## 7. Risk matrix consolidato (Phase 1 cross-cutting)

| Rischio | P | I | Score | Mitigation |
|---------|---|---|-------|------------|
| R1 · Decision points (§5) non lockati prima di start → rework | 4 | 4 | **16** | HARD BLOCKER §5, no work senza lock |
| R2 · Designer non disponibile per DS-17-3 → blocca Wave 3 | 4 | 3 | **12** | DP-5 split 3a/3b, 3a ship subito |
| R3 · `lint-tokens-mockups.mjs` violations esplodono → CI rosso massivo | 3 | 4 | **12** | Whitelist incrementale: prima run = inventory only, CI blocking solo nuove violazioni |
| R4 · Top 30 route annotations PR >500 LOC unreviewable | 3 | 3 | **9** | Split in 3 PR sub-cluster (10 route ciascuno) |
| R5 · YAML schema cambia post-T2 → rework esempio | 2 | 3 | **6** | DP-3 lock zod+yaml subito |
| R6 · Conflict con WIP #1972 vitest v4 stashato → rebase fail | 2 | 3 | **6** | Branch da main-dev fresh, no overlap path file |
| R7 · Spec doc PR #2068 review pivot decisioni → rework Phase 1 | 1 | 5 | **5** | DP-6 B: Wave 1 ortogonali al spec; Wave 2-3 wait for merge |

---

## 8. Anti-patterns (NON fare)

- ❌ **Big-bang PR Phase 1**: 4 sub-issue in 1 PR. Splittare in min 4 PR (1 per sub-issue) + eventualmente sub-PR per DS-17-1 (3 cluster route) e DS-17-3 (3-10 cluster mockup).
- ❌ **Skip decision points §5**: iniziare T1 senza DP locked = pivot mid-task = rework.
- ❌ **Whitelist tutto per CI verde**: DS-17-2 deve bloccare almeno *nuove* violazioni, anche se le esistenti sono tollerate temporaneamente.
- ❌ **DS-17-3 senza designer**: migrare 10 mockup con stati inventati = drift Phase 1+Phase 3 garantito.
- ❌ **Annotation in route senza fixtures.json esistente**: DS-17-1 annotation deve essere annotation-only (pointer al mockup), non assumere che fixtures esistano già (sono in Phase 2 DS-17-7).
- ❌ **Override stashato #1972 vitest setup**: lavorare su path file disjoint, non toccare `apps/web/vitest.setup.tsx`.

---

## 9. Effort breakdown finale

| Sub-issue | Effort | Wave | Dipendenze | PR count |
|-----------|--------|------|------------|----------|
| DS-17-2 (#2070) | ~0.5-1gg | 1 | nessuna | 1 |
| DS-17-4 (#2072) | ~0.5gg | 1 | nessuna | 1 |
| DS-17-1 (#2069) | ~1gg | 2 | DS-17-4 (annotation syntax) | 1-3 (split cluster route) |
| DS-17-3a (#2071) | ~0.5gg | 3 | nessuna (lint+naming only) | 1 |
| DS-17-3b (#2071) | ~1-1.5gg | 3 (deferred) | designer | 3-10 (split per mockup) |
| **Totale Phase 1** | **3.5-4.5gg** | — | — | **5-15 PR** |

**Comparativo**: il body umbrella stimava ~2-3gg Phase 1 con 1 dev. Il piano dettagliato rivela ~3.5-4.5gg single dev (rebaseline +40% post-discovery). Coerente con pattern P181 (spec-panel rebaseline +60% storico).

---

## 10. Checklist start work

Pre-implementation checklist (run prima di T1 di qualunque sub-issue):

- [ ] DP-1..DP-6 user-locked (§5)
- [ ] Spec doc PR #2068 status (merged | in-review | pivot)
- [ ] WIP #1972 vitest setup stashato safe + recovery instructions documentati per sviluppatore originario
- [ ] Branch fresh da main-dev per ogni sub-issue (no stacking)
- [ ] Sub-issue body referenced nel commit message + PR body (Closes #NNNN)
- [ ] CLAUDE.md "🔒 Active Freezes" updated con stato DS-17 Phase 1 in-progress

---

## 11. Next step decisionale

Approval su §5 DP-1..DP-6 → start Wave 1 (DS-17-2 + DS-17-4 parallel).

Pattern operativo standard sess.46+:
1. `gh issue view 2070 -c` per leggere sub-issue + plan
2. Branch `feature/issue-2070-...` da main-dev
3. T1-T7 TDD style
4. Commit incrementali
5. Push + PR → review → merge
6. Update umbrella body checkbox + cleanup branch

---

## References

- Umbrella: [#2063](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Spec doc: `docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md` (PR #2068 in review)
- Sub-issues Phase 1: [#2069](https://github.com/meepleAi-app/meepleai-monorepo/issues/2069) · [#2070](https://github.com/meepleAi-app/meepleai-monorepo/issues/2070) · [#2071](https://github.com/meepleAi-app/meepleai-monorepo/issues/2071) · [#2072](https://github.com/meepleAi-app/meepleai-monorepo/issues/2072)
- Pattern plan analogo: `docs/superpowers/plans/2026-06-09-large-medium-remaining-plan.md` (sess.46d)
- Memory: `memory/umbrella-ds-17-mockup-fidelity.md`
- Discovery files (esistenti, riusati):
  - `apps/web/scripts/lint-tokens.mjs` (DS-2 era — base per DS-17-2 extension)
  - `apps/web/eslint-rules/no-hardcoded-color-utility.js` (DS-15 — rule esistente)
  - `scripts/mockup_demo/apply_map.py` (pattern idempotente marker — modello per DS-17-1)
  - `audits/2026-05-12-token-violations.{json,md}` (output esteso post DS-17-2)

---

🤖 Generated with [Claude Code](https://claude.com/claude-code) — `/sc:pm` planning post discovery 2026-06-09 sess.46h
