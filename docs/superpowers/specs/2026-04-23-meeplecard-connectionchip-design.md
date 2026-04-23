# MeepleCard ConnectionChip Redesign — Design Spec

**Date**: 2026-04-23
**Scope**: MeepleCard UI component — iconography, visual states, entity colors, typography
**Focus areas**: A (Visual identity & tone), C (Iconography & illustration), E (Stati visuali & feedback)
**Status**: Draft, pending user review

---

## 1. Problem Statement

The `MeepleCard` component currently has two parallel sub-systems that express related concepts with inconsistent visual treatment:

- **`ManaPips`** — small colored dots (6/8/12px) shown in meta-row. Originally conceived to represent connected entities (KB docs, sessions, agent, chat) linked to the parent card. After a recent update, pips lost their functional role (no popover drill-down, no create-affordance) and their visual quality (icons replaced by flat colored dots). Users perceive this as a regression.
- **`NavFooterItem`** — richer buttons (28px circle + Lucide icon + count badge + plus overlay + label + hover glow) used in card footer. This is the visual language users expect.

Related issues:
- Icon dualism: `tokens.entityIcon` uses emoji (🎲👤🎯🤖📚💬📅🧰🔧) in popover headers; `nav-items/icons.tsx` uses Lucide icons in NavFooter. Same entity renders differently in different contexts.
- `CardStatus` enum (12 values) mixes three orthogonal semantic axes (ownership/lifecycle/processing) forcing users to choose one when multiple could apply.
- `entityColor` covers only 7/9 entities; missing `toolkit` and `tool`. Derived values (`/0.12`, `/0.35`) are inlined everywhere with no named tokens.
- Typography scale is ad-hoc: title/subtitle/meta sizes drift slightly between variants.

## 2. Goals

1. **Unify** `ManaPips` and `NavFooterItem` into a single `ConnectionChip` component with size variants.
2. **Restore functional role** of connection visualization: count, drill-down popover, create-affordance.
3. **Unify iconography** around Lucide icons for all 9 entity types; remove emoji dualism.
4. **Consolidate CardStatus** from 12 values into two orthogonal dimensions (ownership + lifecycle).
5. **Complete entity color system** for all 9 entities with named derived tokens and WCAG AA validation.
6. **Codify typography scale** for MeepleCard with tokens applied consistently across variants.

## 3. Non-Goals

- Redesigning MeepleCard variants (grid/list/compact/featured/hero/focus layouts stay).
- Motion/micro-interactions overhaul (focus B was explicitly deferred).
- Replacing Quicksand/Nunito fonts.
- Changing backend DTOs (connections is a presentation-layer concept).

---

## 4. Design: `ConnectionChip` component

### 4.1 API

```ts
// src/components/ui/data-display/meeple-card/parts/ConnectionChip.tsx

export interface ConnectionItem {
  id: string;
  label: string;
  href: string;
}

export interface ConnectionChipProps {
  entityType: MeepleEntityType;
  count?: number;
  items?: ConnectionItem[];
  size?: 'sm' | 'md';       // sm=22px inline, md=28px footer
  showLabel?: boolean;       // default: size==='md'
  label?: string;            // override auto-label
  onCreate?: () => void;
  createLabel?: string;
  href?: string;             // direct link (no popover)
  colorOverride?: string;    // rare, for custom contexts
  disabled?: boolean;
  loading?: boolean;
}
```

```ts
// src/components/ui/data-display/meeple-card/parts/ConnectionChipStrip.tsx

export interface ConnectionChipStripProps {
  variant: 'footer' | 'inline';
  children: React.ReactNode;
  className?: string;
}
```

### 4.2 Integration with MeepleCard

```diff
// types.ts — MeepleCardProps
- navItems?: NavItemProps[];
- manaPips?: ManaPipProps[];
+ connections?: ConnectionChipProps[];
+ connectionsVariant?: 'footer' | 'inline' | 'auto';   // default: 'auto'
```

**Per-variant auto-defaults** when `connectionsVariant='auto'`:

