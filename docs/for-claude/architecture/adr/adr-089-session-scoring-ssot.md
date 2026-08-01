# ADR-089 — SSOT tra i modelli di "sessione live" e i sistemi di scoring

**Date**: 2026-08-01
**Status**: Accepted — la direzione (coesistenza delimitata) ratifica e consolida decisioni già prese in ADR-083; questo ADR le rende esplicite come **mappa responsabilità → SSOT** e come contratto di non-sincronizzazione dello scoring.
**Issue**: #3395 (finding C1 + C10 dell'audit `docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md`) — parte del programma-ombrello #3397
**Related**: ADR-060 (live session persistence, xmin), ADR-065 (sessions namespace split), ADR-071 (5-state FSM), ADR-083 (convergenza aggregati live su `LiveGameSession` — **prerequisito di lettura**)

## Context

L'audit del 2026-07-29 (verifica avversariale multi-agente su `main-dev`) ha riconfermato due finding già noti ma mai formalizzati in un unico documento di riferimento:

- **C10** — coesistono **più aggregati "sessione live"** e **più sistemi di scoring indipendenti**, collegati ma **non sincronizzati**. Un unico frontend deve riconciliarli e ogni feature cross-context paga una *tassa di traduzione*.
- **C1** — il grounding dell'agente in-sessione è biforcato tra due bounded context; il finding rimanda a #3390 (contratto RAG unificato) ma **presuppone** che sia chiaro chi possiede *cosa* nella sessione.

ADR-083 ha già mappato e **ratificato** la direzione strutturale della *superficie live* (aggregato canonico = `LiveGameSession`; `GameSession` = shadow di lifecycle/quota/history; companion `SessionTracking.Session` via Saga). Quello che ADR-083 **non** ha fatto — e che #3395 chiede — è una **tabella esplicita responsabilità → aggregato SSOT** più una dichiarazione formale che **lo scoring non è sincronizzato tra i modelli** (per prevenire l'assunzione errata, ricorrente, che i due punteggi "si parlino"). Questo ADR non introduce codice: fissa il contratto architetturale e la direzione, ancorati al codice reale (tutti i `file:line` sotto sono stati letti, non inferiti).

## Mappa verificata degli aggregati

Sono **tre** aggregati su **due** bounded context, su tre prefissi di route distinti e **senza id condiviso** (verificato in ADR-083 OQ1 e riconfermato qui):

| Aggregato | Tabella | BC | Route | Concorrenza | "Vivo" quando | Scoring |
|---|---|---|---|---|---|---|
| `GameSession` | `game_sessions` | GameManagement | `/api/v1/sessions/*` | — | lifecycle 4-state | **nessuno** (shadow) |
| `Session` | `session_tracking_sessions` | SessionTracking | `/api/v1/game-sessions/*` | `RowVersion` (`byte[]`, `[Timestamp]`) | `StartedAt≠null && FinalizedAt==null` | **polimorfico** (JSON) **+** `ScoreEntry` (righe) |
| `LiveGameSession` | `live_game_sessions` | GameManagement | `/api/v1/live-sessions/*` | `Xmin` (Postgres, ADR-060) | `Status ∈ {Setup,InProgress,Paused}` | **round-based** (dimensioni × round) |

