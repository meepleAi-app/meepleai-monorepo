# RAG Copyright-Aware KB Cards in Chat

**Date**: 2026-03-21
**Status**: Draft
**Scope**: Copyright-tiered references in AI agent chat responses

## Problem

When the AI agent answers using RAG-retrieved content from publisher PDFs (rulebooks, expansions), the system must respect copyright. Users who own the game and uploaded the rulebook should see full verbatim citations with PDF access. Users accessing shared catalog content without ownership should see AI-paraphrased references only.

## Decision Summary

| Decision | Choice | Rationale |
|----------|--------|-----------|
| Where paraphrasing happens | LLM-side (system prompt) | Simplest, no extra service, LLM already generates the response |
| How KB Cards render | RuleSourceCard evolves internally | Preserves existing UX (collapsible, chips, keyboard nav) |
| Where tier is determined | Runtime calculation by `CopyrightTierResolver` | Tier depends on user ownership (changes over time) |
| Where tier is stored | Persisted in `CitationsJson` on ChatMessage | Historical accuracy — reflects what LLM actually generated |
| Copyright scope | Rulebook, Expansion, and Errata PDFs | Publisher-distributed content is protected; QuickStart, Reference, PlayerAid, Other are not |
| BC isolation | Read-only projection pattern (Phase 6) | `ICopyrightDataProjection` in KB BC, consistent with existing `IUserProjection` pattern |

## Architecture

### Copyright Tier Model

Two tiers determine what the user sees:

| Tier | Condition | User sees |
|------|-----------|-----------|
| **Full** | Content is `creative_commons` or `public_domain` | Verbatim quote, PDF page screenshot, "Vedi nel PDF" button |
| **Full** | Document is NOT Rulebook/Expansion/Errata (QuickStart, Reference, PlayerAid, Other) | Same as above |
| **Full** | User uploaded the PDF AND owns the game (`OwnershipDeclaredAt != null`) | Same as above |
| **Protected** | Rulebook/Expansion/Errata, user did not upload or does not own | AI-paraphrased text, page number, "Regolamento ufficiale" link, upsell CTA |

### Determination Logic

```
CopyrightTier Resolve(chunk, userId):
  pdf = lookup PdfDocument by chunk.DocumentId
  gameId = pdf.GameId ?? pdf.PrivateGameId

  IF pdf.LicenseType == LicenseType.CreativeCommons OR LicenseType.PublicDomain
    → Full
  ELIF pdf.DocumentCategory NOT IN (Rulebook, Expansion, Errata)
    → Full
  ELIF pdf.UploadedByUserId == userId
    AND UserLibraryEntry(userId, gameId).OwnershipDeclaredAt != null
    → Full
  ELSE
    → Protected
```

**Note**: `gameId` is resolved per-chunk from the PDF document, not passed as a single parameter. This handles expansion PDFs that may belong to different games in the user's library.

## Data Model Changes

### PdfDocumentEntity — New Field

```csharp
public LicenseType LicenseType { get; private set; } = LicenseType.Copyrighted;
```

### LicenseType Enum (NEW)

```csharp
// DocumentProcessing/Domain/Enums/LicenseType.cs
public enum LicenseType
{
    Copyrighted = 0,      // Default — publisher-owned
    CreativeCommons = 1,   // CC license
    PublicDomain = 2       // No copyright restrictions
}
```

```sql
ALTER TABLE pdf_documents
ADD COLUMN license_type INTEGER NOT NULL DEFAULT 0;
-- 0 = Copyrighted, 1 = CreativeCommons, 2 = PublicDomain
```

Default `Copyrighted` = safe by default. Existing PDFs are treated as protected until classified. Stored as integer consistent with `DocumentCategory` enum pattern.

### ChunkCitation — Extended

```csharp
public record ChunkCitation(
    string DocumentId,
    int PageNumber,
    float RelevanceScore,
    string SnippetPreview,
    CopyrightTier CopyrightTier = CopyrightTier.Protected,  // NEW — default safe
    string? ParaphrasedSnippet = null                        // NEW — populated for Protected only
);

public enum CopyrightTier { Full, Protected }
```

New fields use default values to maintain backward compatibility with existing call sites (6 files currently construct `ChunkCitation` with 4 positional args).

`CopyrightTier` is **computed at runtime** (not stored on the document) because it depends on the requesting user's ownership status. It **is persisted** in `ChatMessage.CitationsJson` for historical accuracy.

### CitationsJson Format

