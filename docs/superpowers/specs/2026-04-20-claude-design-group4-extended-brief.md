# Brief per Claude Design — Gruppo 4 · Pagine estese (Extended Coverage)

> **Scopo:** Documento da fornire a Claude Design per generare i mockup delle aree app non coperte dal brief originale `2026-04-20-claude-design-missing-pages-brief.md`. Coprono aree full-page che il drawer/mobile-app.jsx non gestisce.

**Data:** 2026-04-20
**Autore:** Migration planning session (Gruppo 4 scoping)
**Stato:** Pronto per Claude Design — 6 brief indipendenti, eseguibili in parallelo
**Prerequisiti:** Gruppo 1 già consegnato (Auth, Onboarding, Settings, Notifications, Public)

---

## Contesto (uguale al brief originale)

Claude Design ha già prodotto:
- **tokens.css** + **components.css** — Design System v1
- **mobile-app.jsx** — 24 screen mobile
- **5 HTML demo** baseline (00-hub → 05-dark-mode)
- **Gruppo 1 completato**: `auth-flow.html`, `onboarding.html`, `settings.html`, `notifications.html`, `public.html`

Questo brief **estende** il piano aggiungendo 6 aree non coperte dal brief originale, identificate durante la gap analysis vs `apps/web/src/app/` (199 pagine totali, ~109 non-admin).

---

## Principi di coerenza (immutati)

1. **Entity-first color coding** — ogni pagina usa `--c-<entity>` come accento (vedi tokens.css)
2. **Warm palette** — `--bg: #f7f3ee` (light), `--bg: #14100a` (dark)
3. **Typography** — `--f-display: Quicksand` · `--f-body: Nunito` · `--f-mono: JetBrains Mono`
4. **Mobile-first** phone frame 380×780, drawer bottom sheet primario
5. **Desktop pattern riuso** — Split/Sidebar/HeroTabs per ogni pagina
6. **Connection bar** presente dove serve
7. **Dark mode** variante completa
8. **Path fix CRITICO**: NON usare path `uploads/...`. Referenzia `tokens.css` e `components.css` come relativi.

---

## 📦 Brief 4.1 · Profile + Player Public Profiles (`profile-player.html` + jsx)

### Contesto
- **Profilo utente (self)**: pagina personale editabile, avatar, stats aggregate
- **Player public profile**: profilo altro utente, vista read-only con stats, partite, achievements
- Oggi in app: `(authenticated)/profile/page.tsx`, `/profile/achievements`, `/players/[id]/page.tsx`, `/players/[id]/{achievements,games,sessions,stats}`

### Screen da produrre

- **01 · Profile self (tab overview)** — Avatar grande, stats row (partite, ore giocate, giochi preferiti, win rate), bio editabile inline, edit avatar/cover
- **02 · Profile self — Achievements** — Grid badges, filtri (tutti/sbloccati/in corso), progress ring per ogni achievement
- **03 · Player public profile (altrui)** — Stesso layout ma senza edit, pulsante "Aggiungi agli amici" / "Invita in game night" top-right
- **04 · Player sessions tab** — Timeline sessioni del player con game chip + punteggio + posizione
- **05 · Player stats tab** — Charts (sparkline win rate, game frequency), top 5 giochi, nemesis/rival detection
- **06 · Player games tab** — Grid dei giochi giocati con count partite e win rate per gioco

### Design hint

- **Entità primaria**: `player` (colore `--c-player` viola)
- **Desktop**: HeroTabs pattern — hero top (avatar + stats chip) + tab bar persistente
- **Mobile**: Phone frame con hero ridotto, tab scrollabile orizzontalmente
- **Connection bar**: pip per `session` (partite recenti), `game` (giochi top), `event` (prossima game night condivisa)
- **Privacy states**: mostra variante "profilo privato" con placeholder
- **Avatar source**: gradient placeholder con iniziali, ring entity-colored
- **Stats card**: `.card` con numero grande Quicksand bold + label mono uppercase

### Entità collegate
`player` primario · `session` + `game` + `event` secondarie (connection bar)

---

