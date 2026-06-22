# P2 #807 — Design system v2 token redesign + a11y AA compliance

> **Date**: 2026-05-09
> **Owner**: @DegrassiAaron
> **Status**: ✅ COMPLETED 2026-05-10 (PR #876 — AA-compliant CSS vars + entity Tailwind utilities, sblocca #808)
> **Parent spec**: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md` (Goal P2)
> **Issues**: #807 (audit) + #808 (freeze policy lift)

## Context

### Goal di sessione

Risolvere le 30+ axe-core color-contrast violations sistemiche su SP6 v2 design tokens (`hsl(*, 89%, 48%)` foreground + `hsla(0.10)` semi-transparent bg), centralizzando i 194 inline HSL/HSLA dispersi in `components/v2/**` in un unico sistema di token CSS variables + Tailwind theme.

### Findings dalla discovery

- **194 inline HSL/HSLA** dispersi in components/v2 (grep audit 2026-05-09)
- Token sources frammentati: `tailwind.config.js` + `components/ui/data-display/meeple-card/tokens.ts` + 194 inline
- Inline values **divergono** già da HANDOFF.md (admin-mockups README) per 8 dei 9 entity tokens (kb è l'unico aligned)
- Issue #807 plan suggerisce shift specifici (event L=38, chat L=40, session L=35)
- HANDOFF.md prescrive HSL CSS variables (`--c-game`, etc) — non implementato attualmente

### Decisioni di scoping (confermate via brainstorming)

- **Centralization**: CSS variables in `globals.css` + Tailwind theme reference (option 3)
- **Target values**: HANDOFF-aligned + AA fallback (option C, table sotto)
- **Refactor scope**: full centralization 194 inline (option I — no legacy)
- **PR splitting**: atomic mega-PR per spec parent (option α)

## 1. Token architecture

### 1.1 CSS variables in `globals.css`

```css
:root {
  /* Entity color palette — AA-compliant L values for foreground use on bg-card #FFFFFF */
  /* Final values from audit Iter 2 (2026-05-09): tutti 18 ratios pass ≥ 4.5:1 */
  --c-game:    25 95% 38%;   /* 4.82:1 */
  --c-player:  262 83% 45%;  /* 8.55:1 */
  --c-session: 240 60% 35%;  /* 12.22:1 */
  --c-agent:   38 92% 32%;   /* 4.87:1 (was L=38, FAIL 3.61:1) */
  --c-kb:      174 60% 30%;  /* 5.12:1 (was L=40, FAIL 3.09:1 — cyan hue requires lower L) */
  --c-chat:    220 80% 40%;  /* 7.72:1 */
  --c-event:   350 89% 38%;  /* 6.79:1 */
  --c-toolkit: 142 70% 30%;  /* 4.88:1 (was L=35, FAIL 3.74:1) */
  --c-tool:    195 80% 32%;  /* 5.44:1 (was L=38, FAIL 4.08:1) */
}

.dark {
  /* Dark mode — AA-compliant on bg-card-dark, matches HANDOFF.md dark spec */
  --c-game:    28 95% 58%;
  --c-player:  262 75% 70%;
  --c-session: 235 70% 70%;
  --c-agent:   38 92% 62%;
  --c-kb:      174 60% 55%;
  --c-chat:    218 80% 68%;
  --c-event:   350 85% 70%;
  --c-toolkit: 142 60% 58%;
  --c-tool:    195 75% 62%;
}
```

### 1.2 Tailwind theme reference

```js
// apps/web/tailwind.config.js
module.exports = {
  theme: {
    extend: {
      colors: {
        game:    'hsl(var(--c-game) / <alpha-value>)',
        player:  'hsl(var(--c-player) / <alpha-value>)',
        session: 'hsl(var(--c-session) / <alpha-value>)',
        agent:   'hsl(var(--c-agent) / <alpha-value>)',
        kb:      'hsl(var(--c-kb) / <alpha-value>)',
        chat:    'hsl(var(--c-chat) / <alpha-value>)',
        event:   'hsl(var(--c-event) / <alpha-value>)',
        toolkit: 'hsl(var(--c-toolkit) / <alpha-value>)',
        tool:    'hsl(var(--c-tool) / <alpha-value>)',
      }
    }
  }
}
```

### 1.3 Utility class usage in components

```tsx
// Before (inline)
<div style={{ background: 'hsla(350, 89%, 48%, 0.10)', color: 'hsl(350, 89%, 48%)' }}>