| Card variant | Strip variant |
|--------------|---------------|
| grid         | footer        |
| list         | inline        |
| compact      | inline        |
| featured     | footer        |
| hero         | footer        |
| focus        | inline        |

### 4.3 File layout

```
apps/web/src/components/ui/data-display/meeple-card/
  parts/
    ConnectionChip.tsx                  NEW
    ConnectionChipStrip.tsx             NEW
    ConnectionChipPopover.tsx           NEW (was ManaPipPopover)
    entity-icons.tsx                    NEW (unified icon registry)
    typography.ts                       NEW (cardTypeClass)
    ManaPips.tsx                        DELETE
    nav-items/NavFooter.tsx             DELETE
    nav-items/NavFooterItem.tsx         DELETE
    nav-items/icons.tsx                 DELETE (merged into entity-icons)
  MeepleCard.tsx                        MODIFIED (props diff)
  types.ts                              MODIFIED
  tokens.ts                             MODIFIED (see §6, §7)
  hooks/useEntityColor.ts               NEW (light/dark resolver)
  index.ts                              MODIFIED (re-exports)
```

---

## 5. Iconography

### 5.1 Unified entity icon registry

```tsx
// src/components/ui/data-display/meeple-card/parts/entity-icons.tsx
import {
  Dices, UserCircle2, Swords, Bot,
  BookOpen, MessageCircle, Calendar,
  Briefcase, Wrench, type LucideIcon,
} from 'lucide-react';
import type { MeepleEntityType } from '../types';

export const entityIcons: Record<MeepleEntityType, LucideIcon> = {
  game:    Dices,
  player:  UserCircle2,
  session: Swords,
  agent:   Bot,
  kb:      BookOpen,
  chat:    MessageCircle,
  event:   Calendar,
  toolkit: Briefcase,
  tool:    Wrench,
};

export const ENTITY_ICON_STROKE = 1.75;
export const ENTITY_ICON_SIZE = { sm: 14, md: 16 } as const;
```

### 5.2 Emoji removal

```diff
// tokens.ts
- export const entityIcon: Record<MeepleEntityType, string> = {
-   game: '🎲', player: '👤', session: '🎯', agent: '🤖',
-   kb: '📚', chat: '💬', event: '📅', toolkit: '🧰', tool: '🔧',
- };
```

Emoji usage survives only where **textual/narrative** (empty-state copy, avatar fallbacks). All **iconographic** usage migrates to Lucide.

### 5.3 Stroke and size policy

| Context                                 | Icon size | Container | Stroke |
|-----------------------------------------|-----------|-----------|--------|
| `ConnectionChip size="sm"` (inline)     | 14px      | 22×22     | 1.75   |
| `ConnectionChip size="md"` (footer)     | 16px      | 28×28     | 1.75   |
| `ConnectionChipPopover` header          | 16px      | inline    | 1.75   |

---

## 6. Visual states

### 6.1 State matrix

| State            | count  | onCreate | Badge  | Plus overlay | Border  | Icon opacity |
|------------------|--------|----------|--------|--------------|---------|--------------|
| Default          | >0     | any      | ✓      | ✗            | solid   | 1.0          |
| Empty + create   | 0      | ✓        | ✗      | ✓            | dashed  | 0.55         |
| Empty muted      | 0      | ✗        | ✗      | ✗            | dashed  | 0.35         |
| Hover            | >0     | any      | ✓      | ✗            | solid   | 1.0          |
| Popover open     | >0     | any      | ✓      | ✗            | solid   | 1.0          |
| Disabled         | any    | any      | dimmed | dimmed       | solid   | 0.4          |
| Loading          | —      | —        | ✗      | ✗            | none    | — (skeleton) |

### 6.2 Count formatting

- `count <= 99` → numeric badge
- `count > 99` → `"99+"`
- `count === 0 && onCreate` → plus overlay, no badge
- `count === 0 && !onCreate` → no badge, no plus

### 6.3 Tokens

