# SP6 Libro Game — Brief Claude Design (MVP Phase 1, 6 mockup)

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Questo brief introduce **SP6 Libro Game**, scope nuovo fuori dal redesign v2 (SP3/SP4/SP5). Copre la feature "AI Assistant per gamebook narrativi" descritta in `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` §6 (MVP Phase 1).

## Stato programma

| SP | PR | Stato | Audience |
|----|----|-------|----------|
| SP3 public-secondary | merged | ✅ | nuovi visitatori |
| SP4 entity-desktop wave 1+2+3 | merged | ✅ | utenti autenticati |
| SP5 admin-tools | in design | ⏳ | admin/power-user |
| **SP6 libro-game MVP Phase 1** | **(questo brief)** | ⏳ in design | **casual italian gamer (Sara persona)** |

**SP6 = nuova feature, NON redesign**. Aggiunge 3 route mobile-first (`/gamebook`, `/gamebook/upload`, `/gamebook/[gameId]/play`) + flussi modal/drawer (quota credits, house rule). Riusa al 100% il design system esistente (token, EntityChip/Pip, MeepleCard, ConnectionBar, Drawer, BottomBar). Persona primaria: **Sara, 32 anni, GM al tavolo, smartphone in mano** — `mobile-first è obbligatorio`, desktop è adaptation secondary.

## Persona target & contesto d'uso

**Sara, 32, designer freelance, casual gamer**. Gioca 1-2 volte al mese gamebook narrativi (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor Chronicles) **in inglese** con un gruppo di 3-6 amici. Pain: «la partita rischia di morire alla seconda sessione perché traduco live ogni paragrafo».

**Setup tavolo**: salotto, luce media, manuale fisico EN aperto, telefono in mano (single-device GM master), WiFi instabile è la norma. Sara è l'unica utente loggata; gli amici sono ascoltatori passivi.

**4 user goal MVP Phase 1** (vedi vision §2):
- **G1** — Acquisire manuale fotografandolo (50 pag in <5 min, confidence ≥ 0.7 ≥ 95% pagine)
- **G2 ridotto** — Mostra paragrafo setup tradotto (NO checklist interattiva, deferred MVP-1)
- **G3** — Q&A regole con confidence + citation pagina + house rule capability (P95 < 5 sec)
- **G4** — Traduzione narrativa on-demand single paragraph (NO pre-translate chapter, deferred MVP-1)

## Scope SP6 — esattamente 6 mockup

| # | File | Route | Pattern primario | Audience UX |
|---|------|-------|------------------|-------------|
| A | `sp6-libro-game-index.{html,jsx}` | `/gamebook` | Hero + grid + empty | Lista manuali Sara |
| B | `sp6-libro-game-photo-upload.{html,jsx}` | `/gamebook/upload` | Multistep wizard (3 step) | Acquire manuale (G1) |
| C | `sp6-libro-game-play-session.{html,jsx}` | `/gamebook/[id]/play` | Tabbed mobile / Split desktop | Hub session (G3+G4+G2) |
| D | `sp6-libro-game-translation-viewer.{html,jsx}` | `/gamebook/[id]/play/paragraph/[num]` (fullscreen modal) | Reading mode | Lettura ad alta voce (G4) |
| E | `sp6-libro-game-quota-credits.{html,jsx}` | modale + `/gamebook/checkout` success | Multi-step modal | Quota free esaurita (G4.6) |
| F | `sp6-libro-game-house-rule.{html,jsx}` | bottom sheet drawer dentro play-session | Drawer tabbed | Definisci house rule (G3.3, G3.4) |

**Sequence di consegna consigliata**: A → B → C → D → E → F (A/B/C sono il critical path Sara; D/E/F polish flussi laterali).

## Componenti già stabili — NON ridisegnare

Sono in produzione post-SP4. Il mockup li **istanzia** (placeholder JSX che indica il componente prod), non li clona:

| Componente | Path codice | Riuso obbligatorio in SP6 |
|------------|-------------|---------------------------|
| `MeepleCard` (grid/list/compact/featured/hero) | `apps/web/src/components/ui/data-display/meeple-card/` | A (grid manuali), B step 1 (game picker), C tab Setup |
| `EntityChip` / `EntityPip` | `apps/web/src/components/ui/data-display/entity-chip/` | tutti i mockup, ovunque c'è cross-reference |
| `ConnectionBar` + `ConnectionPip` | `apps/web/src/components/ui/data-display/connection-bar/ConnectionBar.tsx` | C (sotto hero session), F (drawer header) |
| `Drawer` (cascadeNavigationStore) | `apps/web/src/stores/cascadeNavigationStore.ts` | F (bottom sheet mobile / side panel desktop) |
| `MobileBottomBar` + `RecentsBar` | `apps/web/src/components/ui/navigation/` | tutti i mockup mobile (shell layout) |
| `Tabs` animated underline (wave 1) | wave 1 SP4 | C (3 tab principali), E (multi-step indicator) |
| `AdvancedFiltersDrawer` | `apps/web/src/components/ui/v2/advanced-filters-drawer/` | non serve in SP6 |

