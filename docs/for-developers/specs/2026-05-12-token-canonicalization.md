# Design Token Canonicalization — Mockup Authority Convergence

| Field | Value |
|---|---|
| **Status** | draft |
| **Date** | 2026-05-12 |
| **Author** | spec-panel (Fowler/Wiegers/Nygard/Adzic/Doumont) |
| **Extends** | [`2026-05-11-design-system-deversioning.md`](./2026-05-11-design-system-deversioning.md) §8 (out-of-scope) — questa spec lo riapre |
| **Mockup source of truth** | [`admin-mockups/design_files/tokens.css`](../../../admin-mockups/design_files/tokens.css), [`components.css`](../../../admin-mockups/design_files/components.css) |
| **Branch convention** | `feature/token-canonicalization-{stage}` per PR stage |

## 1. Problem statement

Il runtime frontend ospita **due token system paralleli**:

- **Runtime v1**: `apps/web/src/styles/{globals,design-tokens,premium-gaming}.css` — usa `--bg-base`, `--gaming-bg-*`, `--nh-bg-*`, `--background` (shadcn), `.dark` class via `next-themes`.
- **Mockup canonical**: `admin-mockups/design_files/tokens.css` — usa `--bg`, `--bg-card`, `--bg-muted`, `--bg-sunken`, `[data-theme]` attribute, scale typography/spacing/radius/motion proprie.

I 9 layer (color, typography, spacing, radius, motion, glass, shadow, z-index, theming-mechanism) hanno delta significativi tra runtime e mockup. Gli sviluppatori non hanno una source of truth univoca; alcune pagine consumano `bg-white`/`bg-slate-*` hardcoded di Tailwind, altre usano i tokens semantici v1 (`--bg-base`), altre ancora hanno migrato parzialmente.

**Risultato visivo osservato (utente, 2026-05-12)**: "vedo ancora sfondi legacy" su pagine in produzione, indice di drift tra cluster.

## 2. Goals & Non-goals

### Goals

- G1: **Single source of truth** = `admin-mockups/design_files/tokens.css`. Tutti i layer (color, typography, spacing, radius, motion, glass, shadow) consumati esclusivamente da quel file (importato in runtime).
- G2: **Theming mechanism** unificato: `[data-theme="light|dark"]` attribute su `<html>` (mockup convention), `.dark` class deprecato.
- G3: **Light theme default** (mockup): `--bg: #f7f3ee` warm cream. Dark theme via `[data-theme="dark"]` override.
- G4: **Lint enforcement**: regole custom che proibiscono `bg-white|bg-slate-*|text-slate-*|bg-gray-*` hardcoded e CSS custom properties non listate in `tokens.css`. Mode `warning` fase 1 → `error` fase finale.
- G5: **Visual conformity** ≤ 2% pixel diff vs mockup HTML per ogni route migrata.
- G6: **A11y AA** mantenuto: 0 axe violations su WCAG 2.1 AA tags durante l'intera transizione.
- G7: **Bridge layer temporaneo**: alias `--bg-base → var(--bg)` etc. permettono migrazione consumer-by-consumer senza big-bang.

### Non-goals

- NG1: Refactor logico di Server Action / data hooks (separato).
- NG2: Routing/IA changes — separato in [`2026-05-11-design-system-deversioning.md`](./2026-05-11-design-system-deversioning.md) §6.
- NG3: Mobile React Native app.
- NG4: Storybook catalog rebuild (separato post-migration).
- NG5: Backend API renames (frontend-only).

## 3. Stage plan (12 PR sequenziali, parallelizzabili dopo DS-3)

### Stage DS-1 — Token import + bridge layer + theming mechanism

**PR title**: `feat(design-system): import mockup tokens + bridge alias (DS-1)`

**Branch**: `feature/token-canonicalization-ds-1`

