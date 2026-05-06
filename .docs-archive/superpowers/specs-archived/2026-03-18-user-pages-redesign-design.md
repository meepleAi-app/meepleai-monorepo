# User Pages Redesign — "Il Tavolo"

**Date**: 2026-03-18
**Status**: Draft
**Scope**: Dashboard, Personal Library, Public Library, Agent page

## Vision

L'utente è **seduto al tavolo da gioco**. L'app è il suo tavolo, le card sono i pezzi di gioco, le entità sono collegate tra loro tramite mana pips. Lo stile è gaming/entertainment (Steam, Epic Games, Discord) — visivo, immersivo, con personalità.

## Design Principles

1. **Metafora del tavolo**: Ogni pagina riflette l'esperienza di essere a un tavolo da gioco
2. **Card MtG-inspired**: Anatomia card ispirata a Magic: The Gathering con icone categoria e badge stato
3. **Mana pips**: Sistema di connessioni visive tra entità, cliccabili per navigare
4. **Zero mock**: Solo dati reali da API + empty states con CTA chiare
5. **Stile Discord/Steam**: Feed attività personale, engagement, personalità visiva

## Card System — MtG-Inspired

### Anatomia universale

Ogni entity card segue lo stesso pattern:

| Posizione | Ruolo | Descrizione |
|-----------|-------|-------------|
| **Cover/Immagine** | Visual identity | Immagine del gioco o gradient colorato con icona |
| **Basso-SX immagine** | Classificazione ("cosa è") | Icona SVG custom della categoria/meccanica |
| **Basso-DX immagine** | Stato ("come sta") | Badge stato/sottogruppo |
| **Body** | Info | Titolo, sottotitolo, metadati |
| **Footer** | Connessioni | Mana pips per entità collegate |

### Icone meccanica (basso-SX) — SVG Custom Set

Set di ~15 icone SVG custom per le meccaniche di board game:

| Icona | Meccanica | Usata su |
|-------|-----------|----------|
| ⚙️ | Engine Building | Game card |
| 🗺️ | Area Control | Game card |
| 🃏 | Deck Building | Game card |
| 👷 | Worker Placement | Game card |
| 🤝 | Cooperativo | Game card |
| ⚔️ | Competitivo | Game card |
| 🎲 | Dice Rolling | Game card |
| 🧩 | Puzzle/Abstract | Game card |
| 📜 | Narrative/RPG | Game card |
| 🏗️ | Tile Placement | Game card |
| 🔄 | Trading/Negotiation | Game card |
| 🎯 | Set Collection | Game card |
| 🛡️ | Dungeon Crawler | Game card |
| 🚂 | Route Building | Game card |
| 👥 | Social Deduction | Game card |

**Per altri entity type**:

| Entity | Basso-SX (classificazione) | Icone |
|--------|---------------------------|-------|
| Agent | Tipo agente | Rules Expert, Strategy Advisor, FAQ Helper, Setup Guide |
| Session | Tipo sessione | Competitiva, Cooperativa, Tutorial, Solo |
| Chat | Contesto | Regole, Strategia, Setup, Generale |
| PDF/KB | Tipo documento | Regolamento, Scenario, FAQ, Reference |

### Badge stato (basso-DX)

| Entity | Stati possibili |
|--------|----------------|
| Game | Nuovo, Giocato, Preferito, In sessione |
| Agent | Ready, Configuring, No KB, Error |
| Session | Attiva, Completata, In pausa |
| Chat | Attiva, Archiviata |
| PDF/KB | Indexed, Extracting, Processing, Failed |

### Mana Pips

Badge circolari colorati che indicano connessioni tra entità:

I colori mana pip devono allinearsi ai `entityColors` esistenti in `meeple-card-styles.ts`:

| Pip | Colore (HSL) | Hex approx | Significato |
|-----|-------------|------------|-------------|
| 🤖 | `hsl(38 92% 50%)` | `#f0a030` (amber) | Ha agente configurato |
| 📄 | `hsl(174 60% 40%)` | `#29a68f` (teal) | Ha PDF/KB |
| 🎮 | `hsl(240 60% 55%)` | `#5252d4` (indigo) | Ha sessione |
| 💬 | `hsl(220 80% 55%)` | `#2e6de6` (blue) | Ha chat |

- **Attivo**: Colore pieno, cliccabile → naviga all'entità collegata
- **Inattivo**: `var(--border)` / `#30363d`, non cliccabile
- **Sizing**: 22px nelle card normali, 32px nella scheda agente

## Pages

### 1. Dashboard — "Il Tavolo"

**Layout**: Grid 2 colonne — Tavolo (main) + Feed (sidebar 280px)

