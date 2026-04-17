# Session Vision AI — Design Spec

**Date**: 2026-04-17
**Status**: Draft
**Scope**: Integrare immagini caricate dall'utente durante sessioni di gioco per permettere all'agente AI di capire lo stato della partita tramite vision API.

## Overview

L'utente può fornire immagini all'agente AI in due modalità:
1. **Inline chat** — allega 1+ immagini a un messaggio nella chat (effimere, non persistite)
2. **Snapshot sessione** — carica foto come "stato corrente della partita" in un'area dedicata (persistenti in storage)

L'agente usa modelli vision multimodali per:
- **Descrizione libera** — rispondere nel contesto di ciò che vede nell'immagine
- **Estrazione GameState strutturata** — estrarre dati di gioco (risorse, punteggi, posizioni) in JSON, best-effort

## Decisioni Architetturali

| Decisione | Scelta | Motivazione |
|-----------|--------|-------------|
| Flusso | Ibrido (inline chat + snapshot sessione) | Massima flessibilità per l'utente |
| Analisi agente | Descrizione libera + GameState strutturato | Best-effort: non bloccante se l'extraction fallisce |
| Provider | DeepSeek + OpenRouter | Routing esistente, accesso ai migliori modelli vision |
| Limiti | Configurabili per tier utente | Segue pattern AgentTierLimits esistente |
| Persistenza | Inline effimere, snapshot persistenti | Minimizza storage, snapshot hanno valore duraturo |
| Extraction timing | Lazy (on-demand) | Zero costo LLM se l'utente non interagisce con l'agente |
| Approccio architetturale | ILlmClient esteso + ImagePreprocessor | 1 chiamata LLM quando possibile, fallback testuale per provider solo-testo |

## Data Model

### ContentPart — Messaggi Multimodali

```csharp
public abstract record ContentPart;
public record TextContentPart(string Text) : ContentPart;
public record ImageContentPart(string Base64Data, string MediaType) : ContentPart;

public record LlmMessage(string Role, List<ContentPart> Content);
```

Segue lo standard OpenAI content parts, compatibile con DeepSeek e OpenRouter.

### ILlmClient — Estensione Interfaccia

```csharp
public interface ILlmClient
{
    // Esistenti (backward compat)
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string model, string systemPrompt, string userPrompt,
        float temperature, int maxTokens, CancellationToken ct = default);

    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model, string systemPrompt, string userPrompt,
        float temperature, int maxTokens, CancellationToken ct = default);

    // Nuovi: multimodale
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string model, List<LlmMessage> messages,
        float temperature, int maxTokens, CancellationToken ct = default);

    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model, List<LlmMessage> messages,
        float temperature, int maxTokens, CancellationToken ct = default);

    bool SupportsVision { get; }
    bool SupportsModel(string modelId);
    Task<bool> CheckHealthAsync(CancellationToken ct = default);
}
```

### IImagePreprocessor

```csharp
public interface IImagePreprocessor
{
    Task<ProcessedImage> ProcessAsync(
        byte[] imageData, string mediaType, ImageProcessingOptions options);

    Task<string> DescribeImageAsync(byte[] imageData, string mediaType);
}

public record ProcessedImage(byte[] Data, string MediaType, int Width, int Height, long SizeBytes);

public record ImageProcessingOptions(
    int MaxWidth = 1024,
    int MaxHeight = 1024,
    long MaxSizeBytes = 5_000_000,
    bool ConvertToJpeg = false);
```

`DescribeImageAsync` è il fallback: chiama il provider vision con il ranking più alto disponibile (anche se non è il provider primario scelto dalla strategia) per generare una descrizione testuale, che viene poi iniettata nel prompt del provider solo-testo primario. Se nessun provider vision è raggiungibile, l'immagine viene ignorata e l'agente risponde solo con contesto testuale, avvisando l'utente.

### SessionSnapshot — Entità (SessionTracking BC)

