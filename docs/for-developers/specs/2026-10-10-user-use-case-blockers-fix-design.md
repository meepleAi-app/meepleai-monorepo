# Correzione dei bloccanti lato utente — revisione del panel

**Data**: 2026-10-10 · **Issue**: #4154 #4155 #4156 #4157 #4158 · **Origine**: verifica nel browser dei casi d'uso, in [`user-use-cases.md`](../user-use-cases.md)
**Metodo**: `/sc:spec-panel`, in modalità critica con discussione sui punti di decisione. Panel: Wiegers (requisiti), Adzic (esempi), Cockburn (attori e obiettivi), Fowler (confini e contratti), Nygard (modi di guasto), Crispin (test).
**Base di fatti**: i corpi delle issue; ADR-083, ADR-089, ADR-090, ADR-094; [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md). Ogni affermazione su un file è stata riletta sul codice di `main-dev` a `f8829d954`, non dedotta.

## Sintesi

| Issue | Natura | Correzione | Decisione (presa il 2026-10-10) |
|---|---|---|---|
| #4154 | regressione di contratto | togliere `type` da `AgentDtoSchema` + test di contratto sul payload reale | — |
| #4155 | flusso irraggiungibile | rendere raggiungibile la dichiarazione di possesso in tre punti + 403 con codice macchina | **D1 = A**: il cancello resta |
| #4156 | aggregato sbagliato | portare le pagine `/sessions/[id]/*` **e lo Score tab** su `LiveGameSession`, completando ADR-083 Fase 1 e il «ritiro delimitato» di ADR-089 | **D3**: lo Score tab resta in #4156 |
| #4157 | contratto a sostituzione completa | aggiornamento parziale lato backend (campi nullable) | **D2 = A**: parziale nel backend |
| #4158 | parametri scartati | passare `search`, `page` e `pageSize` al server; `GET /games/{id}` per risolvere un id | — |

**Ordine raccomandato**: #4154 → in parallelo #4157 e #4158 → #4155 (dopo D1) → #4156. Le ragioni sono in [§ Ordine e dipendenze](#ordine-e-dipendenze).

---

## #4154 — `AgentDtoSchema` esige un campo che il backend non manda più

**FOWLER**: è un contratto rotto fra due consegne della stessa decisione. #4147 ha tolto `Type` da `AgentDto` e nessuno ha aggiornato il lettore. Va corretto il caso, ma soprattutto la regola che l'ha permesso: lo schema **esige** un campo che nessun consumatore legge. `AgentSelector` usa `AgentOption` (la persona statica di `DEFAULT_AGENTS`), non `AgentDto`. `AgentCharacterSheet` usa un suo `AgentDetailData`, e nessuna pagina lo monta.

**NYGARD**: il modo di guasto è il peggiore possibile. Un `200` con dati validi diventa la perdita totale della funzione, e l'unica traccia è un log in console. Non chiedo uno schema permissivo, perché il repo ha scelto apposta di «fallire rumorosamente» su `isSystemDefined`. Chiedo che sia **obbligatorio solo ciò che qualcuno legge**.

**WIEGERS**: allora il requisito è verificabile: *«`GetAllAgentsResponseSchema` e `AgentDtoSchema.array()` accettano la risposta che il backend produce oggi»*. L'oracolo è il payload reale, non una fixture scritta a mano.

### Soluzione

1. Togliere `type` da `AgentDtoSchema` (`lib/api/schemas/agents.schemas.ts:19`) e quindi dal tipo `AgentDto`. Il compilatore elenca i consumatori: si correggono quelli e non si cerca con `grep`, perché in quest'area un grep sovrastima (vedi ADR-094 §1).
2. Aggiornare le fixture di `agentsClient.*.test.ts`, che oggi contengono `type` e per questo non vedono il difetto.

### Criteri di accettazione

- **Rosso prima**: un test di schema che valida il payload catturato da `GET /api/v1/agents?scope=my-library` (senza `type`) con `GetAllAgentsResponseSchema`. Oggi fallisce; dopo passa. Lo stesso vale per `AgentDtoSchema.array()` sul payload di `GET /api/v1/games/{id}/agents`.
- **Nel browser**: `/library` senza errori di validazione in console. In `/sessions/{id}/live` il pannello dell'assistente non mostra più «Impossibile avviare l'assistente».
- **Fuori dall'ambito, ma da verificare subito dopo**: che il lancio dell'agente in sessione (`/agent/launch`) riesca davvero. Il difetto dello schema lo nasconde, e non è detto che dietro funzioni.

