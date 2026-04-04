# MeepleAI User Pages Redesign — Frontend UX Spec

**Status**: Draft
**Date**: 2026-03-28
**Approach**: B — Redesign dei Flussi (mobile-first, Premium Gaming)
**Companion Spec**: Backend AI (RAG pipeline, qualità risposte) — TBD separately

## Context

MeepleAI has 150+ frontend components and 300+ backend endpoints, but the user-facing pages suffer from:

- Runtime errors and broken API calls
- Unsatisfying visual design, not optimized for mobile
- Disconnected user flows — individual pages work but journeys break
- AI chat that fails to respond or gives unsatisfying answers
- Overly complex agent setup process

This spec redesigns the **4 core user flows** as coherent end-to-end mobile-first experiences with a unified "Premium Gaming" design system. Old pages are **replaced**, not kept alongside.

## Target

- **Device**: Mobile-first (all pages), responsive to tablet/desktop
- **Alpha scope**: Auth → Games+BGG → PDF upload → RAG Chat → Library → Game Night
- **Players**: 2-8+ per session (scalable UI)

---

## 1. Design System — "Premium Gaming"

### Colors

| Token | Value | Usage |
|-------|-------|-------|
| `--bg-base` | `#0f0a1a` | Page background |
| `--bg-elevated` | `#1a1333` | Card backgrounds |
| `--bg-glass` | `rgba(255,255,255,0.05)` | Glass card fill |
| `--border-glass` | `rgba(255,255,255,0.1)` | Glass card border |
| `--accent-primary` | `#f59e0b → #ef4444` gradient | CTA buttons, brand |
| `--accent-ai` | `#8b5cf6` | AI/agent elements |
| `--accent-success` | `#22c55e` | Success states |
| `--accent-info` | `#3b82f6` | Info states |
| `--text-primary` | `#f8fafc` | Primary text |
| `--text-secondary` | `#94a3b8` | Secondary text |
| `--text-accent` | `#fbbf24` | Highlighted labels |

### Surfaces

- **GlassCard**: `background: var(--bg-glass)`, `border: 1px solid var(--border-glass)`, `backdrop-filter: blur(12px)`, `border-radius: 12px`
- **GradientButton**: `background: linear-gradient(90deg, #f59e0b, #ef4444)`, `border-radius: 8px`, `font-weight: 600`
- **Bottom Sheet**: `background: var(--bg-elevated)`, `border-radius: 16px 16px 0 0`, slide-up animation

### Typography

- Font: System font stack (`-apple-system, BlinkMacSystemFont, 'Segoe UI', ...`)
- Weights: 400 (body), 500 (labels), 600 (headings), 700 (display)
- Scale: Tailwind default (text-xs through text-3xl)

### Spacing & Radius

- Spacing: 4px base (Tailwind standard)
- Border radius: 12px cards, 8px buttons/inputs, 16px modals/sheets, full for avatars

### Mobile-First Patterns

- **Bottom sheets** for actions (not centered modals)
- **Swipe gestures** for tab navigation
- **Pull-to-refresh** for lists
- **FAB** (Floating Action Button) for primary action per page
- **No sidebar on mobile** — bottom nav only
- **Sticky headers** that compress on scroll
- **Numpad input** for scores (one-hand friendly)

### Component Updates

| Component | Action |
|-----------|--------|
| `MeepleCard` | Reskin to Premium Gaming (glass bg, luminous borders). **Keep ManaPips, entity links, all variants (grid/list/compact/featured/hero)**. Entity color accents remain (game=orange, player=purple, collection=teal, event=rose) |
| `GlassCard` | **New** — base container with glassmorphism |
| `GradientButton` | **New** — primary CTA with amber→red gradient |
| `MobileBottomNav` | **New** — 5-icon fixed bottom bar |
| `MobileHeader` | **New** — compact header with title + context actions |
| `BottomSheet` | Existing `session/BottomSheet.tsx` — restyle and promote to shared component |
| `SessionBottomNav` | **New** — 4-tab nav replacing app nav during live session |

---

## 2. Flusso 1 — Discovery & Add Game

**Journey**: User opens app → searches for a game → finds it → adds to library

### 2a. Dashboard (`/dashboard`)

Replaces current gaming hub dashboard.

**Layout** (vertical scroll, single column):

