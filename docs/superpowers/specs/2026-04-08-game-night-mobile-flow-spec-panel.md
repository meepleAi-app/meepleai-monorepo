# Spec Panel — Mobile Game Night End-to-End Flow

> **Modalità**: `/sc:spec-panel` — Multi-expert review (ultrathink depth)
> **Data**: 2026-04-08
> **Scope**: Flusso utente mobile completo "Game Night" dal login all'archiviazione del diario, contro l'implementazione attuale (post PR #265 Game Night Experience v2).
> **Tipo**: Gap analysis + qualità requisiti + raccomandazioni, NON implementazione.

---

## 1. Requirements Brief (formalizzato)

### 1.1 User story (narrativa originale)
> Dopo aver avviato l'applicazione sul mio smartphone, voglio avviare una "game night" con me e 2 amici per giocare a una serie di giochi. L'app deve: (1) tenere score/fasi/turni resilienti a pause, (2) offrire un agente per setup/regole/dispute, (3) fornire strumenti digitali (dadi, carte, note), (4) registrare un diario automatico con foto. Selezioniamo un gioco con PDF indicizzato e avviamo la sessione.

### 1.2 Attori

| Attore | Ruolo |
|---|---|
| **Host** (io) | Autenticato, crea la game night, possiede il device primario, sincronizza stato |
| **Guest 1, Guest 2** | Possono essere autenticati o ospiti via join code; ricevono diario in SSE |
| **AI Session Agent** | Risponde a domande su setup/regole/dispute usando RAG del PDF gioco |
| **Quartz AutoSave Job** | Attore di sistema: salva checkpoint ogni 60s |

### 1.3 Capability map (4 aree × obiettivi SMART)

| # | Capability | Obiettivo misurabile |
|---|---|---|
| **C1** | **Stato sessione resiliente** (L1+L2) | Zero perdita dati fino a 60s di disconnessione; ripristino stato turno/fase/risorse su resume con RTO ≤ 3s |
| **C2** | **AI Agent in-session** | Rispondere a "setup/rules/dispute" citando il PDF indicizzato, ≤ 5s primo token, con thread persistente per sessione |
| **C3** | **Strumenti digitali** (dadi, carte, pen&paper) | ≥ 4 tool attivabili offline lato host; log delle azioni tool nel diario |
| **C4** | **Diario automatico + foto** | Ogni evento L1/L2/tool emesso come `SessionEvent` con `GameNightId`, broadcast SSE; foto caricabili e allegabili agli entries |

### 1.4 Precondizioni
1. Utente autenticato su device mobile (touch, PWA installata o browser)
2. Almeno 1 `Game` con PDF indicizzato (`Games.HasIndexedContent = true` e vector store popolato)
3. `GameNightEvent` esistente in stato `Published` con ≥ 1 game ID
4. Feature flag `Features.GameNightMultiSession` attivo
5. Feature flag `Features.PdfUpload` seeded in `system_configurations`

### 1.5 Postcondizioni (stato finale desiderato)
1. `GameNightEvent` in stato `Completed` con ≥ 1 `GameNightSession` finalizzata
2. Ogni sessione ha ≥ 1 checkpoint L2 e ≥ N `SessionEvent` (con N ≥ numero turni)
3. Diario esportabile (lista ordinata di entries cross-game)
4. Foto persistite (o con gap documentato, vedi §4)

---

## 2. Coverage Map vs Implementazione Attuale

Baseline: post-merge PR #265 + toolkit drawer (PR #266) + ripetute refactor meeple-card (#268, #270, #271).

| Requisito (capability) | Stato | Evidenza | Commento |
|---|---|---|---|
| **C1.1** Creazione GameNight (wizard mobile) | ✅ | `GameNightWizard.tsx`, route `/game-nights/new`, `CreateGameNightCommand` | Flow wizard esistente |
| **C1.2** Start GameNightSession cross-BC | ✅ | `StartGameNightSessionCommand` usa `IMediator.Send(CreateSessionCommand)` | ADR-compliant, no cross-BC injection |
| **C1.3** Stato L1: turno/fase/score | ✅ | `Session`, `SessionEvent`, `LiveScoreboard.tsx`, `play-mode-mobile.tsx` | Copertura completa turno+fase |
| **C1.4** Stato L2: risorse giocatore | ✅ | `PlayerResources.cs` (value object), `PlayerResourcesPanel.tsx`, JSON roundtrip | L2 schema generico |
| **C1.5** Auto-save 60s + resume | ✅ | `AutoSaveSessionJob.cs` (Quartz), `IAutoSaveSchedulerService`, `ResumeSessionPanel.tsx` | Documentato in plan §I3 |
| **C1.6** Pausa/Resume esplicito | ✅ | `SessionCheckpoint.SnapshotData`, wiring finalize/pause job.Remove() | |
| **C2.1** Agent chat per setup/rules | ✅ | `ChatWithSessionAgentCommand`, `SessionChatWidget.tsx`, route `/sessions/live/[sessionId]/agent` | |
| **C2.2** Dispute resolution via RAG | 🟡 | `RulesExplainer.tsx`, `RulesSummary.tsx`, threads Q&A | Nessun flag "dispute mode" specifico nel thread; i prompt sono aperti |
| **C2.3** Thread persistente per sessione | ✅ | `CreateChatThreadCommand`, `ChatSessionMessage` | |
| **C2.4** Selezione game con PDF indicizzato | 🟡 | `Games.HasIndexedContent` esiste; wizard non filtra esplicitamente | Gap UX: utente può selezionare game non indicizzato e l'agente rispondere generico |
| **C3.1** Dadi | ✅ | `toolkit/DiceRoller.tsx`, `SimpleDiceRoller.tsx`, `useDiceRoller.ts`, `DicePoolBuilder.tsx` | |
| **C3.2** Carte | ✅ | `toolkit/CardDeckTool.tsx` (PR #207 Phase 2) | |
| **C3.3** Note / pen&paper | ✅ | `NoteManagerWidget.tsx`, `WhiteboardWidget.tsx` | |
| **C3.4** Scoreboard standalone | ✅ | `toolkit/Scoreboard.tsx`, `ScoreTrackerWidget.tsx` | |
| **C3.5** Turn manager | ✅ | `TurnManagerWidget.tsx` | |
| **C3.6** Default drawer sempre disponibile | ✅ | Issue #266 "default game toolkit drawer" mergiato | |
| **C3.7** Tool azioni loggate nel diario | ✅ | `useSessionToolLog.ts` + `SessionEvent` con event type tool-specific | |
| **C4.1** Diario multi-game (SSE) | ✅ | `GameNightDiary.tsx`, `useGameNightDiary.ts`, `SseEventTypeMapper` con 5 diary events | |
| **C4.2** Cross-game timeline | ✅ | `SessionEvent.GameNightId` nullable → query by GameNightId | Spec §I5 |
| **C4.3** Foto in-session | ❌ **GAP** | `photos/page.tsx` tiene foto in `useState` locale, nessuna persistenza backend | Memory 2026-04-08: "Deferred: photo gallery UI" |
| **C4.4** Foto allegate a diary entry | ❌ **GAP** | Nessun `SessionEvent.AttachmentId` | Non in spec v2 |
| **C4.5** Export diario | 🟡 | Spec §Confini: "NON include export come immagine/PDF (fase futura)" | Esplicitamente deferito |
| **X.1** Offline host-only | ✅ | `sw-toolkit.js` cache static assets | Guest offline escluso (decisione Q2-A) |
| **X.2** Sync multi-device SSE | ✅ | `useSessionSSE`, `SseEventTypeMapper` esteso | |
| **X.3** Guest join via codice | ✅ | `/join/session/[code]/guest-session-view.tsx` | |

**Riassunto**: 19 ✅ / 3 🟡 / 2 ❌ — copertura ~83%. I gap critici riguardano **foto** (C4.3/C4.4) e **selezione PDF-aware** (C2.4).

---

## 3. Expert Panel Analysis

### 📚 Karl Wiegers — Requirements Engineering

**Framework**: EARS notation, fit criteria, testable requirements.

**Findings**:
1. **Ambiguità "complessità dipende dal boardgame"** — La narrativa non definisce CHI decide quale schema L2 serve. Il PDF indicizzato fornisce meta-dati? L'host configura manualmente? Attuale `PlayerResources` è generico (`Dictionary<string,int>`), scelta pragmatica ma senza contratto esplicito.
2. **Fit criterion mancante per "dimenticarsi dello stato"** — Il requisito "Capita che si fanno delle pause" è una motivazione, non un criterio. La spec v2 traduce questo in auto-save 60s: buono, ma manca una **Recovery Time Objective** formale (RTO) e test esplicito per "after 10 min background app resumes in ≤ Xs with zero state loss".
3. **Requisito non-funzionale mancante**: max latenza SSE tra device (P95)? Tempo massimo fra azione tool e apparizione in diario su device remoto?
4. **EARS reformulation consigliato**:
   - *Ubiquitous*: "The system shall persist L1+L2 session state."
   - *Event-driven*: "When a player performs a tool action, the system shall append a SessionEvent with gameNightId within 500ms P95."
   - *State-driven*: "While the session is Paused, the system shall suppress AutoSave runs."
   - *Unwanted behavior*: "If the device loses connectivity, the host shall continue operating the toolkit offline without creating new SessionEvents until reconnection."

**Verdict**: 🟡 Requisiti sottospecificati su metriche e failure modes. Domain layer è solido ma manca un **Software Requirements Specification** versionato per questa feature.

---

### 🧪 Gojko Adzic — Specification by Example

**Framework**: Given-When-Then, living documentation, key examples.

**Key examples da scrivere** (mancano o parziali):

```gherkin
Feature: Avvio Game Night mobile con gioco PDF-indicizzato

  Scenario: Happy path — gioco con PDF indicizzato
    Given sono autenticato come "Host" su smartphone
    And esistono 3 giochi nel catalogo, di cui "Catan" ha HasIndexedContent=true
    And ho creato un GameNightEvent "Sabato Sera" in stato Published con gameIds=[Catan]
    When navigo a /game-nights/<id> e premo "Start Session"
    And seleziono "Catan" come gioco corrente
    Then viene creata una Session collegata via GameNightSession (PlayOrder=1)
    And ricevo un SessionEvent "GameStartedInNight" via SSE
    And il diario cross-game mostra l'entry "Catan iniziato alle HH:MM"

  Scenario: Soft-filter — gioco senza PDF indicizzato (decisione: soft-filter)
    Given il gioco "ObscureGame" ha HasIndexedContent=false
    And il gioco "Catan" ha HasIndexedContent=true
    When apro il selettore gioco nel wizard
    Then "Catan" mostra il badge "AI pronto"
    And "ObscureGame" mostra il badge "Solo manuale"
    When seleziono "ObscureGame"
    Then vedo un warning inline "Agente AI non disponibile — il PDF non è ancora indicizzato"
    And posso comunque procedere a creare la session

  Scenario: Pausa e ripresa dopo 30 minuti
    Given una Session attiva con 2 turni e risorse giocatore settate
    When metto l'app in background per 30 minuti
    And l'AutoSave è girato 30 volte (ogni 60s)
    And riapro l'app
    Then il ResumeSessionPanel mostra lo stato esatto al minuto 30
    And nessun SessionEvent è duplicato

  Scenario: Foto scattata durante partita (decisione: IndexedDB MVP)
    Given sono nella sessione live, tab "Photos"
    When scatto una foto della board
    Then la foto viene salvata in IndexedDB con key "photo:<sessionId>:<uuid>"
    And la grid mostra la nuova foto con timestamp
    And dopo refresh della pagina la foto è ancora presente
    And l'eliminazione la rimuove sia dalla grid sia da IndexedDB

  Scenario: Dispute — agente cita regola
    Given Host e Guest disagreeano sulla risoluzione di un attacco
    When Host chiede all'agente "Chi vince in caso di parità?"
    Then la risposta cita la pagina del PDF indicizzato (RAG con page number)
    And il messaggio è visibile a tutti i device via SSE broadcast della chat

  Scenario: AutoSave health metric (decisione: F3 in scope)
    Given una Session attiva e AutoSaveJob schedulato ogni 60s
    When chiamo GET /metrics
    Then trovo la metrica "meepleai_autosave_last_run_age_seconds" con valore < 60
    When l'AutoSaveJob non gira per 130 secondi (es. job stuck)
    Then il valore della metrica è > 120
    And nei log appare un warning "AutoSave job stale: last run X seconds ago"
```

**Verdict**: 🟡 Solo il happy path è coperto da test. Mancano scenari per PDF-awareness, persistenza foto, e dispute citation. **Azione**: convertire in Playwright E2E + BDD tests backend.

---

### 🎭 Alistair Cockburn — Use Cases

**Framework**: Goal-level use cases, sunny/rainy day, actor hierarchy.

**Use case principale (user goal level)**:
```
UC-GN-01: Orchestra Game Night Multi-Gioco
  Primary actor: Host (mobile)
  Stakeholders: Guests (2), AI Agent, System
  Precondition: autenticato, GameNight Published, ≥1 game con PDF indexed
  Minimal guarantee: stato sessione persistente, ricoverabile
  Success guarantee: diario cross-game completo, finalizzato
  Trigger: Host apre app e preme "Start" su evento Published

  Main success scenario:
    1. Host autentica (auto da sessione precedente)
    2. Host seleziona GameNightEvent Published
    3. Sistema mostra lista giochi dell'evento (con badge PDF-indexed)
    4. Host sceglie Game A → StartGameNightSessionCommand
    5. Sistema crea Session, PlayOrder=1, emette SSE GameStartedInNight
    6. Host + Guests vedono LiveSessionView
    7. Durante la partita: azioni turno/tool/chat agente emettono SessionEvents
    8. Host conclude Game A → CompleteGameNightSessionCommand, punteggio finale
    9. Sistema mostra GameTransitionDialog → Host sceglie Game B o Finalize
    10. [Se B] loop da 4; [se Finalize] FinalizeGameNightCommand

  Extensions (rainy day):
    3a. Nessun gioco PDF-indexed → avviso "agente limitato" [GAP]
    4a. Start fallisce (DB down) → rollback command, toast errore, retry
    6a. Host perde connessione → host continua offline (toolkit), guests vedono "host offline" badge
    7a. AutoSave job detecta conflict su checkpoint → ultimo-vince con warning
    9a. Host chiude app prima di Finalize → alla riapertura trova GameNight in stato "In corso" con ResumeSessionPanel
    10a. Finalize fallisce validazione (sessione non chiusa) → richiede close-all
```

**Verdict**: ✅ Use case supportato end-to-end. Le extensions 3a e 9a rappresentano i gap C2.4 e il flusso di resume cross-app-lifecycle.

---

### 🏗️ Martin Fowler — Architecture & Domain Model

**Framework**: DDD, bounded contexts, event-driven, anti-corruption layers.

**Findings**:
1. **Cross-BC orchestration corretta** — `StartGameNightSessionCommand` in GameManagement BC usa `IMediator.Send(CreateSessionCommand)` per SessionTracking BC → no repository cross-BC injection (ADR §C4 ✅). **Rischio residuo**: se il command SessionTracking fallisce dopo il persist del `GameNightSessionEntity`, serve compensazione. Verifica: l'unità di lavoro è atomica (stesso DbContext transaction) o esiste saga?
2. **PlayerResources come Value Object** — Immutabile con JSON roundtrip. Buona scelta. Ma la serializzazione `Dictionary<string,int>` è **povera di schema**: non permette di differenziare "risorsa con max" da "contatore". Per giochi complessi (Terraforming Mars) servirà evoluzione verso schema-per-gioco.
3. **SessionEvent come event log condiviso** — Riuso invece di nuova entity `DiaryEntry` (§I5) è **ottima decisione DDD**: evita duplicazione del concetto "cosa è successo". Il diario cross-game è una proiezione read-side, consistente con CQRS.
4. **GameNightSession come linking entity** — Corretto. Alternativa sarebbe stata "Session.GameNightId FK" ma quella avrebbe mescolato concetti di appartenenza. La linking entity con PlayOrder e status separato è più espressiva.
5. **Manca**: **read model materializzato** per "MyOngoingGameNights" — query corrente probabilmente joina 4+ tabelle. Se diventa hot path, serve projection.
6. **Assenza ACL verso KnowledgeBase** — Il chat agent legge dal vector store del gioco. Se KB cambia embedding (es. e5-base → altro), servirà un contract test. Il claim del memory ("e5-base 768d globally") è una foundation assumption.

**Verdict**: ✅ Architettura DDD solida. Rischi: (a) atomicità cross-BC in error path, (b) evoluzione PlayerResources verso schema per gioco (L3 già out-of-scope ma è un debt).

---

### 🚨 Michael Nygard — Production Readiness / Release It

**Framework**: Stability patterns, failure modes, circuit breakers, observability.

**Failure modes analysis**:

| Failure | Probabilità | Impatto | Mitigazione attuale | Gap |
|---|---|---|---|---|
| Quartz AutoSave job stuck | Media | Alto — perdita stato | job.Remove() wired in pause/finalize | **Manca**: health check del job, metric "last_autosave_age_seconds" |
| SSE connection drop | Alta (mobile) | Medio | `useSessionStream` reconnect | **Manca**: backoff exponential esplicito in codice? Verificare |
| LLM slowdown/timeout (agente) | Media | Medio | `ChatWithSessionAgentCommand` | **Manca**: circuit breaker su LLM provider, fallback "agente non disponibile" |
| Vector store vuoto per game selected | Bassa ma certa se C2.4 gap | Alto UX | Nessuna | **Gap C2.4** |
| S3/Blob storage unreachable (foto C4.3 futuro) | Bassa | Medio | N/A | Quando si implementa: retry + queue |
| Concurrent finalize da 2 device host | Bassa | Alto | `[Timestamp] RowVersion` su Session? Verificare | **Verifica** concurrency token |
| GameNightEvent deleted mentre session attiva | Bassa | Alto | Soft delete `IsDeleted` | Verifica cascade: cosa accade a GameNightSession orfani? |

**Observability gaps**:
- Metric `game_night_sessions_active_gauge` (per alerting)
- Metric `autosave_job_execution_latency_histogram`
- Trace: span propagation da SSE pubblicato a consumato sui device (è complicato in browser ma log correlation ID almeno)
- Log strutturato su `SessionEvent` creation con `gameNightId` sempre tag

**Verdict**: 🟡 Stabilità applicativa ragionevole; telemetria insufficiente per feature mission-critical. **Priorità**: health check AutoSave job + circuit breaker LLM.

---

### 🧪 Lisa Crispin — Agile Testing Quadrants

**Framework**: Q1 (unit/component auto), Q2 (business-facing auto), Q3 (exploratory), Q4 (non-functional).

| Quadrant | Copertura attuale | Gap |
|---|---|---|
| **Q1** Unit/component | ✅ Alta: `PlayerResourcesTests`, `GameNightSessionTests`, `StartGameNightSessionTests`, `FinalizeGameNightTests`, `AutoSaveSessionTests`, hook tests previsti | Plan nota: "Deferred: hook unit tests" per `useGameNightDiary`, `useGameNightMultiSession` → **completare** |
| **Q2** Business-facing auto (acceptance) | 🟡 Playwright E2E per flow base? Verificare | Scrivere E2E mobile viewport per: start→play→pause→resume→finalize con 2 giochi consecutivi |
| **Q3** Exploratory (manual) | ❓ | Session di "dogfooding" con reale serata di 3 persone, checklist: offline→online transition, SSE cross-device, agent citation accuracy |
| **Q4** Non-functional | ❌ | **Manca**: load test con N device connessi in SSE (es. 10), battery drain test mobile, accessibility audit su touch targets |

**Accessibility specifica mobile**:
- Touch targets ≥ 44×44px per tool drawer (verificare)
- Screen reader labels su dice roller results
- Color contrast sotto gradient overlays (photo page usa `bg-black/60` su testo bianco → OK)

**Verdict**: 🟡 Quadrant 1 solido, Q2-Q4 parziali o assenti. Feature mission-critical richiede Q3 dogfooding formalizzato.

---

### ✏️ Jean-luc Doumont — Communication Clarity

**Framework**: Trees (hierarchy), effective messages, cognitive load.

**Narrativa originale — audit**:
- **Messaggio principale**: "Voglio orchestrare una serata multi-gioco con supporto digitale completo."
- **Supporto 4 pilastri**: stato / agente / strumenti / diario — ✅ struttura chiara.
- **Debolezze**:
  1. "Capita che si fanno delle pause..." è un *sintomo*, non un requisito. Riformulare: "Il sistema deve sopravvivere a pause di durata arbitraria senza intervento utente."
  2. "La complessità dello stato dipende dal boardgame" → split in L1/L2/L3 come già fatto nella spec v2. Comunicare ai devs con esempi: L1=Catan, L2=Terraforming Mars risorse, L3=Gloomhaven mappa.
  3. "Selezioniamo un gioco che abbia un pdf indicizzato" → **implicito requisito di filtro UI**. Diventa requirement: "Il selettore gioco mostra badge 'AI pronto' solo per giochi con PDF indicizzato."

**Raccomandazione communication**:
- Spec panel doc (questo) come **single source of truth** per il flusso
- Copy UI: "Agente AI disponibile" vs "Regole solo manuali" (coppia binaria chiara)
- Diary entries: formato consistente `<HH:MM> <icona> <soggetto> <verbo> <oggetto>`

**Verdict**: ✅ La decomposizione a 4 pilastri è già chiara. Principali miglioramenti sono su UI copy e sui criteri del requirement implicito "gioco deve essere PDF-indicizzato".

---

## 4. Synthesis — Cross-Framework

### 🤝 Convergent insights (tutti gli esperti concordano)
1. **Gap critico #1 — C4.3/C4.4 Foto persistenti**: Wiegers (fit criterion mancante), Adzic (scenario rosso), Nygard (futuro S3 integration). **Azione**: spec + implementazione `SessionEventAttachment` + S3 upload via `IBlobStorageService` (già esistente, vedi `storage.secret`).
2. **Gap critico #2 — C2.4 Filtro PDF-indexed nel wizard**: Adzic (scenario sad path), Cockburn (extension 3a), Doumont (requirement implicito). **Azione**: aggiungere badge "AI pronto" nel selettore gioco + warning se utente sceglie gioco non indicizzato.
3. **Testabilità**: Crispin e Adzic concordano su necessità di E2E mobile viewport per flusso completo multi-gioco.

### ⚖️ Productive tensions
1. **Nygard (robustezza) ⚡ Fowler (semplicità DDD)**: Nygard vuole saga compensation; Fowler dice "unit of work è sufficiente finché transazione locale". Risoluzione: verificare se `StartGameNightSessionCommand` e il `CreateSessionCommand` interno condividono DbContext. Se sì, abbastanza. Se no, serve outbox.
2. **Wiegers (formalità) ⚡ Adzic (esempi concreti)**: Wiegers chiede EARS specs scritti; Adzic dice "scrivi Gherkin eseguibili". Risoluzione: **Gherkin SONO gli EARS** — un feature file ben scritto soddisfa entrambi.
3. **Crispin (test Q4) ⚡ Time constraint**: Load test SSE e battery drain sono costosi. Risoluzione: prioritizzare dogfooding Q3 (basso costo, alto valore su feature consumer).

### 🕸️ System patterns (Meadows — lens aggiunto)
- **Feedback loop positivo**: più azioni tool → più `SessionEvent` → diario più ricco → più soddisfazione utente → più uso del sistema. Leverage point: ridurre friction di "trigger azione tool" (UX del drawer toolkit).
- **Loop bilanciante**: complessità stato L2 ↔ costo di modellazione per gioco. Pattern emergente: investire in **schema-per-gioco** solo quando il gioco supera 100+ giocate misurate (pay-as-you-go).

### ⚠️ Blind spots
1. **Onboarding primo utilizzo**: nessuna spec copre "prima volta che l'utente apre l'app e non ha alcun gioco". Serve empty state.
2. **Multi-language**: UI è IT; PDF possono essere in EN/IT/altro. RAG supporta multilingue? Chi garantisce?
3. **Privacy foto**: se si implementa C4.3, le foto possono contenere volti di giocatori → GDPR. Consenso esplicito richiesto.
4. **Rate limit LLM**: se 3+ giocatori chattano in parallelo, throttling del provider?

### 💬 Clarity / actionable takeaways
Il sistema è all'**83%** di copertura. I 2 gap fondamentali (foto + filtro PDF) sono identificabili in < 1 sprint ciascuno.

---

## 5. Decisioni utente (2026-04-08)

| Q | Domanda | Decisione | Razionale |
|---|---|---|---|
| 1 | **Foto** persistenza | **IndexedDB client-side** | MVP rapido, no backend changes, gallery per-device (host-only). Cross-device deferito. |
| 2 | **PDF-aware filter** | **Soft-filter** (badge + warning) | Inclusivo, non blocca uso giochi non indicizzati. |
| 3 | **Dispute mode** | **Chat libera attuale** | Nessuna UI dedicata. Riusa `SessionChatWidget` esistente. |
| 4 | **L2 schema per-gioco** | (out-of-scope MVP) | Resta `Dictionary<string,int>` finché un gioco non lo forza. |
| 5 | **Observability** | **AutoSave health metric** (P1) | Massimo rischio data-loss, fix più semplice, valore immediato. SSE lag e LLM circuit breaker deferiti. |

## 5bis. Scope del piano di implementazione

**IN scope** (MVP hardening pack):
- **F1** — Soft-filter PDF-indexed nel wizard (badge "AI pronto" + warning su selezione gioco non indicizzato)
- **F2** — Foto persistenti via IndexedDB (carica da camera → store IndexedDB → grid render → delete → survive refresh)
- **F3** — AutoSave health metric: gauge `meepleai_autosave_last_run_age_seconds` esposto via `/metrics`, log warning se > 120s

**OUT of scope** (deferiti, da issue separate):
- E2E Playwright multi-game flow (backlog #3)
- LLM circuit breaker (backlog #5)
- Hook unit tests deferiti (backlog #6)
- Foto cross-device via S3 (richiede backend changes, fuori MVP)
- Dispute UI dedicata
- L2 schema per-gioco

---

## 6. Recommended Backlog (priorità ordinate)

| # | Item | Capability | Effort | Expert drivers |
|---|---|---|---|---|
| 1 | Badge "AI pronto" + warning su game senza PDF indicizzato | C2.4 | S | Adzic, Cockburn, Doumont |
| 2 | `SessionEventAttachment` entity + S3 upload foto + UI refactor photo page | C4.3/C4.4 | M | Wiegers, Adzic, Nygard |
| 3 | E2E Playwright mobile: multi-game flow completo (start→pause→resume→finalize) | test Q2 | M | Crispin, Adzic |
| 4 | Health check + metric per AutoSave job (`last_autosave_age_seconds`) | observability | S | Nygard |
| 5 | Circuit breaker LLM + fallback "agente temporaneamente non disponibile" | robustness | S | Nygard |
| 6 | Hook unit tests: `useGameNightDiary`, `useGameNightMultiSession` | test Q1 | XS | Crispin |
| 7 | Dogfooding session strutturata con checklist Q3 | test Q3 | S | Crispin |
| 8 | EARS requirements doc formale per feature (basato su questa spec) | governance | S | Wiegers |
| 9 | `MyOngoingGameNights` read model materializzato (se perf lo richiede) | performance | M | Fowler (deferibile) |
| 10 | Schema registry L2 per-gioco (quando un gioco lo richiede) | evolvibilità | L | Fowler (defer) |

---

## 7. Closing

La feature **Game Night Experience v2** è già una delle feature più ricche del monorepo e l'allineamento tra spec v2 (PR #265) e la narrativa utente qui analizzata è **ottimo**. I gap residui (foto persistenti, filtro PDF-aware, autosave health) sono piccoli e ben isolati.

**MVP hardening pack** (questa spec): F1 + F2 + F3 → un solo plan, 3 fasi indipendenti.

**Plan di riferimento**: `docs/superpowers/plans/2026-04-08-game-night-mvp-hardening.md`
