# Entity Color Systems

> **Issue**: closes #1067 (WS-A token unification, umbrella #1066)
> **Status**: documented 2026-05-12
> **Related**: [`meeple-card-design-tokens.md`](./meeple-card-design-tokens.md), [`v2-token-system.md`](./v2-token-system.md), [`v2-a11y-token-audit.md`](./v2-a11y-token-audit.md), [`token-bridge-map.md`](./token-bridge-map.md)

## Overview

The frontend hosts **three parallel entity color systems** that initially appeared redundant but are **semantically distinct** and individually optimised for different contrast directions. WS-A originally proposed merging them into a single source of truth (per [`2026-05-12-mockup-conformity-roadmap.md`](../specs/2026-05-12-mockup-conformity-roadmap.md) §3 WS-A). After investigation, this convergence is **not feasible without regressing WCAG 2.1 AA compliance** across 11 consumers. This document records the rationale for keeping the three systems separate.

## The three systems

### 1. Runtime CSS variables — canonical source of truth

**File**: `apps/web/src/styles/globals.css` `:root` block (and the layered cascade through `design-tokens-canonical.css` and `design-tokens.css` post DS-1..DS-16 series).

**Tokens**: `--c-{entity}` HSL triplets (without `hsl()` wrapper).

**Use case**: Tailwind utilities (`text-entity-game`, `bg-entity-game/10`, etc.) and shadcn-style semantic tokens. Consumed widely across the app (Wave A–D migrated routes).

**Contrast tuning**: post #807 audit values (AA-compliant for **mixed** uses — used as foreground on light backgrounds AND background under white text via Tailwind class composition).

```css
--c-game: 25 95% 38%;       /* 4.82:1 ✅ */
--c-player: 262 83% 45%;    /* 8.55:1 ✅ */
--c-session: 240 60% 35%;   /* 12.22:1 ✅ */
```

### 2. EntityBadge palette — entity-color-as-background

**File**: `apps/web/src/components/ui/data-display/meeple-card/tokens.ts`.

**Export**: `entityColors`, `entityHsl(entity, alpha?)`, `entityTokens(entity)` (derived: solid/fill/border/hover/glow/shadow/muted/dashed/textOn).

**Use case**: `MeepleCard.EntityBadge` and related elements where the entity color is **the background** with white text on top (e.g. `<span style="background: hsl(...); color: #fff">GAME</span>`).

**Contrast tuning**: lightness chosen so each entity color, used as bg with white text at `text-[9px] font-extrabold`, meets WCAG 2.1 AA SC 1.4.3 ≥ 4.5:1. Tracked in issue #636 (Wave B.1/B.2/B.3 a11y exclusions audit).

```ts
game: { h: 25, s: '95%', l: '39%' }    // c25405 → 4.60:1 on white
player: { h: 262, s: '83%', l: '58%' } // 8b56eb → 5.67:1 on white
session: { h: 240, s: '60%', l: '55%' } // 5757c2 → 6.80:1 on white
```

The `l` values are **higher** than the canonical text-tuned palette because a more saturated/lighter color renders white text legibly while remaining visually identifiable as the entity hue.

### 3. Text-on-light palette — entity-color-as-text

**File**: `apps/web/src/lib/sessions-summary/entity-text-tokens.ts`.

**Export**: `ENTITY_TEXT_HSL`, `entityTextHslClass(entity)`, `entityBorderHslClass(entity)`.

**Use case**: Wave D.3 session-summary components (ConnectionBar pip text, SessionKpiGrid KPI value, AchievementsCarousel toolkit-tinted titles, ScoringBreakdownTable winner cells, SessionDiaryTimeline active filter pill, SessionShareCard winner chip, SessionSummaryHero winner emoji + tied banner + podium border).

The entity color is rendered **as text** on light surfaces (`bg-card`, `bg-card/80`, entity-tinted alpha overlays at 0.06–0.18).

**Contrast tuning**: lightness **darkened by ~7%** vs the EntityBadge palette so each entity color reads as text on near-white backgrounds with ≥ 4.5:1 contrast. Tracked in issue #756 (Wave D.3 a11y hotfix).

