# UX Core Flow — Search, Chat, PDF, Discovery→Chat

**Date**: 2026-04-19
**Status**: Approved
**Scope**: Blocco A (UX critica) + Blocco B (Infrastruttura)

---

## Problem Statement

The core value proposition of MeepleAI — discover a board game, get AI-powered answers about its rules — is broken. The AI Chat tab in the game detail page is a placeholder. ManaPips exist but aren't wired on library cards. There's no direct path from game discovery to chat. PDF citations are not clickable. The private PDF upload limit (50MB) is too low for large rulebooks.

## Decisions

| # | Decision | Choice | Rationale |
|---|----------|--------|-----------|
| 1 | Chat entry point from library | **Hybrid**: ManaPips → slide-over quick chat + link for full chat | Covers both "quick question" and "study session" use cases |
| 2 | AI-ready indicator | **ManaPips enhanced** with color states (green/yellow/grey) | Reuses existing infrastructure, no new UI element needed |
| 3 | Citation viewer | **Inline (accordion)** in slide-over + **Side panel** in full chat | Scales viewer complexity with chat context |
| 4 | Chat without library | **Yes** — direct chat from catalog if KB exists | Removes unjustified friction; library remains organizational, not a gate |
| 5 | Private PDF limit | **Bump validator to 100MB** | One-line fix; pipeline already handles large files |
| 6 | NewChatView refactoring | **Full decomposition** into 5 components | Required for ChatSlideOverPanel reuse of GameSelector/AgentSelector |

---

## Blocco A — UX Core Flow

### A1. ManaPips Enhanced (Visual State)

**Current state**: ManaPips show counts (sessions/KB/agents) on game hero cards only. Not wired on library cards. No color differentiation by state.

**Target state**:
- KB pip color states:
  - **Green** (`kbIndexedCount > 0`): KB ready, chat available
  - **Yellow** (`kbProcessingCount > 0, kbIndexedCount === 0`): PDF uploaded, indexing in progress
  - **Grey** (no PDF): No knowledge base
- Agent pip: colored when count > 0, grey when 0
- Session pip: unchanged (count-based)
- **Wire ManaPips on all game cards** in library: `PersonalLibrarySection`, `CatalogCarouselSection`, dashboard cards
- KB pip click → opens ChatSlideOverPanel with game pre-selected

**Files to modify**:
- `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx` — add color state logic
- `apps/web/src/hooks/queries/useGameManaPips.ts` — expose indexing state (not just count)
- Library section components — pass manaPips prop to MeepleCard instances

### A2. Chat Direct from Catalog (No Library Required)

**Current state**: User MUST add game to UserLibrary before chatting. Chat thread creation requires library entry.

**Target state**:
- CTA "Chatta con AI" visible on game cards and game detail pages when `hasKb: true`
- Clicking CTA navigates to `/chat/new?game={sharedGameId}` or opens slide-over
- No `UserLibraryEntry` required for chat thread creation
- After first chat message, non-blocking suggestion: "Aggiungi alla tua libreria per ritrovare le conversazioni"

**Backend changes**:
- `CreateChatThreadCommand`: accept `SharedGameId` without requiring `UserLibraryEntry` ownership
- `CreateChatThreadCommandHandler`: resolve KB documents from SharedGame directly
- New query: `GetSharedGameKbStatusQuery` — returns KB availability for a shared game

**Frontend changes**:
- CTA button component: visible when `hasKb: true`, navigates to chat
- Post-chat library suggestion banner (dismissable)

### A3. ChatSlideOverPanel Wiring (Quick Chat)

