# SP4 — Brief Claude Design: Entity Detail Desktop

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.

## Contesto

Il cuore dell'app autenticata. Ogni entity type (game, agent, session, player, toolkit, kb, event) ha una **detail page** che oggi esiste in `apps/web/src/app/(authenticated)/<entity>/` ma **non ha mockup strutturato**. Il `mobile-app.jsx` copre la versione drawer mobile — qui servono le versioni **desktop** full-page e i pattern di index (list/grid).

Questo sub-project è il più ampio. Definisce come si naviga il 70% dell'app autenticata.

## Fonti di riferimento

- `mobile-app.jsx` — drawer mobile di ogni entity (pattern da mantenere)
- `02-desktop-patterns.html` — tre pattern desktop: split-view, sidebar-detail, tabs (scegli per ogni entity)
- `03-drawer-variants.html` — sei varianti drawer (usa side-panel desktop, bottom-sheet mobile)
- `tokens.css`, `components.css`
- `data.js` — shape domain model

## Pattern architetturale — scegli per ogni entity

Per ogni detail page decidi esplicitamente:

| Pattern | Quando usarlo |
|---------|---------------|
| **Split-view** (sidebar 320px + content) | Entity con molte sub-risorse navigabili (es. KB con documents) |
| **Sidebar-detail** (sidebar 240px nav + content main) | Index + detail di una collection (es. agents list + selected agent) |
| **Tabs** (header fisso + tab content) | Entity con aspetti distinti (es. session: Live, Stats, Events, Chat) |
| **Hero + body** (hero ampio in cima + body scrollable) | Entity con forte identità visiva (es. game con cover) |

Connection bar (pip delle entità collegate) **sempre presente** sotto l'hero/header, entro 80px dal top.

### Componenti già stabili — NON ridisegnare

Questi componenti v2 sono **già implementati e mergiati su `main-dev`**. Riusali pixel-perfect, non reinventare:

