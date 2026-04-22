# Brief per Claude Design — Redesign v2 MeepleAI, pagine mancanti

**Data**: 2026-04-20
**Destinatario**: Claude Design (o qualunque designer che riceva l'handoff)
**Progetto**: MeepleAI — app companion per board game enthusiasts (RAG + multi-agent AI + live play sessions)
**Contesto**: Esiste già un set di mockup hi-fi consegnato (`admin-mockups/README.md` + `admin-mockups/design_files/`) che copre 24 screen mobile, desktop patterns, drawer variants, design system, dark mode. Questo brief richiede **13 pagine/flussi aggiuntivi** non coperti dal pacchetto originale, necessari per sostituire completamente l'app non-admin.

---

## 0. Regole di ingaggio (non negoziabili)

Tutte le nuove pagine DEVONO:

1. **Usare i tokens esistenti** (`admin-mockups/design_files/tokens.css`, 202 righe commentate). Nessun nuovo colore, font, radius, shadow o motion. L'entity palette (9 colori canonical) è la stessa.
2. **Usare gli stessi font**: Quicksand (display), Nunito (body), JetBrains Mono (mono). Nessuna quarta famiglia.
3. **Supportare dark mode day-one** — ogni surface nei mockup dark/light side-by-side come in `05-dark-mode.html`.
4. **Essere mobile-first** con phone frame identici a `01-screens.html` (380px wide) + desktop adaptation (vedi `02-desktop-patterns.html` — usare pattern "sidebar + detail" come default v2).
5. **NON introdurre grigi neutri**. I warm browns sono obbligatori per mantenere identità board-game.
6. **NON aggiungere iconography per riempire spazio**. Icon slop è anti-pattern qui.
7. **Emoji-per-entity restraint**: emoji solo come entity marker o dove aggiunge personalità reale.
8. **Formato output**: HTML hi-fi in `admin-mockups/design_files/v2/<NN>-<nome-kebab>.html`, più JSX reference se il comportamento è ricco (come `mobile-app.jsx`), più una sezione nel README aggiornata con la nuova pagina.
9. **Livello di fedeltà**: hi-fi pixel-accurate. Trattare come contract per lo sviluppo.
10. **Lingua UI**: italiano (coerente con `01-screens.html`).

Entity palette reminder:

| Token | Emoji | Label |
|---|---|---|
| `--c-game` | 🎲 | Game |
| `--c-player` | 👤 | Player |
| `--c-session` | 🎯 | Session |
| `--c-agent` | 🤖 | Agent |
| `--c-kb` | 📄 | KB |
| `--c-chat` | 💬 | Chat |
| `--c-event` | 🎉 | Event |
| `--c-toolkit` | 🧰 | Toolkit |
| `--c-tool` | 🔧 | Tool |

Semantic aliases: `--c-success` → toolkit, `--c-warning` → agent, `--c-danger` → event, `--c-info` → chat.

---

## 1. Auth flow (5 screens)

**File target**: `v2/01-auth.html` (5 phone frames in una grid) + `v2/01-auth-desktop.html`

**Screens**:

1. **Login**
   - Logo mark (gradient `--c-game` → `--c-event`) + wordmark "MeepleAI"
   - Input email, Input password (con show/hide eye icon)
   - Button primary "Accedi" (bg `--c-game`, white text)
   - Button ghost "Crea account"
   - OAuth buttons: Google, GitHub (icon + label, border `--border`, bg `--bg-card`)
   - Link "Password dimenticata?" (font mono, `--text-muted`)
   - Error banner inline sopra form (bg `hsl(var(--c-event) / .1)`, text `--c-event`) se credenziali errate
2. **Register**
   - Input: nome, email, password, conferma password
   - Checkbox "Accetto termini e privacy" (custom styled, `--c-game` accent)
   - Button primary "Crea account"
   - Link "Hai già un account?"
3. **Forgot password**
   - Input email, button "Invia link recovery"
   - Stato success con icon ✉️ centrato e messaggio "Controlla la tua email"
4. **Verify email**
   - Icon 📧 grande, testo "Abbiamo inviato un link a {email}"
   - Button "Reinvia" (secondary)
   - Countdown timer su reinvio (60s)
5. **Magic-link landing**
   - Spinner + "Accesso in corso..."
   - Fallback error "Link scaduto o non valido" con CTA "Riprova login"

**Layout**:
- Mobile: single column centered, padding 24px, max-width 360px
- Desktop: split 50/50 con illustrazione (gradient board-game-themed, no emoji slop) a sinistra, form a destra max 400px

**Motion**: fade-in form con `--dur-md`, shake horizontal su errore.

---

## 2. Onboarding wizard (3-4 step)

**File target**: `v2/02-onboarding.html`

**Flow** (progress bar top, step indicator `1/4`):

1. **Welcome + profilo**: campo nome, avatar picker (12 emoji preset + "upload photo"), lingua UI (select IT/EN)
2. **Pick-games**: search bar + grid di `EntityCard` (variante compact) selezionabili. Chip count in alto "Selezionati: 3". Skip button in basso
3. **Import library BGG** (opzionale): input "BGG username" + button "Importa". Lista preview risultati con checkbox. Skip disponibile
4. **Finish**: confetti animation (solo entity-colored particles, no random), messaggio "Benvenuto in MeepleAI!", CTA "Vai alla Home"

**Layout**: mobile single column, desktop centered card max 560px con background `--bg` e illustrazione laterale.

**Motion**: slide-right tra step (`--dur-md`, `--ease-out`), progress bar animata.

---

## 3. Settings (tab layout)

**File target**: `v2/03-settings.html`

**Sezioni** (sidebar desktop / tabs mobile):

1. **Account**: avatar upload, nome, email, cambia password, elimina account (zona danger con `--c-event` accent)
2. **Preferenze**: tema (radio toggle: Sistema / Chiaro / Scuro), lingua, formato data, formato durata (min/ore)
3. **Notifiche**: lista canali (Push, Email, In-app) con toggle. Raggruppati per tipo evento (Sessioni, Chat, Menzioni, News)
4. **Privacy**: toggle profilo pubblico, chi può invitarmi, cronologia chat conservata (Mai / 30gg / Sempre)
5. **Integrazioni**: BGG (connect/disconnect), n8n (config webhook URL), Export dati (CSV)
6. **Advanced**: cache size + pulsante pulisci, info versione, link docs

**Layout**:
- Mobile: list master → tap → detail view (drill-down). Back button top-left
- Desktop: sidebar 240px verticale + content area fluid, max-width content 720px

**Pattern**: ogni riga setting = label a sinistra + control a destra (toggle/select/button). Divider `--border-light` tra righe. Section title `--f-display` bold uppercase mono style.

---

## 4. Notifications center full page

**File target**: `v2/04-notifications.html`

**Elementi**:

- Topbar: titolo "Notifiche" + icon "segna tutte come lette" + icon filter
- Filter bar pills: Tutte / Non lette / Menzioni (counter badge per ognuna)
- Lista notifiche raggruppate per tempo: "Oggi" / "Questa settimana" / "Più vecchie"
- Ogni item row:
  - Entity pip a sinistra (colore in base a tipo evento)
  - Testo "Giorgio ha finito la partita di Azul" (who bold, entity chip per game)
  - Timestamp relativo destra
  - Se non letta: dot `--c-event` dopo il testo
  - Swipe-left (mobile) → "Archivia" / "Segna letta"
  - Tap → apre drawer entità correlata
- Empty state: illustration minimalista + "Nessuna notifica" + secondary text

**Desktop**: come mobile ma max-width 720px centered.

---

## 5. Upload PDF flow (3-4 step)

**File target**: `v2/05-upload.html`

**Step**:

1. **Select game** (se non pre-selezionato): search box + EntityCard list giochi utente
2. **Drop zone**: area dashed border `--border-strong`, icon 📄 grande, testo "Trascina PDF qui o clicca per selezionare". Max 50MB, tipo .pdf only. Su drag-over: bg `hsl(var(--c-kb) / .08)`
3. **Upload + OCR progress**:
   - Progress bar orizzontale (`--c-kb` fill)
   - Sotto: 3 step con check animato (Upload → OCR → Indexing)
   - Tempo stimato "~2 minuti rimanenti"
   - Button "Annulla"
4. **Mapping pagine** (opzionale per manuali ricchi):
   - Preview thumbnails pagine PDF (griglia 3 colonne)
   - Per ogni pagina: dropdown sezione ("Setup" / "Turno" / "Scoring" / "Custom")
   - Button "Salta mapping" (usa auto-detection)
5. **Publish**:
   - Riepilogo: titolo, pagine, sezioni, agent che userà questa KB
   - Toggle "Pubblico" / "Solo io"
   - Button primary "Pubblica" (bg `--c-toolkit`)
   - Success state: check grande + "KB pubblicato" + CTA "Vai al Game"

**Motion**: step transition slide, progress bar smooth animation.

---

## 6. Rule Editor + Versions

**File target**: `v2/06-editor.html`

**Layout split pane**:

- **Left sidebar (desktop) / top sheet (mobile)**: outline regole (tree view). Click → scroll to section. Search bar top.
- **Center pane**: markdown editor con syntax highlight (KB color palette — teal accents)
- **Right pane**: preview rendering live
- **Top toolbar**:
  - Version selector dropdown (v1.0, v1.1-draft, ecc.)
  - Button "Diff vs versione X" (apre side-by-side modal)
  - Button "Save draft" (ghost)
  - Button "Publish version" (primary `--c-kb`)
- **Bottom status bar**: word count, last saved time, sync status

**Mobile**: tab switch tra Outline / Editor / Preview. Full-screen.

**Diff modal** (separato nel file HTML):
- Split view left (old) / right (new)
- Changes highlighted: added `hsl(var(--c-toolkit) / .15)`, removed `hsl(var(--c-event) / .15)`
- Navigation "prev/next change"
- Button "Rollback to this version"

---

## 7. Game Night hub (serata multi-sessione)

**File target**: `v2/07-game-night.html`

**Scenario**: l'utente sta organizzando/vivendo una "serata" con più giochi consecutivi (es. 3 giochi in una sera con gli stessi 4 player).

**Elementi**:

- **Hero top**: titolo "Serata del 15 Aprile" (editable), subtitle "con Luca, Anna, Marco, Sara" (player pips), data + ora
- **Session sequence**: 3-5 card in row orizzontale (horizontal scroll mobile)
  - Ogni card: game cover gradient, titolo gioco, status (finito / in corso / in coda), durata effettiva, winner pip
  - Card attiva (in corso): border `--c-session` + pulse dot
- **Timeline cross-gioco** (event log verticale):
  - Header "Attività della serata"
  - Eventi: game-started, score-update, game-ended, custom notes
  - Ogni evento entity-colored accent
  - FAB bottom-right "+ Nota" per custom event
- **CTA bar bottom** (sticky mobile):
  - Se serata attiva: "Prossimo gioco" / "Pausa" / "Finalizza serata"
  - Se serata finita: "Replay highlights" / "Export log"
- **Summary card** (mostrata dopo "Finalizza"):
  - MVP della serata (player pip + stat)
  - Gioco più lungo, più corto
  - Total play time
  - Button "Salva nel diario"

**Desktop**: 2-column layout (60/40) — sessioni a sinistra, timeline a destra.

---

## 8. Play Records / Diary (timeline cross-gioco)

**File target**: `v2/08-play-records.html`

**Scenario**: utente vuole rivedere tutte le sue partite storicamente.

**Elementi**:

- **Top filters**:
  - Period picker (ultimi 7gg / 30gg / 90gg / Tutto / Custom range)
  - Game multi-select (chip picker con EntityChip)
  - Player multi-select
  - Winner-only toggle
- **Stats overview card** (sticky top):
  - Totale partite, ore giocate, winrate
  - Mini chart (bar chart 30gg)
- **Timeline**:
  - Cluster per mese ("Aprile 2026", "Marzo 2026")
  - Ogni row: data, game pip, player pips (chi ha giocato), risultato (winner + scores), durata
  - Tap → apre Session drawer
  - Action inline "Rigioca questa config" (crea nuova sessione pre-compilata)
- **Empty state**: illustrazione + "Nessuna partita nel periodo scelto"
- **Export**: button top-right "Export CSV"

---

## 9. Discover / Shared catalog

**File target**: `v2/09-discover.html`

**Scenario**: browse del catalogo community dei giochi per aggiungerli alla propria libreria.

**Elementi**:

- **Top**: search bar grande + filter chips orizzontali (Popolari / Nuovi / Top rated / Cooperative / Family-friendly / 2 player / ecc.)
- **Complexity slider**: 1-5 con labels
- **Grid giochi** (EntityCard variante featured):
  - Cover, titolo, designer, anno
  - Complexity dots, player range, durata
  - Badge top-right:
    - "Nella tua libreria" (bg `--c-toolkit`) se posseduto
    - "In wishlist" (bg `--c-agent`) se in wishlist
    - Altrimenti: nessun badge
  - CTA bottom card:
    - Se non in libreria: "Aggiungi alla wishlist" + "Importa ora"
    - Se request import pending: "Import in corso..." con spinner
- **Infinite scroll** con skeleton loader
- **Empty state**: "Nessun gioco trovato per questi filtri"

**Desktop**: grid 4 colonne, sidebar filter a sinistra (collapse-to-chip pattern come library).

---

## 10. Private Games

**File target**: `v2/10-private-games.html`

**Nota**: da valutare se dedicato o copribile da filtro Library. In questo brief ipotizziamo dedicato per chiarezza designer.

**Scenario**: giochi personalizzati/custom creati dall'utente (non presenti nel catalogo community).

**Elementi**:

- Uguale a Library ma con:
  - Badge "Privato" su ogni card (icon 🔒)
  - Button top-right "+ Nuovo gioco privato"
  - Toggle visibilità (Solo io / Inviti / Condivisibile con link)
  - Drawer per generazione invite link (URL + copy button + expire date picker)
- Empty state con CTA "Crea il tuo primo gioco" + illustrazione

**Se il designer ritiene coperto da Library con filtro, può confermarlo nel file HTML con nota e layout minimale.**

---

## 11. Pipeline Builder / n8n integrations

**File target**: `v2/11-pipeline-builder.html`

**Scenario**: power users configurano pipeline custom (webhook → AI agent → Slack/email). Layout node-based.

**Elementi**:

- **Canvas** (center): node-based graph (ispirazione react-flow). Zoom + pan
- **Left panel**: node library (Triggers, Actions, AI, Logic, Output) — drag to canvas
- **Right panel** (quando node selected): properties editor (form)
- **Top toolbar**: nome pipeline, save, run test, enable/disable toggle, version history
- **Bottom**: log run-history (collapsible)
- **Node style**: rounded rectangle, entity-colored left border, title mono, connection points sides

**Nota per designer**: qui è legittimo deviare leggermente dal look "board-game" per adottare un pattern UX standard per flow-editor, MA i tokens (colori/fonts/radius) restano gli stessi. Obiettivo: che un power-user lo riconosca come node editor ma che appartenga visualmente a MeepleAI.

**Desktop-only**: il canvas richiede schermo grande. Mobile: stato "Apri su desktop per modificare" + lista pipeline esistenti in read-only.

---

## 12. Error / 404 / offline / join-landing

**File target**: `v2/12-errors.html` (4 layout nel file)

1. **404 Not Found**:
   - Illustration whimsical (un meeple perso in una scacchiera vuota)
   - Titolo "Questa pagina non esiste"
   - Subtitle con routing suggerimenti
   - CTA "Torna alla home" + "Cerca"
2. **500 Server Error**:
   - Illustration (un meeple con cappello da saldatore)
   - Titolo "Qualcosa è andato storto"
   - Subtitle "Abbiamo ricevuto il report, riproviamo"
   - CTA "Riprova" + link "Segnala bug"
3. **Offline**:
   - Icon 📡 o wifi slash
   - Titolo "Sei offline"
   - Content hint: "Queste pagine sono disponibili offline: Home, Library (cached), Chat recenti"
   - CTA "Riprova connessione"
4. **Join-link landing**:
   - Hero card con preview entità (game/session/toolkit a cui si è stati invitati)
   - Info invitante (player pip + nome)
   - CTA primary "Accetta invito" + secondary "Rifiuta"
   - Se non loggato: "Accedi per continuare" + link register

---

## 13. Empty states + Loading skeletons (pattern reusable)

**File target**: `v2/13-empty-loading-patterns.html`

**Empty states** (8 varianti):

1. Empty library — "Non hai ancora giochi" + CTA "Esplora catalogo"
2. Empty wishlist — "La tua wishlist è vuota" + CTA
3. Empty chats — "Inizia una chat con un agente"
4. Empty sessions — "Nessuna partita registrata"
5. Empty notifications — "Tutto tranquillo"
6. Empty search — "Nessun risultato per {query}"
7. Empty drawer tab — "Niente da mostrare qui"
8. Empty KB — "Nessun documento caricato"

**Pattern**: illustration minimalista (NO emoji slop, illustration stile coerente) + titolo + subtitle + CTA. Ogni empty state usa un entity color subtile.

**Loading skeletons** (7 pattern):

1. Skeleton card (EntityCard shape con shimmer)
2. Skeleton list row (list variant)
3. Skeleton drawer (hero + tabs + content blocks)
4. Skeleton session mode (hero + scoreboard + log)
5. Skeleton chat messages (bubble alternating left/right)
6. Skeleton settings (rows di pattern)
7. Skeleton pipeline canvas (nodes grigiati)

**Motion**: shimmer animation (`--dur-lg` loop, `cubic-bezier(.4, 0, .6, 1)`) su tutti.

---

## Output deliverable

Per ciascuna delle 13 richieste:

1. File HTML hi-fi in `admin-mockups/design_files/v2/<NN>-<nome>.html`
2. Opzionale: JSX se il comportamento è ricco (come `mobile-app.jsx` fa per 01-screens)
3. Aggiornamento `admin-mockups/README.md` sezione "Files in this Handoff" con riga per ciascun nuovo file
4. Se introdotti nuovi pattern riusabili (es: wizard progress bar, filter pill bar, diff view), aggiungere sezione dedicata in `04-design-system.html`

**Dark mode**: ogni pagina deve funzionare in light/dark senza lavoro aggiuntivo (usare `tokens.css` variables, mai valori hardcoded). Verifica finale con toggle button `themeToggle` già presente nei mockup.

**Consistency check**: prima della consegna, side-by-side con `01-screens.html` per verificare che font-size, spacing, radius, shadow usati sono dentro il token set.

---

## Priorità di consegna

Per allinearsi con il piano di implementazione (vedi `2026-04-20-redesign-v2-full-consumer-design.md`):

**Batch 1 — critical path Alpha** (consegna prima):
1. Auth flow (blocca tutto)
2. Onboarding wizard
5. Upload PDF
13. Empty states + Loading skeletons (servono per tutti gli screens M2/M3)

**Batch 2 — consumer complete**:
3. Settings
4. Notifications center
7. Game Night hub
12. Error / 404 / offline

**Batch 3 — power features**:
6. Rule Editor + Versions
8. Play Records / Diary
9. Discover / Shared catalog
10. Private Games
11. Pipeline Builder / n8n

---

## Domande aperte al designer

Se emergono durante il lavoro, documentare nel file HTML con commento `<!-- OPEN-QUESTION: ... -->` o aprire issue con label `design-question`.

Esempi di possibili domande:
- "Private Games è davvero separato o coperto da Library con filtro `status=private`?"
- "Pipeline Builder richiede un node editor dedicato o un form-based flow più semplice è sufficiente per v1?"
- "Game Night hub condivide screen con Session Mode o è una route separata?"

---

**Contatto**: questo brief è autoritativo. Per chiarimenti sul dominio MeepleAI consultare `admin-mockups/README.md` sezione "Data Model" + `CLAUDE.md` nel repo.
