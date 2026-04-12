# Navigation System Redesign — Design Spec

**Date**: 2026-04-12  
**Status**: Draft — pending user review  
**Scope**: TopBar, HandRail, ActionPill, ActionBar, Hand Drawer, MeepleCard navItems  
**Target**: Figma design → implementation plan (separate step)

---

## 1. Vision: Hand-First Navigation

The navigation metaphor is a **card hand** — the same hand used in board games. When the user visits an entity (a game, a player, a session), child cards are automatically dealt into the hand. Tapping a card navigates to that view. There are no separate tabs, no ContextBar, no sidebar nav links.

**What is removed:**
- All nav links from TopBar
- ContextBar (entirely — no tabs in the shell navigation layer)
- FloatingActionPill (current implementation, hardcoded to 4 FabPage types)

**What replaces it:**
- HandRail (desktop) — left rail with entity-colored cards
- ActionPill (desktop) — floating bottom-center pill with breadcrumb + contextual CTA
- ActionBar (mobile) — sticky bottom bar with mini-hand + CTA
- Hand Drawer (mobile) — bottom sheet per entity type, opened from ActionBar

---

## 2. Entity System

### 2.1 Entity Types

| Entity | Color (HSL) | Icon |
|--------|-------------|------|
| game | hsl(25, 95%, 45%) | 🎲 |
| player | hsl(262, 83%, 58%) | 👤 |
| session | hsl(240, 60%, 55%) | 🎮 |
| agent | hsl(38, 92%, 50%) | 🤖 |
| kb | hsl(174, 60%, 40%) | 📚 |
| chat | hsl(220, 80%, 55%) | 💬 |
| event | hsl(350, 89%, 60%) | 📅 |
| toolkit | hsl(142, 70%, 45%) | 🧰 |
| tool | hsl(195, 80%, 50%) | 🔧 |

### 2.2 Card Auto-Population

When the user navigates to an entity page, the system automatically deals its child entity cards into the hand:

| Page visited | Cards dealt |
|-------------|-------------|
| Game detail | Sessioni (session), Documenti (kb), Toolkit (toolkit), Agente (agent) |
| Session detail | Game parent card (game), Timeline (session), Toolkit (toolkit) |
| Player detail | Sessioni (session), Statistiche (player) |
| Agent detail | Chat (chat), KB collegata (kb) |
| KB detail | Game collegato (game), Preview (kb) |

The page itself is added to hand as a card too (e.g., visiting `Catan` → `game:catan` card appears).

**Stato iniziale (mano vuota)**: Se la mano è vuota (primo accesso o dopo `clearHand()`), HandRail mostra un messaggio vuoto: "Naviga su una scheda per iniziare." Non esistono card di default pre-populate.

### 2.3 `HandCard` Interface

```ts
interface HandCard {
  id: string              // unique: "game:abc123"
  entityType: EntityType  // game | player | session | agent | kb | chat | event | toolkit | tool
  entityId: string
  label: string           // "Catan"
  sublabel?: string       // "2–6 giocatori"
  color: string           // HSL value from entity color system
  href: string            // navigation target
  pinned: boolean
  addedAt: number         // timestamp for FIFO
}
```

### 2.4 `useCardHand` Store (existing, extended)

- **Max cards**: 10 (FIFO eviction, pinned cards exempt)
- **Pinned cards**: persist to `localStorage` under key `meepleai:hand:pinned`
- **FIFO eviction**: oldest non-pinned card removed when max reached
- **Methods**: `drawCard(card)` | `discardCard(id)` | `pinCard(id)` | `unpinCard(id)` | `clearHand()`
- **Sections**: "Fissate" (pinned, shown first) | "Recenti" (FIFO, rest)

---

## 3. Desktop Layout

### 3.1 Layer Stack

```
┌─────────────────────────────────────────────────────┐
│  TopBar (sticky, 56px)                               │
├──────────┬──────────────────────────────────────────┤
│          │                                           │
│ HandRail │  Page Content                             │
│ (64px    │                                           │
│  →200px) │                                           │
│          │                                           │
│          │              [ActionPill floating]        │
└──────────┴──────────────────────────────────────────┘
```

### 3.2 TopBar