**Evidenza (`file:line`):**
- `SessionTracking/Domain/Entities/Session.cs:113` (`IsLive`), `:120` (`ScoringType`), `:127` (`ScoreData` JSONB), `:174` (`RowVersion`), `:471` (`SetScores`).
- `GameManagement/Domain/Entities/LiveGameSession.cs:62` (`ScoringConfig`), `:87` (`Xmin`), `:104` (`IsActive`), `:561` (`RecordScore`), `:984` (`RecalculatePlayerScores`), `:73` (`TrackingSessionId`), `:85` (`CorrelatedGameSessionId`).
- `GameSession` come shadow (no scoring autoritativo): ADR-083 Update 2026-06-30 (#2587).

> Nota terminologica: la tabella dell'issue #3395 elenca **due** aggregati ("`SessionTracking.Session`" vs "`GameManagement.LiveGameSession`"). Il terzo — `GameManagement.GameSession`, il guscio di lifecycle/quota/history — è l'aggregato *dietro* i link di correlazione e va incluso per completezza (ADR-083). L'issue è corretta nella sostanza; questo ADR estende la mappa a tre per non lasciare zone grigie.

## Mappa verificata dei sistemi di scoring

Contrariamente al framing "due sistemi", il codice reale espone **tre store di punteggio** raggiunti da **almeno quattro comandi di scrittura**:

| # | Store | Aggregato / tabella | Forma dati | Comandi di scrittura (verificati) |
|---|---|---|---|---|
| 1 | Round-based | `LiveGameSession.RoundScores` / `live_session_round_scores` | matrice `player × round × dimension`, validata contro `ScoringConfig.HasDimension` | `RecordScore` (`LiveGameSession.cs:561`) |
| 2a | Polimorfico | `Session.ScoringType` + `Session.ScoreData` (JSONB sull'aggregato) | discriminated-union JSON per `ScoreType` (Points/BinaryWin/Objectives/Ranking) via `IScoringStrategy` | `UpdateSessionScoresCommand` → `SetScores` |
| 2b | Storico righe | `ScoreEntry` / `session_tracking_score_entries` | righe `(participant, round?, category?, value)` | `UpdateScoreCommand` (#4765), `UpdatePlayerScoreCommand` (#4765, `RowVersion`), `UpsertScoreWithDiaryCommand` (Session Flow v2.1 T8, + diary event) |
| 3 | Play-record storico | `UserLibrary.GameSession` / PlayRecord | derivato a fine partita | `RecordGameSessionCommandHandler` su `LiveSessionCompletedEvent` (da `LiveGameSession`) |

**Osservazione chiave**: lo store #2a e lo store #2b vivono **entrambi** dentro SessionTracking ma su modelli dati incompatibili (JSON sull'aggregato vs righe normalizzate). Il commento di `UpdateSessionScoresCommand.cs:9-13` lo dichiara esplicitamente: *"Distinct from `UpdateScoreCommand` … this command replaces the session's polymorphic `ScoringType` + `ScoreData` payload atomically"*. Sono due code-path che **non si riconciliano tra loro**, prima ancora di considerare il cross-BC.

## Il finding centrale — lo scoring NON è sincronizzato tra i modelli

I due aggregati live sono collegati da **due link a livello di identità**, entrambi `Guid?` e **portatori del solo id** (nessun payload di dominio):

- `LiveGameSession.TrackingSessionId` (`:73`, `SetTrackingSessionId` `:882`) → la `SessionTracking.Session` companion (Saga at-creation, ADR-083 SP0; backfill lazy SP5-c/#2600). Serve a correlare chat-RAG / diary / media / stream.
- `LiveGameSession.CorrelatedGameSessionId` (`:85`, `SetCorrelatedGameSessionId` `:918`) → la `GameSession` di quota/lifecycle/history (#2587 Slice 1).

**Nessuno dei due link trasporta punteggi.** Non esiste code-path che legga `RoundScores` e scriva `ScoreData` (o viceversa): la correlazione è per **lifecycle / quota / companion**, non per scoring. ADR-083 OQ3 aveva già stabilito che i modelli sono *"genuinamente incompatibili … discriminated-union JSON vs matrice round×dimensione×player: nessuno è superset dell'altro"*; #2587 ha ratificato che `LiveGameSession.ScoringConfig` è *"la single source of truth per lo scoring in-play"* e che i campi scoring del `GameSession` correlato restano **non popolati**.

**Rischio da prevenire** (motivo per cui lo mettiamo nero su bianco): un contributor futuro che veda `TrackingSessionId`/`CorrelatedGameSessionId` può assumere che i punteggi siano condivisi o riconciliati, e cablare una lettura cross-modello (es. "leggi il totale dal companion") ottenendo dati fantasma o vuoti. Lo scoring polimorfico shippato in `SessionLiveView` (#2389/#2483) gira di fatto sul guscio vuoto `GameSessionDto` → non è mai stato collegato a dati reali (spiega perché #2354 funziona solo su fixtures `IS_VISUAL_TEST_BUILD`). Questa è la trappola concreta che l'ADR chiude.

## Decisione

### 1. Mappa responsabilità → aggregato SSOT

Ogni responsabilità ha **un solo** owner autoritativo. Nessuna responsabilità è condivisa tra due aggregati.

| Responsabilità | SSOT | BC | Ancora |
|---|---|---|---|
| Lifecycle 4-state (Setup→InProgress→Paused→Completed/Abandoned) | `GameSession` | GameManagement | ADR-083 Fase 4 (Opzione B) |
| Quota per-user cross-session | `GameSession` | GameManagement | `SessionQuotaService`, `CountActiveByUserIdAsync` |
| History query post-play (lista/dettaglio partite) | `GameSession` (visibilità) + `UserLibrary.GameSession`/PlayRecord (record) | GameManagement / UserLibrary | `FindHistoryAsync`; `RecordGameSessionCommandHandler` |
| Runtime in-play: players/team/turni/fasi/setup-checklist/dispute/snapshot/agent-mode | `LiveGameSession` | GameManagement | ADR-071 (FSM), ADR-060 (xmin) |
| **Scoring in-play (live)** | **`LiveGameSession.ScoringConfig` + `RoundScores`** (round-based) | GameManagement | #2587 |
| Diary append-only multi-autore (live) | `LiveGameSession.DiaryEntry` (nativo) | GameManagement | ADR-083 SP3 (#2570) |
| Chat-RAG con citazioni (risposta agente) | `KnowledgeBase` (keyed su `LiveGameSession.Id`) | KnowledgeBase | ADR-083 SP0/SP1; direzione #3390 |
| Note/eventi cifrati private per-partecipante, media companion | `SessionTracking.Session` (companion, via `TrackingSessionId`) | SessionTracking | ADR-083 SP0 |
| Turni / note / partecipanti fuori dal path live canonico (superficie storica) | `SessionTracking.Session` | SessionTracking | legacy, vedi §Direzione |

### 2. Contratto di non-sincronizzazione dello scoring

- **In-play**: l'unica fonte autoritativa del punteggio a tavolo è `LiveGameSession.RoundScores` (round-based). Il `GameSession` correlato **non** deve mai essere letto come fonte del risultato; i suoi campi scoring restano `null`.
- **Storico**: il record post-partita si deriva **da `LiveGameSession`** (evento `LiveSessionCompletedEvent`), non dal companion né dal polimorfico.
- Lo scoring **polimorfico** (`ScoreData`) e lo **storico-righe** (`ScoreEntry`) di SessionTracking **non** sono, e non devono diventare, sincronizzati con `RoundScores`. Non è previsto alcun reconciler bidirezionale: sincronizzarli reintrodurrebbe il rischio dual-write/dedup che ADR-083 SP3 ha esplicitamente evitato per il diary.
- Regola operativa: **scegli l'SSOT giusto per il contesto**, non ponticellare tra modelli. FE già oggi scrive lo scoring live via `useUpdateSessionScores` (CLAUDE.md); l'accoppiamento allo store round-based è responsabilità del wiring di quel path, non di una riconciliazione runtime.

### 3. Direzione — **coesistenza documentata e delimitata** (non nuova convergenza)

La direzione difendibile, alla luce dell'evidenza, è la **coesistenza delimitata** con SSOT-per-responsabilità, **non** un nuovo epic di convergenza/sincronizzazione. Motivazioni:

1. La convergenza della *superficie live* su `LiveGameSession` è **già** la direzione ratificata (ADR-083, Direzione A) e in larga parte shippata. #3395 non deve ri-decidere quel percorso.
2. La coesistenza di `GameSession` è **permanente e ratificata** (ADR-083 Fase 4, Opzione B / #2587): lifecycle+quota+history e runtime in-play sono concern **complementari**, non duplicazione accidentale. Fonderli violerebbe l'SRP (la quota è policy per-user cross-session, non attributo di un runtime per-istanza).
3. I link Saga esistono per lifecycle/quota/companion, **non** per scoring (verificato). La lettura corretta di questo dato non è "sincronizziamo lo scoring" ma "**non** sincronizziamolo — dichiariamo un SSOT per contesto".

L'unico item genuinamente aperto **non** è una sincronizzazione, ma un **ritiro delimitato**: portare fuori dal path live lo scoring polimorfico/`ScoreEntry` di SessionTracking (già indicato da ADR-083 #2587 come *"ri-orientato ai play-records storici, da tracciare in Fase 1/2"* e ri-emerso incompleto in questo audit). È cleanup a scope chiuso, tracciabile come follow-up, **non** un big-bang.

**Fuori scope esplicito**: #3390 (contratto RAG grounded unificato) riguarda la *risposta dell'agente* — chi possiede "risposta grounded sul rulebook" (`KnowledgeBase`) — e **non unifica lo scoring**. Le due questioni condividono la diagnosi ("confine sulla costura sbagliata") ma hanno owner e piani distinti. Questo ADR non anticipa né vincola #3390 sul lato scoring.

## Consequences

### Positive
- Un unico documento dichiara chi è autoritativo per ogni responsabilità della sessione live → elimina la zona grigia che alimenta C10.
- La trappola "i due punteggi si parlano" è chiusa esplicitamente: nessuna sincronizzazione attesa, SSOT per contesto.
- Sblocca la lettura di C1/#3390 dando per acquisito il possesso di lifecycle/runtime/scoring, così quel lavoro può concentrarsi sulla sola *risposta grounded*.
- Zero costo di migrazione: formalizza lo stato ratificato, non muove codice.

### Negative / debt
- La coesistenza di tre aggregati resta cognitivamente costosa: un nuovo contributor deve leggere questo ADR + ADR-083 per orientarsi. Mitigazione: pointer da CLAUDE.md (§ Domain Model).
- Il ritiro dello scoring polimorfico/`ScoreEntry` dal path live è debito reale, qui **dichiarato ma non eseguito**. Finché non è fatto, esistono comandi di scrittura scoring in SessionTracking che, se cablati per errore al path live, producono dati non autoritativi.
- La "tassa di traduzione" cross-context per feature che attraversano i due modelli (es. correlare uno score live a una nota companion) rimane: è il prezzo accettato della separazione degli SSOT.

### Neutral
- Nessun cambiamento di schema, DTO o API. Nessun impatto su test esistenti.

## Follow-up (tracciati, non parte di questo ADR)
- Ritiro delimitato dello scoring polimorfico/`ScoreEntry` di SessionTracking dal path live (re-orientamento ai play-records storici). Da aprire come issue di cleanup con guard di non-uso sul path live.
- #3390 (epic) — contratto RAG grounded unificato (risposta agente): concern separato, dipendente da #3388/#3389.

## References
- Issue #3395 (questo ADR); audit `docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md` (finding C1 + C10); programma-ombrello #3397; epic #3390.
- ADR-083 (convergenza aggregati live; OQ1/OQ3; Fase 4 Opzione B; #2587 shadow scoring; SP0/SP3/SP5-c companion).
- ADR-060 (persistence + xmin), ADR-065 (namespace split), ADR-071 (5-state FSM).
- Codice: `SessionTracking/Domain/Entities/Session.cs`, `.../Entities/ScoreEntry.cs`, `.../Application/Commands/{UpdateSessionScoresCommand,UpdateScoreCommand,UpdatePlayerScoreCommand,UpsertScoreWithDiaryCommand}.cs`; `GameManagement/Domain/Entities/LiveGameSession.cs`.
- CLAUDE.md § Domain Model — GameNight / Session (pointer a questo ADR).