## 📦 Brief 4.2 · Game Detail + Subsections (`game-detail.html` + jsx)

### Contesto
- Pagina dedicata al singolo gioco con tab per FAQ, recensioni, regole, strategie, sessioni
- Oggi in app: `(authenticated)/games/[id]/{page,faqs,reviews,rules,strategies,sessions}`
- **NB**: esiste già drawer game detail in `mobile-app.jsx` — QUESTE sono le viste full-page che richiedono più real estate

### Screen da produrre

- **01 · Game hero + overview** — Cover art top, titolo + publisher + anno, stats (BGG weight, player count, tempo medio), CTA "Aggiungi alla library" / "Nuova sessione" / "Chiedi all'agente"
- **02 · FAQs tab** — Accordion Q/A, search bar, contributor chip per ogni FAQ, sort by helpfulness
- **03 · Reviews tab** — Lista reviews con rating (1-10) entity-colored, bottone "Scrivi review", filtri (tutti/amici/community)
- **04 · Rules tab** — KB viewer integrato, ToC left, contenuto center, highlight+quote → apre chat inline
- **05 · Strategies tab** — Card tips per strategia, filtro per difficoltà/ruolo, voti up/down
- **06 · Sessions tab** — Timeline sessioni del gioco (proprie + amici), CTA "Nuova sessione di questo gioco"

### Design hint

- **Entità primaria**: `game` (colore `--c-game` arancione)
- **Desktop**: HeroTabs — hero cover parallax + tab sticky sotto
- **Mobile**: Hero collapsing al scroll, tab bar orizzontale sticky
- **Connection bar**: pip `kb` (rulebook disponibile), `agent` (agent configurato), `session` (mie partite), `player` (amici che giocano)
- **Stats row**: Quicksand bold + entity mini-icon
- **Subsection variants**: ogni tab ha un'icona + colore accento che rimane `--c-game` ma con tint secondario

### Entità collegate
`game` primario · `kb` + `session` + `agent` + `player` secondarie

---

## 📦 Brief 4.3 · Chat Desktop Full-Page (`chat-desktop.html` + jsx)

### Contesto
- Chat thread desktop full-page (NON il drawer mobile)
- Oggi in app: `(chat)/chat/{page,[threadId],new,agents/create}`
- Split view Linear-style: thread list sx + chat area dx

### Screen da produrre

- **01 · Chat hub (lista threads)** — Sidebar sx con thread list raggruppati per agent, main area con empty state "Seleziona un thread o crea nuovo"
- **02 · Active thread** — Stesso split, main area con message feed, composer sticky bottom, agent chip top + game context
- **03 · New thread wizard** — Modal o panel: seleziona agent → seleziona gioco → kickoff message
- **04 · Agent creation** — Form wizard 3 step: persona (name, avatar, bio) → knowledge (upload docs, link KB) → behavior (tone, answer style, toolkit access)
- **05 · Thread empty + suggested prompts** — Prompt chips quick-start (5-6 suggestion buttons entity-colored)

### Design hint

- **Entità primaria**: `chat` (colore `--c-chat` blu)
- **Secondaria**: `agent` (giallo) visibile in chip top thread
- **Desktop**: Split view 280px sidebar + fluid main, drawer dx per citations/sources
- **Mobile**: Sidebar collassa a drawer, main fullscreen
- **Message bubble**: user = `--bg-muted`, agent = `bg-card` con border-left `--c-agent`
- **Streaming indicator**: 3 dots animati entity-colored
- **Rag citations**: inline superscript `[1]` → hover tooltip source snippet + "apri KB"
- **Composer**: textarea auto-grow + send btn disabled until text, attachment icon (PDF), slash-command `/rules /faq /suggest`

### Entità collegate
`chat` primario · `agent` + `game` + `kb` secondarie (connection bar header thread)

---

## 📦 Brief 4.4 · Upload Wizard (`upload-wizard.html` + jsx)

### Contesto
- Flussi upload: PDF rulebook + propose new game (community catalog) + private game add
- Oggi in app: `(authenticated)/upload/`, `library/private/add/`, `library/propose/`

### Screen da produrre