**Deliverable**:
1. Copia `admin-mockups/design_files/tokens.css` → `apps/web/src/styles/design-tokens-canonical.css` (file canonico runtime).
2. Aggiorna `apps/web/src/app/layout.tsx`:
   - Import `design-tokens-canonical.css` come **primo** stylesheet (prima di `globals.css`).
3. Aggiorna `apps/web/src/styles/globals.css`:
   - Rimuovi `:root` redeclarations dei token canonici (color, typography, spacing, radius).
   - Mantieni **solo** bridge alias `v1 → canonical`:
     ```css
     :root {
       --bg-base: var(--bg);
       --background: var(--bg);
       --foreground: var(--text);
       --text-primary: var(--text);
       --text-secondary: var(--text-sec);
       --text-tertiary: var(--text-muted);
       /* ... 30+ alias documentati */
     }
     ```
4. Aggiorna `apps/web/src/components/providers/ThemeProvider.tsx`:
   - `next-themes` config: `attribute="class"` → `attribute="data-theme"`.
   - `defaultTheme="dark"` → `defaultTheme="light"` (mockup default).
   - `enableSystem` mantenuto.
5. Aggiorna `apps/web/tailwind.config.ts` o `@theme` in `globals.css`:
   - `--color-background: var(--bg)` (Tailwind utility `bg-background` ora risolve al canonico).
   - Stesso pattern per `text`, `border`, `muted`, `card`, etc.
   - Typography scale: `--text-xs..3xl` mappati a `--fs-xs..3xl` (mockup).
6. Documento `docs/for-developers/frontend/token-bridge-map.md`: tabella `v1-name → canonical-name` per ogni alias.

**Acceptance criteria**:

- AC1.1: `pnpm typecheck && pnpm lint && pnpm test && pnpm build` verdi.
- AC1.2: Visual regression run — snapshot baseline rebake (diff atteso significativo, validato manualmente sui 5-6 routes principali).
- AC1.3: A11y E2E suite passa (con `colorScheme: 'dark'` rimosso, ora redundant — light default).
- AC1.4: Bundle size delta ≤ 3% (più tokens, ma meno duplicazione).
- AC1.5: `<html data-theme="light">` SSR-rendered al primo paint (no FOUC dark→light).

**Rollback**: revert PR. Tutti i consumer puntano ancora ai nomi v1 (alias attivi), zero breakage.

### Stage DS-2 — Lint rules (token vocabulary enforcement)

**PR title**: `feat(eslint): forbid hardcoded Tailwind colors + non-canonical tokens (DS-2)`

**Branch**: `feature/token-canonicalization-ds-2`

**Deliverable**:
1. Custom ESLint plugin `eslint-plugin-meepleai-tokens` (o estensione del plugin esistente) con 2 regole:
   - `meepleai-tokens/no-hardcoded-color-utility`: vieta classi Tailwind di colore hardcoded:
     - `bg-white`, `bg-black`
     - `bg-slate-*`, `bg-gray-*`, `bg-zinc-*`, `bg-neutral-*`, `bg-stone-*`
     - `text-slate-*`, `text-gray-*`, etc.
     - `border-slate-*`, etc.
     - Eccezione: i 9 entity colors (`bg-entity-game`, `text-entity-session`, etc.).
   - `meepleai-tokens/no-non-canonical-custom-property`: vieta CSS custom properties non listate in `tokens.css` whitelist (es. `var(--my-custom-thing)`).
2. Modalità inizio: `warn` (fase 1).
3. Output: report aggregato `audit/2026-05-12-token-violations.json` con count per file.
4. CI workflow: `pnpm lint:tokens` step separato, non-blocking in fase 1.

**Acceptance criteria**:

- AC2.1: Lint rules eseguibili su tutto il monorepo `apps/web/src/**/*.{ts,tsx}` in < 30s.
- AC2.2: Report violations totali ≥ 1 (deve trovare violazioni esistenti).
- AC2.3: Eccezioni documentate (entity colors) effettivamente whitelistate.