**Tavolo (colonna principale)**:
- **Sessioni attive**: Card con bordo verde, prominenti. Mostrano gioco, giocatori, durata. Mana pips.
- **Giochi recenti dalla libreria**: Grid di card medie con mana pips. Ordinati per ultimo utilizzo.
- **I tuoi agenti**: Card agente con stats compatte (invocazioni, ultimo uso).

**Feed attività (sidebar)**:
- Solo attività personali dell'utente, ordinate cronologicamente
- Stile Discord activity log
- Tipi evento: chat con agente, PDF indicizzato, sessione avviata/completata, gioco aggiunto, agente configurato
- Timestamp relativi ("2 min fa", "ieri")
- Elementi cliccabili → navigano all'entità

**Card sizing**: Medie uniformi con thumbnail 48px. Né troppo grandi né troppo piccole.

**Empty states**:
- Nessun gioco → "Il tavolo è vuoto — esplora il catalogo" + CTA → Public Library
- Nessuna sessione → "Nessuna partita in corso — inizia a giocare"
- Nessun agente → "Configura il tuo primo agente AI"

**Mobile**: Feed scende sotto il tavolo. Card full-width.

**API utilizzate**:
- `GET /api/v1/users/me/stats` — KPI
- `GET /api/v1/sessions/recent` — Sessioni recenti
- `GET /api/v1/dashboard/activity-timeline` — Feed attività
- `GET /api/v1/dashboard/stream` (SSE) — Aggiornamenti real-time
- `GET /api/v1/agents/recent` — Agenti recenti

### 2. Personal Library — "La Collezione"

**Layout**: Vetrina/scaffale con 2 sezioni

**Toolbar**: Ricerca + conteggio giochi + toggle Grid/List

**Sezione 1 — "Dal catalogo condiviso"**:
- Giochi aggiunti dalla Public Library
- Scroll orizzontale (shelf-row) o grid view
- Card 140px con cover + info + mana pips

**Sezione 2 — "I tuoi giochi creati"**:
- Giochi custom dell'utente
- Stessa card ma con badge "Custom"
- CTA "Crea gioco" come card tratteggiata a fine riga

**Rimossi**: Wishlist, Proposals — la libreria mostra solo ciò che hai.

**Filtri** (da aggiungere):
- Per meccanica (usando icone SVG custom)
- Per numero giocatori
- Per stato (nuovo, giocato, preferito)
- Ordinamento (nome, data aggiunta, ultimo giocato)

**Empty state**: "La tua libreria è vuota — esplora il catalogo" + CTA → Public Library

**API utilizzate**:
- `GET /api/v1/library` — Libreria utente (paginata, filtri, ricerca)
- `GET /api/v1/library/stats` — Stats libreria
- `GET /api/v1/library/quota` — Quota usage

### 3. Public Library — Catalogo Condiviso

**Layout**: Marketplace stile Steam Store

**Ricerca**: Barra grande centrata, prominente. Punto di ingresso principale.

**Sezione Trending**: Scroll orizzontale con giochi trending (algoritmo time-decay già nel backend).

**Sezione "Tutti i giochi"**:
- Filtri per categoria/meccanica (chip/pill selezionabili usando le icone SVG custom)
- Grid di card con bottone "+ Aggiungi" o badge "✓ Nella tua libreria"
- Paginazione: infinite scroll o Load More

**Filtri** (da aggiungere):
- Per meccanica (icone SVG)
- Per numero giocatori
- Per rating
- Per complessità

**API utilizzate**:
- `GET /api/v1/shared-games` — Catalogo (paginato, ricerca)
- `GET /api/v1/catalog/trending` — Trending games
- `POST /api/v1/library/games/{gameId}` — Aggiungi a libreria personale

### 4. Agent — "Scheda Personaggio"

**Layout**: Character sheet RPG — 2 colonne, scrollabile, NO tab

**Portrait (colonna SX, 280px, sticky)**:
- Avatar grande con gradient
- Nome agente + type badge
- Link al gioco collegato
- Stats compatte: invocazioni, chat, PDF, ultimo uso
- Mana pips grandi (32px) per connessioni
- Bottone "Configura"

**Body (colonna DX, scrollabile)**:

**Sezione "Equipaggiamento — Knowledge Base"**:
- Lista PDF con icona, nome, pagine, data, status badge (Indexed/Extracting/Failed)
- CTA "Carica PDF" tratteggiata

**Sezione "Area Azione — Chat"**:
- Chat embedded prominente
- Validazione readiness (KB popolata, RAG inizializzato)
- Se non ready → messaggio con link a configurazione
- Fullscreen toggle