```ts
// tokens.ts additions
export const connectionChipSizes = {
  sm: { chip: 22, icon: 14, badge: 14, plus: 12, gap: 4 },
  md: { chip: 28, icon: 16, badge: 16, plus: 14, gap: 6 },
} as const;

export const connectionChipTransitions = {
  hover: 'transform 180ms cubic-bezier(0.34, 1.56, 0.64, 1), box-shadow 180ms ease-out',
  glow:  (entityColor: string) =>
    `0 0 0 4px hsl(${entityColor} / 0.18), 0 4px 12px hsl(${entityColor} / 0.25)`,
};
```

### 6.4 Hover behaviour

- Transform: `scale(1.08)` on md, `scale(1.12)` on sm
- Box-shadow: glow using entity-colored drop + spread
- `prefers-reduced-motion`: disables transform, keeps glow

### 6.5 Popover

Uses Radix `Popover`, `side="bottom"`, `align="start"`. Structure:

- Header: `<Icon /> Label (count)` + close button
- Body: list of items with `<a href>`, hover bg `entity.fill`, max-height 320px scroll internal
- Footer (optional): "+ Create new ..." if `onCreate` defined

### 6.6 Accessibility

- Chip: `role="button"` when clickable; `aria-label` includes count + entity label (or `createLabel` for empty+create)
- Plus overlay: `aria-hidden="true"` (decorative)
- Popover inherits Radix a11y (focus trap, ESC, ARIA)
- `prefers-reduced-motion` respected

---

## 7. CardStatus consolidation

### 7.1 Two-axis model

Replace single `CardStatus` enum with two orthogonal dimensions:

```ts
// types.ts
export type OwnershipBadge =
  | 'owned'
  | 'wishlist'
  | 'archived';

export type LifecycleState =
  | 'active'
  | 'idle'
  | 'completed'
  | 'setup'
  | 'processing'
  | 'failed';

// MeepleCardProps diff
+ ownership?: OwnershipBadge;
+ lifecycle?: LifecycleState;
  status?: CardStatus;  // retained during Step 1–2 (deprecated), removed in Step 3
```

During Steps 1–2 the legacy `status` prop coexists with the new `ownership`/`lifecycle` props and is mapped via the adapter in §7.4. In Step 3 `status` and `CardStatus` enum are removed.

### 7.2 Consolidation decisions

| Legacy value | New mapping                  | Rationale                                         |
|--------------|------------------------------|---------------------------------------------------|
| `owned`      | `ownership: 'owned'`         | Direct                                            |
| `wishlist`   | `ownership: 'wishlist'`      | Direct                                            |
| `archived`   | `ownership: 'archived'`      | Direct                                            |
| `active`     | `lifecycle: 'active'`        | Direct                                            |
| `inprogress` | `lifecycle: 'active'`        | **Merged** — synonym of `active`                  |
| `idle`       | `lifecycle: 'idle'`          | Direct                                            |
| `paused`     | `lifecycle: 'idle'`          | **Merged** — semantically equivalent              |
| `completed`  | `lifecycle: 'completed'`     | Direct                                            |
| `setup`      | `lifecycle: 'setup'`         | Direct                                            |
| `processing` | `lifecycle: 'processing'`    | Direct                                            |
| `indexed`    | `{}` (removed from UI)       | Internal KB pipeline state, not user-facing       |
| `failed`     | `lifecycle: 'failed'`        | Direct                                            |

Net: **12 → 9 values** across 2 orthogonal axes. A card can now express BOTH ownership and lifecycle (e.g. `owned` + `active`).

### 7.3 Visual treatment

**Ownership badge** (top-right corner):

| Value      | Icon (Lucide) | Foreground           | Background             |
|------------|---------------|----------------------|------------------------|
| `owned`    | `CheckCircle2`| `hsl(152 76% 40%)`   | `hsl(152 76% 40% / .12)` |
| `wishlist` | `Heart` filled| `hsl(350 89% 60%)`   | `hsl(350 89% 60% / .12)` |
| `archived` | `Archive`     | `hsl(215 20% 50%)`   | `hsl(215 20% 50% / .08)` |

