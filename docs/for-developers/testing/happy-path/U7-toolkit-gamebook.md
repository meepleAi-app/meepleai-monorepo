# Happy Path — U7 · Toolkit & Gamebook play

> Catalogo scenari happy-path (percorso di successo) per l'area **U7**. Solo happy path — nessuno scenario negativo/errore/edge. Formato e legenda: vedi [`_TEMPLATE.md`](./_TEMPLATE.md). Mappa route→area: vedi [`_coverage-map.md`](./_coverage-map.md) § U7.

## Intestazione

- **Area**: U7 — Toolkit & Gamebook play (strumenti di gioco standalone/toolkit + riproduzione libro-game).
- **Prerequisiti dati (seed `make seed-sp4`)**:
  - Giochi seed (owned da `marco`): **Azul, Catan, Wingspan, Brass, 7wonders, Codenames, Carcassonne, Pandemic, Terraforming, Arknova, Spirit** (`data.json:games[]` + `library[]`).
  - Toolkit seed (`data.json:toolkits[]`, creati via `POST /game-toolkits`, script `50-toolkits.sh`): `Azul Toolkit v2` (marco, published, 3 tool), `Catan Essentials` (sara, published, 3 tool), `Brass Pro Tools` (marco, bozza), `Universal Game Night` (sara, published, 2 tool).
  - Sessioni seed (`data.json:sessions[]`, script `70-sessions.sh`) → alimentano `toolkit/history` e `toolkit/stats`.
  - **Template di gioco config-based** (`lib/config/game-templates.ts`, NON seed): disponibili solo per **Azul, Catan, Wingspan, 7 Wonders, Splendor, Ticket to Ride**. Il launcher `library/[gameId]/toolkit` mostra la card template solo per questi.
- **Utente/i**: `marco@meepleai.test` (premium, verificato) per la maggior parte; `sara@meepleai.test` come owner alternativo di alcuni toolkit seed. Admin non richiesto.
- **Stack**: richiede `make dev` (full). La **generazione AI toolkit** (`generate-from-kb`) e i flussi **gamebook translate/encounter** (OCR + SSE LLM) richiedono lo stack AI completo; se giù → `⚠️ blocked-env` (vedi note per scenario).

### ⚠️ Note ambientali critiche per U7