```json
[
  {
    "documentId": "abc-123",
    "pageNumber": 14,
    "relevanceScore": 0.92,
    "snippetPreview": "Durante la fase di costruzione...",
    "copyrightTier": "protected",
    "paraphrasedSnippet": "Nella fase dedicata alla costruzione..."
  }
]
```

### Prerequisite: CitationsJson Persistence

**Current gap**: `ChatWithSessionAgentCommandHandler` (line 267) calls `AddAssistantMessageWithMetadata` but does NOT persist `CitationsJson`. This spec requires fixing that baseline gap: the handler must pass the assembled citations to the persistence call. This is a prerequisite, not a new design choice.

## Backend Pipeline

### Integration Point

In `ChatWithSessionAgentCommandHandler`, after RAG retrieval and before LLM streaming:

```
1. RAG retrieval (existing) → chunks with ChunkCitation
2. CopyrightTierResolver.ResolveAsync(chunks, userId)
   - Batch lookup PdfDocuments (DocumentId → LicenseType, DocumentCategory, UploadedByUserId, GameId)
   - Resolve gameId per-chunk from PdfDocument
   - Batch lookup UserLibraryEntries for all unique (userId, gameId) pairs
   - Apply rules → CopyrightTier per chunk
3. System prompt injection with annotation [Copyright: FULL|PROTECTED]
4. LLM streaming (existing)
5. Post-stream: extract paraphrasedSnippet from response text for PROTECTED chunks
6. Persist ChatMessage with CitationsJson (fix existing gap + new fields)
```

### Bounded Context Isolation: Read-Only Projection

`CopyrightTierResolver` lives in the **KnowledgeBase** BC. It cannot directly depend on `IPdfDocumentRepository` (internal to DocumentProcessing BC) or `IUserLibraryRepository` (internal to UserLibrary BC).

Following the **Phase 6 projection pattern** (commit `ec27583f3`), we introduce:

```csharp
// KnowledgeBase/Domain/Projections/ICopyrightDataProjection.cs
public interface ICopyrightDataProjection
{
    /// Returns copyright-relevant metadata for a batch of PDF documents.
    Task<IReadOnlyDictionary<string, PdfCopyrightInfo>> GetPdfCopyrightInfoAsync(
        IReadOnlyList<string> documentIds,
        CancellationToken ct);

    /// Checks ownership for a user across multiple game IDs.
    Task<IReadOnlyDictionary<Guid, bool>> CheckOwnershipAsync(
        Guid userId,
        IReadOnlyList<Guid> gameIds,
        CancellationToken ct);
}

public record PdfCopyrightInfo(
    string DocumentId,
    LicenseType LicenseType,
    DocumentCategory DocumentCategory,
    Guid UploadedByUserId,
    Guid? GameId,
    Guid? PrivateGameId,
    bool IsPublic
);
```

The **Infrastructure layer** implements this projection by querying both BCs' tables directly (read-only, no domain logic). This is the same pattern used for `IUserProjection` in Phase 6.

**Implementation note**: `PdfDocumentEntity.DocumentCategory` is stored as a string column. The projection must use `Enum.TryParse<DocumentCategory>()` with fallback to `DocumentCategory.Other` for any unparseable value. `LicenseType` (new integer column) uses safe integer-to-enum cast with fallback to `LicenseType.Copyrighted`.

### New Service: ICopyrightTierResolver

```csharp
// KnowledgeBase/Application/Services/ICopyrightTierResolver.cs
public interface ICopyrightTierResolver
{
    Task<IReadOnlyList<ChunkCitation>> ResolveAsync(
        IReadOnlyList<ChunkCitation> citations,
        Guid userId,
        CancellationToken ct);
}
```

No `gameId` parameter — the resolver extracts `gameId` per-chunk from the projection data. This correctly handles chunks from expansion PDFs belonging to different games.

**Implementation note**: The resolver combines TWO data sources to evaluate the Full tier condition: `PdfCopyrightInfo.UploadedByUserId == userId` (from the PDF projection) AND `CheckOwnershipAsync(userId, gameId) == true` (from the ownership projection). Both must be true simultaneously for user-uploaded copyrighted content to qualify as Full.

Dependencies:
- `ICopyrightDataProjection` (NEW — in KnowledgeBase BC)

### System Prompt Injection

Chunks are passed to LLM with copyright annotation:

```
[Source: Document {pdfId}, Page {pageNum}, Relevance: {score:F2}, Copyright: PROTECTED]
{chunk_text}
---
```

System prompt instruction added (localized to agent/document language):
> "Per le fonti marcate PROTECTED, riformula il contenuto con parole tue senza citare verbatim. Usa il marker [ref:documentId:pageNum] prima di ogni riformulazione. Per le fonti FULL, puoi citare direttamente."

