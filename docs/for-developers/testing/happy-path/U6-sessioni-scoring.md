# Happy Path — U6 · Sessioni & Scoring

> Catalogo scenari happy-path (percorso di successo) per l'area **Sessioni & Scoring**. Solo happy path — nessuno scenario negativo/errore/edge. Formato e legenda: [`_TEMPLATE.md`](./_TEMPLATE.md). Mappa route globale: [`_coverage-map.md`](./_coverage-map.md) § U6.

## Intestazione area

- **Prerequisiti dati (seed `make seed-sp4`)**:
  - **Sessioni live** (`POST /live-sessions`, aggregato `LiveGameSession`): `s-azul-live` (Azul, **InProgress**, owner `marco`, giocatori marco/sara/luca/giulia), `s-wing-042` (Wingspan, **Completed**, owner `sara`, winner sara), `s-brass-041` (Brass, **Completed**, owner `marco`, winner marco), `s-catan-pause` (Catan, **Paused**, owner `andrea`), `s-arknova-setup` (Ark Nova, **Created**, owner `marco`). (`s-agricola-arch` è skippata dal seed: `gameSlug` null.)
  - **Play-records**: ~20% di `totalSessions` per utente (`80-play-records.sh`, `PLAY_RECORDS_FACTOR=0.2`), completati, con score self. Volumi seed: marco ~18, sara ~28, luca ~11, giulia ~2, andrea ~16.
  - **Giocatori** (`/players`): derivati dai play-record → 5 utenti seed (`marco|sara|luca|giulia|andrea@meepleai.test`).
  - **Game night pubblicate** (per `join/event/[code]`): `e-marco-serata`, `e-club-night`, `e-archive`, `e-strategici` (`_publish: true`).
- **Utenti**: `marco@meepleai.test` (host della maggior parte delle sessioni), `sara@meepleai.test` (secondo utente per join/multi-utente). Password: default `seed_password()` o `SEED_SP4_PASSWORD`.
- **Dati creati marcati** `HP-TEST-2026-07-10` nel titolo/nome (sessioni, play-record).

### ⚠️ Nota tecnica — scoring polimorfico live (vincolo di osservabilità)

Il tab **Score** della sessione live è **polimorfico** (`ScoreType` ∈ Points / BinaryWin / Objectives / Ranking). Lo store `useLiveSessionStore` parte con `scoringType = null` / `scoreData = null` e viene **popolato solo da un evento SignalR `ScoringConfigured`** emesso dal backend (`useSignalrSession.ts:133`). **Nessuna UI utente** chiama `setScoringConfig`; il `SessionCreationWizard` configura le "dimensioni di punteggio" del play-record, non il tipo polimorfico della sessione live. Il `LiveSessionDto` caricato da `useLiveSession` è round-based e non espone `scoringType` (per ADR-083 Fase 1 la reidratazione REST del tipo polimorfico è stata rimossa; wiring da `LiveSessionDto` differito a Fase 2).

**Conseguenza per gli scenari**: finché il backend non emette `ScoringConfigured`, `ScoreTabContent` mostra il placeholder `data-slot="scoring-panel-empty"` (né editor né renderer polimorfico). Perciò:

- Lo scenario **U6-06** (host edita i punteggi polimorfici) ha come **precondizione ambientale** che `ScoringConfigured` sia stato emesso per la sessione. Se in locale/seed l'evento non arriva, lo scenario è `⚠️ blocked-env` (gate ambientale, **non** fail) — vedi legenda `_TEMPLATE.md`.
- I 4 sotto-tipi di editing sono descritti in un unico scenario matriciale (U6-06) con gli osservabili strutturali per ciascun tipo, dato che l'attivazione dipende dal medesimo gate.
- Lo scenario **U6-07** (viewer non-host, renderer read-only) condivide lo stesso gate `ScoringConfigured`.

Gli scenari live che **non** dipendono dallo scoring polimorfico (apertura shell live, tab Turn/Widget/Notes, note, complete→play-record, scoreboard dedicato) restano pienamente osservabili sul seed.

### 🔁 Nota CRUD — operazioni di mutazione & Delete disponibili (spec §3.1)

Verifica **nel codice** (`play-records.api.ts`, `usePlayRecords.ts`, `session-flow/client.ts`, `clients/liveSessionsClient.ts`, `clients/sessionsClient.ts`, le pagine `sessions/**` e `play-records/**`) delle operazioni realmente esposte dalla UI:

| Entità | Create (UI) | Edit/Save (UI) | **Delete (UI)** | Persistenza verificabile | Ciclo CRUD |
|--------|-------------|----------------|-----------------|--------------------------|-----------|
| **Play-record** | ✅ `/play-records/new` (`POST /play-records`) | ✅ `/play-records/[id]/edit` (`PUT`, gate K5: solo `sessionDate`/`notes`/`location`) | ✅ `/play-records/[id]/edit` → `EditGateBanner` "Cancella partita" → dialog conferma → `DELETE /play-records/{id}` → redirect `/play-records` (`useDeleteRecord`, AC-4.6) | ✅ reload lista `/play-records` (backend `PlayHistory`) | **completo** → U6-28 |
| **Session note (personale)** | ✅ `/sessions/[id]/notes` Textarea "My Notes" → "Save Notes" (crea la chiave) | ✅ stesso Textarea → sovrascrive la chiave | ⚠️ **nessun bottone Delete dedicato** — la "cancellazione" è: svuota il Textarea + "Save Notes" (salva stringa vuota) | ✅ reload pagina (`localStorage` `meepleai_session_notes_{id}`, riletto in `useEffect`) | **parziale** (create+edit+clear, no delete-button) → U6-29 |
| **Session live** (`LiveGameSession`) | ✅ `/sessions/new` wizard (U6-02) | — (transizioni pause/resume/complete/save via LiveTopBar/live shell) | ❌ **nessun Delete/Abbandona esposto in `/sessions/*`** | — | **assente** (vedi nota sotto) |
| **Session note "ufficiale"** (`session.notes`) | — (creata a fine partita dal backend) | — sola lettura in `/sessions/[id]/notes` (card "Session Summary") | — | — | read-only |