1. **Search bar** — prominent, top of page. Placeholder: "Cerca un gioco...". Tap opens full-screen search.
2. **Recent games** — horizontal scrollable row of MeepleCard (compact variant). Hidden if library is empty.
3. **Quick actions** — 2-3 large GlassCards:
   - "Nuova Serata" → `/sessions/new`
   - "Chat AI" → `/chat`
   - "Esplora Catalogo" → triggers search
4. **AI suggestions** — "Potresti provare..." based on library (gracefully hidden if AI unavailable)

**Empty state** (new user): Welcome message + single CTA "Cerca il tuo primo gioco"

### 2b. Game Search (Full Screen)

Replaces `BggSearchPanel` + `AddGameSheet` wizard.

- Tap search bar → **full screen overlay** with keyboard auto-opened
- Search queries BGG API with debounce
- Results: list of MeepleCard (list variant) — cover, name, year, rating
- Tap result → **GamePreviewSheet** (large bottom sheet):
  - Cover image + name + base info (players, duration, complexity as ManaPips)
  - **"Aggiungi alla Libreria"** — GradientButton, primary action
  - "Aggiungi alla Wishlist" — secondary text button
- After add → snackbar confirmation with "Vai al gioco" link
- **Zero wizard steps** — add is a direct action. Advanced config (PDF, agent) happens later in game detail.

### 2c. Post-Add

- User returns to search (can add more games)
- Or navigates to game detail for PDF/AI setup (Flow 2)

**Components reused**: `MeepleCard`, `BggSearchPanel` (restyled), `AddToLibraryButton`
**Components new**: `FullScreenSearch`, `GamePreviewSheet`
**Components removed**: `AddGameSheet` multi-step wizard → replaced by direct add flow

---

## 3. Flusso 2 — Library & Game Detail

**Journey**: User views their games → opens a game → uploads rules PDF → AI auto-configures

### 3a. Library Page (`/library`)

Replaces current tab-based library with 65+ components.

**Layout**:

1. **Header**: "La Mia Libreria" + game count + filter icon
2. **Segmented control**: Collezione | Privati | Wishlist — swipeable between segments
3. **Search bar**: Sticky below segmented control
4. **Game grid**: MeepleCard in 2-column grid (mobile), 3-4 columns (tablet/desktop)
5. **Filters**: Bottom sheet triggered by filter icon — mechanics, player count, complexity
6. **FAB**: "+" bottom-right → opens game search
7. **Pull-to-refresh** support

**Empty state**: Illustration + CTA "Cerca il tuo primo gioco" linking to BGG search

### 3b. Game Detail (`/library/games/[gameId]`)

Replaces Game Table layout with zones. New: vertical scroll, collapsible sections.

**Layout** (top to bottom):

1. **Hero section**: Large cover + name + rating + ManaPips (players, duration, complexity)
2. **Action bar**: Icon row — Favorite, Chat AI, Rules, Share
3. **"Regole & AI" section** (most important):
   - No PDF uploaded: Prominent CTA **"Carica Regolamento"** → upload sheet (camera or file picker)
   - PDF uploaded, processing: Inline progress bar + status text
   - PDF processed: "AI Pronta" badge + **"Chiedi alle Regole"** button → opens chat
   - **Agent auto-creates on first PDF upload — zero manual configuration**
4. **"Le Tue Sessioni" section**: Recent play sessions with this game
5. **"Info" section**: Mechanics, description, BGG link
6. **"Nella Community" section**: FAQs, strategies, reviews (collapsed by default)

### 3c. PDF Upload (Bottom Sheet)

Replaces `PdfUploadModal` + `PdfProcessingStatus` + `PdfVersionManager`.

- 2 options: **Camera** (take photo of rulebook — creates PDF from image, requires backend OCR support) | **File** (choose PDF)
- Camera option: if backend OCR not ready, hide this option and show only File picker. The UI supports both but degrades gracefully.
- Progress bar during upload
- Status displayed inline in game detail (not separate modal)
- Push notification when processing completes
- **No version management UI** — user uploads, versioning is internal

### 3d. Agent Auto-Setup

Replaces `/library/games/[gameId]/agent` with `AgentConfigModal`, `AgentConfigForm`.