```csharp
public class SessionSnapshot : AuditableEntity
{
    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public int TurnNumber { get; private set; }
    public string? Caption { get; private set; }
    public List<SnapshotImage> Images { get; private set; }
    public string? ExtractedGameState { get; private set; }  // JSON, nullable

    public static SessionSnapshot Create(
        Guid sessionId, Guid userId, int turnNumber, string? caption);
    public void AddImage(string storageKey, string mediaType, int width, int height);
    public void UpdateGameState(string gameStateJson);
}

public class SnapshotImage : Entity
{
    public Guid Id { get; private set; }
    public string StorageKey { get; private set; }
    public string MediaType { get; private set; }
    public int Width { get; private set; }
    public int Height { get; private set; }
    public int OrderIndex { get; private set; }
}
```

### VisionTierLimits

```csharp
public record VisionTierLimits(
    int MaxImagesPerMessage,
    int MaxSnapshotsPerSession,
    int MaxImagesPerSnapshot,
    int MaxImageResolution,
    bool GameStateExtractionEnabled);
```

| Tier | ImagesPerMsg | SnapshotsPerSession | ImagesPerSnapshot | MaxResolution | GameStateExtraction |
|------|-------------|--------------------|--------------------|---------------|---------------------|
| Alpha | 5 | 20 | 5 | 2048px | ON |
| Free | 2 | 5 | 3 | 1024px | OFF |
| Premium | 5 | 30 | 10 | 2048px | ON |

## Flusso Inline Chat (Immagini Effimere)

### Command

```csharp
public record AskSessionAgentCommand(
    Guid SessionId,
    Guid SenderId,
    string Question,
    int? TurnNumber,
    List<ChatImageAttachment>? Images
) : IRequest<AskSessionAgentResult>;

public record ChatImageAttachment(byte[] Data, string MediaType, string? FileName);
```

### Endpoint

```csharp
app.MapPost("/api/v1/live-sessions/{sessionId}/agent/ask",
    async (Guid sessionId, [FromForm] AskAgentWithImagesRequest request,
           IMediator mediator) => ...);
```

Multipart form: `question` (string) + `turnNumber` (int?) + `images` (IFormFile[]).

### Execution Flow

```
Utente allega immagini + domanda
    │
    ▼
1. Valida tier limits (MaxImagesPerMessage)
2. IImagePreprocessor.ProcessAsync() per ogni immagine (resize/optimize)
3. Recupera ultimo GameState da snapshot (se presente) come contesto
4. Assembla List<LlmMessage> con ContentPart (testo + immagini)
5. LlmProviderSelector sceglie provider con SupportsVision=true
6. Se nessun provider vision → DescribeImageAsync() fallback testuale
7. ILlmClient.GenerateCompletionStreamAsync(messages)
8. Salva risposta come SessionChatMessage (tipo AgentResponse)
    │
    ▼
Immagini scartate — solo la risposta persiste
```

### Prompt Assembly

```
SYSTEM: {agent_prompt_template}
        {rag_chunks_from_rulebook}
        {latest_extracted_game_state_if_available}

USER:   [Image1: base64] [Image2: base64]
        "Ecco il tavolo, cosa mi consigli per il prossimo turno?"
```

## Flusso Snapshot Sessione (Immagini Persistenti)

### Commands & Queries

```csharp
public record CreateSessionSnapshotCommand(
    Guid SessionId, Guid UserId, int TurnNumber,
    string? Caption, List<SnapshotImageUpload> Images
) : IRequest<CreateSnapshotResult>;

public record SnapshotImageUpload(byte[] Data, string MediaType, string? FileName);

public record GetLatestGameStateQuery(Guid SessionId)
    : IRequest<GameStateResult?>;

public record GetSessionSnapshotsQuery(Guid SessionId)
    : IRequest<List<SessionSnapshotDto>>;
```

### Endpoints

```csharp
app.MapPost("/api/v1/live-sessions/{sessionId}/snapshots", ...);   // upload
app.MapGet("/api/v1/live-sessions/{sessionId}/snapshots", ...);    // list
app.MapGet("/api/v1/live-sessions/{sessionId}/game-state", ...);   // latest state
```

### Lazy Extraction

L'extraction del GameState avviene on-demand, alla prima domanda dell'utente dopo un upload:

```
Utente carica snapshot → immagini salvate in storage → DONE (zero costi LLM)
    ...
Utente chiede all'agente "Come sto messo?"
    │
    ▼
Handler: c'è GameState estratto per l'ultimo snapshot?
    │
    ├─ SÌ → usa GameState nel system prompt
    └─ NO → chiama vision LLM sulle immagini snapshot
             salva ExtractedGameState nel DB
             procede con la risposta
             (+3-8s latenza sulla prima domanda)
             Chiamate successive: GameState cachato
```

L'extraction è idempotente — non si rifa se già estratta. Si ricalcola solo con nuovo snapshot.

### GameState Extraction Prompt

```
SYSTEM: Sei un analizzatore di immagini di giochi da tavolo.
        Analizza le immagini e restituisci un JSON strutturato.
        Il gioco è: {gameName}. Regole note: {ragContext}.

USER:   [Image1] [Image2] [Image3]
        Estrai lo stato del gioco da queste foto.
        Restituisci SOLO JSON valido nel formato:
        {
          "turn_estimate": number | null,
          "players": [{ "position": string, "resources": {}, "score": number | null }],
          "board_description": string,
          "notable_state": string[],
          "confidence": number  // 0.0 - 1.0
        }
```

GameState con `confidence < 0.4` non viene incluso nel system prompt dell'agente.

### Storage Structure

```
snapshots/{sessionId}/{snapshotId}/img_0.jpg
snapshots/{sessionId}/{snapshotId}/img_1.jpg
```

Usa il pattern `IStorageProvider` esistente (S3/local via factory).

## Provider Vision & Fallback

### Provider Support

| Provider | SupportsVision | Modelli Vision |
|----------|---------------|----------------|
| DeepSeek | `true` | deepseek-chat (OpenAI-compatible) |
| OpenRouter | `true` | claude-sonnet-4, gpt-4o, gemini-2.5-pro |
| Ollama | `false` | modelli locali tipicamente solo-testo |

### Fallback Chain

```
Richiesta con immagini
    │
    ▼
Provider con SupportsVision=true disponibile?
    │
    ├─ SÌ → content parts nativi (1 chiamata LLM)
    └─ NO → DescribeImageAsync via provider vision
             → descrizione testuale iniettata nel prompt
             → invio a provider solo-testo (2 chiamate LLM)
```

### LlmProviderSelector Esteso

```csharp
public class LlmProviderSelector : ILlmProviderSelector
{
    public async Task<ILlmClient> SelectProviderAsync(LlmRoutingContext context)
    {
        var candidates = _providers
            .Where(p => p.IsHealthy)
            .Where(p => !context.RequiresVision || p.SupportsVision)
            .OrderBy(p => ScoreProvider(p, context.Strategy));

        if (!candidates.Any() && context.RequiresVision)
            return SelectWithTextFallback(context);

        return candidates.First();
    }
}
```

## Frontend

### Componenti

| Componente | Path | Descrizione |
|-----------|------|-------------|
| ChatInputBar | `components/chat/panel/ChatInputBar.tsx` | Esteso: bottone 🖼️, preview immagini, rimozione |
| ChatMessageBubble | `components/chat/panel/ChatMessageBubble.tsx` | Esteso: render thumbnail immagini nel messaggio |
| SessionSnapshotPanel | `components/session/SessionSnapshotPanel.tsx` | NUOVO: area snapshot con timeline per turno |
| SnapshotCard | `components/session/SnapshotCard.tsx` | NUOVO: card singolo snapshot con badge stato |
| SnapshotUploadDialog | `components/session/SnapshotUploadDialog.tsx` | NUOVO: dialog upload con caption e turn number |
| GameStateDisplay | `components/session/GameStateDisplay.tsx` | NUOVO: visualizzazione GameState JSON leggibile |

### Hooks

```typescript
// Inline chat images (effimere)
useChatImageAttachments()
  → addImage(file) / removeImage(index) / clearImages()
  → images: ChatImagePreview[]

// Snapshot sessione
useSessionSnapshots(sessionId)
  → snapshots, isLoading, createSnapshot(files, caption, turnNumber)

// Game state
useLatestGameState(sessionId)
  → gameState, isAnalyzed, isLoading
```

### UX Details

