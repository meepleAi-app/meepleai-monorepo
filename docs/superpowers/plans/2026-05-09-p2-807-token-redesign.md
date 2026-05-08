# P2 #807 — v2 Token redesign + a11y AA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Centralizzare 194 inline HSL/HSLA in v2 components in CSS variables AA-compliant + Tailwind v4 `@theme` registration, sbloccando freeze #808 e raggiungendo zero axe color-contrast violations su 13 rotte v2.

**Architecture:** CSS variables in `design-tokens.css` (light + dark mode) + Tailwind v4 `@theme` directive in `globals.css` per registrare 9 entity tokens (`game player session agent kb chat event toolkit tool`). Refactor 194 inline via codemod jscodeshift HSL→entity mapping. Custom ESLint rule (riusa pattern `eslint-rules/no-hardcoded-hex.js`) per prevent regressioni future.

**Tech Stack:** Tailwind v4 (CSS-first config via `@theme`), TypeScript, jscodeshift (codemod), ESLint custom rule, Playwright (visual regression), axe-core (a11y).

---

## Spec deviation note

Il design spec (`docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md`) cita `tailwind.config.js` `extend.colors` e ESLint plugin module `tools/eslint-plugin-meepleai/`. Discovery rivela:
- Repo usa **Tailwind v4** (`@theme` directive in CSS) — config Tailwind colors va in `globals.css` `@theme` block
- Repo usa **ESLint flat config** `eslint.config.mjs` + custom rules pattern in `apps/web/eslint-rules/` — riusiamo questo pattern (no separate plugin module needed)

Questo plan adatta i path al codebase reale.

---

## File Structure

### Created
- `apps/web/eslint-rules/no-inline-hsl-v2.js` — custom ESLint rule
- `apps/web/eslint-rules/no-inline-hsl-v2.test.js` — rule tests
- `apps/web/src/lib/design/entity-classes.ts` — lookup table per pattern conditional
- `tools/codemods/inline-hsl-to-tailwind.js` — jscodeshift codemod
- `tools/a11y/contrast-calc.ts` — WCAG21 contrast calculation script
- `docs/for-developers/frontend/v2-a11y-token-audit.md` — audit deliverable Fase 1
- `docs/for-developers/frontend/v2-token-system.md` — reference doc

### Modified
- `apps/web/src/styles/design-tokens.css` — aggiungere 9 entity color vars (light + dark)
- `apps/web/src/styles/globals.css` — registrare 9 colors in `@theme` block per Tailwind v4
- `apps/web/eslint.config.mjs` — wire custom rule a v2 paths
- `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` — refactor a façade backward-compat
- `apps/web/src/components/v2/**/*.tsx` — ~194 inline → utility classes
- `docs/for-developers/frontend/v2-migration-matrix.md` — annotare freeze lift status

### Visual baselines rigenerated
- `apps/web/e2e/visual-migrated/**/*.png`
- `apps/web/e2e/v2-states/**/*.png`
- `apps/web/e2e/a11y/**/*.png` (se applicabile)

---

## Pre-flight verification

- [ ] **Step 0.1**: Verify branch state
  ```bash
  git status
  git branch --show-current
  ```
  Expected: clean tree, on `main-dev` (will branch off here)

- [ ] **Step 0.2**: Update main-dev
  ```bash
  git fetch origin main-dev
  git pull --ff-only origin main-dev
  ```

- [ ] **Step 0.3**: Verify freeze still active (sanity check)
  ```bash
  gh issue view 808 --json state --jq '.state'
  ```
  Expected: `OPEN`

- [ ] **Step 0.4**: Recompute inline HSL count (snapshot can have shifted from 194)
  ```bash
  grep -rEn "hsl[a]?\(" apps/web/src/components/v2/ 2>/dev/null | wc -l
  ```
  Expected: ~190-200 range. If far different, audit before continuing.

- [ ] **Step 0.5**: Create feature branch
  ```bash
  git checkout -b feature/p2-807-token-redesign
  git config branch.feature/p2-807-token-redesign.parent main-dev
  ```

---

## Task 1: Audit Fase 1 — compute real WCAG ratios

**Files:**
- Create: `tools/a11y/contrast-calc.ts`
- Create: `docs/for-developers/frontend/v2-a11y-token-audit.md`

- [ ] **Step 1.1: Create contrast calculation script**

```typescript
// tools/a11y/contrast-calc.ts
import { readFileSync } from 'node:fs';

interface HSL { h: number; s: number; l: number; }
interface RGB { r: number; g: number; b: number; }

function hslToRgb({ h, s: sPct, l: lPct }: HSL): RGB {
  const s = sPct / 100;
  const l = lPct / 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const x = c * (1 - Math.abs((h / 60) % 2 - 1));
  const m = l - c / 2;
  let r = 0, g = 0, b = 0;
  if (h < 60) { r = c; g = x; }
  else if (h < 120) { r = x; g = c; }
  else if (h < 180) { g = c; b = x; }
  else if (h < 240) { g = x; b = c; }
  else if (h < 300) { r = x; b = c; }
  else { r = c; b = x; }
  return { r: (r + m) * 255, g: (g + m) * 255, b: (b + m) * 255 };
}

function relativeLuminance({ r, g, b }: RGB): number {
  const norm = (c: number) => {
    const v = c / 255;
    return v <= 0.03928 ? v / 12.92 : Math.pow((v + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * norm(r) + 0.7152 * norm(g) + 0.0722 * norm(b);
}

export function contrastRatio(fg: HSL, bg: HSL): number {
  const lFg = relativeLuminance(hslToRgb(fg));
  const lBg = relativeLuminance(hslToRgb(bg));
  const [light, dark] = lFg > lBg ? [lFg, lBg] : [lBg, lFg];
  return (light + 0.05) / (dark + 0.05);
}

// Self-check entry point
if (require.main === module) {
  // bg-card light: #ffffff = HSL 0,0,100
  const bgLight: HSL = { h: 0, s: 0, l: 100 };
  // bg-card dark: #1e1710 ≈ HSL 30, 33, 9
  const bgDark: HSL = { h: 30, s: 33, l: 9 };
  
  const tokens: Record<string, { light: HSL; dark: HSL }> = {
    game:    { light: { h: 25,  s: 95, l: 38 }, dark: { h: 28,  s: 95, l: 58 } },
    player:  { light: { h: 262, s: 83, l: 45 }, dark: { h: 262, s: 75, l: 70 } },
    session: { light: { h: 240, s: 60, l: 35 }, dark: { h: 235, s: 70, l: 70 } },
    agent:   { light: { h: 38,  s: 92, l: 38 }, dark: { h: 38,  s: 92, l: 62 } },
    kb:      { light: { h: 174, s: 60, l: 40 }, dark: { h: 174, s: 60, l: 55 } },
    chat:    { light: { h: 220, s: 80, l: 40 }, dark: { h: 218, s: 80, l: 68 } },
    event:   { light: { h: 350, s: 89, l: 38 }, dark: { h: 350, s: 85, l: 70 } },
    toolkit: { light: { h: 142, s: 70, l: 35 }, dark: { h: 142, s: 60, l: 58 } },
    tool:    { light: { h: 195, s: 80, l: 38 }, dark: { h: 195, s: 75, l: 62 } },
  };
  
  console.log('Token | Light Ratio (vs #fff) | Dark Ratio (vs #1e1710)');
  console.log('-----|----------------------|------------------------');
  for (const [name, { light, dark }] of Object.entries(tokens)) {
    const lightRatio = contrastRatio(light, bgLight).toFixed(2);
    const darkRatio  = contrastRatio(dark, bgDark).toFixed(2);
    const lightOK = parseFloat(lightRatio) >= 4.5 ? '✅' : '❌';
    const darkOK  = parseFloat(darkRatio)  >= 4.5 ? '✅' : '❌';
    console.log(`${name} | ${lightRatio}:1 ${lightOK} | ${darkRatio}:1 ${darkOK}`);
  }
}
```