> ⚠️ #4138 è ancora aperta e un'altra sessione ci sta lavorando. Questa correzione va **coordinata** con quella linea, o fatta lì dentro, per non produrre due PR in conflitto sugli stessi file.

---

## #4155 — la dichiarazione di possesso non è raggiungibile

**COCKBURN**: partiamo dall'attore e dall'obiettivo. Un `User` vuole sapere una regola di un gioco che ha sul tavolo. Il sistema lo ferma per proteggere il manuale, ed è legittimo, ma non gli dice cosa fare e non gli dà nessuna porta. Il caso d'uso «dichiarare il possesso» (AI-15) non ha uno scenario principale: esiste solo come componente.

**FOWLER**: prima della UI c'è una decisione di policy, perché il repo ne contiene **due** che si contraddicono:

- [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md): a chi non possiede il gioco il contenuto protetto arriva **parafrasato**. La postura alpha dichiarata è «fail-open priority over strict compliance»;
- `RagAccessService` + `StreamQaQueryHandler.cs:106-120` («Bug B5», #443): a chi non possiede il gioco la risposta è **negata**, e il commento la motiva come correzione di sicurezza: senza, *«any authenticated user could stream-QA a non-public game's KB»*.

Nessun ADR registra il cancello. La prima azione è scegliere, e scriverlo.

**NYGARD**: chiunque scelga, il frontend non deve riconoscere il caso confrontando il testo del messaggio. `ForbiddenException` oggi porta solo una stringa italiana. Serve un **codice macchina** nel corpo dell'errore, altrimenti la prossima traduzione rompe la CTA in silenzio.

**ADZIC**: concretizzo i tre punti d'ingresso con esempi.

### D1 — decisione richiesta

| Opzione | Effetto | Rischio |
|---|---|---|
| **A. Tenere il cancello** e rendere raggiungibile la dichiarazione (raccomandata) | cambia solo la UI; B5 resta chiuso | nessuno nuovo; costa un passaggio in più all'utente |
| B. Allentare il cancello e affidarsi ai livelli copyright (parafrasi) | chiunque interroga qualunque KB indicizzata | riapre B5; cambia la postura legale (ADR-059) e vuole un ADR e il via del proprietario del prodotto |

Il panel raccomandava **A**: è reversibile, non tocca la sicurezza e sblocca subito il flusso. **Decisione del 2026-10-10: A.** B resta una decisione di prodotto da prendere a parte, se e quando servirà.

### Soluzione (con A)

1. **Backend**: il diniego porta un codice, per esempio `errorCode: "rag_ownership_required"` con il `gameId`, tramite l'handler di eccezioni esistente. Il messaggio resta per i log.
2. **Tre punti d'ingresso**, tutti sul `OwnershipDeclarationDialog` esistente (con la sua casella di conferma):
   - **dove serve**: nella chat, al primo diniego con quel codice, un riquadro spiega il motivo (copyright dei manuali) e offre «Dichiara di possedere {gioco}». Dopo la conferma la domanda viene rinviata;
   - **dove si acquisisce**: nell'aggiunta alla libreria (`AddGameDrawer`/`CatalogSearchStep` e `FirstGameStep` dell'onboarding), una casella «Possiedo una copia di questo gioco»;
   - **dove si guarda**: nel dettaglio `/games/[id]`, se il gioco è in libreria e non è dichiarato.
3. **Fuori dall'ambito**: i contenitori orfani (`HomeFeed`, `LibraryPanel`, `GameTableZoneTools`). Vanno rimontati o tolti in un intervento di pulizia a parte; non vanno rimontati per riavere il pulsante.
4. **Documentazione**: un ADR breve che registra il cancello (chi passa, perché, dove si dichiara). Oggi esiste solo nei commenti del codice.

### Criteri di accettazione

```gherkin
Scenario: la chat spiega il diniego e offre la dichiarazione
  Given un utente User con Azul in libreria e possesso non dichiarato
  When chiede una regola in una chat su Azul
  Then vede la spiegazione e il pulsante «Dichiara di possedere Azul», non «An error occurred»
  When conferma la dichiarazione
  Then POST /library/{id}/declare-ownership risponde 200
  And la stessa domanda riceve una risposta con citazioni di pagina

Scenario: il cancello resta chiuso per chi non dichiara
  Given un utente User con il gioco in libreria e possesso non dichiarato
  When interroga lo stream QA via API
  Then riceve 403 con errorCode "rag_ownership_required"

Scenario: dichiarazione all'aggiunta
  Given un utente aggiunge un gioco del catalogo con la casella «Possiedo una copia» spuntata
  Then la voce di libreria ha OwnershipDeclaredAt valorizzato
```

