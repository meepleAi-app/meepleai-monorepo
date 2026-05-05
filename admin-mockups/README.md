# Handoff: MeepleAI — Board Games + AI Companion App

## Overview

MeepleAI is a mobile-first (with desktop adaptations) companion app for board game
enthusiasts. The app lets users:

- **Track their game library** (owned, wishlist, played sessions, ratings)
- **Chat with AI "Agents"** that are experts on specific games (rules, strategy, disputes)
- **Run live play sessions** with real-time scoring, timers, and event logging
- **Browse and publish Toolkits** (reusable bundles of Agent + KB docs + tools for a game)
- **Connect knowledge bases (KB)** of rules PDFs, manuals, FAQs that power the Agents

The design's distinctive pattern is **entity-driven navigation**: nine canonical
entity types (Game, Player, Session, Agent, KB, Chat, Event, Toolkit, Tool), each
with a dedicated color, emoji, and "drawer" peek view. Every cross-reference between
entities is a tappable pip that opens a drawer without losing context.

## About the Design Files

The files in `design_files/` are **design references created in HTML** — prototypes
showing intended look and behavior. They are **not production code to copy
directly**.

Your task is to **recreate these HTML designs in the target codebase's environment**
using its established patterns and libraries. If no environment exists yet, the
recommended stack is:

