# Review /sc:spec-panel — Epic #3188: canonicalizzazione modello session-live

> **Metodo**: workflow spec-panel multi-esperto grounded sul codice — 7 agenti ground-truth (verifica claim vs codice reale) → 8 esperti (Wiegers, Fowler, Nygard, Newman, Adzic, Cockburn, Crispin, Completeness Critic; 55 finding) → verifica avversariale (17/20 finding BLOCKING/CRITICAL sopravvissuti) → sintesi.
> **Data**: 2026-07-19 · **Epic**: [#3188](https://github.com/meepleAi-app/meepleai-monorepo/issues/3188) (OPEN, `enhancement`+`tech-debt`+`area/backend`, parent #3157 C2b/C2c).

## Verdetto sintetico

L'epic è **tecnicamente accurato nel suo claim centrale**: il path direct-create con envelope (`CreateSessionCommandHandler.cs:190-200`) hand-builda davvero il link `game_night_sessions` nato `InProgress` con `StartedAt=now` bypassando l'aggregato, e `Session.OpenLiveMode()` non viene mai invocato, quindi `SessionStartedDomainEvent` non scatta e la tracking Session resta `Active/StartedAt=null/IsLive==false` (GT-1 a/c CONFIRMED). La diagnosi dei 5 consumer di liveness (WHY a-e) è corretta e verificata end-to-end da BE (`GetGameNightLiveQueryHandler`, `CompleteCurrentSession`, `FinalizeSessionCommandHandler`) a FE (`mapNightLive.ts`). È **correttamente dimensionato come epic e non come slice**: tocca simultaneamente un invariante di unicità DB, tre read-model BE, il mapper FE e una decisione di prodotto bloccante — non collassabile in una singola PR senza rompere `main-dev`. Tuttavia l'epic ha **quattro gap materiali non marginali**: (1) nessuna slice di data-migration per le righe legacy `InProgress` già persistite (GT-4 REFUTED); (2) incoerenza interna tra North Star (three-phase, corretto) e decomposizione item 2 (two-phase, incompleto — omette `OpenSessionLiveModeCommand`); (3) una **seconda** decisione di prodotto non dichiarata (con N draft coesistenti, quale draft va live? `StartCurrentSession()` promuove sempre il `FirstOrDefault(Pending)`); (4) il guard count-based max-5-per-night non è riconciliato con l'obiettivo multi-draft. La qualità dell'analisi è alta, ma la DoD attuale non è verificabile finché queste lacune non sono chiuse.

## Ground truth: claim dell'epic verificati

| # | Claim dell'epic | Verdetto | Evidence (file:line) |
|---|---|---|---|
| Anomalia core | Direct-create hand-builda il link nato `InProgress`/`StartedAt=now` al persistence layer, bypassando l'aggregato | **CONFIRMED** | `CreateSessionCommandHandler.cs:190-200` + `:207 AddAsync`; solo dentro `if(!SkipGameNightEnvelope && nightEntity!=null)` (:180) |
| WHY(a) | max-1-live garantito solo dall'indice parziale `status='InProgress'`; born-Pending libera lo slot | **CONFIRMED** | `20260719111334_...cs:42-47` + `GameNightSessionEntityConfiguration.cs:44-47` (indice su EF model *e* migration) |
| WHY(b) | `CompleteCurrentSession()` richiede `InProgress` → 409 su born-Pending | **CONFIRMED** | `GameNightEvent.cs:698-699` throw; `CompleteGameNightSessionCommandHandler.cs:99-104` catch→409 |
| WHY(c) | `GetGameNightLiveQueryHandler` costruisce roster via `Status==InProgress` → roster vuoto | **CONFIRMED** | `GetGameNightLiveQueryHandler.cs:78-85` |
| WHY(d) | FE `mapNightLive` deriva live/currentGame/badge/progress da `status==='InProgress'` | **CONFIRMED** | `mapNightLive.ts:182,197,202,160` |
| WHY(e) | Finalize (C1) chiude il link solo se `InProgress` → born-Pending orfano | **CONFIRMED** | `FinalizeSessionCommandHandler.cs:159-168` |
| North Star | `GameNightSession.Create()` nasce `Pending`; `Start()` promuove `Pending→InProgress` | **CONFIRMED** | `GameNightSession.cs:46` (Pending) + `:70-76` (Start, guard hard-throw) |
| Item 2 | Routing via `AddSession()→StartCurrentSession()` è buildabile su metodi esistenti | **CONFIRMED (ma incompleto)** | `GameNightEvent.cs:628-650,678-689` — **manca la 3ª fase** `OpenSessionLiveModeCommand` |
| Item 3 | Guard read-check keyed su `Session.Status==Active` blocca multi-draft | **CONFIRMED (ma mis-scoped)** | `CreateSessionCommandHandler.cs:360-374` — gira **solo** sul branch `GameNightEventId.HasValue` (:338-391); il branch ad-hoc puro (:393-410) non ha alcun read-check |
| Migration | L'epic prevede una migration/backfill per le righe legacy `InProgress` | **REFUTED** | Decomposizione (1)-(5) e DoD: nessun item; grep `3188\|born.?Pending\|StartLive\|backfill` → nessun file. Righe `InProgress` persistite confermate esistenti |
| WHY(c)/(e) FE | FE consuma il roster winner-picker che il BE costruisce via `Status==InProgress` | **PARTIAL** | `mapNightLive.ts:231-235` passa `dto.currentSessionRoster` verbatim; la logica `Status==InProgress` è BE-side, ma il cascade è confermato |

### Correzioni all'epic (da REFUTED/PARTIAL)

- **REFUTED — nessuna data-migration.** Non esiste alcuna migration #3188 e la DoD non contiene un item di backfill, **nonostante righe `InProgress` standalone siano confermate persistite** (`CreateSessionCommandHandler.cs:190-200`) e il codebase abbia già il pattern esatto in `20260719111334_...cs:31-40` (che demota duplicate `InProgress`→`Pending` con `ORDER BY started_at DESC`). Buco di completezza, non un'impossibilità.
- **CORREZIONE — item 2 è a due fasi, il path corretto è a tre.** La North Star elenca correttamente `AddSession()→StartCurrentSession()→OpenSessionLiveModeCommand last`, ma la decomposizione item 2 e la DoD lo descrivono come "two-phase". La 3ª fase (`OpenSessionLiveModeCommand`, `StartGameNightSessionCommandHandler.cs:132`) fa scattare `SessionStartedDomainEvent`/`OpenLiveMode()`, unico setter di `Session.StartedAt`/`IsLive`. Ometterla **riproduce l'anomalia GT-1c**, solo rilocata.
- **CORREZIONE — item 3 conflaziona due branch.** Il guard `Active`-keyed esiste solo sull'attach-to-existing-night; il branch ad-hoc puro (no `GameNightEventId`) non ha read-check e hand-builda anche l'**envelope night** a `Status=InProgress` (`:397-407`) — l'anomalia è più ampia del solo link.

## Finding critici e bloccanti (verificati)

### 1. [BLOCKING · Wiegers] Decisione di prodotto non legata a conseguenze osservabili/testabili
**Issue:** la BLOCKING PRODUCT DECISION è posta come domanda aperta senza decision matrix che mappi ciascuna delle 3 opzioni a contratto endpoint/command, default del flag `StartLive`, backward-compat per i caller esistenti, e quali acceptance test flippano. Finché non è vincolata a comportamento osservabile per branch, **nessun requisito downstream è testabile**.
**Raccomandazione:** convertire in decision table con, per `{draft-only, live-only, both-explicit}`: (a) firma endpoint + default, (b) meccanismo trigger go-live, (c) stato atteso di **link** *e* **tracking Session** immediatamente post-POST, (d) contratto backward-compat, (e) assertion concreta di ogni acceptance test.
**Evidence:** `CreateSessionCommand.cs:36-37`; decomposizione item (1) offre due alternative irrisolte.

### 2. [BLOCKING · Newman] Backward-compatibility: flip born-status è breaking su POST /game-sessions
**Issue:** oggi il DTO pubblico `CreateSessionRequest` strippa `SkipGameNightEnvelope` (`SessionCommandEndpoints.cs:34-67`, #2920), quindi **ogni** POST client-visibile passa dal path enveloped born-`InProgress` — ogni caller corrente si aspetta anche il go-live. Un flip silenzioso del default fa creare a tutti i caller esistenti una night che renderizza not-live (roster vuoto, stato FE `transition`, link orfano su finalize) senza errore né version bump. **Calibrazione:** il refactor è breaking *solo se* cambia la post-condition osservata; routare il direct-create attraverso l'aggregato preservando le semantiche go-live nella stessa request è non-breaking (= additivo).
**Raccomandazione:** rispondere alla product decision e alla compat decision insieme. Se POST deve significare "create draft" di default, trattarlo come breaking: (a) flag `StartLive` opzionale il cui **default preserva l'attuale go-live** (additivo) + deprecation window, oppure (b) nuovo contratto versionato. Precedente RFC 8594 già presente in `SessionCommandEndpoints.cs:124-162`.

### 3. [BLOCKING · Nygard] Nessun piano di deploy-ordering, rollback o analisi dati esistenti
**Issue:** la DoD non ha deploy-ordering, rollback story né existing-data analysis per un cambio che altera contemporaneamente l'enforcement dell'invariante di unicità, tre read-model BE e il live-mapper FE. Se il flip born-status deploya prima del wiring go-live, o la migrazione read-model (item 5) atterra fuori sync col FE, le night attive appena create renderizzano not-live e complete/finalize danno 409. Nessun rollback: una volta che le righe nascono `Pending`, tornare al BE born-`InProgress` lascia tabella mixed-state senza path di riconciliazione.
**Raccomandazione:** sezione "Rollout & Rollback" obbligatoria: (1) **read-model tolerance FIRST** (BE+FE accettano link Pending senza sibling InProgress come draft valido), POI flip born-status, POI wire go-live; (2) read-path forward-compatible; (3) procedura di rollback documentata (feature-flag o down-migration/reconciliation).

### 4. [BLOCKING · Cockburn] Actor/goal di POST /game-sessions mai identificati
**Issue:** l'epic inquadra tutto in termini di system-state e non identifica mai l'attore primario né il goal. Due use case latenti: **UC-A "Logga una partita già giocata"** (garanzia debole, reversibile, nessuna liveness, slot non occupato, night non promossa) e **UC-B "Avvia un tavolo live ora"** (garanzia forte, singleton: unico live-slot, promozione night, SSE, roster). Un singolo endpoint che esegue silenziosamente i side-effect di UC-B su ciò che il chiamante crede un "create" è il classico CRUD-as-use-case overload.
**Raccomandazione:** **BOTH via intent esplicito `StartLive`, DEFAULT a DRAFT (UC-A)** — il goal safe/minimal-guarantee è il default, i goal side-effecting richiedono scelta esplicita. Aggiungere in DoD un main-success-scenario per ciascun use case.

### 5. [BLOCKING→PLAUSIBLE · Adzic] Nessun worked example per la product decision
**Issue:** decisione posta come domanda astratta senza request/response payload, stato link/Session, né proiezione `/live` per le 3 opzioni. **Calibrazione:** l'epic descrive le semantiche in prosa e i metodi aggregati esistono già → "engineers cannot build" è overstated; è un miglioramento SbE da produrre nella spec della slice implementante.
**Raccomandazione:** acceptance table per opzione con GIVEN/WHEN/THEN concreti (link status, tracking Session, occupazione slot indice, output `/live`), firmata dal product; cancellare le opzioni non scelte.

### 6. [BLOCKING→PLAUSIBLE · Crispin] Nessuna migration/backfill per righe born-InProgress legacy
**Issue:** GT-4 conferma righe persistite a `status='InProgress'`/`StartedAt=now` dal direct-create e REFUTA qualsiasi backfill #3188. **Riscopo corretto:** le righe legacy **NON** vengono orfanizzate da Finalize (`FinalizeSessionCommandHandler.cs:161` continua a chiudere qualsiasi link `InProgress`). L'hazard reale è **split-brain per item (5)**: post-flip, i link legacy `InProgress` la cui tracking Session non è mai stata aperta live (`StartedAt=null`) fanno disaccordare `link.Status==InProgress` con `Session.StartedAt`.
**Raccomandazione:** data-migration + test: (1) link legacy `InProgress` con Session finalizzata → riconciliato/chiuso; (2) con Session live → preservato come unico live; (3) idempotenza; template `20260719111334`.

### 7. [CRITICAL · Fowler] Item 5 non dichiara quale bounded context possiede la "canonical liveness"
**Issue:** il codebase legge liveness in due modi incompatibili: GameManagement (`GetGameNightLiveQueryHandler.cs:78-79`) su `link Status==InProgress`; SessionTracking (`FinalizeSessionCommandHandler.cs:227-234`, warning #13) su `Session.StartedAt!=null && FinalizedAt==null`. Il FE legge solo il link status, senza campo che mirroria `Session.IsLive`. Senza decidere l'owner, item 5 rischia di migrare i consumer sul segnale sbagliato o perpetuare la doppia sorgente.
**Raccomandazione:** decisione ADR-style esplicita: canonical liveness = `Session.IsLive` (owned da SessionTracking) e il link Status diventa proiezione derivata via `SessionStartedDomainEvent`/`OpenLiveMode` — **oppure** l'inverso. Prerequisito per item 2 e 4.

### 8. [CRITICAL · Nygard] max-1-live enforcement e mapping 23505→409 sono load-bearing e fragili
**Issue:** l'unico enforcer atomico è l'indice parziale `ix_game_night_sessions_unique_active` (`filter status='InProgress'`); il mapping C2a 23505→409 è keyed sulla **stringa letterale** del nome indice (`CreateSessionCommandHandler.cs:327`). Due failure mode: (a) quando il direct-create non inserisce più un link InProgress, quel catch diventa **dead code** sul create path — il 409 va ristabilito sul nuovo go-live write; (b) item (5) potrebbe rinominare l'indice/predicato, rompendo silenziosamente lo string-match → concurrent go-live emergono come 500 grezzi invece di 409. **Precisazione:** un mapping 23505→409 dedicato **non** esiste già sul go-live path — `StartGameNightSessionCommandHandler` restituisce 409 via xmin `DbUpdateConcurrencyException` sull'aggregato (`:105-112`), non via constraint-name.
**Raccomandazione:** dichiarare che l'indice parziale è l'UNICO enforcer atomico, preservarlo **verbatim** (nome + predicato) attraverso item (5); portare il mapping 23505→409 sul nuovo go-live command; test: due go-live concorrenti → esattamente un 409, mai 500; estrarre il nome indice in costante condivisa.

### 9. [CRITICAL · Completeness] Item 2 omette la 3ª fase (OpenSessionLiveModeCommand)
**Issue:** item 2 lista un go-live a due fasi ma il reference path è a **tre**: `AddSession→StartCurrentSession→OpenSessionLiveModeCommand` (dispatched LAST). La 3ª fase fa scattare `SessionStartedDomainEvent`/`Session.OpenLiveMode()`, unico setter di `StartedAt`/`IsLive`. Ometterla lascia la Session `Active/StartedAt=null/IsLive=false` dopo il "go-live" — riproduce GT-1c.
**Raccomandazione:** rendere item 2 esplicitamente three-phase, mirroring `StartGameNightSessionCommandHandler.cs:89-132` (OpenSessionLiveModeCommand last, dentro la transazione ambient). Correggere "two-phase"→"three-phase" in References.

### 10. [CRITICAL · Adzic] "Multiple drafts coexist" collide col guard corrente
**Issue:** il branch attach-to-existing-night lancia 409 quando `activeSessionCount>0`; un draft appena creato è `Active`, quindi un flip born-Pending naive fa 409 sul **secondo** draft proprio sul path che #19 dice debba avere successo. **Diagnosi rafforzata:** poiché il guard è keyed su `Session.Status==Active` (non link status), il 409 sul secondo draft esiste **già oggi** indipendentemente dal flip → flippare solo il link status è insufficiente; la rilocazione del guard su `link Status==InProgress` at go-live è il cambiamento load-bearing.
**Raccomandazione:** coexistence example: GIVEN night N con draft D1 (Session Active) WHEN POST secondo draft (G2) THEN secondo link Pending, NO 409; AND `/live` → status='transition', plannedGames=[{G1:upcoming},{G2:upcoming}].

### 11. [CRITICAL · Adzic] Selezione del draft al go-live: ambiguità di prodotto nascosta in FirstOrDefault
**Issue:** `StartCurrentSession()` promuove sempre il `FirstOrDefault(Pending)` (per playOrder) — nessun parametro per selezionare un draft specifico. Con multi-draft coesistenti, "go live" su una night con D1(po1) e D2(po2) promuove D1 a prescindere dall'intento utente. Product decision nascosta in un dettaglio implementativo.
**Raccomandazione:** GIVEN D1 Pending(po1) + D2 Pending(po2), nessuno live WHEN client richiede go-live per D2 THEN (i) `sessionId` obbligatorio promuove esattamente D2, oppure (ii) documentato che go-live promuove sempre lowest-playOrder e la UI previene per-draft go-live.
**Evidence:** `GameNightEvent.cs:678-689`; `GameNightSession` è `internal sealed`, go-live deve passare per l'aggregato.

### 12. [CRITICAL · Newman] Contract drift su GameNightSessionDto.Status (item 5)
**Issue:** item 5 è descritto come rename ma è un wire-contract change con due silent-break. L'enum Zod FE è `['Pending','InProgress','Completed','Skipped','Corrupted']` consumato via switch `never` esaustivo. Mode A: stessi valori ma cambio di significato → build FE vecchi parsano ma misrender. Mode B: nuovo valore (es. 'Draft') → il parser fail-fast rompe l'**intero** payload `/live`.
**Raccomandazione:** non reinterpretare i valori enum esistenti in-place. Se serve un nuovo stato liveness, aggiungerlo come **nuovo campo additivo** (es. `isLive` derivato da StartedAt/FinalizedAt); contract test: set status BE == enum Zod FE.
**Evidence:** `mapNightLive.ts:91-108,182-197`; `game-nights.schemas.ts:69-75`.

### 13. [CRITICAL · Newman] go-live come flag vs sub-resource dedicata
**Issue:** item 1 lascia irrisolto "StartLive flag vs distinct command+endpoint", ma il codebase ha già una convention REST: `POST /api/v1/sessions/{id}/{pause|resume|end|complete|abandon}` (`sessionsClient.ts:137-193`). Un go-live come flag nascosto divergerebbe dalla convention e sarebbe non-discoverable in OpenAPI/HATEOAS.
**Raccomandazione:** modellare go-live come sub-resource lifecycle esplicita — `POST /api/v1/sessions/{id}/go-live` (o `/start`) — con propria operation OpenAPI, idempotency e error surface (409). **Precisazione path:** i verbi lifecycle stanno su `/api/v1/sessions/{id}/...`, mentre `/game-sessions/{id}/...` è create/finalize/scores → il go-live va su `/sessions/{id}/go-live`.

### 14. [CRITICAL · Crispin] Complete/Finalize handler assenti dal blast radius
**Issue:** i due write handler che item (4) esplicitamente riconcilia non sono nel blast radius dichiarato. `CompleteGameNightSessionCommandHandler` richiede `InProgress` e `FinalizeSessionCommandHandler` chiude il link solo se `InProgress`. Il blast radius nomina `CreateSessionCommandHandlerTests` e `GetGameNightLiveQueryHandlerTests` ma né `CompleteGameNightSessionCommandHandlerTests` né `FinalizeSessionCommandHandlerTests`.
**Raccomandazione:** scenari: (1) finalize di draft mai promosso → link NON orfano; (2) complete di night con solo Pending → comportamento riconciliato (409 o auto-promote per decisione); (3) i due guard indipendenti sul complete path tengono entrambi.

### 15. [CRITICAL · Crispin] Il test C2a concorrente va rilocato al go-live
**Issue:** dopo il flip born-Pending, i direct-create non occupano più lo slot → la premise del test C2a si dissolve e la race reale si sposta al go-live. **Precisazioni:** (a) il test C2a NON è realmente concorrente — è una repro deterministica; (b) "provare che l'indice DB è ancora l'ultima linea" è già coperto da `RestoredUniqueIndex_RejectsSecondInProgressLinkInSameNight` (`PauseResumeSessionTests.cs:349`). Il gap netto si riduce a UN test.
**Raccomandazione:** integration test `Task.WhenAll`: due go-live concorrenti su due draft della **stessa** night → esattamente un `InProgress`, l'altro 409 (`MaxLiveSessionsExceededException`), nessuna Session orfana, nessun 500, assertion sul rollback xmin.

### 16. [CRITICAL · Completeness] Item 5 omette la promozione night-level (invariante #15)
**Issue:** item 5 enumera solo i consumer del link ma omette la promozione `GameNightStatus` (`Published→InProgress` via `HandleFirstSessionStarted`/`SessionStartedHandler`, #15). **Riframe:** il segnale live/not-live del FE deriva solo dal link status, quindi un fix link-only restaura il rendering; la conseguenza reale di una night stuck-Published è che la CTA night-level di finalize non compare (`showFinalizeCta` gated su `nightStatus==='InProgress'`, `NightLiveClientView.tsx:181-186`) e `POST /complete` dà 409.
**Raccomandazione:** includere la catena di promozione night (SessionStartedHandler/HandleFirstSessionStarted, #15) nella migration surface di item 5; il nuovo go-live command deve firare `SessionStartedDomainEvent`.

## Finding minori

- **[MAJOR · Fowler] Item 2 infeasible per l'ad-hoc puro.** `AddSession()` richiede la night `Published`/`InProgress` e lancia su `Draft` (`GameNightEvent.cs:632`); l'ad-hoc path non ha envelope Published. L'unica factory implicita, `CreateAdHoc()` (`:132-149`), setta `Status=InProgress` ed è essa stessa un conflate-create-with-go-live domain-level. Espandere item 2 al full aggregate path e portare `CreateAdHoc()` in scope.
- **[MAJOR · Fowler] Flag vs command (control-coupling smell).** `CreateSessionCommand` porta già `SkipGameNightEnvelope` + `SkipKbReadinessGate`; un terzo boolean `StartLive` = 2³ combinazioni. Rifiutare il flag; esporre go-live come endpoint distinto.
- **[MAJOR · Fowler/Completeness] Serve `GameNightEvent.StartSession(sessionId)`.** `StartCurrentSession()` non ha parametro; per multi-draft serve un overload che valida target Pending + riusa `EnsureCanStartSession`.
- **[MAJOR · Nygard/Fowler/Crispin/Cockburn] Guard max-5-per-night non riconciliato.** `CreateSessionCommandHandler.cs:183-187` conta `GameNightSessions` a prescindere dallo status; con N draft si esaurisce il budget con zero live. Decidere: 5 = totale link vs non-terminal vs live-o-completed. Test sul boundary del 6°.
- **[MAJOR · Nygard] Degradazione graceful FE.** Se item 1 introduce un nuovo valore link status, il parser Zod + switch `never` hard-fail l'intero payload `/live`. Il draft nasce con `Pending` esistente; se serve nuovo stato, deploy FE-first.
- **[MAJOR · Nygard] TOCTOU al go-live.** I guard domain sono non-atomici; l'affidabilità è preservata solo perché la UPDATE `status='InProgress'` collide sull'indice. Documentare che il guard domain è convenience UX e l'indice è il backstop autoritativo.
- **[MAJOR · Newman] Error-contract su create.** Se go-live confluisce in POST con `StartLive=true`, il verbo create restituisce 409 ma dichiara solo `Produces(201/400/401)` (`SessionCommandEndpoints.cs:83-85`). Preferire la sub-resource; se resta su POST, aggiungere `.Produces(409)`.
- **[MAJOR · Newman] Response-contract.** POST restituisce 201 senza campo che distingua "draft created" da "live started". La promozione deve essere osservabile (`status`/`isLive` su `CreateSessionResult`).
- **[MAJOR · Wiegers] Item 3 conflaziona i due branch di create.** Specificare coexistence + acceptance test separatamente per attach-to-existing-night (guard Active) e ad-hoc puro (nessun guard oggi).
- **[MAJOR · Crispin] FE contract-test gap.** Aggiungere `game-nights-live.schemas.test.ts` (enum + parse-resilience) + regression `mapNightLive`: night born-Pending-only → `status:'transition'`.
- **[MINOR · Wiegers/Adzic/Cockburn] DoD #4 "test blast radius updated" non enumerato.** Sostituire con checklist esplicita.
- **[MINOR · Crispin] Roster isolation con draft.** 2 Pending + 1 InProgress → roster = solo partecipanti della live; night all-Pending → roster vuoto.
- **[MINOR · Wiegers] `GameNightSessionTests.cs:23`.** Il "born Pending lock" vive dentro un test largo; estrarne uno single-purpose.

## Decisione di prodotto (bloccante): draft vs live-now vs both

| Opzione | Cosa cambia su POST /game-sessions | Impatto su #19 (multi-draft) | Superficie go-live | Rischio contratto FE | Sforzo |
|---|---|---|---|---|---|
| **A — draft-only** | link nato `Pending`, Session `Active/StartedAt=null/IsLive=false`, slot indice libero, night NON promossa. Breaking per i caller che oggi ottengono live-on-create | Nativamente supportato: N draft coesistono, previa rilocazione guard su `link Status==InProgress` at go-live | Endpoint go-live **nuovo e separato** | Basso (riusa `Pending`, FE mappa già → `transition`) | Medio-alto |
| **B — live-now-only** | Post-condition invariata (create+go-live). Non-breaking se routato via aggregato three-phase | Non risolve #19 | Nessuna nuova, ma va spostato dall'inline all'aggregato three-phase | Basso | Basso-medio |
| **C — both (intent esplicito)** | Campo intent **obbligatorio** (`startLive`); omissione → 400. Additivo se il default preserva il corrente | Supporta #19 sul ramo draft + go-live esplicito; richiede selezione draft al go-live | Sia ramo draft sia go-live + `StartSession(sessionId)` | Medio | Alto |

### Raccomandazione ragionata del panel (peso Cockburn + Newman)

**Opzione C con default DRAFT (UC-A), go-live come sub-resource REST dedicata `POST /api/v1/sessions/{id}/go-live`.**

1. **Cockburn (garanzie):** UC-A ha garanzia debole/reversibile; UC-B porta garanzia forte, singleton, difficile da revertire (unico live-slot, promozione night, SSE). Il goal safe/minimal dev'essere il **default**; i goal side-effecting richiedono scelta esplicita. Un default live-on-create è il CRUD-as-use-case overload che l'epic vuole eliminare.
2. **Newman (contratto):** go-live come sub-resource lifecycle (coerente con `pause/resume/end/complete/abandon`) gli dà operation OpenAPI propria, error surface propria (409 su operazione opt-in), idempotency propria, senza inquinare create con un 409 nuovo. Rifiutare il flag boolean su un command già flag-heavy (Fowler).
3. **Riuso (Fowler + GT-3):** il go-live three-phase esiste già in `StartGameNightSessionCommandHandler` con guard pre-INSERT (`EnsureCanStartSession`), transazione e rollback xmin corretti. La sub-resource delega a questo pattern.
4. **Compat (Newman):** poiché oggi ogni POST client-visibile ottiene live, il passaggio a draft-default è breaking; RFC 8594 (`SessionCommandEndpoints.cs:124-162` fornisce il precedente) o `startLive` default-true deprecato su finestra. **Unico punto che richiede firma product.**

**Secondo punto da sbloccare insieme:** con multi-draft, il go-live deve prendere un `sessionId` esplicito (Adzic/Completeness). Decidere: go-live su "l'unico draft" (implica max 1 draft at go-live) oppure l'attore seleziona quale (richiede `GameNightEvent.StartSession(sessionId)`).

## Decomposizione in slice implementabili

Sequenza pensata per tenere C1/C2a verdi e non lasciare mai `main-dev` rotto.

### Slice 0 — Product & ownership decision (no code) — RICHIEDE PRODUCT DECISION
- **Scope:** registrare (a) scelta draft/live/both con decision table, (b) scelta "quale draft va live", (c) ADR-style ownership della canonical liveness.
- **File:** nuovo doc spec di slice (pattern C1).
- **Acceptance:** riga scelta con (endpoint signature, default, backward-compat, stato link + Session per branch, assertion per test); ADR liveness-owner nominato.
- **Dipendenze:** nessuna. **Blocca tutte le successive.**

### Slice 1 — Read-model tolerance (forward-compat, deploy FIRST)
- **Scope:** BE+FE tolleranti a link `Pending` senza sibling `InProgress` come draft valido, **prima** di qualsiasi flip. Nessun cambio di comportamento create.
- **File:** `GetGameNightLiveQueryHandler.cs`, `mapNightLive.ts` (test baseline), `game-nights-live.schemas.test.ts`.
- **Acceptance:** night con solo link Pending → `/live` 200; `mapNightLive` → `status:'transition'` (baseline pinnato); no regressione.
- **Dipendenze:** Slice 0.

### Slice 2 — go-live sub-resource + routing aggregato three-phase — RICHIEDE PRODUCT DECISION (opzione)
- **Scope:** nuovo `POST /api/v1/sessions/{id}/go-live` che delega al pattern `StartGameNightSessionCommandHandler` (three-phase); `GameNightEvent.StartSession(sessionId)` se serve selezione draft; mapping 23505→409 (constraint-name in costante condivisa) sul promotion write.
- **File:** nuovo endpoint, command+handler, `GameNightEvent.cs`, `GameNightSessionEntityConfiguration.cs` + catch.
- **Acceptance:** go-live promuove `Pending→InProgress` + fira `SessionStartedDomainEvent` + promuove night `Published→InProgress`; due go-live concorrenti → esattamente un 409, mai 500; `.Produces(409)`.
- **Dipendenze:** Slice 0, 1.

### Slice 3 — Flip born-status del direct-create + guard relocation — RICHIEDE PRODUCT DECISION
- **Scope:** direct-create non hand-builda più il link/envelope `InProgress`; nasce `Pending`; rilocare il guard da `Session.Status==Active` at create a `link Status==InProgress` at go-live; coprire entrambi i branch (attach + ad-hoc puro).
- **File:** `CreateSessionCommandHandler.cs:180-210,357-410`, `CreateSessionCommand.cs`.
- **Acceptance:** POST draft → link `Pending`, slot libero, night non promossa, Session `Active/IsLive=false`; secondo draft stessa night → coesiste, NO 409; go-live successivo → C2a vivo. Backward-compat rispettata.
- **Dipendenze:** Slice 2 (go-live prima del flip).

### Slice 4 — Reconcile complete/finalize transitions (item 4)
- **Scope:** riconciliare `CompleteGameNightSessionCommandHandler` e `FinalizeSessionCommandHandler` sul modello born-Pending.
- **File:** `CompleteGameNightSessionCommandHandler.cs:52-53,77,99-104`, `FinalizeSessionCommandHandler.cs:159-168`.
- **Acceptance:** finalize di draft mai promosso → link NON orfano; complete di night solo-Pending → riconciliato; i due guard tengono.
- **Dipendenze:** Slice 3.

### Slice 5 — Data-migration/backfill righe legacy InProgress (item mancante, GT-4)
- **Scope:** riconciliare i link standalone `InProgress`/`StartedAt=now` già persistiti la cui Session non fu mai aperta live (split-brain). Riusare `20260719111334_...cs:31-40`.
- **File:** nuova migration EF Core.
- **Acceptance:** (1) link legacy con Session finalizzata → chiuso; (2) con Session live → preservato unico live; (3) idempotenza; count righe ambigue == 0.
- **Dipendenze:** Slice 3. Può precedere Slice 6 se item 5 tocca il predicato indice.

### Slice 6 — Canonical live read-model migration (item 5)
- **Scope:** puntare `GetGameNightLiveQueryHandler`, `GameNightSessionDto.Status`, `mapNightLive.ts` all'owner scelto in Slice 0; se serve nuovo stato liveness, campo **additivo** (`isLive`), non overload enum; deploy FE-first se l'enum cambia; se l'indice viene rinominato, aggiornare costante + catch 23505 nello stesso commit.
- **File:** `GetGameNightLiveQueryHandler.cs`, `game-nights.schemas.ts`, `mapNightLive.ts`, eventuale indice + costante.
- **Acceptance:** contract test `set status BE == enum Zod FE`; nessun rename indice senza aggiornamento catch; FE non hard-fail su `/live`.
- **Dipendenze:** Slice 0, 1, 3, 5. **Ultima** — tocca il predicato indice load-bearing.

## Rischi e gap residui (richiedono sign-off umano)

1. **Due decisioni di prodotto, non una.** draft/live/both + **quale draft va live** (nascosta in `FirstOrDefault`, `GameNightEvent.cs:685-687`). Entrambe firma product.
2. **Semantica del cap max-5.** `CreateSessionCommandHandler.cs:183-187` conta tutti i `GameNightSessions`; sotto multi-draft un utente che logga 5 draft blocca il 6° con zero live. Non riconciliato in nessun item.
3. **Backward-compat di un verbo v1.** Oggi ogni POST client-visibile ottiene live. Passaggio a draft-default è breaking; hard-break versionato vs deprecation window (RFC 8594) richiede sign-off.
4. **`CreateAdHoc()` è un secondo conflate-create-with-go-live nel dominio** (`GameNightEvent.cs:132-149`, `Status=InProgress` diretto). L'epic non lo nomina. Human sign-off architetturale.
5. **Ownership canonical liveness.** Due modi di leggere liveness (`link Status==InProgress` vs `Session.StartedAt/FinalizedAt`). ADR da firmare in Slice 0.
6. **GAP-001 adiacente** (`CreateSessionCommandHandler.cs:112-114`): `request.StateTier` propagato ma non applicato (`Session.Create` non lo accetta). Verificare se includere o escludere dallo scope.
7. **Nessun down-migration/rollback per il flip.** Procedura (feature-flag vs reconciliation script) da approvare prima del deploy di Slice 3.
