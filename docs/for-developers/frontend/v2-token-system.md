# v2 Token System — Reference

> Canonical reference per il sistema di design token v2 di MeepleAI.
> Refs spec: `docs/superpowers/specs/2026-05-09-p2-807-token-redesign-design.md`

## 1. Overview

Il sistema usa **CSS custom properties** in `apps/web/src/styles/design-tokens.css` come single source of truth, parallelo a un secondo set in `apps/web/src/styles/globals.css` per Tailwind v4 `@theme inline` registration. Tutte le utility classes vengono generate automaticamente.

Architettura:

```
design-tokens.css :root { --c-event: 350 89% 38%; }    ← entityHsl() helper consumer
globals.css :root { --e-event: 350 89% 38%; }          ← Tailwind utilities consumer
       ↓
globals.css @theme inline {
  --color-entity-event: hsl(var(--e-event));
}
       ↓
Tailwind utilities: text-entity-event, bg-entity-event/10, border-entity-event, ring-entity-event
```

> Note: kb maps to Tailwind class `entity-document` (legacy naming preserved). Tutte le altre 8 entity usano lo stesso nome.

## 2. Token catalog (post #807 audit Iter 2 — 2026-05-09)

| Token | Light HSL | Dark HSL | Light Ratio | Dark Ratio | Use case |
|-------|-----------|----------|-------------|------------|----------|
| `game` | 25 95% 38% | 28 95% 58% | 4.82:1 | 7.51:1 | Game entity (catalog, library) |
| `player` | 262 83% 45% | 262 75% 70% | 8.55:1 | 5.49:1 | Players, profiles, stats |
| `session` | 240 60% 35% | 235 70% 70% | 12.22:1 | 5.43:1 | Live play sessions |
| `agent` | 38 92% 32% | 38 92% 62% | 4.87:1 | 9.89:1 | AI agents, bots |
| `kb` (`document`) | 174 60% 30% | 174 60% 55% | 5.12:1 | 9.44:1 | Knowledge base, docs |
| `chat` | 220 80% 40% | 218 80% 68% | 7.72:1 | 6.44:1 | Conversations |
| `event` | 350 89% 38% | 350 85% 70% | 6.79:1 | 6.38:1 | Events, alerts |
| `toolkit` | 142 70% 30% | 142 60% 58% | 4.88:1 | 9.39:1 | Toolkits (published agent+KB) |
| `tool` | 195 80% 32% | 195 75% 62% | 5.44:1 | 8.68:1 | Tools (timer, counter, etc) |

Audit dettagliato: `docs/for-developers/frontend/v2-a11y-token-audit.md`.

## 3. Usage examples

### Static known entity (preferred)

```tsx
<div className="text-entity-event bg-entity-event/10 border border-entity-event/30">
  Event item
</div>
```

### Dynamic entity (data-driven)

Usa il helper `getEntityToken()` da `apps/web/src/components/ui/v2/entity-tokens.ts`:

```tsx
import { getEntityToken, type EntityType } from '@/components/ui/v2/entity-tokens';

function EntityChip({ entity }: { entity: EntityType }) {
  const t = getEntityToken(entity);
  return <span className={`${t.text} ${t.bgSoft} ${t.border}`}>{t.emoji} {t.label}</span>;
}
```

Il helper ritorna 6 fields: `bg`, `bgSoft`, `text`, `border`, `emoji`, `label`.

### Alpha modifier

```tsx
<div className="bg-entity-event">100% opacity</div>
<div className="bg-entity-event/50">50% opacity</div>
<div className="bg-entity-event/10">10% opacity (soft bg)</div>
```

### Gradient

```tsx
<div className="bg-gradient-to-br from-entity-game to-entity-event">decorative</div>
```

### Conditional state mapping

Quando lo state determina l'entity, usa lookup table inline:

```tsx
const ENTITY_BY_STATE: Record<SessionState, EntityType> = {
  active: 'session',
  archived: 'kb',
  draft: 'agent',
};
const t = getEntityToken(ENTITY_BY_STATE[state]);
```

## 4. Dark mode behavior

Toggle: aggiungi/rimuovi class `.dark` su `<html>` (Tailwind v4 darkMode: 'class').

Il CSS var swap è automatico:
- Light: `--c-event: 350 89% 38%` / `--e-event: 350 89% 38%`
- Dark: `--c-event: 350 85% 70%` / `--e-event: 350 85% 70%` (in `.dark { ... }` block)