- Agent auto-created when PDF finishes processing
- Default type: "Rules Expert" for that game
- No configuration page for regular users
- Advanced users: "Impostazioni Agente" hidden in "..." menu on game detail
- **Flow becomes**: Upload PDF → Processing → "AI Pronta, chiedi qualcosa!" → Chat

**Components reused**: `MeepleCard`, `FavoriteToggle`, `PdfProcessingStatus` (restyled), `KbStatusBadge`
**Components new**: `GameDetailMobile` (vertical layout), `PdfUploadSheet`, `AiReadyBadge`
**Components simplified**: Game Table zones → linear scroll, AgentConfig → hidden auto-setup

---

## 4. Flusso 3 — Chat AI & Rules

**Journey**: User has a game with processed PDF → asks AI → gets answers with citations

### 4a. Entry Points

1. **From Game Detail**: "Chiedi alle Regole" button → opens chat pre-linked to that game
2. **From Bottom Nav**: Chat tab → conversation list + "Nuova chat"

In both cases, agent is already configured — user chooses nothing.

### 4b. Chat Page (`/chat/[sessionId]`)

Replaces current chat with debug cards and complex UI.

**Layout**:

1. **Header**: Game icon + game name + "..." for options
2. **Message area**: Full screen scroll
   - **User messages**: Right-aligned, dark glass bubble
   - **AI messages**: Left-aligned, glass bubble with amber border
   - **Inline citations**: When AI cites a rule, clickable chip below message → CitationSheet (bottom sheet with PDF page + highlighted text)
   - **Typing indicator**: Meeple "thinking" animation (3-dot with meeple icon)
3. **Input area**: Fixed at bottom
   - Text field with contextual placeholder: "Chiedi qualcosa su {game name}..."
   - **Quick prompt chips**: Horizontally scrollable above keyboard:
     - "Come si prepara il gioco?"
     - "Quanti giocatori servono?"
     - "Spiega il turno di gioco"
     - "Cosa succede in caso di pareggio?"
   - Quick prompts change per game (generated from PDF content if possible, generic templates otherwise)

### 4c. Conversation List (`/chat`)

Replaces `ChatConversationList` grouped by agent.

- Simple list grouped by **game** (not agent — users think in terms of games)
- Each item: game cover thumbnail + last message + timestamp
- Swipe left to delete conversation
- FAB "+" → game selection → new chat
- **Empty state**: "Nessuna conversazione. Vai alla libreria e chiedi qualcosa sulle regole di un gioco!"

### 4d. Citation Sheet (Bottom Sheet)

Replaces `CitationBadge`, `RuleSourceCard`, `PdfPageModal`.

- Rendered PDF page with highlight on cited section
- Citation text in plain above
- "Vedi pagina completa" button to expand
- Swipe down to close

### 4e. AI Error Handling

- **Loading**: "Sto cercando nelle regole..." with animated progress
- **Timeout (30s)**: "Non riesco a trovare una risposta. Prova a riformulare la domanda" + "Riprova" button
- **No RAG results**: "Non ho trovato questa informazione nel regolamento. Vuoi che provi a rispondere in base alla mia conoscenza generale?" (clear disclaimer)
- **No PDF**: "Carica il regolamento per risposte più precise" + upload CTA

**Components reused**: `ChatMessageList` (restyled), `ChatInputArea` (restyled), `CitationBadge`
**Components new**: `QuickPromptChips`, `CitationSheet`, `AiLoadingState`, `ChatEmptyState`
**Components removed**: `DebugStepCard`, `DebugSummaryBar` (admin/dev only), `AgentCreationWizard` (auto-setup), `AgentSwitchDialog` (one agent per game)

---

## 5. Flusso 4 — Game Night

**Journey**: User organizes a game night → invites friends → plays with AI assistance → tracks scores

### 5a. Create Session (`/sessions/new`)

Replaces both `/sessions/new` and `/game-nights/new`. Single unified flow.

**3-step swipeable wizard**:

**Step 1 — Choose Game(s)**:
- Grid of library games (MeepleCard compact)
- Search to filter
- Multi-game selection support (multi-game night)
- Empty library: "Aggiungi un gioco prima" → link to search

**Step 2 — Invite Players**:
- Recent players at top (quick add)
- "Aggiungi giocatore" → name + color (auto-generated avatar)
- **QR Code** large center: friends scan to join as guest viewer (see scores, rules — no account required)
- Shareable link alternative (WhatsApp, Telegram)