- **Rosso prima**: un test di componente di `ChatThreadView` in cui un 403 con `errorCode` mostra la CTA. Oggi mostra il messaggio d'errore generico.
- **Rosso prima**: un test di integrazione backend in cui il 403 contiene `errorCode`.
- **Viaggio completo**: uno spec Playwright con login reale (registrazione o account seed → aggiunta → dichiarazione → domanda → risposta con citazione). È il percorso che oggi nessun test copre.

---

## #4156 — le pagine di sessione leggono l'aggregato sbagliato

**FOWLER**: il problema è già risolto a metà. ADR-083 Fase 1 (#2501) ha portato `/sessions/[id]/live` su `useLiveSession`, cioè `LiveGameSession`, *«the aggregate the wizards actually create»*. Le altre tre pagine sono rimaste su `useSession` → `api.sessions.getById` → il guscio `GameSession`. Non serve una decisione architetturale nuova: serve finire quella presa. L'id nella rotta è un id di `LiveGameSession`: lo usano il wizard, `HomeFeed`, `PlayPanel` e `PrivateGameHub`.

**NYGARD**: niente fallback che prova un endpoint e poi l'altro. ADR-089 §2 lo vieta di fatto: si sceglie l'SSOT per responsabilità. Le altre informazioni si raggiungono **dai link di correlazione** (`CorrelatedGameSessionId`, `TrackingSessionId`), non indovinando l'aggregato dall'id.

**CRISPIN**: e serve una guardia, perché questa regressione si è già ripresentata una volta: un test architetturale che vieti `useSession` e `api.sessions.getById` sotto `app/(authenticated)/sessions/[id]/`.

### Soluzione — mappa per pagina (da ADR-089 §1)

| Pagina | Legge oggi | Deve leggere |
|---|---|---|
| wizard, dopo «Crea Sessione» | naviga su `/sessions/{id}` | naviga su `/sessions/{id}/live`, come gli altri chiamanti |
| `/sessions/{id}` (riepilogo) | `GameSession` → 404 | `LiveGameSession`: se è in gioco rimanda a `/live`; se è completata, totali da `RoundScores` e record da `PlayRecord` (evento `LiveSessionCompletedEvent`) |
| `/sessions/{id}/scoreboard` | `GameSession` → 404 | `LiveGameSession`, giocatori ordinati per **totale** decrescente (chiude anche SES-16) |
| `/sessions/{id}/notes` | `GameSession` → 404 | `LiveGameSession` per l'intestazione; le note private restano dove sono oggi (vedi D3) |

### D3 — decisione: lo Score tab resta in #4156, presa il 2026-10-10

Il panel aveva raccomandato una issue separata. Si è deciso di tenere tutto in #4156: le pagine di lettura e la scrittura dei punteggi arrivano insieme, e la issue chiude l'intero percorso «crea → gioca → segna → riepilogo».

### Lo Score tab — rotto due volte

**FOWLER**: in `ScoreTabContent.tsx` (`app/(authenticated)/sessions/[id]/live/_components/`) i difetti sono due, indipendenti:

1. **Lettura**: `scoringType` viene da `useLiveSessionStore` e non è mai impostato. Il commento lo collega a un TODO (#1899-followup) che ne blocca il cablaggio. Di qui «Caricamento punteggi…» senza nessuna chiamata.
2. **Scrittura**: `useUpdateSessionScores` scrive su `PUT /api/v1/game-sessions/{id}/scores-polymorphic`, cioè SessionTracking, passando un id di `LiveGameSession`. È la stessa forma di #4114, e il risultato sarebbe un 404.

**WIEGERS**: il requisito si ricava dal modello che l'utente ha già configurato. Il wizard al passo «Configura Punteggi» chiede **dimensioni** (nome e unità), cioè il `ScoringConfig` di `LiveGameSession`, e non un tipo polimorfico. Lo Score tab deve quindi presentare un editor **giocatore × dimensione × round** su `RoundScores`. Questo è anche l'SSOT dichiarato da ADR-089 §1, e il «ritiro delimitato» dello scoring polimorfico dal percorso live che lo stesso ADR lascia come debito.

**NYGARD**: c'è un conflitto documentale da chiudere nella stessa PR. CLAUDE.md (§ *Live-session scoring*) dice ancora che lo scoring polimorfico è «the current pattern» e che si scrive con `useUpdateSessionScores`. ADR-089, che è Accepted, dice il contrario per il percorso live. Se la PR corregge il codice e lascia CLAUDE.md com'è, la prossima sessione ricablerà il polimorfico.

**Soluzione per lo Score tab**

- **Lettura**: dimensioni e punteggi da `LiveSessionDto` (`useLiveSession`), totali per giocatore calcolati dal backend (`RecalculatePlayerScores`).
- **Scrittura**: `POST /api/v1/live-sessions/{id}/scores` (`RecordScore`: `playerId`, `round`, `dimension`, `value`) e `PUT` per la correzione. Nessuna chiamata a `/game-sessions/*` dalla superficie `/sessions/[id]/live`.
- **Polimorfico**: esce dal percorso live, come prevede ADR-089. Resta ai play-record storici. La regola ESLint `local/no-store-scores-direct` resta valida: vieta di scrivere lo store direttamente e non dice quale endpoint usare.
- **Documentazione**: aggiornare CLAUDE.md § *Live-session scoring* e mettere una riga «Update» in ADR-089, per dire che il ritiro delimitato è eseguito.

**Domanda aperta (da chiudere in fase di piano)**: i tipi polimorfici che oggi l'utente non può comunque usare (vittoria sì/no, obiettivi, classifica) vanno resi con dimensioni convenzionali (`win` 0/1, una dimensione per obiettivo, `rank`), oppure si rinviano? Il wizard oggi non li offre, quindi nessun utente li perde.

### Criteri di accettazione

- **Rosso prima**: un test di `ScoreboardPage` con `api.liveSessions.getSession` simulato e `api.sessions.getById` non simulato. Oggi fallisce perché la pagina chiama il secondo.
- **Rosso prima**: un test architetturale. Nessun file sotto `app/(authenticated)/sessions/[id]/` importa `useSession` né chiama `api.sessions.getById`.
- **Rosso prima**: un test di `ScoreTabContent` in cui l'inserimento di un punteggio chiama `POST /api/v1/live-sessions/{id}/scores`. Oggi chiama `/game-sessions/{id}/scores-polymorphic`.
- **Rosso prima**: lo stesso test architetturale si estende a `/live`. Nessun file sotto `app/(authenticated)/sessions/[id]/` chiama `/api/v1/game-sessions/`.

```gherkin
Scenario: il percorso completo di una partita creata dal wizard
  Given un utente crea una sessione su Azul con la dimensione «punti» e i giocatori Marco e Giulia
  When preme «Crea Sessione»
  Then arriva su /sessions/{id}/live e vede i due giocatori
  When segna 10 per Marco e 30 per Giulia al round 1
  Then POST /live-sessions/{id}/scores risponde 204 per ciascuno
  And dopo un ricaricamento i totali sono ancora 10 e 30
  And /sessions/{id}/scoreboard mostra Giulia per prima
  When chiude la partita
  Then /sessions/{id} mostra il riepilogo con i totali reali, senza achievement di prova
  And la partita registrata derivata (LiveSessionCompletedEvent) porta gli stessi totali
```

- **Nel browser**: `/notes` e `/sessions/{id}` non mostrano più l'errore 404.

---

## #4157 — le preferenze non si salvano

**WIEGERS**: il requisito implicito di un `PUT` a sostituzione completa è che *ogni* client mandi *ogni* campo. Lo rispetta zero client su uno, perché `PreferencesSection` è l'unico chiamante. E il difetto vero non è il `422`, è ciò che succederebbe dopo una correzione ingenua: `ShowProfile`, `ShowActivity` e `ShowLibrary` tornerebbero `true` a ogni salvataggio.

**FOWLER**: oggi nessuna UI scrive quei tre flag; li dichiara solo `authClient.ts:95-97`. Il rischio è latente, ma il contratto lo rende inevitabile appena qualcuno aggiunge quella UI.

### D2 — decisione: A (aggiornamento parziale nel backend), presa il 2026-10-10

| Opzione | Pro | Contro |
|---|---|---|
| **A. Aggiornamento parziale nel backend** (raccomandata): campi nullable in `UpdatePreferencesCommand`, `null` = invariato, validator con `When(x => x.DataRetentionDays.HasValue)` | protegge ogni client presente e futuro | `PUT` con semantica di `PATCH`; va documentato nell'OpenAPI |
| B. Il frontend rimanda l'intero stato letto dal `GET` | nessuna modifica backend | fragile: ogni campo aggiunto in futuro va ricordato in ogni client |

### Criteri di accettazione

- **Rosso prima** (integrazione backend): utente con `dataRetentionDays = 90` e `showProfile = false`. `PUT` con `{theme, language, emailNotifications}` → oggi `422`. Dopo la correzione: `200`, lingua aggiornata, `dataRetentionDays` ancora 90, `showProfile` ancora `false`.
- **Resta rosso**: un `dataRetentionDays: 0` esplicito risponde ancora `422`.
- **Nel browser**: salvare una lingua diversa e ricaricare la pagina mostra la lingua salvata.
- **Fuori dall'ambito**: che la lingua salvata cambi i testi (`IntlProvider` segue `navigator.language`, e `fr` non ha traduzioni). Va in una issue a parte, collegata a SET-07.

---

## #4158 — `api.games.getAll` scarta i parametri

**ADZIC**: l'esempio è già nella issue. «Azul» viene trovato e «Dominion» no, perché il client non manda `search` e filtra la prima pagina. Il backend supporta `search`, `page` e `pageSize` (`GameEndpoints.cs:289-298`).

**WIEGERS**: requisito: *«la ricerca restituisce le corrispondenze dell'intero catalogo, e `page`/`pageSize` del chiamante arrivano al server»*. Il resto dei filtri (`minPlayers`, `maxPlayTime`, `yearFrom`, `bggOnly`…) il server non li conosce. Applicarli lato client su una pagina rende falsi i conteggi di paginazione.

**FOWLER**: due usi diversi, due strumenti diversi. Chi **elenca** passa i parametri al server. Chi **risolve un id** (`ChatThreadView.tsx:247`, `ChatHistoryDrawer.tsx:222`) chiama `GET /api/v1/games/{id}`, non scorre una lista.

### Soluzione

1. `gamesClient.getAll` costruisce la query con `search`, `page` e `pageSize`, e rinuncia al filtro lato client su `search`.
2. Per gli altri filtri: censire i chiamanti (`GamebookUploadView` passa `gamesFilters`). Se nessuno li usa, si tolgono da `GameFilters` (YAGNI). Se qualcuno li usa, si aggiungono alla query del backend nella stessa issue.
3. `ChatThreadView` e `ChatHistoryDrawer` risolvono il gioco per id. Effetto oggi: una chat su un gioco oltre la prima pagina **non ne mostra il titolo**. La risposta invece non è toccata, perché dipende da `thread.gameId` (`ChatThreadView.tsx:350`).

### Criteri di accettazione

- **Rosso prima**: un test di `gamesClient` che verifica che `getAll({ search: 'Dominion' }, undefined, 2, 24)` chiami `httpClient.get` con `?search=Dominion&page=2&pageSize=24`. Oggi la chiamata parte senza query string.
- **Nel browser**: nell'onboarding «Dominion» viene trovato. Una chat su un gioco oltre la prima pagina mostra il titolo del gioco.

---

## Ordine e dipendenze

```
#4154 ──────────────► #4156 (verifica dell'assistente in /live)
#4157 ─┐ (indipendente)
#4158 ─┴───────────► #4155 (la casella nell'onboarding tocca FirstGameStep, come #4158)
D1 = A ─────────────► #4155
#4114 ──────────────► #4156 (stessa famiglia: id di live session su /game-sessions; coordinare)
```

1. **#4154 per prima**: è una regressione fresca su `main-dev`, è la correzione più piccola, e senza di essa non si può verificare l'assistente in sessione dopo #4156. Va coordinata con la linea #4138.
2. **#4157 e #4158 in parallelo**: sono indipendenti e piccole. #4158 va chiusa prima che #4155 tocchi `FirstGameStep`, per non sovrapporre due modifiche allo stesso componente.
3. **#4155**: è la più importante per l'utente, e con D1 = A non ha più blocchi di policy. Prima il codice d'errore nel backend, poi i tre punti d'ingresso.
4. **#4156 per ultima**: è la più ampia, perché con D3 comprende anche la scrittura dei punteggi, e la sua verifica completa dipende da #4154. È la stessa famiglia di #4114 (gli hook SSE su `/game-sessions`). Conviene coordinarle o farle nello stesso ramo, perché toccano la stessa superficie `/sessions/[id]/live`.

## Rischi trasversali

- **Il gate E2E non gira sulle PR verso `main-dev`** (CLAUDE.md, #4032). I viaggi Playwright proposti qui vanno lanciati a mano (`gh workflow run`), con un run di controllo su `main-dev`.
- **Lo stack locale può servire una build vecchia**: ogni verifica nel browser va fatta su immagini ricostruite dal commit della PR, come in questa revisione.