```ts
game:    'hsl(25, 95%, 32%)'   // 6.31:1 on white
player:  'hsl(262, 83%, 45%)'  // 8.55:1 on white
session: 'hsl(240, 60%, 38%)'  // 11.35:1 on white
```

## Why these three cannot be collapsed

The three palettes target **inverse contrast directions**:

| Direction | System | Optimised for |
|---|---|---|
| White text on entity bg | EntityBadge palette (#2) | Identifiability under saturated bg + readability of white text |
| Entity color text on white bg | Text-on-light palette (#3) | Readability of entity-tinted text on near-white surfaces |
| Mixed (Tailwind utilities) | Runtime CSS vars (#1) | Worst-case both directions — bias toward darker |

A single set of lightness values cannot satisfy all three constraints without regressing at least one direction. The runtime CSS vars (#1) bias toward darker (text-friendly) values, but that makes them too dark for the EntityBadge use case (#2) — white text on dark bg loses the identifiability of the entity hue.

## Migration assessment (rejected)

The original WS-A goal proposed unifying all three to consume `var(--c-{entity})` plus a derived `var(--c-{entity}-text)` token set. Concrete blockers:

1. **EntityBadge requires per-entity tuning** that cannot be derived linearly from a single base value. Player at l=58% needs to stay visually distinct from session at l=55% — both are bright enough for white-text-on-bg use but their relative position matters for entity disambiguation.
2. **Wave D.3 components consume Tailwind arbitrary values** (`text-[hsl(25,95%,32%)]`) via JIT static class extraction. Migrating these to `text-entity-game` Tailwind utilities would either lose the dark variant (the canonical `--c-game` is too light for text-on-light) or require a new `text-entity-game-text` utility class set.
3. **Bundle delta non-trivial**: 11 consumer files would need refactor. Visual baselines (Wave D.3 PNGs, Wave B.1/B.2/B.3 baselines) would all rebake.
4. **Effort**: 3-5 days vs WS-A original estimate (3-5h). Cost/benefit unfavourable when the current three-system setup is verified WCAG 2.1 AA compliant across all 11 consumers.

## Decision

WS-A scope reduced to **documentation only**. The three systems remain:

- ✅ Runtime CSS vars (`globals.css` + `design-tokens-canonical.css`) — primary, used by Tailwind utilities
- ✅ EntityBadge palette (`meeple-card/tokens.ts`) — preserved for entity-as-bg use case
- ✅ Text-on-light palette (`sessions-summary/entity-text-tokens.ts`) — preserved for entity-as-text use case

The major token canonicalization work (DS-1..DS-16 series, PR #1044–#1065) achieved the practical goal of removing legacy `--bg-base`/`--gaming-bg-*`/`--nh-bg-*` tokens and consolidating runtime CSS vars onto the mockup-faithful `design-tokens-canonical.css` source. The three entity palettes are an architectural choice, not technical debt.

## When to use which

| Scenario | Use |
|---|---|
| Tailwind utility class (`text-entity-game`, `bg-entity-game/10`) | Runtime CSS vars via `var(--c-game)` (transparent to consumer) |
| Custom EntityBadge or status pill where entity color is **background** + white text | `entityHsl(entity)` from `meeple-card/tokens.ts` |
| Session-summary component or D.3 surface where entity color is **text on near-white bg** | `entityTextHslClass(entity)` from `sessions-summary/entity-text-tokens.ts` |
| New surface — neither badge nor text-on-light | Default to Tailwind utility (`text-entity-X` / `bg-entity-X`) and verify contrast ≥ 4.5:1 in implementation review |

## References

- Issue #1067 (WS-A token unification) — closed with documentation-only PR
- Umbrella #1066 — Mockup Conformity Roadmap
- DS-1..DS-16 series PRs #1044–#1065 — token canonicalization runtime work
- Issue #807 — a11y AA token contrast audit (runtime CSS vars)
- Issue #636 — Wave B.1/B.2/B.3 EntityBadge a11y exclusions audit (EntityBadge palette)
- Issue #756 — Wave D.3 text-on-light a11y hotfix (text-on-light palette)
- Spec: [`2026-05-12-mockup-conformity-roadmap.md`](../specs/2026-05-12-mockup-conformity-roadmap.md) §3 WS-A