**Delete assenti (nessuno scenario inventato — annotato per completezza):**
- **Session (hard delete)**: nessun endpoint/UI di eliminazione di una `LiveGameSession`. `liveSessionsClient` espone solo transizioni di stato + `removePlayer` (DELETE **a livello di giocatore**, non della sessione); nessun `deleteSession`.
- **Session (abbandona/archivia)**: `api.sessions.abandon(id)` (`POST /api/v1/sessions/{id}/abandon` → status `Abandoned`, client legacy `GameSessionDto`) **non è cablato** ad alcun bottone nelle route `/sessions/*`. L'unica "archiviazione" utente-raggiungibile (`PrivateGameHub.handleAbandonSession`) vive sotto `/library/[gameId]` (dettaglio gioco privato) e in realtà chiama `completeSession` (non `abandon`) → fuori dal perimetro U6 (appartiene a U3). La chiusura di una live via `LiveTopBar` "Termina sessione" è un **complete → play-record** (U6-11), non un delete.
- **Play-record player/score (delete granulare)**: `liveSessionsClient.removePlayer` esiste ma la rimozione singola giocatore/punteggio da un play-record non è un flusso happy-path utente indipendente (i punteggi si gestiscono nell'editor); non catalogato come Delete a sé.

---

## Matrice di copertura

| Route | Liv. | Scenario/i |
|-------|------|-----------|
| `(authenticated)/sessions` | Smoke | U6-01 |
| `(authenticated)/sessions/new` | Flow | U6-02 |
| `(authenticated)/sessions/join` | Flow | U6-03 |
| `(authenticated)/sessions/[id]` | Smoke | U6-04 |
| `(authenticated)/sessions/[id]/live` | Flow | U6-05, U6-06, U6-07, U6-11 |
| `(authenticated)/sessions/[id]/notes` | Flow | U6-08, U6-29 |
| `(authenticated)/sessions/[id]/scoreboard` | Smoke | U6-09 |
| `(authenticated)/sessions/[id]/join` | Flow | U6-10 |
| `(authenticated)/play-records` | Smoke | U6-12, U6-28 |
| `(authenticated)/play-records/new` | Flow | U6-13, U6-28 |
| `(authenticated)/play-records/[id]` | Smoke | U6-14 |
| `(authenticated)/play-records/[id]/edit` | Flow | U6-15, U6-28 |
| `(authenticated)/play-records/stats` | Smoke | U6-16 |
| `(authenticated)/players` | Smoke | U6-17 |
| `(authenticated)/players/[id]` | Smoke | U6-18 |
| `(authenticated)/players/[id]/achievements` | Smoke | U6-19 |
| `(authenticated)/players/[id]/games` | Smoke | U6-20 |
| `(authenticated)/players/[id]/sessions` | Smoke | U6-21 |
| `(authenticated)/players/[id]/stats` | Smoke | U6-22 |
| `(public)/join` | Smoke | U6-23 |
| `(public)/join/event/[code]` | Flow | U6-24 |
| `(public)/join/session/[code]` | Flow | U6-25 |
| `(public)/play-records/shared/[token]` | Smoke | U6-26 |
| `/join/[token]` (top-level, guest landing) | Flow | U6-27 |

**Copertura**: 24/24 route mappate ad ≥1 scenario (incl. `/join/[token]` top-level fuori route-group, aggiunta in A-FINAL). Nessun `smoke-aggregato`, nessuno `skip`. Gli scenari di **ciclo CRUD con verifica persistenza** (spec §3.1) sono U6-28 (play-record: crea→edita→**elimina**, ognuno con reload di verifica) e U6-29 (nota personale sessione: persistenza cross-reload + svuotamento).

> Nota d'area: `(public)/join/event/[code]` appartiene concettualmente a U5 (Game Night, RSVP) ma è catalogato qui con gli altri `join/*` per prossimità di percorso (vedi `_coverage-map.md` § U5/U6). Lo scenario U6-24 copre l'happy path RSVP-accept; il flusso di creazione/pubblicazione della game night è in U5.

---

## Scenari

### Sessioni

```gherkin
Scenario U6-01 [Smoke]: Lista sessioni carica e filtra per stato
  Given sono loggato come marco@meepleai.test
    And il seed ha creato ≥1 sessione live per marco (es. s-azul-live InProgress)
  When apro /sessions
    And clicco il filtro di stato "Completate" (Completed)
  Then la pagina carica lo shell lista senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'hero "Le tue partite" con la CTA "Registra partita"
    And vedo la barra filtri (Tutti / Attiva / Completata / Abbandonata) + toggle vista + ricerca
    And con filtro "Completate" la lista mostra solo le sessioni completate (o l'empty-state filtrato se nessuna)
  Osservabile ✅: root data-slot="sessions-library-view" presente + hero title + ≥1 card sessione o empty-state legittimo + il click sul filtro cambia l'insieme delle card a schermo
  Route: /sessions
  Utente: marco
```

```gherkin
Scenario U6-02 [Flow]: Creazione sessione via wizard e ingresso in live
  Given sono loggato come marco@meepleai.test
    And ho "Azul" nella library (seed: marco possiede Azul)
  When apro /sessions/new
    And nella card "Serata di Gioco" non avvio il quick-start, ma uso il wizard sottostante (SessionCreationWizard)
    And step "Scegli il gioco": seleziono "Azul" e imposto titolo "HP-TEST-2026-07-10 Azul"
    And step "Configura Punteggi": lascio/aggiungo almeno una dimensione
    And step "Aggiungi Giocatori": aggiungo me stesso (marco) e "sara"
    And step "Riepilogo": clicco "Crea Sessione"
  Then vengo reindirizzato a /sessions/{nuovoId} (dettaglio) — o a /sessions/{nuovoId}/live se il flusso apre la live
    And la nuova sessione è marcata HP-TEST-2026-07-10
  Osservabile ✅: avanzamento tra gli step del wizard (StepIndicator) + CTA "Crea Sessione" abilitata allo step finale + navigazione all'URL /sessions/{id} con id non vuoto
  Route: /sessions/new
  Utente: marco
  Dati creati: sessione "HP-TEST-2026-07-10 Azul"
```

```gherkin
Scenario U6-03 [Flow]: Join sessione tramite codice
  Given sono loggato come sara@meepleai.test
    And esiste una sessione live raggiungibile via join-code (es. la sessione HP-TEST creata in U6-02, o s-azul-live)
  When apro /sessions/join
    And digito il codice sessione (6 caratteri, auto-uppercase) nell'input
    And clicco "Unisciti"
  Then il client risolve il codice (GET /live-sessions/code/{code}) e mi reindirizza a /sessions/{id}
  Osservabile ✅: header "Unisciti a una Sessione" + input codice mono-spaziato + dopo submit navigazione a /sessions/{id} con id non vuoto (nessun testo d'errore "Sessione non trovata")
  Route: /sessions/join
  Utente: sara
```

```gherkin
Scenario U6-04 [Smoke]: Dettaglio sessione (riepilogo post-partita)
  Given sono loggato come marco@meepleai.test
    And esiste una sessione completata di marco (seed: s-brass-041, Completed, winner marco)
  When apro /sessions/{brassSessionId}
  Then la pagina raggiunge uno shell terminale senza errori 4xx/5xx né errori JS
    And se lo stato è Completed vedo il riepilogo (hero riepilogo, KPI grid, classifica/podio con l'indicazione del vincitore)
    And se lo stato risultasse InProgress/Paused/Setup vengo reindirizzato a /sessions/{id}/live (comportamento previsto)
  Osservabile ✅: root data-slot="session-summary-view" (con data-ui-state) presente + uno shell terminale renderizzato (riepilogo / go-live / not-found) senza errori Console/Network
  Route: /sessions/[id]
  Utente: marco
  Nota: la summary usa useSessionDetail (GameSessionDto legacy); per le sessioni create via /live-sessions il record GameSession potrebbe non esistere → lo shell terminale legittimo può essere anche il redirect a /live o il not-found. Lo smoke passa se raggiunge uno shell terminale senza errori.
```

```gherkin
Scenario U6-05 [Flow]: Apertura shell sessione live (layout G1 2 colonne)
  Given sono loggato come marco@meepleai.test
    And esiste la sessione InProgress s-azul-live (owner marco)
  When apro /sessions/{azulLiveSessionId}/live
  Then carica lo shell live (aggregato LiveGameSession via useLiveSession) senza errori 4xx/5xx né errori JS
    And vedo la LiveTopBar (titolo sessione, stato "In corso", CTA pausa/termina)
    And a sinistra (60%) il ChatAgentPanel + l'ActionLogTimeline
    And a destra (40%) i tab polimorfici Score / Turn / Widget / Notes
    And cliccando il tab "Turn" vedo l'indicatore di turno + il roster giocatori
  Osservabile ✅: LiveTopBar visibile + colonna sinistra (chat + action log) + tabs colonna destra + il click su un tab (es. Turn) cambia il contenuto a destra
  Route: /sessions/[id]/live
  Utente: marco
```

```gherkin
Scenario U6-06 [Flow]: Host edita i punteggi polimorfici (Points / BinaryWin / Objectives / Ranking)
  Given sono loggato come marco@meepleai.test (Host della sessione)
    And apro /sessions/{azulLiveSessionId}/live sul tab "Score"
    And PRECONDIZIONE AMBIENTALE: il backend ha emesso l'evento SignalR "ScoringConfigured" per questa sessione (scoringType impostato nello store)
  When come Host modifico i punteggi nell'editor del tipo attivo:
    And [Points] imposto un valore nell'input numerico di un giocatore (data-testid="points-input-{playerId}")
    And [BinaryWin] seleziono il radio "Win" di un giocatore (data-testid="binary-win-{playerId}")
    And [Objectives] spunto un obiettivo di un giocatore (data-testid="obj-{playerId}-{objective}")
    And [Ranking] trascino/riordino un giocatore nella lista (data-testid="ranking-item-{playerId}")
  Then dopo ~500ms (debounce) parte PUT /api/v1/game-sessions/{id}/scores-polymorphic con { scoringType, scoreData }
    And l'UI riflette in modo ottimistico il valore modificato prima della conferma
  Osservabile ✅: come Host è montato PolymorphicScoreEditor (uno tra data-testid="points-editor" | "binary-win-editor" | "objectives-editor" | "ranking-editor") + il controllo del tipo attivo modifica il valore a schermo + nessun errore Console/Network sul PUT (200)
  Route: /sessions/[id]/live
  Utente: marco (Host)
  Nota: se "ScoringConfigured" NON è stato emesso, il tab Score mostra data-slot="scoring-panel-empty" (placeholder) → lo scenario è ⚠️ blocked-env, non fail (vedi § Nota tecnica).
```

```gherkin
Scenario U6-07 [Flow]: Viewer non-host vede il renderer punteggi in sola lettura
  Given sono loggato come sara@meepleai.test (giocatrice non-host della sessione s-azul-live)
    And apro /sessions/{azulLiveSessionId}/live sul tab "Score"
    And PRECONDIZIONE AMBIENTALE: "ScoringConfigured" è stato emesso (scoringType impostato)
  When osservo il pannello punteggi (senza poterlo modificare)
  Then vedo ScoringPanelRenderer in sola lettura (nessun editor per il mio ruolo — IDOR guard host-only)
    And in base al tipo: Points → lista ordinata con badge leader; Ranking → posizioni 1°/2°/3°; BinaryWin → esito Win/Lose; Objectives → conteggio X/Y + spunte
  Osservabile ✅: presente uno tra data-slot="scoring-panel-points" | "scoring-panel-ranking" | "scoring-panel-binary-win" | "scoring-panel-objectives" + NESSUN editor (nessun input/radio/checkbox/handle editabile) per il ruolo non-host
  Route: /sessions/[id]/live
  Utente: sara (Player)
  Nota: condivide il gate "ScoringConfigured" con U6-06 → ⚠️ blocked-env se non emesso.
```

```gherkin
Scenario U6-08 [Flow]: Note della sessione — nota personale salvata localmente
  Given sono loggato come marco@meepleai.test
    And esiste una sessione di marco (es. s-azul-live o s-brass-041)
  When apro /sessions/{sessionId}/notes
    And scrivo del testo nell'area "My Notes" (Textarea, aria-label "Personal session notes")
    And clicco "Save Notes"
  Then la nota personale viene salvata in localStorage (chiave meepleai_session_notes_{id})
    And il pulsante mostra lo stato "Saved!" temporaneo
  Osservabile ✅: heading "Session Notes" + card "Session Summary" (note ufficiali o empty italic) + card "My Notes" con Textarea + dopo Save il bottone cambia in "Saved!"
  Route: /sessions/[id]/notes
  Utente: marco
```

```gherkin
Scenario U6-09 [Smoke]: Scoreboard dedicato della sessione
  Given sono loggato come marco@meepleai.test
    And esiste una sessione completata con vincitore (seed: s-brass-041, winner marco)
  When apro /sessions/{brassSessionId}/scoreboard
  Then la pagina carica la classifica senza errori 4xx/5xx né errori JS
    And vedo l'heading "Classifica" + badge stato + eventuale banner "Vincitore: {name}"
    And vedo la lista giocatori ordinata (medaglie 🥇/🥈/🥉 o #rank, avatar colore, nome) — o l'empty-state "Nessun giocatore"
  Osservabile ✅: heading "Classifica" + lista giocatori ordinata (o empty-state legittimo) + assenza di errori Console/Network
  Route: /sessions/[id]/scoreboard
  Utente: marco
```

```gherkin
Scenario U6-10 [Flow]: Join sessione via link token (guest interno)
  Given sono loggato come sara@meepleai.test
    And ho un link di invito sessione con ?token=... (Game Night Improvvisata) per una sessione di marco
  When apro /sessions/{sessionId}/join?token={inviteToken}
    And inserisco il mio "Display Name" nel form
    And clicco "Join Session"
  Then il client chiama sessionInvites.joinSession({token, guestName}) e ottiene un connectionToken
    And il connectionToken (cifrato) viene salvato in sessionStorage
    And vengo reindirizzato a /sessions/{targetSessionId}
  Osservabile ✅: CardTitle "Join Session" + input display-name (data-testid="display-name-input") + bottone (data-testid="join-session-button") + dopo submit navigazione a /sessions/{id}
  Route: /sessions/[id]/join
  Utente: sara
  Nota: la precondizione (link ?token=) richiede un invito sessione valido. Se non recuperabile in locale → ⚠️ blocked-env sullo step di submit.
```

```gherkin
Scenario U6-11 [Flow]: Chiusura sessione live → creazione play-record e navigazione
  Given sono loggato come marco@meepleai.test (Host)
    And apro /sessions/{azulLiveSessionId}/live per una sessione InProgress
  When clicco la CTA "Termina sessione" nella LiveTopBar
    And confermo l'endgame nel dialog
    And clicco "Salva partita"
  Then parte POST /live-sessions/{id}/complete
    And il client fa polling del nuovo play-record auto-creato (baseline pre-complete) e, alla risoluzione, naviga a /play-records/{recordId} (o alla lista play-records su timeout)
  Osservabile ✅: CTA "Termina sessione" → dialog endgame → CTA "Salva partita" → navigazione a /play-records/{id} (o /play-records) senza errori Console/Network sul complete (2xx)
  Route: /sessions/[id]/live
  Utente: marco (Host)
  Nota additiva: usa una sessione HP-TEST (non le sessioni seed condivise) per non alterare le precondizioni degli altri scenari.
```

### Play-records

```gherkin
Scenario U6-12 [Smoke]: Lista play-records
  Given sono loggato come marco@meepleai.test
    And il seed ha creato ~18 play-record completati per marco
  When apro /play-records
  Then la lista (PlayHistory) carica senza errori 4xx/5xx né errori JS
    And vedo l'header "Partite Giocate" con l'azione statistiche
    And vedo ≥1 riga partita (gioco, data, badge) — o l'empty-state legittimo
    And è presente la CTA sticky "Registra partita" (data-testid="new-play-record-btn")
  Osservabile ✅: header "Partite Giocate" + ≥1 record o empty-state + CTA nuova partita visibile
  Route: /play-records
  Utente: marco
```

```gherkin
Scenario U6-13 [Flow]: Creazione play-record completo
  Given sono loggato come marco@meepleai.test
    And ho "Azul" nella library
  When apro /play-records/new
    And nel form (SessionCreateForm) step "Gioco": scelgo "Azul" dal catalogo (GameCombobox)
    And step "Quando": imposto la data e (opzionale) location "HP-TEST-2026-07-10"
    And step "Punteggi": aggiungo giocatori con punteggi + imposto visibilità
    And clicco "Crea"/submit
  Then parte POST /play-records e ottengo il recordId
    And vengo reindirizzato a /play-records/{recordId}
    And vedo il toast di successo
  Osservabile ✅: avanzamento step del form + submit → navigazione a /play-records/{id} con id non vuoto + toast di successo
  Route: /play-records/new
  Utente: marco
  Dati creati: play-record HP-TEST-2026-07-10 (location marcata)
```

```gherkin
Scenario U6-14 [Smoke]: Dettaglio play-record
  Given sono loggato come marco@meepleai.test
    And esiste un play-record di marco (seed o quello creato in U6-13)
  When apro /play-records/{recordId}
  Then il dettaglio (PlayRecordDetailView) carica senza errori 4xx/5xx né errori JS
    And vedo l'hero/podio con il nome del gioco + classifica + KPI grid
    And vedo l'etichetta prospettiva (Hai vinto / Hai perso / Pareggio) e le CTA (Condividi, ecc.)
  Osservabile ✅: hero con nome gioco + podio/classifica renderizzati + assenza di errori Console/Network (nessuno stato "Partita non trovata")
  Route: /play-records/[id]
  Utente: marco
```

```gherkin
Scenario U6-15 [Flow]: Modifica play-record (K5 gate — campi editabili)
  Given sono loggato come marco@meepleai.test
    And esiste un play-record editabile di marco (stato non Archived) — es. quello creato in U6-13
  When apro /play-records/{recordId}/edit
    And vedo il banner K5 (EditGateBanner) e modifico un campo consentito (sessionDate o notes o location) → es. note "HP-TEST-2026-07-10 aggiornato"
    And clicco submit/salva
  Then parte PUT /api/v1/play-records/{id} con UpdatePlayRecordRequest (+ xmin per concorrenza)
    And le query play-records vengono invalidate e vengo reindirizzato a /play-records/{recordId}
    And vedo il toast di successo
  Osservabile ✅: form pre-compilato (SessionCreateForm mode="edit") + banner K5 + submit → navigazione a /play-records/{id} + toast di successo
  Route: /play-records/[id]/edit
  Utente: marco
```

```gherkin
Scenario U6-16 [Smoke]: Statistiche play-records
  Given sono loggato come marco@meepleai.test
    And il seed ha creato play-record completati per marco
  When apro /play-records/stats (redirect a /play-records?tab=stats) — o /play-records e clicco l'icona statistiche
  Then la StatisticsView carica senza errori 4xx/5xx né errori JS
    And vedo il filtro range (Tutto / 30g / 90g / 12 mesi), la KPI grid (Partite/Giochi/Win rate/Preferito) e i grafici (giochi più giocati, win-rate)
    And cliccando un preset di range il contenuto si aggiorna
  Osservabile ✅: data-testid="stats-page" + KPI grid + almeno un grafico o empty-state + il click su un preset range cambia la vista
  Route: /play-records/stats
  Utente: marco
```

```gherkin
Scenario U6-28 [Flow]: Ciclo CRUD play-record — crea → edita → elimina (con verifica di persistenza)
  Given sono loggato come marco@meepleai.test
    And ho "Azul" nella library (seed: marco possiede Azul)
  # --- CREATE ---
  When apro /play-records/new
    And nel form (SessionCreateForm) step "Gioco" scelgo "Azul" dal catalogo (GameCombobox)
    And step "Quando" imposto la data e location "HP-TEST-2026-07-10 CRUD"
    And step "Punteggi" aggiungo me stesso (marco) con un punteggio + visibilità Private
    And submit ("Crea")
  Then parte POST /play-records → navigo a /play-records/{recordId} con id non vuoto + toast di successo
    And apro /play-records: la nuova riga (Azul, location "HP-TEST-2026-07-10 CRUD") è nella lista (PlayHistory)
    And PERSISTENZA CREATE — ricarico /play-records (F5): la riga è ANCORA presente (persistita nel backend, non solo cache ottimistica)
  # --- EDIT ---
  When apro /play-records/{recordId}/edit
    And vedo il banner K5 (EditGateBanner) e modifico un campo consentito → notes "HP-TEST-2026-07-10 CRUD nota-editata"
    And submit/salva
  Then parte PUT /api/v1/play-records/{recordId} (UpdatePlayRecordRequest + xmin, nessun conflitto 409 sull'happy path) → redirect /play-records/{recordId} + toast di successo
    And il dettaglio mostra la nota aggiornata
    And PERSISTENZA EDIT — ricarico /play-records/{recordId} (F5): il valore "nota-editata" PERSISTE
  # --- DELETE ---
  When apro /play-records/{recordId}/edit
    And clicco "Cancella partita" nell'EditGateBanner (aria-label playRecords.edit.banner.deleteAction)
    And nel dialog di conferma clicco il bottone distruttivo di conferma (playRecords.edit.delete.confirm)
  Then parte DELETE /api/v1/play-records/{recordId} → redirect /play-records + toast di eliminazione
    And la riga "HP-TEST-2026-07-10 CRUD" NON è più nella lista
    And PERSISTENZA DELETE — ricarico /play-records (F5): la riga resta ASSENTE (eliminata nel backend)
  Osservabile ✅: dopo CREATE la riga è in data-testid="play-history" e sopravvive al reload · dopo EDIT il valore modificato è nel dettaglio e sopravvive al reload · dopo DELETE (dialog conferma → DELETE) la riga sparisce e resta assente al reload (nessun errore Console/Network sui 3 verbi: 2xx POST/PUT/DELETE)
  Route: /play-records/new → /play-records/[id]/edit → /play-records
  Utente: marco
  Dati creati: play-record "HP-TEST-2026-07-10 CRUD" (creato e rimosso nello stesso ciclo)
  Nota: scenario di ciclo di vita completo (spec §3.1) — estende U6-13 (create) + U6-15 (edit) con la gamba DELETE e i reload di verifica su tutte e 3 le operazioni. Il Delete è esposto SOLO dalla pagina edit (la pagina dettaglio /play-records/[id] non ha delete). Delete eseguito solo su dato HP-TEST (mai su record seed).
```

### Players

```gherkin
Scenario U6-17 [Smoke]: Lista giocatori
  Given sono loggato come marco@meepleai.test
    And il seed ha popolato i play-record dei 5 utenti (i giocatori derivano da questi)
  When apro /players
  Then la lista (PlayersLibraryView) carica senza errori 4xx/5xx né errori JS
    And vedo l'hero giocatori con i KPI (partite totali / giochi distinti) + la ricerca
    And vedo ≥1 card giocatore (nome + "{n} partite") — o l'empty-state legittimo
    And cliccando una card navigo a /players/{id}
  Osservabile ✅: root data-slot="players-library-view" + hero + ≥1 card giocatore o empty-state + il click su una card naviga a /players/{id}
  Route: /players
  Utente: marco
```

```gherkin
Scenario U6-18 [Smoke]: Dettaglio giocatore con tab
  Given sono loggato come marco@meepleai.test
  When apro /players/{playerId} (da una card in U6-17)
  Then il dettaglio (PlayerDetailView) carica senza errori 4xx/5xx né errori JS
    And vedo il nome del giocatore + la connection bar
    And vedo i tab (Sessions / Games / Achievements / Stats) con default "sessions"
    And cliccando un tab (?tab=) il contenuto del pannello cambia
  Osservabile ✅: root data-slot="player-detail-view" + nome giocatore + tab navigabili (il click su un tab aggiorna ?tab= e il pannello)
  Route: /players/[id]
  Utente: marco
```

```gherkin
Scenario U6-19 [Smoke]: Sotto-pagina giocatore — Achievements
  Given sono loggato come marco@meepleai.test
  When apro /players/{playerId}/achievements
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'heading "Achievements" + back-link "Back to {player}"
    And vedo la griglia badge (badge con tier/data) — o l'empty-state "No badges yet" (lucchetto)
  Osservabile ✅: heading "Achievements" + griglia badge o empty-state legittimo + assenza errori Console/Network
  Route: /players/[id]/achievements
  Utente: marco
  Nota: usa api.badges.getMyBadges() (badge dell'utente corrente) — smoke valido comunque (heading + lista/empty).
```

```gherkin
Scenario U6-20 [Smoke]: Sotto-pagina giocatore — Games Played
  Given sono loggato come marco@meepleai.test
  When apro /players/{playerId}/games
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'heading "Games Played" + back-link
    And vedo la lista giochi numerata (nome, sessioni, avg pts) — o l'empty-state "No games recorded yet."
  Osservabile ✅: heading "Games Played" + lista giochi o empty-state + assenza errori Console/Network
  Route: /players/[id]/games
  Utente: marco
```

```gherkin
Scenario U6-21 [Smoke]: Sotto-pagina giocatore — Sessions
  Given sono loggato come marco@meepleai.test
  When apro /players/{playerId}/sessions
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'heading "Sessions" + back-link
    And vedo le card sessione (stato color-coded, data, durata, player count; Trophy se vincitore) linkate a /sessions/{id} — o l'empty-state "No sessions found"
  Osservabile ✅: heading "Sessions" + card sessione o empty-state + le card linkano a /sessions/{id}
  Route: /players/[id]/sessions
  Utente: marco
```

```gherkin
Scenario U6-22 [Smoke]: Sotto-pagina giocatore — Statistics
  Given sono loggato come marco@meepleai.test
  When apro /players/{playerId}/stats
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'heading "Statistics" + back-link
    And vedo le 4 KPI card (Total Sessions / Total Wins / Win Rate / Unique Games) + i breakdown per gioco (Sessions by Game / Average Scores)
  Osservabile ✅: heading "Statistics" + 4 KPI card + almeno una lista breakdown (o empty-state "No sessions/score data recorded")
  Route: /players/[id]/stats
  Utente: marco
```

### Pubbliche (join & shared)

```gherkin
Scenario U6-23 [Smoke]: Landing pubblica /join (waitlist Alpha)
  Given non sono autenticato (route pubblica)
  When apro /join
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo la superficie waitlist Alpha (form di iscrizione) renderizzata dopo l'hydration
  Osservabile ✅: pagina pubblica raggiungibile senza login + form waitlist visibile + assenza errori Console/Network
  Route: /join
  Utente: anonimo
```

```gherkin
Scenario U6-24 [Flow]: RSVP pubblico a serata di gioco via codice — Accetta
  Given non sono autenticato (route pubblica)
    And esiste un invito valido non scaduto per una game night pubblicata (seed: e-marco-serata / e-club-night, _publish: true) con token {code}
  When apro /join/event/{code}
    And digito un display name (opzionale) e clicco "Accetta" (Accept)
  Then parte POST /api/v1/game-nights/invitations/{token}/respond con action=accept
    And la superficie mostra la conferma "già risposto" (respondedByName) in sola lettura
  Osservabile ✅: root data-slot="public-join-event-page" + banner pubblico + azione Accetta → surface di conferma risposta (senza errori Console/Network sul respond 2xx)
  Route: /join/event/[code]
  Utente: anonimo
  Nota: la precondizione (token invito valido) richiede una game night pubblicata + il relativo invito. Se il token non è recuperabile in locale → ⚠️ blocked-env sullo step di respond. Il flusso di creazione/pubblicazione game night è in U5.
```

```gherkin
Scenario U6-25 [Flow]: Vista guest pubblica di una sessione live via codice
  Given non sono autenticato (route pubblica)
    And esiste una sessione live con join-code (es. s-azul-live InProgress)
  When apro /join/session/{code}
  Then la pagina pubblica carica (GET /live-sessions/code/{code}/public) senza errori 4xx/5xx né errori JS
    And vedo il nome del gioco come heading + il badge di stato + il codice sessione
    And vedo la scoreboard read-only (LiveScoreboard) + la lista giocatori attivi
    And vedo il disclaimer "Non serve registrazione per visualizzare questa partita."
  Osservabile ✅: heading nome gioco + scoreboard read-only + lista giocatori + disclaimer (nessuno stato "Sessione non trovata")
  Route: /join/session/[code]
  Utente: anonimo
  Nota: il join-code della sessione va recuperato dall'host (LiveTopBar / dettaglio sessione). Se non recuperabile → ⚠️ blocked-env.
```

```gherkin
Scenario U6-26 [Smoke]: Play-record condiviso via token pubblico
  Given non sono autenticato (route pubblica)
    And esiste un play-record con link di condivisione valido (token) — es. condiviso da /play-records/{id} tramite "Condividi"
  When apro /play-records/shared/{token}
  Then la vista pubblica (PlayRecordPublicView) carica senza errori 4xx/5xx né errori JS
    And vedo il dettaglio del play-record in sola lettura (hero/podio, classifica, KPI) — SENZA le CTA da creatore (Condividi/Aggiungi foto/Modifica)
  Osservabile ✅: hero con nome gioco + podio/classifica read-only + assenza di CTA creatore + assenza errori Console/Network (nessuno stato "Partita Non Trovata")
  Route: /play-records/shared/[token]
  Utente: anonimo
  Nota: il token di condivisione va generato da un play-record esistente (dialog Condividi). Se non recuperabile → ⚠️ blocked-env.
```

```gherkin
Scenario U6-27 [Flow]: Guest join a sessione live via token (Game Night Improvvisata)
  Given non sono autenticato (route pubblica /join/*, whitelisted in middleware PUBLIC_PREFIXES)
    And un host ha condiviso un token di guest-join per una sessione live (es. s-azul-live)
  When apro /join/{token}
    And inserisco il nome ospite "HP-TEST-2026-07-10 Ospite" e confermo l'ingresso
  Then entro nella vista guest (GuestJoinView risolve il token)
    And vedo la scoreboard read-only + il form di proposta punteggio
  Osservabile ✅: pagina pubblica raggiungibile senza login + campo nome ospite → dopo conferma scoreboard read-only + form proposta punteggio presenti (nessuno stato "sessione non trovata", nessun errore Console/Network)
  Route: /join/[token]
  Utente: anonimo (ospite)
  Nota: il token di guest-join va recuperato dall'host (condivisione sessione live). Se non recuperabile in locale → ⚠️ blocked-env. Aggiunto in A-FINAL (route top-level rilevata dalla guardia di copertura).
```

### CRUD nota personale sessione

```gherkin
Scenario U6-29 [Flow]: Nota personale sessione — persistenza cross-reload + svuotamento (crea → edita → cancella)
  Given sono loggato come marco@meepleai.test
    And esiste una sessione di marco (es. s-brass-041 Completed, o s-azul-live)
  # --- CREATE (scrive la chiave localStorage) ---
  When apro /sessions/{sessionId}/notes
    And scrivo "HP-TEST-2026-07-10 nota-personale" nel Textarea "My Notes" (aria-label "Personal session notes")
    And clicco "Save Notes"
  Then il bottone mostra lo stato temporaneo "Saved!"
    And la nota è scritta in localStorage (chiave meepleai_session_notes_{sessionId})
    And PERSISTENZA CREATE — ricarico /sessions/{sessionId}/notes (F5): il Textarea è pre-popolato con "HP-TEST-2026-07-10 nota-personale" (riletto da localStorage in useEffect)
  # --- EDIT (sovrascrive la stessa chiave) ---
  When modifico il testo in "HP-TEST-2026-07-10 nota-editata" e clicco "Save Notes"
  Then il bottone torna in "Saved!"
    And PERSISTENZA EDIT — dopo reload (F5) il Textarea mostra "HP-TEST-2026-07-10 nota-editata"
  # --- DELETE (svuota + salva: nessun bottone Delete dedicato) ---
  When svuoto completamente il Textarea e clicco "Save Notes"
  Then la chiave localStorage viene sovrascritta con stringa vuota
    And PERSISTENZA DELETE — dopo reload (F5) il Textarea è vuoto (nessun contenuto ripristinato)
  Osservabile ✅: dopo Save il bottone diventa "Saved!" · il valore digitato sopravvive al reload (create) · il valore modificato sopravvive al reload (edit) · dopo svuota+Save il Textarea resta vuoto al reload (delete) · nessun errore Console/Network
  Route: /sessions/[id]/notes
  Utente: marco
  Dati creati: nota personale localStorage "HP-TEST-2026-07-10 …" (client-side, azzerata a fine ciclo)
  Nota: la nota personale è client-side (localStorage, chiave meepleai_session_notes_{id}) — NON persiste sul backend e NON è un flusso CQRS. La UI NON espone un bottone "Elimina" dedicato: la cancellazione happy-path è svuotare il Textarea e ri-salvare (salva ""). La card "Session Summary" (session.notes ufficiali) è sola-lettura e non modificabile da questa pagina. Complementa U6-08 (che copre solo il Save iniziale) con le gambe reload/edit/clear.
```

---

## Auto-verifica (checklist autore)

- **Copertura route**: tutte le 24 route dell'area U6 (23 in `_coverage-map.md` § U6 + `/join/[token]` top-level aggiunta in A-FINAL) compaiono nella matrice, ognuna mappata ad ≥1 scenario. Nessun buco, nessun `smoke-aggregato`, nessuno `skip`. ✓
- **Ogni scenario ha ≥1 osservabile** strutturale (`Osservabile ✅`), ancorato a marker a schermo (data-slot/data-testid, heading, lista/empty-state, navigazione), non a testo letterale generato da LLM. ✓
- **Solo happy path**: nessuno scenario negativo/errore/edge. I gate ambientali (scoring `ScoringConfigured`, token invito/condivisione, join-code) sono marcati `⚠️ blocked-env` — distinti da fail. ✓
- **Ciclo CRUD & persistenza (spec §3.1)**: U6-28 copre il ciclo completo del **play-record** (crea → edita → **elimina**) con **reload di verifica** dopo ogni operazione (l'entità persiste dopo create+reload, il valore persiste dopo edit+reload, resta assente dopo delete+reload); U6-29 copre la **nota personale sessione** (create/edit/clear + reload). Delete del play-record verificato nel codice: `EditGateBanner` → dialog → `DELETE /play-records/{id}` (`useDeleteRecord`, AC-4.6), esposto solo dalla pagina edit. **Delete assenti annotati (nessuno inventato)**: sessione live (no hard-delete; `api.sessions.abandon` non cablato in `/sessions/*`; l'"archivia" utente vive in `/library` → U3); nota ufficiale `session.notes` (read-only). Vedi § Nota CRUD. ✓
- **Dati creati marcati** `HP-TEST-2026-07-10` (U6-02 sessione, U6-13/U6-28 play-record, U6-15 update, U6-29 nota); ordine additivo (crea nuovi dati; U6-11 usa una sessione HP-TEST, non le sessioni seed condivise). I `Delete` (U6-28 play-record, U6-29 nota) operano **solo** su dati HP-TEST creati nello stesso ciclo. ✓
- **Flow vs Smoke** coerenti con `_coverage-map.md`: 15 Flow (U6-02, 03, 05, 06, 07, 08, 10, 11, 13, 15, 24, 25, 27, 28, 29) + 14 Smoke per liste/dettagli/sotto-pagine read-only. ✓
- **29 scenari totali** (15 Flow / 14 Smoke) su 24 route — la route `sessions/[id]/live` porta 4 scenari (U6-05 shell, U6-06 host scoring, U6-07 renderer read-only, U6-11 complete→play-record); U6-27 copre la guest-landing `/join/[token]`; U6-28 (ciclo CRUD play-record) attraversa `/play-records/new` → `/play-records/[id]/edit` → `/play-records`; U6-29 (CRUD nota) su `/sessions/[id]/notes`.