**Rollback**: revert PR. Lint rule disabilitata, zero breakage.

### Stage DS-3 — Inventory & cluster prioritization

**PR title**: `docs(audit): token violation inventory + cluster priority (DS-3)`

**Branch**: `feature/token-canonicalization-ds-3`

**Deliverable**:
1. Run `pnpm lint:tokens` → `audit/2026-05-12-token-violations.json`.
2. Aggregazione per cluster (es. `features/sessions/*`, `features/gamebook/*`, etc.).
3. Documento `docs/for-developers/audits/2026-05-12-token-canonicalization-inventory.md`:
   - Tabella cluster × violations count × suggested order.
   - Note su orphan files (no cluster assignment).
4. Aggiornamento `v2-migration-matrix.md`: nuova colonna `Token compliance: pending|partial|done`.

**Acceptance criteria**:

- AC3.1: 100% file con violations classificati per cluster.
- AC3.2: Priority order proposta basata su (a) traffico utente, (b) violations count, (c) test coverage esistente.

**Rollback**: revert PR (doc only).

### Stage DS-4..DS-11 — Cluster migration (8 PR cluster-by-cluster, parallelizzabili)

**PR title pattern**: `feat(<cluster>): migrate to canonical tokens (DS-<N>)`

**Branch pattern**: `feature/token-canonicalization-ds-<N>-<cluster>`

**Proposed cluster order** (rivedibile post DS-3):

| Stage | Cluster | Priority rationale |
|-------|---------|--------------------|
| DS-4 | `app/(authenticated)/dashboard` | Landing page utente — alta visibilità |
| DS-5 | `features/sessions/*` | 5 component, già toccati in batch B2 |
| DS-6 | `features/session-live/*` + `features/session-summary/*` | A11y critical |
| DS-7 | `features/games/*` + `app/.../games/*` | Catalogo utente |
| DS-8 | `features/agents/*` + `features/agent-detail/*` | Wave B.2 / γ |
| DS-9 | `features/players/*` + `features/player-detail/*` | Wave 3 pending |
| DS-10 | `features/gamebook/*` + `features/game-chat/*` | SP6 |
| DS-11 | `features/library/*` + `app/(authenticated)/library/*` | Multi-tab UI |

**Procedura per cluster**:
1. Rimuovi `bg-white|bg-slate-*|text-slate-*` hardcoded → `bg-background`, `text-muted-foreground`, etc. (token semantici).
2. Rimuovi qualsiasi `var(--bg-base)` etc. → `var(--bg)`.
3. Migra inline styles con valori non-token → tokens.
4. Aggiorna snapshot Playwright + Vitest.
5. Visual diff vs mockup HTML corrispondente (≤ 2%).
6. axe-core AA pass.
7. ESLint `lint:tokens` per il cluster → 0 violations (passa da `warn` → `error` solo per file del cluster).

**Acceptance criteria per cluster**:

- AC-N.1: 0 lint violations nel cluster.
- AC-N.2: Visual diff ≤ 2% vs mockup HTML.
- AC-N.3: A11y axe AA pass (0 violations).
- AC-N.4: Test suite passa per il cluster (unit + E2E).
- AC-N.5: Snapshot baseline rebake committato.

**Rollback**: per-cluster — revert del PR specifico, alias bridge ancora attivi, zero breakage cross-cluster.

### Stage DS-12 — Bridge removal + lint→error

**PR title**: `chore(design-system): remove bridge aliases, enforce token vocabulary (DS-12)`

**Branch**: `feature/token-canonicalization-ds-12`

**Pre-conditions**: stage DS-4..DS-11 mergiati. Zero violations cluster-wide.