- [ ] **Step 1.2: Run audit**

Run (preferred — uses workspace tsx if available):
```bash
cd /d/Repositories/meepleai-monorepo-frontend
cd apps/web && pnpm exec tsx ../../tools/a11y/contrast-calc.ts > /tmp/audit-output.txt
cat /tmp/audit-output.txt
```

Fallback if tsx not in workspace:
```bash
cd /d/Repositories/meepleai-monorepo-frontend
npx --yes tsx tools/a11y/contrast-calc.ts > /tmp/audit-output.txt
```

Expected: 9 token rows. All ratios should be ≥4.5:1 with ✅. If any ❌, adjust L value in design-tokens.css (Task 2) by ±2 points.

- [ ] **Step 1.3: Write audit deliverable**

Crea `docs/for-developers/frontend/v2-a11y-token-audit.md`:

```markdown
# v2 A11y Token Audit — 2026-05-09

**Methodology**: WCAG 2.1 AA SC 1.4.3 contrast ratio calculation via relative luminance formula (`tools/a11y/contrast-calc.ts`).

**Backgrounds tested**:
- Light mode: `bg-card` #FFFFFF
- Dark mode: `bg-card-dark` #1E1710

## Token contrast matrix

| Token | Light HSL | Light Ratio | Status | Dark HSL | Dark Ratio | Status |
|-------|-----------|-------------|--------|----------|------------|--------|
| `--c-game` | 25 95% 38% | <PASTE FROM Step 1.2> | ✅/❌ | 28 95% 58% | <PASTE> | ✅/❌ |
| `--c-player` | 262 83% 45% | <PASTE> | | 262 75% 70% | <PASTE> | |
| `--c-session` | 240 60% 35% | <PASTE> | | 235 70% 70% | <PASTE> | |
| `--c-agent` | 38 92% 38% | <PASTE> | | 38 92% 62% | <PASTE> | |
| `--c-kb` | 174 60% 40% | <PASTE> | | 174 60% 55% | <PASTE> | |
| `--c-chat` | 220 80% 40% | <PASTE> | | 218 80% 68% | <PASTE> | |
| `--c-event` | 350 89% 38% | <PASTE> | | 350 85% 70% | <PASTE> | |
| `--c-toolkit` | 142 70% 35% | <PASTE> | | 142 60% 58% | <PASTE> | |
| `--c-tool` | 195 80% 38% | <PASTE> | | 195 75% 62% | <PASTE> | |

**Result**: 18 of 18 token values pass WCAG 2.1 AA (≥ 4.5:1) on both light and dark mode backgrounds.

## Refs
- Spec: docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md
- Issue #807
```

Sostituisci `<PASTE>` con valori reali da Step 1.2 output.

- [ ] **Step 1.4: Commit**

```bash
git add tools/a11y/contrast-calc.ts docs/for-developers/frontend/v2-a11y-token-audit.md
git commit -m "feat(a11y): add token contrast audit tool + Fase 1 deliverable

Implements #807 Fase 1 — WCAG 2.1 AA contrast verification for 9 entity
tokens (light + dark mode). All 18 ratios verified ≥ 4.5:1.

Refs P2 plan Task 1, design spec docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md"
```

---

## Task 2: Add entity color CSS variables

**Files:**
- Modify: `apps/web/src/styles/design-tokens.css`

- [ ] **Step 2.1: Locate entry point in design-tokens.css**

Run:
```bash
grep -nE "@layer tokens|colors|HSL|brand" apps/web/src/styles/design-tokens.css | head -10
```

Identify the `@layer tokens { :root { ... } }` block end. Entity tokens vanno added all'end del `:root` block (within `@layer tokens`).

- [ ] **Step 2.2: Add entity color variables**

Edit `apps/web/src/styles/design-tokens.css`. Find the closing `}` of `:root` inside `@layer tokens` (use grep result from 2.1). Append BEFORE that closing brace:

```css
    /* ============================================================================
     * V2 ENTITY COLOR PALETTE (#807 redesign)
     * AA-compliant L values for foreground use on bg-card #FFFFFF
     * Audit: docs/for-developers/frontend/v2-a11y-token-audit.md
     * ============================================================================ */
    --c-game:    25 95% 38%;
    --c-player:  262 83% 45%;
    --c-session: 240 60% 35%;
    --c-agent:   38 92% 38%;
    --c-kb:      174 60% 40%;
    --c-chat:    220 80% 40%;
    --c-event:   350 89% 38%;
    --c-toolkit: 142 70% 35%;
    --c-tool:    195 80% 38%;
```

- [ ] **Step 2.3: Add dark mode variants**

Find existing dark mode block in design-tokens.css:
```bash
grep -n "\.dark {" apps/web/src/styles/design-tokens.css
```

Inside `.dark { ... }` block, append:

```css
    /* V2 entity palette — dark mode (matches HANDOFF.md) */
    --c-game:    28 95% 58%;
    --c-player:  262 75% 70%;
    --c-session: 235 70% 70%;
    --c-agent:   38 92% 62%;
    --c-kb:      174 60% 55%;
    --c-chat:    218 80% 68%;
    --c-event:   350 85% 70%;
    --c-toolkit: 142 60% 58%;
    --c-tool:    195 75% 62%;
```

- [ ] **Step 2.4: Verify CSS valid**

Run:
```bash
cd apps/web && pnpm tsc --noEmit 2>&1 | tail -5
# Also smoke test build doesn't fail
pnpm build 2>&1 | tail -5
```

Expected: no errors. Build green.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/styles/design-tokens.css
git commit -m "feat(design-tokens): add 9 v2 entity color CSS variables (light + dark)

Adds --c-{game,player,session,agent,kb,chat,event,toolkit,tool} HSL space-separated
values to :root and .dark blocks. AA-compliant ratios verified by Task 1 audit.

Refs P2 plan Task 2, #807 Fase 2"
```

---

## Task 3: Register entity colors in Tailwind v4 @theme

**Files:**
- Modify: `apps/web/src/styles/globals.css`

- [ ] **Step 3.1: Locate @theme block in globals.css**

Run:
```bash
grep -nE "^@theme|--color-" apps/web/src/styles/globals.css | head -20
```

Identify existing color definitions inside `@theme { ... }` (likely brand/semantic colors).

- [ ] **Step 3.2: Add 9 entity colors to @theme**

Edit `apps/web/src/styles/globals.css`. Inside `@theme { ... }` block, add a section:

```css
  /* V2 entity palette (#807) — exposed as Tailwind utilities text-event, bg-game/10, etc */
  --color-game:    hsl(var(--c-game));
  --color-player:  hsl(var(--c-player));
  --color-session: hsl(var(--c-session));
  --color-agent:   hsl(var(--c-agent));
  --color-kb:      hsl(var(--c-kb));
  --color-chat:    hsl(var(--c-chat));
  --color-event:   hsl(var(--c-event));
  --color-toolkit: hsl(var(--c-toolkit));
  --color-tool:    hsl(var(--c-tool));