**Height**: 56px  
**Background**: `var(--bg)` = `hsl(40, 30%, 97%)` + `box-shadow: 0 1px 0 hsl(40,20%,88%)`

**Contents (left to right)**:
1. Logo mark `⬡` + wordmark "MeepleAI" (Quicksand 600, amber)
2. Global search input (expandable, 240px default → 400px on focus)
3. Chat icon button (💬 → `/chat`)
4. Notification bell (🔔 + badge)
5. Avatar / user menu

**No nav links.** The TopBar is a utility bar only.

### 3.3 HandRail

**Position**: Fixed left, below TopBar  
**Width**: 64px collapsed / 200px expanded on hover (CSS transition 200ms ease)  
**Background**: `hsl(220, 15%, 11%)` (dark)

**Structure**:

```
┌────────────────────────────────┐
│  ← toggle (64px: icon only)   │
├────────────────────────────────┤
│  📌 FISSATE                    │  (section label, hidden when 64px)
│  [card chip] Catan             │
│  [card chip] Marco             │
├────────────────────────────────┤
│  🕐 RECENTI                    │
│  [card chip] Sessione #12      │
│  [card chip] Rulebook.pdf      │
│  [card chip] Chat con Agente   │
└────────────────────────────────┘
```

**Card chip (64px)**: 40px colored square, entity icon centered, entity-colored left border (3px)  
**Card chip (200px)**: same square + label (Nunito 13px, truncated) + sublabel (11px, muted) + pin icon on hover  

**Interactions**:
- Click chip → navigate to `href`
- Long press / right-click → context menu (Apri, Fissa/Sfissa, Rimuovi dalla mano)
- Hover rail → expand to 200px (delay 150ms)
- Click `←` toggle → pin open / collapse

### 3.4 ActionPill

**Position**: Fixed, bottom-center, `bottom: 24px`, `z-index: 50`  
**Style**: glassmorphism pill — `backdrop-filter: blur(12px)`, `background: rgba(15,20,40,0.82)`, `border-radius: 999px`, `box-shadow: 0 8px 32px rgba(0,0,0,0.4)`

**Contents**:
```
[ ⬡ Libreria  ›  Catan  ›  Sessioni  ]  [▶ Nuova sessione]
   breadcrumb (left)                      CTA (right, amber)
```

**Breadcrumb**: max 3 segments, each segment is a link, separator `›`. Segments use entity colors if applicable (e.g., "Catan" in game-orange).

**CTA**: Contextual per page. Examples:
- Libreria → `+ Aggiungi gioco`
- Game detail → `▶ Nuova sessione`
- Session detail → `▶ Riprendi` or `✓ Concludi`
- Agent detail → `💬 Inizia chat`
- KB detail → `↑ Carica PDF`

**CTA is absent** if no primary action exists for the current page.

---

## 4. Mobile Layout

### 4.1 Layer Stack

```
┌─────────────────────────────────────┐
│  TopBar mobile (48px)               │
├─────────────────────────────────────┤
│                                     │
│  Page Content                       │
│                                     │
├─────────────────────────────────────┤
│  ActionBar (56px)                   │
└─────────────────────────────────────┘
        ↕ (swipe up or tap "+N ›")
┌─────────────────────────────────────┐
│  Hand Drawer (bottom sheet, 70vh)   │
└─────────────────────────────────────┘
```

### 4.2 TopBar (Mobile)

**Height**: 48px  
**Contents**:
1. Logo `⬡` + "MeepleAI" (compact)
2. Search icon (🔍) — tap expands full-width search overlay with **filter chips by entity type** (Giochi, Sessioni, Agenti, Docs, Chat)
3. Notification bell (🔔)
4. Avatar

Search overlay: full-width input floats over content, entity filter chips below it (scrollable row), results as list below chips. Tap outside or `✕` to close.

### 4.3 ActionBar

**Height**: 56px  
**Position**: Fixed bottom, safe-area aware (`padding-bottom: env(safe-area-inset-bottom)`)  
**Background**: `rgba(8,12,24,0.92)` + `backdrop-filter: blur(16px)` + `border-top: 1px solid rgba(255,255,255,0.08)`

