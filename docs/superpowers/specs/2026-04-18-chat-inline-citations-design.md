# Chat Inline Citations & Concise Responses

**Date**: 2026-04-18
**Status**: Approved
**Author**: Claude + Aaron

## Problem

Chat responses are truncated when `max_tokens` budget is exhausted. Users cannot verify answers against the original rulebook. No link to PDF source pages.

## Solution

Three coordinated changes:
1. **Concise prompt** — instruct LLM to summarize, not rewrite. Response style switchable (`concise`/`detailed`)
2. **Inline citations** — backend matches LLM response text against RAG snippets, emits positioned citation events
3. **Continuation** — if response truncates, emit continuation token so user can request the rest

## SSE Event Flow (revised)

```
StateUpdate("Searching knowledge base...")
  → Citations (snippets with page, pdfId, text)
  → StateUpdate("Generating answer...")
  → Token* (streaming concise response)
  → Complete (final answer, usage, confidence, finishReason)
  → InlineCitation (array of positioned matches)
  → ContinuationAvailable (if truncated, with continuation token)
```

### New Event Types

Added to `StreamingEventType` enum:

- `InlineCitation = 28` — positioned snippet matches within the response text
- `ContinuationAvailable = 29` — response was truncated, continuation available

### InlineCitation Payload

```json
{
  "citations": [
    {
      "startOffset": 42,
      "endOffset": 128,
      "snippetIndex": 0,
      "pageNumber": 3,
      "pdfDocumentId": "uuid",
      "confidence": 0.9
    }
  ]
}
```

### ContinuationAvailable Payload

```json
{
  "continuationToken": "base64-encoded-context",
  "reason": "max_tokens_reached"
}
```

The `continuationToken` encodes: gameId, original query, partial response (for context), snippet offsets already used.

## Prompt System

### responseStyle parameter

`QaStreamRequest` gains optional `responseStyle: "concise" | "detailed"` (default `concise`).

**concise** system prompt addition:
> "Rispondi in modo sintetico e strutturato (max 200 parole). Concentrati sul rispondere alla domanda, non riscrivere il regolamento. Usa elenchi puntati. Non citare le pagine nel testo — le citazioni vengono gestite automaticamente dal sistema."

**detailed** system prompt addition:
> "Rispondi in modo completo e approfondito. Spiega ogni aspetto rilevante con esempi dal regolamento. Usa sottosezioni se necessario."

### Max tokens per style

- `concise`: 800 tokens (~200 words)
- `detailed`: 2000 tokens (with continuation fallback)

### Continuation mechanism

1. After Complete, backend checks `finish_reason` from LLM
2. If `length` (not `stop`), emits `ContinuationAvailable` with encoded context
3. Frontend shows "Continua la risposta →" button
4. Click calls `qaStream` with `continuationToken` in body
5. Backend reconstructs context, adds prompt: "Continua la risposta precedente da dove ti eri fermato. Non ripetere ciò che hai già detto."
6. New streamed response appends to same assistant message

## Inline Citation Matching

### Service: `InlineCitationMatcherService`

Location: `KnowledgeBase.Domain.Services`

**Algorithm**:
1. Receives final LLM response + RAG snippets list
2. For each snippet, extracts significant phrases (>8 words, excluding stop words)
3. For each phrase, searches response text with three strategies:
   - **Exact substring** (case-insensitive) → confidence 1.0
   - **Fuzzy match** (Levenshtein ≤ 15% of length) → confidence 0.8
   - **N-gram overlap** (3-gram, >60% match) → confidence 0.6
4. For each match: `{ startOffset, endOffset, snippetIndex, confidence }`
5. Overlapping matches: keep highest confidence
6. Minimum threshold: confidence ≥ 0.6

**Performance**: Operates on final string (200-800 words) × 5 snippets. Expected: <50ms. No external calls.

**Fallback**: If no snippet reaches inline threshold, all citations appear only in the block below the response.

### Integration in StreamQaQueryHandler

```csharp
yield Complete(...)
var inlineCitations = _citationMatcher.Match(finalAnswer, snippets);
if (inlineCitations.Any())
    yield InlineCitation(inlineCitations)
if (finishReason == "length")
    yield ContinuationAvailable(...)
```

## Frontend Components

### InlineCitationText

Renders response text with highlighted spans at citation positions.

Each highlighted span:
- Background `amber-500/10`, left border `amber-500`
- Hover tooltip: `"Pagina 3 — carcassonne_rulebook.pdf"`
- **Click on span** → expands accordion below with full RAG chunk text
- **`↗` icon** right of span → opens PDF in browser at page (`/api/v1/pdfs/{id}/download#page=N`)

### CitationBlock

For snippets without inline match (fallback). Chip row below the response:

```
📄 Pagina 3  📄 Pagina 5  📄 Pagina 12
```

Each chip:
- Click → expands accordion with chunk text
- `↗` icon → opens PDF at page

### ContinueButton

Shown when `ContinuationAvailable` event received:

```
[Continua la risposta →]
```

Click calls `qaStream` with `continuationToken`. New response appends to same assistant message.

### Shared components

`InlineCitationText`, `CitationBlock`, `ContinueButton` live in `components/chat-unified/` and are used by both `ChatMobile` and `ChatThreadView`.

## Backend Changes

| File | Change |
|------|--------|
| `Models/Contracts.cs` | Add `InlineCitation = 28`, `ContinuationAvailable = 29` to enum + `StreamingInlineCitation`, `StreamingContinuation` records |
| `StreamQaQueryHandler.cs` | After Complete: call matcher, emit new events, handle `continuationToken` input |
| `HybridLlmService.cs` | Expose `finish_reason` in final `LlmStreamChunk` |
| `AiEndpoints.cs` | Accept `responseStyle` and `continuationToken` in `QaRequest`, pass to prompt |
| `PromptTemplateService.cs` | Add template variants for `concise`/`detailed`/`continuation` |
| New: `InlineCitationMatcherService.cs` | Text-snippet matching logic |

## Frontend Changes

| File | Change |
|------|--------|
| `chatClient.ts` | Add `responseStyle`, `continuationToken` to `QaStreamRequest`, new event types 28/29 |
| New: `InlineCitationText.tsx` | Text with highlighted citation spans + accordion |
| New: `CitationBlock.tsx` | Chip citations below response |
| New: `ContinueButton.tsx` | "Continue response" button |
| `ChatMobile.tsx` | Integrate new components, handle events 28/29, pass `responseStyle` |
| `ChatThreadView.tsx` | Same for desktop |
| `ChatMessageList.tsx` | Use `InlineCitationText` for assistant messages |

## No DB Changes

All required data (page number, pdfDocumentId, chunk text) is already available in RAG snippets. No schema migration needed.

## Testing Strategy

- **Unit**: `InlineCitationMatcherService` — exact match, fuzzy match, n-gram, overlap resolution, empty input
- **Unit**: Prompt template variants — concise/detailed/continuation
- **Integration**: Full SSE flow emitting all new event types
- **Frontend**: `InlineCitationText` renders highlights at correct positions, accordion expand/collapse, PDF link generation
- **E2E**: Send message → verify citations appear → click accordion → click PDF link