- **01 · Upload hub (scelta tipo)** — 3 card CTA: "Carica un rulebook" (PDF) / "Proponi un gioco nuovo" (community) / "Aggiungi gioco privato" (personal library)
- **02 · PDF upload + drag-drop** — Drop zone large, file type validation, progress bar processing (extracted, OCR, chunking, embedding), stato real-time per step
- **03 · Propose game wizard** — Form multi-step: identificazione (nome, BGG id autofill) → metadata (player count, weight, year, publisher) → immagine cover → submit per review
- **04 · Private game add** — Form simplificato: nome + cover opzionale + rulebook PDF opzionale → "Solo nella mia library"
- **05 · Processing status** — Lista upload in corso con progress per step, retry su errore, notifica push "Rulebook pronto"
- **06 · Success + next action** — Confirmation + CTA "Crea prima sessione" / "Chiedi all'agente" / "Vai alla library"

### Design hint

- **Entità primaria**: `game` per propose/private, `kb` per PDF upload
- **Desktop**: Card layout 3-column per hub, wizard HeroTabs per propose
- **Mobile**: Stack verticale con step scroll
- **Drop zone**: dashed border `--c-kb`, hover state con tint `--c-kb/0.1`, drop active con `--c-kb` fill
- **Progress stepper**: 4-5 step con dot connector, current step entity-ring
- **File validation**: inline error `--c-danger` + icon
- **Preview**: se PDF → thumbnail prima pagina, se gioco → cover preview

### Entità collegate
`game` + `kb` primarie · `session` (CTA post-upload) secondaria

---

## 📦 Brief 4.5 · Live Session Screens (`live-session.html` + jsx)

### Contesto
- Sessioni di gioco real-time con scoreboard, foto, timer, chat agente, player join
- Oggi in app: `(authenticated)/sessions/live/[sessionId]/{page,play,scoreboard,photos,players,agent}`
- **Differenza da session drawer**: QUESTE sono full-screen per uso al tavolo, TV-friendly, big-touch

### Screen da produrre

- **01 · Live session lobby** — Pre-start, lista giocatori con ready-state, timer countdown start, host controls (kick, reorder)
- **02 · Play screen (big timer + turn)** — Timer turno grande full-screen, nome player corrente Quicksand 4xl, next player preview, pause/skip buttons
- **03 · Scoreboard real-time** — Tabella punteggi live, colonne personalizzabili per gioco, swipe per incrementare score, leader highlight
- **04 · Photos / memories** — Grid foto scattate durante sessione, upload/tag player, caption, filter per round/turno
- **05 · Agent chat (in-session)** — Chat drawer lateral con agent del gioco, quick-prompts "Come funziona X regola?", answer ultra-concise mode
- **06 · Players tab live** — Roster con avatar + resources (monete/meeple/carte count) + status, connection bar pip per stats in-session
- **07 · Session complete summary** — Final scoreboard, MVP, achievements unlocked, CTA "Save memory" / "Nuova partita" / "Salva review"

### Design hint

- **Entità primaria**: `session` (colore `--c-session` indigo)
- **Design philosophy**: BIG TOUCH — tutti i target ≥ 56px altezza, font XXL per chi è seduto a distanza
- **Desktop/tablet**: layout landscape-friendly, prevedi 1024×768 (iPad al tavolo)
- **Mobile**: focus singolo task per screen (no tab multipli)
- **Timer visivo**: ring progress grande + numeric big
- **Score increment**: tap big `+1` / `+5` buttons, long-press custom value
- **Dark mode importante**: sessioni serali al tavolo
- **Connection bar**: pip `player` (roster), `game` (rules quick-ref), `agent` (chat), `toolkit` (dadi/moneta embedded)

### Entità collegate
`session` primario · `player` + `game` + `agent` + `toolkit` secondarie

---

## 📦 Brief 4.6 · Toolkit Ecosystem (`toolkit-ecosystem.html` + jsx)