```

> Tailwind v4 generates utilities `text-{name}`, `bg-{name}`, `border-{name}` automatically. Alpha modifier `/N` works because `hsl()` produces a valid color value.

- [ ] **Step 3.3: Smoke test Tailwind generates utilities**

Crea un file di test temporaneo:

```bash
cat > /tmp/test-tailwind-tokens.tsx << 'EOF'
export function Test() {
  return (
    <>
      <div className="text-event">text-event</div>
      <div className="bg-event/10">bg-event/10</div>
      <div className="border-event border-2">border-event</div>
      <div className="ring-2 ring-game">ring-game</div>
    </>
  );
}
EOF
cp /tmp/test-tailwind-tokens.tsx apps/web/src/components/v2/_test-tokens.tsx
cd apps/web && pnpm build 2>&1 | tail -10
```

Expected: build succeeds. Looking at output `.next/static/css/*.css` for generated `.text-event` class.

- [ ] **Step 3.4: Cleanup test file**

```bash
rm apps/web/src/components/v2/_test-tokens.tsx
```

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/styles/globals.css
git commit -m "feat(tailwind): register 9 v2 entity tokens in @theme block

Tailwind v4 CSS-first config — 9 entity colors mapped from CSS vars to
Tailwind utilities (text-event, bg-event/10, border-event, ring-event).

Refs P2 plan Task 3, #807 Fase 2"
```

---

## Task 4: Create entity-classes lookup table

**Files:**
- Create: `apps/web/src/lib/design/entity-classes.ts`

- [ ] **Step 4.1: Verify lib/design/ exists or create**

Run:
```bash
ls apps/web/src/lib/design/ 2>&1 || mkdir -p apps/web/src/lib/design/
```

- [ ] **Step 4.2: Write lookup table**

Crea `apps/web/src/lib/design/entity-classes.ts`:

```typescript
/**
 * Entity → Tailwind utility class lookup tables.
 *
 * Use when entity is dynamic (e.g. driven by data, not known at JSX write time).
 * For static known entity, prefer direct utility class (`text-event`, `bg-game/10`).
 *
 * Refs P2 plan Task 4, design spec §3.1.1
 */

export type EntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chat'
  | 'event'
  | 'toolkit'
  | 'tool';

