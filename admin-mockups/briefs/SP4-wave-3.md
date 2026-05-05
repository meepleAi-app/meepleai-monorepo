# SP4 wave 3 — Brief Claude Design (scope ridotto, 5 mockup)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Questo brief **estende e restringe** `SP4-entity-desktop.md` — non riproduce tutto SP4, solo la wave 3.

## Stato programma

| Wave | PR | Stato | Mockup |
|------|----|----|--------|
| Wave 1 | #569 | ✅ MERGED `main-dev` (2026-04-26) | A1 games-index, A2 game-detail, B1 agents-index, B2 agent-detail, H library-desktop |
| Wave 2 | #570 | ✅ MERGED `main-dev` (2026-04-26) | C1 sessions-index, C2 session-live (+ parts), C3 session-summary (+ parts) |
| **Wave 3** | **(questo brief)** | ⏳ in design | **D player-detail, E toolkit-detail, F kb-detail, G game-nights-index, I discover** |
| Wave 4+ | TBD | post-wave-3 | C1 sessions-index re-review se serve, D1 players-index, E1 toolkits-index, F1 kb-index, G2 game-night-detail |

**Wave 3 = chiude i 5 percorsi che restano per coprire le entity meno usate ma necessarie ad alpha**: tracking giocatori, browse toolkit community, foundation knowledge base, calendario eventi, discovery cross-entity.

## Scope wave 3 — esattamente 5 mockup

| # | File | Route | Pattern desktop | Note |
|---|------|-------|-----------------|------|
| D | `sp4-player-detail.{html,jsx}` | `/players/[id]` | Hero + body con tabs | Character sheet style, riusa pattern Agent detail |
| E | `sp4-toolkit-detail.{html,jsx}` | `/toolkits/[id]` | Split-view (sinistra summary+install, destra contenuti) | Public + own (edit) variants |
| F | `sp4-kb-detail.{html,jsx}` | `/kb/[id]` | Split-view (sinistra meta+chunks, destra preview) | Document detail singolo (no index) |
| G | `sp4-game-nights-index.{html,jsx}` | `/game-nights` | Toggle Calendar / List | Index mese + lista raggruppata |
| I | `sp4-discover.{html,jsx}` | `/discover` | Hero + horizontal rows (Netflix style) | Cross-entity discovery |

**Sequence di consegna consigliata**: D → E → F → G → I (D/E sbloccano UX più visibili; G/I sono nice-to-have alpha).

## Componenti già stabili — NON ridisegnare

Tabella aggiornata post-wave 2. Questi sono in produzione e devi **istanziarli**, non clonare il loro JSX nei mockup:

| Componente | Path codice | Mergiato da | Riuso obbligatorio in wave 3 |
|------------|-------------|-------------|------------------------------|
| `ConnectionBar` | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` | PR #549, #552, riconfermato wave 1+2 | D, E, G2 (G1 = index, niente) |
| `ConnectionChip` family + `ConnectionChipStrip` | `apps/web/src/components/ui/data-display/meeple-card/parts/` | PR #542, #545, #549, #552 | E (footer card community), I (rows) |
| `MeepleCard` (grid/list/compact/featured/hero) | `apps/web/src/components/ui/data-display/meeple-card/` | base | D Sessions tab, G list, I rows |
| `RecentsBar` + `MobileBottomBar` + `MiniNavSlot` | `apps/web/src/components/{ui/navigation,layout}/` | M5 | implicito per layout shell |
| Drawer stack (`useCascadeNavigationStore`) | `apps/web/src/stores/cascadeNavigationStore.ts` | M5 | side-panel desktop / bottom-sheet mobile |
| Tabs animated underline (wave 1) | introdotto wave 1 | wave 1 | D tabs, E tabs body, F tabs |
| AdvancedFiltersDrawer | `apps/web/src/components/ui/v2/advanced-filters-drawer/` | wave 1 | I (filtri entity-type) |
| Confetti CSS-only + reduced-motion (wave 2 summary) | wave 2 | wave 2 | non serve in wave 3 |

**ConnectionBar reproduction 1:1 prod (lezione PR #568, #569, #570)**: `borderRadius: 999` (`var(--r-pill)`), bg pieno = `entityHsl(type, 0.1)` non-empty, dashed + opacity 0.6 + Plus icon empty, aria-label `${label}: ${count}` non-empty / solo `${label}` empty, tabular-nums per count. Per impl: NON copiare JSX mockup, **istanziare componente prod** con `connections: ConnectionPip[]`.

### `entityHsl` helper inline (palette 9 entity)

Replica nel mockup come funzione locale (l'impl userà `apps/web/src/lib/theme/entity-hsl.ts`):

```js
const ENTITY_HSL = {
  game:    '25 95% 45%',
  player:  '262 83% 58%',
  session: '240 60% 55%',
  agent:   '38 92% 50%',
  kb:      '174 60% 40%',
  chat:    '220 80% 55%',
  event:   '350 89% 60%',
  toolkit: '142 70% 45%',
  tool:    '195 80% 50%',
};
const entityHsl = (entity, alpha) =>
  alpha != null ? `hsl(${ENTITY_HSL[entity]} / ${alpha})` : `hsl(${ENTITY_HSL[entity]})`;
