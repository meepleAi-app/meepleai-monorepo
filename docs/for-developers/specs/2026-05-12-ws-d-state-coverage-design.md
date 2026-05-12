# WS-D Foundation — Mockup State Coverage Matrix (Design)

| Field | Value |
|---|---|
| **Status** | approved (brainstorming 2026-05-12) |
| **Date** | 2026-05-12 |
| **Author** | brainstorming session — user + assistant |
| **Implements** | WS-D from [`2026-05-12-mockup-conformity-roadmap.md`](./2026-05-12-mockup-conformity-roadmap.md) §3 |
| **Umbrella issue** | #1066 |
| **Tracking issue** | #1070 |
| **Branch convention** | `feature/issue-1070-mockup-conformity-states` (Foundation), `feature/issue-1070-mockup-conformity-states-exemplar` (Exemplar PR2) |

## 1. Problem statement

Ogni mockup HTML in `admin-mockups/design_files/` dichiara stati canonici nel commento header (es. `Stati: default · loading · error · not-found`). Pattern sentinel `?state=...` Wave B.1/B.2/B.3 copre 4-5 stati per route migrate ma:

- non uniformato cross-route (Wave A non lo usa)
- non lega gli stati alle dichiarazioni del mockup (drift possibile)
- 5 file `*-visual-test-fixture.ts` esistenti seguono pattern simili ma divergenti

Mancano:

- Parser automatico che estrae `Stati:` dai mockup
- Matrix machine-readable che traccia copertura
- Helper unificato cross-route per `?state=` URL override
- CI gate che blocca drift

## 2. Scope decisions (brainstorming 2026-05-12)