### Contesto
- Toolkit = strumenti digitali al tavolo (dadi, timer, moneta, contatore, deck, scheduler phase)
- Oggi in app: `toolkit/{history,stats,templates,play}`, `toolkit/[sessionId]`, `library/games/[gameId]/toolbox`, `library/private/[gameId]/toolkit/configure`
- **NB**: ToolSheetContent già in `mobile-app.jsx` — QUESTE sono configurator + analytics

### Screen da produrre

- **01 · Toolkit hub** — Grid templates (Dadi, Monete, Contatore, Timer, Card deck, Phase tracker) con "Usa" / "Duplica" / "Personalizza"
- **02 · Toolkit configurator** — Builder drag-drop per creare tool set custom per gioco: sidebar componenti + canvas center + preview dx
- **03 · Toolkit templates library** — Template preconfigurati per giochi popolari (Catan set, Wingspan set, Terraforming Mars set), community share
- **04 · Toolkit history** — Timeline uso tool: "Ieri, 2 ore fa, Wingspan session — dadi usati 12 volte, timer totale 1h 42m"
- **05 · Toolkit stats** — Dashboard insights: tool più usato, tempo medio per turn, bias dadi (fairness), trend temporale
- **06 · Play mode (compact overlay)** — Versione minimale per use in-session, flottante sopra scoreboard, drag-reorder tools visibili

### Design hint

- **Entità primaria**: `toolkit` (colore `--c-toolkit` verde)
- **Desktop**: Sidebar+Drawer pattern (sidebar templates + main configurator)
- **Mobile**: Grid templates → tap → bottom sheet configurator
- **Tool icons**: big emoji o SVG custom (🎲🪙⏱️🎴)
- **Configurator**: drag-drop placeholder area, snap-to-grid, undo/redo
- **Stats**: charts sparkline + bar entity-colored
- **History**: timeline pattern come diary in game-night

### Entità collegate
`toolkit` primario · `session` (dove usati) + `game` (template specifici) + `player` (history chi ha tirato) secondarie

---

## Checklist di consegna per Claude Design

Per OGNI brief, Claude Design deve produrre:
- [ ] File HTML standalone (`<area>.html`) con tutti gli screen, path relativi per `tokens.css` + `components.css`
- [ ] File JSX companion (`<area>.jsx`) con componenti React interattivi
- [ ] Variante dark mode completa
- [ ] Tweaks panel per navigare tra screen (pattern notifications.html)
- [ ] Aggiornamento stat counter `52+ screen` → nuovo totale

**Priorità ordine di lavoro consigliato:**
1. **4.2 Game Detail** — usato ovunque, blocking per molte altre feature
2. **4.5 Live Session** — feature differenziante, alta visibilità
3. **4.3 Chat Desktop** — power user feature, alto impatto
4. **4.1 Profile/Players** — social/retention feature
5. **4.4 Upload Wizard** — critical path ma meno visibile
6. **4.6 Toolkit Ecosystem** — complementare, più avanzata

---

## Integrazione nel piano di migrazione

Una volta consegnati i mockup Gruppo 4, si aggiungono come **M8** al plan di migrazione:
- **M6 Gruppo 1** — Auth + Settings + Notifications + Public + Onboarding (5 PR, mockup ✅)
- **M7 Gruppo 2** — Game nights + Play records + Session wizard (3 PR, mockup da fare)
- **M8 Gruppo 4** — Profile/Players + Game detail + Chat + Upload + Live session + Toolkit (6 PR, mockup da fare via questo brief)
- **M9 Gruppo 3 + residui** — Editor/Pipeline + Calendar + Agents builder + Playlists + Invites

**Esecuzione parallela possibile:** M6 e M8 (parzialmente) possono procedere in parallelo se team separati.

---

## Note operative

- **Path fix**: SEMPRE verificare che HTML finale NON contenga `uploads/...` prefixes
- **Stat counter hub**: dopo ogni merge, aggiornare `00-hub.html` (nav link + stat + hub-card + lead)
- **Entity color consistency**: verificare ogni screen abbia l'entity color corretta secondo la mappa sopra
- **Componenti riuso**: segnalare a Claude Design se vuoi riusare componenti da `mobile-app.jsx` (PhoneFrame, EntityChip, DrawerHeader) vs riscrivere