```

## Vincolo dati (lezione PR #568)

GitGuardian del repo è zero-tolerance sui pattern UUID-like che lo scanner classifica come fake-secret. Nei dati finti dei mockup wave 3:

❌ **VIETATO**: token che assomigliano a UUID (es. `g6h7i8j9-...`, `1234567890abcdef`, hex string ≥ 32 char), bearer-like (`sk_test_...`, `eyJ...`), api-key pattern.

✅ **OK**: ID short e leggibili (`p-marco`, `tk-azul-helper`, `kb-wingspan-rules`), GUID solo se ovviamente fittizi (`00000000-...-aaaa`).

Pattern dati: nomi italiani da `data.js` (Marco, Giulia, Davide, Sara, ecc.), giochi tipici (Wingspan, Azul, 7 Wonders, Catan, Carcassonne, Ticket to Ride, Brass: Birmingham), location ("Casa Marco", "Sala B&B", "Online").

---

## D — Player detail (character sheet)

**File**: `sp4-player-detail.{html,jsx}`
**Route**: `/players/[id]`
**Pattern**: Hero + body con tabs. Replica struttura B2 (agent-detail) ma con palette `--c-player` e tabs adattate.

### Hero
- Avatar gradient circolare 96px (mobile) / 128px (desktop), bordo 3px `entityHsl('player', 0.4)`
- Nome grande (Quicksand bold, `--fs-3xl`)
- Sottotitolo: "{N} partite · {M} vittorie · {W/L}% win rate" — mono kicker per i numeri
- Edit button + entity badge `player`
- Variante: **registrato (user app)** vs **guest player** — il guest NON ha avatar gradient, mostra emoji 👤 + flag "Guest" pill

### ConnectionBar (sotto hero, entro 80px dal top)

```ts
const connections: ConnectionPip[] = [
  { entityType: 'game',    count: 5,  label: 'Top giochi',   icon: Dice5,        isEmpty: false },
  { entityType: 'session', count: 23, label: 'Partite',       icon: Target,       isEmpty: false },
  { entityType: 'event',   count: 4,  label: 'Game Nights',   icon: Calendar,     isEmpty: false },
  { entityType: 'agent',   count: 0,  label: 'Agenti usati',  icon: Bot,          isEmpty: true  },
  { entityType: 'toolkit', count: 1,  label: 'Toolkit',       icon: ToolboxIcon,  isEmpty: false },
  { entityType: 'chat',    count: 12, label: 'Chat',          icon: MessageCircle,isEmpty: false },
];
```

### Tabs body (4 tabs, animated underline come wave 1)
1. **Stats** — totali per gioco (top 5 con MeepleCard variante `compact` entity=game), trend temporale (line chart placeholder gradient), favorite games row, achievement badges row
2. **Sessions** — lista cronologica con win/loss indicator (chip verde 🏆 / chip neutro), filtro by game, MeepleCard variante `list` entity=session
3. **Games** — collection personale (solo se user app, hidden per guest); MeepleCard grid entity=game
4. **Achievements** — badge ottenuti (gamification): griglia 3-col mobile / 6-col desktop, badge locked/unlocked

### Stati richiesti
- **Default** (registered user, dati pieni)
- **Guest player** (no avatar gradient, no Games tab, achievements limitati)
- **Empty stats** (player appena creato, "Aggiungi la prima partita")
- **Loading** (skeleton hero + tabs)
- **Light + dark** entrambi

### Deviazioni accettate
- Win rate display `:0%` formato culture-independent (lezione issue #2593) — nel mockup mostra `73%` non `73,0%`
- Achievements gamification = solo visual placeholder, vera logica post-alpha

---

## E — Toolkit detail

**File**: `sp4-toolkit-detail.{html,jsx}`
**Route**: `/toolkits/[id]`
**Pattern**: Split-view desktop (left 380px summary + install, right flex content). Mobile: stacked (summary top, content sotto).

### Hero compatto (in summary sinistra)
- Icona bundle 64px gradient `entityHsl('toolkit', 0.2)` → `entityHsl('toolkit', 0.5)`
- Nome toolkit (Quicksand `--fs-2xl`)
- Author pip (entity=player se single author, kicker mono "+ N contributor" se più)
- Game pip 1 (1 gioco target obbligatorio)
- Stats row: install count (icon Download), rating stars (icon Star), version (mono)
- CTA principale entity-colored:
  - `not-installed` → "Installa" pieno toolkit color
  - `installed` → "Apri" outlined + badge update se versione nuova
  - `own` → "Modifica" outlined + chip "Tuo"
  - `deprecated` → banner warning sopra CTA
- Version history collapsible (ultime 3 + "Vedi tutte")

### ConnectionBar (sotto summary header, 6 pip)

```ts
const connections: ConnectionPip[] = [
  { entityType: 'agent', count: 2, label: 'Agenti',     icon: Bot,        isEmpty: false },
  { entityType: 'kb',    count: 5, label: 'Documenti',  icon: FileText,   isEmpty: false },
  { entityType: 'tool',  count: 3, label: 'Strumenti',  icon: Wrench,     isEmpty: false },
  { entityType: 'game',  count: 1, label: 'Gioco',      icon: Dice5,      isEmpty: false },
  { entityType: 'session',count:8, label: 'Usato in',    icon: Target,     isEmpty: false },
  { entityType: 'player',count:42, label: 'Installato',  icon: User,       isEmpty: false },
];
```

### Tabs body (4 tabs)
1. **Contenuti** — lista combinata agent+kb+tool con MeepleCard variante `list`, group header per type
2. **Readme** — markdown render (placeholder con tipografia Nunito body, code blocks JetBrains Mono)
3. **Changelog** — version timeline verticale, ogni entry: version pill mono + data + bullets changes
4. **Reviews** — lista review utenti (avatar + stars + commento + data), distribution bar in cima (5★/4★/.../1★ con %)

### Stati richiesti
- **not-installed** (CTA install, no badge update)
- **installed** (CTA Apri, no update available)
- **installed + update available** (badge "Aggiornamento v1.2 disponibile")
- **own** (CTA Modifica + chip "Tuo", tab Readme editable inline)
- **deprecated** (banner warning rosso, CTA disabled, suggerisce alternativa)
- **Loading** (skeleton split-view)
- **Light + dark**

### Deviazioni accettate
- Reviews distribution bar: gradient verticale `--c-success` → `--c-warning` → `--c-danger` ammesso (uso semantico, non nuova palette)

---

## F — KB document detail

**File**: `sp4-kb-detail.{html,jsx}`
**Route**: `/kb/[id]`
**Pattern**: Split-view desktop (left 320px metadata+chunks, right flex preview). Mobile: tabs (Preview | Chunks | Usage | Meta).

### Sinistra (metadata + navigazione)
- Icona tipo documento 48px (PDF=📄 rosso, MD=📝 blu, Web=🌐 verde — entity color base `--c-kb`)
- Filename (Quicksand bold `--fs-xl`, truncate con tooltip)
- Game pip (1 obbligatorio — il documento è sempre legato a un gioco)
- Meta righe (kicker mono):
  - "Pagine: 24"
  - "Indicizzato: 2 giorni fa"
  - "Chunks: 142"
  - "Embedding model: bge-large-it"
- Section "Usato da" (entity pip stripe agent + count, max 4 + "+N")
- Section "Pagine" — list verticale paginata (1, 2, 3...) con thumbnail mini 32×40 + numero pagina
- Section "Chunks indicizzati" — lista verticale con relevance score visual bar (progress 0-100% `entityHsl('kb', ...)`)

### Destra (preview)
- **Preview tab**: PDF embedded mockup (placeholder gradient con frame iframe), toolbar zoom +/- + paginazione + search inline
- **Chunks tab**: lista chunks expandable con highlight, ogni chunk = card piccola con score + page ref + text preview 3 righe
- **Usage tab**: list di chat/agent che hanno citato il doc, MeepleCard variante `list` entity=chat o entity=agent + page reference badge
- **Metadata tab**: SettingsRow pattern (key/value) — title, author, language, file size, hash (NO UUID-like!), upload date, indexed by

### Stati richiesti
- **indexed** (default, tutto disponibile)
- **indexing** (progress bar `entityHsl('kb', 0.4)` con %, banner "Indicizzazione in corso… 47%")
- **failed-indexing** (banner danger + CTA "Riprova", chunks tab vuota con messaggio errore)
- **preview unavailable** (PDF non renderizzabile, fallback raw text con monospace)
- **Loading** (skeleton split)
- **Light + dark**

### Deviazioni accettate
- Embedding model name è leggibile per debug (`bge-large-it`) — è un identificatore pubblico, NON un secret
- File hash mostrato come `sha256: 4a7b...e9f2` (truncated middle, 7 char + ellipsis + 4 char) — non UUID-like, non triggerable da scanner

---

## G — Game Nights index (calendar + list toggle)

**File**: `sp4-game-nights-index.{html,jsx}`
**Route**: `/game-nights`
**Pattern**: Hero compatto + Toggle view switcher + body conditional (Calendar vs List).

### Hero compatto
- Title "Game Nights" (Quicksand `--fs-3xl`)
- Subtitle stats row: "{N} programmate · {M} questo mese · {K} totali"
- CTA primary: "+ Nuova serata" (entity color event)

### Toggle view switcher (segmented control)
- Due opzioni: 📅 Calendar | 📋 Lista
- Default: Calendar desktop, Lista mobile (mobile non ha spazio per calendar denso)
- Persiste preferenza via `localStorage` nell'impl (placeholder JSX)

### View Calendar (desktop)
- Grid 7-col mese con righe variabili (4-6 settimane)
- Each cell: numero giorno (top-left), event dots colorati `entityHsl('event', ...)` per ogni event nel giorno (max 3 visibili + "+N" se più)
- Today cell: bordo `entityHsl('event', 0.6)` 2px
- Hover cell con events: popover con MeepleCard variante `compact` per ogni event
- Header mese: "Aprile 2026" + frecce navigazione + "Oggi" CTA per reset
- Filter row: by game (dropdown EntityChip game), by status (planned/ongoing/done), by attendee (player picker)

### View List (default mobile)
- Sezioni raggruppate sticky header:
  - "Questa settimana"
  - "Prossima settimana"
  - "Future"
  - "Passate" (collapsible, default closed)
- Card event = MeepleCard custom entity=event variante `list`:
  - Data+ora grande sinistra (`--fs-2xl` numero giorno + `--fs-xs` mese mono)
  - Titolo event + game pip + location label
  - Player pips row (max 5 + "+N")
  - Status badge dx (planned/ongoing/done — colori semantici)

### Stati richiesti
- **Default Calendar** (mese corrente con 8-12 events distribuiti)
- **Default List** (4-6 events raggruppati su 4 sezioni)
- **Empty** ("Nessun evento programmato — crea la prima serata")
- **Filtered empty** ("Nessun risultato per i filtri attivi" + reset CTA)
- **Loading Calendar** (skeleton grid 7×6 con shimmer)
- **Loading List** (3 skeleton card)
- **Light + dark**
- **Mobile + desktop** (mobile = solo lista per default; toggle disponibile ma calendar è denso a 375px → preview con horizontal scroll)

### Deviazioni accettate
- Mobile calendar = horizontal scroll giornaliero (1 colonna per giorno, 7 giorni visibili) — non è il calendario mensile classico, ma evita di rendere illeggibili i numeri
- prefers-reduced-motion: disabilita transition tra calendar/list (instant swap)

---

## I — Discover (Netflix-style cross-entity)

**File**: `sp4-discover.{html,jsx}`
**Route**: `/discover`
**Pattern**: Hero + body horizontal rows.

### Hero
- Background gradient sottile multi-entity (5 colori a 0.08 alpha, blend left→right)
- Title "Scopri" (Quicksand `--fs-4xl`)
- Subtitle: "Giochi, agenti e toolkit consigliati per te"
- Search input centrale (placeholder "Cerca giochi, toolkit, agenti…") full-width mobile / max-w-2xl desktop
- Entity type filter chips below: "Tutto" (default) | 🎲 Giochi | 🧰 Toolkit | 🤖 Agenti | 📄 KB
- AdvancedFiltersDrawer trigger (icona filter, mostra count active filters)

### Body — 5 rows orizzontali

Ogni row:
- Header: title + "Vedi tutti →" link a destra
- Horizontal scroll (desktop = scroll-snap, mobile = swipe naturale)
- Card = MeepleCard variante `compact` entity-specific
- Larghezza card: 200px mobile, 240px desktop
- 6-10 card per row, scroll continua oltre il viewport

Rows in ordine (priorità per attenzione):
1. **🎯 Per te** — recommendation personalizzata (mix entity)
2. **🆕 Nuovi toolkit** — entity=toolkit recenti (sort by createdAt desc)
3. **🔥 Trending giochi** — entity=game most-played-this-week community-wide
4. **🤖 Agenti popolari** — entity=agent most-installed
5. **⭐ Community picks** — curated mix (toolkit + game) con badge "Curato"

### Stati richiesti
- **Default** (5 rows piene, recommendation calcolata)
- **Loading** (skeleton 5 rows con 4 card placeholder ciascuna, shimmer)
- **Empty recommendations** (row "Per te" mostra fallback "Gioca di più per avere consigli personalizzati" + CTA "Esplora libreria") — altre rows restano popolate community-wide
- **Filtered** (entity chip attivo riduce rows visibili a quelle pertinenti)
- **No results search** ("Nessun risultato per '\<query>'" + suggerimenti)
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate
- Recommendation algorithm = placeholder visivo (4 card statiche con badge "Per te") — vera logica post-alpha
- Entity chip filter = mostra/nascondi rows, NON ricarica con query backend (è un filter visuale)
- Search input = no auto-suggest in mockup (basta input + bottone), suggest impl post-MVP

---

## Definition of Done (per ogni mockup wave 3)

Estratto da `_common.md` con additional wave 3:

### Token & visual
- [ ] Solo CSS variables da `tokens.css` (zero hex hardcoded)
- [ ] Helper `entityHsl(entity, alpha?)` inline per i 9 entity color
- [ ] Light + dark mode entrambi funzionanti (`<html data-theme="dark">`)
- [ ] Mobile 375px + desktop 1440px entrambi presenti

### Componenti
- [ ] EntityChip/Pip per ogni reference cross-entity (mai testo semplice)
- [ ] ConnectionBar 1:1 prod (D, E, G2 se applicabile) — `borderRadius: 999`, bg `entityHsl(.1)`, `dashed + opacity 0.6 + Plus` empty, aria-label `${label}: ${count}` non-empty / `${label}` empty
- [ ] MeepleCard riusato (NON ridisegnare card)
- [ ] Tabs animated underline (riusato wave 1)

### Stati
- [ ] Default
- [ ] Empty (illustrazione + CTA)
- [ ] Loading (skeleton, NO spinner generico)
- [ ] Error (messaggio + retry)
- [ ] Stati specifici per mockup (vedi sezioni)

### A11y
- [ ] `role="dialog"` + `aria-modal="true"` su drawer/popover
- [ ] `role="tablist"` + `role="tabpanel"` su tab groups (lezione wave 1 review)
- [ ] `aria-label` su icon-only buttons
- [ ] Focus visibile keyboard (`outline: 2px solid ...` o `--e-ring`)
- [ ] `prefers-reduced-motion` disabilita animazioni inline (lezione wave 1: spesso dimenticato)

### Dati
- [ ] Testo UI in italiano
- [ ] Dati realistici da `data.js` (giochi/player italiani)
- [ ] **NO UUID-like, NO bearer-pattern, NO hex ≥32 char** (lezione PR #568, GitGuardian gate)
- [ ] ID short e leggibili (`p-marco`, `tk-azul`, `kb-wingspan`)

### File hygiene
- [ ] Commento di apertura: nome schermata + route + descrizione 1-riga
- [ ] Nessun TODO o placeholder visibile in UI
- [ ] Nessuna deviazione token non flaggata esplicitamente

## Output handoff (dopo completamento wave 3)

Quando consegni i 5 mockup, produci tabella riepilogo:

| File | Route | Pattern | ConnectionPip count | Nuovi componenti v2 emersi | Deviazioni flaggate |
|------|-------|---------|---------------------|----------------------------|---------------------|
| sp4-player-detail | /players/[id] | Hero+tabs | 6 | PlayerHero, PlayerStatsGrid, AchievementBadgeGrid | win rate culture-indep |
| sp4-toolkit-detail | /toolkits/[id] | Split-view | 6 | ToolkitSummary, VersionHistoryList, ReviewDistribution | reviews gradient semantico |
| sp4-kb-detail | /kb/[id] | Split-view | (no bar) | KbDocumentPreview, ChunksList, KbUsageList | hash truncated middle |
| sp4-game-nights-index | /game-nights | Hero+toggle+body | (no bar) | GameNightCalendarGrid, EventListSection | mobile calendar horizontal scroll |
| sp4-discover | /discover | Hero+rows | (no bar) | DiscoverRow, DiscoverHero | recommendation visual-only |

E lista master nuovi componenti v2 da implementare in PR successive.

## Vincoli non-negoziabili

- ❌ NON ridisegnare ConnectionBar/ConnectionChip/MeepleCard/RecentsBar/MobileBottomBar — sono in produzione
- ❌ NON inventare entity type (restano 9)
- ❌ NON usare grey palette (warm neutrals only)
- ❌ NON includere UUID-like, bearer token, hex string ≥32 char nei dati
- ❌ NON deviare dai token senza flag esplicito + motivazione
- ❌ NON consegnare un mockup senza tutti gli stati richiesti

## File di riferimento da allegare in chat Claude Design

Quando apri la sessione Claude Design (no repo access), allega questi file:

**Obbligatori**:
1. `admin-mockups/briefs/_common.md` — preambolo
2. `admin-mockups/briefs/SP4-wave-3.md` — questo brief
3. `admin-mockups/design_files/tokens.css` — design tokens
4. `admin-mockups/design_files/components.css` — classi base
5. `admin-mockups/design_files/data.js` — dati finti

**Reference visivi (1:1 prod)**:
6. `admin-mockups/design_files/sp4-game-detail.jsx` — wave 1 reference per ConnectionBar 1:1 + tabs
7. `admin-mockups/design_files/sp4-agent-detail.jsx` — wave 1 reference per character sheet (D usa pattern simile)
8. `admin-mockups/design_files/sp4-session-summary.jsx` — wave 2 reference per Hero+body con KPI
9. `admin-mockups/design_files/02-desktop-patterns.html` — split-view e tabs reference
10. `admin-mockups/design_files/03-drawer-variants.html` — drawer side-panel desktop

## Risposta attesa nel thread Claude Design

Per ogni mockup wave 3:
1. Conferma scope ridotto wave 3 (5 mockup, NON tutto SP4)
2. Genera **una risposta = un mockup** (no batch)
3. File completo (HTML + JSX entrambi se complesso, solo HTML se statico)
4. Path salvataggio esplicito: `admin-mockups/design_files/sp4-<name>.{html,jsx}`
5. ConnectionPip[] inline nel JSX (replicato dal brief)
6. Note finali: deviazioni flaggate, nuovi componenti v2 emersi
7. Quando wave 3 completo, tabella handoff

Buon lavoro. Wave 3 chiude SP4 entity-driven; post-merge si passa a wave 4 (index residui) e SP5 admin.
