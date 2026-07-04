# #2587 — Dual-creation funnel convergence — DESIGN (owner ratification required)

**Status**: Proposed (design session, 2026-06-30). Non implementato. De-risk: workflow `wci4cjzkv` + `.superpowers/sdd/gamesession-architecture-decision.md`.

## TL;DR — il framing della issue è in parte superato
Il body di #2587 dice "SessionSetupModal (GameSession) vs wizard (LiveGameSession)". **Scoperta del de-risk**: `SessionSetupModal` è **UNUSED in produzione** (solo stories/test — `SessionSetupModal.stories.tsx`/`.test.tsx`, zero usage in `apps/web/src/app`). Quindi NON c'è un dual-creation live: l'unico funnel reale è **wizard → LiveGameSession** (`api.liveSessions.createSession`).

Il **problema reale** non è "due creazioni" ma "**creazione e read-surface leggono aggregati diversi**":

## Problema (severità ALTA — broken discovery + quota bypass)
1. **History-invisibility**: le sessioni reali sono `LiveGameSession` (`live_game_sessions`), ma la history/active-list (`SessionsLibraryView` → `useActiveSessions` → `api.sessions.getActive`) legge `GameSession` (`game_sessions`). Aggregati non correlati (no shared id/FK) → **le sessioni create dal wizard sono giocabili ma invisibili nella history**. L'utente non può vedere/gestire ciò che ha appena creato. (ADR-083 conferma: lo scoring polimorfico gira sul "guscio vuoto GameSessionDto, mai collegato a dati reali".)
2. **Quota bypass**: `CreateLiveSessionCommandHandler` NON chiama alcun quota service (`LiveSessionEndpoints.cs:36-44`); solo il path GameSession (`StartGameSessionCommandHandler.cs:53-64` → `SessionQuotaService.CountActiveByUserIdAsync`) controlla la quota. Siccome il funnel reale è LiveGameSession, **la quota per-user è di fatto non applicata**.
3. **Tensione con Opzione-B (Fase 4, #2589)**: Opzione-B ha tenuto GameSession PERCHÉ "possiede quota/history/lifecycle". Ma il de-risk mostra che le sessioni reali **bypassano GameSession** per quota e history → la premessa di Opzione-B è **minata dall'evidenza del funnel reale**. Questo design re-apre legittimamente quella valutazione con dati nuovi.

## api.sessions.* call-site (GameSession) — breakdown
42 totali: **16 READ** (getState×9 toolkit-state, getById×7, getQuota×3, getHistory×2, getActive×1, getSnapshots×1) vs **6 WRITE** (start×3 [il funnel morto], end×2, pause/resume/complete×1). I READ (history/quota/state) sono il vero coupling da risolvere.

## Due direzioni di convergenza

### Direzione A — Saga-correlate (onora Opzione-B alla lettera)
`CreateLiveSessionCommandHandler` crea atomicamente ANCHE una `GameSession` correlata (quando `GameId.HasValue`), linkata da una nuova proprietà (es. `CorrelatedGameSessionId`), + sposta il **quota check** dentro questo handler. Pattern identico alla Saga SP0 companion (`CompanionSessionService` lo prova). History/quota continuano a leggere GameSession (ora popolata per ogni sessione reale).
- **Pro**: onora Opzione-B (GameSession resta l'aggregato quota/history, NON rimosso); quota uniforme; atomicità single-SaveChanges; bassa novità architetturale (riusa il pattern Saga).
- **Contro**: dual-write (una GameSession per ogni LiveGameSession) per un aggregato altrimenti vestigiale; transazione 3-BC (GameManagement GameSession + SessionTracking companion + catalog FK); backfill legacy (lazy `EnsureGameSessionCompanion` analogo a SP5-c). Mantiene 2 aggregati "vivi" per la stessa sessione logica.

### Direzione B — Repoint history/quota a LiveGameSession (re-apre Opzione-B)
History/active/quota leggono direttamente `LiveGameSession`; GameSession diventa veramente vestigiale → rimovibile in futuro. È la "Fase 1 realignment" di ADR-083 (loader già allineato a LiveGameSession in #2511).
- **Pro**: **single-aggregate** per il funnel reale (più pulito long-term); GameSession non è mai stato popolato da sessioni reali → rimuoverlo dalla read-surface elimina la confusione; ~1-2 settimane (loader/schema adapt).
- **Contro**: **reverte la decisione Opzione-B (#2589, questa stessa epic)** — whiplash decisionale; serve ri-homing della **quota** (CountActive su `live_game_sessions`) e della **history** (`FindHistoryAsync` su LiveGameSession completate) — ma la history post-play ha già home in `UserLibrary.GameSession` (entità diversa, popolata su `LiveSessionCompletedEvent`). La quota cross-session è una policy che andrebbe ri-wirata.

### Direzione C — GameSession-spawns-LiveGameSession (scartata)
GameSession crea, LiveGameSession spawn lazy al primo play. Scartata: il funnel reale è già LiveGameSession-first; invertirlo è più disruptivo + race su lazy-spawn.

## Raccomandazione
**La scelta è genuinamente dell'owner perché re-apre Opzione-B.** Analisi onesta:
- Se la priorità è **coerenza architetturale long-term** → **Direzione B** (single aggregate; GameSession era vestigiale per le sessioni reali; la quota/history si ri-homeano su LiveGameSession/UserLibrary). Costo: reverte Opzione-B (ma con evidenza nuova che la giustifica).
- Se la priorità è **non rivisitare Opzione-B + minimo rischio** → **Direzione A** (Saga-correlate; GameSession diventa finalmente popolata-per-davvero, onorando la sua ratifica come aggregato quota/history). Costo: dual-write permanente.

**Raccomando Direzione B con un fix urgente disaccoppiato**: indipendentemente dalla direzione finale, il **quota bypass** (#severità alta) e l'**history-invisibility** vanno chiusi. Il fix minimo urgente = repointare la read-surface history + il quota-check su LiveGameSession (Direzione B Fase 1), che risolve entrambi i sintomi user-visible in ~1-2 settimane SENZA il dual-write di A. Opzione-B andrebbe formalmente re-scoped (3a revisione ADR-083, registrata onestamente) riconoscendo che GameSession è vestigiale per il funnel reale.

## Rischi del recommended (Direzione B)
1. Ri-homing quota: `CountActiveByUserIdAsync` deve contare `live_game_sessions` attive; verificare nessun doppio-conteggio durante transizione.
2. History query: switch da `game_sessions` a `live_game_sessions` (completate) o a `UserLibrary.GameSession`; confermare parità campi (winner, durata, players).
3. Reverte Opzione-B → registrare onestamente come 3a revisione ADR-083 (no no-op silenzioso).
4. `getState×9` (toolkit-state GameSession-scoped) — verificare che il toolkit-state non dipenda da GameSession come chiave (potrebbe già essere su LiveGameSession id post-#2579).
5. Nessun test del flusso dual-funnel oggi → aggiungere e2e create-via-wizard → visibile-in-history.

## Decisione ratificata dall'owner (2026-06-30) — **Direzione A (Saga-correlate)**
L'owner ha scelto **Direzione A**: `CreateLiveSessionCommandHandler` crea atomicamente ANCHE una `GameSession` correlata (quando `GameId.HasValue`) + sposta il quota-check qui; nuova prop `CorrelatedGameSessionId` su LiveGameSession; single SaveChanges; backfill lazy per legacy. Onora Opzione-B (GameSession resta l'aggregato quota/history, ora popolato per ogni sessione reale).

### ⚠️ Impedenza di design scoperta all'innesto (2026-06-30) — correlation-at-START, non at-create
Leggendo `CreateLiveSessionCommandHandler.cs:34-78` + `StartGameSessionCommandHandler.cs`:
- **GameSession richiede `players` non-vuoti** (`new GameSession(id, gameId, players, createdByUserId)` + `Guard.AgainstEmptyCollection`), MA `CreateLiveSessionCommandHandler` crea la LiveGameSession **senza players** (il wizard fa create→addPlayer×N→startSession). A create-time non ci sono players da passare a GameSession.
- **La quota (`ISessionQuotaService.CheckQuotaAsync`) richiede `UserTier` + `UserRole`**, che `CreateLiveSessionCommand` NON porta e l'endpoint `/live-sessions` (`RequireAuthenticatedUser` only) NON risolve. Vanno risolti dai claim/tier dell'utente all'endpoint.

**Risoluzione**: la GameSession correlata si crea a **StartSession** (quando i players esistono sulla LiveGameSession), NON a create. Il quota-check va all'endpoint che risolve tier/role (start o create con tier/role aggiunti al command).

### Decomposizione in slice (RIVISTA dopo l'impedenza)
- **Slice 1 (quota gate)**: risolvi `UserTier`+`UserRole` all'endpoint LiveSession + aggiungi il quota-check (chiudendo subito il **quota-bypass**, il bug più severo, indipendente dalla correlazione). Self-contained.
- **Slice 2 (correlate-at-start)**: nello StartSession handler (players presenti), crea la GameSession correlata con i players della LiveGameSession + `CorrelatedGameSessionId` + single-SaveChanges. Popola GameSession per le sessioni che partono → history-visibility.
- **Slice 3 (backfill lazy)**: `EnsureGameSessionCorrelation` per legacy `CorrelatedGameSessionId == null` (pattern SP5-c EnsureCompanion, trigger on-history-read/on-start).
- **Slice 4 (verify)**: e2e create→start→visibile-in-history; scoring source-of-truth = LiveGameSession.ScoringConfig (GameSession.WinnerName = cache denormalizzata).

### Nota di esecuzione
L'impedenza players/tier conferma che #2587-A è un effort **dedicato multi-settimana** (4 slice, 3-BC atomicità, quota-tier-resolution, scoring-reconciliation, 42-call-site audit). Va eseguito come sessione dedicata con questo design come base — NON crammato. Il design e le 4 slice sono ratificati e pronti.

Ref: #2587 · #2501 Fase 4 · ADR-083 (Opzione-B amendment, da re-scope se Direzione B) · `.superpowers/sdd/gamesession-architecture-decision.md`