**Lifecycle state** (inline, in meta-row):

| Value        | Icon          | Color                | Animation           |
|--------------|---------------|----------------------|---------------------|
| `active`     | filled dot    | entity color         | pulse opacity 0.8→1, 2s infinite |
| `idle`       | outline dot   | `hsl(215 20% 60%)`   | none                |
| `completed`  | `CheckCheck`  | `hsl(152 76% 40%)`   | none                |
| `setup`      | `Settings2`   | `hsl(38 92% 50%)`    | none                |
| `processing` | `Loader2`     | `hsl(200 89% 55%)`   | rotate 360°, 1.2s infinite |
| `failed`     | `AlertTriangle` | `hsl(0 84% 60%)`   | none                |

**Rule**: max 1 ownership + 1 lifecycle visible per card. On `compact` variant: ownership only (no lifecycle to preserve density).

### 7.4 Legacy adapter

Step 1 (additive, non-breaking): `MeepleCard` accepts both `status` (legacy) and `ownership`/`lifecycle` (new). Internally, legacy maps via:

```ts
function mapLegacyStatus(status: CardStatus): {
  ownership?: OwnershipBadge;
  lifecycle?: LifecycleState;
} {
  switch (status) {
    case 'owned':      return { ownership: 'owned' };
    case 'wishlist':   return { ownership: 'wishlist' };
    case 'archived':   return { ownership: 'archived' };
    case 'active':
    case 'inprogress': return { lifecycle: 'active' };
    case 'idle':
    case 'paused':     return { lifecycle: 'idle' };
    case 'completed':  return { lifecycle: 'completed' };
    case 'setup':      return { lifecycle: 'setup' };
    case 'processing': return { lifecycle: 'processing' };
    case 'indexed':    return {};
    case 'failed':     return { lifecycle: 'failed' };
  }
}
```

Deprecation console warning in dev mode.

---

## 8. Entity colors and design tokens

### 8.1 Complete entity color registry

```ts
// tokens.ts
export const entityColor: Record<MeepleEntityType, string> = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '210 40% 55%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '165 70% 42%',   // NEW — teal
  tool:    '280 55% 55%',   // NEW — violet (desaturated vs player purple)
};
```

Hue separation check: minimum ΔH ≥ 15° between any two entities. Current minimum: `chat` (220) vs `session` (240) = 20°. Passes.

### 8.2 Named derived tokens

```ts
export const entityTokens = (entity: MeepleEntityType) => {
  const h = entityColor[entity];
  return {
    solid:  `hsl(${h})`,
    fill:   `hsl(${h} / 0.12)`,
    border: `hsl(${h} / 0.35)`,
    hover:  `hsl(${h} / 0.22)`,
    glow:   `hsl(${h} / 0.18)`,
    shadow: `hsl(${h} / 0.25)`,
    muted:  `hsl(${h} / 0.06)`,
    dashed: `hsl(${h} / 0.25)`,
    textOn: '#ffffff',
  };
};
```

Replaces all inline `hsl(... / 0.12)` etc. across ConnectionChip, popover, badges.

### 8.3 Dark mode parity

```ts
export const entityColorDark: Record<MeepleEntityType, string> = {
  game:    '25 95% 60%',
  player:  '262 83% 70%',
  session: '240 70% 70%',
  agent:   '38 92% 62%',
  kb:      '210 55% 68%',
  chat:    '220 80% 70%',
  event:   '350 89% 72%',
  toolkit: '165 65% 55%',
  tool:    '280 60% 68%',
};
```

Values above are initial estimates; final lightness per entity will be tuned to pass the contrast thresholds in §11.1 before merging Step 1. Any entity that fails validation gets its lightness adjusted (hue stays fixed for brand consistency).

`useEntityColor(entity)` hook resolves light/dark based on theme. Exposed as CSS variables at root (`--entity-color-game`, etc.) so any component can read without prop-drilling.

