# Private PDF Agent Scope + Document Selection UI

## Goal

When a user uploads a private PDF for a game, auto-include it in the agent's RAG scope. Let users control which PDFs the agent uses via a document selection UI in both the creation wizard and the game detail page.

## Architecture

An event handler in KnowledgeBase BC listens to `PrivatePdfAssociatedEvent` and auto-adds the PDF to the agent's `SelectedDocumentIds`. The user manages selection through two touchpoints: the creation wizard (ConfigAgentStep) and the Knowledge Zone on the game detail page. A new user-facing endpoint handles updates.

## Document Selection Rules

| DocumentType | Behavior | UI Control |
|---|---|---|
| `Base` (rulebook) | **Mutually exclusive** — only 1 at a time | Radio button |
| `Expansion` | **Additive** — stacks on top of selected Base | Checkbox |
| `Errata` | **Additive** | Checkbox |
| `HomeRule` | **Additive** | Checkbox |

Documents in non-Ready states (Pending, Extracting, etc.) can be selected. The UI shows processing status badges. The document readiness check in `SendAgentMessageCommandHandler` (Fix #1) prevents chat until all selected documents are Ready.

## Backend Components

### 1. PrivatePdfAssociatedEventHandler

**Location:** `BoundedContexts/KnowledgeBase/Application/EventHandlers/PrivatePdfAssociatedEventHandler.cs`

**Behavior:**
- Listens to `PrivatePdfAssociatedEvent` (from UserLibrary BC)
- Finds agent by `event.GameId` via `IAgentRepository`
- If agent exists with current config → adds `event.PdfDocumentId` to `SelectedDocumentIds`
- If no agent exists → noop (user will create the agent later and select documents)
- Logs the action for diagnostics

**Dependencies:** `IAgentRepository`, `MeepleAiDbContext` (for AgentConfiguration), `IUnitOfWork`

### 2. GetAvailableDocumentsForGameQuery

**Location:** `BoundedContexts/KnowledgeBase/Application/Queries/GetAvailableDocumentsForGameQuery.cs`

**Endpoint:** `GET /api/v1/library/games/{gameId}/agent/documents`
**Auth:** User session, validates game is in user's library

**Input:** `GameId`, `UserId`

**Output:** `AvailableDocumentsDto`
```csharp
record AvailableDocumentsDto(
    Guid? AgentId,
    List<DocumentSelectionItemDto> BaseDocuments,
    List<DocumentSelectionItemDto> AdditionalDocuments
);

record DocumentSelectionItemDto(
    Guid DocumentId,
    string FileName,
    string DocumentType,       // "Base", "Expansion", "Errata", "HomeRule"
    string ProcessingState,    // "Ready", "Extracting", "Pending", "Failed", etc.
    bool IsPrivate,
    bool IsSelected,
    int? PageCount
);
```

**Logic:**
- Query shared game documents (from SharedGameCatalog PDFs via VectorDocuments)
- Query private documents (user's private PDFs for this game via UserLibraryEntry.PrivatePdfId + DocumentProcessing)
- For each document, check if its ID is in the agent's current `SelectedDocumentIds`
- Split into `BaseDocuments` (DocumentType.Base) and `AdditionalDocuments` (Expansion/Errata/HomeRule)

### 3. UpdateUserAgentDocumentsCommand

**Location:** `BoundedContexts/KnowledgeBase/Application/Commands/UpdateUserAgentDocumentsCommand.cs`

**Endpoint:** `PUT /api/v1/library/games/{gameId}/agent/documents`
**Auth:** User session, validates game is in user's library

**Input:**
```csharp
record UpdateUserAgentDocumentsCommand(
    Guid GameId,
    Guid UserId,
    List<Guid> SelectedDocumentIds
) : ICommand<Unit>;
```

**Validation:**
- Game must be in user's library
- All document IDs must belong to this game (shared or private)
- At most 1 document with `DocumentType.Base` among selected documents
- Agent must exist for this game

**Handler logic:**
- Load agent and current config
- Validate selected documents exist and belong to the game
- Validate Base document constraint (max 1)
- Update `SelectedDocumentIdsJson` on the agent's current configuration
- Save changes

### 4. Endpoint Registration

**Location:** `Routing/UserLibrary/UserLibraryAgentEndpoints.cs` (new file)

```
GET  /api/v1/library/games/{gameId}/agent/documents  → GetAvailableDocumentsForGameQuery
PUT  /api/v1/library/games/{gameId}/agent/documents  → UpdateUserAgentDocumentsCommand
```

Both require authenticated user session. Both validate game ownership via library entry.

## Frontend Components

### 5. DocumentSelectionPanel

**Location:** `components/library/DocumentSelectionPanel.tsx`

Reusable component used in both ConfigAgentStep and Knowledge Zone.

**Props:**
```typescript
interface DocumentSelectionPanelProps {
  gameId: string;
  onSelectionChange?: (selectedIds: string[]) => void;
  initialSelection?: string[];  // for wizard mode (pre-select just-uploaded PDF)
  readOnly?: boolean;
}
```

**Layout:**
```
📖 Regolamento (scegli uno):
  ○ Catan - Regolamento IT (shared, 45 pag) ✅ Ready
  ○ Catan - Regolamento EN (shared, 42 pag) ✅ Ready
  ○ Mio scan regolamento (privato) ⏳ In elaborazione

📑 Espansioni & Aggiunte (seleziona quanti vuoi):
  ☑ Catan - Marinai (expansion) ✅ Ready
  ☐ Catan - Errata 2024 (errata) ✅ Ready
  ☑ Le mie house rules (houserule, privato) ✅ Ready
```

**Behavior:**
- Fetches data from `GET /api/v1/library/games/{gameId}/agent/documents`
- Base documents: radio group — selecting one deselects others
- Additional documents: independent checkboxes
- Status badges: ✅ Ready (green), ⏳ Processing (amber), ❌ Failed (red)
- Private documents labeled with "(privato)" suffix
- Saves on change via `PUT /api/v1/library/games/{gameId}/agent/documents`
- Debounced save (300ms) to avoid rapid API calls

### 6. ConfigAgentStep Integration

**Location:** `app/(authenticated)/library/private/add/steps/ConfigAgentStep.tsx`

**Changes:**
- Integrate `DocumentSelectionPanel` below agent typology selection
- Pass `initialSelection={[pdfId]}` to pre-select the just-uploaded PDF
- In wizard mode, the panel doesn't save immediately — passes selected IDs to the agent creation call

### 7. Knowledge Zone Integration

**Location:** `components/library/game-detail/GameTableZoneKnowledge.tsx` (or equivalent)

**Changes:**
- Add expandable "Gestisci documenti agente" section
- Renders `DocumentSelectionPanel` with `gameId`
- Only shown when agent exists for the game
- Changes save immediately via the PUT endpoint

## Data Flow

```
Upload private PDF
    → PrivatePdfAssociatedEvent (UserLibrary BC)
    → PrivatePdfAssociatedEventHandler (KnowledgeBase BC)
        → Auto-add to SelectedDocumentIds if agent exists

User opens document selection (wizard or knowledge zone)
    → GET /api/v1/library/games/{gameId}/agent/documents
        → Returns shared + private PDFs with state and selection

User changes selection
    → PUT /api/v1/library/games/{gameId}/agent/documents
        → Validates: max 1 Base, ownership, documents exist
        → Updates SelectedDocumentIdsJson
```

## Error Handling

- **No agent exists:** Query returns `agentId: null`, UI shows "Crea un agente prima di selezionare documenti" or hides the panel
- **Document not found:** Command rejects with 404 if selected document ID doesn't belong to game
- **Base constraint violation:** Command rejects with 400 "Puoi selezionare al massimo un regolamento base"
- **Game not in library:** Both endpoints return 403

## Testing

**Backend unit tests:**
- EventHandler: agent exists → auto-add; no agent → noop; duplicate add → idempotent
- Query: returns combined shared+private, correctly marks isSelected, splits by type
- Command: validates max 1 Base, rejects invalid doc IDs, rejects non-owned games

**Backend integration tests:**
- Upload PDF → event fires → auto-add → query shows updated selection

**Frontend tests:**
- DocumentSelectionPanel: radio behavior for Base, checkbox for others, status badges render
- Optimistic update on selection change

## Non-Goals

- No changes to the admin document management endpoints
- No changes to the auto-create agent flow in `SendAgentMessageCommandHandler` (it already picks completed VectorDocuments)
- No drag-and-drop reordering of documents
- No document preview from the selection panel
