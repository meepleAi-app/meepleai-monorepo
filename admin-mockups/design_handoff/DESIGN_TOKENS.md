# Design Tokens — copia 1:1

Tutti i CSS tokens del design system MeepleAI. Da copiare interi in `src/styles/tokens.css` (o equivalente). Non hex-codificare nulla nella UI — usa solo questi.

Il file sorgente completo è in `design/tokens.css`. Questo documento estrae le sezioni e spiega come usarle.

## Setup

```ts
// src/main.tsx (o entry)
import './styles/tokens.css';
import './styles/components.css';
```

```html
<!-- index.html -->
<link rel="preconnect" href="https://fonts.googleapis.com"/>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
<link href="https://fonts.googleapis.com/css2?family=Quicksand:wght@400;500;600;700&family=Nunito:wght@400;500;600;700;800&family=JetBrains+Mono:wght@400;500;600;700;800&display=swap" rel="stylesheet"/>
```

```html
<!-- Attribute per dark mode auto -->
<html data-theme="light">
```

```ts
// auto-detect dark mode
function initTheme() {
  const saved = localStorage.getItem('mai-theme');
  if (saved) {
    document.documentElement.setAttribute('data-theme', saved);
  } else if (window.matchMedia('(prefers-color-scheme: dark)').matches) {
    document.documentElement.setAttribute('data-theme', 'dark');
  }
}
```

## 9 Entity Colors

| Entity | HSL | Emoji | Uso |
|--------|-----|-------|-----|
| **game** | `--c-game` (orange) | 🎲 | Game e library |
| **player** | `--c-player` (violet) | 👤 | Giocatori e gruppo |
| **session** | `--c-session` (indigo) | 🎯 | Partite |
| **agent** | `--c-agent` (amber/yellow) | 🤖 | AI agents |
| **kb** | `--c-kb` (green) | 📚 | Knowledge base / documenti |
| **chat** | `--c-chat` (blue) | 💬 | Conversazioni |
| **event** | `--c-event` (rose/red) | 🔔 | Eventi e notifiche |
| **toolkit** | `--c-toolkit` (teal/green) | 🧰 | Strumenti |
| **tool** | `--c-tool` (cyan) | 🔧 | Sub-strumento |

**Regole**:
1. **NON aggiungere mai un 10° entity type**. Se serve un colore nuovo, motiva e discuti col team.
2. **NON usare i nomi entity per cose generiche**. `error/danger` non è `event`. `success` non è `kb`.
3. **NON usare grigi diretti**. Usa `--text`, `--text-sec`, `--text-muted`, `--border`, `--bg-muted`.

### Helper TypeScript

```ts
// src/lib/entity-color.ts
export type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

const HSL_MAP: Record<EntityType, { h: number; s: number; l: number; em: string }> = {
  game:    { h: 28,  s: 84, l: 55, em: '🎲' },
  player:  { h: 268, s: 56, l: 56, em: '👤' },
  session: { h: 235, s: 64, l: 56, em: '🎯' },
  agent:   { h: 38,  s: 90, l: 56, em: '🤖' },
  kb:      { h: 142, s: 54, l: 42, em: '📚' },
  chat:    { h: 200, s: 78, l: 52, em: '💬' },
  event:   { h: 348, s: 78, l: 52, em: '🔔' },
  toolkit: { h: 168, s: 60, l: 38, em: '🧰' },
  tool:    { h: 192, s: 78, l: 52, em: '🔧' },
};

export function entityColor(entity: EntityType, alpha?: number): string {
  const c = HSL_MAP[entity];
  return alpha !== undefined
    ? `hsla(${c.h}, ${c.s}%, ${c.l}%, ${alpha})`
    : `hsl(${c.h}, ${c.s}%, ${c.l}%)`;
}

export function entityEmoji(entity: EntityType): string {
  return HSL_MAP[entity].em;
}
```

Uso:

```tsx
<div style={{ color: entityColor('agent'), background: entityColor('agent', 0.12) }}>
  {entityEmoji('agent')} Wingspan Rules
</div>
```

## Typography

```css
--f-display: 'Quicksand', system-ui, -apple-system, sans-serif;
--f-body:    'Nunito', system-ui, -apple-system, sans-serif;
--f-mono:    'JetBrains Mono', ui-monospace, 'SF Mono', monospace;

--fs-xs:  11px;
--fs-sm:  12px;
--fs-md:  14px;
--fs-lg:  16px;
--fs-xl:  18px;
--fs-2xl: 22px;
--fs-3xl: 28px;
--fs-4xl: 36px;

--fw-regular: 400;
--fw-medium:  500;
--fw-semi:    600;
--fw-bold:    700;
--fw-extra:   800;
```

**Regole**:
- Titoli + display + UI chrome → `var(--f-display)` Quicksand
- Body text + paragrafi → `var(--f-body)` Nunito
- Numbers, codes, ID, timestamps → `var(--f-mono)` JetBrains Mono
- **MAI** introdurre un 4° font (Inter, Roboto, Arial, system-ui da soli)

## Spacing scale (4px grid)