**Language policy**: The copyright instruction must be localized to match the agent's configured language (from `AgentSession.Language` or `PdfDocument.Language`). The Italian example above is for Italian agents. English equivalent: "For sources marked PROTECTED, paraphrase in your own words without verbatim citation. Use the marker [ref:documentId:pageNum] before each paraphrase. For FULL sources, you may cite directly."

### Paraphrased Snippet Extraction

After LLM stream completes, for each PROTECTED chunk:

1. Search response text for `[ref:documentId:pageNum]` marker
2. Extract the text following the marker until next marker or paragraph break
3. Validate extracted text is NOT a verbatim substring of the original chunk (similarity check)
4. Store as `paraphrasedSnippet` on the citation

**Fallback if marker not found or validation fails**: show only page reference with no snippet text. Do NOT use any portion of the original `snippetPreview` — that would be a copyright leak.

**Security**: The extraction logic must filter out markers that appear in the user's input message to prevent prompt injection attacks that forge fake `[ref:...]` markers.

## Frontend Changes

### RuleSourceCard Props

```typescript
export interface RuleSourceCardProps {
  citations: Citation[];
  gameTitle?: string;
  publisherUrl?: string;
  mode?: AppMode;
  className?: string;
  // NEW
  gameImageUrl?: string;
  documentCategory?: string;
}
```

**Removed card-level `copyrightTier`** — tier is per-citation, not per-card. The card renders each citation according to its own tier.

### Citation Type Extended

```typescript
interface Citation {
  id: string;
  label: string;
  page?: number;
  source?: string;
  // NEW
  copyrightTier: 'full' | 'protected';
  snippet: string;
  paraphrasedSnippet?: string;
}
```

### Mixed-Tier Card Handling

A single `RuleSourceCard` may contain citations with different tiers (e.g., p.14 from a copyrighted rulebook = Protected, p.22 from a CC-licensed FAQ = Full). Each citation renders independently according to its own `copyrightTier`:

- Citation chips use **teal** or **amber** border based on individual tier
- Quote block renders verbatim or paraphrased per the active citation's tier
- "Vedi nel PDF" button appears only when the active citation is Full
- Upsell CTA appears only when the active citation is Protected

The card header shows a combined indicator: if ANY citation is Protected, a small 🔒 icon appears in the header alongside the source count.

### Conditional Rendering (per active citation)

**Tier Full** (teal accent):
- Quote border: `border-l-[hsl(174,60%,40%)]` (KB teal)
- Quote label: "Citazione originale"
- Quote text: `citation.snippet` verbatim, italic
- Page preview: PDF page thumbnail/screenshot
- Actions: "Vedi nel PDF" (primary teal button) + "Regolamento ufficiale" (secondary)

**Tier Protected** (amber accent):
- Quote border: `border-l-amber-500`
- Quote label: "Riformulazione AI"
- Quote text: `citation.paraphrasedSnippet` if available, otherwise "Vedi pagina {n} del regolamento" (no snippet)
- Page preview: **hidden**
- Actions: "Regolamento ufficiale" only + upsell CTA
- Upsell CTA logic:
  - If PDF is public (admin-uploaded, `IsPublic = true`): show "Dichiara possesso per accesso completo" (ownership declaration, not upload)
  - If PDF is user-uploadable: show "Carica il regolamento per la versione completa"

### SSE Complete Event

The `StreamingComplete` record in `Contracts.cs` gains a new optional field:

```csharp
// Add with default to avoid breaking existing 8+ call sites
public record StreamingComplete(
    // ... existing 10 fields ...
    IReadOnlyList<CitationDto>? Citations = null  // NEW — optional, default null
);
```

All existing call sites pass `null` (no change needed). Only the handlers that assemble RAG citations populate this field.

Frontend SSE parser updated to read `citations` from Complete event when present.

```typescript
{
  totalTokens: number;
  confidence: number;
  citations?: Citation[];  // optional — only present when RAG was used
}
```

## Visual Design

### Tier Full — Teal Accent

```
┌─────────────────────────────────────────┐
│ ▸ 📖 2 fonti dal regolamento di Catan   │  ← collapsible header
├─────────────────────────────────────────┤
│ [p.14 92%] [p.22 78%]                  │  ← citation chips (teal)
│                                         │
│ ┌─────────────────────────────────┐     │
│ │ 📄 Pagina 14    [screenshot]    │     │  ← PDF page preview
│ └─────────────────────────────────┘     │
│                                         │
│ █ CITAZIONE ORIGINALE                   │  ← teal left border
│ █ "Durante la fase di costruzione,      │
│ █  ogni giocatore può piazzare..."      │
│ █ — Regolamento, p.14                   │
│                                         │
│ [📄 Vedi nel PDF]  [🔗 Reg. ufficiale] │
│ ▰▰▰▰▰▰▰▰▰░ 92%                        │
└─────────────────────────────────────────┘
```