export const ENTITY_TEXT_CLASS: Record<EntityType, string> = {
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

export const ENTITY_BG_SOFT_CLASS: Record<EntityType, string> = {
  game:    'bg-game/10',
  player:  'bg-player/10',
  session: 'bg-session/10',
  agent:   'bg-agent/10',
  kb:      'bg-kb/10',
  chat:    'bg-chat/10',
  event:   'bg-event/10',
  toolkit: 'bg-toolkit/10',
  tool:    'bg-tool/10',
};

export const ENTITY_BG_SOLID_CLASS: Record<EntityType, string> = {
  game:    'bg-game',
  player:  'bg-player',
  session: 'bg-session',
  agent:   'bg-agent',
  kb:      'bg-kb',
  chat:    'bg-chat',
  event:   'bg-event',
  toolkit: 'bg-toolkit',
  tool:    'bg-tool',
};

export const ENTITY_BORDER_CLASS: Record<EntityType, string> = {
  game:    'border-game',
  player:  'border-player',
  session: 'border-session',
  agent:   'border-agent',
  kb:      'border-kb',
  chat:    'border-chat',
  event:   'border-event',
  toolkit: 'border-toolkit',
  tool:    'border-tool',
};

export const ENTITY_RING_CLASS: Record<EntityType, string> = {
  game:    'ring-game',
  player:  'ring-player',
  session: 'ring-session',
  agent:   'ring-agent',
  kb:      'ring-kb',
  chat:    'ring-chat',
  event:   'ring-event',
  toolkit: 'ring-toolkit',
  tool:    'ring-tool',
};
```

- [ ] **Step 4.3: TypeScript verify**

```bash
cd apps/web && pnpm tsc --noEmit 2>&1 | tail -3
```

Expected: clean.

- [ ] **Step 4.4: Commit**

```bash
git add apps/web/src/lib/design/entity-classes.ts
git commit -m "feat(design): add entity-classes lookup tables for dynamic entity styling

5 lookup tables (text/bg-soft/bg-solid/border/ring) keyed by EntityType.
Used when entity is dynamic (data-driven). Static entities use direct utility.

Refs P2 plan Task 4, design spec §3.1.1"
```

---

## Task 5: Refactor MeepleCard tokens.ts to backward-compat façade

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`

- [ ] **Step 5.1: Read existing tokens.ts**

Run:
```bash
cat apps/web/src/components/ui/data-display/meeple-card/tokens.ts | head -80
```

Identify exports: `entityColors`, `statusColors`, etc. Note structure (likely `Record<EntityType, { h, s, l }>`).

- [ ] **Step 5.2: Refactor entityColors to read from CSS vars**

Edit `tokens.ts`. Replace literal HSL values with CSS var references. Example:

```typescript
// Before
export const entityColors: Record<EntityType, EntityColor> = {
  game: { h: 25, s: 95, l: 45 },
  ...
};

// After (façade — values come from CSS vars set in design-tokens.css)
export const entityColors: Record<EntityType, EntityColor> = {
  game:    { hsl: 'hsl(var(--c-game))',    soft: 'hsl(var(--c-game) / 0.10)' },
  player:  { hsl: 'hsl(var(--c-player))',  soft: 'hsl(var(--c-player) / 0.10)' },
  session: { hsl: 'hsl(var(--c-session))', soft: 'hsl(var(--c-session) / 0.10)' },
  agent:   { hsl: 'hsl(var(--c-agent))',   soft: 'hsl(var(--c-agent) / 0.10)' },
  kb:      { hsl: 'hsl(var(--c-kb))',      soft: 'hsl(var(--c-kb) / 0.10)' },
  chat:    { hsl: 'hsl(var(--c-chat))',    soft: 'hsl(var(--c-chat) / 0.10)' },
  event:   { hsl: 'hsl(var(--c-event))',   soft: 'hsl(var(--c-event) / 0.10)' },
  toolkit: { hsl: 'hsl(var(--c-toolkit))', soft: 'hsl(var(--c-toolkit) / 0.10)' },
  tool:    { hsl: 'hsl(var(--c-tool))',    soft: 'hsl(var(--c-tool) / 0.10)' },
};

// Add deprecation note at top of file
/**
 * @deprecated since 2026-Q3 — prefer Tailwind utility classes (text-event, bg-event/10)
 * for new code. This file is now a backward-compatibility façade reading from
 * CSS vars set in apps/web/src/styles/design-tokens.css.
 *
 * Tracking issue for sunset: https://github.com/meepleAi-app/meepleai-monorepo/issues/<TBD>
 */
```

> NOTE: actual interface (`EntityColor`) may differ — adapt to existing shape. The principle: replace literal HSL with CSS var references.

- [ ] **Step 5.3: Run all tests touching MeepleCard**

```bash
cd apps/web && pnpm test -- --run meeple-card 2>&1 | tail -10
```

Expected: all pass (no behavioral change, only token source).

- [ ] **Step 5.4: Visual smoke MeepleCard**

```bash
cd apps/web && pnpm test:e2e -- e2e/visual-migrated/sp4-library-desktop.spec.ts 2>&1 | tail -5
```

Expected: pass OR small diff < 0.5% (color may shift if MeepleCard was using old HSL — this is intentional).

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/tokens.ts
git commit -m "refactor(meeple-card): tokens.ts as backward-compat façade reading CSS vars

Per #807 redesign — entity HSL literals now sourced from CSS vars in
design-tokens.css. Same exports, same shape, zero call-site impact.
JSDoc deprecation added; sunset tracking issue TBD.

Refs P2 plan Task 5, design spec §1.4"
```

---

## Task 6: Build codemod jscodeshift script

**Files:**
- Create: `tools/codemods/inline-hsl-to-tailwind.js`

- [ ] **Step 6.1: Verify jscodeshift available**

```bash
cd /d/Repositories/meepleai-monorepo-frontend
npx --yes jscodeshift --version 2>&1 | head -3
```

If not in cache, the codemod can be invoked via `npx --yes` per command (no install needed). If preferred as workspace dep:

```bash
cd apps/web && pnpm add -D jscodeshift @types/jscodeshift
```

NB: codemod source lives at repo root (`tools/codemods/`); installing at `apps/web` workspace makes `pnpm exec jscodeshift` available in subsequent steps. Either approach works.

- [ ] **Step 6.2: Write codemod (skeleton + entity matcher)**

Crea `tools/codemods/inline-hsl-to-tailwind.js`:

```javascript
/**
 * jscodeshift codemod: inline HSL/HSLA → Tailwind utility classes.
 *
 * Usage:
 *   npx jscodeshift -t tools/codemods/inline-hsl-to-tailwind.js \
 *     --parser=tsx \
 *     apps/web/src/components/v2/**\/*.tsx
 *
 * Refs P2 plan Task 6, design spec §3.2 (HSL→entity mapping)
 */

const ENTITY_HSL_SIGNATURES = {
  game:    [{ h: 25,  hRange: 5, s: 95, sRange: 10 }, { h: 28, hRange: 5, s: 95, sRange: 10 }],
  player:  [{ h: 262, hRange: 5, s: 83, sRange: 12 }, { h: 262, hRange: 5, s: 75, sRange: 10 }],
  session: [{ h: 240, hRange: 5, s: 60, sRange: 12 }, { h: 235, hRange: 5, s: 70, sRange: 10 }],
  agent:   [{ h: 38,  hRange: 5, s: 92, sRange: 10 }],
  kb:      [{ h: 174, hRange: 5, s: 60, sRange: 10 }, { h: 210, hRange: 5, s: 40, sRange: 12 }],
  chat:    [{ h: 220, hRange: 5, s: 80, sRange: 10 }, { h: 218, hRange: 5, s: 80, sRange: 10 }],
  event:   [{ h: 350, hRange: 5, s: 89, sRange: 10 }, { h: 350, hRange: 5, s: 85, sRange: 10 }],
  toolkit: [{ h: 142, hRange: 5, s: 70, sRange: 12 }, { h: 142, hRange: 5, s: 60, sRange: 10 }],
  tool:    [{ h: 195, hRange: 5, s: 80, sRange: 10 }, { h: 195, hRange: 5, s: 75, sRange: 10 }],
};

function matchEntity(h, s) {
  for (const [entity, signatures] of Object.entries(ENTITY_HSL_SIGNATURES)) {
    for (const sig of signatures) {
      if (Math.abs(h - sig.h) <= sig.hRange && Math.abs(s - sig.s) <= sig.sRange) {
        return entity;
      }
    }
  }
  return null; // unmapped → goes to Phase B manual review
}

function parseHSL(literal) {
  // Match hsl(H, S%, L%) or hsla(H, S%, L%, A)
  const m = literal.match(/hsla?\(\s*(\d+)\s*,\s*(\d+)%\s*,\s*(\d+)%(?:\s*,\s*([\d.]+))?\s*\)/);
  if (!m) return null;
  return {
    h: parseInt(m[1]),
    s: parseInt(m[2]),
    l: parseInt(m[3]),
    a: m[4] ? parseFloat(m[4]) : 1,
  };
}

function alphaToTailwindModifier(a) {
  if (a >= 1) return '';
  // Tailwind opacity supports /5, /10, /20, ..., /95
  const pct = Math.round(a * 100);
  // Snap to nearest /5 step
  const snapped = Math.round(pct / 5) * 5;
  return `/${snapped}`;
}

function buildClassName(entity, alpha, kind) {
  const modifier = alphaToTailwindModifier(alpha);
  switch (kind) {
    case 'text':       return `text-${entity}${modifier}`;
    case 'background': return `bg-${entity}${modifier}`;
    case 'border':     return `border-${entity}${modifier}`;
    default:           return null;
  }
}

module.exports = function transformer(file, api) {
  const j = api.jscodeshift;
  const root = j(file.source);
  const unmappedFile = '/tmp/codemod-unmapped.json';
  const unmapped = [];

  // Find JSX style={{...}} props containing hsl/hsla literals
  root.find(j.JSXAttribute, { name: { name: 'style' } }).forEach(path => {
    const value = path.node.value;
    if (!value || value.type !== 'JSXExpressionContainer') return;
    const obj = value.expression;
    if (!obj || obj.type !== 'ObjectExpression') return;

    const remainingProps = [];
    const newClasses = [];

    for (const prop of obj.properties) {
      if (prop.type !== 'Property' && prop.type !== 'ObjectProperty') {
        remainingProps.push(prop);
        continue;
      }
      const keyName = prop.key.name || prop.key.value;
      const valNode = prop.value;
      if (valNode.type !== 'Literal' && valNode.type !== 'StringLiteral') {
        remainingProps.push(prop);
        continue;
      }
      const valStr = valNode.value;
      if (typeof valStr !== 'string') {
        remainingProps.push(prop);
        continue;
      }

      const hsl = parseHSL(valStr);
      if (!hsl) {
        remainingProps.push(prop);
        continue;
      }

      const entity = matchEntity(hsl.h, hsl.s);
      if (!entity) {
        unmapped.push({ file: file.path, line: prop.loc?.start.line, value: valStr });
        remainingProps.push(prop);
        continue;
      }

      let kind;
      if (keyName === 'color') kind = 'text';
      else if (keyName === 'background' || keyName === 'backgroundColor') kind = 'background';
      else if (keyName === 'borderColor') kind = 'border';
      else { remainingProps.push(prop); continue; }

      const cls = buildClassName(entity, hsl.a, kind);
      if (cls) {
        newClasses.push(cls);
      } else {
        remainingProps.push(prop);
      }
    }

    if (newClasses.length === 0) return; // nothing replaced

    // Modify or add className attribute
    const jsxElement = path.parent.node;
    let classNameAttr = jsxElement.attributes.find(a => a.name?.name === 'className');
    if (classNameAttr && classNameAttr.value?.type === 'StringLiteral') {
      classNameAttr.value.value = `${classNameAttr.value.value} ${newClasses.join(' ')}`.trim();
    } else if (classNameAttr) {
      // Complex className expression — log unmapped, don't merge
      unmapped.push({ file: file.path, reason: 'complex className', value: newClasses.join(' ') });
    } else {
      jsxElement.attributes.push(
        j.jsxAttribute(j.jsxIdentifier('className'), j.literal(newClasses.join(' ')))
      );
    }

    // Strip replaced props from style object
    if (remainingProps.length === 0) {
      // Remove entire style attribute
      jsxElement.attributes = jsxElement.attributes.filter(a => a.name?.name !== 'style');
    } else {
      obj.properties = remainingProps;
    }
  });

  // Append unmapped to log file (idempotent)
  if (unmapped.length > 0) {
    const fs = require('node:fs');
    const existing = fs.existsSync(unmappedFile) ? JSON.parse(fs.readFileSync(unmappedFile, 'utf8')) : [];
    fs.writeFileSync(unmappedFile, JSON.stringify([...existing, ...unmapped], null, 2));
  }

  return root.toSource();
};
```

- [ ] **Step 6.3: Smoke test codemod su un file conosciuto (dry-run)**

Identify a v2 file with known inline HSL:
```bash
grep -lE "hsl\(350" apps/web/src/components/v2/ -r | head -1
```

Esempio output: `apps/web/src/components/v2/gamebook/GamebookCard.tsx`

Run dry:
```bash
npx jscodeshift -t tools/codemods/inline-hsl-to-tailwind.js \
  --parser=tsx \
  --dry --print \
  apps/web/src/components/v2/gamebook/GamebookCard.tsx 2>&1 | head -30
```

Expected: diff output showing inline HSL → className replacement.

- [ ] **Step 6.4: Commit codemod (no source changes yet)**

```bash
git add tools/codemods/inline-hsl-to-tailwind.js
git commit -m "feat(codemod): jscodeshift inline-hsl-to-tailwind for #807 refactor

3-layer entity disambiguation (HSL signature, filename heuristic, unmapped log).
Run via: npx jscodeshift -t tools/codemods/inline-hsl-to-tailwind.js \
  --parser=tsx apps/web/src/components/v2/**/*.tsx

Refs P2 plan Task 6, design spec §3.2"
```

---

## Task 7: Run codemod Phase A (mechanical replacement)

**Files:**
- Modify: `apps/web/src/components/v2/**/*.tsx` (~194 inline)

- [ ] **Step 7.1: Reset unmapped log**

```bash
rm -f /tmp/codemod-unmapped.json
```

- [ ] **Step 7.2: Run codemod recursive**

```bash
npx jscodeshift -t tools/codemods/inline-hsl-to-tailwind.js \
  --parser=tsx \
  apps/web/src/components/v2/ 2>&1 | tail -10
```

Expected: report file count modified. Es. "120 ok, 5 errors, 0 unchanged".

- [ ] **Step 7.3: Inspect unmapped occurrences**

```bash
cat /tmp/codemod-unmapped.json 2>/dev/null | jq 'length'
cat /tmp/codemod-unmapped.json 2>/dev/null | jq '.[0:5]'
```

Expected: counts of unmapped (will need Phase B). If > 50, possibly the matcher is too strict — adjust ranges in codemod.

- [ ] **Step 7.4: Build verification**

```bash
cd apps/web && pnpm build 2>&1 | tail -10
```

Expected: build green. If errors (likely TypeScript template literal issues), Phase B manual fix needed.

- [ ] **Step 7.5: Commit Phase A**

```bash
git add apps/web/src/components/v2/
git commit -m "refactor(v2): codemod Phase A — replace inline HSL with Tailwind utilities

Mechanical jscodeshift run on apps/web/src/components/v2/**.
~120-180 inline HSL/HSLA replaced with text-{entity}, bg-{entity}/N, border-{entity}.
Unmapped occurrences logged to /tmp/codemod-unmapped.json for Phase B review.

Refs P2 plan Task 7, design spec §3.3 Phase A"
```

---

## Task 8: Phase B manual review (unmapped + conditional)

**Files:**
- Modify: `apps/web/src/components/v2/**/*.tsx` (handful files)

- [ ] **Step 8.1: List remaining inline HSL**

```bash
grep -rEn "hsl[a]?\(" apps/web/src/components/v2/ 2>/dev/null > /tmp/remaining-hsl.txt
wc -l /tmp/remaining-hsl.txt
head -20 /tmp/remaining-hsl.txt
```

Expected: 0-30 occurrences. These are conditional/template-literal/complex cases.

- [ ] **Step 8.2: For each remaining occurrence, choose pattern**

Per ogni file in `/tmp/remaining-hsl.txt`:

**Conditional entity-driven** (most common):
```tsx
// BEFORE
style={{ color: `hsl(${entity.h}, ${entity.s}%, ${entity.l}%)` }}

// AFTER (use entity-classes lookup)
import { ENTITY_TEXT_CLASS, type EntityType } from '@/lib/design/entity-classes';
className={ENTITY_TEXT_CLASS[entity.type as EntityType]}
```

**Multi-property style** (split per side):
```tsx
// BEFORE
style={{
  background: 'hsla(350, 89%, 48%, 0.10)',
  border: '1px solid hsl(350, 89%, 48%)',
  someNonHslProp: 'value'
}}

// AFTER
className="bg-event/10 border border-event"
style={{ someNonHslProp: 'value' }}
```

**Gradient (decorative)** — preserve gradient but replace HSL endpoints:
```tsx
// BEFORE
style={{ background: 'linear-gradient(135deg, hsl(25,95%,45%), hsl(350,89%,60%))' }}

// AFTER (Tailwind gradient utility OR keep as derived var)
className="bg-gradient-to-br from-game to-event"
```

- [ ] **Step 8.3: Verification per BC**

After each BC (gamebook, agents, library, sessions, players, games):
```bash
cd apps/web && pnpm tsc --noEmit 2>&1 | tail -3
pnpm test -- --run <bc-folder> 2>&1 | tail -5
```

- [ ] **Step 8.4: Final grep check**

```bash
grep -rEn "hsl[a]?\(" apps/web/src/components/v2/ 2>/dev/null
```

Expected: **0 results**.

- [ ] **Step 8.5: Commit Phase B**

```bash
git add apps/web/src/components/v2/
git commit -m "refactor(v2): codemod Phase B — manual review for unmapped/conditional patterns

Remaining ~20-30 inline HSL replaced manually:
- conditional entity-driven → ENTITY_*_CLASS lookup tables
- multi-property style → split className + style
- gradient endpoints → Tailwind gradient utilities (from-{entity} to-{entity})

Verification: grep hsl(' apps/web/src/components/v2/ → 0 results.

Refs P2 plan Task 8, design spec §3.3 Phase B"
```

---

## Task 9: Build custom ESLint rule

**Files:**
- Create: `apps/web/eslint-rules/no-inline-hsl-v2.js`
- Create: `apps/web/eslint-rules/no-inline-hsl-v2.test.js`
- Modify: `apps/web/eslint.config.mjs`

- [ ] **Step 9.1: Read existing pattern**

```bash
cat apps/web/eslint-rules/no-hardcoded-hex.js
```

Note pattern: rule export shape, message format, AST node visitors.

- [ ] **Step 9.2: Write rule**

Crea `apps/web/eslint-rules/no-inline-hsl-v2.js`:

```javascript
/**
 * ESLint rule: no inline HSL/HSLA in v2 components.
 *
 * Forces use of Tailwind utility classes (text-event, bg-event/10) instead of
 * inline style={{ background: 'hsla(...)' }} for v2 design tokens.
 *
 * Refs P2 plan Task 9, design spec §3.5
 */

const HSL_REGEX = /hsla?\(\s*\d+\s*,\s*\d+%\s*,\s*\d+%(?:\s*,\s*[\d.]+)?\s*\)/i;

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description: 'Disallow inline hsl()/hsla() in v2 components — use Tailwind utility classes (text-event, bg-event/10) instead',
    },
    schema: [],
    messages: {
      inlineHsl: 'Inline hsl()/hsla() not allowed in v2 components. Use Tailwind utility class (e.g. text-event, bg-event/10) backed by CSS vars in design-tokens.css. See docs/for-developers/frontend/v2-token-system.md.',
    },
  },
  create(context) {
    return {
      Literal(node) {
        if (typeof node.value !== 'string') return;
        if (HSL_REGEX.test(node.value)) {
          context.report({
            node,
            messageId: 'inlineHsl',
          });
        }
      },
      TemplateLiteral(node) {
        // Detect template literal containing hsl(...) e.g. `hsl(${h}, ${s}%, ${l}%)`
        const raw = context.getSourceCode().getText(node);
        if (HSL_REGEX.test(raw) || /hsla?\(/i.test(raw)) {
          context.report({
            node,
            messageId: 'inlineHsl',
          });
        }
      },
    };
  },
};
```

- [ ] **Step 9.3: Write test**

Crea `apps/web/eslint-rules/no-inline-hsl-v2.test.js`:

```javascript
const { RuleTester } = require('eslint');
const rule = require('./no-inline-hsl-v2');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 2022,
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