**Deliverable**:
1. Rimuovi bridge alias da `globals.css`.
2. Elimina `premium-gaming.css` (nessun consumer post-migration).
3. Elimina `design-tokens.css` se ridondante.
4. Lint rules `meepleai-tokens/*` → mode `error` globale.
5. CI workflow: `pnpm lint:tokens` blocking.

**Acceptance criteria**:

- AC12.1: `grep -rE "bg-(white|slate-|gray-|zinc-)|--bg-base|--gaming-bg-" apps/web/src` → 0 matches (esclusi commenti e file di test).
- AC12.2: CI `pnpm lint:tokens` blocking ed verde su `main-dev`.
- AC12.3: Bundle size: -10 a -20% sul foglio di stile (eliminazione duplicazioni).

**Rollback**: revert PR. Bridge layer ripristinato per emergenza.

## 4. Failure matrix

| Stage | Failure mode | Detection | Mitigation |
|-------|--------------|-----------|------------|
| DS-1 | FOUC dark→light al primo paint | Manual review + Playwright trace inspection | `<html data-theme="light">` SSR hint hardcoded in layout.tsx |
| DS-1 | Snapshot regression mass | Visual regression test fail | Baseline rebake committato in stesso PR + screenshot review umano |
| DS-1 | A11y AA regression (es. contrast su light theme) | axe-core E2E | Block merge, fix tokens prima del merge |
| DS-2 | Lint rule false positive (entity colors esclusi male) | Test su un file pulito | Whitelist tuning |
| DS-3 | Cluster orphans (file non assegnabili) | Inventory review | Manual cluster assignment in DS-3 PR review |
| DS-4..DS-11 | Visual diff > 2% per cluster | Playwright pixelmatch | Block merge, investigate (può essere bug nel mockup) |
| DS-4..DS-11 | Cluster cross-import inconsistency | TypeScript build fail | Per-cluster atomic — rivedi import boundaries |
| DS-12 | Hidden consumer non migrato | Lint `pnpm lint:tokens --max-warnings 0` fail | Block PR, identifica consumer, riassegna cluster |

## 5. Sequencing & estimated effort

| Stage | PR count | Estimated effort | Parallelizzabile? |
|-------|----------|------------------|-------------------|
| DS-1 | 1 | 1g (incluso baseline rebake) | NO — fondazione |
| DS-2 | 1 | 0.5g | NO |
| DS-3 | 1 | 0.5g (doc) | NO |
| DS-4..DS-11 | 8 | 1-2g/cluster | SÌ post DS-3 |
| DS-12 | 1 | 0.5g + verifica | NO |

**Total**: ~14g con 1 dev FTE; ~7-8g con 2 dev paralleli sui cluster.

## 6. Open questions

- Q1: Esiste già un ESLint plugin custom in cui appendere le 2 nuove regole, o crearne uno nuovo? → Verificare in DS-2 kickoff.
- Q2: `[data-theme]` su `<html>` o su `<body>`? Mockup esempi mostrano `<html data-theme="light">`. Confermare in DS-1.
- Q3: Cosa fare di `--nh-bg-*` (warm-modern palette)? Consolidare in alias del canonico o ridichiarare in cluster-specific layer? → Risolto in DS-1 design.

## 7. References

- Mockup source: [`admin-mockups/design_files/`](../../../admin-mockups/design_files/)
- De-versioning spec (parent): [`2026-05-11-design-system-deversioning.md`](./2026-05-11-design-system-deversioning.md)
- Existing audit: [`2026-05-11-mockup-conformity.md`](../audits/2026-05-11-mockup-conformity.md)
- Token system v1 audit: [`v2-a11y-token-audit.md`](../frontend/v2-a11y-token-audit.md)
- next-themes attribute config: https://github.com/pacocoursey/next-themes#with-attribute

---

**Sign-off required from**:
- [x] Project owner — Strategy X confirmed 2026-05-12 (chat)
- [ ] Frontend architecture review — DS-1 PR review
- [ ] A11y review — DS-1 visual diff + axe baseline