**Contents**:
```
[ 🎲 [🎮][📚][🤖]  +2 ›  ]    [▶ Nuova sessione]
  mini-hand (left)  overflow   CTA (right, amber)
```

**Mini-hand**: shows up to 3 card chips (28×20px each, entity-colored, slightly overlapping like a fan). Each chip tap → navigates to that card's `href`.

**Overflow indicator**: `+N ›` badge (N = cards beyond 3). Tap → opens Hand Drawer.

**CTA**: Same contextual logic as desktop ActionPill.

**Swipe up on ActionBar**: opens Hand Drawer.

### 4.4 Hand Drawer

**Type**: Bottom sheet, 70vh, draggable  
**CSS component**: `.dr-overlay` + `.dr-panel` (from `admin-mockups/meeple-card-drawer-tabs-mockup.html`)  
**Handle**: `.dr-handle` — centered pill, drag to expand/close  

**Drawer sections**:
1. **Header** (`.dr-header`): entity icon + name + sublabel + close `✕`
2. **Tabs** (`.dr-tabs` > `.dr-tab`): 2–3 tabs per entity (see §5)
3. **Content** (`.dr-content`): tab-specific content
4. **Footer** (`.dr-footer`): contextual action buttons (`.af-btn`)

**Desktop vs Mobile**: The Hand Drawer è esclusivamente mobile. Su desktop, cliccare un chip nel HandRail naviga direttamente (`href`) senza aprire nessun drawer.

**Trigger**: Opened by:
- Tap `+N ›` overflow in ActionBar
- Swipe up on ActionBar
- Long-press on a card chip in ActionBar

**Tap a card chip (no overflow)**: Direct navigation — no drawer, just navigate.

---

## 4b. Action → Landing con Drawer Aperto

Quando l'utente esegue un'azione contestuale (da ActionPill, footer di un drawer, o navItem), la navigazione atterra sulla pagina dell'entità target con il **drawer già aperto sulla tab corretta**.

### Mapping azione → tab di atterraggio

| Azione | Pagina di destinazione | Drawer aperto su |
|--------|----------------------|------------------|
| `▶ Gioca` (da game) | `/games/:id/sessions/new` | Session drawer · tab **Live** |
| `🤖 Chiedi AI` (da game) | `/agents/:id/chat` | Agent drawer · tab **Overview** |
| `↑ Carica PDF` (da game/kb) | `/games/:id/kb` | KB drawer · tab **Overview** (upload pre-aperto) |
| `💬 Continua` (da chat) | `/chat/:id` | Chat drawer · tab **Messaggi** |
| `▶ Riprendi` (da session) | `/sessions/:id` | Session drawer · tab **Live** |
| `▶ Usa` (da toolkit) | `/sessions/new?toolkit=:id` | Toolkit drawer · tab **Template** |
| `💬 Chat` (da agent) | `/agents/:id/chat` | Agent drawer · tab **Overview** |
| `🔄 Reindex` (da kb) | stessa pagina | KB drawer · tab **Overview** (progress visibile) |

### Meccanismo di implementazione

La navigazione porta un **navigation state** (non URL param, non persistito) che il componente legge on mount:

```ts
// mittente (es. ActionPill CTA o drawer footer button)
router.push('/agents/abc/chat', {
  state: { drawerOpen: true, drawerTab: 'overview' }
});

// destinazione (es. AgentPage)
const location = useLocation();
const { drawerOpen, drawerTab } = location.state ?? {};
useEffect(() => {
  if (drawerOpen) openHandDrawer({ tab: drawerTab });
}, []);
```

Su **desktop**, la stessa logica apre una panel laterale o uno stato contestuale al posto del drawer mobile.

---

## 5. Drawer Specialization per Entity

Each entity type has a specialized drawer using the `E` data model from `admin-mockups/meeple-card-drawer-tabs-mockup.html`. The drawer shows the **currently active card** from the hand.

### 5.1 game

| Tab | Content |
|-----|---------|
| Info | Cover, publisher, year, player count, play time, rating |
| Statistiche | Play count, last played, top players, win rates |
| Storico | Chronological session list |

**Footer actions**: `▶ Gioca` (primary) · `🤖 Chiedi AI` (primary, only if agent exists) · `↗ Apri`

### 5.2 player

