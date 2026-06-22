# Token Bridge Map — v1 → canonical

| Field | Value |
|---|---|
| **Date** | 2026-05-12 (DS-1) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../specs/2026-05-12-token-canonicalization.md) |
| **Bridge file** | **`apps/web/src/styles/token-bridge.css`** _(planned)_ |
| **Canonical source** | [`apps/web/src/styles/design-tokens-canonical.css`](../../../apps/web/src/styles/design-tokens-canonical.css) (mirror of [`admin-mockups/design_files/tokens.css`](../../../admin-mockups/design_files/tokens.css)) |
| **Removal target** | DS-12 (when 0 consumers reference legacy names) |

## Purpose

Map legacy custom property names (used across ~167 v2 components + ~36 legacy non-prefixed components) to the canonical token vocabulary from the mockup. Each alias is a one-way redirect: consumer code keeps its current spelling while the cascade resolves to a single source of truth.

## Theming mechanism

- next-themes is configured with `attribute={['class', 'data-theme']}` (see [`ThemeProvider.tsx`](../../../apps/web/src/components/providers/ThemeProvider.tsx)). When the user picks dark mode, both `class="dark"` AND `data-theme="dark"` land on `<html>`.
- Canonical tokens use `:root[data-theme="dark"]` selectors.
- Legacy tokens (e.g. `globals.css :is(.dark, [data-theme="dark"]) { … }`) use a dual selector so they fire under both schemes during the transition.
- SSR hint: `<html lang="it" data-theme="light">` in [`layout.tsx`](../../../apps/web/src/app/layout.tsx) — eliminates dark→light FOUC on first paint.

## Alias table

### Backgrounds

| Legacy name (v1) | Canonical | Notes |
|---|---|---|
| `--bg-base` | `--bg` | Base page background |
| `--bg-elevated` | `--bg-card` | Cards, sheets |
| `--bg-surface` | `--bg-card` | Alias of card |
| `--background` (shadcn) | `--bg` | Tailwind `bg-background` resolves here |
| `--card` (shadcn) | `--bg-card` | Tailwind `bg-card` |
| `--card-foreground` | `--text` | Card text |
| `--popover` | `--bg-card` | Popovers, dropdowns |
| `--popover-foreground` | `--text` | |
| `--muted` | `--bg-muted` | Muted background |
| `--muted-foreground` | `--text-muted` | Muted text |
| `--nh-bg-base` | `--bg` | Warm-modern palette (deprecated) |
| `--nh-bg-surface` | `--bg-card` | |
| `--nh-bg-surface-end` | `--bg-muted` | |
| `--nh-bg-elevated` | `--bg-card` | |
| `--gaming-bg-base` | `--bg` | Legacy "premium gaming" (deprecated, file slated for removal in DS-12) |
| `--gaming-bg-elevated` | `--bg-card` | |
| `--gaming-bg-glass` | `--glass-bg` | |
| `--bg-glass` | `--glass-bg` | |

### Text

| Legacy name | Canonical | Notes |
|---|---|---|
| `--foreground` (shadcn) | `--text` | Tailwind `text-foreground` |
| `--text-primary` | `--text` | |
| `--text-secondary` | `--text-sec` | |
| `--text-tertiary` | `--text-muted` | |
| `--nh-text-primary` | `--text` | |
| `--nh-text-secondary` | `--text-sec` | |
| `--nh-text-muted` | `--text-muted` | |
| `--gaming-text-primary` | `--text` | |
| `--gaming-text-secondary` | `--text-sec` | |
| `--gaming-text-tertiary` | `--text-muted` | |
| `--gaming-text-accent` | `--brand-fg` | |

### Borders

| Legacy name | Canonical | Notes |
|---|---|---|
| `--border-primary` | `--border` | |
| `--border-secondary` | `--border-strong` | |
| `--nh-border-default` | `--border` | |
| `--gaming-border-glass` | `--glass-border` | |

### Entity colors