### 8.4 Card surface tokens

```ts
export const cardSurface = {
  light: {
    bg:          '#fdf8f3',
    bgElevated:  '#ffffff',
    shadow:      '0 2px 8px hsl(25 40% 30% / 0.08), 0 1px 2px hsl(25 40% 30% / 0.04)',
    shadowHover: '0 8px 24px hsl(25 40% 30% / 0.14), 0 2px 6px hsl(25 40% 30% / 0.06)',
    border:      'hsl(25 20% 88%)',
  },
  dark: {
    bg:          '#1c1917',
    bgElevated:  '#292524',
    shadow:      '0 2px 8px hsl(0 0% 0% / 0.3), 0 1px 2px hsl(0 0% 0% / 0.15)',
    shadowHover: '0 8px 24px hsl(0 0% 0% / 0.5), 0 2px 6px hsl(0 0% 0% / 0.25)',
    border:      'hsl(25 10% 20%)',
  },
};
```

Shadow hue is warm (25°) instead of neutral gray, reinforcing the "board game warmth" identity.

---

## 9. Typography scale

### 9.1 Token definition

```ts
// tokens.ts
export const cardTypography = {
  display:      { family: 'Quicksand', weight: 700, size: '1.875rem', leading: '1.15', tracking: '-0.02em' },
  title:        { family: 'Quicksand', weight: 600, size: '1.125rem', leading: '1.3',  tracking: '-0.005em' },
  titleCompact: { family: 'Quicksand', weight: 600, size: '0.9375rem', leading: '1.35', tracking: '0' },
  subtitle:     { family: 'Nunito',    weight: 500, size: '0.875rem', leading: '1.4',  tracking: '0', opacity: 0.75 },
  body:         { family: 'Nunito',    weight: 400, size: '0.875rem', leading: '1.5',  tracking: '0' },
  meta:         { family: 'Nunito',    weight: 500, size: '0.75rem',  leading: '1.3',  tracking: '0.01em', opacity: 0.7 },
  caption:      { family: 'Nunito',    weight: 600, size: '0.6875rem', leading: '1.2', tracking: '0.02em' },
  numeric:      { family: 'Quicksand', weight: 600, size: '0.625rem', leading: '1',    tracking: '0', features: '"tnum"' },
};
```

### 9.2 Tailwind class derivatives

```ts
// src/components/ui/data-display/meeple-card/parts/typography.ts
export const cardTypeClass = {
  display:      'font-[Quicksand] font-bold text-[1.875rem] leading-[1.15] -tracking-[0.02em]',
  title:        'font-[Quicksand] font-semibold text-lg leading-[1.3] -tracking-[0.005em]',
  titleCompact: 'font-[Quicksand] font-semibold text-[0.9375rem] leading-[1.35]',
  subtitle:     'font-[Nunito] font-medium text-sm leading-[1.4] opacity-75',
  body:         'font-[Nunito] font-normal text-sm leading-[1.5]',
  meta:         'font-[Nunito] font-medium text-xs leading-[1.3] tracking-[0.01em] opacity-70',
  caption:      'font-[Nunito] font-semibold text-[0.6875rem] leading-[1.2] tracking-[0.02em]',
  numeric:      'font-[Quicksand] font-semibold text-[0.625rem] leading-none [font-feature-settings:"tnum"]',
} as const;
```

No wrapper components — classes only.

### 9.3 Variant mapping

| Variant  | Title          | Subtitle  | Body                | Meta   |
|----------|----------------|-----------|---------------------|--------|
| grid     | `title`        | `subtitle`| —                   | `meta` |
| list     | `titleCompact` | `subtitle`| `body` (1-line)     | `meta` |
| compact  | `titleCompact` | `meta`    | —                   | `meta` |
| featured | `title`        | `subtitle`| `body` (2-line)     | `meta` |
| hero     | `display`      | `subtitle`| `body`              | `meta` |
| focus    | `title`        | `subtitle`| `body`              | `meta` |

### 9.4 Truncation rules