### Tier Protected — Amber Accent

```
┌─────────────────────────────────────────┐
│ ▸ 📖 🔒 2 fonti dal regolamento di ... │  ← 🔒 if any Protected
├─────────────────────────────────────────┤
│ [p.14 92%] [p.22 78%]                  │  ← chips (amber)
│                                         │
│ █ RIFORMULAZIONE AI                     │  ← amber left border
│ █ Nella fase dedicata alla costruzione, │
│ █ i giocatori hanno la possibilità di   │
│ █ posizionare nuovi insediamenti...     │
│ █ — Regolamento, p.14                   │
│                                         │
│ [🔗 Reg. ufficiale]                    │
│ 🔓 Carica il regolamento per la        │
│    versione completa                    │
│ ▰▰▰▰▰▰▰▰▰░ 92%                        │
└─────────────────────────────────────────┘
```

## Files Touched

| Layer | File | Change |
|-------|------|--------|
| Domain | `DocumentProcessing/.../LicenseType.cs` (NEW) | Enum: Copyrighted, CreativeCommons, PublicDomain |
| Domain | `PdfDocumentEntity` | + `LicenseType` field |
| Domain | `ChunkCitation` record | + `CopyrightTier`, `ParaphrasedSnippet` (with defaults) |
| Domain | `CopyrightTier` enum (NEW) | `Full`, `Protected` |
| Domain | `KnowledgeBase/.../ICopyrightDataProjection.cs` (NEW) | Read-only projection interface |
| Application | `ICopyrightTierResolver` (NEW) | Interface in KnowledgeBase BC |
| Application | `CopyrightTierResolver` (NEW) | Implementation in KnowledgeBase BC |
| Application | `RagPromptAssemblyService` | + copyright annotation in prompt |
| Application | `ChatWithSessionAgentCommandHandler` | + resolve tier, + extract paraphrase, + persist CitationsJson |
| Infrastructure | `CopyrightDataProjection` (NEW) | Implements projection, queries across BCs |
| Infrastructure | EF Migration | + `license_type` column |
| Contracts | `StreamingComplete` record | + optional `Citations` field (with default) |
| Frontend | `RuleSourceCard.tsx` | Per-citation conditional rendering full/protected |
| Frontend | `types.ts` (Citation) | + `copyrightTier`, `paraphrasedSnippet` |
| Frontend | SSE parser | + read `citations` from Complete event |

## Out of Scope

- MeepleCard cover redesign (4-corner layout) — separate spec
- Multi-label stack on cover — separate spec
- Upload flow LicenseType selection (admin manages, default `copyrighted`)
- Bulk re-classification of existing PDFs
- PDF page screenshot/thumbnail rendering infrastructure

## Dependencies

- `ICopyrightDataProjection` (NEW — replaces direct BC cross-references)
- `PdfPageModal` component (existing — used in Full tier only)

## Testing Strategy

- **Unit**: `CopyrightTierResolver` — all rule combinations:
  - LicenseType (Copyrighted × CC × PublicDomain) × DocumentCategory (Rulebook × Expansion × Errata × Other) × Ownership (owned+uploaded × owned+not-uploaded × not-owned)
  - Multi-game chunks (base game + expansion with different ownership)
  - Anonymous user (`Guid.Empty` → always Protected)
- **Unit**: Paraphrased snippet extraction:
  - Marker found → correct extraction
  - Marker missing → no snippet (not truncated original)
  - Forged marker in user input → ignored (prompt injection test)
  - Extracted text too similar to original → rejected (similarity check)
- **Unit**: `CopyrightDataProjection` — batch query correctness
- **Integration**: Full RAG pipeline with annotated chunks → verify CitationsJson persisted with correct tiers
- **Integration**: `StreamingComplete` backward compatibility — existing handlers still work with `Citations = null`
- **Frontend**: RuleSourceCard renders both tiers correctly (visual regression)
- **Frontend**: Mixed-tier card — chip colors, active citation switching, action visibility
- **E2E**: Chat with owned game + uploaded PDF → Full tier
- **E2E**: Chat with non-owned shared game → Protected tier
- **E2E**: Chat with CC-licensed document → Full tier regardless of ownership