**Sezione "Storia — Conversazioni Recenti"**:
- Lista thread con titolo, data, conteggio messaggi
- Click → apre thread

**Empty states**:
- No PDF → "Equipaggia il tuo agente — carica un regolamento"
- No chat → "Inizia la tua prima conversazione"

**Mobile**: Portrait sopra, body sotto. Chat full-width.

**API utilizzate**:
- `GET /api/v1/agents/{id}` — Dettaglio agente (include `gameId` per lookup KB)
- `GET /api/v1/agents/{id}/configuration` — Config
- `GET /api/v1/knowledge-base/{gameId}/documents` — KB docs (gameId ottenuto dal dettaglio agente)
- `POST /api/v1/agents/{id}/chat` (SSE) — Chat streaming
- `GET /api/v1/chat-threads/my?gameId={gameId}` — Thread history filtrate per gioco dell'agente

**Nota**: La chain di lookup per KB docs è: `GET agent → agent.gameId → GET knowledge-base/{gameId}/documents`. Per giochi privati (`GameId = null, PrivateGameId = <id>`), usare il `privateGameId` dell'agente.

## Design Tokens

### Colori

- **Primary (amber)**: `#f0a030` — accenti, titoli, CTA
- **Background**: `#0d1117` (base), `#161b22` (surface), `#21262d` (elevated)
- **Border**: `#30363d`
- **Text**: `#e6edf3` (primary), `#8b949e` (secondary), `#484f58` (muted)
- **Entity colors** (da `entityColors` in `meeple-card-styles.ts`): Agent `hsl(38 92% 50%)` amber, KB `hsl(174 60% 40%)` teal, Session `hsl(240 60% 55%)` indigo, ChatSession `hsl(220 80% 55%)` blue

### Typography

- **Headings**: Quicksand (esistente)
- **Body**: Nunito (esistente)
- **Card titles**: 13px semibold
- **Card subtitles**: 11px regular, color secondary
- **Section titles**: 14px semibold uppercase, letter-spacing 1px

### Card Sizing

- **Shelf card**: 140px wide, cover 100px height
- **Game card (tavolo)**: Full-width row, thumb 48px
- **Mana pip**: 22px (normal), 32px (agent portrait)
- **Card hover**: translateY(-4px), box-shadow amber 0.15 opacity

### Animations

- **Card hover**: 0.2s ease — translateY + glow
- **Mana pip hover**: 0.2s — scale(1.2)
- **Page transitions**: Framer Motion layout (esistente)
- **Feed items**: Stagger in dall'alto

## Responsive Breakpoints

| Viewport | Dashboard | Library | Agent |
|----------|-----------|---------|-------|
| **Desktop** (>1024px) | 2 colonne (tavolo + feed) | Shelf rows + grid | 2 colonne (portrait + body) |
| **Tablet** (640-1024px) | 1 colonna, feed collassabile | Grid 3 colonne | 1 colonna, portrait compatto |
| **Mobile** (<640px) | 1 colonna, feed sotto | Grid 2 colonne | 1 colonna, portrait sopra |

## Implementation Notes

- **MeepleCard**: Estendere il componente esistente con nuove props:
  - `mechanicIcon?: React.ReactNode` — slot basso-SX sulla cover per icona classificazione
  - `stateLabel?: { text: string; variant: 'success' | 'warning' | 'error' | 'info' }` — slot basso-DX sulla cover per badge stato
  - Queste nuove props coesistono con le esistenti `badge`, `status`, `documentStatus`, `agentStatus`
- **Icone SVG**: Creare set in `apps/web/src/components/icons/mechanics/` (~15 icone)
- **Feed component**: Nuovo componente `ActivityFeed` con hook `useActivityTimeline()` basato su `GET /api/v1/dashboard/activity-timeline`
  - Event types da gestire: `game_added`, `session_completed`, `chat_saved`, `wishlist_added`
  - SSE opzionale via `GET /api/v1/dashboard/stream` per aggiornamenti real-time
- **Empty states**: Componente riutilizzabile `EmptyState` con varianti per entity type
- **Filtri**: Componente `MechanicFilter` con chip selezionabili usando icone SVG
- **Rimuovere**: Tab Wishlist e Proposals dalla Library, tutti i mock/placeholder data
- **Entity type naming**: Usare `chatSession` (non `chat`) per allinearsi a `MeepleEntityType` nel codebase
- **Paginazione Public Library**: Load More button (non infinite scroll) per consistenza UX e semplicità

## Out of Scope

- Gamification (achievements, badges, leaderboard) — futuro
- Social features (seguire utenti, condividere) — futuro
- Notifiche push — futuro
- Tema chiaro — solo dark theme per ora
