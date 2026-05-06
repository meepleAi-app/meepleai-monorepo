# MeepleAI — Project Brief for AI Layout Generation

> **Purpose**: This document describes MeepleAI's product, design language, and page structure for use with AI layout generators (v0, Figma AI, Bolt, Lovable, etc.)
> **Last Updated**: 2026-04-03

---

## 1. Product Overview

**MeepleAI** is a web application that acts as an AI-powered board game assistant. It helps players:
- Manage their personal **game library** and wishlist
- Access a community **shared game catalog**
- Track **game sessions** with scores and player stats
- Upload and query **rulebook PDFs** via RAG (retrieval-augmented generation)
- Chat with **AI agents** for rules clarification and game strategy during play
- Organize **game nights** with friends

**Target users**: Board game enthusiasts (casual to hardcore), from teens to adults.

**Tone**: Playful, smart, community-oriented. Think Discord meets a tabletop gaming companion.

---

## 2. Visual Identity

### 2.1 Brand Mascot / Logo

A **meeple** (classic Carcassonne-style board game pawn silhouette) with:
- **Eyes**: Two horizontal dash lines `— —` (zen, knowing expression)
- **Crown**: Small, golden, tilted
- **Glow**: Glowing edges + sparkle effects
- **Frame**: Hexagonal (like a board game tile)
- **Background**: Dark `#0f172a`
- **Style**: Flat vector, Discord-style mascot. Playful + tech-smart.

The meeple acts as the **"A"** in **Meeple**A**i** conceptually.

### 2.2 Color System

**Primary brand color**: Warm orange `hsl(25, 95%, 38%)` — used for CTAs, primary buttons, interactive accents.

**Entity colors** (each content type has its own identity color):

| Entity | Color | HSL |
|--------|-------|-----|
| Game | Orange | `hsl(25, 95%, 45%)` |
| Player | Purple | `hsl(262, 83%, 58%)` |
| Session | Indigo | `hsl(240, 60%, 55%)` |
| AI Agent | Amber | `hsl(38, 92%, 50%)` |
| Document | Slate | `hsl(210, 40%, 55%)` |
| Chat | Blue | `hsl(220, 80%, 55%)` |
| Event | Rose | `hsl(350, 89%, 60%)` |

**Backgrounds**:
- Dark mode base: `#0f172a` (slate-950)
- Cards/surfaces: warm dark tones with subtle warm shadows

**Shadows**: Warm brown-toned (not neutral gray), giving an organic, premium feel:
- `rgba(180, 130, 80, 0.08–0.20)` in light mode
- Pure black opacity in dark mode

### 2.3 Typography

| Role | Font | Weight |
|------|------|--------|
| Headings / Card titles | **Quicksand** | 500–700 |
| Body / Nav / UI | **Nunito** | 400–800 |

- Entity badges: Quicksand Bold, 10px, UPPERCASE, wide tracking
- Metadata labels: Nunito SemiBold, 12px
- AI chat messages: Nunito Regular, 14–16px

### 2.4 Dark Mode

The application is **dark-mode first**. Light mode exists but dark is the primary experience.

---

## 3. Layout System

### 3.1 Three-Tier Navigation

**Mobile**:
```
┌─────────────────────────────────────────────┐
│  TopNavbar (sticky, 52px)                   │  Logo + hamburger + search + avatar
├─────────────────────────────────────────────┤
│  MiniNav (optional, 40px)                   │  Contextual tabs
├─────────────────────────────────────────────┤
│                                             │
│  Page Content                               │
│                                             │
│                             [Smart FAB 48px]│  Floating action button (context-aware)
├─────────────────────────────────────────────┤
│  [Context Breadcrumb]                       │  e.g. 🎲 Settlers of Catan
├─────────────────────────────────────────────┤
│  ActionBar (sticky bottom)                  │  3 slots + overflow ⋯
└─────────────────────────────────────────────┘
```

**Desktop**:
```
┌─────────────────────────────────────────────┐
│  TopNavbar (sticky, 52px)                   │  Logo + nav links + search + avatar
├─────────────────────────────────────────────┤
│  MiniNav (optional, 40px)                   │  Contextual tabs
├─────────────────────────────────────────────┤
│                                             │
│  Page Content                               │
│                                             │
│  ┌─────────────────────────────────────┐   │
│  │ 🎲 Catan  │ ▶️ Gioca │ 🤖 AI │ ❤️ │⋯ │  │  ← Floating glassmorphism pill
│  └─────────────────────────────────────┘   │
└─────────────────────────────────────────────┘
```