| Decision | Choice |
|---|---|
| Foundation vs Foundation+Exemplar PR1 | **Foundation only** (route exemplar split in PR2 dopo merge #1074) |
| Coordination con PR #1074 (`/library/[gameId]` shared file) | **Wait & rebase** — PR2 Exemplar parte dopo merge #1074 |
| HTML parser | **jsdom** (`^29.1.1` già installato, edge-case-safe) |
| Approach matrix sync | **Approach 1: bootstrap-then-enforce** (progressive adoption, no big-bang regression) |

## 3. Goals & Non-goals

### Goals

- **G1** — Parser standalone (`scripts/extract-mockup-states.ts`) che estrae `Stati:` da TUTTI i mockup, output deterministico
- **G2** — Matrix JSON canonica `apps/web/e2e/state-coverage/state-matrix.json` con shape stabile + JSON schema sidecar
- **G3** — Helper unificato `apps/web/src/lib/visual-test/state-override.ts` (tree-shakeable, zero impact production)
- **G4** — CI workflow `state-coverage-check.yml` con dual-check (sync + enforcement)
- **G5** — Doc `docs/for-developers/testing/frontend/visual-state-coverage.md` adoption guide

### Non-goals

- **NG1** — Route exemplar migration (split a PR2 post-#1074)
- **NG2** — Migrazione dei 5 `*-visual-test-fixture.ts` esistenti a `state-override.ts` (progressive, future PRs)
- **NG3** — Implementare visual-conformity workflow WS-C (separato issue #1069)
- **NG4** — Generare PR di update mockup-by-mockup automaticamente

## 4. Architecture

### 4.1 File layout

```
apps/web/
├── scripts/
│   └── extract-mockup-states.ts         NEW  parser jsdom (regenerates matrix)
├── src/
│   └── lib/
│       └── visual-test/                  NEW
│           ├── state-override.ts         helper unificato `?state=` URL override
│           └── __tests__/
│               └── state-override.test.ts
└── e2e/
    └── state-coverage/
        ├── state-matrix.json             NEW  bootstrap inventory (~60 mockup)
        └── state-matrix.schema.json      NEW  JSON Schema sidecar (IDE validation)

.github/workflows/
└── state-coverage-check.yml              NEW  CI gate

docs/for-developers/
└── testing/
    └── frontend/
        └── visual-state-coverage.md      NEW  adoption workflow doc
```

### 4.2 Parser (`extract-mockup-states.ts`)

**Input**: glob `admin-mockups/design_files/*.html`

**Output**: `state-matrix.json` deterministic

**Algorithm**:

1. Glob `admin-mockups/design_files/*.html`
2. Per file: read → `new JSDOM(html).window.document`
3. Walk DOM, collect `NODE_COMMENT` (filter `Node.COMMENT_NODE === 8`)
4. Regex su comment text:
   - `Route:\s*(\S+)` → route string (può essere null)
   - `Stati:\s*([^\n]+)` → state list raw (multiline `/m` flag)
5. Split state list su `·` (middot) primario, fallback `,` o `|`. Normalize: trim, lowercase, no diacritics
6. Build entry. Ordering alphabetic per `mockup_path` (deterministic)
7. Confronta con `state-matrix.json` esistente: **preserva** `covered_states` + `enforced` (manualmente curati), aggiorna `declared_states` + `missing` + `generated_at`
8. Write file. Exit 0 se nessun diff content; exit 1 se diff non-meta

**CLI flags**:

| Flag | Behavior |
|---|---|
| `--check` | Dry-run, exit 1 se matrix.json out-of-sync con mockup |
| `--write` | Regenera e scrive matrix.json |
| `--enforced-only` | Filtra entries `enforced: true`, fail se `missing.length > 0` |

**Edge cases**:

- Mockup senza `Stati:` → `declared_states: []`, entry presente per inventory
- Multi-line `Stati:` (es. `sp4-game-chat-tab.html:23-31`) → match prima linea + continuation lines indented
- Route placeholder `{gameId}` vs `[gameId]` → preservato as-is

### 4.3 Matrix schema (`state-matrix.json`)

```json
{
  "$schema": "./state-matrix.schema.json",
  "generated_at": "2026-05-12T16:42:00.000Z",
  "total_mockups": 62,
  "enforced_count": 0,
  "entries": [
    {
      "mockup_path": "admin-mockups/design_files/nanolith-runthrough-game-detail.html",
      "route": "/library/{gameId}",
      "declared_states": ["default", "loading", "error", "not-found"],
      "covered_states": [],
      "missing": ["default", "loading", "error", "not-found"],
      "enforced": false
    }
  ]
}
```

**TypeScript interface** (anche esportato da `extract-mockup-states.ts`):

```ts
interface MockupStateEntry {
  mockup_path: string;
  route: string | null;
  declared_states: string[];     // preserva ordering autore
  covered_states: string[];      // curated manualmente
  missing: string[];             // declared - covered, computed
  enforced: boolean;             // bootstrap PR1: tutti false
}

interface StateMatrix {
  generated_at: string;
  total_mockups: number;
  enforced_count: number;
  entries: MockupStateEntry[];
}
```

**Sorting** (deterministic):
- `entries[]` ordered by `mockup_path` alphabetic ASC
- `declared_states[]` + `covered_states[]` + `missing[]` ordered per **declaration order** del mockup

**Versioning**:
- Parser preserva `covered_states` + `enforced` su update
- `generated_at` bumped solo se content effettivo differs (no spurious commits)

### 4.4 State override helper (`state-override.ts`)

```ts
export const IS_VISUAL_TEST_BUILD: boolean =
  process.env.NEXT_PUBLIC_VISUAL_TEST_BUILD === '1';

export function readStateOverride(searchParams: URLSearchParams | null): string | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  if (!searchParams) return null;
  return searchParams.get('state');
}

export function readTypedStateOverride<S extends string>(
  searchParams: URLSearchParams | null,
  allowedStates: readonly S[]
): S | null {
  const raw = readStateOverride(searchParams);
  if (raw === null) return null;
  if (!allowedStates.includes(raw as S)) return null;
  return raw as S;
}

export function useStateOverride<S extends string>(
  allowedStates: readonly S[]
): S | null {
  if (!IS_VISUAL_TEST_BUILD) return null;
  const { useSearchParams } = require('next/navigation');
  const params = useSearchParams() as URLSearchParams | null;
  return readTypedStateOverride(params, allowedStates);
}
```

**Production guarantee**:
- `NEXT_PUBLIC_VISUAL_TEST_BUILD=1` settato solo in `playwright.config.ts:webServer.env`
- Production build: env var absent → `IS_VISUAL_TEST_BUILD === false` → terser dead-code-elimination rimuove **tutto** il body
- Bundle size delta atteso: 0 KB

**Three-layer API**:
- `readStateOverride`: standalone, testabile senza React
- `readTypedStateOverride`: type-safety opzionale
- `useStateOverride`: React-friendly per consumer components

### 4.5 CI workflow (`state-coverage-check.yml`)

**Trigger**:

| Source | Behavior |
|---|---|
| PR tocca `admin-mockups/design_files/**` | Sync check |
| PR tocca `state-matrix.json` direttamente | Sync check |
| PR tocca `extract-mockup-states.ts` | Sync check (regression parser logic) |
| `workflow_dispatch` mode=`regenerate` | Bootstrap/refresh, uploads artifact |
| PR su altri path | Skipped (path filter) |

**Two-stage check** (entrambi run su PR):

1. **`--check`**: parser rigenera in memoria, diff vs committed → fail su diff content
2. **`--enforced-only`**: per ogni entry `enforced: true`, fail se `missing.length > 0`

PR1 bootstrap: tutte `enforced: false` → stage 2 passa sempre.

**Cost CI atteso**: ~2-3min job (no Playwright, no browser, no Next build — solo tsx script + jsdom).

**PR comment on failure** (sync): istruzioni `pnpm tsx ... --write` + commit guide.

### 4.6 Distinzione vs workflow esistenti (no overlap)

| Workflow | Scope |
|---|---|
| `visual-regression-mockups.yml` (#571) | Mockup HTML → baseline PNG (visual stability) |
| `visual-regression-migrated.yml` (Wave A/B) | Route live → baseline PNG (implementation stability) |
| **`state-coverage-check.yml`** (NEW PR1) | Mockup `Stati:` ↔ matrix.json consistency (gate ≠ visual) |
| `visual-regression-conformity.yml` (WS-C future) | Route ↔ mockup pixel diff (gate visual) |

## 5. Three levels of "data"

Distinzione esplicitata durante brainstorming (clarification post-sezione 3):

| Layer | Source | Type | Example |
|---|---|---|---|
| **1. Mockup** | `admin-mockups/design_files/*.html` | Hardcoded fictitious | "Nanolith" hardcoded |
| **2. Route in production** | Browser utente reale | Backend reale | Aaron vede sua libreria reale |
| **3. Route in visual-test** | CI Playwright `?state=...` | **Sentinel fixture deterministica** | `gameTitle: 'Test Game'` |

**WS-D traccia SOLO nomi degli stati** (livello dichiarativo). I dati live (livello 2) e i fixture sentinel (livello 3) sono fuori scope.

## 6. Acceptance Criteria (per Issue #1070)

- **AC-D.1** — 100% degli stati dichiarati nel mockup di ciascuna route registrata sono coperti (`missing.length === 0` in state-matrix.json) **per route con `enforced: true`**. PR1 bootstrap: zero route enforced.
- **AC-D.2** — Generator output deterministico (run idempotente, ordering stabile, `generated_at` bumped solo su content diff)
- **AC-D.3** — Fixture pattern uniformato disponibile via `state-override.ts` helper (consumo opzionale)
- **AC-D.4** — Route exemplar `/library/[gameId]` cita stato `error ≠ not-found` (sinergia WS-B AC-B.2) → **deferred PR2 Exemplar**

PR1 Foundation soddisfa **AC-D.2 + AC-D.3** (parser deterministico + helper disponibile).
PR2 Exemplar soddisfa **AC-D.1 + AC-D.4** (prima route enforced, `error ≠ not-found` distinction).

## 7. Test plan (PR1 Foundation)

**Unit tests** (`state-override.test.ts`):

- `readStateOverride`:
  - returns null when `IS_VISUAL_TEST_BUILD=false`
  - returns `?state` value when `IS_VISUAL_TEST_BUILD=true`
  - returns null when searchParams missing param
- `readTypedStateOverride`:
  - returns typed value when in allowedStates
  - returns null when value not in allowedStates
  - handles empty allowedStates array

**Integration tests** (parser, `__tests__/extract-mockup-states.test.ts`):

- Parse `nanolith-runthrough-game-detail.html` fixture → 4 stati estratti
- Parse `nanolith-runthrough-error-states.html` fixture → 4 stati estratti (no Route)
- Parse `sp4-game-chat-tab.html` fixture → 4 stati multi-line correttamente parsati
- Mockup senza `Stati:` → entry con `declared_states: []`
- Re-run idempotente: `generated_at` invariato su content stable

**CI integration**:

- `pnpm tsx scripts/extract-mockup-states.ts --check` su matrix bootstrap → exit 0
- Simulare drift via test fixture (modifica mockup senza update matrix) → exit 1

## 8. Failure matrix

| Mode | Detection | Mitigation |
|---|---|---|
| Mockup edit senza matrix update | CI `--check` exit 1 | PR comment guida `--write` + commit |
| Parser breaking change rinomina mockup field | Diff content | Test fixture regression catch |
| State proliferation (>20 per route) | Manual review matrix.json | Cap canonical 4-6 stati documentato in doc |
| Stati timing-dependent (es. SSE) non visual-testable | Manual review | Exception list documentata (mirror Wave D.3 `error` state escluso visual, coperto unit) |
| Workflow path filter rinomina parser senza update | Workflow non triggera, silenzioso | Future PR: lint rule check filter ↔ script path (out-of-scope PR1) |
| Bundle size delta da `state-override.ts` | `pnpm build` size report | `IS_VISUAL_TEST_BUILD=false` in production garantisce dead-code-elim → 0 KB |

## 9. Rollback

- Workflow disable via revert PR
- `state-matrix.json` retained come documentation snapshot (no breaking change)
- `state-override.ts` zero consumer in production → safe da rimuovere

## 10. Sequencing post-PR1

| PR | Scope | Pre-requisite |
|---|---|---|
| **PR1 Foundation** | parser + matrix.json + CI + state-override.ts + doc | Niente |
| **PR2 Exemplar** | `/library/[gameId]` usa `state-override`, 4 stati covered, `enforced: true` | **Merge #1074 (WS-B)** prima di toccare `page.tsx` |
| **PR3+ Wave migration** | Progressive enforcement route-by-route | PR2 |

## 11. References

- Roadmap spec: [`2026-05-12-mockup-conformity-roadmap.md`](./2026-05-12-mockup-conformity-roadmap.md) §3 WS-D
- Token canonicalization companion: [`2026-05-12-token-canonicalization.md`](./2026-05-12-token-canonicalization.md) — finished via DS-1..DS-16 series
- Existing visual regression workflows: [`visual-regression-mockups.yml`](../../../.github/workflows/visual-regression-mockups.yml), [`visual-regression-migrated.yml`](../../../.github/workflows/visual-regression-migrated.yml)
- Existing fixture pattern Wave B: 5 files `*-visual-test-fixture.ts` in `apps/web/src/lib/sessions*/`
- Sample mockups con `Stati:` declaration:
  - [`nanolith-runthrough-game-detail.html`](../../../admin-mockups/design_files/nanolith-runthrough-game-detail.html) — 4 stati
  - [`nanolith-runthrough-error-states.html`](../../../admin-mockups/design_files/nanolith-runthrough-error-states.html) — 4 stati tecnici
  - [`sp4-game-chat-tab.html`](../../../admin-mockups/design_files/sp4-game-chat-tab.html) — 4 stati multi-line

---

**Sign-off**:
- [x] Project owner — design approved 2026-05-12 brainstorming session
- [ ] User review of written spec (next step)
- [ ] Implementation plan via writing-plans skill