- **Next.js 14+ (App Router)** or **Vite + React** for the web
- **Tailwind CSS** with the tokens in `tokens.css` ported to `tailwind.config.ts`
- **shadcn/ui** as a component baseline (button, input, dialog, tabs, scroll-area)
- **vaul** for bottom-sheet drawers (nailed gesture physics; don't reinvent)
- **Framer Motion** for the motion system that's documented here
- **React Query** + **Zustand** for state

For iOS/Android native, use SwiftUI / Jetpack Compose and re-derive tokens.

## Fidelity

**High-fidelity (hifi)**. The mockups are pixel-accurate:
- Final colors in HSL (see `tokens.css`)
- Final typography (Quicksand / Nunito / JetBrains Mono, all via Google Fonts)
- Final spacing, radius, shadow, motion tokens
- Final interaction behavior (drawer physics, connection pips, tab logic)

Treat them as a contract. Reproduce pixel-perfect; deviate only with intent.

---

## Files in this Handoff

All files live in `design_files/`:

| File | What it is |
|---|---|
| `tokens.css` | **Source of truth for all design tokens.** Colors (light + dark), typography, spacing, radius, shadows, motion, z-index. 202 lines, fully commented. **Port this first.** |
| `components.css` | Shared component styles (phone frame, nav, theme toggle, cards, buttons, entity chips). Demonstrates how tokens compose. |
| `data.js` | **Fake dataset** with cross-references between all 9 entities (games, players, sessions, agents, KBs, chats, events, toolkits, tools). Use this as the shape of your domain model — see "Data Model" section below. |
| `mobile-app.jsx` | React component implementing the full mobile app: 5-tab nav, 4 screen types (Home / Library / Search / Chat / Session), connection pips, drawer overlay, drag-to-close drawer. ~870 lines. |
| `00-hub.html` | Deliverable index — navigation hub between all five pages. |
| `01-screens.html` | 24 interactive mobile screens in phone frames. Click phones to focus, tap connection pips to open drawers. Covers every screen type. |
| `02-desktop-patterns.html` | Three desktop layout patterns explored side-by-side: split-view, sidebar-detail, tabs. |
| `03-drawer-variants.html` | Six drawer variants compared (bottom sheet, side panel, modal, inline, docked, full-screen). |
| `04-design-system.html` | Live design system doc: all tokens rendered with a component playground. |
| `05-dark-mode.html` | Light vs dark side-by-side across 7 surfaces + contrast tests. |

**Recommended reading order for the developer:**
1. This README (end to end).
2. `04-design-system.html` — see all tokens live.
3. `05-dark-mode.html` — understand the theming contract.
4. `01-screens.html` — see all screens.
5. `mobile-app.jsx` — understand the interaction model.
6. `data.js` — understand the domain model.
7. `tokens.css` — port values to your config.

---

## Design Tokens

**Port these values verbatim.** All colors in HSL to make alpha composition trivial
(`hsl(var(--c-game) / 0.12)` for a 12% tint).

### Entity palette (9 canonical colors)

| Token | Light (H S L) | Dark (H S L) | Emoji | Label | Usage |
|---|---|---|---|---|---|
| `--c-game` | `25 95% 45%` | `28 95% 58%` | 🎲 | Game | Games, library, covers |
| `--c-player` | `262 83% 58%` | `262 75% 70%` | 👤 | Player | Users, stats, wins |
| `--c-session` | `240 60% 55%` | `235 70% 70%` | 🎯 | Session | Live play sessions |
| `--c-agent` | `38 92% 50%` | `38 92% 62%` | 🤖 | Agent | AI bots, experts |
| `--c-kb` | `174 60% 40%` | `174 60% 55%` | 📄 | KB | Docs, knowledge base |
| `--c-chat` | `220 80% 55%` | `218 80% 68%` | 💬 | Chat | Conversations |
| `--c-event` | `350 89% 60%` | `350 85% 70%` | 🎉 | Event | Events, calendar |
| `--c-toolkit` | `142 70% 45%` | `142 60% 58%` | 🧰 | Toolkit | Bundles, published |
| `--c-tool` | `195 80% 50%` | `195 75% 62%` | 🔧 | Tool | Timer, counter, etc |

**Semantic aliases** (use these in code, don't bypass):
- `--c-success` → toolkit
- `--c-warning` → agent
- `--c-danger` → event
- `--c-info` → chat

### Surfaces (light / dark)

| Token | Light | Dark | Usage |
|---|---|---|---|
| `--bg` | `#f7f3ee` | `#14100a` | Base canvas |
| `--bg-card` | `#ffffff` | `#1e1710` | Card surface |
| `--bg-muted` | `#efe6d9` | `#241c13` | Subtle block, stat chip |
| `--bg-sunken` | `#ede2d0` | `#0f0c08` | Deep / inset (code blocks, inputs) |
| `--bg-hover` | `rgba(180,130,80,.06)` | `rgba(255,200,130,.05)` | Row hover |

**Warm neutrals, not grey.** The browns tint maintains the board-game / wood
identity. Do not substitute `#fff`/`#000` or grey palettes.

### Text

| Token | Light | Dark |
|---|---|---|
| `--text` | `#2b1f12` | `#f0e4d2` |
| `--text-sec` | `#5a4a38` | `#c8b896` |
| `--text-muted` | `#9a8870` | `#8a7860` |

### Borders

| Token | Light | Dark |
|---|---|---|
| `--border` | `rgba(180,130,80,.18)` | `rgba(224,180,110,.14)` |
| `--border-light` | `rgba(180,130,80,.1)` | `rgba(224,180,110,.08)` |
| `--border-strong` | `rgba(180,130,80,.32)` | `rgba(224,180,110,.28)` |

### Typography

- `--f-display`: `'Quicksand', system-ui, sans-serif` — UI, titles, brand, buttons (400/500/600/700)
- `--f-body`: `'Nunito', system-ui, sans-serif` — body copy, paragraphs (400/500/600/700/800)
- `--f-mono`: `'JetBrains Mono', ui-monospace, monospace` — labels, tokens, code (400/500/600)

**Type scale:**

| Token | Size | Line-height | Use |
|---|---|---|---|
| `--fs-xs` | 11px | 1.5 | Mono labels, kickers, meta |
| `--fs-sm` | 12px | 1.5 | Captions, secondary UI |
| `--fs-base` | 14px | 1.5 | Body default |
| `--fs-lg` | 17px | 1.5 | Lead paragraphs |
| `--fs-xl` | 20px | 1.35 | Subtitles |
| `--fs-2xl` | 24px | 1.25 | Card headings |
| `--fs-3xl` | 32px | 1.2 | Section titles |
| `--fs-4xl` | 40px | 1.05 | Display hero |

**Weights:**
- `--fw-reg: 400`, `--fw-med: 500`, `--fw-semi: 600`, `--fw-bold: 700`, `--fw-ext: 800`

### Spacing (4px grid)

| Token | Value |
|---|---|
| `--s-1` | 4px |
| `--s-2` | 8px |
| `--s-3` | 12px |
| `--s-4` | 16px |
| `--s-5` | 20px |
| `--s-6` | 24px |
| `--s-7` | 32px |
| `--s-8` | 40px |
| `--s-9` | 48px |
| `--s-10` | 64px |

### Radius

| Token | Value | Use |
|---|---|---|
| `--r-xs` | 4px | Tiny (sparkline dots) |
| `--r-sm` | 6px | Chips, badges |
| `--r-md` | 10px | Buttons, inputs |
| `--r-lg` | 14px | Cards small |
| `--r-xl` | 18px | Cards, modals |
| `--r-2xl` | 24px | Drawer, big containers |
| `--r-pill` | 9999px | Pills, entity pips, segment toggles |

### Shadows (warm-tinted light, pure black dark)

| Token | Light | Dark | Use |
|---|---|---|---|
| `--shadow-xs` | `0 1px 2px rgba(90,60,20,.06)` | `0 1px 2px rgba(0,0,0,.3)` | Hover subtle |
| `--shadow-sm` | `0 2px 8px rgba(90,60,20,.08)` | `0 2px 8px rgba(0,0,0,.4)` | Card resting |
| `--shadow-md` | `0 4px 16px rgba(90,60,20,.1)` | `0 4px 16px rgba(0,0,0,.5)` | Card hover |
| `--shadow-lg` | `0 12px 36px rgba(90,60,20,.14)` | `0 12px 36px rgba(0,0,0,.6)` | Floating, drawer |

### Motion

| Token | Duration | Use |
|---|---|---|
| `--dur-xs` | 120ms | Micro hover, toggle |
| `--dur-sm` | 180ms | Chip, button press |
| `--dur-md` | 280ms | Drawer open, card transitions |
| `--dur-lg` | 400ms | Bottom sheet with bounce |
| `--dur-xl` | 600ms | Page transitions |

Easings:
- `--ease-out`: `cubic-bezier(.16, 1, .3, 1)` — default for entries, UI
- `--ease-spring`: `cubic-bezier(.34, 1.56, .64, 1)` — drawer, bottom sheets (slight overshoot)
- `--ease-in-out`: `cubic-bezier(.65, 0, .35, 1)` — page transitions, symmetric

### Z-index scale

- `--z-sticky: 10`
- `--z-drawer: 40`
- `--z-modal: 50`
- `--z-toast: 70`

---

## Porting tokens to Tailwind

```ts
// tailwind.config.ts
import type { Config } from 'tailwindcss';

const config: Config = {
  darkMode: ['class', '[data-theme="dark"]'],
  theme: {
    extend: {
      colors: {
        // Entity palette — reference as `bg-game`, `text-session`, `border-chat/40`, etc.
        game:    'hsl(var(--c-game) / <alpha-value>)',
        player:  'hsl(var(--c-player) / <alpha-value>)',
        session: 'hsl(var(--c-session) / <alpha-value>)',
        agent:   'hsl(var(--c-agent) / <alpha-value>)',
        kb:      'hsl(var(--c-kb) / <alpha-value>)',
        chat:    'hsl(var(--c-chat) / <alpha-value>)',
        event:   'hsl(var(--c-event) / <alpha-value>)',
        toolkit: 'hsl(var(--c-toolkit) / <alpha-value>)',
        tool:    'hsl(var(--c-tool) / <alpha-value>)',
        // Surfaces
        bg: 'var(--bg)',
        card: 'var(--bg-card)',
        muted: 'var(--bg-muted)',
        sunken: 'var(--bg-sunken)',
        // Text
        ink: 'var(--text)',
        'ink-sec': 'var(--text-sec)',
        'ink-muted': 'var(--text-muted)',
        // Border
        border: 'var(--border)',
      },
      fontFamily: {
        display: ['Quicksand', 'system-ui', 'sans-serif'],
        body: ['Nunito', 'system-ui', 'sans-serif'],
        mono: ['JetBrains Mono', 'ui-monospace', 'monospace'],
      },
      borderRadius: {
        xs: '4px', sm: '6px', md: '10px', lg: '14px',
        xl: '18px', '2xl': '24px', pill: '9999px',
      },
      boxShadow: {
        xs: 'var(--shadow-xs)', sm: 'var(--shadow-sm)',
        md: 'var(--shadow-md)', lg: 'var(--shadow-lg)',
      },
      transitionTimingFunction: {
        out: 'cubic-bezier(.16, 1, .3, 1)',
        spring: 'cubic-bezier(.34, 1.56, .64, 1)',
      },
    },
  },
};
export default config;
```

Then import `tokens.css` in your root layout so the CSS variables are defined
globally. Switch themes by toggling `data-theme="dark"` on `<html>`.

---

## Data Model (from `data.js`)

All entities cross-reference by string IDs. This is the minimum shape you need;
your backend will likely add more fields.

```ts
type EntityType = 'game' | 'player' | 'session' | 'agent' | 'kb' | 'chat' | 'event' | 'toolkit' | 'tool';

interface Game {
  id: string;           // e.g. 'azul'
  title: string;
  designer: string;
  year: number;
  players: [min: number, max: number];
  duration: [min: number, max: number]; // minutes
  complexity: number;   // 1-5
  coverGradient: string; // CSS gradient for placeholder
  status: 'owned' | 'wishlist' | 'played' | 'top10';
  rating?: number;      // user's rating 0-10
  playedCount: number;
  winRate?: number;     // 0-1
  lastPlayed?: string;  // ISO date
  agentIds: string[];
  kbIds: string[];
  toolkitIds: string[];
}

interface Player {
  id: string;
  name: string;
  avatar: string;       // emoji or URL
  color: string;        // hue 0-360 for derived avatar bg
  stats: { played: number; wins: number; avgScore: number };
}

interface Session {
  id: string;
  gameId: string;
  status: 'in-corso' | 'in-pausa' | 'finita';
  startedAt: string;
  turn: number;
  totalTurns?: number;
  playerIds: string[];
  scores: Record<string, number>;
  eventIds: string[];   // events logged during this session
  chatIds: string[];    // chats during this session
  toolIds: string[];    // tools active in this session
}

interface Agent {
  id: string;
  name: string;         // e.g. 'Azul Rules Expert'
  gameId: string;       // game it's expert on
  model: string;
  kbIds: string[];
  status: 'attivo' | 'in-addestramento' | 'archiviato';
  publishedInToolkits: string[];
}

interface KB {
  id: string;
  title: string;        // filename e.g. 'azul-regole.pdf'
  gameId: string;
  type: 'pdf' | 'md' | 'web';
  pages?: number;
  indexedAt: string;
  usedByAgents: string[];
}

interface Chat {
  id: string;
  agentId: string;
  gameId: string;
  title: string;        // first user message truncated
  createdAt: string;
  sessionId?: string;   // if asked during a session
  messageCount: number;
}

interface Event {
  id: string;
  type: 'game-start' | 'turn-change' | 'score-update' | 'custom';
  sessionId: string;
  playerId?: string;
  text: string;
  at: string;
  data?: Record<string, unknown>;
}

interface Toolkit {
  id: string;
  name: string;         // 'Azul Toolkit v2'
  authorId: string;
  gameId: string;
  agentIds: string[];
  kbIds: string[];
  toolIds: string[];
  publishedAt: string;
  installCount: number;
}

interface Tool {
  id: string;
  name: string;         // 'Timer Turno'
  type: 'timer' | 'counter' | 'scoreboard' | 'random';
  config: Record<string, unknown>;
}
```

---

## Architecture

### Component hierarchy (the non-negotiable four)

Everything else composes from these:

1. **`<EntityChip entity={...} size="sm|md">`** — colored pill with emoji + label.
   Tappable → opens drawer. Used everywhere references appear.
2. **`<EntityPip entity={...}>`** — round avatar version of the chip. Used in
   "connection bars" to show related entities.
3. **`<Drawer>`** — universal bottom sheet on mobile, side panel on desktop.
   Opens to show an entity's detail with tabs. See `03-drawer-variants.html`
   for the six variants; the **tabbed bottom sheet** is the canonical one.
4. **`<BottomBar>`** — 5-tab mobile nav (Home, Cerca, Libreria, Chat, Profilo).
   Switches to a dedicated **Session Bar** when a session is active (pulsing
   session-color indicator, quick actions).

### Screens

Each screen is described in detail below. See `01-screens.html` for visual
reference.

#### Mobile screens (from `mobile-app.jsx`)

**Home Feed**
- **Purpose**: Dashboard landing. Resume active sessions, recent activity feed.
- **Layout (top to bottom)**:
  - Status bar (iOS/Android — 32px, time/battery)
  - Top bar (52px): logo mark + "MeepleAI" wordmark + search icon + notifications icon + avatar
  - Scrollable content:
    - "Sessioni in corso" section: horizontal scroll of session cards with a Play/Resume button (session color)
    - "Attività recente" section: activity feed, one row per event with entity-colored avatar, `<who>` bolded, `<action>` in muted, entity references as colored chips, relative time on right
  - Bottom bar (60px): 5 icons with labels

**Library**
- Grid or list of owned/wishlist games. Filter tabs at top: Tutti / In libreria / Top 10 / Wishlist / Nuovi.
- Each row: cover placeholder (44×56, gradient), title, meta chips (Game / In libreria / rating stars).
- Tap row → Game detail drawer.

**Search**
- Input at top, empty state shows "Recent" and "Trending".
- As user types, results cluster by entity type (Games, Agents, Players, KBs…) each section with a count badge.

**Chat**
- List of recent chats (Chat icon, chat title, preview of last message, agent chip, time).
- Tap → full-screen chat view with agent bubble on left (muted bg) and user on right (chat color solid, white text). Input bar at bottom with send button (round, chat color).

**Session (dedicated mode)**
- Replaces the bottom bar with a Session Bar (session color, pulsing dot).
- Top: large game cover with session name overlaid.
- Middle: scoreboard (one row per player, live-updating score, color-coded).
- Below: active tools (timer counting down, turn counter, etc) as swipeable cards.
- Bottom: Event log (scrollable, newest on top).
- FAB: "+ Evento" to log a custom event.

**Entity Detail Drawer**
- Bottom sheet (mobile) / side panel (desktop). Max 78% height on mobile.
- Handle at top (drag to close), icon+title+close button row.
- Tab bar below title: Info / Stats / Sessions / Chats / Docs / Toolkits (count badges).
- Content area for the selected tab, including a **connection bar**: horizontal
  scrollable strip of `<EntityPip>`s to related entities, tap to replace current
  drawer with that entity.

#### Desktop patterns (from `02-desktop-patterns.html`)

Three explored; **pick one** with the team:

1. **Split view**: sidebar (entity nav) | list column | detail column. Best for power users.
2. **Sidebar + detail**: collapsed sidebar, one content pane, detail as side-drawer when opening entities. Closest to mobile.
3. **Tabs**: tabbed detail at top, nothing in sidebar except user. Best for "focus mode" on one entity.

Recommendation: **sidebar + detail** for v1 (mobile parity), add split view as
a power-user setting later.

---

## Interactions & Behavior

### Drawer physics

Reference: `vaul` (https://vaul.emilkowal.ski/) for mobile; custom for desktop.

- **Open**: bottom sheet slides from 100% down to `max-height: 78%`. Duration
  `--dur-md` (280ms), easing `--ease-spring`.
- **Drag handle** at top is draggable. Dragging past 40% threshold closes;
  otherwise snaps back to open.
- **Swap**: tapping an `<EntityPip>` inside a drawer replaces its content
  (no close+open flash). Animate content with a 120ms cross-fade.
- **Stack**: drawer can be 1 level deep (replace-on-tap). Browser/OS back
  button = close drawer (not navigate).

### Connection pips

- When showing an entity, every reference to another entity is a tappable pip.
- Pips overlap horizontally by 8px (stacked avatar pattern) up to 4 visible,
  then "+N more" text.
- Each pip is `<EntityPip>`: 32px circle, entity color bg, emoji centered,
  2px white (or `--bg-card`) border.
- Tap → open that entity in the drawer.

### Theme toggle

- Floating button top-right, 🌗 icon + current theme label.
- Persists in `localStorage` under `mai-theme` ('light' | 'dark').
- On first load: read `prefers-color-scheme` if no stored preference.
- Toggles `data-theme` attribute on `<html>`. All colors re-derive via CSS vars.

### Session mode lock

- When a session is `in-corso`, the bottom bar transforms (session-tinted background,
  pulsing session-color dot) and the primary CTA becomes "Apri sessione".
- Tapping a different tab dims the session bar; it stays visible and tappable.
- Explicit "End session" is required to restore normal bottom bar.

### Keyboard navigation (desktop)

- `/` focuses search.
- `Esc` closes drawer.
- `←`/`→` cycle between peer entities in the same drawer tab (e.g. prev/next game).
- `G` then letter: go to tab (`G H` home, `G L` library, `G C` chats).

### Motion reference

All reusable motion tokens in the Motion table above. Apply via `transition`
utilities or Framer Motion `transition={{ duration, ease }}`.

---

## State Management

Suggested store shape (Zustand or Redux):

```ts
interface Store {
  // Navigation
  currentTab: 'home' | 'library' | 'search' | 'chat' | 'profile' | 'session';
  drawer: { type: EntityType; id: string; tab: string } | null;

  // Session mode
  activeSession: Session | null;

  // Data (cached or hydrated from backend)
  games: Record<string, Game>;
  players: Record<string, Player>;
  sessions: Record<string, Session>;
  agents: Record<string, Agent>;
  kbs: Record<string, KB>;
  chats: Record<string, Chat>;
  events: Record<string, Event>;
  toolkits: Record<string, Toolkit>;
  tools: Record<string, Tool>;

  // UI
  theme: 'light' | 'dark';

  // Actions
  openDrawer(type: EntityType, id: string, tab?: string): void;
  replaceDrawer(type: EntityType, id: string): void;
  closeDrawer(): void;
  setTab(tab: string): void;
  startSession(session: Session): void;
  endSession(): void;
  toggleTheme(): void;
}
```

Backend contract (REST or tRPC):

- `GET /api/games?status=owned|wishlist|...` → Game[]
- `GET /api/games/:id` → Game + embedded counts
- `GET /api/games/:id/sessions` → Session[]
- `GET /api/sessions/:id/events` → Event[]
- `POST /api/sessions/:id/events` → Event
- `POST /api/chats` (start new chat with an agent)
- `POST /api/chats/:id/messages` → Message (with streaming for agent replies)
- `GET /api/toolkits?gameId=...` → Toolkit[]

For AI chat: use **Anthropic's streaming API** directly from a backend route.
Don't expose API keys in the client.

---

## Implementation Roadmap

**Phase 1 — Foundation (1-2 days)**
1. Scaffold project (Next.js + Tailwind + shadcn/ui).
2. Copy `tokens.css` into `app/globals.css` (or import at root).
3. Port tokens to `tailwind.config.ts`.
4. Load Google Fonts (Quicksand, Nunito, JetBrains Mono).
5. Implement theme toggle + `data-theme` persistence.
6. Verify with `04-design-system.html` side-by-side.

**Phase 2 — Core components (2-3 days)**
1. `<EntityChip>`, `<EntityPip>`, `<ConnectionBar>`.
2. `<Drawer>` (using `vaul` on mobile, Radix Dialog + custom on desktop).
3. `<BottomBar>` with session-mode variant.
4. `<Card>`, `<Button>`, `<Input>` tuned to tokens.
5. Verify with `04-design-system.html` component playground.

**Phase 3 — Screens (1-2 weeks)**
1. Home Feed.
2. Library (list + detail drawer).
3. Search.
4. Chat list + Chat detail.
5. Session Mode (full screen takeover).
6. Verify each screen against `01-screens.html`.

**Phase 4 — Backend integration (parallel to Phase 3)**
1. Auth (Clerk or Supabase).
2. Data schemas in Postgres / Prisma matching the Data Model section.
3. API routes per the contract above.
4. AI chat endpoint with Anthropic streaming.

**Phase 5 — Desktop adaptations (3-5 days)**
1. Responsive sidebar layout (collapse-to-icons under 1024px).
2. Drawer → side panel on desktop (≥ 1024px).
3. Keyboard navigation.
4. Verify with `02-desktop-patterns.html`.

**Phase 6 — Polish**
1. Skeleton loading states for every async surface.
2. Empty states with illustrations (use placeholders until a brand illustrator
   is briefed — do not invent emoji-heavy empty states).
3. Error states (inline banners, not modals).
4. Accessibility: focus rings use `hsl(var(--e) / .4)` per entity context;
   verify contrast on entity-bg white text (all pass AA at 4.5:1).

---

## Assets

**No bundled imagery.** All game covers in the design are CSS gradient placeholders
— intentional. Real art requires rights/licensing (BoardGameGeek API is a common
source). Keep the gradient pattern as a fallback for unloaded/missing images.

**Emoji**: used sparingly as entity markers (🎲 🎯 🤖 etc). For production,
consider a custom icon set (Lucide works well with this aesthetic) for UI icons
while keeping emoji only for entity avatars where personality matters.

**Fonts**: Quicksand, Nunito, JetBrains Mono — all free on Google Fonts. For
native apps, bundle the TTF/OTF files with the same weights listed above.

---

## Notes

- **Do not introduce a 4th font family.** If you need more variety, use weight
  and letter-spacing — the three families already cover UI, body, and technical.
- **Do not introduce grey neutrals.** The warm browns are intentional and
  define the brand. Greys on top will look washed out.
- **Do not add iconography to fill empty space.** Icon slop is an anti-pattern
  here; the emoji-per-entity is the restraint.
- **Dark mode is not optional.** It's already solved in tokens — wire it up on
  day one or it will accrue debt.
- **Connection pips are the feature.** Don't cut them in v1 to save time — they're
  what makes the app feel alive. If you cut anything, cut Toolkits or the Session
  event log first.

Good luck. If you get stuck on the drawer physics or connection pip animation,
open `01-screens.html` in a browser and poke the prototypes directly —
they're the source of truth for behavior.