| Tab | Content |
|-----|---------|
| Profilo | Avatar, bio, preferred games |
| Statistiche | Win rate, sessions played, favorite game |
| Storico | Match history with dates and results |

**Footer actions**: `📊 Confronta` (secondary) · `↗ Apri`

### 5.3 session

| Tab | Content |
|-----|---------|
| Live | Current state, players, scores, elapsed time |
| Toolkit | Active tools and dice in session |
| Timeline | Chronological event log |

**Footer actions**: `▶ Riprendi` (primary, if `in_progress`) · `📊 Risultati` (secondary, if `complete`) · `↗ Apri`  
State is mutually exclusive: only one of `▶ Riprendi` or `📊 Risultati` is shown.

### 5.4 agent

| Tab | Content |
|-----|---------|
| Overview | Agent description, game linked, doc count |
| Storico | Recent conversations list |
| Config | Temperature, persona, retrieval mode |

**Footer actions**: `💬 Chat` (primary) · `🔄 Riavvia` (secondary) · `↗ Apri`

### 5.5 kb (document / knowledge base)

| Tab | Content |
|-----|---------|
| Overview | File name, size, status badge, indexed chunks count |
| Preview | First extracted text block |
| Citazioni | Sample citations generated from the document |

**Footer actions**: `🔄 Reindex` · `⬇ Download` · `↗ Apri`

### 5.6 chat

| Tab | Content |
|-----|---------|
| Messaggi | Last 3 messages preview |
| Fonti | Documents cited in the conversation |

**Footer actions**: `💬 Continua` (primary) · `📦 Archivia` (if open) · `🔓 Riapri` (if archived) · `↗ Apri`  
`📦 Archivia` and `🔓 Riapri` are mutually exclusive.

### 5.7 event

| Tab | Content |
|-----|---------|
| Overview | Event name, date, location, organizer |
| Programma | Session list / agenda |

**Footer actions**:
- If `pending`: `✅ Conferma` (primary) · `❌ Declina` · `📤 Invita` · `↗ Apri`
- If `confirmed`: `❌ Cancella partecipazione` · `📤 Invita` · `↗ Apri`
- If `declined`: `✅ Partecipa ora` (primary) · `📤 Invita` · `↗ Apri`

### 5.8 toolkit

| Tab | Content |
|-----|---------|
| Overview | Toolkit description, tool count, linked game |
| Template | Default tools included |
| Storico | Past sessions using this toolkit |

**Footer actions**: `▶ Usa` (primary) · `✏ Modifica` (secondary) · `↗ Apri`

### 5.9 tool

| Tab | Content |
|-----|---------|
| Dettaglio | Tool name, type (dice/timer/scoreboard/notes), description |
| Preview | Live preview of the tool UI |

**Footer actions**: `▶ Usa` (primary) · `✏ Modifica` (secondary) · `↗ Apri`

---

## 6. MeepleCard — Varianti, ManaPips e navItems

### 6.1 Varianti di visualizzazione

| Variant | Contesto | Dimensione |
|---------|----------|-----------|
| `grid` (default) | Dashboard hub, libreria, ricerca | 196px wide |
| `list` | Lista compatta multi-colonna | full-width row, 72px tall |
| `compact` | Risultati ricerca, suggestion inline | 160px wide, ridotto |
| `focus` | Pagina entità (header area) | full content-width, auto height |
| `featured` | Card in evidenza, primo slot dashboard | full-width hero |

### 6.2 ManaPips

**ManaPips** sono piccoli indicatori circolari colorati (8–10px) che compaiono sul bordo inferiore della card nelle varianti `grid` e `compact`. Indicano visivamente quali tipi di entità figlio sono collegati, senza testo.

```
┌──────────────────────┐
│  [cover image]       │
│  Catan               │
│  Klaus Teuber        │
│  ★ 7.1               │
│  ● ● ●  ·  +1        │  ← ManaPips row
└──────────────────────┘
   🎮  📚  🤖  (+toolkit)
```

**Regole di visualizzazione ManaPips**:
- Mostra max 3 pip; se ne esistono 4+, mostra 3 + `+N`
- Pip colorato (`opacity: 1`) = entità esiste e ha contenuto
- Pip grigio (`opacity: 0.25`) = entità non collegata (opzionale, solo se `showEmpty=true`)
- Pip con badge numerico: solo se `count > 0` (es. `3 sessioni`)