**Step 3 — Ready!**:
- Summary: game(s) + players + rules available (yes/no)
- Large button: **"Inizia a Giocare"** (GradientButton full width)

Progress indicator: 3 dots at top.

### 5b. Play Mode (`/sessions/live/[sessionId]`)

Replaces 5+ separate sub-pages. Single page with contextual bottom tab bar.

**Session bottom nav** (replaces app bottom nav during session):

| Icon | Label | Content |
|------|-------|---------|
| 🎲 | Gioco | Main play tab (default) |
| 📊 | Punteggi | Live scoreboard |
| 💬 | Chiedi | AI chat in session context |
| 👥 | Giocatori | Player list and management |

#### Tab: Gioco (default)
- **Game timer** at top (total time, optional per-turn)
- **Quick Tools bar**: Horizontal scrollable icons
  - 🎲 Dice (configurable: D6, D8, D10, D20)
  - 🪙 Coin flip
  - ⏱️ Turn timer
  - 🔢 Counter
  - 🃏 Card deck
- Tap tool → **bottom sheet** with interactive tool (dice with simple 3D animation, shake-to-roll on mobile)
- **Activity feed**: Event log ("Marco rolled 2D6: 8", "Sara +3 points")
- **FAB "Chiedi Regola"**: Quick shortcut to AI chat with session context

#### Tab: Punteggi
- **Live scoreboard**: Table with players (colors) × score
- Tap score → **numpad** for quick input/update (one-hand friendly)
- **Mini chart** of score progression (optional, collapsible)
- Support for scoring types: single points, categories, round-based

#### Tab: Chiedi (AI Chat in-session)
- Same UI as Flow 3 chat but with session context
- Contextual quick prompts: "È il turno di chi?", "Cosa succede se...", "Regola per {fase corrente}"
- Agent has access to game state (scores, current turn) for better answers
- **Voice input**: Microphone button for hands-free at the table — v2

#### Tab: Giocatori
- Player list with color, avatar, current score
- Current turn indicator
- "Aggiungi giocatore tardivo" (late joiner)
- Guest viewer status (who scanned QR)

### 5c. Guest View (`/join/[code]` — public, no auth)

- Read-only view for QR scanners:
  - Live scoreboard
  - Quick game rules
  - Activity feed
- **No registration required** — friction-free experience

### 5d. End of Session

- "Termina Partita" button (with confirmation dialog)
- **Final summary**:
  - Ranking with 🥇🥈🥉
  - Stats: duration, turns, dice rolled
  - Session photos (if taken)
  - **"Salva e Condividi"**: Generate shareable image with ranking
  - **"Gioca Ancora"**: Recreate session with same players + new game

### 5e. Multi-Game Night

- If session has multiple games:
  - Complete one game → "Prossimo gioco" → switch to next game
  - Separate scores per game
  - Aggregate final ranking (who won the most)

**Components reused**: `DiceRoller` (restyled), `CounterTool`, `CoinFlip`, `CardDeck`, `CountdownTimer`, `LiveScoreboard` (restyled), `PlayerList`, `ActivityFeed`, `RulesExplainer`
**Components new**: `SessionBottomNav`, `QuickToolBar`, `ScoreNumpad`, `QrInviteSheet`, `SessionSummaryCard`, `GuestSessionView`, `MultiGameSwitcher`
**Components removed**: `/game-nights/*` pages, separate session sub-pages (photos, players, scores), `ArbitroModal` (integrated into AI chat)

---

## 6. Navigation & Architecture

### 6a. Mobile Bottom Navigation

Fixed bottom bar, 5 icons:

| Icon | Label | Route | Function |
|------|-------|-------|----------|
| 🏠 | Home | `/dashboard` | Dashboard with search and quick actions |
| 📚 | Libreria | `/library` | Your games |
| ➕ | — | — | Central FAB: context menu "Nuova Serata" / "Aggiungi Gioco" |
| 💬 | Chat | `/chat` | AI conversations |
| 👤 | Profilo | `/profile` | Settings, achievements, stats |

During live session: bottom nav becomes **session nav** (4 tabs: Gioco, Punteggi, Chiedi, Giocatori).

On desktop/tablet: sidebar (existing pattern) with same items + expansions.

### 6b. Route Structure (Replacement)