### 3.2 Responsive Breakpoints

| Breakpoint | Width | Layout |
|-----------|-------|--------|
| Mobile | < 640px | Hamburger nav, Smart FAB, bottom ActionBar (3 slots + overflow) |
| Tablet | 640–1024px | Expanded nav, bottom ActionBar (4 slots) |
| Desktop | > 1024px | Full nav, **floating glassmorphism pill** con breadcrumb + azioni (5–6 slots) |

### 3.3 TopNavbar Elements

| Element | Mobile | Desktop |
|---------|--------|---------|
| Logo | Center | Left |
| Primary nav | Hamburger | Inline: Library, Catalog, Sessions |
| Search | Icon → expands | Wide input (searches game catalog) |
| Auth | Avatar icon | Login / Register buttons OR Avatar + dropdown |

**Auth dropdown** (logged in): Profile · My Stats · Settings · Logout

### 3.4 Smart FAB (Mobile Only)

A single floating button that **adapts to context**:
- Library → ➕ Add game
- Game Detail (in library) → ▶️ New session
- Active Session → 🤖 Ask AI
- Catalog → 🔍 Advanced search
- Long-press reveals 2–3 secondary actions

### 3.5 Contextual Actions (ActionBar / Glass Pill)

Le azioni contestuali cambiano in base alla pagina corrente. Priority-sorted.

**Mobile** → sticky bottom ActionBar (icona + label, 3 slot + overflow ⋯)
**Desktop** → floating glassmorphism pill centrata in basso, backdrop-blur, bordatura sottile. Contiene: context label (breadcrumb) + separatore + azioni orizzontali.

| Contesto | Azioni (in ordine di priorità) |
|----------|-------------------------------|
| Library Overview | ➕ Aggiungi · 🔍 Filtra · ↕️ Ordina · 📊 Vista · 📤 Esporta |
| Game Detail | ▶️ Nuova sessione · ➕ Aggiungi · 🤖 AI · ❤️ Wishlist · 📤 Condividi |
| Active Session | 🤖 AI · ⏱️ Timer · 📝 Punteggi · ⏸️ Pausa · ✅ Termina |
| AI Chat | 📤 Invia · 📎 Allega · 🎤 Voce · 🔄 Nuova · 📋 Cronologia |

---

## 4. Key Pages

### 4.1 Landing / Home (`/`)

**Purpose**: Marketing + onboarding entry point for unauthenticated visitors.
**Sections**:
1. Hero — "Your AI board game companion" with CTA (Get started / Login)
2. Feature highlights — RAG rules lookup, session tracking, game catalog
3. How it works — 3–4 steps
4. Community / catalog preview
5. Call to action — Sign up

### 4.2 Library (`/library`)

**Purpose**: User's personal game collection.
**Layout**: Grid/list toggle. Card-based display.
**Card info**: Game cover, title, BGG rating, last played, session count, wishlist toggle.
**Filters**: By genre, player count, play time, rating, owned/wishlist.
**Empty state**: Illustrated prompt to add first game.

### 4.3 Game Detail (`/games/:id`)

**Purpose**: Full info on a specific game.
**Sections**:
- Cover image + metadata (publisher, year, players, duration, BGG rating)
- Library status badge + quick actions (Add, New Session, Wishlist)
- Uploaded rulebooks (PDFs) with AI query
- Session history for this game
- Community stats

### 4.4 Shared Game Catalog (`/catalog` or `/shared-games`)

**Purpose**: Community-curated game database.
**Layout**: Search + filters + grid/list of games.
**Card**: Cover, title, year, rating, player count range.
**Features**: Search by name/publisher, filter by mechanic/genre/player count.

### 4.5 Sessions (`/sessions`)

**Purpose**: Log and review game sessions.
**Views**: Timeline list + calendar view.
**Session card info**: Game, date, players, winner, duration, notes.

### 4.6 Active Session (`/sessions/:id`)

**Purpose**: Live session management during play.
**Features**:
- Player score tracking
- Turn timer
- AI rules assistant chat (inline, SSE streaming)
- Session notes
- End session / save results

### 4.7 AI Chat (`/chat` or inline in sessions)

**Purpose**: Chat with AI agent for game rules, strategy, and queries.
**UI**: Bubble chat interface, markdown rendering, streaming SSE.
**Context**: Knows which game is active and what documents are loaded.
**Input**: Text, voice, attachment (PDF).

### 4.8 Knowledge Base / Documents (`/knowledge-base`)