**Per variante**:
- `grid`: pip row da 8px con badge count piccolo
- `compact`: pip da 6px senza badge
- `list`: pip row inline dopo il rating
- `focus` / `featured`: pip sostituiti dai **navItems chip** completi (§6.3)

**Mapping entity → pip color**:
```ts
const PIP_COLORS: Record<EntityType, string> = {
  session:  'hsl(240,60%,55%)',   // blue
  kb:       'hsl(210,40%,55%)',   // light blue
  agent:    'hsl(38,92%,50%)',    // amber
  toolkit:  'hsl(142,70%,45%)',   // green
  chat:     'hsl(220,80%,55%)',   // blue accent
  player:   'hsl(262,83%,58%)',   // purple
  event:    'hsl(350,89%,60%)',   // red
  tool:     'hsl(195,80%,50%)',   // teal
  game:     'hsl(25,95%,45%)',    // orange
}
```

### 6.3 navItems (variante focus/featured)

Quando una `MeepleCard` è renderizzata in variante `focus` o `featured`, i ManaPips si espandono in navItem chip cliccabili. Questi chip **trigger `drawCard()`** sul hand store.

```ts
interface NavFooterItem {
  label: string         // "Sessioni"
  href: string          // "/games/abc123/sessions"
  entityType: EntityType
  count?: number        // 12
  icon?: string
}
```

**Esempio — Game MeepleCard (focus)**:
```
[🎮 Sessioni 12]  [📚 Docs 3]  [🧰 Toolkit]  [🤖 Agente]
```

Tap su un navItem chip:
1. Naviga a `href`
2. `drawCard({ entityType, entityId, label, ... })` → card aggiunta alla mano

---

## 6b. Dashboard Hub e MeepleCard in Focus

### Dashboard = MeepleCard Grid

La dashboard home (`/dashboard`) non usa più i componenti `.game-card` / `.hub-block` / `.game-thumb` del mockup `dashboard-new-user-mockup.html`. Questi vengono **rimpiazzati da MeepleCard** in variante `grid` o `compact`.

| Sezione dashboard | Componente precedente | Componente nuovo |
|-------------------|----------------------|------------------|
| Giochi recenti / libreria | `.game-card` (thumb + badge + titolo) | `MeepleCard variant="grid"` con ManaPips |
| Sessioni recenti | `.empty-cta` block | `MeepleCard variant="compact" entity="session"` |
| Agenti | `.empty-cta` block | `MeepleCard variant="compact" entity="agent"` |
| Toolkit carousel | `.tk-card` | `MeepleCard variant="compact" entity="toolkit"` |

**Empty state** (nuovo utente): usa il pattern `.empty-cta` del mockup originale — icona grande, titolo, sottotitolo, CTA button — invariato. Solo i card popolati usano MeepleCard.

**Filter chips**: i chip `Tutti / Recenti / ★ Preferiti` sopra la griglia rimangono e filtrano il tipo di MeepleCard mostrate.

### Entity Page = MeepleCard in Focus

Quando l'utente naviga a una pagina entità (es. `/games/:id`), il contenuto della pagina inizia con una `MeepleCard variant="focus"` che occupa la larghezza completa del content area.

```
┌─────────────────────────────────────────────┐
│  TopBar                                     │
├─────────────────────────────────────────────┤
│  ┌─────────────────────────────────────┐    │
│  │  MeepleCard variant="focus"         │    │  ← entity header
│  │  [cover hero]  titolo  rating       │    │
│  │  metadata chips                     │    │
│  │  [navItem chips — sessioni/docs/…]  │    │  ← espansi da ManaPips
│  └─────────────────────────────────────┘    │
│                                             │
│  entity-specific content below             │
│  (session list, doc list, etc.)             │
└─────────────────────────────────────────────┘
        ↕ (mobile: drawer auto-aperto sulla tab corretta)
```

**Su mobile**: arrivare su una pagina entità via action → MeepleCard focus visibile + Hand Drawer auto-aperto (§4b).