| Token          | Line clamp |
|----------------|------------|
| `display`      | 2          |
| `title`        | 2          |
| `titleCompact` | 2          |
| `subtitle`     | 1          |
| `body` (list)  | 1          |
| `body` (featured/hero) | 2  |

### 9.5 Responsive

Typography is non-responsive within a card (variant switching handles layout changes). Single exception:

- `display` below 640px viewport scales to 1.5rem (24px) to avoid layout break in mobile hero.

### 9.6 Accessibility

- Minimum size 11px (caption only, not on continuous text)
- Minimum line-height 1.3 (`body` uses 1.5 per WCAG 1.4.12 suggestion)
- Opacity 0.7/0.75 on meta/subtitle validated via contrast script (§11.1)

---

## 10. Migration strategy

### 10.1 File changes summary

**DELETE**: `ManaPips.tsx`, `ManaPipPopover.tsx` (renamed), `nav-items/NavFooter.tsx`, `nav-items/NavFooterItem.tsx`, `nav-items/icons.tsx`
**NEW**: `ConnectionChip.tsx`, `ConnectionChipStrip.tsx`, `ConnectionChipPopover.tsx`, `entity-icons.tsx`, `typography.ts`, `hooks/useEntityColor.ts`
**MODIFIED**: `MeepleCard.tsx`, `tokens.ts`, `types.ts`, `variants/*.tsx`, `index.ts`

### 10.2 Consumer call-sites

| File                                                              | Change                                                        |
|-------------------------------------------------------------------|---------------------------------------------------------------|
| `ui/navigation/connection-bar/ConnectionBar.tsx`                  | Internals refactor to use `ConnectionChipStrip variant="inline"` |
| `ui/navigation/connection-bar/build-connections.ts`               | Return type becomes `ConnectionChipProps[]`                   |
| `app/(authenticated)/games/[id]/GameDetailDesktop.tsx`            | No signature change (uses `ConnectionBar`)                    |
| `app/(authenticated)/agents/[id]/AgentCharacterSheet.tsx`         | No signature change                                           |
| `app/(authenticated)/sessions/[id]/page.tsx`                      | No signature change                                           |
| Storybook stories / snapshot tests touching MeepleCard            | Update fixtures from `navItems`/`manaPips` to `connections`   |

Scope discovery commands:
```bash
grep -rn "navItems=\|manaPips=" apps/web/src
grep -rn "entityIcon\[" apps/web/src
grep -rn "NavFooterItem\|ManaPips" apps/web/src
```

### 10.3 Three-step rollout