New routes replace old ones. Old pages are deleted, not hidden.

```
(authenticated)/
├── dashboard/              → Mobile-first home (replaces old dashboard)
├── library/
│   ├── page.tsx            → Game grid with segmented control (replaces tab-based)
│   ├── private/add/        → Add private game (kept)
│   └── games/[gameId]/     → Vertical game detail (replaces Game Table layout)
├── chat/
│   ├── page.tsx            → Conversations by game (replaces agent grouping)
│   └── [sessionId]/        → Single chat (restyled)
├── sessions/
│   ├── new/                → 3-step wizard (replaces sessions/new AND game-nights/new)
│   └── live/[sessionId]/   → Single-page play mode (replaces 5 sub-pages)
├── profile/                → Profile and settings
└── join/[code]             → Guest view (public, no auth)
```

**Routes deleted** (code removed):

| Removed Route | Replaced By |
|---------------|-------------|
| `/game-nights/*` (4 pages) | `/sessions/new` + `/sessions/live/[id]` |
| `/sessions/[id]/play,notes,players,scoreboard` | Tabs in `/sessions/live/[id]` |
| `/sessions/live/[id]/agent,photos,players,scores` | Tabs in `/sessions/live/[id]` |
| `/sessions/[id]/join`, `/sessions/join` | `/join/[code]` (public) |
| `/library/games/[gameId]/agent` | Auto-setup, config hidden in game detail |
| `/library/games/[gameId]/toolbox,toolkit` | Tools integrated in play mode |
| `/agents/*` (3 pages) | v2 — removed from nav |
| `/knowledge-base/*` (2 pages) | Upload from game detail |
| `/toolkit/*` (4 pages) | Tools integrated in play mode |
| `/games/[id]/rules,reviews,faqs,strategies,sessions` | Sections inside game detail |
| `/play-records/*` (5 pages) | v2 |
| `/editor/*` (4 pages) | v2 |
| `/pipeline-builder` | Removed |
| `/library/playlists/*` | v2 |
| `/library/proposals`, `/library/propose` | v2 |
| `/library/wishlist` | Segmented control in `/library` |

**Principle**: One route = one function. Zero zombie pages.

### 6c. State Management

| Concern | Approach |
|---------|----------|
| Live session | WebSocket via `GET /game-sessions/{id}/stream/v2` for real-time scores, events, player status |
| Chat AI | SSE streaming for AI responses (existing backend) |
| Library | React Query with aggressive cache (games change infrequently) |
| Session wizard | Zustand local store for 3-step wizard state |
| Theme | CSS custom properties for Premium Gaming tokens, future dark/light toggle |

### 6d. Implementation Order

| Phase | Flow | Depends On | Scope |
|-------|------|------------|-------|
| **1** | Design System | — | Tokens, GlassCard, GradientButton, MobileBottomNav, MeepleCard reskin |
| **2** | Discovery & Add Game | Phase 1 | Dashboard, search, add game |
| **3** | Library & Game Detail | Phase 1, 2 | Library page, game detail, PDF upload |
| **4** | Chat AI | Phase 1, 3 | Chat UI, citations, quick prompts, error handling |
| **5** | Game Night | Phase 1-4 | Session wizard, play mode, scoreboard, tools, guest view |

Each phase: plan → implement → test → PR. Independent spec → plan → implementation cycle.

### 6e. Out of Scope

| Item | Reason |
|------|--------|
| Backend AI (RAG pipeline, response quality) | Separate spec |
| Admin pages | Already consolidated, not touched |
| Onboarding/auth flow | Working, not priority |
| Desktop-specific layouts | Mobile-first, desktop comes free from responsive |
| Knowledge base standalone pages | Simplified to upload from game detail |
| Toolkit/Toolbox standalone pages | Tools integrated in play mode |
| Play records / statistics | v2 |
| Agent catalog / editor | v2 (agents are auto-created) |
| Playlists, proposals | v2 |
| Speech-to-text in chat | v2 |

---

## Summary

This spec redesigns MeepleAI's user-facing pages as 4 coherent mobile-first flows with a Premium Gaming visual identity. It replaces 40+ existing routes with ~10 focused ones, reuses existing components where they work, and creates new ones where flows are broken. The backend API surface (300+ endpoints) is already sufficient — this is purely a frontend UX overhaul.