1. **Bug BE noto — RowVersion su `POST /game-toolkits`** (`50-toolkits.sh:118-129`): la creazione toolkit seed può ritornare 500 (colonna `RowVersion` not-null senza default su Postgres). Se il seed toolkit fallisce, le pagine che leggono i toolkit dell'utente possono mostrare empty-state legittimo. Verificare l'esito del seed prima di eseguire gli scenari toolkit-detail.
2. **`/toolkits` e `/toolkits/[id]` usano un endpoint DIVERSO dal seed**: la lista community `/toolkits` consuma `GET /api/v1/toolkits/recommended?limit=50` (`useDiscoverRecommendedToolkits`), NON `game-toolkits`. Questo endpoint è "v1 carryover" (installCount=0, rating null fino a Phase 4b) e **può legittimamente restituire empty-state** anche con toolkit seed presenti. Empty-state ⇒ è comunque un pass Smoke (skeleton→empty legittimo). Per uno scenario detail con dato reale, aprire l'id da un item se la lista è popolata; altrimenti marcare `⚠️ blocked-env`.
3. **Gamebook NON è seedato**: `data.json` non ha chiave `gamebooks`/`campaigns` e non esiste uno step di seed gamebook. Le campagne libro-game vanno **create a runtime** via il wizard `CampaignSetupDrawer` (scenario U7-13). Il resume-picker `library/[gameId]/play` mostra quindi l'empty-state "prima campagna" di default (stato atteso, pass Smoke).
4. **Salvataggio paragrafo gamebook gated dai narrative books**: in `GamebookPlayShell` il submit "Salva paragrafo" resta disabilitato se il gioco non ha **narrative book** indicizzati. Poiché i gamebook non sono seedati, per la maggior parte dei giochi seed il submit è disabilitato → l'osservabile happy-path della campagna è il **rendering della shell** (titolo campagna + form visibile), non il salvataggio effettivo.
5. **Translate/Encounter richiedono foto + AI**: `TranslateViewer` (`translate`) e la cheatsheet `encounter` producono il risultato solo dopo upload foto (input `type="file" accept="image/*"` — su desktop basta caricare un'immagine, la fotocamera fisica non è obbligatoria) + stack LLM (OCR + SSE / parse). Senza foto+AI è osservabile solo la **shell idle** (heading + CTA cattura). Il risultato tradotto/parsato → `⚠️ blocked-env` se lo stack AI è giù.

### Copertura CRUD & operazioni assenti (spec §3.1)

Verifica nel codice di quali operazioni CRUD siano realmente esposte in UI per le entità U7 create dall'utente:

| Entità | Create | Save/Edit | Rename | Delete | Scenario ciclo |
|--------|--------|-----------|--------|--------|----------------|
| **Gamebook campaign** | ✅ wizard `CampaignSetupDrawer` (U7-13b) | ✅ progress `PUT .../progress` **gated dai narrative book** (U7-14) | ✅ `MultiCampaignList` (prompt) — solo ≥2 campagne | ✅ `MultiCampaignList` (confirm, soft-delete owner-only) — solo ≥2 campagne | **U7-17** (crea 2 → rinomina → cancella + reload) |
| **Toolkit session** (live session da launcher) | ✅ launcher "Start {Game} Session" (U7-11) | ✅ `recordScore` / pause-resume-complete (U7-12) | ❌ nessun rename in UI | ❌ **nessun Delete in UI** | — (niente ciclo con Delete) |

**Toolkit session — Delete ASSENTE (annotato, niente Delete inventati)**: lo store `useSessionStore` (`lib/stores/session-store.ts`) espone `createSession · startSession · pauseSession · resumeSession · completeSession · addPlayer · removePlayer · recordScore` — **nessun `deleteSession`**. Il client `api.liveSessions` non ha un endpoint DELETE-session wirato. La pagina `toolkit/history` (`client.tsx`) è read-only: nessun pulsante Elimina/Rimuovi sulle card sessione. `completeSession`/Finalize è una **transizione di stato** (la sessione resta poi visibile in `toolkit/history` come finalizzata), NON un delete. `removePlayer` rimuove un partecipante dalla sessione attiva, non la sessione. Di conseguenza **non esiste un ciclo CRUD con Delete per le sessioni toolkit**: la copertura U7 sulle sessioni resta Create (U7-11) + Save/score (U7-12), senza scenario di cancellazione.

---

## Matrice di copertura (16 route)

| Route | Scenario/i | Tipo |
|-------|-----------|------|
| `(authenticated)/toolkit` | U7-01 | Smoke |
| `(authenticated)/toolkit/play` | U7-02 | Flow |
| `(authenticated)/toolkit/history` | U7-03 | Smoke |
| `(authenticated)/toolkit/stats` | U7-04 | Smoke |
| `(authenticated)/toolkit/templates` | U7-05, U7-06 | Smoke |
| `(authenticated)/toolkit/[sessionId]` | U7-12 | Flow |
| `(authenticated)/toolkits` | U7-07 | Smoke |
| `(authenticated)/toolkits/[id]` | U7-08 | Flow |
| `(authenticated)/hub/toolkits` | U7-09 | Smoke |
| `(authenticated)/library/[gameId]/toolbox` | U7-10 | Smoke |
| `(authenticated)/library/[gameId]/toolkit` | U7-11 | Flow |
| `(authenticated)/library/[gameId]/toolkit/[sessionId]` | U7-12 | Flow |
| `(authenticated)/library/[gameId]/play` | U7-13a, U7-17 | Smoke + Flow (CRUD) |
| `(authenticated)/library/[gameId]/play/[campaignId]` | U7-13b, U7-14 | Flow |
| `(authenticated)/library/[gameId]/play/[campaignId]/encounter` | U7-15 | Flow |
| `(authenticated)/library/[gameId]/play/[campaignId]/translate` | U7-16 | Flow |

> Nessuna route U7 marcata `skip`. `hub/toolkits` e `library/[gameId]/toolbox` sono redirect server-side (coperti come Smoke di redirect). `toolkit/[sessionId]` (tool rail generico) e `library/[gameId]/toolkit/[sessionId]` (variante game-specific con score input) condividono la stessa infrastruttura `useSessionStore.loadSession` e sono coperti insieme (U7-12), con osservabili distinti per la variante game-specific in U7-11→U7-12.

---

## Scenari

### U7-01 · Toolkit hub (landing)

```gherkin
Scenario U7-01 [Smoke]: Il toolkit hub carica con empty-state segnaposto
  Given sono loggato come marco@meepleai.test (premium, verificato)
  When apro /toolkit
  Then la pagina carica dentro l'HubLayout (search "Cerca tool...", toggle vista)
    And vedo l'empty-state segnaposto con l'icona 🛠️ e il testo "Toolkit in arrivo."
    And il sotto-testo elenca "timer, contatori, mazzi e altri strumenti di gioco"
  Osservabile ✅: HubLayout renderizzato + icona 🛠️ + testo "Toolkit in arrivo." presente; nessun errore Console/Network
  Route: /toolkit
  Utente: marco
```

### U7-02 · Toolkit standalone play (strumenti locali)

```gherkin
Scenario U7-02 [Flow]: Uso gli strumenti standalone (dado, timer, contatore, randomizzatore)
  Given sono loggato come marco@meepleai.test
    And /toolkit/play usa il DEFAULT_TOOLKIT (strumenti in localStorage, nessun backend)
  When apro /toolkit/play
    And digito un nome in "Chi gioca?" (es. "HP-TEST-2026-07-10 Marco")
    And clicco un dado nella sezione "Dadi" per tirarlo
    And avvio/uso un contatore nella sezione "Contatori"
  Then compare l'header "Toolkit" con l'input attore
    And le sezioni "Dadi", "Contatori", "Randomizzatore" sono renderizzate
    And dopo il tiro/azione compare una voce nella sezione "Cronologia" con orario + risultato
  Osservabile ✅: header "Toolkit" + sezioni Dadi/Contatori/Randomizzatore presenti + ≥1 riga in "Cronologia" dopo un'azione (es. "Estrazione tessere → N"); nessun errore Console/Network
  Route: /toolkit/play
  Utente: marco
```

### U7-03 · Toolkit history (storico sessioni)

```gherkin
Scenario U7-03 [Smoke]: Lo storico sessioni carica e mostra i filtri
  Given sono loggato come marco@meepleai.test
    And il seed ha creato sessioni (data.json:sessions[]) via GET /api/v1/sessions/history
  When apro /toolkit/history
  Then vedo l'header "Session History"
    And la card "Filters" con Game / Start Date / End Date / Reset
    And dopo il caricamento (skeleton → contenuto) vedo la griglia delle sessioni finalizzate
      oppure l'empty-state "No sessions found" con CTA "Start Your First Session" (empty-state legittimo)
  Osservabile ✅: header "Session History" + card "Filters" + (griglia sessioni con ≥1 card "View Details" OPPURE empty-state "No sessions found"); nessun errore Console/Network
  Route: /toolkit/history
  Utente: marco
```

### U7-04 · Toolkit stats (analytics sessioni)

```gherkin
Scenario U7-04 [Smoke]: Le statistiche sessione caricano i KPI e i grafici
  Given sono loggato come marco@meepleai.test
    And GET /game-sessions/session-statistics restituisce i dati aggregati del seed
  When apro /toolkit/stats
  Then dopo lo spinner vedo l'header "Session Analytics"
    And la griglia KPI (data-testid="kpi-cards") con Total Sessions / Games Played / Avg Duration
    And se ci sono dati: sezioni "Most Played Games" / "Monthly Activity" (data-testid="monthly-chart") / "Recent Scores"
      oppure l'empty-state "No session data available yet." (legittimo se nessuna sessione conclusa)
  Osservabile ✅: header "Session Analytics" + data-testid="kpi-cards" con 3 valori numerici (OPPURE empty-state "No session data available yet."); nessun errore Console/Network
  Route: /toolkit/stats
  Utente: marco
```

### U7-05 · Toolkit templates (griglia)

```gherkin
Scenario U7-05 [Smoke]: La galleria dei template approvati carica
  Given sono loggato come marco@meepleai.test
    And GET /api/v1/game-toolkits/templates restituisce i template approvati (possibile lista vuota v1)
  When apro /toolkit/templates
  Then vedo l'header "Toolkit Templates" e il sotto-testo "Browse approved templates..."
    And il filtro categoria (Select "All / Strategy / Party / CardGames / Cooperative")
    And dopo "Loading templates..." vedo la griglia (data-testid="templates-grid") con card template
      oppure il messaggio "No approved templates found." (empty-state legittimo)
  Osservabile ✅: header "Toolkit Templates" + Select categoria presente + (data-testid="templates-grid" con ≥1 data-testid="template-card" OPPURE "No approved templates found."); nessun errore Console/Network
  Route: /toolkit/templates
  Utente: marco
```

### U7-06 · Toolkit templates — filtro categoria

```gherkin
Scenario U7-06 [Smoke]: Filtrare i template per categoria aggiorna la griglia
  Given sono loggato come marco@meepleai.test
    And sono su /toolkit/templates con la lista template caricata
  When apro il Select categoria e scelgo "Strategy"
  Then la query si rilancia con la categoria selezionata (queryKey include la categoria)
    And la griglia si aggiorna al set filtrato (o mostra "No approved templates found." se vuoto per quella categoria)
  Osservabile ✅: il valore del Select passa a "Strategy" + la griglia cambia contenuto/conteggio (o empty-state coerente); nessun errore Console/Network
  Route: /toolkit/templates
  Utente: marco
```

### U7-07 · Toolkits — catalogo community (lista)

```gherkin
Scenario U7-07 [Smoke]: Il catalogo community dei toolkit carica hero, filtri e griglia
  Given sono loggato come marco@meepleai.test
    And GET /api/v1/toolkits/recommended?limit=50 alimenta la lista (endpoint "recommended", v1 carryover)
  When apro /toolkits
  Then vedo l'hero con eyebrow/title/subtitle e le 3 statistiche (Toolkits / Installs / Featured)
    And la barra filtri (ricerca + tablist status all/featured/new/top + sort)
    And dopo lo stato loading vedo la griglia di card toolkit
      oppure l'empty-state filtrato/vuoto legittimo (l'endpoint recommended può essere vuoto in v1)
  Osservabile ✅: hero con 3 stat presenti + tablist status + (griglia con ≥1 card toolkit OPPURE empty-state legittimo); nessun errore Console/Network
  Route: /toolkits
  Utente: marco
```

### U7-08 · Toolkit detail — dettaglio + cambio tab

```gherkin
Scenario U7-08 [Flow]: Apro il dettaglio di un toolkit e navigo tra i tab abilitati
  Given sono loggato come marco@meepleai.test
    And esiste un toolkit apribile per id (da /toolkits se popolato, es. "Azul Toolkit v2" via il suo id)
  When apro /toolkits/{toolkitId}
    And attendo il caricamento (skeleton data-slot="toolkit-detail-loading" → contenuto)
    And clicco il tab "Tools"
  Then vedo l'hero ToolkitSummaryPanel (titolo, autore, gioco, install/rating)
    And la ConnectionBar con i pip (tools, game, + placeholder agent/kb/author/sessions)
    And i tab: "overview" (default) e "tools" abilitati; agent/kb/versions/ratings disabilitati (Phase 5)
    And cliccando "Tools" l'URL passa a ?tab=tools e compare il pannello ToolsTabPanel (conteggio tool o empty-state)
  Osservabile ✅: hero con titolo toolkit + ConnectionBar + tab "overview"/"tools" + dopo click "Tools" URL contiene tab=tools e il pannello Tools è visibile; nessun errore Console/Network
  Route: /toolkits/[id]
  Utente: marco
  Nota: se /toolkits è vuoto (endpoint recommended v1) e nessun id è apribile → ⚠️ blocked-env.
```

### U7-09 · Hub/toolkits — redirect canonico

```gherkin
Scenario U7-09 [Smoke]: /hub/toolkits reindirizza al canonico /toolkits
  Given sono loggato come marco@meepleai.test
    And /hub/toolkits è un redirect server-side a /toolkits (Issue #1480)
  When apro /hub/toolkits
  Then il browser atterra su /toolkits
    And la pagina catalogo community è renderizzata (hero + filtri + griglia/empty-state)
  Osservabile ✅: URL finale = /toolkits + hero catalogo toolkit visibile; nessun errore Console/Network (no loop di redirect)
  Route: /hub/toolkits → /toolkits
  Utente: marco
```

### U7-10 · Library toolbox — redirect al tab game

```gherkin
Scenario U7-10 [Smoke]: /library/[gameId]/toolbox reindirizza al tab "toolbox" del dettaglio gioco
  Given sono loggato come marco@meepleai.test
    And "Azul" è in libreria di marco (Owned) con {azulId}
    And /library/[gameId]/toolbox è un redirect 307 a /library/[gameId]?tab=toolbox (S4 library-to-game)
  When apro /library/{azulId}/toolbox
  Then il browser atterra su /library/{azulId}?tab=toolbox
    And il dettaglio gioco è renderizzato con il tab "toolbox" attivo
  Osservabile ✅: URL finale contiene ?tab=toolbox + dettaglio gioco "Azul" visibile con tab toolbox; nessun errore Console/Network
  Route: /library/[gameId]/toolbox → /library/[gameId]?tab=toolbox
  Utente: marco
```

### U7-11 · Library game toolkit — launcher sessione con template

```gherkin
Scenario U7-11 [Flow]: Apro il launcher toolkit di un gioco con template e avvio una sessione
  Given sono loggato come marco@meepleai.test
    And "Azul" è in libreria (Owned) e ha un template config (game-templates.ts → categorie/round)
  When apro /library/{azulId}/toolkit
  Then vedo l'header "{Azul} Toolkit" con l'icona template 🎨
    And la card "Game Template" con le Scoring Categories (Wall Tiles, Rows, Columns, Colors, Penalties) e i Round 1-5
    And la card "Start {Azul} Session" con l'input giocatori e il pulsante "Start ... Session"
  When compilo un giocatore (es. "HP-TEST-2026-07-10 P1") e clicco "Start Azul Session"
  Then viene creata una live session (POST via useSessionStore.createSession) + aggiunti i player + startSession
    And vengo reindirizzato a /library/{azulId}/toolkit/{sessionId}
  Osservabile ✅: header "Azul Toolkit" + card "Game Template" con categorie/round + toast "Azul session started!" + navigazione a /library/{azulId}/toolkit/{sessionId}; nessun errore Console/Network
  Route: /library/[gameId]/toolkit → /library/[gameId]/toolkit/[sessionId]
  Utente: marco
```

### U7-12 · Sessione toolkit attiva (tool rail + score)

```gherkin
Scenario U7-12 [Flow]: Gioco una sessione toolkit (game-specific): scoreboard + registro punteggio
  Given ho appena avviato una sessione Azul da U7-11 e sono su /library/{azulId}/toolkit/{sessionId}
    And la sessione è InProgress con ≥1 partecipante e il template Azul carica round/categorie
  When la pagina carica la sessione (useSessionStore.loadSession) e apre la connessione SSE
    And inserisco un punteggio dal ScoreInput sticky (partecipante + round + categoria + valore)
  Then vedo il SessionHeader con nome sessione e azioni Pause/Finalize
    And la lista partecipanti (MeepleParticipantCard) e lo Scoreboard
    And dopo il submit il punteggio compare nello Scoreboard (o via SSE) con toast di conferma
  Osservabile ✅: SessionHeader visibile + ≥1 MeepleParticipantCard + Scoreboard renderizzato + dopo il submit il valore appare nello Scoreboard (o toast "+N"); nessun errore Console/Network
  Route: /library/[gameId]/toolkit/[sessionId] (variante generica: /toolkit/[sessionId] — tool rail Turn/Dice/Whiteboard/Scoreboard)
  Utente: marco
  Nota: la variante generica /toolkit/[sessionId] (SessionToolLayout con tool rail e ?tool=<id>) condivide loadSession; osservabile = tool rail con le voci Turn Order/Dice/Whiteboard/Scoreboard e cambio tool che aggiorna ?tool.
```

### U7-13a · Gamebook resume picker (empty-first-time)

```gherkin
Scenario U7-13a [Smoke]: Il resume-picker libro-game mostra l'empty-state "prima campagna"
  Given sono loggato come marco@meepleai.test
    And "Azul" è in libreria e non esistono campagne libro-game per marco su questo gioco (gamebook NON seedato)
  When apro /library/{azulId}/play
  Then dopo lo skeleton (data-testid="gamebook-resume-shell-skeleton") vedo l'empty-state
      (data-testid="gamebook-resume-empty-first-time", data-state="state-01-first-time")
    And l'header "Inizia la tua prima campagna" con la CTA "📖 Inizia campagna" (data-testid="gamebook-resume-empty-cta")
  Osservabile ✅: data-testid="gamebook-resume-empty-first-time" presente + testo "Inizia la tua prima campagna" + CTA "📖 Inizia campagna"; nessun errore Console/Network
  Route: /library/[gameId]/play
  Utente: marco
  Nota: se marco ha ≥1 campagna (creata da U7-13b), il picker mostra ResumeHero (1) o MultiCampaignList (2+) — anch'essi pass Smoke read-only.
```

### U7-13b · Crea campagna libro-game (wizard 3 step)

```gherkin
Scenario U7-13b [Flow]: Creo una campagna libro-game col wizard e atterro sulla play shell
  Given sono loggato come marco@meepleai.test
    And sono su /library/{azulId}/play in empty-state e clicco "📖 Inizia campagna"
  When si apre il CampaignSetupDrawer (data-testid="campaign-setup-drawer", "Nuova campagna · Azul")
    And Step 1 "Nome": digito "HP-TEST-2026-07-10 Serata Azul" (≥3 char) e scelgo un preset gruppo
    And clicco "Avanti →" (data-testid="campaign-setup-next") fino allo Step 3 "Conferma"
    And clicco "📖 Inizia sessione" (data-testid="campaign-setup-submit")
  Then viene creata la campagna (POST /api/v1/gamebook/campaigns { gameId, title })
    And vengo reindirizzato a /library/{azulId}/play/{campaignId}
  Osservabile ✅: drawer con Stepper (Nome/Giocatori/Conferma) + review card con titolo "HP-TEST-... Serata Azul" + dopo submit navigazione a /library/{azulId}/play/{campaignId}; nessun errore Console/Network
  Route: /library/[gameId]/play (drawer) → /library/[gameId]/play/[campaignId]
  Utente: marco
```

### U7-14 · Gamebook play shell (campagna)

```gherkin
Scenario U7-14 [Flow]: La play shell della campagna carica titolo, paragrafo e navigazione
  Given ho creato la campagna "HP-TEST-2026-07-10 Serata Azul" da U7-13b
    And sono su /library/{azulId}/play/{campaignId}
  When la shell carica la campagna (useGamebookCampaign → GET /gamebook/campaigns/{id})
  Then dopo lo skeleton (data-testid="gamebook-play-shell-skeleton") vedo l'header
      (data-testid="gamebook-play-shell-header") con l'<h1> = titolo campagna e la riga "§ N" (o "§ —")
    And la sezione "Aggiorna paragrafo corrente" con input (data-testid="gamebook-paragraph-input")
      e submit (data-testid="gamebook-paragraph-submit")
    And i pulsanti nav "Traduci pagina" (data-testid="gamebook-open-translate") e "Apri chat con agente"
  Osservabile ✅: header con titolo "HP-TEST-... Serata Azul" + riga "§ ..." + form paragrafo visibile + pulsante "Traduci pagina"; nessun errore Console/Network
  Route: /library/[gameId]/play/[campaignId]
  Utente: marco
  Nota: il submit "Salva paragrafo" resta disabilitato se il gioco non ha narrative book (gamebook non seedato) — l'osservabile happy-path è il rendering della shell, non il salvataggio. Se esiste ≥1 narrative book, l'happy path estende a: digito un numero → "Salva" → la riga "§" si aggiorna.
```

### U7-15 · Gamebook encounter — shell cheatsheet (parse AI)

```gherkin
Scenario U7-15 [Flow]: La schermata Encounter Book mostra la card di ingresso parse
  Given sono loggato come marco@meepleai.test
    And ho una campagna libro-game (da U7-13b) su /library/{azulId}/play/{campaignId}
  When apro /library/{azulId}/play/{campaignId}/encounter (senza param foto: stato di ingresso)
  Then vedo la EncounterCheatsheetView in stato "entry" con il titolo/riferimento paragrafo (§N se presente)
    And la CTA di parse ("Estrai cheatsheet"/entryCta) e l'accesso al Glossario
  Osservabile ✅: EncounterCheatsheetView renderizzata in stato entry + CTA parse visibile + link Glossario; nessun errore Console/Network
  Route: /library/[gameId]/play/[campaignId]/encounter
  Utente: marco
  Nota: il RISULTATO della cheatsheet richiede un photoId + paragrafo + gameBookId (prodotti dal flusso TranslateViewer) e la POST .../encounter-parse (LLM). Senza foto+AI l'osservabile è la sola shell di ingresso; l'esecuzione del parse reale (nemici/opzioni/condizioni) → ⚠️ blocked-env se lo stack AI è giù o manca il photoId.
```

### U7-16 · Gamebook translate — shell idle (cattura pagina)

```gherkin
Scenario U7-16 [Flow]: La schermata Traduci pagina mostra la shell idle con cattura foto
  Given sono loggato come marco@meepleai.test
    And ho una campagna libro-game (da U7-13b) su /library/{azulId}/play/{campaignId}
  When apro /library/{azulId}/play/{campaignId}/translate
  Then vedo l'header "Traduci pagina libro game"
    And il ReaderModeToggle e il pulsante cattura "Scatta o scegli foto" (data-testid="open-camera-button")
    And il link "inserimento manuale" (EnterManualLink) come alternativa senza fotocamera
      oppure, se il gioco non ha narrative book, l'alert data-testid="translate-viewer-no-narrative-books"
  Osservabile ✅: header "Traduci pagina libro game" + pulsante "Scatta o scegli foto" (data-testid="open-camera-button") + link inserimento manuale (o alert no-narrative-books); nessun errore Console/Network
  Route: /library/[gameId]/play/[campaignId]/translate
  Utente: marco
  Nota: la TRADUZIONE effettiva richiede upload foto (input type="file" accept="image/*" — su desktop basta un'immagine) + OCR/segmentazione + SSE LLM (useTranslateSegmentSSE). Il testo tradotto (TranslationPane) e l'entry encounter (data-testid="translate-open-encounter") → ⚠️ blocked-env se lo stack AI è giù. La shell idle è l'osservabile happy-path base.
```

### U7-17 · Ciclo CRUD campagna libro-game (crea → rinomina → cancella) con persistenza

```gherkin
Scenario U7-17 [Flow]: Ciclo di vita campagna libro-game — crea 2, rinomina, cancella, verifica persistenza via reload
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And "Azul" è in libreria (Owned) con {azulId}
    And il resume-picker /library/{azulId}/play espone Rinomina/Elimina SOLO in stato multi-campagna
      (MultiCampaignList, data-state="state-03-multi-campaign", quando le campagne di marco per questo gioco sono ≥2)
  When apro /library/{azulId}/play e creo la 1ª campagna col wizard (CampaignSetupDrawer)
      titolo "HP-TEST-2026-07-10 Campagna A" (Step Nome ≥3 char → Avanti → "📖 Inizia sessione", data-testid="campaign-setup-submit")
    And torno su /library/{azulId}/play e creo la 2ª campagna "HP-TEST-2026-07-10 Campagna B"
      (dal picker uso "+ Nuova campagna", data-testid="gamebook-resume-multi-cta", stesso drawer)
    And torno su /library/{azulId}/play
  Then il picker mostra MultiCampaignList (data-testid="gamebook-resume-multi-list") con header "Le tue campagne attive (2)"
    And vedo entrambe le card (data-testid="gamebook-resume-multi-item-{idA}" e "...-{idB}")
    And dopo reload della pagina le 2 campagne HP-TEST sono ancora presenti (persistite, POST /gamebook/campaigns)
  When clicco "Rinomina" sulla card A (data-testid="gamebook-resume-multi-rename-{idA}") e nel prompt confermo il nuovo titolo "HP-TEST-2026-07-10 Campagna A rinominata"
  Then la card A mostra il titolo aggiornato (PATCH /gamebook/campaigns/{idA}, la query si invalida)
    And dopo reload il titolo rinominato persiste
  When clicco "Elimina" sulla card B (data-testid="gamebook-resume-multi-delete-{idB}") e confermo il dialog window.confirm ("Eliminare la campagna ... soft-delete")
  Then la campagna B sparisce dalla lista (DELETE /gamebook/campaigns/{idB}, soft-delete owner-only, query invalidata)
    And con 1 sola campagna rimasta il picker passa allo stato single-resume (ResumeHero, data-testid="gamebook-resume-hero", data-state="state-02-single-resume") con la sola campagna A rinominata
    And dopo reload la campagna B resta assente e la A rinominata resta presente
  Osservabile ✅: (create) header "Le tue campagne attive (2)" + 2 item con marker HP-TEST, entrambe presenti dopo reload · (rename) card A col titolo "...rinominata" dopo reload · (delete) card B assente + shift a ResumeHero single, B ancora assente dopo reload; nessun errore Console/Network
  Route: /library/[gameId]/play (+ drawer /library/[gameId]/play → [campaignId] per la create)
  Utente: marco
  Dati creati: "HP-TEST-2026-07-10 Campagna A/B" (A rinominata e conservata a fine ciclo; B soft-deleted). Delete SOLO su dati HP-TEST.
  Nota CRUD: le operazioni Rinomina/Elimina sono wirate in GamebookResumeShell (renameCampaign/deleteCampaign) e passate a MultiCampaignList → esposte in UI SOLO con ≥2 campagne; per questo lo scenario ne crea 2. Con 0/1 campagne il picker mostra EmptyFirstTime/ResumeHero senza pulsanti Rinomina/Elimina. Il ramo "Salva progresso paragrafo" (UPDATE, PUT .../progress) NON è incluso qui perché gated dai narrative book (gamebook non seedato) — vedi U7-14. L'"Archivia/chiudi campagna" (CampaignCloseSelector, POST .../close, outcome Completed/Abandoned) è una transizione di stato terminale (SI-8 #2639), NON un delete: fuori scope di questo ciclo.
```

---

## Riepilogo copertura

- **Route coperte**: 16 / 16 (100%). Nessuna route `skip`.
- **Scenari**: 18 totali (U7-13 splittato in 13a Smoke + 13b Flow; U7-17 = ciclo CRUD campagna).
  - **Flow** (9): U7-02, U7-08, U7-11, U7-12, U7-13b, U7-14, U7-15, U7-16, U7-17.
  - **Smoke** (9): U7-01, U7-03, U7-04, U7-05, U7-06, U7-07, U7-09, U7-10, U7-13a.
- **Copertura CRUD & persistenza (spec §3.1)**: vedi la tabella "Copertura CRUD & operazioni assenti" nelle note ambientali.
  - **Gamebook campaign**: ciclo Create→Rename→Delete con reload di persistenza = **U7-17** (Create anche in U7-13b; Save/progress gated dai narrative book = U7-14, conditional).
  - **Toolkit session**: Create (U7-11) + Save/score (U7-12); **Delete assente in UI** (nessun `deleteSession` nello store né endpoint DELETE-session wirato; `toolkit/history` read-only; Finalize = transizione di stato, non delete). Nessun ciclo con Delete → annotato, niente Delete inventati.
- **Rischi env da monitorare in esecuzione (Fase B)**:
  - Bug RowVersion (`POST /game-toolkits`) → toolkit seed potenzialmente assenti (nota 1).
  - `/toolkits` e `/toolkits/[id]` su endpoint `recommended` v1 → empty-state legittimo; U7-08 → `⚠️ blocked-env` se nessun id apribile (nota 2).
  - Gamebook non seedato → resume-picker in empty-state; campagne create a runtime (nota 3).
  - Salvataggio paragrafo gated dai narrative book → osservabile = rendering shell (nota 4).
  - Translate/Encounter risultato = foto + stack AI → `⚠️ blocked-env` per la parte AI (nota 5); shell idle sempre osservabile.
  - **U7-17 (CRUD campagna)**: i pulsanti Rinomina/Elimina compaiono SOLO in stato multi-campagna (≥2 campagne di marco per il gioco). Lo scenario crea 2 campagne HP-TEST proprio per attivare `MultiCampaignList`; con l'ambiente pulito (gamebook non seedato) marco parte da 0 → il flusso è deterministico. Se marco avesse già campagne residue di run precedenti, il conteggio header e lo stato del picker vanno letti come "≥2" (comunque multi-list). Delete opera solo su dati HP-TEST (soft-delete owner-only).