| Legacy name (`--e-*`) | Canonical (`--c-*`) | Notes |
|---|---|---|
| `--e-game` | `--c-game` | |
| `--e-player` | `--c-player` | |
| `--e-session` | `--c-session` | |
| `--e-agent` | `--c-agent` | |
| `--e-document` | `--c-kb` | KB entity uses `--c-kb` in canonical |
| `--e-chat` | `--c-chat` | |
| `--e-event` | `--c-event` | |
| `--e-toolkit` | `--c-toolkit` | |
| `--e-tool` | `--c-tool` | |

Dark overrides cascade through the canonical block; both `--e-*` and `--c-*` names resolve to the lighter HSL values automatically.

### Radius

| Legacy name | Canonical | Notes |
|---|---|---|
| `--radius` (shadcn) | `--r-md` | 10px |
| `--radius-card` | `--r-xl` | 18px |
| `--radius-btn` | `--r-md` | 10px |
| `--radius-pill` | `--r-pill` | 9999px |
| `--radius-sheet` | `--r-2xl` | 24px |

### Typography

| Legacy name | Canonical | Notes |
|---|---|---|
| `--font-display` | `--f-display` | Quicksand |
| `--font-heading` | `--f-display` | |
| `--font-body` | `--f-body` | Nunito |
| `--font-sans` | `--f-body` | |

## What is **NOT** aliased (still legacy)

The bridge intentionally does not alias:

- `--mc-*` (MeepleCard v2 internal palette) — clusters using `MeepleCard` migrate component-side in DS-7/DS-8.
- `--chart-{1..5}` — chart-specific, no mockup equivalent yet.
- `--sidebar-*` — UI shell tokens, awaiting dedicated cluster.
- Tailwind shadcn semantic tokens fully redeclared in `globals.css` (`--primary`, `--secondary`, `--accent`, `--destructive`, `--ring`, `--input`) — these stay until DS-7+ when individual clusters migrate.
- `--text-xs..3xl` Tailwind sizes — bridge to `--fs-*` happens at Tailwind `@theme` level (DS-4+).
- `--s-*` spacing tokens — Tailwind already provides 4-based scale; clusters migrate inline `style={{ padding: ... }}` consumers individually.

## Migration status

| Cluster | Status | Stage | Tracking PR |
|---|---|---|---|
| `app/(authenticated)/dashboard/*` | pending | DS-4 | — |
| `components/features/sessions/*` | pending | DS-5 | — |
| `components/features/session-live/*` | pending | DS-6 | — |
| `components/features/session-summary/*` | pending | DS-6 | — |
| `components/features/games/*` | pending | DS-7 | — |
| `components/features/game-detail/*` | pending | DS-7 | — |
| `components/features/agents/*` | pending | DS-8 | — |
| `components/features/agent-detail/*` | pending | DS-8 | — |
| `components/features/players/*` | pending | DS-9 | — |
| `components/features/player-detail/*` | pending | DS-9 | — |
| `components/features/gamebook/*` | pending | DS-10 | — |
| `components/features/game-chat/*` | pending | DS-10 | — |
| `components/features/library/*` | pending | DS-11 | — |
| `app/(authenticated)/library/*` | pending | DS-11 | — |

## Removal procedure (DS-12)

1. Verify `pnpm lint:tokens` reports 0 violations (no consumer uses legacy names).
2. Delete `apps/web/src/styles/token-bridge.css`.
3. Delete `apps/web/src/styles/premium-gaming.css` (legacy palette).
4. Reduce `apps/web/src/styles/globals.css` to Tailwind `@theme` + components + base-layer rules only (no `:root` token declarations).
5. Reduce `apps/web/src/styles/design-tokens.css` to consumer-facing utility classes only.
6. Update `layout.tsx` to import only `design-tokens-canonical.css` + `globals.css`.

## See also

- Spec: [`docs/for-developers/specs/2026-05-12-token-canonicalization.md`](../specs/2026-05-12-token-canonicalization.md)
- Parent spec: [`docs/for-developers/specs/2026-05-11-design-system-deversioning.md`](../specs/2026-05-11-design-system-deversioning.md)
- Mockup audit (Stage 1): [`docs/for-developers/audits/2026-05-11-mockup-conformity.md`](../audits/2026-05-11-mockup-conformity.md)
