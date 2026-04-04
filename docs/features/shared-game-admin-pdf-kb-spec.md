# Spec Panel: Admin Shared Game + PDF + KB Agent + Notifiche

**Data generazione**: 10 marzo 2026
**Spec Panel Mode**: Ultrathink — Discussion + Critique
**Esperti coinvolti**: Wiegers, Adzic, Fowler, Nygard, Crispin

---

## Indice

1. [User Stories con Acceptance Criteria](#1-user-stories)
2. [Bug Analysis](#2-bug-analysis)
3. [E2E Test Design](#3-e2e-test-design)
4. [Architectural Flow](#4-architectural-flow)
5. [Improvement Roadmap](#5-improvement-roadmap)

---

## 1. User Stories

### US-01: Admin gestisce Shared Game nel catalogo

**Come** admin/editor,
**voglio** creare, modificare, approvare e archiviare giochi nel catalogo condiviso,
**in modo da** mantenere un catalogo curato di giochi da tavolo per la community.

#### Acceptance Criteria

| AC | Criterio | Verifica |
|----|----------|----------|
| AC-01.1 | Admin crea shared game con tutti i campi obbligatori (Title, MinPlayers, MaxPlayers) | `POST /admin/shared-games` → 201 + `GameStatus.Draft` |
| AC-01.2 | Admin modifica game esistente | `PUT /admin/shared-games/{id}` → 200 + campi aggiornati |
| AC-01.3 | Admin sottomette per approvazione | `POST .../submit-for-approval` → status `PendingApproval` |
| AC-01.4 | Admin approva pubblicazione | `POST .../approve-publication` → status `Published` |
| AC-01.5 | Admin rifiuta con motivazione | `POST .../reject-publication` → status `Draft` + `RejectionReason` |
| AC-01.6 | Admin archivia game pubblicato | `POST .../archive` → status `Archived` |
| AC-01.7 | Soft-delete con workflow di approvazione | `DELETE` → `DeleteRequest` pending, non cancellazione immediata |
| AC-01.8 | Batch approve/reject (max 50) | `POST .../batch-approve` → tutti aggiornati atomicamente |

#### Scenari BDD

```gherkin
Scenario: Admin crea e pubblica un shared game
  Given un utente con ruolo "Admin"
  When crea un shared game con:
    | Campo         | Valore          |
    | Title         | Catan           |
    | MinPlayers    | 3               |
    | MaxPlayers    | 4               |
    | YearPublished | 1995            |
  Then il game viene creato con status "Draft"
  And CreatedBy = admin userId

  When l'admin sottomette per approvazione
  Then lo status diventa "PendingApproval"
  And viene inviata notifica AdminSharedGameSubmitted

  When un secondo admin approva la pubblicazione
  Then lo status diventa "Published"
  And il game è visibile nell'API pubblica GET /shared-games

Scenario: Solo game Draft possono essere sottomessi
  Given un shared game con status "Published"
  When l'admin tenta di sottomettere per approvazione
  Then riceve errore 400 "Only Draft games can be submitted"

Scenario: Soft-delete richiede approvazione
  Given un shared game "Published"
  When l'editor richiede cancellazione
  Then viene creata una DeleteRequest con status "Pending"
  And il game rimane visibile fino all'approvazione admin
  When l'admin approva la delete request
  Then IsDeleted = true, DeletedAt = now
  And il game è escluso dalle query con HasQueryFilter
```

#### Definition of Done
- [ ] Tutti gli endpoint admin funzionanti e testati
- [ ] Rate limiting: 100 req/min per admin
- [ ] Audit trail via domain events (SharedGameCreatedEvent, etc.)
- [ ] Authorization: AdminOrEditorPolicy per CRUD, AdminOnlyPolicy per approve/delete
- [ ] Cache invalidation su update/delete (L1 15min, L2 1h)

---

### US-02: Admin carica PDF regolamento per Shared Game

**Come** admin,
**voglio** caricare PDF di regolamenti per i giochi del catalogo condiviso e approvarli per il RAG processing,
**in modo da** alimentare il knowledge base dell'agente AI.

#### Acceptance Criteria

| AC | Criterio | Verifica |
|----|----------|----------|
| AC-02.1 | Upload PDF per shared game | `POST /admin/shared-games/{id}/documents` → crea `PdfDocument` + `SharedGameDocument` |
| AC-02.2 | Validazione file (magic bytes, size ≤100MB, .pdf) | Upload di .exe → 400; Upload 150MB → 400 |
| AC-02.3 | Pipeline 7-state si avvia automaticamente | Stato: Pending → Uploading → Extracting → Chunking → Embedding → Indexing → Ready |
| AC-02.4 | Document versioning (solo 1 active per tipo) | Upload v2.0 + SetAsActive → v1.0 diventa inactive |
| AC-02.5 | Approval workflow pre-RAG | `SharedGameDocument.ApprovalStatus` = Pending → Admin approva → Approved |
| AC-02.6 | Deduplicazione via ContentHash | Upload stesso file → 409 Conflict |
| AC-02.7 | Document category (Rulebook, Expansion, Errata) | Category determina pipeline routing (analyzable vs search-only) |
| AC-02.8 | Progress tracking real-time | `GET /pdfs/{id}/progress/stream` → SSE con % e ETA |

#### Scenari BDD

```gherkin
Feature: Upload PDF per Shared Game

  Background:
    Given un admin autenticato
    And un shared game "Catan" con status "Published"

  Scenario: Upload standard con pipeline completo
    When l'admin carica "catan-regolamento.pdf" (8MB, 24 pagine)
      con DocumentType = "Rulebook" e Version = "1.0"
    Then il sistema:
      1. Valida il file (magic bytes, size, MIME)
      2. Crea PdfDocument con SharedGameId = catan_id, GameId = Guid.Empty
      3. Crea SharedGameDocument con PdfDocumentId, ApprovalStatus = Pending
      4. Avvia pipeline in background
    And la risposta contiene pdfDocumentId e sharedGameDocumentId

  Scenario: Pipeline processing con timing
    Given "catan-regolamento.pdf" è stato caricato
    When il pipeline processa il PDF
    Then gli stati transizionano con timing registrato:
      | Stato       | Campo Timing           | Timeout Max |
      | Uploading   | UploadingStartedAt     | 10s         |
      | Extracting  | ExtractingStartedAt    | 60s         |
      | Chunking    | ChunkingStartedAt      | 30s         |
      | Embedding   | EmbeddingStartedAt     | 30s         |
      | Indexing    | IndexingStartedAt      | 15s         |
    And al completamento ProcessingState = Ready
    And viene creato un VectorDocument con:
      - GameId matching
      - TotalChunks > 0
      - IndexedAt = now

  Scenario: Approvazione per RAG processing
    Given "catan-regolamento.pdf" è Ready e ApprovalStatus = Pending
    When l'admin approva via POST .../documents/{docId}/approve
    Then ApprovalStatus = Approved
    And ApprovedBy = admin userId
    And il documento è incluso nelle ricerche RAG (IsActiveForRag = true)

  Scenario: Fallback extractor (3 stage)
    Given un PDF con layout complesso (tabelle, colonne multiple)
    When Stage 1 (Unstructured) produce qualità < 0.80
    Then il sistema prova Stage 2 (SmolDocling)
    When Stage 2 produce qualità < 0.70
    Then il sistema usa Stage 3 (Docnet) come best-effort

  Scenario: Upload duplicato
    Given "catan-regolamento.pdf" già caricato con ContentHash = "abc123"
    When l'admin carica un file identico (stesso hash SHA-256)
    Then riceve errore 409 "Duplicate document detected"

  Scenario: PDF fallisce e viene ritentato
    Given il servizio Unstructured è temporaneamente offline
    When il PDF fallisce durante Extracting
    Then ProcessingState = Failed, ErrorCategory = Service
    And RetryCount = 0
    When RetryFailedPdfsJob esegue (ogni 5 min)
    Then il PDF viene riprocessato con backoff esponenziale
    And RetryCount incrementa (max 3 tentativi)
```

#### Definition of Done
- [ ] Upload crea sia PdfDocument che SharedGameDocument
- [ ] Pipeline 7-state con timing per stato
- [ ] 3-stage extractor fallback con quality threshold
- [ ] Deduplicazione via ContentHash SHA-256
- [ ] SSE progress stream funzionante
- [ ] Retry automatico con backoff esponenziale (max 3)
- [ ] Admin può approvare/rifiutare documenti per RAG

---

### US-03: Agente AI risponde con KB completo

**Come** utente,
**voglio** che l'agente AI risponda alle mie domande usando il knowledge base completo del gioco,
**in modo da** ricevere risposte accurate con citazioni dalle regole ufficiali.

#### Acceptance Criteria

| AC | Criterio | Verifica |
|----|----------|----------|
| AC-03.1 | Agent linkato a SharedGame | `POST .../link-agent/{agentId}` → `SharedGame.AgentDefinitionId` = agentId |
| AC-03.2 | RAG search su tutti i PDF indexed | Qdrant search filtra per `game_id`, recupera da tutti i VectorDocument |
| AC-03.3 | KB completeness check | Se PDF ancora in processing → errore "X of Y documents still processing" |
| AC-03.4 | Risposta con citazioni | Response include `citations` con pagina e sezione di origine |
| AC-03.5 | SSE streaming | Token-by-token: StateUpdate → Token(s) → Complete con usage/cost |
| AC-03.6 | Multi-game search (espansioni) | RAG cerca in base game + espansioni, boost 1.3x per espansioni |
| AC-03.7 | Degradazione graceful | Se KB parziale → `SessionDegradationLevel.LimitedAI` anziché errore |
| AC-03.8 | Fallback LLM | Rate limit → retry (2s) → fallback tier-based (PR #5249) |

#### Scenari BDD

```gherkin
Feature: Agent risponde con KB completo

  Background:
    Given un shared game "Catan" con:
      - 1 PDF regolamento "Ready" (VectorDocument con 45 chunks)
      - 1 Agent "Catan Tutor" linkato (AgentType = Tutor)
      - AgentSession creata con GameId corretto

  Scenario: Domanda con KB completo → risposta accurata
    When l'utente chiede "Come si costruisce una strada?"
      via POST /api/v1/agents/{agentId}/chat
    Then il sistema:
      1. Genera embedding della domanda (1536-dim)
      2. Cerca in Qdrant con filter game_id = catan_game_id
      3. Recupera top-15 chunks per similarity
      4. Applica cross-encoder reranking → top-5
      5. Applica sentence windowing (±1 chunks adiacenti)
      6. Assembla prompt con system prompt + context + question
      7. Streama risposta via SSE
    And la risposta contiene testo sulla costruzione strade
    And le citazioni referenziano pagine specifiche del PDF
    And l'evento Complete include tokenUsage e cost

  Scenario: KB incompleto → errore informativo
    Given "catan-expansion.pdf" è nello stato "Extracting"
    When l'utente chiede una domanda
    Then riceve errore: "1 of 2 documents are still being processed"
    And il LLM NON viene invocato (risparmio costi)

  Scenario: KB parziale con degradazione (sessione con espansioni)
    Given base game "Catan" ha KB Ready
    And espansione "Seafarers" NON ha PDF
    When l'utente apre sessione con entrambi i giochi
    Then SessionDegradationLevel = LimitedAI
    And il sistema cerca solo nel KB di Catan (base game)
    And la risposta include disclaimer sulle limitazioni

  Scenario: Multi-game RAG search
    Given "Catan" (base) ha 45 chunks in Qdrant
    And "Catan: Seafarers" (espansione) ha 30 chunks in Qdrant
    When l'utente chiede "Come funzionano le navi?"
    Then il sistema cerca in ENTRAMBI i game_id
    And i risultati di Seafarers hanno score × 1.3 (boost)
    And i top-5 risultati post-reranking includono chunk da entrambi

  Scenario: LLM fallback su rate limit
    Given il provider primario (OpenRouter) restituisce 429
    When la prima retry (dopo 2s) fallisce ancora
    Then il sistema:
      - Seleziona fallback model tier-based
      - Invia SSE event ModelDowngrade (type=21)
      - Continua la risposta con il model fallback
    And il frontend mostra banner ambra "Modello di fallback in uso"
```

#### Definition of Done
- [ ] Agent linkato correttamente a SharedGame con GameId mapping (**BUG-02 da risolvere**)
- [ ] RAG pipeline: embedding → Qdrant search → reranking → prompt assembly → streaming
- [ ] KB completeness check usa `ProcessingState` enum (**BUG-01 da risolvere**)
- [ ] Citazioni con pagina e sezione nel response
- [ ] Multi-game search con expansion boost 1.3x
- [ ] SSE streaming con tutti gli event types
- [ ] Fallback LLM con SSE ModelDowngrade event
- [ ] ChatThread persistence con metadata (agentType, confidence, citations)

---

### US-04: Admin riceve notifiche su processing PDF

**Come** admin,
**voglio** ricevere notifiche real-time quando un PDF è stato processato (successo o fallimento),
**in modo da** sapere quando il KB è pronto e intervenire su errori.

#### Acceptance Criteria

| AC | Criterio | Verifica |
|----|----------|----------|
| AC-04.1 | Notifica in-app su PDF Ready | `PdfStateChangedEvent` → `NotificationType.PdfUploadCompleted` → SSE broadcast |
| AC-04.2 | Notifica in-app su PDF Failed | `PdfStateChangedEvent` → `NotificationType.ProcessingFailed` → SSE broadcast |
| AC-04.3 | Notifica KB indexed | `VectorDocumentReadyIntegrationEvent` → `NotificationType.ProcessingJobCompleted` |
| AC-04.4 | Email asincrona (se preferenza attiva) | `EmailOnDocumentReady = true` → email accodata via `EnqueueEmailCommand` |
| AC-04.5 | Push notification (se configurata) | VAPID Web Push API → notifica browser |
| AC-04.6 | Preferenze utente rispettate | Delivery solo su canali abilitati dall'admin nelle preferences |
| AC-04.7 | Deep-link nella notifica | Link = `/admin/shared-games/{id}/documents` o `/library/games/{id}/agent` |
| AC-04.8 | Recupero notifiche perse | SSE drop → `GET /notifications` ritorna tutte le non-lette |

#### Scenari BDD

```gherkin
Feature: Notifiche processing PDF

  Background:
    Given un admin con preferenze:
      | Canale  | DocumentReady | DocumentFailed |
      | In-App  | true          | true           |
      | Email   | true          | true           |
      | Push    | false         | true           |

  Scenario: PDF completato → notifica multi-canale
    Given l'admin ha caricato "catan-rules.pdf"
    When il pipeline completa con ProcessingState = Ready
    Then PdfStateChangedEvent viene pubblicato
    And PdfNotificationEventHandler crea:
      - Notification in-app (type=PdfUploadCompleted, severity=Success)
      - EmailQueueItem (template: "document_ready")
    And InMemoryUserNotificationBroadcaster invia via SSE:
      ```json
      {
        "type": "pdf_upload_completed",
        "severity": "success",
        "title": "PDF processato con successo",
        "message": "catan-rules.pdf è pronto per il RAG",
        "link": "/admin/shared-games/{id}/documents"
      }
      ```
    And Push NON viene inviato (preferenza disabilitata)

  Scenario: PDF fallito → notifica con dettagli errore
    Given "complex-rules.pdf" fallisce durante Extracting
    And ErrorCategory = Parsing, RetryCount = 3 (max raggiunto)
    When ProcessingState transiziona a Failed
    Then l'admin riceve notifica:
      - type = ProcessingFailed
      - severity = Error
      - message include ErrorCategory e suggerimento (es. "Prova con un PDF di qualità migliore")
    And Push viene inviato (preferenza attiva per Failed)

  Scenario: VectorDocument indexed → notifica KB ready
    Given il PDF è stato processato e i chunks indicizzati in Qdrant
    When VectorDocumentReadyIntegrationEvent viene pubblicato
    Then VectorDocumentReadyNotificationHandler crea notifica:
      - type = ProcessingJobCompleted
      - link = "/library/games/{gameId}/agent"
      - message = "Il knowledge base del gioco è pronto"

  Scenario: Admin offline → recupero notifiche
    Given l'admin NON è connesso via SSE
    When un PDF completa il processing
    Then la notifica è persistita nel DB
    And quando l'admin riapre il browser:
      GET /api/v1/notifications?isRead=false
      → ritorna tutte le notifiche non lette

  Scenario: Email retry su fallimento SMTP
    Given EmailQueueItem creato per document_ready
    When il primo invio fallisce (SMTP timeout)
    Then EmailQueueStatus = Failed, RetryCount = 1
    And NextRetryAt = now + 1 minuto
    When EmailProcessorJob riprova dopo 1 minuto
    And il secondo invio riesce
    Then EmailQueueStatus = Sent

  Scenario: Email dead letter dopo 3 tentativi
    Given email fallisce 3 volte consecutive
    Then EmailQueueStatus = DeadLetter
    And admin può forzare retry via POST /admin/emails/{id}/retry
```

#### Definition of Done
- [ ] PdfNotificationEventHandler gestisce PdfStateChangedEvent (Ready + Failed)
- [ ] VectorDocumentReadyNotificationHandler gestisce integration event
- [ ] Multi-canale: in-app (SSE), email (queued), push (VAPID)
- [ ] Preferenze utente rispettate per ogni canale
- [ ] Email retry con backoff esponenziale (1m, 5m, 30m)
- [ ] Dead letter monitoring con alert admin
- [ ] Deep-link nelle notifiche
- [ ] Heartbeat SSE ogni 30s per mantenere connessione

---

## 2. Bug Analysis

### BUG-01: KB Completeness Check usa ProcessingStatus deprecato

**Severità**: 🔴 CRITICO
**Contesto**: Il check che determina se il Knowledge Base è completo usa la proprietà deprecated `ProcessingStatus` (4-state string) anziché `ProcessingState` (7-state enum autoritativo).

#### File Interessati

| # | File | Riga | Severità |
|---|------|------|----------|
| 1 | `KnowledgeBase/Application/Commands/AskAgentQuestionCommandHandler.cs` | 118 | 🔴 CRITICO |
| 2 | `Infrastructure/BackgroundServices/StalePdfRecoveryService.cs` | 135-142 | 🟡 IMPORTANTE |
| 3 | `DocumentProcessing/Infrastructure/Persistence/PdfDocumentRepository.cs` | 84 | 🟡 IMPORTANTE |
| 4 | `DocumentProcessing/Application/Queries/GetAllPdfsQueryHandler.cs` | 34, 86 | 🟡 IMPORTANTE |
| 5 | `DocumentProcessing/Application/Handlers/Queue/GetDashboardMetricsQueryHandler.cs` | 48, 52 | 🟡 IMPORTANTE |
| 6 | `DocumentProcessing/Application/Commands/ProcessPendingPdfs/ProcessPendingPdfsCommandHandler.cs` | 38 | 🟡 IMPORTANTE |
| 7 | `UserLibrary/Application/Handlers/GetGameWizardPreviewQueryHandler.cs` | 65 | 🟡 IMPORTANTE |
| 8 | `tests/Api.Tests/Integration/PerformanceQueryTests.cs` | 130 | 🟢 MINORE |

#### Root Cause

Il domain model `PdfDocument.cs` mantiene entrambe le proprietà in sync:

```csharp
// PdfDocument.cs:371-380
ProcessingStatus = newState switch
{
    PdfProcessingState.Pending => "pending",
    PdfProcessingState.Uploading or PdfProcessingState.Extracting or
    PdfProcessingState.Chunking or PdfProcessingState.Embedding or
    PdfProcessingState.Indexing => "processing",
    PdfProcessingState.Ready => "completed",
    PdfProcessingState.Failed => "failed",
    _ => "pending"
};
```

Il problema è che **5 stati intermedi** (Uploading, Extracting, Chunking, Embedding, Indexing) sono tutti mappati a `"processing"`. Il codice di query usa stringhe hardcoded:

```csharp
// AskAgentQuestionCommandHandler.cs:118 — IL BUG
var notCompleted = documents.Count(d =>
    !string.Equals(d.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase));
```

#### Impatto

- **Funzionale**: Il check funziona in pratica (il sync mantiene coerenza), ma:
  - Non distingue tra stati intermedi (impossibile sapere se un PDF è in Embedding vs Extracting)
  - Dipende da logica di sync che potrebbe divergere in futuro
  - Il warning message mostra solo "processing" senza dettagli sullo stato reale
- **Manutenibilità**: 8 file usano stringhe hardcoded fragili
- **Documentazione**: Il campo è marcato come deprecated (Issue #4215) ma mai migrato (Issue #4216)

#### Fix Raccomandato

```csharp
// PRIMA (bug)
var notCompleted = documents.Count(d =>
    !string.Equals(d.ProcessingStatus, "completed", StringComparison.OrdinalIgnoreCase));

// DOPO (fix)
var notCompleted = documents.Count(d =>
    d.ProcessingState != PdfProcessingState.Ready);

// Con messaggio migliorato
if (notCompleted > 0)
{
    var stateBreakdown = documents
        .Where(d => d.ProcessingState != PdfProcessingState.Ready)
        .GroupBy(d => d.ProcessingState)
        .Select(g => $"{g.Count()} {g.Key}")
        .ToList();

    throw new InvalidOperationException(
        $"{notCompleted} of {documents.Count} documents are still being processed " +
        $"({string.Join(", ", stateBreakdown)}). Please wait for processing to complete.");
}
```

#### Migration Path (Issue #4216)

1. Creare `FindByStateAsync(PdfProcessingState)` nel repository
2. Aggiornare ogni handler per usare enum
3. Migrare DTO per includere `ProcessingState`
4. Deprecare `ProcessingStatus` nelle API response (versionamento)
5. Aggiornare test con valori enum

---

### BUG-02: Agent.GameId mai impostato quando linkato a SharedGame

**Severità**: 🔴 CRITICO
**Contesto**: Quando un agent viene linkato a uno shared game via `LinkAgentToSharedGameCommand`, il `Agent.GameId` non viene mai aggiornato. Questo causa un gap nel flusso RAG perché il frontend non ha modo di sapere quale `GameId` usare per le ricerche Qdrant.

#### Flusso Attuale (Buggy)

```
1. Admin linka Agent a SharedGame
   → LinkAgentToSharedGameCommandHandler.cs:29-51
   → Chiama game.LinkAgent(agentId)
   → Imposta SharedGame.AgentDefinitionId = agentId  ✅
   → Agent.GameId rimane NULL  ❌

2. Admin carica PDF per SharedGame
   → UploadSharedGamePdfCommandHandler.cs:99-115
   → Crea PdfDocument con:
     - SharedGameId = shared_game_id  ✅
     - GameId = Guid.Empty  ⚠️ (sentinel value)

3. Pipeline indicizza in Qdrant
   → QdrantService.IndexDocumentChunksAsync
   → game_id nel payload = PdfDocument.GameId (Guid.Empty!)  ❌

4. Utente chatta con Agent
   → AgentSessionEndpoints.cs:97
   → Client invia GameId nel request body
   → RagPromptAssemblyService cerca in Qdrant con filter game_id = ???
   → Se client invia SharedGameId → NO MATCH in Qdrant
   → Se client invia Guid.Empty → potenziale match ma semanticamente sbagliato
```

#### Root Cause

Due problemi concatenati:

**Problema A**: `LinkAgentToSharedGameCommandHandler` non aggiorna `Agent.GameId`:

```csharp
// LinkAgentToSharedGameCommandHandler.cs:42-45
var game = await _repository.GetByIdAsync(request.SharedGameId, ct);
game.LinkAgent(request.AgentId);  // Solo SharedGame.AgentDefinitionId viene impostato
await _repository.UpdateAsync(game, ct);
// MANCA: await _agentRepository.UpdateGameId(request.AgentId, someGameId, ct);
```

**Problema B**: `UploadSharedGamePdfCommandHandler` usa `GameId = Guid.Empty`:

```csharp
// UploadSharedGamePdfCommandHandler.cs:103
GameId = Guid.Empty,  // Sentinel value — Qdrant indicizzerà con game_id = "00000000..."
```

Questo significa che il Qdrant `game_id` per i PDF di shared games è `Guid.Empty`, ma nessun agent sa di cercare con quel valore.

#### Impatto

- **Funzionale**: L'agente linkato a uno shared game **non può trovare nessun chunk in Qdrant** perché:
  - `Agent.GameId` è null → il client non sa quale GameId usare
  - I PDF sono indicizzati con `game_id = Guid.Empty` → nessun agent cerca per quel valore
- **UX**: L'utente chatta con l'agent e riceve risposte senza contesto KB
- **Silenzioso**: Non produce errori, semplicemente restituisce 0 risultati da Qdrant

#### Fix Raccomandato

**Opzione A (Minima)**: SharedGameId come game_id in Qdrant

```csharp
// UploadSharedGamePdfCommandHandler.cs — Fix indicizzazione
var qdrantGameId = command.SharedGameId.ToString();  // Usa SharedGameId come game_id

// LinkAgentToSharedGameCommandHandler.cs — Fix Agent.GameId
var agent = await _agentRepository.GetByIdAsync(request.AgentId, ct);
agent.SetGameId(request.SharedGameId);  // Imposta Agent.GameId = SharedGameId
await _agentRepository.UpdateAsync(agent, ct);
```

**Opzione B (Completa)**: Mapping table per SharedGame → GameId

```csharp
// Nuovo servizio di risoluzione
public class SharedGameIdResolver : ISharedGameIdResolver
{
    public async Task<Guid> ResolveGameIdForRagAsync(Guid sharedGameId, CancellationToken ct)
    {
        // Cerca se esiste un Game (UserLibrary) associato
        // Altrimenti usa SharedGameId direttamente
        // Gestisce anche espansioni
    }
}
```

**Raccomandazione**: Opzione A per fix immediato, Opzione B per architettura a lungo termine.

#### File da Modificare

| File | Modifica |
|------|----------|
| `LinkAgentToSharedGameCommandHandler.cs` | Aggiungere update di `Agent.GameId` |
| `UploadSharedGamePdfCommandHandler.cs` | Usare `SharedGameId` come `game_id` per Qdrant |
| `Agent.cs` | Aggiungere metodo `SetGameId(Guid gameId)` se mancante |
| `RagPromptAssemblyService.cs` | Verificare che accetti SharedGameId come input |

---

### BUG-03: ApproveDocumentForRag non triggera RAG pipeline

**Severità**: 🟡 IMPORTANTE
**Contesto**: `ApproveDocumentForRagProcessingCommandHandler` pubblica `DocumentApprovedForRagEvent` ma **nessun handler lo ascolta**. L'approvazione cambia solo lo stato della `SharedGameDocument` senza effetti sulla pipeline di processing.

#### Codice Attuale

```csharp
// ApproveDocumentForRagProcessingCommandHandler.cs:61-68
await _mediator.Publish(
    new DocumentApprovedForRagEvent(
        command.DocumentId,
        document.SharedGameId,
        document.PdfDocumentId,
        command.ApprovedBy,
        document.ApprovedAt ?? DateTime.UtcNow),
    cancellationToken);
// Evento pubblicato ma NESSUN HANDLER registrato
```

#### Impatto

- Il workflow admin "upload → approve → RAG" non è collegato end-to-end
- L'approvazione è un no-op funzionale (cambia solo lo stato nel DB)
- Il PDF viene già processato automaticamente all'upload (via `UploadSharedGamePdfCommandHandler`)

#### Fix Raccomandato

Creare un handler per `DocumentApprovedForRagEvent` che:
1. Verifica che il PdfDocument associato sia in stato Ready
2. Se Ready: imposta `IsActiveForRag = true` sul PdfDocument
3. Se non Ready: schedula un check differito
4. Pubblica notifica all'admin

---

## 3. E2E Test Design

### Test: Admin Full Journey — SharedGame + PDF + Agent + KB + Notifications

```csharp
[Fact]
[Trait("Category", "E2E")]
[Trait("BoundedContext", "CrossContext")]
public async Task Admin_SharedGame_PDF_Agent_KB_Notification_FullFlow()
{
    // ============================================================
    // ARRANGE: Setup admin context
    // ============================================================
    var adminToken = await AuthHelper.GetAdminTokenAsync(_client);
    _client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", adminToken);

    var adminUserId = JwtHelper.ExtractUserId(adminToken);

    // ============================================================
    // STEP 1: Create SharedGame
    // ============================================================
    var createGameRequest = new
    {
        Title = "E2E Test Game - Catan",
        MinPlayers = 3,
        MaxPlayers = 4,
        PlayingTimeMinutes = 90,
        YearPublished = 1995,
        Description = "Classic resource trading game"
    };

    var createResponse = await _client.PostAsJsonAsync(
        "/api/v1/admin/shared-games", createGameRequest);
    createResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var sharedGame = await createResponse.Content
        .ReadFromJsonAsync<SharedGameDto>();
    sharedGame!.Status.Should().Be("Draft");
    var sharedGameId = sharedGame.Id;

    // ============================================================
    // STEP 2: Publish SharedGame
    // ============================================================
    var submitResponse = await _client.PostAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}/submit-for-approval", null);
    submitResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var approveResponse = await _client.PostAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}/approve-publication", null);
    approveResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    // Verify published
    var getResponse = await _client.GetAsync(
        $"/api/v1/shared-games/{sharedGameId}");
    var publishedGame = await getResponse.Content
        .ReadFromJsonAsync<SharedGameDto>();
    publishedGame!.Status.Should().Be("Published");

    // ============================================================
    // STEP 3: Upload PDF
    // ============================================================
    var pdfContent = TestPdfGenerator.CreateSimplePdf(
        pages: 10,
        content: "Road building rules: A player may build a road...");

    using var formContent = new MultipartFormDataContent();
    formContent.Add(
        new ByteArrayContent(pdfContent),
        "file",
        "catan-rules.pdf");
    formContent.Add(new StringContent("Rulebook"), "documentType");
    formContent.Add(new StringContent("1.0"), "version");

    var uploadResponse = await _client.PostAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}/documents",
        formContent);
    uploadResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var uploadResult = await uploadResponse.Content
        .ReadFromJsonAsync<UploadSharedGamePdfResult>();
    var pdfDocumentId = uploadResult!.PdfDocumentId;
    var sharedGameDocId = uploadResult.SharedGameDocumentId;

    // ============================================================
    // STEP 4: Wait for PDF processing to complete
    // ============================================================
    var maxWait = TimeSpan.FromMinutes(2);
    var pollInterval = TimeSpan.FromSeconds(2);
    var deadline = DateTime.UtcNow + maxWait;

    PdfProgressDto? progress = null;
    while (DateTime.UtcNow < deadline)
    {
        var progressResponse = await _client.GetAsync(
            $"/api/v1/pdfs/{pdfDocumentId}/progress");

        if (progressResponse.IsSuccessStatusCode)
        {
            progress = await progressResponse.Content
                .ReadFromJsonAsync<PdfProgressDto>();

            if (progress!.State == "Ready")
                break;

            if (progress.State == "Failed")
                Assert.Fail($"PDF processing failed: {progress.Error}");
        }

        await Task.Delay(pollInterval);
    }

    progress.Should().NotBeNull();
    progress!.State.Should().Be("Ready");
    progress.ProgressPercentage.Should().Be(100);

    // ============================================================
    // STEP 5: Verify VectorDocument created
    // ============================================================
    // Query via admin endpoint or direct DB check
    var docsResponse = await _client.GetAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}/documents/active");
    docsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    // ============================================================
    // STEP 6: Approve document for RAG
    // ============================================================
    var approveDocResponse = await _client.PostAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}" +
        $"/documents/{sharedGameDocId}/approve",
        null);
    approveDocResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    // ============================================================
    // STEP 7: Create Agent
    // ============================================================
    var createAgentRequest = new
    {
        Name = "Catan Tutor",
        Type = "Tutor",
        GameId = sharedGameId  // Note: This is the mapping issue (BUG-02)
    };

    var agentResponse = await _client.PostAsJsonAsync(
        "/api/v1/agents", createAgentRequest);
    agentResponse.StatusCode.Should().Be(HttpStatusCode.Created);

    var agent = await agentResponse.Content
        .ReadFromJsonAsync<AgentDto>();
    var agentId = agent!.Id;

    // ============================================================
    // STEP 8: Link Agent to SharedGame
    // ============================================================
    var linkResponse = await _client.PostAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}/link-agent/{agentId}",
        null);
    linkResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    // ============================================================
    // STEP 9: Chat with Agent (SSE streaming)
    // ============================================================
    var chatRequest = new
    {
        Message = "Come si costruisce una strada a Catan?",
        ChatThreadId = (Guid?)null,
        GameSessionId = (Guid?)null
    };

    var chatRequestMessage = new HttpRequestMessage(
        HttpMethod.Post,
        $"/api/v1/agents/{agentId}/chat")
    {
        Content = JsonContent.Create(chatRequest)
    };
    chatRequestMessage.Headers.Accept.Add(
        new MediaTypeWithQualityHeaderValue("text/event-stream"));

    var chatResponse = await _client.SendAsync(
        chatRequestMessage,
        HttpCompletionOption.ResponseHeadersRead);
    chatResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    chatResponse.Content.Headers.ContentType!.MediaType
        .Should().Be("text/event-stream");

    // Parse SSE stream
    var sseEvents = new List<RagStreamingEvent>();
    await using var stream = await chatResponse.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream);

    while (await reader.ReadLineAsync() is { } line)
    {
        if (line.StartsWith("data: "))
        {
            var json = line["data: ".Length..];
            var sseEvent = JsonSerializer.Deserialize<RagStreamingEvent>(
                json, SseJsonOptions.Default);
            if (sseEvent != null)
                sseEvents.Add(sseEvent);
        }

        // Stop after Complete event
        if (sseEvents.Any(e => e.Type == "Complete"))
            break;
    }

    // ============================================================
    // ASSERTIONS: Verify SSE events
    // ============================================================

    // Should have StateUpdate
    sseEvents.Should().Contain(e => e.Type == "StateUpdate");

    // Should have Token events with content
    var tokenEvents = sseEvents.Where(e => e.Type == "Token").ToList();
    tokenEvents.Should().NotBeEmpty("Agent should generate response tokens");

    // Concatenate response text
    var fullResponse = string.Join("",
        tokenEvents.Select(e => e.Content));
    fullResponse.Should().NotBeNullOrWhiteSpace();

    // Should have Complete with usage
    var completeEvent = sseEvents.Single(e => e.Type == "Complete");
    completeEvent.Usage.Should().NotBeNull();
    completeEvent.Usage!.PromptTokens.Should().BeGreaterThan(0);

    // ============================================================
    // STEP 10: Verify notifications
    // ============================================================
    var notificationsResponse = await _client.GetAsync(
        "/api/v1/notifications?isRead=false");
    notificationsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

    var notifications = await notificationsResponse.Content
        .ReadFromJsonAsync<PaginatedList<NotificationDto>>();

    // Should have PDF completion notification
    notifications!.Items.Should().Contain(n =>
        n.Type == "pdf_upload_completed" &&
        n.Severity == "success");

    // Should have KB ready notification
    notifications.Items.Should().Contain(n =>
        n.Type == "processing_job_completed");

    // Verify notification has deep-link
    var pdfNotification = notifications.Items
        .First(n => n.Type == "pdf_upload_completed");
    pdfNotification.Link.Should().NotBeNullOrWhiteSpace();

    // ============================================================
    // CLEANUP
    // ============================================================
    await _client.DeleteAsync(
        $"/api/v1/admin/shared-games/{sharedGameId}");
}
```

### Edge Case Tests

```csharp
[Fact]
[Trait("Category", "E2E")]
public async Task Agent_Chat_With_Incomplete_KB_Returns_Error()
{
    // Upload PDF but don't wait for completion
    // Chat with agent → expect "X of Y documents still processing" error
}

[Fact]
[Trait("Category", "E2E")]
public async Task Duplicate_PDF_Upload_Returns_409()
{
    // Upload same PDF twice (same ContentHash)
    // Second upload → 409 Conflict
}

[Fact]
[Trait("Category", "E2E")]
public async Task PDF_Failure_Generates_Error_Notification()
{
    // Upload corrupted PDF
    // Wait for failure
    // Verify ProcessingFailed notification exists
}

[Fact]
[Trait("Category", "E2E")]
public async Task Agent_Cannot_Be_Linked_Twice_To_Same_Game()
{
    // Link agent A to game
    // Try link agent B to same game → error "agent already linked"
}

[Fact]
[Trait("Category", "E2E")]
public async Task Multi_PDF_Same_Game_All_Searchable()
{
    // Upload 3 PDFs for same SharedGame
    // Wait for all Ready
    // Chat with agent → response should reference chunks from all 3
}
```

---

## 4. Architectural Flow

### 4.1 Cross-BC Event Flow

```
┌─────────────────────────┐
│   SharedGameCatalog BC  │
│                         │
│  Admin creates game     │
│  Admin uploads PDF ─────┼──────────────────────────────────────────┐
│  Admin approves doc     │                                          │
│  Admin links agent      │                                          │
└────────────┬────────────┘                                          │
             │                                                       │
             │ UploadSharedGamePdfCommand                            │
             │ (creates BOTH entities)                               │
             ▼                                                       │
┌─────────────────────────┐                                          │
│  DocumentProcessing BC  │                                          │
│                         │                                          │
│  PdfDocument            │◄─────────────────────────────────────────┘
│  ├─ SharedGameId        │        SharedGameDocument
│  ├─ GameId (or Empty)   │        ├─ PdfDocumentId (reference)
│  ├─ ProcessingState     │        ├─ ApprovalStatus
│  └─ ContentHash         │        └─ Version
│                         │
│  Pipeline (7-state):    │
│  Pending → Uploading    │
│  → Extracting           │ ◄── Unstructured/SmolDocling/Docnet
│  → Chunking             │ ◄── SemanticChunker
│  → Embedding            │ ◄── EmbeddingService (1536-dim)
│  → Indexing             │ ◄── QdrantService
│  → Ready                │
│                         │
│  Events:                │
│  PdfStateChangedEvent ──┼──────────────────────────────────┐
│                         │                                  │
└────────────┬────────────┘                                  │
             │                                               │
             │ VectorDocument created                        │
             ▼                                               │
┌─────────────────────────┐                                  │
│   KnowledgeBase BC      │                                  │
│                         │                                  │
│  VectorDocument         │                                  │
│  ├─ GameId              │                                  │
│  ├─ PdfDocumentId       │                                  │
│  ├─ TotalChunks         │                                  │
│  └─ SharedGameId        │                                  │
│                         │                                  │
│  Agent (linked)         │                                  │
│  ├─ GameId (⚠️ BUG-02) │                                  │
│  └─ AgentConfiguration  │                                  │
│                         │                                  │
│  RAG Pipeline:          │                                  │
│  Question → Embedding   │                                  │
│  → Qdrant Search        │                                  │
│  → Reranking (top-5)    │                                  │
│  → Prompt Assembly      │                                  │
│  → LLM Streaming (SSE)  │                                  │
│                         │                                  │
│  Events:                │                                  │
│  VectorDocumentReady ───┼──────────────────────────┐      │
│                         │                          │      │
└─────────────────────────┘                          │      │
                                                     │      │
                                                     ▼      ▼
                                          ┌─────────────────────────┐
                                          │  UserNotifications BC   │
                                          │                         │
                                          │  Handlers:              │
                                          │  ├─ PdfNotification     │
                                          │  │  (PdfStateChanged)   │
                                          │  └─ VectorDocReady      │
                                          │     (Integration Event) │
                                          │                         │
                                          │  Channels:              │
                                          │  ├─ In-App (SSE)        │
                                          │  ├─ Email (Queue)       │
                                          │  └─ Push (VAPID)        │
                                          │                         │
                                          │  Preferences:           │
                                          │  Per-channel toggles    │
                                          └─────────────────────────┘
```

### 4.2 Data Mapping: SharedGameId ↔ GameId ↔ Qdrant

```
                    ENTITÀ                          QDRANT
                    ══════                          ══════

SharedGame ─────┐
  Id: SGid-123  │
  AgentDefId    │
                │
                │  UploadSharedGamePdfCommand
                │
                ▼
PdfDocument ────────────────────────────►  Qdrant Collection
  Id: PDFid-456                            "meepleai_documents"
  SharedGameId: SGid-123                   ┌──────────────────┐
  GameId: Guid.Empty ⚠️                   │ game_id: "000..." │ ← BUG: Empty!
  ProcessingState: Ready                   │ pdf_id: "456..."  │
                │                          │ chunk_text: "..."  │
                │                          │ embedding: [...]   │
                ▼                          └──────────────────┘
VectorDocument
  Id: VDid-789
  GameId: Guid.Empty ⚠️
  PdfDocumentId: PDFid-456
  SharedGameId: SGid-123
  TotalChunks: 45

                    AGENT RAG SEARCH
                    ════════════════

Agent ──────────► RagPromptAssemblyService
  Id: AGid-101      │
  GameId: null ⚠️   │ Cerca con filter:
                    │ game_id = agentSession.GameId
                    │
                    ▼
              QdrantService.SearchAsync(
                gameId: ???,     ← NON SA QUALE USARE
                embedding: [...],
                limit: 15
              )
              → 0 risultati! ❌


                    FIX PROPOSTO
                    ════════════

1. UploadSharedGamePdfCmd: GameId = SharedGameId (non Guid.Empty)
2. LinkAgentToSharedGameCmd: Agent.GameId = SharedGameId
3. Qdrant game_id = SharedGameId
4. RAG search filter = Agent.GameId = SharedGameId
   → MATCH! ✅
```

### 4.3 Notification Pipeline

```
PdfDocument.TransitionTo(Ready)
        │
        ├──► PdfStateChangedEvent (Domain Event, same BC)
        │         │
        │         ▼
        │    PdfNotificationEventHandler
        │         │
        │         ├─► Check NotificationPreferences(userId)
        │         │
        │         ├─► [In-App] Notification.Create()
        │         │       → Save to DB
        │         │       → InMemoryBroadcaster.Publish()
        │         │       → SSE channel → client browser
        │         │
        │         ├─► [Email] if (prefs.EmailOnDocumentReady)
        │         │       → EnqueueEmailCommand
        │         │       → EmailQueueItem (Pending)
        │         │       → EmailProcessorJob (ogni 30s)
        │         │         ├─► Send SMTP
        │         │         ├─► Success → Sent
        │         │         └─► Failure → retry (1m, 5m, 30m)
        │         │                     → 3x fail → DeadLetter
        │         │
        │         └─► [Push] if (prefs.PushOnDocumentReady)
        │                 → IPushNotificationService
        │                 → VAPID Web Push API → browser
        │
        └──► IndexPdfCommandHandler (pipeline completion)
                  │
                  ▼
             VectorDocument.Create()
                  │
                  ▼
             VectorDocumentReadyIntegrationEvent (Cross-BC)
                  │
                  ▼
             VectorDocumentReadyNotificationHandler
                  │
                  ├─► [In-App] Notification (ProcessingJobCompleted)
                  │       link: "/library/games/{gameId}/agent"
                  │
                  ├─► [Email] if enabled
                  │
                  └─► [Push] if enabled
```

### 4.4 Admin Complete Journey

```
FASE 1: SETUP GIOCO
═══════════════════

Admin ──► POST /admin/shared-games
          { title: "Catan", ... }
          ◄── 201 { id: SGid, status: "Draft" }

Admin ──► POST /admin/shared-games/{id}/submit-for-approval
          ◄── 200 { status: "PendingApproval" }

Admin ──► POST /admin/shared-games/{id}/approve-publication
          ◄── 200 { status: "Published" }
          └──► SharedGamePublicationApprovedEvent
               └──► Audit trail

FASE 2: UPLOAD PDF
══════════════════

Admin ──► POST /admin/shared-games/{id}/documents
          Content-Type: multipart/form-data
          file: catan-rules.pdf
          documentType: Rulebook
          version: 1.0
          ◄── 200 { pdfDocumentId, sharedGameDocumentId, status: "processing" }
          └──► Crea PdfDocument + SharedGameDocument
          └──► Avvia pipeline in background

          ┌─ Pipeline automatico ──────────────────────────┐
          │ Pending → Uploading → Extracting → Chunking    │
          │ → Embedding → Indexing → Ready                  │
          │                                                 │
          │ SSE Progress: GET /pdfs/{id}/progress/stream    │
          │ data: { "percentage": 45, "state": "Chunking" } │
          │ data: { "percentage": 100, "state": "Ready" }   │
          └──── Al completamento ──────────────────────────┘
                    │
                    ├──► PdfStateChangedEvent → Notifica SSE
                    ├──► VectorDocument creato in Qdrant
                    └──► VectorDocumentReadyEvent → Notifica SSE

FASE 3: APPROVAZIONE RAG
═════════════════════════

Admin ──► POST /admin/shared-games/{id}/documents/{docId}/approve
          ◄── 200 { approvalStatus: "Approved" }
          └──► DocumentApprovedForRagEvent
               └──► (Handler mancante — BUG-03)

FASE 4: CONFIGURAZIONE AGENT
═════════════════════════════

Admin ──► POST /api/v1/agents
          { name: "Catan Tutor", type: "Tutor", gameId: SGid }
          ◄── 201 { id: AGid }

Admin ──► POST /admin/shared-games/{id}/link-agent/{AGid}
          ◄── 200
          └──► SharedGame.AgentDefinitionId = AGid
          └──► (Agent.GameId dovrebbe essere impostato — BUG-02)

Admin ──► PATCH /api/v1/agents/{AGid}/configuration
          { llmProvider: "OpenRouter", llmModel: "openai/gpt-4o-mini",
            temperature: 0.7, maxTokens: 4096 }
          ◄── 200

FASE 5: UTENTE CHATTA
══════════════════════

Utente ──► POST /api/v1/agents/{AGid}/chat
           { message: "Come si costruisce una strada?" }
           Accept: text/event-stream
           ◄── 200 text/event-stream

           data: { "type": "StateUpdate", "message": "Retrieving rules..." }
           data: { "type": "Token", "content": "Per" }
           data: { "type": "Token", "content": " costruire" }
           data: { "type": "Token", "content": " una strada" }
           ...
           data: { "type": "Complete", "usage": { "promptTokens": 850,
                   "completionTokens": 245 }, "cost": { "total": 0.003 } }

FASE 6: ADMIN MONITORA
═══════════════════════

Admin ──► GET /api/v1/notifications?isRead=false
          ◄── 200 [
            { type: "pdf_upload_completed", severity: "success",
              title: "PDF processato", link: "/admin/shared-games/{id}/documents" },
            { type: "processing_job_completed", severity: "success",
              title: "KB pronto", link: "/library/games/{id}/agent" }
          ]

Admin ──► GET /admin/pdfs/analytics/distribution
          ◄── 200 { ready: 1, failed: 0, processing: 0, pending: 0 }
```

---

## 5. Improvement Roadmap

### Immediate (Bug Fix) — Sprint Corrente

| # | Azione | Severità | File Principali |
|---|--------|----------|-----------------|
| 1 | Migrare KB check da `ProcessingStatus` a `ProcessingState` | 🔴 | `AskAgentQuestionCommandHandler.cs` |
| 2 | Impostare `Agent.GameId` in `LinkAgentToSharedGameCommandHandler` | 🔴 | `LinkAgentToSharedGameCommandHandler.cs`, `Agent.cs` |
| 3 | Usare `SharedGameId` come `game_id` in Qdrant per shared game PDFs | 🔴 | `UploadSharedGamePdfCommandHandler.cs` |

### Short-Term — Prossimo Sprint

| # | Azione | Severità |
|---|--------|----------|
| 4 | Creare handler per `DocumentApprovedForRagEvent` | 🟡 |
| 5 | Migrare tutti gli 8 file da `ProcessingStatus` a `ProcessingState` | 🟡 |
| 6 | Aggiungere E2E test per flusso completo | 🟡 |
| 7 | Definire comportamento degradazione KB (LimitedAI vs blocco) | 🟡 |

### Long-Term — Backlog

| # | Azione | Severità |
|---|--------|----------|
| 8 | Admin wizard per shared game + PDF + agent setup | 🟢 |
| 9 | Persistent notification queue (sostituire in-memory broadcaster) | 🟢 |
| 10 | DocumentCategory-aware RAG retrieval | 🟢 |
| 11 | SharedGameIdResolver service per mapping completo | 🟢 |
| 12 | Deprecare `ProcessingStatus` dalle API response (v2) | 🟢 |

---

## Appendice: File Reference

| Bounded Context | File Chiave | Scopo |
|-----------------|-------------|-------|
| SharedGameCatalog | `SharedGame.cs` | Aggregate root (959 righe) |
| SharedGameCatalog | `SharedGameDocument.cs` | Entity per documenti con approval |
| SharedGameCatalog | `SharedGameCatalogEndpoints.cs` | 60+ route definitions |
| SharedGameCatalog | `LinkAgentToSharedGameCommandHandler.cs` | Linking agent (BUG-02) |
| DocumentProcessing | `PdfDocument.cs` | PDF aggregate con 7-state pipeline |
| DocumentProcessing | `EnhancedPdfProcessingOrchestrator.cs` | 3-stage extractor |
| DocumentProcessing | `UploadSharedGamePdfCommandHandler.cs` | Upload per shared games |
| KnowledgeBase | `AskAgentQuestionCommandHandler.cs` | KB check + RAG (BUG-01) |
| KnowledgeBase | `RagPromptAssemblyService.cs` | RAG pipeline assembly |
| KnowledgeBase | `ChatWithSessionAgentCommandHandler.cs` | SSE streaming handler |
| UserNotifications | `PdfNotificationEventHandler.cs` | PDF state → notifiche |
| UserNotifications | `VectorDocumentReadyNotificationHandler.cs` | KB ready → notifiche |
| UserNotifications | `InMemoryUserNotificationBroadcaster.cs` | SSE channel broadcaster |
| Infrastructure | `QdrantService.cs` | Vector DB operations |