**Step 1 — Additive (non-breaking PR #1)**
- Introduce all new components, tokens, icons.
- `MeepleCard` accepts both legacy (`navItems`, `manaPips`, `status`) and new (`connections`, `ownership`, `lifecycle`) props.
- Legacy props internally mapped via adapters.
- Dev-only `console.warn` on legacy props usage.
- Contrast validation CI enabled.

**Step 2 — Migration sweep (one PR per bounded area)**
- Convert call-sites per area: games, agents, sessions, library, admin.
- Update `ConnectionBar` internals to use `ConnectionChipStrip`.
- Update snapshot baselines.

**Step 3 — Cleanup (PR N)**
- Remove legacy props from `MeepleCardProps`.
- Delete deprecated files.
- Remove `entityIcon` emoji export from `tokens.ts`.
- Update `bundle-size-baseline.json`.

### 10.4 Out of scope

- Variants of MeepleCard (layouts stay).
- Motion/hover animations beyond chip-level glow.
- Backend or DTO changes.

---

## 11. Test & validation strategy

### 11.1 Token validation (CI gate)

New script: `apps/web/scripts/validate-contrast.ts`

Validates for each `MeepleEntityType` × `{light, dark}`:
- `contrast(solid, cardBg) ≥ 3.0` (icon on card surface)
- `contrast(white, solid) ≥ 4.5` (count badge text)
- `contrast(solid, fill) ≥ 3.0` (icon on own fill)

Additional:
- Ownership/lifecycle colors vs card bg ≥ 3.0
- Opacity-reduced text (`meta` at 0.7, `subtitle` at 0.75) vs card bg ≥ 4.5

Outputs `apps/web/reports/contrast-validation.md`. CI workflow `.github/workflows/design-tokens.yml` fails if any threshold unmet.

### 11.2 Unit tests (Vitest)

**`ConnectionChip.test.tsx`** (target ≥95% coverage):
- Renders count badge when count > 0
- Renders "99+" when count > 99
- No badge when count === 0
- Plus overlay when count === 0 && onCreate
- No plus when count === 0 && !onCreate
- Click opens popover when items present
- Click calls onCreate when empty + create, no items
- Disabled blocks interactions
- Enter/Space triggers same as click
- Escape closes popover
- aria-label includes count + entity label
- Every `MeepleEntityType` resolves a Lucide component in `entityIcons`

**`status-adapter.test.ts`**: All 12 legacy `CardStatus` values map correctly, including merges (`inprogress → active`, `paused → idle`, `indexed → {}`).

### 11.3 Integration tests

**`MeepleCard.integration.test.tsx`**: "Wingspan ALL entities" scenario asserting 4 count badges, 1 plus overlay, ownership badge rendered.

Visual regression via existing snapshot tooling (if present): 6 variants × 2 modes = 12 snapshots baseline.

### 11.4 E2E tests (Playwright)

**`tests/e2e/meeple-card-connections.spec.ts`** (new):
- Connection chip opens popover and navigates via item link
- Empty chip with create affordance triggers create flow

Existing E2E on games/agents/sessions pages must continue passing unchanged (no snapshot updates without justification).

### 11.5 Accessibility audit

- `axe-core` run in Vitest: zero violations of severity `serious` or `critical`.
- Manual keyboard checklist documented:
  - Tab order through every chip
  - Enter/Space activates chip
  - Escape closes popover, focus returns to chip
  - Arrow keys navigate popover items
  - Screen reader announces count + entity + action
  - `prefers-reduced-motion` disables transform, keeps glow

### 11.6 Bundle size check

Update `bundle-size-baseline.json` at end of Step 3. Expected net delta: **−3 KB to +2 KB gzipped** (fewer components, more Lucide icon imports, all tree-shakeable). CI fails if delta > +5 KB without justification.

### 11.7 Definition of done

- [ ] Unit + integration + contrast CI green
- [ ] E2E on ≥3 migrated call-sites
- [ ] Axe zero serious/critical
- [ ] Contrast validation covers 9 entities × 2 modes
- [ ] Bundle delta documented
- [ ] Snapshot baselines updated
- [ ] Keyboard nav manually verified
- [ ] Session memory updated with `ConnectionChip` pattern

---

## 12. Risks and mitigations

| Risk                                                              | Mitigation                                                       |
|-------------------------------------------------------------------|------------------------------------------------------------------|
| Consumer outside `MeepleCard` imports `tokens.entityIcon` emoji   | Step 1 grep sweep; keep emoji export deprecated until Step 3     |
| Dark mode color shifts fail contrast                              | `validate-contrast.ts` is CI-gated; adjust lightness per entity  |
| `AgentCharacterSheet` loading regression                          | `ConnectionChip.loading` prop is drop-in on existing `docsLoading \|\| threadsLoading` gate |
| Bundle size grows beyond baseline                                 | CI enforces +5 KB limit; tree-shakeable Lucide imports           |
| Snapshot churn delays migration PRs                               | Snapshots regenerated in Step 2 PRs per bounded area, not bulk   |
| Users still have legacy muscle memory for `status` enum           | Step 1 keeps `status` working with adapter + console.warn        |

---

## 13. Open questions

None after user review of sections 1–5 + extended sections 4.1–4.3 (all approved).

---

## 14. Next steps

1. User reviews this spec.
2. On approval, invoke `writing-plans` skill to produce an implementation plan matching the three-step rollout in §10.3.