// After (Tailwind)
<div className="bg-event/10 text-event">
```

Patterns covered: text, bg solid, bg with alpha, border, border with alpha, gradient, box-shadow accent, conditional entity-driven (lookup table).

### 1.4 MeepleCard tokens.ts compatibility (decision: keep as façade)

`apps/web/src/components/ui/data-display/meeple-card/tokens.ts` è consumato sia da componenti v2 che da componenti non-v2 (rotte legacy v1). Per evitare breaking change ai consumer legacy, il file resta come **backward-compatibility façade**.

Migration strategy:
1. Refactor `entityColors` map per leggere da CSS vars: es. `entityColors.event.h = 'var(--c-event)'` (struttura adattata)
2. Rimuovere literal HSL hardcoded duplicati
3. Aggiungere JSDoc `@deprecated since 2026-Q3 — prefer Tailwind utility classes (text-event, bg-event/10) for new code`
4. Tracking issue (NEW) per sunset del file quando tutti i consumer non-v2 migrano

Component callers esistenti continuano a funzionare senza touch (zero breaking change).

## 2. AA target values + audit deliverable

### 2.1 Target table (FINAL — post audit Iter 2 verification)

| Token | HANDOFF L | New L target | Real Ratio (#FFFFFF) | Status |
|-------|-----------|--------------|----------------------|--------|
| `--c-game` | 45% | **38%** | 4.82:1 | ✅ |
| `--c-player` | 58% | **45%** | 8.55:1 | ✅ |
| `--c-session` | 55% | **35%** | 12.22:1 | ✅ |
| `--c-agent` | 50% | **32%** | 4.87:1 | ✅ |
| `--c-kb` | 40% | **30%** | 5.12:1 | ✅ |
| `--c-chat` | 55% | **40%** | 7.72:1 | ✅ |
| `--c-event` | 60% | **38%** | 6.79:1 | ✅ |
| `--c-toolkit` | 45% | **30%** | 4.88:1 | ✅ |
| `--c-tool` | 50% | **32%** | 5.44:1 | ✅ |

> Iter 2 fix: kb (40→30), agent (38→32), toolkit (35→30), tool (38→32) — Iter 1 stime troppo ottimistiche per cyan/teal/yellow hues (perceptually light, richiedono L significativamente più basso). Vedi `docs/for-developers/frontend/v2-a11y-token-audit.md` per iteration history.

### 2.2 Dark mode AA verification (target ratios)

I valori dark sono presi da HANDOFF.md ma DEVONO essere verificati in audit Fase 1 contro `bg-card-dark` (#1e1710 da tokens.css). Stime preliminari:

| Token | Dark HSL | Estimated Ratio on bg-card-dark | Status |
|-------|----------|--------------------------------|--------|
| `--c-game` | 28 95% 58% | ~6.8:1 | ✅ pass est. |
| `--c-player` | 262 75% 70% | ~5.9:1 | ✅ pass est. |
| `--c-session` | 235 70% 70% | ~5.4:1 | ✅ pass est. |
| `--c-agent` | 38 92% 62% | ~7.2:1 | ✅ pass est. |
| `--c-kb` | 174 60% 55% | ~5.1:1 | ✅ pass est. |
| `--c-chat` | 218 80% 68% | ~5.2:1 | ✅ pass est. |
| `--c-event` | 350 85% 70% | ~6.0:1 | ✅ pass est. |
| `--c-toolkit` | 142 60% 58% | ~5.5:1 | ✅ pass est. |
| `--c-tool` | 195 75% 62% | ~5.8:1 | ✅ pass est. |

> Verificare in audit Fase 1 con tool reale (no eye-balling). Se qualunque dark token fail → adjust L value e re-baseline visual regression.

### 2.3 Audit deliverable

File: `docs/for-developers/frontend/v2-a11y-token-audit.md`

Structure:
- Methodology (WCAG 2.1 AA SC 1.4.3 ≥ 4.5:1 normal, ≥ 3:1 large/UI)
- Token contrast matrix (tabella sopra con ratios reali post-audit)
- Dark mode parity check
- Calculation script reference: `tools/a11y/contrast-calc.ts`

## 3. Refactor strategy 194 inline

### 3.1 Pattern → replacement mapping

| Pattern | Inline form | New form |
|---------|-------------|----------|
| Solid color text | `style={{color:'hsl(350,89%,48%)'}}` | `className="text-event"` |
| Bg solid | `style={{background:'hsl(350,89%,48%)'}}` | `className="bg-event"` |
| Bg with alpha | `style={{background:'hsla(350,89%,48%,0.10)'}}` | `className="bg-event/10"` |
| Border | `style={{borderColor:'hsl(350,89%,48%)'}}` | `className="border-event"` |
| Border with alpha | `style={{borderColor:'hsla(350,89%,48%,0.30)'}}` | `className="border-event/30"` |
| Gradient (decorative) | `linear-gradient(135deg, hsl(...), hsl(...))` | `className="bg-gradient-to-br from-game to-event"` |
| Box-shadow accent | `boxShadow:'0 0 0 2px hsl(350,89%,48%)'` | `className="ring-2 ring-event"` |
| Conditional entity-driven | `style={{color:\`hsl(${entity.h},${entity.s}%,${entity.l}%)\`}}` | Tailwind class lookup table (vedi 3.1.1 sotto) |

#### 3.1.1 Conditional entity-driven concrete pattern

```tsx
// Before (legacy)
const entity = { h: 350, s: 89, l: 48 };  // event entity
return <div style={{ color: `hsl(${entity.h}, ${entity.s}%, ${entity.l}%)` }}>...</div>;

// After (Tailwind class lookup)
type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

const ENTITY_TEXT_CLASS: Record<EntityType, string> = {
  game:    'text-game',
  player:  'text-player',
  session: 'text-session',
  agent:   'text-agent',
  kb:      'text-kb',
  chat:    'text-chat',
  event:   'text-event',
  toolkit: 'text-toolkit',
  tool:    'text-tool',
};

const ENTITY_BG_SOFT_CLASS: Record<EntityType, string> = {
  game:    'bg-game/10',
  player:  'bg-player/10',
  /* ... */
};

return <div className={ENTITY_TEXT_CLASS[entity.type]}>...</div>;
```

Lookup table file location: `apps/web/src/lib/design/entity-classes.ts` (NEW file, esportato come parte del token system).

### 3.2 HSL → entity mapping for codemod

Inline HSL values devono essere disambiguati prima del replace. La mappa è costruita da grep audit pre-implementazione (snapshot 2026-05-09, da ricomputare al kickoff PR per assorbire concurrent work).

#### 3.2.1 Layer 1 — HSL signature lookup

| Entity | Inline signatures attuali (light) | Inline signatures attuali (dark) |
|--------|-----------------------------------|----------------------------------|
| `game` | `hsl(25, 95%, 39%)`, `hsl(25, 95%, 45%)`, `hsla(25, 95%, *, *)` | `hsl(28, 95%, 58%)` |
| `player` | `hsl(262, 83%, 58%)`, `hsla(262, 83%, *, *)` | `hsl(262, 75%, 70%)` |
| `session` | `hsl(240, 60%, 45%)`, `hsl(240, 60%, 55%)`, `hsl(240, 40%, 10%)` (legacy) | `hsl(235, 70%, 70%)` |
| `agent` | `hsl(38, 92%, 50%)` | `hsl(38, 92%, 62%)` |
| `kb` | `hsl(174, 60%, 40%)`, `hsl(210, 40%, 48%)` (legacy alias) | `hsl(174, 60%, 55%)` |
| `chat` | `hsl(220, 80%, 55%)` | `hsl(218, 80%, 68%)` |
| `event` | `hsl(350, 89%, 48%)`, `hsl(350, 89%, 60%)` | `hsl(350, 85%, 70%)` |
| `toolkit` | `hsl(142, 70%, 31%)`, `hsl(142, 70%, 45%)`, `hsl(142,70%,25%)` | `hsl(142, 60%, 58%)` |
| `tool` | `hsl(195, 80%, 50%)` | `hsl(195, 75%, 62%)` |

Match tolerance: hue ±5°, saturation ±10pp (entity assignment è hue-dominated).

#### 3.2.2 Layer 2 — Filename heuristics (fallback)

When Layer 1 ambiguous (es. shared hue between game e toolkit varianti):
- File path includes `gamebook/` → bias verso `game` (default if conflict)
- File path includes `agents/` → bias verso `agent`
- File path includes `library/` → bias verso `game`
- File path includes `sessions/` → bias verso `session`

#### 3.2.3 Layer 3 — Manual review (Phase B)

Codemod outputs `tools/codemods/output/unmapped-hsl-occurrences.json` per occorrenze non risolte. Human reviewer assegna manualmente.

### 3.3 Refactor execution

**Phase A — Mechanical replacement (codemod jscodeshift)**:
- Custom AST script in `tools/codemods/inline-hsl-to-tailwind.js`
- Parse JSX `style={{...}}` props
- Match HSL/HSLA literals against table 3.2.1
- Replace with `className="..."` Tailwind utility
- Output unmapped occurrences a JSON per Layer 3 review

**Phase B — Manual review pass**:
- Conditional/dynamic styles (template literals) → entity-class-lookup table
- Edge cases (multi-rule single style) → unwind manually
- Per-file review per BC (gamebook, agents, library, sessions, players, games)

**Phase C — Verification**:
- `grep -rEn "hsl[a]?\(" apps/web/src/components/v2/` → 0 results
- TypeScript build green
- Visual diff vs old: COLORS-only, no layout shift > 0.5% (Playwright `toHaveScreenshot({ maxDiffPixelRatio: 0.005 })`)

### 3.4 Codemod failure mode + rollback

Se Phase A codemod produce output incorretti su parte del codebase (es. classe Tailwind sbagliata, side-effect su stile non-target):

1. **Pre-commit safety**: codemod runs su un dry-run mode prima di commit, output di diff a `/tmp/codemod-dry-run.diff` per review umana
2. **Per-file rollback**: ogni file refactored ha commit atomico separato (no big-bang single commit). Bug specifico → `git revert` solo quel commit
3. **Rollback granulare**: se 80% file OK ma 20% fail, revert mirato dei 20% via cherry-pick selettivo
4. **Worst case**: full revert dell'intera sezione codemod commits, manual refactor ripartendo dalla mappa 3.2.1

### 3.5 Lint enforcement post-merge

Custom ESLint rule `meepleai/no-inline-hsl-v2` richiede infrastructure setup:

#### 3.5.1 Plugin module setup

1. Crea local plugin `tools/eslint-plugin-meepleai/` con `package.json` privato (no NPM publish)
2. Implementa rule `no-inline-hsl-v2` (~50 righe TypeScript usando `@typescript-eslint/utils`)
3. Aggiungi a `apps/web/package.json` come local dependency: `"@meepleai/eslint-plugin": "file:../../tools/eslint-plugin-meepleai"`
4. Configura in `apps/web/.eslintrc.cjs` (verificare estensione corretta — repo usa `.eslintrc.cjs` o `.js`)

```js
// apps/web/.eslintrc.cjs (verificare extension at PR start)
module.exports = {
  plugins: ['@meepleai'],
  overrides: [
    {
      files: ['src/components/v2/**/*.{tsx,ts}'],
      rules: {
        '@meepleai/no-inline-hsl-v2': ['error', {
          message: 'Use Tailwind entity tokens (text-event, bg-event/10) instead of inline hsl()'
        }]
      }
    }
  ]
};
```

#### 3.5.2 Effort + scope

Custom rule infrastructure è ~0.5-1 giorno aggiuntivo non originally budgeted in "1-1.5 sett single dev". Estimate riguardanti i totali aggiornati: **1.5-2 settimane single dev**.

Future PR con HSL inline su `components/v2/**` FAIL CI.

## 4. Visual baseline regen

```
1. Pre-merge: bootstrap workflow visual-regression-migrated.yml mode=bootstrap
   - 16+ v2 routes + SP6 routes
   - 5 stati × 2 viewport (desktop + mobile) = ~10 PNG per route
   - Total: ~80-120 PNG canonical Linux x86-64

2. Diff atteso: COLORS only, NO layout shift
   - Manual spot check 5-10 PNG per BC (gamebook/agents/library/sessions/players/games)
   - Eventuali layout shift = bug refactor da investigare

3. Commit baselines come parte del PR (no separate "baselines" commit)
```

## 5. Testing strategy

| Test | Status |
|------|--------|
| Unit (token math) | NON necessari (purely visual concern) |
| E2E A11y `pnpm test:e2e:a11y` | 0 violations serious/critical su tutte le rotte v2 |
| CI gate "Frontend - A11y E2E" | da `continue-on-error: true` → `false` (blocking) |
| Visual regression baselines | rebuilt come parte del PR |
| Manual spot check | 5-10 PNG per BC |
| TypeScript `pnpm typecheck` | green |
| Frontend build `pnpm build` | green |

## 6. PR scope + DoD

### 6.1 Branch + base

- **Branch**: `feature/p2-807-token-redesign`
- **Base**: `main-dev`

### 6.2 Files changed (estimate)

- `apps/web/src/app/globals.css` (+30 lines CSS vars)
- `apps/web/tailwind.config.js` (+30 lines colors entry)
- `apps/web/.eslintrc.cjs` (+5 lines lint rule)
- `apps/web/src/components/v2/**/*.tsx` (~194 inline rimossi → utility classes — count snapshot 2026-05-09, ricomputare al PR kickoff per assorbire concurrent work)
- `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` (refactor a façade backward-compat — vedi 1.4)
- `apps/web/src/lib/design/entity-classes.ts` (NEW lookup table per pattern conditional, vedi 3.1.1)
- `tools/eslint-plugin-meepleai/` (NEW local ESLint plugin per rule no-inline-hsl-v2)
- `apps/web/e2e/.../*.spec.ts` (eventuali update minor)
- `apps/web/e2e/.../*-baseline.png` (~80-120 PNG rigenerati)
- `docs/for-developers/frontend/v2-a11y-token-audit.md` (NEW deliverable)
- `docs/for-developers/frontend/v2-token-system.md` (NEW reference doc)
- `tools/codemods/inline-hsl-to-tailwind.js` (NEW codemod)
- `tools/a11y/contrast-calc.ts` (NEW audit tool)

**Estimated PR size**: ~250-400 files changed
**Estimated review time**: 2-3 settimane
**Estimated implementation**: 1.5-2 settimane single dev (include +0.5-1g per ESLint plugin infrastructure setup, vedi 3.5.2)

### 6.3 Definition of Done

- [ ] `grep -rEn "hsl[a]?\(" apps/web/src/components/v2/` → 0 results
- [ ] `pnpm test:e2e:a11y` → 0 violations serious/critical su 13 rotte v2 (vedi 6.3.1)
- [ ] CI job "Frontend - A11y E2E" blocking (`continue-on-error: false`)
- [ ] Visual baselines rigenerati senza layout shift > 0.5% (Playwright `maxDiffPixelRatio: 0.005`)
- [ ] #807 Fase 1 + Fase 2 entrambi closed
- [ ] #808 freeze policy lifted (commento "lift criteria met")
- [ ] `v2-migration-matrix.md` aggiornato con status "freeze lifted, pending rows pickable"
- [ ] Custom ESLint rule `@meepleai/no-inline-hsl-v2` attiva e fail-on-violation (vedi 3.5)
- [ ] Audit deliverable `v2-a11y-token-audit.md` committato
- [ ] Reference doc `v2-token-system.md` committato (struttura in 6.4)
- [ ] Tracking issue NEW per sunset graduale di `meeple-card/tokens.ts` façade

### 6.3.1 Routes covered by A11y E2E gate (DoD)

13 rotte v2 done as of 2026-05-09 (mirror v2-migration-matrix done rows):

| Wave | Route |
|------|-------|
| Wave A/B | `/games?tab=library` |
| Wave A/B | `/agents` |
| Wave A/B | `/library` |
| Wave A/B | `/games/[id]` |
| Wave A/B | `/agents/[id]` |
| Wave D | `/sessions` |
| Wave D | `/sessions/[id]/live` |
| Wave D | `/sessions/[id]` |
| Wave 3 | `/players/[id]` |
| Wave 4 | `/players` |
| SP6 | `/gamebook` |
| SP6 | `/gamebook/upload` |
| SP6 | `/library/games/[gameId]/translate` |

**Coverage**: 13 routes × 6 user-state fixtures (default/empty/loading/error/quota-soft/quota-hard se applicabile) × 2 viewports (desktop/mobile) ≈ 156 axe-core scan run nel job E2E A11y.

**Out of scope** (Wave 3 frontend pending): `/discover`, `/game-nights`, `/kb/[id]`, `/toolkits/[id]` — sbloccate POST P2 merge per implementazione.

### 6.4 Reference doc structure: v2-token-system.md

File: `docs/for-developers/frontend/v2-token-system.md` (~150-200 righe)

Sections:
1. **Overview** — token system architecture (CSS vars + Tailwind theme reference)
2. **Token catalog** — 9 entity tokens table (name, light HSL, dark HSL, AA ratio, intended use)
3. **Usage examples** — `text-`, `bg-`, `border-`, alpha modifier `/N`, gradient, conditional patterns (link a 3.1.1)
4. **Dark mode behavior** — `data-theme="dark"` toggle on `<html>`, CSS var swap, no component-level changes needed
5. **Migration guide** — converting legacy inline HSL to Tailwind utility (link a 3.2.1 mapping table)
6. **ESLint enforcement** — `@meepleai/no-inline-hsl-v2` rule reference + how to disable per-line con justification (es. `// eslint-disable-next-line @meepleai/no-inline-hsl-v2 -- third-party color hardcoded`)
7. **Adding new tokens** — process per estendere palette (rare — only if new entity introduced)
8. **Audit reference** — link a `v2-a11y-token-audit.md` per ratios storiche

## 7. Sequencing post-PR merge

```
1. PR P2 #807 merged a main-dev → release PR main-dev → main-staging → deploy
2. #808 lifted → Wave 3 frontend rotte rimanenti unblocked:
   - /discover (Tier L Phase 0.5)
   - /game-nights (Tier L Phase 0.5)
   - /kb/[id] (Tier M)
   - /toolkits/[id] (Tier M)
3. P3 plan execution (Lighthouse CI URL list update + bundle budget hard 20MB + first-deploy baseline JSON)
```

## 8. Out of scope (esplicitamente)

- **Wave 3 frontend rotte rimanenti**: implementate POST P2 PR merge (sblocca freeze)
- **P3 perf absolute thresholds**: separato goal (vedi parent spec)
- **Migration di componenti legacy v1 a v2**: out-of-scope, P2 si occupa solo di token redesign su componenti v2 ESISTENTI
- **Dark mode toggle UI**: già implementato, P2 solo cambia variabili CSS attese
- **Token system per non-v2 components** (legacy v1, admin/, _components/): non toccati
- **Backend changes**: nessuno, è purely frontend
- **Risk-based regression test esteso**: spot check manuale + automated visual regression sufficiente per atomic PR

## 9. Riferimenti

- Parent spec: `docs/superpowers/specs/2026-05-07-v2-meepleai-deploy-goals.md` (Goal P2)
- Issue #807 — A11y design system audit
- Issue #808 — Freeze SP6 v2 expansion (policy)
- HANDOFF.md (admin-mockups README) — design intent originale
- Existing token: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`
- v2 migration matrix: `docs/for-developers/frontend/v2-migration-matrix.md`

## Self-review checklist (post C1-C4 + I1-I5 fix)

- ✅ **Placeholder scan**: nessun TBD/TODO non risolto. Audit values labelled "estimated, raffinabile in Fase 1" — è esplicita ambiguity bounded. ESLint config extension `.eslintrc.cjs` flagged "verificare extension at PR start" (M1 documented).
- ✅ **Internal consistency**: token name `--c-event` consistente in CSS + Tailwind config + utility class examples. Light/dark values coerenti con HANDOFF.md dark spec dove applicabile. Section numbering corretto (3.1, 3.1.1, 3.2, 3.2.1-3, 3.3, 3.4, 3.5).
- ✅ **Scope check**: P2 #807 atomic mega-PR. 194 inline + token redesign + visual baseline + audit deliverable + lint rule + ESLint plugin local + entity-classes lookup. Coerent con spec parent "atomic same PR". Estimate updated 1.5-2 settimane (was 1-1.5).
- ✅ **Ambiguity check**: AA target values esplicite + raffinabile in audit; refactor pattern table esplicita per ogni scenario incl. conditional entity-driven (3.1.1) e codemod entity disambiguation (3.2.1-3); DoD ha 11 checkable boxes con 13 rotte enumerate (6.3.1) e v2-token-system.md struttura definita (6.4).

## Revision log

- **2026-05-09 — initial draft** (sezione 1-9 + DoD)
- **2026-05-09 — review fix C1-C4 + I1-I5**: 
  - C1: aggiunta sezione 3.2 HSL→entity mapping (3-layer disambiguation)
  - C2: aggiunta sezione 1.4 MeepleCard tokens.ts façade decision
  - C3: aggiunta sezione 6.3.1 Routes enumeration
  - C4: aggiunta sezione 6.4 v2-token-system.md structure
  - I1: aggiunta sezione 3.5.1-2 ESLint plugin infrastructure setup
  - I2: aggiunta sezione 2.2 Dark mode AA verification
  - I3: aggiunta sezione 3.1.1 Conditional entity-driven concrete pattern
  - I4: corretto findings 7→8 entity tokens
  - I5: aggiunta sezione 3.4 Codemod failure mode + rollback
  - M1: ESLint config extension flagged "verificare at PR start"
  - M2: 194 count flagged "ricomputare al kickoff"
  - M3: layout shift 0.5% threshold con tool config (Playwright maxDiffPixelRatio: 0.005)