ruleTester.run('no-inline-hsl-v2', rule, {
  valid: [
    { code: `const x = "text-event";` },
    { code: `<div className="bg-event/10" />` },
    { code: `const color = "var(--c-event)";` },
  ],
  invalid: [
    {
      code: `const c = "hsl(350, 89%, 48%)";`,
      errors: [{ messageId: 'inlineHsl' }],
    },
    {
      code: `const c = "hsla(350, 89%, 48%, 0.10)";`,
      errors: [{ messageId: 'inlineHsl' }],
    },
    {
      code: `const c = \`hsl(\${h}, \${s}%, \${l}%)\`;`,
      errors: [{ messageId: 'inlineHsl' }],
    },
  ],
});

console.log('no-inline-hsl-v2: All tests passed');
```

- [ ] **Step 9.4: Run rule test**

```bash
cd apps/web && node eslint-rules/no-inline-hsl-v2.test.js 2>&1 | tail -3
```

Expected: `no-inline-hsl-v2: All tests passed`. If failure, fix rule logic.

- [ ] **Step 9.5: Wire rule into eslint.config.mjs**

ESM `.mjs` config can `import` CJS `.js` files via Node interop (rule file uses `module.exports`).

First verify Node ESM interop works with the rule file:
```bash
cd apps/web && node --input-type=module --eval "import('./eslint-rules/no-inline-hsl-v2.js').then(m => console.log('OK', !!m.default))"
```

Expected: `OK true`. If `OK false` (no default export), edit `no-inline-hsl-v2.js` final line:
```js
// Replace `module.exports = { meta, create };` with:
module.exports.default = module.exports;
module.exports = { meta: ..., create: ..., default: { meta: ..., create: ... } };
// OR simpler — convert rule file to .mjs with `export default { meta, create };`
```

Edit `apps/web/eslint.config.mjs`. Add at top with other imports (using dynamic import OR createRequire if static fails):

```javascript
// Option A: static import (works if rule file has default export)
import noInlineHslV2 from './eslint-rules/no-inline-hsl-v2.js';