**ConnectionBar reproduction 1:1 prod** (lezione PR #568, #569, #570): `borderRadius: 999` (`var(--r-pill)`), bg pieno = `entityHsl(type, 0.1)` non-empty, dashed + opacity 0.6 + Plus icon empty, aria-label `${label}: ${count}` non-empty / solo `${label}` empty, tabular-nums per count.

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

## Mapping libro game → entity color

Libro game NON introduce nuovi entity type. Mapping su 9 esistenti:

| Concept libro game | Entity | Razionale |
|--------------------|--------|-----------|
| Manuale fotografato | `game` (🎲 orange `25 95% 45%`) | Il manuale rappresenta IL gioco — il "libro" è una rappresentazione del game |
| Pagina indicizzata | `kb` (📄 teal `174 60% 40%`) | Le pagine sono knowledge base chunks |
| Q&A risposta | `chat` (💬 blue `220 80% 55%`) | Le conversazioni Q&A sono chat |
| Citation pagina sorgente | `kb` (📄 teal) | La fonte è KB |
| House rule | `agent` (🤖 amber `38 92% 50%`) | AgentMemory salva le rules — semantica agent |
| Sessione di gioco attiva | `session` (🎯 indigo `240 60% 55%`) | Pulsing indicator quando in-corso |
| Setup checklist (deferred) | `tool` (🔧 cyan `195 80% 50%`) | Quando attivata MVP-1, è uno strumento |
| Crediti / quota | `event` (🎉 rose) per warning urgenti | Quota esaurita = evento |

**Confidence badge** (G1.2, G3.1, G3.3) usa colori semantici NON entity:
- 🟢 Alta (≥ 0.85) → `--c-success` (toolkit green)
- 🟡 Media (0.5-0.85) → `--c-warning` (agent amber)
- 🔴 Bassa (< 0.5) → `--c-danger` (event rose)

## Vincolo dati (lezione PR #568, GitGuardian)

❌ **VIETATO**: token UUID-like (`g6h7i8j9-...`, hex string ≥ 32 char), bearer-like (`sk_test_...`, `eyJ...`), api-key pattern.

✅ **OK**: ID short e leggibili (`gb-tainted-grail`, `pg-12`, `hr-leader-tie`), GUID solo se ovviamente fittizi (`00000000-...-aaaa`).

### Dati realistici da usare nei mockup SP6

**Gamebook reali** (preferiti per fedeltà persona Sara):
| Titolo | Lingua sorgente | Editore | Note tipologia |
|--------|-----------------|---------|----------------|
| Tainted Grail: The Fall of Avalon | EN | Awaken Realms | §-numbered, dark fantasy |
| ISS Vanguard | EN | Awaken Realms | §-numbered, sci-fi |
| Stuffed Fables | EN | Plaid Hat Games | chapter-based, family-friendly |
| Andor Chronicles | EN | Kosmos | chapter-based, fantasy |
| 7th Continent | EN | Serious Poulp | card-based exploration |
| Sleeping Gods | EN | Red Raven | atlas + § hybrid |

**Personaggi/oggetti per glossario** (G4.3 examples):
- Tainted Grail: Niamh, Sword of Avalon → Spada di Avalon, Wraithstone → Pietra Spettrale
- ISS Vanguard: Ardent → Ardente, Discoveries → Scoperte
- Stuffed Fables: Stitch Punk, Pook → Pook (invariato)

**Player names italiani** (gruppo Sara): Marco, Giulia, Davide, Luca, Sara

**Pagine mockup**: usare numeri realistici (p. 4 setup, p. 18 combat, p. 22 critici, p. 47 magia)

**Paragrafi numerati**: §1, §17, §147 (Tainted Grail tipico)

---

## A — Gamebook Index (`sp6-libro-game-index`)

**File**: `sp6-libro-game-index.{html,jsx}`
**Route**: `/gamebook`
**Pattern desktop**: Hero + grid responsive (12-col → 4-col card grid). **Pattern mobile**: hero compatto + 1-col stack scrollable + bottom CTA fixed.

### Hero compatto

- Title "Libri game" (Quicksand `--fs-3xl` mobile / `--fs-4xl` desktop)
- Subtitle KPI row (mono kicker):
  - "{N} manuali indicizzati"
  - "{M} questo mese"
  - "Quota free: {used}/{total} traduzioni"
- CTA primary: "+ Aggiungi manuale" entity color `game` (🎲 orange) — apre `/gamebook/upload`
- Avatar Sara top-right (entity=player pip)

### Quota mini-widget (sotto hero)

- Bar progress visuale 50 pag/mese: width % usata, fill `entityHsl('toolkit', 0.6)` (semantic success) o `entityHsl('event', 0.6)` quando ≥ 90% (semantic danger)
- Text "{used} / {total} paragrafi tradotti questo mese"
- Reset date (kicker mono): "Si resetta il 1° giugno"
- Inline link "Acquista crediti →" se ≥ 90%

### Grid manuali (default state)

Grid responsive: 1-col mobile / 2-col tablet / 3-col desktop / 4-col widescreen.

Ogni card = `<MeepleCard variant="grid" entity="game">`:
- Cover gradient (placeholder, no immagini esterne) — gradient by gameId hash
- Title (Quicksand `--fs-xl` bold)
- Subtitle: editore + anno
- Status pill bottom-left:
  - 🟢 "Pronto" (indexed completato)
  - 🟡 "Indicizzazione…" + spinner micro (24/50 pages)
  - 🔴 "Errore" + link retry
- Counter bottom-right (mono kicker): "47 pag · 142 chunks"
- ConnectionPip strip (max 3 visibili + "+N"):
  - `kb` count chunks indicizzati
  - `chat` count Q&A storiche su questo manuale
  - `session` count sessioni di gioco in cui è stato usato

### Empty state

Quando 0 manuali:
- Illustrazione (gradient placeholder + emoji 📖🎲, NO immagini esterne)
- Title "Nessun manuale ancora"
- Subtitle "Fotografa il tuo primo gamebook per iniziare"
- CTA "📷 Scatta il primo manuale" entity=game prominente
- Sub-link "Come funziona →" verso `/gamebook/how-it-works` (placeholder)

### Stati richiesti

- **Default** (Sara con 4 manuali misti: Tainted Grail ready, ISS Vanguard indicizzazione 24/50, Stuffed Fables ready, Andor Chronicles errore)
- **Empty** (mai indicizzato un manuale, illustrazione + CTA)
- **Loading** (skeleton hero + 4 card placeholder shimmer, NO spinner generico)
- **Error globale** ("Impossibile caricare la libreria — riprova" + CTA retry)
- **Quota soft warning** (47/50 — quota widget rosso `entityHsl('event', 0.6)`)
- **Quota hard limit** (50/50 — banner sticky top "Quota raggiunta" + link checkout)
- **Light + dark** entrambi
- **Mobile 375px + desktop 1440px**

### Deviazioni accettate

- Quota progress bar usa colori semantici (toolkit/event) — uso semantico standard, non nuova palette
- Status pill "Indicizzazione…" può avere shimmer animation; rispetta `prefers-reduced-motion` (disabilita shimmer, mostra static %)

---

## B — Photo Upload + Indexing (`sp6-libro-game-photo-upload`)

**File**: `sp6-libro-game-photo-upload.{html,jsx}`
**Route**: `/gamebook/upload`
**Pattern**: Multistep wizard fullscreen (mobile) / split-view assistito (desktop). 3 step lineari con back button.

**Critical**: questo è IL flow più importante per Sara. Mobile 375px è canonical viewport — desktop è "stesso flow ma più spazio per preview".

### Step indicator (top, sticky)

Segmented progress 3 step:
1. 🎲 Scegli gioco
2. 📷 Scatta pagine
3. ⚙️ Indicizzazione

Step attivo: entity color game pieno + bold. Step completato: check ✓ + opacity 0.6. Step futuro: outline + opacity 0.4.

### Step 1 — Scegli gioco (G1.5 scenario)

**Mobile**: full-screen, search input top sticky, results list verticale.
**Desktop**: split-view (left: search + filtri, right: result preview).

Header:
- Title "Per quale gioco è il manuale?"
- Subtitle "Cerca nel catalogo o aggiungilo"

Search input large (`--fs-lg`):
- Placeholder "Cerca: Tainted Grail, ISS Vanguard…"
- Icon 🔍 left
- Clear button right quando ha valore

Quick filters (chips orizzontali):
- "Tutti" (default attivo) | "I miei giochi" | "Top 50 BGG"

Result list:
- Each row = `<MeepleCard variant="list" entity="game">`:
  - Cover gradient mini (44×56)
  - Title + designer + anno
  - Player count + duration
  - Tap → step 2

Empty results section (G1.5):
- Quando query non match → 3 azioni stacked:
  - 🆕 **Crea nuovo gioco** "{query}" — apre form inline (nome + editore + anno)
  - 🔍 **Cerca su BoardGameGeek** — chiama BGG API placeholder, mostra primi 5 risultati con cover gradient
  - 📖 **Indicizza solo per me** — manuale privato non condiviso al SharedGameCatalog

### Step 2 — Scatta pagine (G1 happy path)

**Mobile**: camera fullscreen (uses native `<input type="file" capture="environment" multiple>`).
**Desktop**: drop-zone area + "Scegli foto da galleria".

Camera viewfinder (mobile):
- Frame tratteggiato `--border-strong` con corner brackets
- Center: "Allinea la pagina" overlay 24% opacity
- Top-right indicators:
  - 💡 Light meter (low/med/high) — warning se low
  - 📐 Page detection (✓ rilevata / ⚠️ non rilevata)
- Bottom controls (3-button row):
  - 🔄 Rifai (left, secondary outline)
  - ⚪ Scatta (center, large 72px, entity=game color pieno)
  - 🖼️ Galleria (right, secondary outline)

Captured strip (sotto viewfinder):
- Horizontal scroll di thumbnails 56×72 con page number overlay
- Each thumb: tap → preview modal con remove action
- Counter top-right: "12 pagine acquisite"

CTA fixed bottom (sticky):
- "Avanti: indicizza {N} pagine" entity=game color pieno
- Disabled se < 5 pagine
- Subtitle "Puoi aggiungere altre pagine dopo"

Permission denied state:
- Replace viewfinder con illustrazione + "Permesso fotocamera negato"
- CTA "Apri impostazioni" + alternativa "Carica da galleria"

### Step 3 — Indicizzazione (G1 progress + G1.2 confidence + G1.4 connection)

Progress globale top:
- Bar full-width fill `entityHsl('game', 0.7)`
- Counter big "{X}/{N} pagine indicizzate" (`--fs-2xl` mono)
- Subtitle "Tempo stimato: {minutes} min"
- Cancel button discreto top-right (apre modal "Sei sicuro? Perderai progresso")

Page grid (sotto progress):
- Grid 3-col mobile / 6-col desktop
- Ogni card = page tile:
  - Thumbnail 80×100 (gradient placeholder con page number overlay big center)
  - Confidence badge bottom-right:
    - 🟢 Alta — `entityHsl('toolkit', 0.9)` pill
    - 🟡 Media — `entityHsl('agent', 0.9)` pill
    - 🔴 Bassa — `entityHsl('event', 0.9)` pill
    - ⏳ In corso — pulsing `entityHsl('game', 0.4)` con shimmer
  - Action overlay on tap-hover:
    - "📷 Rifotografa" (se confidence < 0.5 — G1.2)
    - "👁️ Anteprima" (sempre disponibile post-indexing)

Real-time CTA bottom (sblocca progressivo):
- Disabled finché < 5 pagine alta confidence: "Indicizzazione in corso…"
- Enabled da 5+ pagine alta: "Inizia a giocare ({N} pagine pronte)" — apre `/gamebook/[id]/play`
- Sub-link "Continua a fotografare" → step 2 (aggiungi pagine)

Banner stati eccezionali:
- **Connection lost** (G1.4) banner top giallo: "🟡 Connessione interrotta — riprenderò automaticamente"
- **Connection restored** banner top verde 3s autodismiss: "🟢 Connessione tornata, ripresi {N} upload"
- **OCR fail** (G1.6 CJK) banner red: "Lingua non supportata: {detected}. Pipeline corrente: latin script (EN/IT/DE/FR/ES/PT/NL)" + 3 azioni inline
- **Duplicate detection** (G1.3) banner amber: "{N} pagine sembrano duplicate" + "Rivedi" CTA → modal con side-by-side pagine sospette

### Stati richiesti

- **Step 1 default** (search vuoto, suggerimenti top giochi BGG)
- **Step 1 typing** (live results 3-5 match)
- **Step 1 no results** (3 azioni: crea / BGG / privato)
- **Step 1 BGG search loading** (skeleton 3 card)
- **Step 2 camera ready** (viewfinder live + 0 pagine)
- **Step 2 camera permission denied** (illustrazione + alternative)
- **Step 2 capturing** (8 pagine in strip)
- **Step 3 indexing in-progress** (24/50 mixed confidence)
- **Step 3 indexing partial-fail** (3 pagine red confidence + retake action)
- **Step 3 indexing complete** (50/50 tutte verdi, CTA "Inizia a giocare" prominente)
- **Step 3 connection lost mid-upload** (banner giallo + auto-resume)
- **Step 3 cancel modal** ("Perderai 24 pagine indicizzate")
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate

- Camera viewfinder è placeholder static (CSS frame + dummy image gradient) — non vero stream camera nel mockup. Aggiungi note "Real camera via getUserMedia / native file picker"
- Page grid usa CSS subgrid se disponibile, fallback flexbox
- Real-time progress polling = mockup mostra static state finale + 1 state intermedio (24/50)

---

## C — Play Session (`sp6-libro-game-play-session`)

**File**: `sp6-libro-game-play-session.{html,jsx}`
**Route**: `/gamebook/[gameId]/play`
**Pattern desktop**: Split-view (left 380px game-context + tabs nav, right flex content area).
**Pattern mobile**: Tabbed fullscreen, segmented control top, content area fills, BottomBar nav transformed in **Session Bar** (entity=session pulsing pill + quick actions).

**Critical**: questa è la schermata che Sara usa per 4 ore consecutive. Massima leggibilità in luce ambiente medio-bassa, font sufficienti per lettura distante (≥ 18pt body).

### Header session (sticky top)

- Game title (Quicksand `--fs-2xl`)
- Game pip entity=game (cover mini + emoji 🎲)
- Session status: "🎯 In corso · iniziata 47 min fa" (mono kicker, entity=session color)
- Menu icon 3-dots → drawer azioni session (chiudi, esci, condividi)

### ConnectionBar (sotto header)

```ts
const connections: ConnectionPip[] = [
  { entityType: 'kb',      count: 142, label: 'Pagine indicizzate', icon: FileText,    isEmpty: false },
  { entityType: 'chat',    count: 8,   label: 'Domande oggi',       icon: MessageCircle,isEmpty: false },
  { entityType: 'agent',   count: 2,   label: 'House rules attive', icon: Bot,         isEmpty: false },
  { entityType: 'tool',    count: 0,   label: 'Setup checklist',    icon: Wrench,      isEmpty: true  }, // Phase 1: vuoto perché G2 ridotto
  { entityType: 'player',  count: 4,   label: 'Giocatori al tavolo', icon: User,       isEmpty: false },
  { entityType: 'session', count: 1,   label: 'Sessione attiva',    icon: Target,      isEmpty: false },
];
```

### Tabs principali (segmented control, animated underline come wave 1)

3 tab equal-weight:
1. 💬 **Chat regole** (default, entity=chat)
2. 📖 **Paragrafo** (entity=kb)
3. 🎲 **Setup** (entity=tool, ridotto Phase 1)

### Tab 1 — Chat regole (G3 default attivo)

Conversation pattern (ChatGPT-style):
- Bubble agent (left): bg `entityHsl('agent', 0.08)`, max-width 80%, Quicksand `--fs-base`, line-height 1.6
- Bubble user (right): bg `entityHsl('chat', 1)` solid, white text, max-width 80%
- Avatar agent left = entity=agent pip (🤖 amber)

Ogni risposta agent contiene:
- **Risposta principale** (testo IT con tono naturale, ≤ 120 parole)
- **Citation row** (sotto risposta):
  - `<EntityChip entity="kb" size="sm">` "📖 p.18 — Combattimento Marziale"
  - Tap → apre preview modal con thumbnail pagina (gradient placeholder con page number big)
- **Confidence badge** (right-aligned):
  - 🟢 Alta · 🟡 Media · 🔴 Bassa
  - Con tooltip on hover: "Confidence 0.92 da KB chunk #47"
- **Inline actions** (chip row sotto risposta):
  - "👍 Utile" / "👎 Non chiara" — telemetry user-flagged
  - "🔁 Rigenera"
  - "📋 Copia"

States di risposta speciali:
- **Ambiguity** (G3.2): risposta testo "La tua domanda può riferirsi a 4 contesti diversi:" + 4 chip selezionabili `<EntityChip entity="kb">` — Combattimento, Iniziativa, Voto, Esplorazione. Tap chip → re-query con disambig.
- **Low confidence** (G3.3): testo "Non sono sicuro — questa regola non è chiara dal manuale." + confidence badge 🟡/🔴. Action chip row:
  - "📷 Rifotografa pagina" → apre Step 2 di B con pre-filtro pagina specifica
  - "🤝 Definisci house rule" → apre drawer F
  - "🔍 Cerca su BoardGameGeek" → external link placeholder
- **House rule applicata** (G3.4): risposta principale "Per il vostro gruppo: doppio danno (house rule definita 14 min fa)." + footnote citation: "📖 Regola ufficiale manuale: +50% danno (p.22) — ma state usando la vostra variante" + chip badge `<EntityChip entity="agent">` "House rule attiva"
- **Latency timeout** (G3.5): typing indicator agent bubble "🟡 Sto pensando, ci vuole più del solito…" + dopo 8s diventa "Connessione lenta — passa a modalità manuale" con CTA "📖 Mostrami le pagine sul tema X"

Input bar bottom (sticky):
- Text input placeholder "Chiedi qualcosa sulle regole…"
- Voice input mic button right (G4.5 voice-to-text — placeholder, low expectation)
- Send button entity=chat color pieno

### Tab 2 — Paragrafo (G4 default)

Top toolbar:
- Input "Vai al §" (mono, type=number, large 18pt)
- OR Search input "Cerca testo nel manuale…" (per chapter-based G4.5)
- Toggle EN/IT (segmented control, IT default)

Body quando paragrafo attivo:
- Header big: "§147" (`--fs-4xl` mono entity=kb color) + chapter name (`--fs-lg` Quicksand)
- Translation body:
  - Font Nunito **`24px` (=18pt)** assoluto — **deviazione esplicita** dalla scala token (`--fs-lg` è 17px ≈ 13pt, sotto soglia accessibilità vision §4.2 ≥ 18pt). NON usare `--fs-base` né `--fs-lg`.
  - Line-height 1.6
  - Max-width 60ch (centered su desktop, full-width mobile)
  - Dialogue lines italic + entity=kb color (`entityHsl('kb', 0.8)`)
- Cache indicator top-right: `<EntityChip entity="kb" size="sm">` "📦 Cached" se già tradotto
- Glossary applied indicator (G4.3): footnote piccola "Glossario applicato: Spada di Avalon, Pietra Spettrale, Niamh"

Bottom controls:
- "← § precedente" | "Letto, vai al prossimo →" (entity=kb buttons outline)
- Font size adjuster: "A− A A+" (a11y)
- Open fullscreen button → apre mockup D

States speciali:
- **Empty** (no paragrafo aperto): illustrazione + "Inserisci un numero § o cerca un testo per iniziare"
- **Not found** (G4.4): "Paragrafo §299 non trovato nel manuale indicizzato." + 3 azioni:
  - "🔍 Cerca testo simile" (mostra paragrafi vicini §290-§300)
  - "📷 Rifotografa pagine mancanti"
  - "⏭️ Salta paragrafo"
- **Translation loading**: skeleton paragraph (3 righe shimmer)
- **Offline cached** (G4.7 partial): "🟡 Connessione persa — paragrafo originale disponibile" banner top + CTA "Mostra EN"
- **Quota exceeded** (G4.6): translation body sostituito da blocco "Hai esaurito la quota gratuita" + CTA `entity=event` "💎 Acquista crediti" → apre mockup E

### Tab 3 — Setup (G2 ridotto MVP Phase 1)

**MVP Phase 1 = "Mostra paragrafo setup tradotto" (NO checklist interattiva).**

Top:
- Number giocatori segmented control: 2 / 3 / 4 / 5 / 6 (default 4)
- CTA "Genera guida setup" entity=tool color

Risultato (dopo tap CTA):
- Header "Setup per 4 giocatori" + citation `<EntityChip entity="kb">` "📖 p.4 — Setup base"
- Translation body (stesso style del Tab 2):
  - Paragrafo setup tradotto IT, font Nunito 18pt+, line-height 1.6
  - Max-width 60ch
- Glossary applied footnote
- Disclaimer pill bottom (entity=tool, opacity 0.7): "💡 Checklist interattiva con step disponibile in MVP-1"

States:
- **Default empty** (player picker selezionato 4, ma non generato ancora — illustrazione + CTA)
- **Generating** (skeleton + "Generazione setup… 12s")
- **Generated** (paragraph reso)
- **Setup unclear** (G2.3): "Non riesco a identificare una sezione setup chiara" + 3 opzioni:
  - "✅ Cerca nel testo generale"
  - "📷 Indica tu le pagine setup"
  - "⏭️ Salta e inizia direttamente"

### Bottom: Session Bar trasformata (mobile)

Sostituisce normale BottomBar quando session in-corso (vedi pattern in `mobile-app.jsx` esistente):
- Bg gradient session color subtle (`entityHsl('session', 0.06)` → `entityHsl('session', 0.12)`)
- Pulsing dot session-color (rispetta `prefers-reduced-motion`)
- Quick actions: "💬 Chat" | "📖 §147" (paragrafo corrente) | "🎲 Setup" | "⏸️ Pausa session"
- Tap "Pausa session" → modal conferma "Vuoi mettere in pausa? La sessione resta attiva, le risposte cached restano."

### Stati richiesti (riepilogo)

- **Tab Chat default** (3 Q&A storiche, ultima high-confidence con citation)
- **Tab Chat ambiguity** (G3.2 con 4 chip)
- **Tab Chat low-confidence** (G3.3 con 3 azioni)
- **Tab Chat house-rule applied** (G3.4 con badge + footnote)
- **Tab Chat timeout** (G3.5 con typing indicator + fallback)
- **Tab Chat empty** (prima domanda della sessione)
- **Tab Paragrafo default** (§147 caricato, glossary applied)
- **Tab Paragrafo not-found** (G4.4 §299)
- **Tab Paragrafo offline cached** (G4.7)
- **Tab Paragrafo quota exceeded** (G4.6 banner)
- **Tab Setup default** (player picker 4)
- **Tab Setup generated** (paragraph rendered)
- **Tab Setup unclear** (G2.3 con 3 opzioni)
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate

- Voice-to-text mic button = placeholder visivo (non implementa real Web Speech API nel mockup)
- Setup tab `--fs-lg` per body = stesso pattern Tab 2, intenzionale (lettura ad alta voce)
- Pulsing session dot rispetta `prefers-reduced-motion` con fallback static (lezione SP4 wave 1)

---

## D — Translation Viewer Fullscreen (`sp6-libro-game-translation-viewer`)

**File**: `sp6-libro-game-translation-viewer.{html,jsx}`
**Route**: `/gamebook/[gameId]/play/paragraph/[num]` (fullscreen modal route, no shell)
**Pattern**: Reading mode fullscreen, minimalista, focus assoluto sul testo.

**Use case**: Sara apre §147 dal Tab Paragrafo del play-session, tap "fullscreen" → schermata dedicata leggibile distante 1.5m da chi è seduto al tavolo.

### Top bar (sticky, minimal)

- Left: ← back button → torna a `/gamebook/[id]/play` (Tab Paragrafo)
- Center: "§147" (mono `--fs-xl`)
- Right: 3-dots menu → drawer azioni:
  - "🌐 Mostra originale (EN)"
  - "📋 Copia traduzione"
  - "🎙️ Leggi tu (TTS)" — disabled, badge "v2"
  - "🐛 Segnala traduzione errata"

### Body (focus reading)

Container max-width 64ch, centered, padding `--s-7`+

Header big (sticky condizionale, hide on scroll down):
- Chapter name (`--fs-lg` Quicksand muted)
- Paragraph number "§147" (`--fs-3xl` mono entity=kb)

Translation body:
- **Font: Nunito `26px` (=20pt) assoluto — deviazione esplicita** dalla scala token (`--fs-xl` è 20px ≈ 15pt, sotto soglia "lettura distante" vision §4.2 ≥ 24pt opt. Default reading 24px=18pt, "lettura distante" 32px=24pt). 26px è bilanciamento default.
- Line-height: **1.6** (esplicito, no default)
- Max-width: 60ch
- Color: `--text` (high contrast)
- Dialogue lines: italic + `entityHsl('kb', 0.85)` color
- Paragraph spacing: `--s-5` between paragrafi narrativi

Toggle EN/IT mode:
- Segmented control bottom: `IT` (default attivo) | `EN` | `IT + EN side-by-side`
- Side-by-side: 2 colonne 50/50, IT a sinistra, EN a destra (desktop only — mobile fa stack verticale)

### Bottom controls (sticky)

3 row di control:
1. **Navigazione paragrafi**: ← § precedente · "Letto, vai a §148 →"
2. **Reading aids**:
   - Font size: A− A A+ (3 step, A−=24px/18pt, A=26px/20pt default, A+=32px/24pt "lettura distante")
   - Line spacing: ≡ (toggle 1.4 / 1.6 / 1.8)
   - Theme: 🌗 (light/dark/sepia — sepia è warm sand `--bg` already, ok)
3. **TTS placeholder** (disabled):
   - "🎙️ Leggi tu" button outline opacity 0.4
   - Tooltip "Disponibile in v2"

### States di body speciali

- **Default IT** (testo italiano formattato lettura)
- **Side-by-side IT+EN** (desktop only, 2 col stretched)
- **Offline cached** (G4.7 partial): banner top "🟡 Connessione persa — testo cached" + auto-retry indicator discreto
- **Mid-translation lost** (G4.7): mostra ultime righe ricevute + "🟡 Sto riprovando…" inline + "Mostra EN" CTA
- **Not found** (G4.4): full-screen empty state "§299 non trovato" + 3 azioni
- **Quota exceeded triggered**: redirect automatico modale E (NON inline)

### Stati richiesti

- **Default IT** (Tainted Grail §147, glossary applied: Niamh, Spada di Avalon, Pietra Spettrale)
- **Side-by-side IT+EN** (desktop only)
- **EN replace** (mostra solo EN, kicker top "Originale")
- **Loading** (skeleton 5 righe shimmer)
- **Offline cached** (G4.7 banner)
- **Mid-translation lost** (G4.7 partial + retry indicator)
- **Not found** (G4.4 §299)
- **Light + dark + sepia** (sepia è light theme con bg `--bg` already)
- **Mobile + desktop**

### Deviazioni accettate

- Font size 20pt è 4pt sopra `--fs-base` — necessario per lettura distante 1.5m (vincolo non-functional §4.2)
- Sepia theme = light theme as-is (warm sand `--bg`), NO nuova palette
- TTS button placeholder è OK essere disabled — feature roadmap v2 (vision §4.9), label "v2" esplicito

---

## E — Quota Credits Checkout (`sp6-libro-game-quota-credits`)

**File**: `sp6-libro-game-quota-credits.{html,jsx}`
**Route**: modale centrale + `/gamebook/checkout/success` page
**Pattern**: Multi-step modal (4 step lineari) con close X top-right.

**Use case G4.6**: Sara ha consumato 50/50 paragrafi gratis questo mese, apre il 51° → modal blocca il read flow ma offre escape graceful.

### Step 1 — Quota raggiunta (warning hard)

Header:
- Icon hero centro: 💎 (size 64px, color entity=event amber-rose) — **rispetta semantic warning**
- Title (Quicksand `--fs-2xl`): "Quota raggiunta"
- Subtitle: "Hai tradotto 50 paragrafi questo mese. La quota gratuita si resetta il 1° giugno."

Visual quota bar:
- Container width 100% padding `--s-5`
- Bar progress 50/50 fill 100% `entityHsl('event', 0.7)`
- Counter mono large "50 / 50" + "paragrafi tradotti"
- Subtitle "Reset tra 12 giorni" (mono kicker)

CTA stack (G4.6 simplified Phase 1):
- **Primary**: "💎 Acquista 100 crediti (€5)" entity color event color, full-width — apre step 2
- **Secondary**: "⏸️ Continua senza traduzione" outline — chiude modal e mostra solo testo EN nel viewer (D)

Footer link discreto:
- "Cosa sono i crediti? →" (apre tooltip o info modal)

### Step 2 — Credit pack picker

Header:
- Title "Scegli il tuo pacchetto"
- Subtitle "Più paragrafi = miglior prezzo"

3 card pack selezionabili (radio behavior):
| Pack | Price | Crediti | Costo/par | Badge |
|------|-------|---------|-----------|-------|
| Starter | €5 | 100 crediti | €0.05 | "Più popolare" |
| Mid | €20 | 500 crediti | €0.04 | — |
| Pro | €35 | 1000 crediti | €0.035 | "Risparmia 30%" |

Card design:
- Border `--border-strong` quando selected, `--border` default
- Bg `entityHsl('toolkit', 0.06)` quando selected
- Crediti big number top
- Price `--fs-2xl` mono
- Costo/par mono kicker subtle

Disclaimer footer:
- "I crediti non scadono. La quota free di 50 paragrafi si resetta automaticamente ogni mese."
- Privacy link "Privacy & GDPR →"

CTA bottom:
- "Continua →" entity=toolkit color (semantic success per checkout flow)
- Disabled finché nessun pack selected
- Total recap right "Totale: €5"

### Step 3 — Checkout placeholder (Stripe Elements style)

Header:
- Title "Pagamento"
- Subtitle "Pacchetto Starter · 100 crediti · €5"

Payment form (placeholder visual, NON real Stripe Elements):
- "Numero carta" input (placeholder `1234 5678 9012 3456`, mostra 4 dummy `•••• •••• •••• 4242`)
- Riga 2: "Scadenza MM/AA" + "CVC"
- "Nome sulla carta"
- Country selector (default Italia)

Riepilogo ordine (collapsible):
- "100 crediti": €5.00
- "IVA inclusa": —
- "Totale": **€5.00** (mono bold)

Trust badges row:
- 🔒 "SSL secure"
- ✓ "Stripe powered"
- ✓ "Politica rimborso 14gg"

CTA bottom:
- "Paga €5" entity=toolkit color full-width
- Loading state: spinner + "Elaborazione…"
- Cancel link discreto "← Torna ai pacchetti"

### Step 4 — Success

Hero centered:
- Icon: 🎉 (animato, rispetta `prefers-reduced-motion` → static fallback)
- Title (`--fs-3xl`): "Crediti aggiunti!"
- Subtitle: "100 crediti aggiunti al tuo account. Buon gioco!"

Recap card (mono breakdown chiaro):
- "Crediti precedenti": 0
- "Crediti acquistati": +100
- **"Bilancio crediti totale": 100** (`--fs-2xl` bold, entity=toolkit color)
- "Quota free questo mese": 50/50 (resetta 1° giugno)
- "1 paragrafo = 1 credito"

CTA primary:
- "Torna al gioco →" entity=session color (riprende `/gamebook/[id]/play`)

Sub-link:
- "Vedi ricevuta" → email dispatch placeholder

### States di errore

- **Step 3 payment failed**:
  - Banner red "Pagamento rifiutato — riprova o cambia metodo"
  - Specific error sub-text (es. "Carta scaduta", "Fondi insufficienti")
  - CTA "Riprova" + "Cambia carta"
- **Step 3 stripe down** (3rd party):
  - Banner amber "Provider pagamento momentaneamente non disponibile"
  - CTA "Riprova fra 1 minuto" + "Contatta supporto"
- **Step 4 partial credit issue** (race condition):
  - Banner "Pagamento ricevuto, accredito in corso… di solito istantaneo, se non vedi i crediti entro 5 min contattaci"

### Soft warning state (separato dai 4 step)

NON è un step, è un **modal preventivo** che appare alla 47ª/50 traduzione (warning soft prima del hard limit):
- Modal small bottom (toast-like su mobile, modal centro desktop)
- "🟡 Quasi alla fine della quota"
- "Hai usato 47/50 paragrafi gratis. Restano 3."
- 2 CTA: "Acquista crediti ora" entity=toolkit / "Ok, continua"

### Stati richiesti

- **Step 1 quota raggiunta** (50/50)
- **Step 2 pack picker** (Starter selected default)
- **Step 3 checkout form** (filled)
- **Step 3 payment loading** (spinner)
- **Step 3 payment failed** (banner red)
- **Step 4 success** (con animation 🎉)
- **Soft warning 47/50** (toast/modal)
- **Light + dark**
- **Mobile + desktop**

### Deviazioni accettate

- Stripe form = placeholder visual (4242 test card example), non integra real Stripe Elements nel mockup
- Animation 🎉 step 4 rispetta `prefers-reduced-motion` (lezione SP4 wave 2 confetti CSS-only)
- Icon hero 💎 step 1 può usare gradient `entityHsl('event', 0.6)` → `entityHsl('toolkit', 0.4)` — uso semantico misto warning→solution intenzionale

---

## F — House Rule Drawer (`sp6-libro-game-house-rule`)

**File**: `sp6-libro-game-house-rule.{html,jsx}`
**Route**: bottom sheet drawer dentro `/gamebook/[gameId]/play` (NO route propria)
**Pattern**: Drawer tabbed bottom sheet mobile / side panel desktop (canonical drawer pattern, vedi `03-drawer-variants.html`).

**Use case G3.3 + G3.4**: trigger dalla risposta low-confidence Q&A nel Tab Chat di C → "🤝 Definisci house rule". Drawer si apre con 2 tab: **Crea** | **Le tue rules**.

### Drawer header

- Drag handle top (mobile only)
- Icon hero: 🤝 (entity=agent amber)
- Title "House rules" (Quicksand `--fs-xl`)
- Game pip entity=game right "Tainted Grail"
- Close button X top-right

### ConnectionBar (sotto header, 4 pip)

```ts
const connections: ConnectionPip[] = [
  { entityType: 'agent',   count: 2,  label: 'Rules attive',  icon: Bot,         isEmpty: false },
  { entityType: 'game',    count: 1,  label: 'Gioco target',  icon: Dice5,       isEmpty: false },
  { entityType: 'session', count: 8,  label: 'Applicate in',  icon: Target,      isEmpty: false },
  { entityType: 'kb',      count: 12, label: 'Sostituiscono', icon: FileText,    isEmpty: false },
];
```

### Tabs (2 tab, animated underline)

1. **Crea** (default attivo se trigger da chat low-confidence)
2. **Le tue rules** (default attivo se aperto da menu manuale)

### Tab 1 — Crea house rule

Form layout:

**Sezione 1 — Domanda originale (read-only echo)**
- Card sunken bg `--bg-sunken` border light
- Label kicker mono "DOMANDA"
- Quote text: "Se due giocatori muoiono nello stesso turno, chi diventa il leader?"
- Footer: kicker mono "Posto da Marco · 14 min fa"

**Sezione 2 — Regola ufficiale manuale (citation)**
- Card border-light bg `entityHsl('kb', 0.04)`
- Label kicker mono "REGOLA UFFICIALE — POCO CHIARA"
- Excerpt text 3 righe del manuale (italic)
- Citation chip `<EntityChip entity="kb" size="sm">` "📖 p.34 — Stato del gruppo"
- Footer disclaimer: "⚠️ La nostra confidence per questa regola è 0.42 (bassa)"

**Sezione 3 — La nostra regola** (input principale)
- Label "La nostra regola"
- Textarea max 280 caratteri, rows 4
- Placeholder: "Es: 'In caso di morte simultanea, chi ha più punti vita massimi diventa leader. Se pareggio, decide il giocatore con più sessioni completate.'"
- Counter caratteri bottom-right mono
- Helper text "Sii specifico — tutti i casi futuri useranno questa regola"

**Sezione 4 — Tag opzionali (collapsed default)**
- Toggle "+ Aggiungi tag" expand inline
- Tags input: situazione (Combat / Movimento / Esplorazione / Voto / Altro)
- Optional: "Quando applicare" (Sempre / Solo questa sessione)

**CTA bottom (sticky)**:
- "Salva house rule per Tainted Grail" entity=agent color full-width
- Disabled se textarea < 10 caratteri
- Sub-link discreto "Annulla" close drawer

### Tab 2 — Le tue rules (list view)

Header section per gioco (collapsible, default expanded):
- Game pip + name + counter "{N} rules"

Each rule card:
- Title kicker mono "REGOLA"
- Body: la regola completa (truncate 3 righe)
- Original question footer: "📚 Originata da: '...'" (truncate 1 riga)
- Citation row: `<EntityChip entity="agent">` agent + `<EntityChip entity="kb">` page ref ufficiale
- Stats row mono kicker:
  - "Applicata 3 volte"
  - "Creata 2 giorni fa da Sara"
- Action row icons:
  - ✏️ Modifica → riapre tab Crea pre-popolato
  - 🗑️ Elimina → confirm dialog
  - 📤 Condividi → share placeholder

Empty state:
- Illustrazione + emoji 🤝
- "Nessuna house rule definita per Tainted Grail"
- Subtitle "Le rules personalizzate sostituiscono o estendono il manuale ufficiale per il vostro gruppo"
- CTA "+ Crea la prima rule" entity=agent

### Saved success state (transition Tab 1 → Tab 2)

Dopo Salva click:
- Tab 1 collapse animato
- Toast bottom (entity=agent color, 3s autodismiss): "✅ House rule salvata · Marco e gli altri vedranno questa regola nelle prossime risposte"
- Auto-switch a Tab 2 con la rule appena creata in highlight (bordo `entityHsl('agent', 0.6)` 2px, fade dopo 2s)

### Stati richiesti

- **Drawer closed** (mostrato come "trigger button" nel parent screen C)
- **Tab Crea empty form** (textarea vuota, CTA disabled)
- **Tab Crea typed** (~150 char, CTA enabled)
- **Tab Crea saving** (spinner inline + CTA disabled)
- **Tab Crea saved success** (transition + toast + Tab 2)
- **Tab Le tue rules con 3 rules** (Tainted Grail 2 + ISS Vanguard 1, group headers)
- **Tab Le tue rules empty** (illustrazione + CTA)
- **Tab Le tue rules edit modal** (riapre Crea pre-popolato)
- **Tab Le tue rules delete confirm** (modal "Elimina house rule? Le risposte future tornano al manuale ufficiale.")
- **Light + dark**
- **Mobile bottom sheet (drag-to-close)** + **Desktop side panel right 380px**

### Deviazioni accettate

- Drag-to-close mobile = riusa pattern `vaul` (placeholder JSX, vero impl userà `<DrawerProvider>` esistente)
- Toast saved success = riusa pattern toast esistente (lib `react-hot-toast` placeholder)
- Edit pre-populated form = same Tab 1 structure, no nuovo componente

---

## Definition of Done (per ogni mockup SP6)

Estratto da `_common.md` con additional SP6:

### Token & visual

- [ ] Solo CSS variables da `tokens.css` (zero hex hardcoded)
- [ ] Helper `entityHsl(entity, alpha?)` inline per i 9 entity color
- [ ] Light + dark mode entrambi funzionanti (`<html data-theme="dark">`)
- [ ] **Mobile 375px è canonical viewport SP6** (la persona è mobile-first)
- [ ] Desktop 1440px presente come adaptation secondary

### Componenti

- [ ] EntityChip/Pip per ogni reference cross-entity (mai testo semplice)
- [ ] ConnectionBar 1:1 prod (C, F) — `borderRadius: 999`, bg `entityHsl(.1)`, `dashed + opacity 0.6 + Plus` empty, aria-label `${label}: ${count}` non-empty / `${label}` empty
- [ ] MeepleCard riusato (NON ridisegnare) per A, B step 1
- [ ] Drawer pattern (F) usa cascadeNavigationStore, `vaul` mobile / Radix Dialog + custom desktop
- [ ] Tabs animated underline (C, F)

### Stati

- [ ] Default
- [ ] Empty (illustrazione + CTA, no spinner)
- [ ] Loading (skeleton, NO spinner generico)
- [ ] Error (messaggio + retry)
- [ ] Stati specifici per mockup (vedi sezioni — minimo 6 state per C)

### A11y (critico SP6, lettura distante 1.5m al tavolo)

- [ ] Body text reading mode ≥ 18pt (D fullscreen 20pt+)
- [ ] Touch target ≥ 48×48 dp (G inputs camera step 2 viewfinder)
- [ ] Contrasto WCAG AA (≥ 4.5:1) testato in light+dark
- [ ] `role="dialog"` + `aria-modal="true"` su drawer F + modal E
- [ ] `role="tablist"` + `role="tabpanel"` su tab groups (C, F)
- [ ] `aria-label` su icon-only buttons (camera shutter B, mic input C)
- [ ] Focus visibile keyboard (`outline: 2px solid ...` o `--e-ring`)
- [ ] `prefers-reduced-motion` disabilita: pulsing session dot (C), shimmer indexing (B), animation 🎉 (E step 4)

### Dati

- [ ] Testo UI in italiano
- [ ] Gamebook reali (Tainted Grail, ISS Vanguard, Stuffed Fables, Andor Chronicles, 7th Continent)
- [ ] Player names italiani (Marco, Giulia, Davide, Luca, Sara)
- [ ] Glossario realistico (Niamh, Spada di Avalon, Pietra Spettrale)
- [ ] Pagine numerate realistiche (p.4 setup, p.18 combat, §147 narrative)
- [ ] **NO UUID-like, NO bearer-pattern, NO hex ≥32 char** (lezione PR #568, GitGuardian gate)
- [ ] **NO real Stripe key** in mockup E (placeholder visual `pk_test_...` evitato — usa "Stripe Elements placeholder" comment)

### Performance perception (UX)

- [ ] B step 3 indexing: progressive sblocco CTA "Inizia a giocare" da 5 pagine alta confidence (no all-or-nothing)
- [ ] C Tab Chat: typing indicator agent < 1 sec dopo invio user
- [ ] C Tab Paragrafo: cache indicator `📦 Cached` esplicito quando hit cache
- [ ] D translation viewer: skeleton 5 righe immediate, no full-screen blocking spinner
- [ ] E step 3 checkout: loading state esplicito su CTA "Paga"

### File hygiene

- [ ] Commento di apertura: nome schermata + route + descrizione 1-riga + persona target ("MVP Phase 1 — Sara casual GM")
- [ ] Nessun TODO o placeholder visibile in UI
- [ ] Nessuna deviazione token non flaggata esplicitamente
- [ ] HTML standalone runnable (link `tokens.css` + `components.css` esistenti)
- [ ] JSX runnable via Babel standalone (pattern `01-screens.html`)

---

## Output handoff (dopo completamento SP6)

Quando consegni i 6 mockup, produci tabella riepilogo:

| File | Route | Pattern | ConnectionPip count | Nuovi componenti v2 emersi | Deviazioni flaggate |
|------|-------|---------|---------------------|----------------------------|---------------------|
| sp6-libro-game-index | /gamebook | Hero+grid | (no bar globale) | LibroGameCard, QuotaWidget | quota colors semantici |
| sp6-libro-game-photo-upload | /gamebook/upload | 3-step wizard | (no bar) | PhotoCaptureViewfinder, ConfidenceBadge, IndexingProgressGrid, BgGameSearchPicker | camera placeholder static |
| sp6-libro-game-play-session | /gamebook/[id]/play | Tabs mobile / Split desktop | 6 | QAChatBubble, CitationChip, ParagraphInput, SetupGenerator | session pulsing reduced-motion |
| sp6-libro-game-translation-viewer | /gamebook/[id]/play/paragraph/[num] | Reading fullscreen | (no bar) | TranslationReadingMode, FontSizeAdjuster, SidebySideToggle | font 20pt > base, sepia=light |
| sp6-libro-game-quota-credits | modale + /checkout/success | Multi-step modal | (no bar) | QuotaModal, CreditPackPicker, CheckoutPlaceholder, SuccessConfetti | Stripe placeholder, gradient hero icon |
| sp6-libro-game-house-rule | drawer dentro play-session | Drawer tabbed | 4 | HouseRuleForm, HouseRulesList, RuleCard | edit reuses Crea form |

E lista master nuovi componenti SP6 da implementare in PR successive (target: `apps/web/src/components/ui/v2/libro-game/`).

---

## Vincoli non-negoziabili SP6

- ❌ NON ridisegnare ConnectionBar/ConnectionChip/MeepleCard/RecentsBar/MobileBottomBar/Drawer — sono in produzione
- ❌ NON inventare entity type (restano 9, libro game mappa su game/kb/chat/agent/session/event)
- ❌ NON usare grey palette (warm neutrals only)
- ❌ NON includere UUID-like, bearer token, hex string ≥32 char nei dati (incluso step 3 E checkout — placeholder visual only)
- ❌ NON deviare dai token senza flag esplicito + motivazione
- ❌ NON consegnare un mockup senza tutti gli stati richiesti (in particolare error/edge: G1.4 connection lost, G3.3 low-confidence, G3.5 timeout, G4.4 not-found, G4.6 quota, G4.7 mid-translation lost)
- ❌ NON usare immagini esterne (gradient placeholder o emoji solo)
- ❌ NON font fuori da Quicksand/Nunito/JetBrains Mono
- ❌ NON scrivere stringhe inglesi UI (libro game è IT-first MVP Phase 1, EN solo per testo originale paragrafo)
- ❌ NON cambiare body font size del play session reading mode (≥ 18pt obbligatorio per accessibilità lettura distante — vincolo non-functional vision §4.2)

---

## File di riferimento da allegare in chat Claude Design

Quando apri la sessione Claude Design (no repo access), allega questi file:

**Obbligatori (preambolo)**:
1. `admin-mockups/briefs/_common.md` — preambolo design system
2. `admin-mockups/briefs/SP6-libro-game.md` — questo brief
3. `admin-mockups/design_files/tokens.css` — design tokens
4. `admin-mockups/design_files/components.css` — classi base
5. `admin-mockups/design_files/data.js` — dati finti (estendere con gamebook reali)

**Spec source (vincoli funzionali)**:
6. `docs/superpowers/specs/2026-05-04-libro-game-assistant-vision.md` — vision completa (specialmente §1 persona, §2 user goal, §3 Gherkin scenarios, §6 MVP Phase 1 scope)

**Reference visivi 1:1 prod (riuso pattern componenti)**:
7. `admin-mockups/design_files/sp4-game-detail.jsx` — wave 1 reference per ConnectionBar 1:1 + tabs animated underline
8. `admin-mockups/design_files/sp4-agent-detail.jsx` — wave 1 reference per character sheet pattern (riuso parziale in C session header)
9. `admin-mockups/design_files/sp4-session-live.jsx` — wave 2 reference per Session Bar transformed (mobile bottom bar quando session attiva — pattern usato in C)
10. `admin-mockups/design_files/sp4-kb-detail.jsx` — wave 3 reference per chunks/preview split-view (parte del pattern C tab Paragrafo)
11. `admin-mockups/design_files/03-drawer-variants.html` — drawer tabbed bottom-sheet canonica (F)
12. `admin-mockups/design_files/02-desktop-patterns.html` — split-view desktop (C, D)
13. `admin-mockups/design_files/mobile-app.jsx` — full mobile shell (BottomBar, drawer physics, connection pips) — fonte per pattern shell SP6

---

## Risposta attesa nel thread Claude Design

Per ogni mockup SP6:

1. Conferma scope SP6 MVP Phase 1 (6 mockup, mobile-first persona Sara, NO save/resume, NO pre-translate, NO multi-device — sono deferred MVP-1)
2. Genera **una risposta = un mockup** (no batch — ogni mockup è ~600-900 righe HTML+JSX)
3. File completo (HTML + JSX entrambi per A/B/C/D/E/F — tutti complessi, NO solo HTML)
4. Path salvataggio esplicito: `admin-mockups/design_files/sp6-libro-game-<name>.{html,jsx}`
5. ConnectionPip[] inline nel JSX dove richiesto (replicato dal brief sezioni C, F)
6. Note finali per ogni mockup:
   - Deviazioni flaggate (con motivazione vs vision constraint)
   - Nuovi componenti v2 emersi (da implementare poi in `apps/web/src/components/ui/v2/libro-game/`)
   - Stati Gherkin coperti (mappare ogni state UI → scenario Gherkin nella vision §3, es. "covered: G1.4 connection lost, G3.3 low-confidence, G4.6 quota")

Quando SP6 completo, tabella handoff finale + lista master componenti.

---

## Note finali per Claude Design

**Tono UI**: warm, accogliente, casual — Sara non è una power-user. Evitare gergo tecnico ("OCR confidence" → "Qualità lettura"; "embedding model" → solo in stati admin/debug, non UI Sara). Linguaggio paritario ("tu/voi" gruppo italiano informale).

**Microcopy hint**:
- Loading state photo upload: "Sto sfogliando il manuale…" (non "Indicizzazione embeddings in corso")
- Q&A typing: "Sto cercando…" (non "Generating LLM response")
- Quota warning: "Quasi alla fine della quota mensile" (non "Free tier rate limit reached")
- House rule: "La regola del vostro gruppo" (sottolinea collettivo, no "user-defined override")

**Prioritizza percepito velocità**: ogni mockup deve mostrare **almeno** 1 stato di feedback immediato sotto 1 sec (skeleton, typing, optimistic UI). La vision §4.1 target P95 5 sec — il mockup mostra fiducia che roba sta succedendo subito.

Buon lavoro. SP6 chiude il primo mile della Phase 1 lato design — post-merge si passa a sprint implementation con i 5-6 mesi calendar revisionati.