**Purpose**: Upload and manage rulebook PDFs per game.
**States**: Upload → Extracting → Indexing → Indexed | Failed.
**Features**: PDF viewer, page-level AI queries, document status tracking.

### 4.9 Game Nights (`/game-nights`)

**Purpose**: Organize group game sessions with invites.
**Features**: Create event, invite players, vote on games, confirm attendance.

### 4.10 Agents (`/agents`)

**Purpose**: Configure AI agents per game or context.
**Card info**: Agent name, assigned game, model, knowledge base status.

### 4.11 Admin Panel (`/admin`)

**Purpose**: Platform management (superadmin/admin only).
**Sections**: Users, Content moderation, Game catalog management, Analytics, System config.

### 4.12 Profile & Settings (`/profile`, `/settings`)

**Purpose**: User account management.
**Features**: Avatar, username, stats, notification preferences, OAuth connections.

---

## 5. Core Components

### 5.1 MeepleCard

The universal entity card component used throughout the app.

**Variants**:
- `grid` — default card in grid layouts
- `list` — compact horizontal row
- `compact` — minimal, for dense lists
- `featured` — larger with gradient overlay
- `hero` — full-width featured card

**Entity types** (each has its own accent color):
`game` (orange) · `player` (purple) · `session` (indigo) · `agent` (amber) · `document` (slate) · `chat` (blue) · `event` (rose)

**Visual elements**:
- Entity badge (top-left pill): color-coded, Quicksand Bold, UPPERCASE
- Left border accent: 4px → 6px on hover
- Warm-toned shadows
- Hover: subtle glow ring in entity color
- Image: cover photo with aspect-ratio crop

### 5.2 Search Bar

Searches the shared game catalog. Expands on focus. Suggestions dropdown with game covers.

### 5.3 AI Chat Bubble

Streaming message bubble. Supports: markdown, code blocks, game rule citations (with PDF page reference), typing indicator animation.

### 5.4 Session Score Tracker

Player list with +/- score controls. Live update during active session.

### 5.5 PDF Viewer with AI Overlay

Inline PDF viewer with floating AI query bar. Highlights referenced sections after AI answers.

---

## 6. User Flows

### 6.1 First-Time User (Happy Path)
1. Land on homepage → See value prop
2. Register (email or OAuth) → Onboarding
3. Add first game (search catalog or scan barcode)
4. Upload rulebook PDF → System indexes it
5. Start a game session → Track scores
6. Ask AI a rules question → Get cited answer

### 6.2 Session Flow (Returning User)
1. Open Library → Pick game → "New Session"
2. Add players, choose mode
3. Start → Track scores + ask AI as needed
4. End session → Save results → Share (optional)

### 6.3 AI Rules Lookup
1. Open game detail or active session
2. Type question in AI chat
3. AI searches indexed PDF knowledge base
4. Returns answer with page citation + highlighted excerpt

---

## 7. Tech Stack (hint per generatori)

- Next.js + Tailwind CSS + **shadcn/ui** (usa componenti shadcn quando possibile)
- Radix UI primitives, Lucide icons

---

## 8. Content States

### Library (`/library`)
- **Empty**: Illustrazione meeple + "Aggiungi il tuo primo gioco" + CTA primario
- **Loading**: Skeleton grid di card (3–4 placeholder animati)
- **Populated**: Grid di MeepleCard entity=game
- **Filtered (0 results)**: "Nessun gioco corrisponde ai filtri" + bottone "Reset filtri"

### Game Detail (`/games/:id`)
- **Loading**: Skeleton hero + skeleton metadati
- **In Library**: Badge "✓ In libreria" + CTA "Nuova sessione" primario
- **Not in Library**: CTA "Aggiungi a libreria" primario + "Wishlist" secondario
- **No PDF uploaded**: Sezione KB con CTA "Carica regolamento" + illustrazione
- **PDF indexed**: Badge verde + AI chat attiva sulla sezione

### Active Session (`/sessions/:id`)
- **Setup**: Form giocatori + configurazione (nome sessione, modalità)
- **In progress**: Scoreboard live + AI chat collassata/espandibile lateralmente
- **Paused**: Overlay semitrasparente sopra contenuto + "Riprendi" centrato
- **Ended**: Riepilogo risultati con podio/classifica + CTA "Salva e condividi"

### AI Chat
- **Empty / Welcome**: Avatar meeple AI + messaggio di benvenuto + 3–4 suggested questions come chip
- **Streaming**: Typing indicator animato + testo parziale che appare gradualmente
- **Response with citation**: Bubble risposta + card citazione `📄 pag. 12 — Regolamento.pdf`
- **Error / no KB**: "Non ho trovato informazioni su questo. Hai caricato il regolamento?" inline (non modal)
- **No game context**: "Seleziona un gioco per domande specifiche" + search inline