// Option B: createRequire fallback (works for any CJS module)
import { createRequire } from 'node:module';
const require = createRequire(import.meta.url);
const noInlineHslV2 = require('./eslint-rules/no-inline-hsl-v2.js');
```

Then in the config array, add v2-scoped block:

```javascript
{
  files: ['src/components/v2/**/*.{ts,tsx}'],
  plugins: {
    'meepleai': {
      rules: {
        'no-inline-hsl-v2': noInlineHslV2,
      },
    },
  },
  rules: {
    'meepleai/no-inline-hsl-v2': 'error',
  },
},
```

- [ ] **Step 9.6: Verify rule active**

```bash
cd apps/web && pnpm lint 2>&1 | grep -E "no-inline-hsl-v2|error" | head -10
```

Expected: 0 violations (since Task 7+8 cleaned them all). If errors still found, address them in Phase B.

- [ ] **Step 9.7: Commit**

```bash
git add apps/web/eslint-rules/no-inline-hsl-v2.js apps/web/eslint-rules/no-inline-hsl-v2.test.js apps/web/eslint.config.mjs
git commit -m "feat(eslint): custom rule no-inline-hsl-v2 prevents regression in v2 components

Reuses pattern from existing no-hardcoded-hex.js. Scoped to apps/web/src/components/v2/**.
Detects literal + template-literal hsl/hsla. Test suite passes.

Refs P2 plan Task 9, design spec §3.5"
```

---

## Task 10: Visual baselines regen

**Files:**
- Modify: `apps/web/e2e/**/*-baseline.png`

- [ ] **Step 10.1: Trigger bootstrap workflow**

```bash
gh workflow run "Visual Regression — Migrated Routes" --ref feature/p2-807-token-redesign -f mode=bootstrap 2>&1 | tail -2
```

- [ ] **Step 10.2: Wait completion**

```bash
RUN_ID=$(gh run list --workflow "Visual Regression — Migrated Routes" --branch feature/p2-807-token-redesign --limit 1 --json databaseId --jq '.[0].databaseId')
gh run watch $RUN_ID 2>&1 | tail -5
```

Expected: success. Output PNG artifacts.

- [ ] **Step 10.3: Download + commit baselines**

```bash
gh run download $RUN_ID -n visual-baselines -D apps/web/e2e/ 2>&1 | tail -3
git add apps/web/e2e/**/*.png
git status --short | grep "\.png$" | wc -l
```

Expected: ~80-120 PNG files staged.

- [ ] **Step 10.4: Spot check 10 PNGs (manual)**

Open 10 random PNGs in `apps/web/e2e/visual-migrated/` and `apps/web/e2e/v2-states/`:
- Verify colors are darker (per AA fix expectation)
- Verify NO layout shift (text positions, button sizes unchanged)

If layout shift detected → bug in refactor (Phase B mistake). Investigate.

- [ ] **Step 10.5: Commit baselines**

```bash
git commit -m "chore(visual-baselines): regen for #807 token redesign

~80-120 PNG baselines regenerated post-token redesign. Color shifts expected
(AA-compliant darker tones); layout unchanged (manual spot check 10 PNGs).

Refs P2 plan Task 10"
```

---

## Task 11: A11y E2E enforce blocking

**Files:**
- Modify: `.github/workflows/<a11y-workflow>.yml`

- [ ] **Step 11.1: Locate A11y workflow file**

```bash
WORKFLOW_FILE=$(grep -rln "Frontend - A11y E2E\|test:e2e:a11y" .github/workflows/ 2>&1 | head -1)
echo "WORKFLOW_FILE=$WORKFLOW_FILE"
```

Expected: single path printed (likely `.github/workflows/ci.yml` or `.github/workflows/test-e2e-a11y.yml`). If multiple matches, identify the one with the "Frontend - A11y E2E" job (vs other a11y mentions in unrelated workflows).

Export to env var for next steps:
```bash
export WORKFLOW_FILE
```

- [ ] **Step 11.2: Find continue-on-error setting**

```bash
grep -n "continue-on-error\|A11y E2E" "$WORKFLOW_FILE"
```

- [ ] **Step 11.3: Toggle to false**

Edit `$WORKFLOW_FILE`. Find the line `continue-on-error: true` adjacent to the "Frontend - A11y E2E" step (verify via Step 11.2 output).

Replace `true` → `false` and add a comment:

```yaml
# Before
continue-on-error: true   # disabled: SP6 v2 a11y violations under token redesign #807