- Bottone 🖼️ separato dal 📎 (PDF) nella ChatInputBar
- Preview thumbnail con X per rimuovere prima dell'invio
- Immagini nel bubble messaggio: thumbnail cliccabili per full-size
- Snapshot panel: timeline per turno, badge "Analizzato" / "Non analizzato"
- Mobile: picker nativo (fotocamera + galleria), HEIC → JPEG server-side

## Error Handling

### Upload Errors

| Scenario | Handling |
|----------|----------|
| File non immagine | Validazione client + server: 400 Bad Request |
| Immagine troppo grande | Reject: "Immagine max {X}MB per il tuo piano" |
| Troppe immagini | Reject: "Max {N} immagini per messaggio" |
| Upload network failure | Retry client-side (max 2), poi toast errore |
| HEIC/WebP | Server converte a JPEG via preprocessor |

### Vision Errors

| Scenario | Handling |
|----------|----------|
| Nessun provider vision | Fallback testuale automatico |
| Vision API timeout | Retry 1x, poi risposta senza contesto visivo + avviso |
| GameState extraction fallisce | Snapshot valido con solo immagini, `ExtractedGameState = null` |
| Confidence < 0.4 | GameState escluso dal system prompt, log per monitoring |
| Immagine non è un gioco | Agente risponde "L'immagine non sembra un gioco da tavolo" |

### Concorrenza

| Scenario | Handling |
|----------|----------|
| 2 utenti caricano snapshot simultaneamente | Snapshot indipendenti, nessun conflitto |
| Lazy extraction doppia | Optimistic lock: primo completa, secondo riusa |
| Upload durante chat streaming | Endpoint indipendenti, nessun blocco |

## Testing Strategy

### Backend Unit Tests
- ContentPart serialization/deserialization
- ImagePreprocessor: resize, HEIC conversion, size limits
- VisionTierLimits validation per ogni tier
- GameState extraction prompt building
- Provider selection con/senza SupportsVision
- Fallback chain: vision → text description

### Backend Integration Tests
- Snapshot CRUD con storage reale (Testcontainers)
- AskSessionAgentCommand con immagini mock
- Lazy extraction trigger e caching
- Tier limits enforcement

### Frontend Unit Tests
- useChatImageAttachments: add/remove/clear
- ChatInputBar: render bottone 🖼️, preview, invio multipart
- ChatMessageBubble: render immagini inline
- SnapshotCard: badge stato, GameState display

### Frontend E2E (Playwright)
- Upload immagine inline e ricezione risposta agente
- Upload snapshot, verifica persistenza, verifica badge
- Tier limits: verificare reject quando superati
- Mobile: fotocamera picker, HEIC handling

## Migration

```sql
-- Nuova tabella SessionSnapshots
CREATE TABLE session_snapshots (
    id UUID PRIMARY KEY,
    session_id UUID NOT NULL REFERENCES sessions(id),
    user_id UUID NOT NULL,
    turn_number INT NOT NULL,
    caption TEXT,
    extracted_game_state JSONB,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ,
    created_by UUID,
    updated_by UUID,
    is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at TIMESTAMPTZ
);

-- Nuova tabella SnapshotImages
CREATE TABLE snapshot_images (
    id UUID PRIMARY KEY,
    snapshot_id UUID NOT NULL REFERENCES session_snapshots(id) ON DELETE CASCADE,
    storage_key TEXT NOT NULL,
    media_type VARCHAR(50) NOT NULL,
    width INT NOT NULL,
    height INT NOT NULL,
    order_index INT NOT NULL DEFAULT 0
);

CREATE INDEX ix_session_snapshots_session_id ON session_snapshots(session_id);
CREATE INDEX ix_session_snapshots_session_turn ON session_snapshots(session_id, turn_number);
CREATE INDEX ix_snapshot_images_snapshot_id ON snapshot_images(snapshot_id);
```

## Out of Scope

- Video upload / streaming
- Real-time board tracking (continuous vision)
- Collaborative snapshot editing tra partecipanti
- OCR specifico per testi su carte/componenti (potrebbe essere un follow-up)
- Training/fine-tuning modelli su giochi specifici