**Current state**: ChatSlideOverPanel (PR#329) exists and is wired to TopBar button. Not connected to game cards or ManaPips.

**Target state**:
- ManaPips KB pip click → `useChatPanel.open({ gameId, source: 'manaPips' })`
- Auto-select default agent: system `auto` type, or single custom agent if only one exists
- Thread creation is **lazy** — created on first message send, not on panel open
- Inline citation viewer (accordion) integrated in message bubbles

**Changes**:
- `useChatPanel` hook: accept `gameId` parameter, auto-resolve agent
- `ChatSlideOverPanel`: accept pre-selected game context
- `ChatInputBar`: trigger lazy thread creation on first send when no threadId exists
- `ChatMessageBubble`: integrate `CitationExpander` component

### A4. NewChatView Refactoring (847 → 5 Components)

**Current state**: `NewChatView.tsx` (847 lines) is a God Component handling game selection, agent selection, thread creation, routing, and quick start suggestions.

**Target state** — 5 focused components:

| Component | Responsibility | Reused by |
|-----------|---------------|-----------|
| `GameSelector` | Game grid with tabs (my games / shared), search, hasKb filter | NewChatView, ChatSlideOverPanel |
| `AgentSelector` | Agent grid (custom + system), mutually exclusive selection | NewChatView, ChatSlideOverPanel |
| `ThreadCreator` | Thread creation logic + redirect (extracted business logic) | NewChatView, ChatSlideOverPanel |
| `QuickStartSuggestions` | Context-aware pre-formatted prompts | NewChatView only |
| `ChatEntryOrchestrator` | Slim orchestrator composing the 4 above | NewChatView page |

**Location**: `apps/web/src/components/chat/entry/` (new directory)

**Key constraint**: `GameSelector` and `AgentSelector` must work both as standalone (in slide-over) and composed (in full page). Props-driven, no internal routing.

### A5. AI Chat Tab → Real Bridge

**Current state**: `GameAiChatTab.tsx` shows placeholder text "Integrazione completa della chat con il gioco in arrivo."

**Target state**:
- Show KB status (indexed/processing/none) with ManaPips-consistent colors
- List existing chat threads for this game (from `GET /api/v1/chat-threads?gameId={id}`)
- CTA "Nuova chat" → opens ChatSlideOverPanel or navigates to `/chat/new?game={id}`
- CTA "Apri chat completa" for each existing thread → `/chat/{threadId}`

---

## Blocco B — Infrastructure

### B1. PDF Validator Bump

**Current state**: `UploadPrivatePdfCommandValidator` limits to 50MB. Endpoint supports 100MB. Pipeline handles large files.

**Target state**: Validator accepts up to 100MB (104,857,600 bytes).

**File**: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPrivatePdfCommandValidator.cs`
**Change**: Single line — update `MaxFileSize` from `52_428_800` to `104_857_600`.

### B2. Page Text Endpoint

**New endpoint**: `GET /api/v1/pdfs/{pdfId}/pages/{pageNumber}/text`

**Behavior**:
- Aggregates text chunks for the requested page number
- Orders chunks by `chunkIndex` within the page
- Returns `{ pageNumber: int, text: string, documentTitle: string, totalPages: int }`
- Authorization: session user must own the PDF, or PDF is from a shared game KB
- No PDF binary served — text only, zero copyright risk

**Query**: `GetPdfPageTextQuery(Guid PdfId, int PageNumber)`
**Handler**: Queries `VectorDocument` chunks filtered by `PageNumber`, ordered by `ChunkIndex`, concatenates text.

### B3. Inline Citation Viewer (Slide-over)

**New component**: `CitationExpander`

**Behavior**:
- Renders inside `ChatMessageBubble` when citation badge is clicked
- Fetches text from page text endpoint (B2)
- Displays as collapsible accordion below the citation badge
- Header: "REGOLAMENTO — PAGINA {n}"
- Body: extracted text with markdown rendering
- Collapse on second click or when another citation is expanded
- Loading state while fetching

**Location**: `apps/web/src/components/chat/panel/CitationExpander.tsx`

### B4. Side Panel Citation Viewer (Full Chat)

**New component**: `PageViewerPanel`

**Behavior**:
- Drawer/panel on the right side of the chat (split view)
- Shows text of the cited page with page navigation (◀ prev | **current** | next ▶)
- Jump-to-page input
- Chat remains visible on the left
- On mobile: full-screen overlay with back button
- Opens when citation is clicked in full chat mode
- Closes with X button or Escape key

**Location**: `apps/web/src/components/chat/viewer/PageViewerPanel.tsx`

**Desktop layout**: `ChatThreadView` becomes a flex container — `ChatMainArea` (flex: 1) + `PageViewerPanel` (width: 400px, conditional)

### B5. Search Ranking Boost for AI-Ready Games

**Current state**: `hasKb` is a boolean filter only. Games with KB don't rank higher.

**Target state**: Games with indexed KB receive a ranking boost in search results.

**Implementation** in `SearchSharedGamesQueryHandler`:
```csharp
.OrderBy(g => g.HasIndexedKb ? 0 : 1)
.ThenByDescending(g => tsRank)
.ThenBy(g => g.Title)
```

When no explicit sort is requested, AI-ready games surface first. When user sorts explicitly (by title, rating, etc.), the boost is not applied.

---

## Out of Scope

- Chunked upload for private PDFs (YAGNI — 99% of rulebooks < 100MB)
- Full PDF binary viewer/renderer (text-only approach solves copyright + performance)
- Discover page implementation (separate project)
- NewChatView mobile layout changes (existing mobile layout unchanged)
- Multi-language FTS (Italian-only for now, as configured)

## Dependencies

- **A3 depends on A4**: ChatSlideOverPanel needs GameSelector/AgentSelector from refactoring
- **A3 depends on B3**: Slide-over needs inline citation viewer
- **A5 depends on A2**: AI Chat tab needs chat-without-library capability
- **B3/B4 depend on B2**: Both viewers need page text endpoint
- **A1 is independent**: Can be implemented first

## Implementation Order

```
Phase 1 (Independent):  A1 (ManaPips enhanced) + B1 (PDF bump) + B2 (page text endpoint)
Phase 2 (Foundation):   A4 (NewChatView refactoring) + A2 (chat without library)
Phase 3 (Wiring):       A3 (slide-over wiring) + B3 (inline citation) + A5 (AI Chat tab)
Phase 4 (Polish):       B4 (side panel viewer) + B5 (search ranking boost)
```

## Testing Strategy

- **E2E**: Catalog → chat (no library) → receive RAG response → click citation → see text
- **E2E**: Library card ManaPips → slide-over → quick chat → inline citation
- **Integration**: Upload 80MB private PDF → success
- **Integration**: Page text endpoint → returns correct text for page number
- **Unit**: ManaPips color state logic (green/yellow/grey)
- **Unit**: GameSelector/AgentSelector as standalone components
- **Unit**: CitationExpander accordion behavior
- **Unit**: PageViewerPanel navigation