### Knowledge Base / Documents (`/knowledge-base`)
- **Empty**: "Nessun documento caricato" + CTA upload + descrizione del beneficio AI
- **Uploading**: Progress bar lineare + nome file
- **Extracting**: Spinner + label "Lettura del documento in corso..."
- **Indexing**: Progress step con indicatore "Indicizzazione chunk N/M"
- **Indexed**: Badge verde "Pronto" + contatore chunk + data indicizzazione
- **Failed**: Badge rosso + messaggio errore sintetico + CTA "Riprova"

### Catalog (`/catalog`)
- **Loading**: Skeleton grid
- **Search results**: Grid filtrata con highlight del termine cercato nel titolo
- **No results**: "Nessun risultato per «X»" + suggerimenti di ricerca alternativi
- **Game not in catalog**: CTA "Proponi questo gioco alla community"

### Game Nights (`/game-nights`)
- **No events**: Illustrazione tavolo da gioco vuoto + "Organizza una serata" CTA
- **Upcoming event**: Card con data, partecipanti confermati, giochi votati
- **Voting in progress**: Lista giochi con barre di voto live
- **Event past**: Card con riepilogo — chi ha vinto, giochi giocati

### Profile / Auth
- **Guest (non autenticato)**: Banner sticky "Accedi per salvare la tua libreria"
- **Onboarding (primo accesso)**: Stepper — Benvenuto → Aggiungi gioco → Invita amici
- **Profile loaded**: Avatar + statistiche (giochi, sessioni, ore di gioco)
- **Settings saved**: Toast di conferma inline (non modal)

---

## 9. Tone & UX Principles

- **Playful but professional**
  → Usa emoji come icone contestuali (🎲 🤖 ▶️), non come decorazione generica
  → Copy in italiano, tono diretto: "Nuova sessione" non "Avvia una nuova sessione di gioco"
  → Titoli in Quicksand bold, mai tutto maiuscolo tranne i badge entità

- **AI-first**
  → Il pulsante AI è sempre visibile nella glass pill / ActionBar, mai in un menu secondario
  → Quando l'AI risponde con una citazione, mostra sempre il riferimento al documento (pagina + nome PDF)
  → L'AI ha un avatar/identità visiva riconoscibile (meeple icon viola)

- **Mobile-first**
  → Tap target minimo 44px, spacing generoso tra elementi interattivi
  → Nessun hover-only interaction — tutto deve funzionare a tocco
  → Contenuto primario sopra la piega, azioni secondarie nell'overflow ⋯

- **Community**
  → Rating e statistiche community visibili nelle card, ma non invasivi (Nunito 11px, colore muted)
  → Contatori sociali (sessioni, giocatori) mostrano attività senza pressione

- **Performance**
  → Skeleton loader per ogni contenuto che richiede fetch — mai spinner globale a pagina intera
  → Feedback ottimistico: aggiungere un gioco alla libreria appare istantaneo lato UI
  → Transizioni: 150ms fade/slide, niente animazioni elaborate che rallentano la navigazione

---

## 10. What to Generate

When using this brief to generate layouts:

**Prioritize these pages first**:
1. Landing / Home (marketing)
2. Library (grid view with MeepleCards)
3. Game Detail page
4. Active Session (with AI chat sidebar/panel)
5. AI Chat standalone

**Design language keywords for AI generators**:
`dark theme` · `warm orange accent` · `board game aesthetic` · `card-based UI` · `glassmorphism pill` · `Discord-like community feel` · `Quicksand + Nunito fonts` · `mobile-first` · `AI assistant chat` · `entity color system`

**Evita (anti-pattern)**:
- ❌ Layout light/bianco come default — siamo dark-first
- ❌ Sans-serif generiche (Inter, Roboto) — usa Quicksand (headings) + Nunito (body)
- ❌ Accent color neutro (blu generico) — il primario è arancione warm `hsl(25, 95%, 38%)`
- ❌ Card piatte senza ombre warm — le shadow fanno parte dell'identità visiva
- ❌ Modal per ogni feedback — preferire toast e stati inline
- ❌ Sidebar laterale fissa per le azioni contestuali — desktop usa floating glass pill centrata in basso
- ❌ Bottom ActionBar su desktop — è solo per mobile/tablet
- ❌ Tutte le entity cards dello stesso colore — ogni tipo di entità ha il suo colore identitario