Nessuna change a livello componente — utility class `text-entity-event` automaticamente consuma la variante corretta in base a `data-theme`.

## 5. Migration guide (legacy inline HSL)

### Direct inline `style={{}}` con entity HSL

```tsx
// BEFORE
<div style={{ background: 'hsla(350, 89%, 48%, 0.10)', color: 'hsl(350, 89%, 48%)' }}>

// AFTER
<div className="bg-entity-event/10 text-entity-event">
```

### Module-level constant

```tsx
// BEFORE
const TOOLKIT_HSL = 'hsl(142, 70%, 35%)';
<div style={{ background: TOOLKIT_HSL }}>

// AFTER (rimuovi constant, usa Tailwind class)
<div className="bg-entity-toolkit">
```

### entityHsl() helper call

```tsx
// BEFORE (color-utils.ts entityHsl)
import { entityHsl } from '@/lib/color-utils';
const solid = entityHsl(entity);
<div style={{ background: solid }}>

// AFTER (entity-tokens.ts getEntityToken)
import { getEntityToken } from '@/components/ui/v2/entity-tokens';
const t = getEntityToken(entity);
<div className={t.bg}>
```

### Edge cases (gradient multi-hue, alpha stops dinamici)

Per gradient multi-entity dove Tailwind utility non basta:

```tsx
// OK to keep inline with TODO comment + ESLint disable per-line
// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: multi-entity gradient
const fallbackGradient = 'linear-gradient(155deg, hsl(25,95%,38%), hsl(350,89%,38%))';
```

## 6. ESLint enforcement

Custom rule `meepleai/no-inline-hsl-v2` (definito in `apps/web/eslint-rules/no-inline-hsl-v2.js`) blocca `hsl()`/`hsla()` con hue match entity signature in `apps/web/src/components/v2/**`.

Rule scope:
- Detect: hsl/hsla literal con hue ∈ entity ranges (game 20-30, player 257-267, session 235-245, agent 33-43, kb 169-179 o 205-215, chat 215-225, event 345-360, toolkit 137-147, tool 190-200) e saturation in declared range
- Allow: non-entity hues (custom decorative palette es. purple 270, grey low-sat)

Disable per-line con justification:

```tsx
// eslint-disable-next-line meepleai/no-inline-hsl-v2 -- TODO #807-followup: complex gradient
const grad = 'linear-gradient(135deg, hsl(25,95%,38%), hsl(350,89%,38%))';
```

## 7. Adding new tokens

Process per estendere palette (rare — solo se introduce nuova entity type):

1. Aggiungi var `--c-{name}` in `apps/web/src/styles/design-tokens.css` :root e .dark
2. Aggiungi var `--e-{name}` in `apps/web/src/styles/globals.css` :root e .dark
3. Registra `--color-entity-{name}: hsl(var(--e-{name}))` in `globals.css` `@theme inline` block
4. Aggiungi entry in `entity-tokens.ts`:
   - `EntityType` union
   - `TAILWIND_KEY` map (kb-style mapping if needed)
   - `EMOJI` map
   - `LABEL` map
   - `ENTITY_TOKENS` array
5. Aggiorna ESLint rule `ENTITY_HUE_RANGES` con il nuovo hue range
6. Run audit `tools/a11y/contrast-calc.ts` (estendi tokens con nuovo entity) per verificare AA
7. Aggiungi riga a questo doc + audit deliverable

## 8. Audit reference

Storico ratios e methodology: `docs/for-developers/frontend/v2-a11y-token-audit.md`.

Tooling: `tools/a11y/contrast-calc.ts` (WCAG 2.1 SC 1.4.3 calculator) — riusa per verifying nuovi token.

## 9. Known follow-ups

- **57 inline HSL ESLint disables** in v2 components con `TODO #807-followup` markers — gradient multi-entity, JS-style alpha stops, Tailwind arbitrary value classes. Da affrontare in PR follow-up dedicate.
- **MeepleCard `tokens.ts`** — sistema separato per "white text on entity bg" use case (inverse direction). Non touched da P2 #807. Tracking issue per sunset graduale: TBD.
- **`entity-text-tokens.ts`** in sessions-summary — parallel system Wave D.3 (foreground darker per inverse direction). Potrà essere consolidato con shared `--c-*` / `--e-*` quando ENTITY_BG_ALPHA / ENTITY_RING_ALPHA pattern viene assorbito da Tailwind utilities.