**Su desktop**: HandRail evidenzia il chip corrispondente; la card in focus è cliccabile per aprire un detail panel laterale (futuro scope) o è solo informativa.

---

## 7. Design Tokens

Consistent with `admin-mockups` style:

| Token | Value |
|-------|-------|
| `--bg` | `hsl(40, 30%, 97%)` |
| `--bg-dark` | `hsl(220, 15%, 11%)` |
| `--amber` | `hsl(25, 95%, 45%)` |
| `--font-heading` | Quicksand, 600 |
| `--font-body` | Nunito, 400 |
| `--font-nav` | Nunito, 500 |
| `--radius-pill` | `999px` |
| `--radius-card` | `16px` |
| `--shadow-float` | `0 8px 32px rgba(0,0,0,0.4)` |
| `--blur-glass` | `backdrop-filter: blur(12px)` |
| `--transition-rail` | `200ms ease` |

---

## 8. Routing & URL Structure

Navigation is URL-first. The hand reflects the current URL hierarchy.

| URL | Active card(s) auto-added |
|-----|--------------------------|
| `/library` | none (root) |
| `/games/:id` | `game:id` |
| `/games/:id/sessions` | `game:id`, `session:list` |
| `/games/:id/sessions/:sid` | `game:id`, `session:sid` |
| `/games/:id/kb` | `game:id`, `kb:list` |
| `/agents/:id` | `agent:id` |
| `/agents/:id/chat` | `agent:id`, `chat:new` |
| `/chat/:cid` | `chat:cid` |

The breadcrumb in ActionPill is derived from the URL segments, resolved to entity labels.

---

## 9. Accessibility

- HandRail: `role="navigation"`, `aria-label="Mano di navigazione"`, keyboard navigable (Tab + Enter)
- ActionPill: `role="navigation"`, breadcrumb uses `<nav aria-label="Breadcrumb">` with `<ol>`
- ActionBar: `role="toolbar"`, each chip `role="button"` with `aria-label="{label}"`
- Hand Drawer: `role="dialog"`, `aria-modal="true"`, focus trap, close on `Escape`
- Color contrast: text on entity-colored chips ≥ 4.5:1

---

## 10. Component Map

| Component | File (target) | Status |
|-----------|---------------|--------|
| `TopBar` (desktop) | `components/layout/UserShell/TopBar.tsx` | Modify: remove nav links |
| `TopBar` (mobile) | same file, responsive | Modify: add search overlay |
| `HandRail` | `components/layout/UserShell/DesktopHandRail.tsx` | Modify: add sections, expand labels |
| `ActionPill` | `components/layout/ActionPill.tsx` | Replace `FloatingActionPill.tsx` |
| `ActionBar` | `components/layout/mobile/ActionBar.tsx` | New |
| `HandDrawer` | `components/layout/mobile/HandDrawer.tsx` | New — uses `.dr-panel` CSS |
| `DrawerContent` | `components/layout/mobile/drawer/DrawerContent.tsx` | New — per-entity switch |
| `MeepleCard` navItems + ManaPips | `components/ui/data-display/meeple-card/MeepleCard.tsx` | Extend: add `manaPips` prop, `variant="focus"` |
| `ManaPips` sub-component | `components/ui/data-display/meeple-card/ManaPips.tsx` | New |
| Dashboard hub grid | `app/(user)/dashboard/page.tsx` | Modify: replace `.game-card` with `MeepleCard` |
| Entity focus card | `components/layout/EntityFocusCard.tsx` | New — wrapper for `MeepleCard variant="focus"` |
| `useCardHand` store | `stores/use-card-hand.ts` | Extend: sections, persist pins |
| `useNavBreadcrumb` | `hooks/useNavBreadcrumb.ts` | New |
| `useMiniNavConfig` | `hooks/useMiniNavConfig.ts` | **Deprecated** — remove all usages, replaced by `useNavBreadcrumb` |
| CSS: drawer | Copy from `admin-mockups/meeple-card-drawer-tabs-mockup.html` | Extract to `styles/drawer.css` |

---

## 11. Out of Scope

- Figma design files (separate deliverable, after this spec)
- Admin shell navigation (separate design system)
- Push notifications panel
- Keyboard shortcut palette
- PWA offline hand persistence