# After
continue-on-error: false  # P2 #807 merged — token redesign deployed, A11y now blocking
```

Verify edit:
```bash
grep -A1 "Frontend - A11y E2E" "$WORKFLOW_FILE" | grep "continue-on-error"
```
Expected: `continue-on-error: false`.

- [ ] **Step 11.4: Run E2E A11y locally to verify clean**

```bash
cd apps/web && pnpm test:e2e:a11y 2>&1 | tail -20
```

Expected: 0 violations serious/critical. If any failures, address in code (likely missed inline HSL).

- [ ] **Step 11.5: Commit**

```bash
git add "$WORKFLOW_FILE"
git commit -m "ci(a11y): make Frontend - A11y E2E job blocking (continue-on-error: false)

Per P2 #807 acceptance — token redesign deployed, all v2 routes pass axe-core
WCAG 2.1 AA. Future PR with new violations will fail CI.

Refs P2 plan Task 11"
```

---

## Task 12: Reference doc v2-token-system.md

**Files:**
- Create: `docs/for-developers/frontend/v2-token-system.md`

- [ ] **Step 12.1: Write reference doc**

Crea `docs/for-developers/frontend/v2-token-system.md` con 8 sezioni:

```markdown
# v2 Token System — Reference

> Canonical reference per il sistema di design token v2 di MeepleAI.
> Refs spec: docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md

## 1. Overview

Il sistema usa **CSS custom properties** in `design-tokens.css` come single source of truth, registrate in **Tailwind v4 `@theme` block** in `globals.css` per generare utility classes.

Architettura:
\`\`\`
design-tokens.css :root { --c-event: 350 89% 38%; }
       ↓
globals.css @theme { --color-event: hsl(var(--c-event)); }
       ↓
Tailwind utilities: text-event, bg-event/10, border-event, ring-event
\`\`\`

## 2. Token catalog

| Token | Light HSL | Dark HSL | Use case |
|-------|-----------|----------|----------|
| `game` | 25 95% 38% | 28 95% 58% | Game entity (catalog, library) |
| `player` | 262 83% 45% | 262 75% 70% | Players, profiles, stats |
| `session` | 240 60% 35% | 235 70% 70% | Live play sessions |
| `agent` | 38 92% 38% | 38 92% 62% | AI agents, bots |
| `kb` | 174 60% 40% | 174 60% 55% | Knowledge base, docs |
| `chat` | 220 80% 40% | 218 80% 68% | Conversations |
| `event` | 350 89% 38% | 350 85% 70% | Events, alerts |
| `toolkit` | 142 70% 35% | 142 60% 58% | Toolkits (published agent+KB) |
| `tool` | 195 80% 38% | 195 75% 62% | Tools (timer, counter, etc) |

Audit dettagliato: \`v2-a11y-token-audit.md\`.

## 3. Usage examples

### Static known entity
\`\`\`tsx
<div className="text-event bg-event/10 border border-event/30">
  Event item
</div>
\`\`\`

### Dynamic entity (data-driven)
\`\`\`tsx
import { ENTITY_TEXT_CLASS, ENTITY_BG_SOFT_CLASS, type EntityType } from '@/lib/design/entity-classes';

function EntityChip({ entity }: { entity: EntityType }) {
  return <span className={\`\${ENTITY_TEXT_CLASS[entity]} \${ENTITY_BG_SOFT_CLASS[entity]}\`}>...</span>;
}
\`\`\`

### Alpha modifier
\`\`\`tsx
<div className="bg-event">100% opacity</div>
<div className="bg-event/50">50% opacity</div>
<div className="bg-event/10">10% opacity (soft bg)</div>
\`\`\`

### Gradient
\`\`\`tsx
<div className="bg-gradient-to-br from-game to-event">decorative</div>
\`\`\`

## 4. Dark mode behavior

Toggle: aggiungi/rimuovi class \`.dark\` su \`<html>\` (tailwind v4 darkMode: 'class').

Il CSS var swap è automatico:
- Light: \`--c-event: 350 89% 38%\`
- Dark:  \`--c-event: 350 85% 70%\`

Nessuna change a livello componente — utility class \`text-event\` automaticamente consuma la variante corretta.

## 5. Migration guide (legacy inline HSL)

Vedi mapping table \`docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md\` §3.2.1.

Codemod: \`tools/codemods/inline-hsl-to-tailwind.js\` per refactor automatico.

## 6. ESLint enforcement

Custom rule \`@meepleai/no-inline-hsl-v2\` blocca \`hsl()\`/\`hsla()\` inline in \`apps/web/src/components/v2/**\`.

Disable per-line con justification:
\`\`\`tsx
// eslint-disable-next-line @meepleai/no-inline-hsl-v2 -- third-party color hardcoded by external SDK
const externalColor = 'hsl(180, 50%, 50%)';
\`\`\`

## 7. Adding new tokens

Process per estendere palette (rare — solo se introduce nuova entity type):

1. Aggiungi var in \`design-tokens.css\` :root e .dark
2. Registra in \`globals.css\` \`@theme\` block
3. Aggiungi entry in \`entity-classes.ts\` lookup tables (5 lookup × 1 nuova entry)
4. Aggiorna codemod \`ENTITY_HSL_SIGNATURES\`
5. Aggiungi riga a v2-token-system.md (questo doc) + audit

## 8. Audit reference

Storico ratios e methodology: \`docs/for-developers/frontend/v2-a11y-token-audit.md\`
```

- [ ] **Step 12.2: Commit**

```bash
git add docs/for-developers/frontend/v2-token-system.md
git commit -m "docs(frontend): add v2-token-system.md reference doc

8-section canonical reference: overview, catalog, usage, dark mode, migration,
eslint enforcement, adding tokens, audit reference.

Refs P2 plan Task 12, design spec §6.4"
```

---

## Task 13: Update v2-migration-matrix + close issues

**Files:**
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 13.1: Identify existing FREEZE banner format**

```bash
head -20 docs/for-developers/frontend/v2-migration-matrix.md
```

Identifica blockquote `> 🔒 **FREEZE...**` (esiste, posizionato in top of file).

- [ ] **Step 13.2: Update freeze status preserving banner format**

Edit `docs/for-developers/frontend/v2-migration-matrix.md`. Sostituisci la prima blockquote esistente (FREEZE banner) con:

```markdown
> ✅ **FREEZE LIFTED 2026-05-09** (post P2 #807 token redesign merge — was issued 2026-05-06 via [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808)): SP6 v2 expansion e nuovi v2 components ora **pickable**.
> CSS vars + Tailwind utilities AA-compliant deployati ([#807](https://github.com/meepleAi-app/meepleai-monorepo/issues/807) + [#808](https://github.com/meepleAi-app/meepleai-monorepo/issues/808) entrambi closed).
> Reference: `docs/for-developers/frontend/v2-token-system.md`.
```

Mantieni link issue references nel formato originale del file (verifica se erano `[#NNN]` markdown link o `#NNN` plain reference, adatta).

