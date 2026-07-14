# Migrate MeepleCard Entity Accents to Canonical CSS Vars (Issue #2862 / C5) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make MeepleCard's entity accents theme-aware and single-source by rewriting `entityHsl`/`entityHslText` (tokens.ts) to emit `hsl(var(--c-*))` instead of hardcoded light-only JS HSL — fixing dark-mode divergence across ~19 call-sites with zero call-site edits — plus adding the 3 missing `--c-*-text` canonical vars and making the EntityBadge pill theme-aware so its (now theme-aware) text stays AA.

**Architecture:** Rewrite the two color helpers to reference canonical `--c-*` / `--c-*-text` CSS variables (theme-switched at runtime). `entityTokens` (built on `entityHsl`) and all consumers inherit theme-awareness. `entityColors` (raw JS triplet) is kept for its external consumers. EntityBadge's `bg-white/85` pill → `bg-card/85` (theme-aware) so `--c-*-text` is legible in both themes.

**Tech Stack:** TypeScript · CSS custom properties · Tailwind (semantic + entity utilities) · Vitest.

**Spec:** `docs/superpowers/specs/2026-07-13-issue-2862-migrate-entity-tokens-design.md`

## Global Constraints

- Work on branch `feature/issue-2862-migrate-entity-tokens` (already created from `main-dev`); PR targets `main-dev`.
- Frontend paths under `apps/web/`. Run from `apps/web/`. `pnpm exec vitest run <path>`, `pnpm typecheck`, `pnpm lint`, `pnpm build`.
- `entityHsl(entity, alpha?)` → `hsl(var(--c-<name>))` or `hsl(var(--c-<name>) / <alpha>)`; `entityHslText(entity, alpha?)` → `hsl(var(--c-<name>-text))` (or `--c-<name>` when no text var). `name` = `CSS_ENTITY[entity]` (`gameNightEvent` → `event`; all others identity).
- Entities WITH a `--c-*-text` var: game, kb, toolkit, session, event, agent, chat. WITHOUT (fall back to solid): player, tool.
- New canonical text vars: `--c-event-text` (light `350 89% 32%` / dark `350 85% 78%`), `--c-agent-text` (light `38 92% 24%` / dark `38 92% 72%`), `--c-chat-text` (light `220 80% 38%` / dark `218 85% 80%`). All must be ≥ 4.5:1 (light on `#ffffff`, dark on `#1e1710`).
- Keep `entityColors` (external consumers + #636 regression). Keep `--mc-*` surface + `statusColors`. a11y gate is BLOCKING; no hardcoded color utilities (`bg-card/85` is semantic — allowed).
- Commit `type(scope): description` + `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`.

---

### Task 1: Add the 3 missing `--c-*-text` canonical vars + AA test

**Files:**
- Modify: `apps/web/src/styles/design-tokens-canonical.css` (light `:root` after line 48; dark `[data-theme="dark"]` after line 240)
- Test: `apps/web/src/__tests__/styles/entity-text-tokens-c5.test.ts` (create)

**Interfaces:**
- Produces: `--c-event-text`, `--c-agent-text`, `--c-chat-text` (light + dark) in canonical CSS. Consumed by Task 2's `entityHslText`.

- [ ] **Step 1: Write the failing AA test**

Create `apps/web/src/__tests__/styles/entity-text-tokens-c5.test.ts`:

```ts
import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const CSS = readFileSync(
  resolve(__dirname, '..', '..', 'styles', 'design-tokens-canonical.css'),
  'utf8'
);

// #ffffff (light card) and #1e1710 (dark card) — the EntityBadge pill surface.
const LIGHT_BG: [number, number, number] = [255, 255, 255];
const DARK_BG: [number, number, number] = [30, 23, 16];

function block(theme: 'light' | 'dark'): string {
  if (theme === 'light') return CSS.slice(0, CSS.indexOf('[data-theme="dark"]'));
  return CSS.slice(CSS.indexOf('[data-theme="dark"]'));
}

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  s /= 100;
  l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const hp = h / 60;
  const x = c * (1 - Math.abs((hp % 2) - 1));
  let r = 0,
    g = 0,
    b = 0;
  if (hp < 1) [r, g, b] = [c, x, 0];
  else if (hp < 2) [r, g, b] = [x, c, 0];
  else if (hp < 3) [r, g, b] = [0, c, x];
  else if (hp < 4) [r, g, b] = [0, x, c];
  else if (hp < 5) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];
  const m = l - c / 2;
  return [Math.round((r + m) * 255), Math.round((g + m) * 255), Math.round((b + m) * 255)];
}
function lum([r, g, b]: [number, number, number]): number {
  const f = (v: number) => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}
function contrast(a: [number, number, number], b: [number, number, number]): number {
  const [hi, lo] = lum(a) > lum(b) ? [lum(a), lum(b)] : [lum(b), lum(a)];
  return (hi + 0.05) / (lo + 0.05);
}
function readVar(theme: 'light' | 'dark', name: string): [number, number, number] {
  const m = block(theme).match(new RegExp(name + ':\\s*(\\d+)\\s+(\\d+)%\\s+(\\d+)%'));
  if (!m) throw new Error(`${name} not found in ${theme} block`);
  return hslToRgb(Number(m[1]), Number(m[2]), Number(m[3]));
}

describe('C5 — new --c-*-text vars are AA on the EntityBadge pill (#2862)', () => {
  it.each(['--c-event-text', '--c-agent-text', '--c-chat-text'])(
    '%s light value >= 4.5:1 on white card',
    name => {
      expect(contrast(readVar('light', name), LIGHT_BG)).toBeGreaterThanOrEqual(4.5);
    }
  );
  it.each(['--c-event-text', '--c-agent-text', '--c-chat-text'])(
    '%s dark value >= 4.5:1 on dark card',
    name => {
      expect(contrast(readVar('dark', name), DARK_BG)).toBeGreaterThanOrEqual(4.5);
    }
  );
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/__tests__/styles/entity-text-tokens-c5.test.ts`
Expected: FAIL — `--c-event-text not found in light block` (vars don't exist yet).

- [ ] **Step 3: Add the light-theme vars**

In `apps/web/src/styles/design-tokens-canonical.css`, after line 48 (`--c-session-text: 240 60% 35%;`), add:

```css
  --c-event-text: 350 89% 32%;   /* #2862 C5: ~5.2:1 on cream/white (from JS entityTextOverrides) */
  --c-agent-text: 38 92% 24%;    /* #2862 C5: ~5.7:1 on cream/white */
  --c-chat-text:  220 80% 38%;   /* #2862 C5: ~5.4:1 on cream/white */
```

- [ ] **Step 4: Add the dark-theme vars**

In the `[data-theme="dark"]` block, after line 240 (`--c-session-text: 235 85% 85%; ...`), add:

```css
  --c-event-text: 350 85% 78%;   /* #2862 C5: lighter for AA on dark card */
  --c-agent-text: 38 92% 72%;    /* #2862 C5: lighter for AA on dark card */
  --c-chat-text:  218 85% 80%;   /* #2862 C5: lighter for AA on dark card */
```

- [ ] **Step 5: Run the AA test + the Phase B gates**

Run: `pnpm exec vitest run src/__tests__/styles/entity-text-tokens-c5.test.ts`
Expected: PASS (6 assertions). If any dark value is < 4.5:1, raise its lightness a few % and re-run.
Run: `pnpm exec vitest run src/__tests__/styles/entity-token-golden.test.ts src/__tests__/styles/entity-token-consistency.test.ts`
Expected: PASS — both gates exclude `-text` variants (they match `--c-<entity>:` only), so the new vars don't affect them.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/styles/design-tokens-canonical.css apps/web/src/__tests__/styles/entity-text-tokens-c5.test.ts
git commit -m "$(cat <<'EOF'
feat(design-tokens): add --c-event/agent/chat-text canonical vars (#2862)

Completes the --c-*-text single source for the 3 entities that only had JS
overrides. Light + dark, AA-verified on the card surface. C5 prep.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: Rewrite `entityHsl` / `entityHslText` to CSS-var-backed

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts` (rewrite `entityHsl` lines 38-44 + `entityHslText` lines 92-98; add `CSS_ENTITY` + `HAS_TEXT_VAR`; remove `entityTextOverrides` lines 79-90 if orphan)
- Modify: `apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts` (update `entityHsl` form assertions; add new)

**Interfaces:**
- Consumes: the canonical `--c-*` / `--c-*-text` vars (Task 1 for event/agent/chat).
- Produces: `entityHsl(entity, alpha?)` → `hsl(var(--c-<name>) [/ alpha])`; `entityHslText(entity, alpha?)` → `hsl(var(--c-<name>-text) [/ alpha])` or solid. `entityTokens` unchanged (auto-migrated). `entityColors` unchanged.

- [ ] **Step 1: Update the tokens test to the new form (fails first)**

In `apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts`, replace the `describe('entityHsl', ...)` block (lines 83-91) with:

```ts
describe('entityHsl (CSS-var-backed, theme-aware — #2862)', () => {
  it('returns hsl(var(--c-<entity>)) without alpha', () => {
    expect(entityHsl('game')).toBe('hsl(var(--c-game))');
  });

  it('returns hsl(var(--c-<entity>) / alpha) with alpha', () => {
    expect(entityHsl('game', 0.5)).toBe('hsl(var(--c-game) / 0.5)');
  });

  it('maps gameNightEvent to the event palette', () => {
    expect(entityHsl('gameNightEvent')).toBe('hsl(var(--c-event))');
  });
});

describe('entityHslText (CSS-var-backed, theme-aware — #2862)', () => {
  it.each(['game', 'kb', 'toolkit', 'session', 'event', 'agent', 'chat'] as const)(
    '%s uses the -text variant',
    entity => {
      expect(entityHslText(entity)).toBe(`hsl(var(--c-${entity}-text))`);
    }
  );

  it('player/tool fall back to the solid var (no -text)', () => {
    expect(entityHslText('player')).toBe('hsl(var(--c-player))');
    expect(entityHslText('tool')).toBe('hsl(var(--c-tool))');
  });

  it('gameNightEvent uses the event -text variant', () => {
    expect(entityHslText('gameNightEvent')).toBe('hsl(var(--c-event-text))');
  });
});
```

Also add `entityHslText` to the import on line 2:
```ts
import { entityColors, entityHsl, entityHslText, entityLabel, entityIcon, statusColors } from '../tokens';
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts`
Expected: FAIL — current `entityHsl('game')` returns `hsl(25, 95%, 39%)`, not `hsl(var(--c-game))`.

- [ ] **Step 3: Rewrite the helpers in tokens.ts**

In `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`:

(a) Replace the `entityHsl` function (lines 38-44) with:

```ts
/**
 * Canonical entity -> --c-* var name. gameNightEvent reuses event's rose palette.
 */
const CSS_ENTITY: Record<MeepleEntityType, string> = {
  game: 'game',
  player: 'player',
  session: 'session',
  agent: 'agent',
  kb: 'kb',
  chat: 'chat',
  event: 'event',
  toolkit: 'toolkit',
  tool: 'tool',
  gameNightEvent: 'event',
};

/** Entity var names that have a canonical --c-*-text (AA text) variant. */
const HAS_TEXT_VAR: ReadonlySet<string> = new Set([
  'game',
  'kb',
  'toolkit',
  'session',
  'event',
  'agent',
  'chat',
]);

/**
 * #2862 (C5): theme-aware, single-source entity color. Emits the canonical
 * `--c-*` CSS var (which has light + dark values) instead of a hardcoded,
 * light-only HSL. `alpha` (0..1) tints via modern `hsl(H S L / A)` syntax.
 */
export function entityHsl(entity: MeepleEntityType, alpha?: number): string {
  const v = `var(--c-${CSS_ENTITY[entity]})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}
```

(b) Delete the `entityTextOverrides` const (lines 79-90) — it becomes orphan after (c). First confirm it has no other consumer:
Run: `grep -rn "entityTextOverrides" apps/web/src` — expected: only its definition in tokens.ts (it is a private, non-exported const). If any other reference exists, keep it and note in your report.

(c) Replace the `entityHslText` function (lines 92-98) with:

```ts
/**
 * #2862 (C5): theme-aware AA-safe entity text color. Uses the canonical
 * `--c-*-text` var for entities that have one; falls back to the solid
 * `--c-*` for player/tool (matching the pre-C5 fallback behavior).
 */
export function entityHslText(entity: MeepleEntityType, alpha?: number): string {
  const name = CSS_ENTITY[entity];
  const suffix = HAS_TEXT_VAR.has(name) ? '-text' : '';
  const v = `var(--c-${name}${suffix})`;
  return alpha !== undefined ? `hsl(${v} / ${alpha})` : `hsl(${v})`;
}
```

(Leave `entityColors`, `entityTokens`, `statusColors`, `entityLabel`, `entityIcon` untouched. `entityTokens` now emits CSS-var strings automatically because it calls the rewritten `entityHsl`.)

- [ ] **Step 4: Run the tokens test + the meeple-card suite**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts`
Expected: PASS (entityHsl/entityHslText new-form assertions + the unchanged entityColors + #636 regression).
Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card`
Expected: PASS (variants, parts, acceptance-matrix — no class assertions, no crash).
Run: `pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/tokens.ts \
        apps/web/src/components/ui/data-display/meeple-card/__tests__/tokens.test.ts
git commit -m "$(cat <<'EOF'
refactor(meeple-card): entityHsl/entityHslText emit canonical --c-* vars (#2862)

Rewrites the two color helpers to reference the theme-aware canonical --c-* /
--c-*-text CSS vars instead of hardcoded light-only JS HSL. entityTokens and all
~19 accent call-sites become theme-aware + single-source with zero call-site
edits. entityColors kept (external consumers + #636). Removes orphan
entityTextOverrides.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: Make the EntityBadge pill theme-aware

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx` (line 1 eslint-disable; line 22 `bg-white/85` → `bg-card/85`)

**Interfaces:**
- Consumes: the theme-aware `entityHslText` (Task 2). No new exports.

- [ ] **Step 1: Swap the pill surface + drop the eslint-disable**

In `apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx`:

Remove line 1 entirely:
```
/* eslint-disable local/no-hardcoded-color-utility -- glass bg-white/85 follows the mockup .e-bg pattern; entity color text via inline style. DS-12 primitive — see token-bridge-map.md for migration plan. */
```

In the `className` on line 22, change `bg-white/85` to `bg-card/85` (theme-aware glass surface so the now-theme-aware `--c-*-text` is legible in both themes). The line becomes:

```tsx
      className={`${positioning} inline-flex items-center gap-1 rounded-md bg-card/85 px-2 py-0.5 font-[var(--font-quicksand)] text-[9px] font-extrabold uppercase tracking-wide shadow-sm backdrop-blur-md ${className}`}
```

- [ ] **Step 2: Lint the file (the disable removal must not re-introduce a violation)**

Run: `pnpm exec eslint src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx`
Expected: no errors. (`bg-card/85` is a semantic utility; `text-*` is set via inline style, not a hardcoded class.)

- [ ] **Step 3: Run EntityBadge's test (if any) + the parts suite**

Run: `pnpm exec vitest run src/components/ui/data-display/meeple-card/parts`
Expected: PASS. (EntityBadge tests assert `data-slot`/label/icon, not the bg class.)

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card/parts/EntityBadge.tsx
git commit -m "$(cat <<'EOF'
refactor(meeple-card): EntityBadge pill uses theme-aware bg-card/85 (#2862)

The entity text is now theme-aware (--c-*-text flips per theme), so the pill
surface must too — bg-white/85 (invariant white) would show light text on white
in dark mode. Swaps to the semantic bg-card/85 and drops the now-unneeded
no-hardcoded-color-utility disable.

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>
EOF
)"
```

---

### Task 4: Final verification + PR

**Files:** none (verification only).

- [ ] **Step 1: Quality gates**

Run each and confirm PASS:
- `cd apps/web && pnpm exec vitest run src/components/ui/data-display/meeple-card src/__tests__/styles`
- `cd apps/web && pnpm typecheck`
- `cd apps/web && pnpm lint`
- `cd apps/web && pnpm build`

- [ ] **Step 2: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-2862-migrate-entity-tokens
gh pr create --base main-dev --title "refactor(meeple-card): migrate entity accents to canonical --c-* vars (#2862)" --body "$(cat <<'EOF'
Closes #2862 (C5 / ST8 of umbrella #2863).

## What
Rewrites `entityHsl`/`entityHslText` (`meeple-card/tokens.ts`) to emit the canonical, theme-aware `--c-*` / `--c-*-text` CSS vars instead of hardcoded **light-only** JS HSL. `entityTokens` and all ~19 accent call-sites (dots, glows, Cover/Hero gradients, AccentBorder, TagStrip, ManaPips, ConnectionChip, EntityBadge) become theme-aware + single-source **with zero call-site edits** — fixing the audit's "the card re-derives colors in JS → diverges in dark". Adds the 3 missing `--c-*-text` canonical vars (event/agent/chat, light + dark, AA-verified). Makes the EntityBadge pill `bg-white/85` → `bg-card/85` so its now-theme-aware text stays AA in both themes.

## Kept (out of scope, per design §5)
`--mc-*` surface chrome (theme-aware warm-glass, no dark bug); `entityColors` (external consumers + #636 regression); `statusColors`.

## Verification
tokens test (new CSS-var form), --c-*-text AA test (light on #fff, dark on #1e1710), Phase B golden/consistency gates (unaffected — exclude -text), meeple-card suite, typecheck/lint/build — all green.

## Designer review
Visual delta: entity accents shift ~1% lightness (JS 39% → canonical 38%) and now render correctly in **dark mode** (previously light-only); the EntityBadge pill changes from white-glass to theme-aware card-glass. Gates don't inspect variant classes — please eyeball light + dark.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 3: After CI green + merge — close-out**

Update issue #2862 (state + DoD) and tick its box in umbrella #2863's Phase C checklist — this completes Phase C (C1-C5 all done).

---

## Self-Review notes (author)

- **Spec coverage:** §3 helper rewrite → Task 2; §3 EntityBadge → Task 3; §3 `--c-*-text` → Task 1; §4 testing → per-task + Task 4; §7 acceptance → all tasks. Mapped.
- **Type consistency:** `CSS_ENTITY` / `HAS_TEXT_VAR` defined in Task 2 and used by both helpers; `entityHsl`/`entityHslText` signatures unchanged (alpha retained). Test assertions match the emitted strings exactly (`hsl(var(--c-game))`, `hsl(var(--c-game) / 0.5)`).
- **Ordering:** Task 1 (CSS vars) before Task 2 (entityHslText references them) before Task 3 (relies on theme-aware text). Task-1 vars must exist before Task-3's badge renders theme-aware text AA — enforced by task order.
- **Risk:** purely visual; gates green but designer review flagged in the PR. `--c-*-text` AA locked by Task-1 test.