| Componente | Path codice | Stato | Mockup riferimento |
|------------|-------------|-------|---------------------|
| `ConnectionBar` | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` | ✅ in produzione (PR #549, #552) — applicata su `GameDetailDesktop`, `AgentCharacterSheet` | layout pip rounded-full, icona + count + label, entity color, isEmpty → "+" |
| `ConnectionChip` / `ConnectionChipPopover` / `ConnectionChipStrip` | `apps/web/src/components/ui/data-display/meeple-card/parts/` | ✅ in produzione (PR #542, #545, #549, #552) — usata in MeepleCard footer | strip orizzontale per card |
| `MeepleCard` | `apps/web/src/components/ui/data-display/meeple-card/` | ✅ in produzione | tutte le varianti (grid/list/compact/featured/hero) e i 9 entity types |
| `RecentsBar` | `apps/web/src/components/ui/navigation/recents-bar/` | ✅ in produzione (M5) | pill recent entities |
| `MobileBottomBar` + `MiniNavSlot` | `apps/web/src/components/layout/` | ✅ in produzione (M5) | 5-tab nav mobile |
| Drawer stack (`useCascadeNavigationStore`) | `apps/web/src/stores/cascadeNavigationStore.ts` | ✅ in produzione (M5) | side-panel desktop / bottom-sheet mobile, max 3 stack |

**Per SP4 il tuo lavoro è**: estendere l'uso di questi componenti alle entity che ancora non li hanno (KB, Toolkit, Player, Game Nights), NON ridefinire il pattern. Se senti il bisogno di deviare dal pattern stabile, **flagga esplicitamente con motivazione** — il default è riuso.

### Connection bar — input concreto per ogni entity

Per ogni detail produrre, definisci esplicitamente la lista pip da mostrare nella connection-bar (entityType + count + icon + label). Esempio già in produzione:

```tsx
// Game detail desktop (riferimento implementazione attuale)
const connections: ConnectionPip[] = [
  { entityType: 'agent',   count: 2, label: 'Agenti',    icon: Bot,        isEmpty: false },
  { entityType: 'kb',      count: 5, label: 'Documenti', icon: FileText,   isEmpty: false },
  { entityType: 'toolkit', count: 1, label: 'Toolkit',   icon: ToolboxIcon,isEmpty: false },
  { entityType: 'session', count: 12,label: 'Partite',   icon: Target,     isEmpty: false },
  { entityType: 'player',  count: 4, label: 'Giocatori', icon: User,       isEmpty: false },
  { entityType: 'chat',    count: 0, label: 'Chat',      icon: MessageCircle, isEmpty: true },
];
```

Riusa identica struttura per tutte le entity SP4. Vedi `apps/web/src/components/ui/data-display/connection-bar/build-connections.ts` per i builder esistenti (`buildGameConnections`, `buildAgentConnections`, `buildSessionConnections`).

## Schermate da produrre (16, raggruppate per entity)

### A. Game (`--c-game`, 🎲)

#### A1. Game index — `sp4-games-index.{html,jsx}`
**Route**: `/games`
**Pattern**: Hero + body. Grid di MeepleCard variante `grid` o `list` (toggle). Filtri top: search, status (owned/wishlist/played), complexity, players, year, designer. Sort: last played, rating, title, year.
**Stati**: default (24+ card), empty library ("Non hai ancora giochi — aggiungi il primo"), loading skeleton, filtered empty.
**Desktop**: 4-col grid. **Mobile**: 2-col grid compact.

#### A2. Game detail — `sp4-game-detail.{html,jsx}`
**Route**: `/games/[id]`
**Pattern**: Hero + body con tabs.
**Hero**: cover gradient full-width 240px, overlay con titolo grande, meta (designer/anno/giocatori/durata/complexity), azioni (Edit, Play Now, Share).
**Connection bar**: pip agent (N), kb (N), toolkit (N), session storiche (N), player (ultimi 4), chat.
**Tabs**:
- Info (descrizione + specs + custom rules + house rules → pattern SettingsRow)
- Sessions (list sessioni passate con scores + date, click → drawer SessionDetail)
- Chat (thread agent correlati, quick-start "Chiedi all'agente")
- Stats (win rate, avg score, play count, last played, giocatori più frequenti)
- Documents (KB list con upload CTA)
**Stati**: default, loading (skeleton hero + tabs), no-data per ogni tab, own vs community gioco.

---

### B. Agent (`--c-agent`, 🤖)

#### B1. Agent index — `sp4-agents-index.{html,jsx}`
**Route**: `/agents`
**Pattern**: Sidebar-detail. Sidebar sinistra: lista agent con pip entity=agent, status dot (attivo/addestramento/archiviato), game expertise sotto il nome. Main: character sheet dell'agent selezionato (vedi B2). Mobile: solo lista, tap → naviga a detail.
**Stati**: lista vuota (CTA "Crea il primo agent"), agent selezionato, loading.

#### B2. Agent detail (character sheet) — `sp4-agent-detail.{html,jsx}`
**Route**: `/agents/[id]`
**Pattern**: Hero + body con tabs. È un **character sheet** — pagina identità dell'agent.
**Hero**: avatar grande (emoji o illustrazione), nome, game expertise (EntityChip game), model name (badge mono), status (attivo/training/archiviato).
**Connection bar**: pip kb (N docs usati), toolkit (pubblicato in N), chat (thread aperti), session (usato in).
**Tabs**:
- Overview (descrizione + capabilities + limits + versione)
- Knowledge (KB sources con relevance score, upload/remove)
- Training (history retraining, evals, quality score)
- Chats (thread correlati — reusa pattern chat list)
- Settings (model params, temperature, prompt customization — pattern SettingsRow)
**Stati**: attivo / in-training / archiviato, loading, no-kb warning.

---

### C. Session (`--c-session`, 🎯)

#### C1. Session index — `sp4-sessions-index.{html,jsx}`
**Route**: `/sessions`
**Pattern**: Tabs (Live | Paused | Finita) + list.
Card session: game cover 60×60 + titolo gioco + player pips avatar + status badge + timestamp. Click → detail.
**Stati**: no sessions ("Crea la prima sessione"), filtered empty, loading.

#### C2. Session live (desktop) — `sp4-session-live-desktop.{html,jsx}`
**Route**: `/sessions/[id]` (status=in-corso)
**Pattern**: Split-view complesso (il più ricco del sub-project).
**Layout desktop 3-col**:
- Left (240px): player list con scores live, turn indicator, add player
- Center (flex): event feed scrollable (card event timeline), floating action "Log event"
- Right (360px): tool panel (dadi, timer, counter, scoreboard) + mini-chat agent collassabile
**Top bar session**: game title + turn counter + status pill + actions (pause, finalize, abandon)
**Mobile**: pattern del drawer session in `mobile-app.jsx` (già coperto — indica riferimento).
**Stati**: in-corso, pausa (overlay "Partita in pausa"), finita (summary card), loading.

#### C3. Session finita / summary — `sp4-session-summary.{html,jsx}`
**Route**: `/sessions/[id]` (status=finita)
**Pattern**: Hero + body.
**Hero**: vincitore (player pip grande + score) + durata partita + titolo gioco.
**Body**: classifica completa, timeline events (compressed), screenshot tool finali, chat excerpts highlights, CTA "Rivedi regole", "Gioca di nuovo", "Condividi".
**Stati**: finita normale, finita con abbandono early, loading.

---

### D. Player (`--c-player`, 👤)

#### D1. Players index — `sp4-players-index.{html,jsx}`
**Route**: `/players`
**Pattern**: Grid. Card player: avatar circolare grande, nome, stats mini (played/wins/avg), entity pip badge. Sort: alphabetical, played desc, wins desc.
**Stati**: default, empty ("Aggiungi player per tracciare le partite"), loading.

#### D2. Player detail — `sp4-player-detail.{html,jsx}`
**Route**: `/players/[id]`
**Pattern**: Hero + body con tabs.
**Hero**: avatar grande gradient, nome, "N partite giocate · M vittorie · W/L ratio", edit button.
**Connection bar**: pip game (più giocati top 4), session (recenti), toolkit (contributor?), achievements.
**Tabs**:
- Stats (totali per gioco, trend temporale, favorite games)
- Sessions (cronologia con win/loss indicator)
- Games (collection personale se è un utente app)
- Achievements (badge ottenuti — gamification)
**Stati**: player registrato (user) vs guest player, loading.

---

### E. Toolkit (`--c-toolkit`, 🧰)

#### E1. Toolkit index — `sp4-toolkits-index.{html,jsx}`
**Route**: `/toolkit`
**Pattern**: Tabs (Miei | Installati | Esplora community) + grid.
Card toolkit: icona bundle + nome + game (EntityChip) + author pip + install count + rating stars.
**Stati**: miei vuoti ("Crea primo toolkit"), community popolata, loading.

#### E2. Toolkit detail / browser — `sp4-toolkit-detail.{html,jsx}`
**Route**: `/toolkit/[id]`
**Pattern**: Split-view. Sinistra: sommario + CTA install/edit + version history. Destra: tabs Contenuti (agent, kb, tool), Readme, Changelog, Reviews.
**Hero compatto**: nome + author + install count.
**Connection bar**: pip agent (N), kb (N), tool (N), game (1).
**Stati**: own (edit mode disponibile), installed (update disponibile badge), not-installed (install CTA), deprecated warning, loading.

---

### F. Knowledge Base (`--c-kb`, 📄)

#### F1. KB index — `sp4-kb-index.{html,jsx}`
**Route**: `/knowledge-base`
**Pattern**: Sidebar-detail. Sidebar: lista documents raggruppati per game. Main: detail del document selezionato.
Item: icona tipo (PDF/MD/Web) + filename + game pip + pages + indexedAt + usedBy agents pip row.
**Stati**: no docs ("Carica primo PDF"), doc selezionato, loading.

#### F2. KB document detail — `sp4-kb-document-detail.{html,jsx}`
**Route**: `/knowledge-base/[id]`
**Pattern**: Split-view con preview PDF. Sinistra: metadata + navigazione pagine + chunks indicizzati. Destra: preview PDF embedded o raw text fallback.
**Tabs body**: Preview, Chunks (lista con embeddings score visual), Usage (quali agent/chat citano), Metadata.
**Stati**: indexed, indexing (progress bar), failed-indexing (retry CTA), loading.

---

### G. Game Nights (`--c-event`, 🎉)

#### G1. Game nights calendar — `sp4-game-nights-calendar.{html,jsx}`
**Route**: `/game-nights`
**Pattern**: Toggle view (Calendar | List).
**Calendar desktop**: mese con event dots colorati, click day → popover events.
**List**: raggruppate per "Questa settimana | Prossima | Future | Passate".
Card event: data+ora grande, titolo, game (pip), location, player pips attesi, status.
**Stati**: default, empty ("Nessun evento programmato"), loading.

#### G2. Game night detail — `sp4-game-night-detail.{html,jsx}`
**Route**: `/game-nights/[id]`
**Pattern**: Hero + body con tabs.
**Hero**: emoji party grande, titolo, data+ora+luogo, status (planned/ongoing/done).
**Connection bar**: pip game (lineup 1-5), player (invitati), session (sessioni associate), chat.
**Tabs**: Lineup (game order + player assignments), Guests (invited/accepted/declined), Diary (cross-game timeline — pattern da SessionTracking), Photos (se uploadate).
**Stati**: planned, ongoing (live badge), completed, cancelled, loading.

---

### H. Library desktop — `sp4-library-desktop.{html,jsx}`

**Route**: `/library` (authenticated)
**Pattern**: Tabs (Owned | Wishlist | Played | Top 10) + hybrid grid/list.
**Filters row**: search, complexity, players, year, status subfilter.
**Sort**: recent, alphabetical, complexity, rating, last-played.
**Grid card**: MeepleCard entity=game variante `grid`. **List row**: MeepleCard variante `list`.
**Desktop sidebar** (facoltativa): filtri avanzati espansi, stats library ("X giochi · Y ore totali · Z partite").
**Stati**: default, empty per tab (CTA contestuale), filtered empty, loading skeleton grid.

---

### I. Discover — `sp4-discover.{html,jsx}`

**Route**: `/discover`
**Pattern**: Hero + rows (stile Netflix).
**Rows**: "Per te" (recommendation), "Nuovi toolkit", "Trending giochi", "Agenti popolari", "Community picks".
Ogni row: horizontal scroll di MeepleCard compact, variante entity-specific.
**Filters**: entity type toggle (All | Games | Toolkit | Agents).
**Stati**: default, loading (skeleton rows), empty-recommendations fallback ("Gioca di più per avere consigli").

---

## Priorità produzione

Produci in quest'ordine per sbloccare percorsi utente critici:

1. **Game detail + Game index** (A1, A2) — entity principale, sblocca library flow
2. **Agent detail + Agent index** (B1, B2) — core value prop dell'app
3. **Session live + summary** (C2, C3) — flusso play principale
4. **Library desktop** (H) — home autenticata
5. **KB index + detail** (F1, F2) — foundation dati
6. **Toolkit index + detail** (E1, E2) — feature community
7. **Player detail + index** (D1, D2) — tracking
8. **Game nights calendar + detail** (G1, G2) — feature eventi
9. **Discover** (I) — discovery UX
10. **Session index** (C1) — list utility

## Componenti v2 nuovi attesi

Dai mockup emergeranno questi nuovi componenti. Nei commenti `/* v2: NewComponentName */` annota dove servono, così l'implementazione post-mockup avrà una mappa chiara.

Attesi:
- `GameHero`, `GameTabs`, `GameSpecsCard`
- `AgentCharacterSheet`, `AgentKnowledgeList`, `AgentTrainingPanel`
- `SessionLiveLayout`, `SessionEventFeed`, `SessionToolPanel`, `SessionSummaryCard`
- `PlayerHero`, `PlayerStatsGrid`
- `ToolkitSummary`, `ToolkitContentList`, `ToolkitVersionHistory`
- `KbDocumentPreview`, `KbChunksList`
- `GameNightCalendar`, `GameNightDetailHeader`, `GameNightLineup`
- `LibraryFilters`, `LibraryStatsSidebar`
- `DiscoverRow`

## Consegna

Dopo ogni entity group (A/B/C/...), produci un handoff con:
- Lista file creati
- Nuovi componenti v2 flaggati con path `apps/web/src/components/ui/v2/<name>/`
- Screenshot mentale delle varianti critiche (light/dark, mobile/desktop, empty/loading)
- Decisioni sul pattern desktop scelto (split-view / sidebar-detail / tabs / hero) con motivazione

Ping finale quando SP4 completo: tabella file → route → pattern desktop usato + lista master dei ~20 nuovi componenti v2.