```css
--s-1: 4px;
--s-2: 8px;
--s-3: 12px;
--s-4: 16px;
--s-5: 20px;
--s-6: 24px;
--s-7: 32px;
--s-8: 40px;
--s-9: 56px;
--s-10: 72px;
```

Tutti i padding, margin, gap usano questi. Mai px hardcoded sopra `--s-3`.

## Border radius

```css
--r-xs:  4px;
--r-sm:  6px;
--r-md:  10px;
--r-lg:  14px;
--r-xl:  20px;
--r-2xl: 28px;
--r-pill: 999px;
```

## Motion

```css
--dur-xs: 80ms;
--dur-sm: 160ms;
--dur-md: 280ms;
--dur-lg: 480ms;

--ease-out:    cubic-bezier(0.16, 1, 0.3, 1);
--ease-in:     cubic-bezier(0.7, 0, 0.84, 0);
--ease-in-out: cubic-bezier(0.65, 0, 0.35, 1);
--ease-spring: cubic-bezier(0.34, 1.56, 0.64, 1);
```

**Regola**: rispetta `@media (prefers-reduced-motion: reduce)` — disable animations.

## Backgrounds & text

```css
/* Light theme (default) */
--bg:        #faf7f0;     /* warm cream */
--bg-card:   #ffffff;
--bg-muted:  #f1ece1;
--bg-sunken: #e9e2d2;

--text:       #1a1612;    /* warm black */
--text-sec:   #4a3f33;
--text-muted: #8b7e6b;

--border:        #d9cfb8;
--border-light:  #ebe3d0;
--border-strong: #b8a98a;

/* Dark theme — html[data-theme="dark"] */
--bg:        #14100a;     /* warm dark */
--bg-card:   #1f1a12;
--bg-muted:  #2a2218;
--bg-sunken: #0e0b07;

--text:       #f6f1e2;
--text-sec:   #c7baa1;
--text-muted: #8a7e69;
```

## Shadows

```css
--shadow-xs: 0 1px 2px hsla(0, 0%, 0%, 0.04);
--shadow-sm: 0 2px 8px hsla(0, 0%, 0%, 0.06);
--shadow-md: 0 6px 20px hsla(0, 0%, 0%, 0.08);
--shadow-lg: 0 16px 48px hsla(0, 0%, 0%, 0.12);
--shadow-drawer: 0 -8px 32px hsla(0, 0%, 0%, 0.12);
```

## Glass / blur

```css
--glass-bg: hsla(40, 38%, 96%, 0.72);   /* light */
--glass-bg-dark: hsla(30, 25%, 10%, 0.72); /* dark via [data-theme=dark] */
```

Per topbar fixed, modal backdrop, ecc.

## z-index scale

```css
--z-dropdown: 50;
--z-sticky:   100;
--z-fixed:    200;
--z-modal:    300;
--z-toast:    400;
```

## Utility class già pronte

`design/components.css` include classi atomiche pronte. Da copiare in `src/styles/components.css`:

- `.e-game`, `.e-player`, ... → utility per `color: entityColor(X)`
- `.e-tint-game`, ... → `background: entityColor(X, 0.12)`
- `.e-ring-game`, ... → `box-shadow: 0 0 0 3px entityColor(X, 0.16)`
- `.mai-cb-scroll` → barra chip horizontal-scroll
- `.phone`, `.phone-sbar` → frame mobile (per design canvas, NON in produzione)

## Antipattern da evitare

```css
/* ❌ NO — hex hardcoded */
.btn { color: #d97757; }

/* ❌ NO — grey arbitrario */
.subtitle { color: #888; }

/* ❌ NO — font fuori sistema */
.title { font-family: Inter, sans-serif; }

/* ❌ NO — radius arbitrario */
.card { border-radius: 13px; }

/* ❌ NO — colore HSL inline hardcoded (FREEZE issue #807/#808) */
.tag { background: hsl(28, 89%, 48%); }

/* ✅ SÌ — token CSS */
.btn { color: var(--c-game); }

/* ✅ SÌ — secondary text */
.subtitle { color: var(--text-sec); }

/* ✅ SÌ — display font */
.title { font-family: var(--f-display); }

/* ✅ SÌ — radius scale */
.card { border-radius: var(--r-lg); }

/* ✅ SÌ — helper TS */
.tag { background: var(--c-game); opacity: 0.12; }
/* meglio: usa entityColor('game', 0.12) inline */
```

## Audit del codebase

Prompt a Claude Code:

```
Audit del codebase per token violations:

1. Trova tutti gli hex codes hardcoded (regex /#[0-9a-fA-F]{3,8}\b/) in src/
   esclude: src/styles/tokens.css, src/lib/entity-color.ts, /*.test.* files.

2. Trova tutti i `font-family` non-token (cioè non `var(--f-*)`).

3. Trova border-radius non-token.

4. Trova hsl()/rgb() inline non da var.

Scrivi report in design_handoff/TOKEN_VIOLATIONS.md con conta per categoria
e file:line:snippet per ognuna. Non modificare nulla, solo audit.
```