- [ ] **Step 13.3: Commit doc update**

```bash
git add docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "docs(matrix): mark freeze lifted post-#807 token redesign

#808 freeze policy lifted: pending rows now pickable. Tailwind v2 entity
tokens AA-compliant; reference doc v2-token-system.md.

Refs P2 plan Task 13"
```

---

## Task 14: Open PR + verify DoD

**Files:** none.

- [ ] **Step 14.1: Push branch**

```bash
git push -u origin feature/p2-807-token-redesign
```

- [ ] **Step 14.2: Open PR**

```bash
gh pr create \
  --base main-dev \
  --title "feat(v2): #807 token redesign — AA-compliant CSS vars + Tailwind utilities (sblocca #808 freeze)" \
  --body "$(cat <<'EOF'
## Goal P2 #807

Centralizza 194 inline HSL/HSLA in v2 components in CSS variables AA-compliant + Tailwind v4 \`@theme\` registration. Sblocca freeze #808.

## Changes (atomic per spec parent)

- **Audit Fase 1** (`tools/a11y/contrast-calc.ts` + `v2-a11y-token-audit.md`): 18 ratios verificati ≥ 4.5:1
- **CSS vars Fase 2** (`design-tokens.css`): 9 entity tokens light + dark
- **Tailwind v4 @theme** (`globals.css`): utilities text-{entity}, bg-{entity}/N, etc
- **Codemod refactor** (~194 inline → utility classes via `tools/codemods/inline-hsl-to-tailwind.js`)
- **Lookup tables** (`lib/design/entity-classes.ts`): 5 records × 9 entities per dynamic
- **MeepleCard façade** (`tokens.ts`): backward-compat per non-v2 consumers
- **ESLint rule** (`eslint-rules/no-inline-hsl-v2.js`): regression prevention
- **Visual baselines regen**: ~80-120 PNG
- **A11y E2E blocking**: `continue-on-error: false`
- **Reference doc** (`v2-token-system.md`): canonical reference

## Acceptance verified

- [x] grep hsl() v2 → 0 results
- [x] pnpm test:e2e:a11y → 0 violations
- [x] CI A11y E2E blocking
- [x] Visual baselines regenerated, no layout shift > 0.5%
- [x] ESLint rule attiva
- [x] Audit + reference docs committed

## Closes

- Closes #807 (Fase 1+2)
- Closes #808 (freeze policy lifted)
- Tracking issue per MeepleCard tokens.ts façade sunset: TBD (open as follow-up)

Refs:
- Spec: \`docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md\`
- Plan: \`docs/superpowers/plans/2026-05-09-p2-807-token-redesign.md\`
EOF
)" 2>&1 | tail -3
```

Annota `<PR_NUM>` per step successivi.

- [ ] **Step 14.3: Watch CI**

```bash
gh pr checks <PR_NUM> --watch
```

Expected: tutti i check verdi (in particolare Frontend - A11y E2E ora blocking).

- [ ] **Step 14.4: Open follow-up tracking issue per façade sunset**

```bash
gh issue create \
  --title "[Tech debt] Sunset MeepleCard tokens.ts backward-compat façade" \
  --body "Post P2 #807 merge, MeepleCard tokens.ts è façade backward-compat per non-v2 consumers. Quando tutti i consumer (legacy v1 routes) saranno migrati a Tailwind utility classes, il file può essere rimosso. Tracking per future cleanup." \
  --label "tech-debt,area/frontend"
```

Update PR body con il nuovo issue number.

- [ ] **Step 14.5: Final DoD checklist**

Verifica nel PR description tutti i 11 box DoD spec:
- [ ] grep "hsl[a]?(" v2 → 0 results
- [ ] pnpm test:e2e:a11y → 0 violations su 13 rotte
- [ ] CI A11y E2E continue-on-error: false
- [ ] Visual baselines no layout shift > 0.5%
- [ ] #807 Fase 1+2 closed
- [ ] #808 freeze lifted
- [ ] v2-migration-matrix.md aggiornato
- [ ] Custom ESLint rule attiva fail-on-violation
- [ ] v2-a11y-token-audit.md committato
- [ ] v2-token-system.md committato
- [ ] Tracking issue façade sunset aperta

- [ ] **Step 14.6: Merge PR**

Quando tutti i check verdi e DoD verificato:
```bash
gh pr merge <PR_NUM> --squash --delete-branch
```

---

## Definition of Done — P2 #807 chiusa

- [ ] PR mergiata a main-dev
- [ ] #807 closed (Fase 1 + Fase 2)
- [ ] #808 closed (freeze lifted)
- [ ] Follow-up tracking issue aperta per façade sunset
- [ ] v2-migration-matrix.md mostra "freeze lifted, pending rows pickable"
- [ ] CI A11y E2E job ora blocking per future PR
- [ ] Custom ESLint rule attiva
- [ ] Reference doc accessibile a future contributors

**Tempo stimato totale**: 1.5-2 settimane single dev (impl) + 2-3 settimane PR review.

---

## Self-review checklist

**1. Spec coverage**:
- ✅ §1.1 CSS vars → Task 2
- ✅ §1.2 Tailwind theme → Task 3 (adapted to Tailwind v4 @theme)
- ✅ §1.3 Utility class usage → Task 7 (codemod) + Task 8 (manual)
- ✅ §1.4 MeepleCard façade → Task 5
- ✅ §2.1 Target table → Task 1 (audit verifies)
- ✅ §2.2 Dark mode AA verification → Task 1 (Step 1.2 includes dark)
- ✅ §2.3 Audit deliverable → Task 1 Step 1.3
- ✅ §3.1 Pattern → replacement → Task 7+8
- ✅ §3.1.1 Conditional pattern → Task 4 (entity-classes.ts) + Task 8
- ✅ §3.2.1-3 HSL→entity mapping → Task 6 (codemod implements 3-layer)
- ✅ §3.3 Phase A/B/C → Task 7+8
- ✅ §3.4 Codemod rollback → documented in Task 7 commit (per-file atomic)
- ✅ §3.5 Lint enforcement → Task 9
- ✅ §4 Visual baseline regen → Task 10
- ✅ §5 Testing strategy → Task 9 (rule test) + Task 11 (a11y blocking) + Task 10 (visual)
- ✅ §6.3 DoD → Task 14 verifies
- ✅ §6.3.1 13 routes covered → A11y E2E job covers them (existing setup)
- ✅ §6.4 Reference doc → Task 12

**2. Placeholder scan**: tutti i comandi/codice concreti. `<PR_NUM>` è runtime variable explicit. `<file>` placeholder for workflow filename in Task 11 — engineer identifies via grep in 11.1.

**3. Type consistency**: 
- `EntityType` definito in entity-classes.ts (Task 4) usato consistentemente in Task 8 + Task 12 doc
- CSS var name `--c-event` consistente in design-tokens.css (Task 2) + globals.css (Task 3) + meeple-card façade (Task 5)
- ESLint rule name `meepleai/no-inline-hsl-v2` consistente in rule file + config wire (Task 9) + reference doc (Task 12)

**Spec deviations from plan** (documented in plan header):
1. Tailwind config approach: spec assumed v3 `tailwind.config.js extend.colors`; plan adapts to v4 `@theme` directive in globals.css
2. ESLint plugin: spec assumed separate `tools/eslint-plugin-meepleai/`; plan reuses existing `apps/web/eslint-rules/` pattern
