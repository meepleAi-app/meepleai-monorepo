# UX Core Flow Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the complete discovery-to-chat flow: ManaPips enhanced states, chat without library, citation viewers, PDF limit bump, NewChatView refactoring, search ranking boost.

**Architecture:** 4-phase incremental delivery. Phase 1 is independent (3 parallel tasks). Phase 2 builds foundation (refactoring + backend). Phase 3 wires everything together. Phase 4 polishes.

**Tech Stack:** Next.js 16 (React 19) + Tailwind 4 + Zustand | .NET 9 + MediatR + EF Core + pgvector | Vitest + xUnit

**Spec:** `docs/superpowers/specs/2026-04-19-ux-core-flow-design.md`

**Branch:** `feature/ux-core-flow` from `main-dev`

---

## Phase 1: Independent Tasks (Parallel)

### Task 1: ManaPips Enhanced — Color States

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx`
- Modify: `apps/web/src/hooks/queries/useGameManaPips.ts`
- Create: `apps/web/src/__tests__/components/meeple-card/ManaPips.test.tsx`
- Create: `apps/web/src/__tests__/hooks/useGameManaPips.test.ts`

- [ ] **Step 1: Write tests for ManaPips color state logic**

```tsx
// apps/web/src/__tests__/components/meeple-card/ManaPips.test.tsx
import { describe, it, expect } from 'vitest';
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

describe('getKbPipColor', () => {
  it('returns green when kbIndexedCount > 0', () => {
    expect(getKbPipColor({ kbIndexedCount: 3, kbProcessingCount: 0 })).toBe('hsl(142, 71%, 45%)');
  });

  it('returns yellow when processing > 0 and indexed === 0', () => {
    expect(getKbPipColor({ kbIndexedCount: 0, kbProcessingCount: 2 })).toBe('hsl(45, 93%, 47%)');
  });

  it('returns grey when both are 0', () => {
    expect(getKbPipColor({ kbIndexedCount: 0, kbProcessingCount: 0 })).toBe('hsl(0, 0%, 60%)');
  });

  it('returns green when both indexed and processing exist', () => {
    expect(getKbPipColor({ kbIndexedCount: 1, kbProcessingCount: 1 })).toBe('hsl(142, 71%, 45%)');
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/meeple-card/ManaPips.test.tsx`
Expected: FAIL — `getKbPipColor` not exported

- [ ] **Step 3: Implement getKbPipColor and wire into ManaPips**

Add to `apps/web/src/components/ui/data-display/meeple-card/parts/ManaPips.tsx` — new export before the ManaPips component:

```tsx
export interface KbPipState {
  kbIndexedCount: number;
  kbProcessingCount: number;
}

export function getKbPipColor(state: KbPipState): string {
  if (state.kbIndexedCount > 0) return 'hsl(142, 71%, 45%)'; // green
  if (state.kbProcessingCount > 0) return 'hsl(45, 93%, 47%)'; // yellow
  return 'hsl(0, 0%, 60%)'; // grey
}
```

In `PipRenderer`, replace line 63 (`const color = entityHsl(pip.entityType);`) with:

```tsx
const color = pip.colorOverride ?? entityHsl(pip.entityType);
```

Add `colorOverride?: string` to the `ManaPip` interface:

```tsx
export interface ManaPip {
  entityType: MeepleEntityType;
  count?: number;
  items?: ManaPipItem[];
  onCreate?: () => void;
  createLabel?: string;
  colorOverride?: string;  // NEW: override entity color (e.g., KB state)
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/meeple-card/ManaPips.test.tsx`
Expected: PASS

- [ ] **Step 5: Update useGameManaPips to expose indexing state**

Modify `apps/web/src/hooks/queries/useGameManaPips.ts` — add to `GameManaPipsBucket`:

```tsx
export interface GameManaPipsBucket {
  count: number;
  items: ManaPipItem[];
  indexedCount?: number;     // NEW
  processingCount?: number;  // NEW
}
```

Update the kbs bucket construction (around line 76-80) to include indexing state:

```tsx
kbs: {
  count: kbDocs.length,
  items: kbDocs.slice(0, 5).map(d => ({
    id: d.id,
    label: d.title ?? d.fileName ?? 'PDF',
    href: `/games/${gameId}/knowledge-base`,
  })),
  indexedCount: kbDocs.filter(d => d.processingState === 'Ready' || d.processingState === 'Completed').length,
  processingCount: kbDocs.filter(d => d.processingState === 'Processing' || d.processingState === 'Pending').length,
},
```

Update `buildGameManaPips` to pass `colorOverride` on the KB pip:

```tsx
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

// In buildGameManaPips, the KB pip:
{
  entityType: 'kb' as MeepleEntityType,
  count: data.kbs.count,
  items: data.kbs.items,
  onCreate: actions?.onCreateKb,
  createLabel: 'Carica PDF',
  colorOverride: getKbPipColor({
    kbIndexedCount: data.kbs.indexedCount ?? 0,
    kbProcessingCount: data.kbs.processingCount ?? 0,
  }),
},
```

- [ ] **Step 6: Write test for buildGameManaPips color override**

```ts
// apps/web/src/__tests__/hooks/useGameManaPips.test.ts
import { describe, it, expect } from 'vitest';
import { buildGameManaPips, type GameManaPipsData } from '@/hooks/queries/useGameManaPips';

describe('buildGameManaPips', () => {
  it('sets green colorOverride when KB is indexed', () => {
    const data: GameManaPipsData = {
      sessions: { count: 0, items: [] },
      kbs: { count: 2, items: [], indexedCount: 2, processingCount: 0 },
      agents: { count: 0, items: [] },
    };
    const pips = buildGameManaPips(data, {});
    const kbPip = pips.find(p => p.entityType === 'kb');
    expect(kbPip?.colorOverride).toBe('hsl(142, 71%, 45%)');
  });

  it('sets yellow colorOverride when KB is processing', () => {
    const data: GameManaPipsData = {
      sessions: { count: 0, items: [] },
      kbs: { count: 1, items: [], indexedCount: 0, processingCount: 1 },
      agents: { count: 0, items: [] },
    };
    const pips = buildGameManaPips(data, {});
    const kbPip = pips.find(p => p.entityType === 'kb');
    expect(kbPip?.colorOverride).toBe('hsl(45, 93%, 47%)');
  });
});
```

- [ ] **Step 7: Run all tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/meeple-card/ src/__tests__/hooks/useGameManaPips.test.ts`
Expected: ALL PASS

- [ ] **Step 8: Commit**

```
feat(ui): ManaPips enhanced color states — green/yellow/grey for KB status
```

---

### Task 2: PDF Validator Bump (50MB to 100MB)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPrivatePdfCommandValidator.cs`
- Modify: relevant test file for this validator

- [ ] **Step 1: Find and update existing test**

Search for the existing validator test:
Run: `cd apps/api && grep -r "UploadPrivatePdfCommandValidator" tests/ --include="*.cs" -l`

Update the test to verify 100MB limit. Add test case:

```csharp
[Fact]
public void Should_Accept_File_Up_To_100MB()
{
    var command = CreateValidCommand();
    command.PdfFile = CreateMockFile(sizeBytes: 104_857_600); // exactly 100MB
    var result = _validator.Validate(command);
    result.IsValid.Should().BeTrue();
}

[Fact]
public void Should_Reject_File_Over_100MB()
{
    var command = CreateValidCommand();
    command.PdfFile = CreateMockFile(sizeBytes: 104_857_601); // 100MB + 1 byte
    var result = _validator.Validate(command);
    result.IsValid.Should().BeFalse();
}
```

- [ ] **Step 2: Run tests to see the 100MB test fail**

Run: `cd apps/api && dotnet test --filter "UploadPrivatePdfCommandValidator"`
Expected: "Should_Accept_File_Up_To_100MB" FAILS (current limit is 50MB)

- [ ] **Step 3: Update validator — one line change**

In `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Validators/UploadPrivatePdfCommandValidator.cs`, line 12:

```csharp
// BEFORE:
private const long MaxFileSizeBytes = 52_428_800; // 50 MB

// AFTER:
private const long MaxFileSizeBytes = 104_857_600; // 100 MB
```

- [ ] **Step 4: Run tests to verify pass**

Run: `cd apps/api && dotnet test --filter "UploadPrivatePdfCommandValidator"`
Expected: ALL PASS

- [ ] **Step 5: Commit**

```
fix(pdf): bump private PDF upload limit from 50MB to 100MB
```

---

### Task 3: Page Text Endpoint

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfPageTextQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfPageTextQueryHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/PdfPageTextDto.cs`
- Modify: `apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs`
- Create: test file for the handler

- [ ] **Step 1: Create the DTO**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/PdfPageTextDto.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record PdfPageTextDto(
    int PageNumber,
    string Text,
    string DocumentTitle,
    int TotalPages
);
```

- [ ] **Step 2: Create the query**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfPageTextQuery.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

public sealed record GetPdfPageTextQuery(
    Guid PdfId,
    int PageNumber,
    Guid UserId
) : IRequest<PdfPageTextDto>;
```

- [ ] **Step 3: Write the handler test**

```csharp
[Fact]
public async Task Should_Return_Page_Text_For_Valid_Page()
{
    var pdfId = await SeedPdfWithChunks(gameId, pages: 5);
    var query = new GetPdfPageTextQuery(pdfId, PageNumber: 2, UserId: _testUserId);

    var result = await _mediator.Send(query);

    result.PageNumber.Should().Be(2);
    result.Text.Should().NotBeNullOrEmpty();
    result.TotalPages.Should().Be(5);
}

[Fact]
public async Task Should_Throw_NotFoundException_For_Invalid_Page()
{
    var pdfId = await SeedPdfWithChunks(gameId, pages: 3);
    var query = new GetPdfPageTextQuery(pdfId, PageNumber: 99, UserId: _testUserId);

    await FluentActions.Invoking(() => _mediator.Send(query))
        .Should().ThrowAsync<NotFoundException>();
}
```

- [ ] **Step 4: Implement the handler**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetPdfPageTextQueryHandler.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Exceptions;
using Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

internal sealed class GetPdfPageTextQueryHandler(AppDbContext db)
    : IRequestHandler<GetPdfPageTextQuery, PdfPageTextDto>
{
    public async Task<PdfPageTextDto> Handle(
        GetPdfPageTextQuery request, CancellationToken cancellationToken)
    {
        var pdf = await db.PdfDocuments
            .Where(p => p.Id == request.PdfId)
            .Select(p => new { p.Id, p.Title, p.FileName, p.GameId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("PdfDocument", request.PdfId);

        var chunks = await db.VectorDocuments
            .Where(v => v.PdfDocumentId == request.PdfId
                     && v.IndexingStatus == "completed")
            .OrderBy(v => v.PageNumber)
            .ThenBy(v => v.ChunkIndex)
            .Select(v => new { v.PageNumber, v.TextContent, v.ChunkIndex })
            .ToListAsync(cancellationToken);

        var totalPages = chunks.Select(c => c.PageNumber).Distinct().Count();

        var pageChunks = chunks
            .Where(c => c.PageNumber == request.PageNumber)
            .OrderBy(c => c.ChunkIndex)
            .ToList();

        if (pageChunks.Count == 0)
            throw new NotFoundException($"Page {request.PageNumber} not found in document");

        var text = string.Join("\n\n", pageChunks.Select(c => c.TextContent));
        var title = pdf.Title ?? pdf.FileName ?? "Documento";

        return new PdfPageTextDto(request.PageNumber, text, title, totalPages);
    }
}
```

- [ ] **Step 5: Wire the endpoint**

Add to `apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs`:

```csharp
group.MapGet("/{pdfId:guid}/pages/{pageNumber:int}/text", HandleGetPageText)
    .WithName("GetPdfPageText")
    .WithDescription("Get extracted text for a specific PDF page")
    .Produces<PdfPageTextDto>()
    .Produces(404);

private static async Task<IResult> HandleGetPageText(
    Guid pdfId,
    int pageNumber,
    IMediator mediator,
    HttpContext httpContext)
{
    var userId = httpContext.GetUserId();
    var result = await mediator.Send(new GetPdfPageTextQuery(pdfId, pageNumber, userId));
    return Results.Ok(result);
}
```

- [ ] **Step 6: Run handler test**

Run: `cd apps/api && dotnet test --filter "GetPdfPageText"`
Expected: PASS

- [ ] **Step 7: Commit**

```
feat(api): add GET /pdfs/{id}/pages/{pageNumber}/text endpoint for citation viewer
```

---

## Phase 2: Foundation

### Task 4: NewChatView Refactoring — 5 Components

**Files:**
- Create: `apps/web/src/components/chat/entry/GameSelector.tsx`
- Create: `apps/web/src/components/chat/entry/AgentSelector.tsx`
- Create: `apps/web/src/components/chat/entry/ThreadCreator.ts`
- Create: `apps/web/src/components/chat/entry/QuickStartSuggestions.tsx`
- Create: `apps/web/src/components/chat/entry/ChatEntryOrchestrator.tsx`
- Create: `apps/web/src/components/chat/entry/index.ts`
- Modify: `apps/web/src/components/chat-unified/NewChatView.tsx` (replace with thin wrapper)
- Create: `apps/web/src/__tests__/components/chat/entry/GameSelector.test.tsx`
- Create: `apps/web/src/__tests__/components/chat/entry/AgentSelector.test.tsx`

- [ ] **Step 1: Create GameSelector component**

Extract lines 160-249 from NewChatView into standalone component:

```tsx
// apps/web/src/components/chat/entry/GameSelector.tsx
'use client';

import { useState, useCallback } from 'react';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/input';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { Search } from 'lucide-react';

export interface GameSelectorGame {
  id: string;
  title: string;
  imageUrl?: string;
  hasKb?: boolean;
  isPrivate?: boolean;
  yearPublished?: number;
}

interface GameSelectorProps {
  onSelect: (game: GameSelectorGame) => void;
  selectedGameId?: string | null;
  showOnlyWithKb?: boolean;
  className?: string;
}

export function GameSelector({ onSelect, selectedGameId, showOnlyWithKb = false, className }: GameSelectorProps) {
  const [search, setSearch] = useState('');
  const [tab, setTab] = useState<'mine' | 'shared'>('mine');

  const { data: library } = useLibrary();
  const { data: sharedGames } = useSharedGames({ search: tab === 'shared' ? search : undefined });

  const myGames: GameSelectorGame[] = (library ?? []).map(entry => ({
    id: entry.gameId ?? entry.id,
    title: entry.gameTitle ?? entry.title,
    imageUrl: entry.imageUrl,
    hasKb: entry.hasKb,
    isPrivate: entry.isPrivateGame,
  }));

  const catalogGames: GameSelectorGame[] = (sharedGames?.items ?? []).map(g => ({
    id: g.id,
    title: g.title,
    imageUrl: g.imageUrl,
    hasKb: g.hasKb,
    yearPublished: g.yearPublished,
  }));

  const filterGames = useCallback((games: GameSelectorGame[]) => {
    let filtered = games;
    if (showOnlyWithKb) filtered = filtered.filter(g => g.hasKb);
    if (search && tab === 'mine') {
      const q = search.toLowerCase();
      filtered = filtered.filter(g => g.title.toLowerCase().includes(q));
    }
    return filtered;
  }, [search, showOnlyWithKb, tab]);

  return (
    <div className={className}>
      <div className="relative mb-4">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
        <Input
          placeholder="Cerca gioco..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          className="pl-9"
        />
      </div>
      <Tabs value={tab} onValueChange={v => setTab(v as 'mine' | 'shared')}>
        <TabsList className="mb-4">
          <TabsTrigger value="mine">I miei giochi</TabsTrigger>
          <TabsTrigger value="shared">Catalogo condiviso</TabsTrigger>
        </TabsList>
        <TabsContent value="mine">
          <GameGrid games={filterGames(myGames)} selectedId={selectedGameId} onSelect={onSelect} />
        </TabsContent>
        <TabsContent value="shared">
          <GameGrid games={filterGames(catalogGames)} selectedId={selectedGameId} onSelect={onSelect} />
        </TabsContent>
      </Tabs>
    </div>
  );
}

function GameGrid({ games, selectedId, onSelect }: {
  games: GameSelectorGame[];
  selectedId?: string | null;
  onSelect: (game: GameSelectorGame) => void;
}) {
  if (games.length === 0) {
    return <p className="text-sm text-muted-foreground py-8 text-center">Nessun gioco trovato</p>;
  }
  return (
    <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
      {games.map(game => (
        <button key={game.id} onClick={() => onSelect(game)} className="text-left">
          <MeepleCard
            entity="game"
            variant="compact"
            title={game.title}
            imageUrl={game.imageUrl}
            className={selectedId === game.id ? 'ring-2 ring-orange-500' : ''}
          />
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Create AgentSelector component**

```tsx
// apps/web/src/components/chat/entry/AgentSelector.tsx
'use client';

import { useMemo } from 'react';
import { useGameAgents } from '@/hooks/queries/useGameAgents';

export interface AgentOption {
  id: string;
  name: string;
  type: string;
  description: string;
  icon: string;
  isSystem?: boolean;
}

const SYSTEM_AGENTS: AgentOption[] = [
  { id: 'system-auto', name: 'Automatico', type: 'auto', description: 'Selezione automatica in base alla domanda', icon: '🤖', isSystem: true },
  { id: 'system-tutor', name: 'Tutor', type: 'tutor', description: 'Spiega regole e meccaniche', icon: '📚', isSystem: true },
  { id: 'system-arbitro', name: 'Arbitro', type: 'arbitro', description: 'Risolve dispute sulle regole', icon: '⚖️', isSystem: true },
  { id: 'system-strategist', name: 'Stratega', type: 'strategist', description: 'Consigli di strategia', icon: '🎯', isSystem: true },
  { id: 'system-narratore', name: 'Narratore', type: 'narratore', description: 'Lore e ambientazione', icon: '���', isSystem: true },
];

interface AgentSelectorProps {
  gameId: string;
  onSelect: (agent: AgentOption) => void;
  selectedAgentId?: string | null;
  className?: string;
}

export function AgentSelector({ gameId, onSelect, selectedAgentId, className }: AgentSelectorProps) {
  const { data: customAgents } = useGameAgents(gameId);

  const allAgents = useMemo(() => {
    const custom: AgentOption[] = (customAgents ?? []).map(a => ({
      id: a.id,
      name: a.name,
      type: a.type ?? 'custom',
      description: a.description ?? '',
      icon: '🧩',
      isSystem: false,
    }));
    return [...custom, ...SYSTEM_AGENTS];
  }, [customAgents]);

  return (
    <div className={className}>
      <h3 className="text-sm font-semibold mb-3 font-quicksand">Scegli agente</h3>
      <div className="grid grid-cols-2 sm:grid-cols-3 gap-3">
        {allAgents.map(agent => (
          <button key={agent.id} onClick={() => onSelect(agent)} className="text-left">
            <div className={`rounded-lg border p-3 transition-all hover:border-orange-400 ${
              selectedAgentId === agent.id ? 'ring-2 ring-orange-500 border-orange-500' : 'border-border'
            }`}>
              <div className="text-2xl mb-1">{agent.icon}</div>
              <div className="font-semibold text-sm">{agent.name}</div>
              <div className="text-xs text-muted-foreground line-clamp-2">{agent.description}</div>
            </div>
          </button>
        ))}
      </div>
    </div>
  );
}

export { SYSTEM_AGENTS };
```

- [ ] **Step 3: Create ThreadCreator utility**

```ts
// apps/web/src/components/chat/entry/ThreadCreator.ts
import { api } from '@/lib/api';

export interface CreateThreadParams {
  gameId: string;
  agentId?: string | null;
  agentType?: string | null;
  title?: string;
  initialMessage?: string;
  selectedKnowledgeBaseIds?: string[];
}

export async function createChatThread(params: CreateThreadParams): Promise<string> {
  const isSystemAgent = params.agentId?.startsWith('system-');
  const response = await api.chat.createThread({
    gameId: params.gameId,
    agentId: isSystemAgent ? null : (params.agentId ?? null),
    agentType: isSystemAgent ? params.agentId!.replace('system-', '') : (params.agentType ?? null),
    title: params.title,
    initialMessage: params.initialMessage,
    selectedKnowledgeBaseIds: params.selectedKnowledgeBaseIds,
  });
  return response.id;
}
```

- [ ] **Step 4: Create QuickStartSuggestions component**

```tsx
// apps/web/src/components/chat/entry/QuickStartSuggestions.tsx
'use client';

export interface QuickStartSuggestion {
  label: string;
  message: string;
  promptType?: 'rule_dispute' | 'setup' | 'general' | 'suggestion';
}

const BASE_SUGGESTIONS: QuickStartSuggestion[] = [
  { label: 'Ho una domanda sulle regole', message: 'Ho una domanda sulle regole di questo gioco.', promptType: 'general' },
  { label: 'Setup della partita', message: 'Come si prepara il gioco per iniziare una partita?', promptType: 'setup' },
  { label: 'Risolvi una disputa', message: 'Abbiamo una disputa su una regola, puoi aiutarci?', promptType: 'rule_dispute' },
];

export function getQuickStartSuggestions(gameName?: string): QuickStartSuggestion[] {
  if (!gameName) return BASE_SUGGESTIONS;
  return [
    { label: `Come si gioca a ${gameName}?`, message: `Spiegami come si gioca a ${gameName}.`, promptType: 'general' },
    { label: 'Setup della partita', message: `Come si prepara ${gameName} per iniziare una partita?`, promptType: 'setup' },
    { label: 'Risolvi una disputa', message: `Abbiamo una disputa su una regola di ${gameName}, puoi aiutarci?`, promptType: 'rule_dispute' },
    { label: 'Strategia consigliata', message: `Quali sono le strategie migliori per ${gameName}?`, promptType: 'suggestion' },
  ];
}

interface QuickStartSuggestionsProps {
  gameName?: string;
  onSelect: (suggestion: QuickStartSuggestion) => void;
  className?: string;
}

export function QuickStartSuggestions({ gameName, onSelect, className }: QuickStartSuggestionsProps) {
  const suggestions = getQuickStartSuggestions(gameName);
  return (
    <div className={className}>
      <h3 className="text-sm font-semibold mb-3 font-quicksand">Inizia con...</h3>
      <div className="flex flex-wrap gap-2">
        {suggestions.map(s => (
          <button
            key={s.label}
            onClick={() => onSelect(s)}
            className="text-sm px-3 py-1.5 rounded-full border border-border hover:border-orange-400 hover:bg-orange-50 dark:hover:bg-orange-950/20 transition-colors"
          >
            {s.label}
          </button>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Create ChatEntryOrchestrator**

```tsx
// apps/web/src/components/chat/entry/ChatEntryOrchestrator.tsx
'use client';

import { useState, useCallback } from 'react';
import { useRouter, useSearchParams } from 'next/navigation';
import { GameSelector, type GameSelectorGame } from './GameSelector';
import { AgentSelector, type AgentOption } from './AgentSelector';
import { QuickStartSuggestions } from './QuickStartSuggestions';
import { createChatThread } from './ThreadCreator';
import { Button } from '@/components/ui/button';
import { MessageCircle } from 'lucide-react';

interface ChatEntryOrchestratorProps {
  className?: string;
}

export function ChatEntryOrchestrator({ className }: ChatEntryOrchestratorProps) {
  const router = useRouter();
  const searchParams = useSearchParams();

  const directGameId = searchParams.get('game') ?? searchParams.get('gameId');
  const preselectedKbIds = searchParams.get('kbIds')?.split(',').filter(Boolean);

  const [selectedGame, setSelectedGame] = useState<GameSelectorGame | null>(null);
  const [selectedAgent, setSelectedAgent] = useState<AgentOption | null>(null);
  const [isCreating, setIsCreating] = useState(false);

  const effectiveGameId = directGameId ?? selectedGame?.id;

  const handleGameSelect = useCallback((game: GameSelectorGame) => {
    setSelectedGame(game);
    setSelectedAgent(null);
  }, []);

  const handleStartChat = useCallback(async (initialMessage?: string) => {
    if (!effectiveGameId) return;
    setIsCreating(true);
    try {
      const threadId = await createChatThread({
        gameId: effectiveGameId,
        agentId: selectedAgent?.id,
        title: selectedGame?.title,
        initialMessage,
        selectedKnowledgeBaseIds: preselectedKbIds,
      });
      router.push(`/chat/${threadId}`);
    } finally {
      setIsCreating(false);
    }
  }, [effectiveGameId, selectedAgent, selectedGame, preselectedKbIds, router]);

  return (
    <div className={className}>
      {!directGameId && (
        <GameSelector
          onSelect={handleGameSelect}
          selectedGameId={selectedGame?.id}
          className="mb-6"
        />
      )}

      {effectiveGameId && (
        <>
          <AgentSelector
            gameId={effectiveGameId}
            onSelect={setSelectedAgent}
            selectedAgentId={selectedAgent?.id}
            className="mb-6"
          />

          <QuickStartSuggestions
            gameName={selectedGame?.title}
            onSelect={s => handleStartChat(s.message)}
            className="mb-6"
          />

          <Button
            onClick={() => handleStartChat()}
            disabled={isCreating}
            className="w-full"
            size="lg"
          >
            <MessageCircle className="mr-2 h-4 w-4" />
            {isCreating ? 'Creazione...' : 'Inizia Chat'}
          </Button>
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 6: Create barrel export**

```ts
// apps/web/src/components/chat/entry/index.ts
export { GameSelector, type GameSelectorGame } from './GameSelector';
export { AgentSelector, type AgentOption, SYSTEM_AGENTS } from './AgentSelector';
export { createChatThread, type CreateThreadParams } from './ThreadCreator';
export { QuickStartSuggestions, getQuickStartSuggestions, type QuickStartSuggestion } from './QuickStartSuggestions';
export { ChatEntryOrchestrator } from './ChatEntryOrchestrator';
```

- [ ] **Step 7: Replace NewChatView with thin wrapper**

Replace the 847-line `apps/web/src/components/chat-unified/NewChatView.tsx` with:

```tsx
'use client';

import { ChatEntryOrchestrator } from '@/components/chat/entry';

export default function NewChatView() {
  return (
    <div className="mx-auto max-w-4xl px-4 py-8">
      <h1 className="text-2xl font-bold font-quicksand mb-6">Nuova Chat</h1>
      <ChatEntryOrchestrator />
    </div>
  );
}
```

- [ ] **Step 8: Write component tests**

```tsx
// apps/web/src/__tests__/components/chat/entry/GameSelector.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { GameSelector } from '@/components/chat/entry/GameSelector';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibrary: () => ({ data: [
    { id: '1', gameTitle: 'Puerto Rico', imageUrl: null, hasKb: true },
    { id: '2', gameTitle: 'Catan', imageUrl: null, hasKb: false },
  ]}),
}));

vi.mock('@/hooks/queries/useSharedGames', () => ({
  useSharedGames: () => ({ data: { items: [] } }),
}));

describe('GameSelector', () => {
  it('renders games from library', () => {
    render(<GameSelector onSelect={vi.fn()} />);
    expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('calls onSelect when game clicked', () => {
    const onSelect = vi.fn();
    render(<GameSelector onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Puerto Rico'));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ title: 'Puerto Rico' }));
  });

  it('filters to KB-only when showOnlyWithKb', () => {
    render(<GameSelector onSelect={vi.fn()} showOnlyWithKb />);
    expect(screen.getByText('Puerto Rico')).toBeInTheDocument();
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });
});
```

```tsx
// apps/web/src/__tests__/components/chat/entry/AgentSelector.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AgentSelector } from '@/components/chat/entry/AgentSelector';

vi.mock('@/hooks/queries/useGameAgents', () => ({
  useGameAgents: () => ({ data: [
    { id: 'custom-1', name: 'My Agent', type: 'custom', description: 'Test agent' },
  ]}),
}));

describe('AgentSelector', () => {
  it('renders custom agents before system agents', () => {
    const onSelect = vi.fn();
    render(<AgentSelector gameId="game-1" onSelect={onSelect} />);
    const buttons = screen.getAllByRole('button');
    expect(buttons[0]).toHaveTextContent('My Agent');
  });

  it('renders all 5 system agents', () => {
    render(<AgentSelector gameId="game-1" onSelect={vi.fn()} />);
    expect(screen.getByText('Tutor')).toBeInTheDocument();
    expect(screen.getByText('Arbitro')).toBeInTheDocument();
    expect(screen.getByText('Stratega')).toBeInTheDocument();
  });

  it('calls onSelect with agent on click', () => {
    const onSelect = vi.fn();
    render(<AgentSelector gameId="game-1" onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Tutor'));
    expect(onSelect).toHaveBeenCalledWith(expect.objectContaining({ type: 'tutor' }));
  });
});
```

- [ ] **Step 9: Run all tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/chat/entry/`
Expected: ALL PASS

- [ ] **Step 10: Run typecheck and lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`
Expected: No errors

- [ ] **Step 11: Commit**

```
refactor(chat): decompose NewChatView into 5 focused components

Extract GameSelector, AgentSelector, ThreadCreator, QuickStartSuggestions,
and ChatEntryOrchestrator. NewChatView becomes thin wrapper.
Components are reusable by ChatSlideOverPanel.
```

---

### Task 5: Chat Without Library (Backend)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/CreateChatThreadCommandHandler.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetSharedGameKbStatusQuery.cs`
- Modify: `apps/api/src/Api/Routing/KnowledgeBaseEndpoints.cs`
- Test files

- [ ] **Step 1: Write test for thread creation without library entry**

```csharp
[Fact]
public async Task Should_Create_Thread_For_SharedGame_Without_LibraryEntry()
{
    var sharedGameId = await SeedSharedGameWithKb();
    var command = new CreateChatThreadCommand(
        UserId: _testUserId,
        GameId: sharedGameId,
        AgentType: "tutor"
    );

    var result = await _mediator.Send(command);

    result.Should().NotBeNull();
    result.Id.Should().NotBeEmpty();
}
```

- [ ] **Step 2: Run test — verify current behavior**

Run: `cd apps/api && dotnet test --filter "Should_Create_Thread_For_SharedGame_Without_LibraryEntry"`

If it passes already (handler doesn't check library ownership), proceed to Step 4.
If it fails, fix the handler in Step 3.

- [ ] **Step 3: Update handler if needed**

In `CreateChatThreadCommandHandler.cs`, ensure no ownership check blocks thread creation for shared games. The current handler (lines 41-57) resolves GameId and creates the thread — verify it does not query UserLibraryEntry.

- [ ] **Step 4: Create GetSharedGameKbStatusQuery**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetSharedGameKbStatusQuery.cs
using Api.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

public sealed record SharedGameKbStatusDto(
    Guid GameId,
    bool HasKb,
    int IndexedCount,
    int ProcessingCount
);

public sealed record GetSharedGameKbStatusQuery(Guid GameId) : IRequest<SharedGameKbStatusDto>;

internal sealed class GetSharedGameKbStatusQueryHandler(AppDbContext db)
    : IRequestHandler<GetSharedGameKbStatusQuery, SharedGameKbStatusDto>
{
    public async Task<SharedGameKbStatusDto> Handle(
        GetSharedGameKbStatusQuery request, CancellationToken cancellationToken)
    {
        var counts = await db.VectorDocuments
            .Where(v => v.GameId == request.GameId)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                IndexedCount = g.Count(v => v.IndexingStatus == "completed"),
                ProcessingCount = g.Count(v => v.IndexingStatus == "processing" || v.IndexingStatus == "pending"),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return new SharedGameKbStatusDto(
            request.GameId,
            HasKb: (counts?.IndexedCount ?? 0) > 0,
            IndexedCount: counts?.IndexedCount ?? 0,
            ProcessingCount: counts?.ProcessingCount ?? 0
        );
    }
}
```

- [ ] **Step 5: Wire endpoint**

Add to `KnowledgeBaseEndpoints.cs`:

```csharp
group.MapGet("/shared-games/{gameId:guid}/kb-status", async (Guid gameId, IMediator mediator) =>
    Results.Ok(await mediator.Send(new GetSharedGameKbStatusQuery(gameId))))
    .WithName("GetSharedGameKbStatus")
    .Produces<SharedGameKbStatusDto>();
```

- [ ] **Step 6: Write and run test for KbStatus query**

```csharp
[Fact]
public async Task Should_Return_Correct_Kb_Status()
{
    var gameId = await SeedSharedGameWithKb(indexedDocs: 3, processingDocs: 1);
    var result = await _mediator.Send(new GetSharedGameKbStatusQuery(gameId));

    result.HasKb.Should().BeTrue();
    result.IndexedCount.Should().Be(3);
    result.ProcessingCount.Should().Be(1);
}
```

Run: `cd apps/api && dotnet test --filter "SharedGameKbStatus"`
Expected: PASS

- [ ] **Step 7: Commit**

```
feat(api): allow chat thread creation for shared games without library entry

Add GetSharedGameKbStatusQuery for frontend to check KB availability.
Thread creation no longer requires UserLibraryEntry for shared games.
```

---

## Phase 3: Wiring

### Task 6: ChatSlideOverPanel Wiring + ManaPips Click

**Files:**
- Modify: `apps/web/src/hooks/queries/useGameManaPips.ts`
- Modify: `apps/web/src/components/chat/panel/ChatSlideOverPanel.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/sections/PersonalLibrarySection.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/sections/CatalogCarouselSection.tsx`

- [ ] **Step 1: Add onKbClick to ManaPips actions**

Modify `apps/web/src/hooks/queries/useGameManaPips.ts` — update `GameManaPipsActions`:

```tsx
export interface GameManaPipsActions {
  onCreateSession?: () => void;
  onCreateKb?: () => void;
  onCreateAgent?: () => void;
  onKbClick?: () => void;  // NEW: opens chat when KB is ready
}
```

In `buildGameManaPips`, update the KB pip:

```tsx
{
  entityType: 'kb' as MeepleEntityType,
  count: data.kbs.count,
  items: data.kbs.items,
  onCreate: (data.kbs.indexedCount ?? 0) > 0 ? actions?.onKbClick : actions?.onCreateKb,
  createLabel: (data.kbs.indexedCount ?? 0) > 0 ? 'Chatta con AI' : 'Carica PDF',
  colorOverride: getKbPipColor({
    kbIndexedCount: data.kbs.indexedCount ?? 0,
    kbProcessingCount: data.kbs.processingCount ?? 0,
  }),
},
```

- [ ] **Step 2: Update ChatSlideOverPanel for lazy thread creation**

In `apps/web/src/components/chat/panel/ChatSlideOverPanel.tsx`, import and use `createChatThread`:

```tsx
import { createChatThread } from '@/components/chat/entry/ThreadCreator';
```

Update `handleSend` to create thread lazily when `gameContext` is set but no `threadId`:

```tsx
const handleSend = useCallback(async (content: string) => {
  let currentThreadId = threadId;

  if (!currentThreadId && gameContext) {
    currentThreadId = await createChatThread({
      gameId: gameContext.id,
      agentType: 'auto',
      title: gameContext.name,
      initialMessage: content,
    });
    setThreadId(currentThreadId);
  }

  if (!currentThreadId) return;
  // ... existing SSE send logic continues
}, [threadId, gameContext]);
```

- [ ] **Step 3: Wire ManaPips on PersonalLibrarySection**

Modify `apps/web/src/app/(authenticated)/library/sections/PersonalLibrarySection.tsx`:

```tsx
import { useChatPanel } from '@/hooks/useChatPanel';
import { getKbPipColor, type ManaPip } from '@/components/ui/data-display/meeple-card/parts/ManaPips';

// Inside component:
const { open: openChat } = useChatPanel();

// Add helper:
function buildLibraryPips(game: PersonalLibraryGame): ManaPip[] {
  const kbIndexed = game.kbIndexedCount ?? 0;
  const kbProcessing = game.kbProcessingCount ?? 0;
  return [{
    entityType: 'kb' as const,
    count: game.kbCardCount ?? 0,
    colorOverride: getKbPipColor({ kbIndexedCount: kbIndexed, kbProcessingCount: kbProcessing }),
    onCreate: kbIndexed > 0
      ? () => openChat({ id: game.id, name: game.title, pdfCount: game.kbCardCount ?? 0, kbStatus: 'ready' })
      : undefined,
    createLabel: kbIndexed > 0 ? 'Chatta con AI' : undefined,
  }];
}

// In MeepleCard render, add prop:
<MeepleCard ... manaPips={buildLibraryPips(game)} />
```

- [ ] **Step 4: Wire ManaPips on CatalogCarouselSection similarly**

Same pattern in `apps/web/src/app/(authenticated)/library/sections/CatalogCarouselSection.tsx`.

- [ ] **Step 5: Run typecheck and tests**

Run: `cd apps/web && pnpm typecheck && pnpm vitest run`
Expected: PASS

- [ ] **Step 6: Commit**

```
feat(chat): wire ManaPips KB click to ChatSlideOverPanel with lazy thread creation

ManaPips on library cards now open slide-over chat when KB is indexed.
Thread created lazily on first message send. Default system auto agent.
```

---

### Task 7: Inline Citation Viewer (CitationExpander)

**Files:**
- Create: `apps/web/src/components/chat/panel/CitationExpander.tsx`
- Create: `apps/web/src/lib/api/clients/pdfPageClient.ts`
- Modify: `apps/web/src/components/chat/panel/ChatMessageBubble.tsx`
- Create: `apps/web/src/__tests__/components/chat/panel/CitationExpander.test.tsx`

- [ ] **Step 1: Create PDF page API client**

```ts
// apps/web/src/lib/api/clients/pdfPageClient.ts
import { httpClient } from '@/lib/api/http-client';

export interface PdfPageTextResponse {
  pageNumber: number;
  text: string;
  documentTitle: string;
  totalPages: number;
}

export const pdfPageApi = {
  getPageText: (pdfId: string, pageNumber: number): Promise<PdfPageTextResponse> =>
    httpClient.get(`/api/v1/pdfs/${pdfId}/pages/${pageNumber}/text`),
};
```

- [ ] **Step 2: Write CitationExpander test**

```tsx
// apps/web/src/__tests__/components/chat/panel/CitationExpander.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { CitationExpander } from '@/components/chat/panel/CitationExpander';

vi.mock('@/lib/api/clients/pdfPageClient', () => ({
  pdfPageApi: {
    getPageText: vi.fn().mockResolvedValue({
      pageNumber: 42,
      text: 'Fase di Draft: Ogni giocatore riceve 7 carte.',
      documentTitle: 'Regolamento',
      totalPages: 80,
    }),
  },
}));

describe('CitationExpander', () => {
  it('renders collapsed by default', () => {
    render(<CitationExpander pdfId="abc" pageNumber={42} />);
    expect(screen.queryByText('REGOLAMENTO — PAGINA 42')).not.toBeInTheDocument();
  });

  it('expands on click and shows page text', async () => {
    render(<CitationExpander pdfId="abc" pageNumber={42} />);
    fireEvent.click(screen.getByText(/p\.42/));
    await waitFor(() => {
      expect(screen.getByText('REGOLAMENTO — PAGINA 42')).toBeInTheDocument();
      expect(screen.getByText(/Fase di Draft/)).toBeInTheDocument();
    });
  });

  it('collapses on second click', async () => {
    render(<CitationExpander pdfId="abc" pageNumber={42} />);
    const badge = screen.getByText(/p\.42/);
    fireEvent.click(badge);
    await waitFor(() => expect(screen.getByText(/Fase di Draft/)).toBeInTheDocument());
    fireEvent.click(badge);
    expect(screen.queryByText(/Fase di Draft/)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/chat/panel/CitationExpander.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 4: Implement CitationExpander**

```tsx
// apps/web/src/components/chat/panel/CitationExpander.tsx
'use client';

import { useState, useCallback } from 'react';
import { pdfPageApi, type PdfPageTextResponse } from '@/lib/api/clients/pdfPageClient';
import { Loader2 } from 'lucide-react';

interface CitationExpanderProps {
  pdfId: string;
  pageNumber: number;
  documentName?: string;
}

export function CitationExpander({ pdfId, pageNumber, documentName }: CitationExpanderProps) {
  const [isExpanded, setIsExpanded] = useState(false);
  const [pageData, setPageData] = useState<PdfPageTextResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleClick = useCallback(async () => {
    if (isExpanded) {
      setIsExpanded(false);
      return;
    }
    setIsExpanded(true);
    if (!pageData) {
      setIsLoading(true);
      setError(null);
      try {
        const data = await pdfPageApi.getPageText(pdfId, pageNumber);
        setPageData(data);
      } catch {
        setError('Impossibile caricare il testo della pagina');
      } finally {
        setIsLoading(false);
      }
    }
  }, [isExpanded, pageData, pdfId, pageNumber]);

  const label = documentName ? `${documentName} p.${pageNumber}` : `Regolamento p.${pageNumber}`;

  return (
    <span className="inline">
      <button
        onClick={handleClick}
        className="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-xs font-medium bg-orange-500 hover:bg-orange-600 text-white transition-all cursor-pointer"
        aria-expanded={isExpanded}
      >
        📄 {label}
      </button>
      {isExpanded && (
        <div className="mt-2 ml-4 border-l-3 border-orange-400 bg-orange-50/50 dark:bg-orange-950/20 rounded-r-lg p-3">
          <div className="text-xs text-muted-foreground mb-2 uppercase tracking-wider font-semibold">
            {pageData?.documentTitle ?? documentName ?? 'REGOLAMENTO'} — PAGINA {pageNumber}
          </div>
          {isLoading && (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-3 w-3 animate-spin" /> Caricamento...
            </div>
          )}
          {error && <p className="text-sm text-destructive">{error}</p>}
          {pageData && !isLoading && (
            <div className="text-sm leading-relaxed whitespace-pre-wrap italic">
              {pageData.text}
            </div>
          )}
        </div>
      )}
    </span>
  );
}
```

- [ ] **Step 5: Run test to verify pass**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/chat/panel/CitationExpander.test.tsx`
Expected: PASS

- [ ] **Step 6: Integrate CitationExpander into ChatMessageBubble**

Modify `apps/web/src/components/chat/panel/ChatMessageBubble.tsx` — replace plain text rendering (line 93) with citation-aware rendering:

```tsx
import { CitationExpander } from './CitationExpander';

// Add helper function before component:
function renderContentWithCitations(content: string): React.ReactNode {
  // Match patterns like [cite:pdfId:pageNumber] or [cite:pdfId:pageNumber:docName]
  const citationRegex = /\[cite:([a-f0-9-]+):(\d+)(?::([^\]]+))?\]/g;
  const parts: React.ReactNode[] = [];
  let lastIndex = 0;
  let match: RegExpExecArray | null;

  while ((match = citationRegex.exec(content)) !== null) {
    if (match.index > lastIndex) {
      parts.push(content.slice(lastIndex, match.index));
    }
    parts.push(
      <CitationExpander
        key={`cite-${match[1]}-${match[2]}-${match.index}`}
        pdfId={match[1]}
        pageNumber={parseInt(match[2], 10)}
        documentName={match[3]}
      />
    );
    lastIndex = match.index + match[0].length;
  }

  if (lastIndex < content.length) {
    parts.push(content.slice(lastIndex));
  }

  return parts.length > 1 ? <>{parts}</> : content;
}

// In the component, replace line 93:
// BEFORE: <p className="whitespace-pre-wrap">{content}</p>
// AFTER:
<div className="whitespace-pre-wrap">{renderContentWithCitations(content)}</div>
```

- [ ] **Step 7: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 8: Commit**

```
feat(chat): inline citation viewer — click citation badge to expand page text

CitationExpander renders as accordion inside chat messages.
Fetches extracted text from page text endpoint. No PDF binary served.
```

---

### Task 8: AI Chat Tab — Real Bridge

**Files:**
- Modify: `apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx`

- [ ] **Step 1: Replace placeholder with real bridge**

```tsx
// apps/web/src/components/game-detail/tabs/GameAiChatTab.tsx
'use client';

import { useMemo } from 'react';
import { useRouter } from 'next/navigation';
import { useChatPanel } from '@/hooks/useChatPanel';
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';
import { getKbPipColor } from '@/components/ui/data-display/meeple-card/parts/ManaPips';
import { Button } from '@/components/ui/button';
import { MessageCircle, ExternalLink } from 'lucide-react';
import type { GameTabProps } from './types';

export function GameAiChatTab({ gameId }: GameTabProps) {
  const router = useRouter();
  const { open: openChat } = useChatPanel();

  const { data: threads } = useQuery({
    queryKey: ['chat-threads', gameId],
    queryFn: () => api.chat.getThreadsByGame(gameId),
    enabled: !!gameId,
  });

  const { data: kbStatus } = useQuery({
    queryKey: ['kb-status', gameId],
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId),
    enabled: !!gameId,
  });

  const indexedCount = useMemo(
    () => (kbStatus ?? []).filter(d => d.processingState === 'Ready' || d.processingState === 'Completed').length,
    [kbStatus]
  );
  const processingCount = useMemo(
    () => (kbStatus ?? []).filter(d => d.processingState === 'Processing' || d.processingState === 'Pending').length,
    [kbStatus]
  );

  const kbColor = getKbPipColor({ kbIndexedCount: indexedCount, kbProcessingCount: processingCount });
  const hasKb = indexedCount > 0;

  return (
    <div className="space-y-6 p-4">
      <div className="flex items-center gap-3">
        <div className="h-3 w-3 rounded-full" style={{ backgroundColor: kbColor }} />
        <span className="text-sm text-muted-foreground">
          {hasKb
            ? `${indexedCount} documento${indexedCount > 1 ? 'i' : ''} indicizzat${indexedCount > 1 ? 'i' : 'o'}`
            : processingCount > 0
              ? `${processingCount} documento${processingCount > 1 ? 'i' : ''} in elaborazione...`
              : 'Nessun documento caricato'}
        </span>
      </div>

      {hasKb && (
        <Button
          onClick={() => openChat({ id: gameId, name: '', pdfCount: indexedCount, kbStatus: 'ready' })}
          className="w-full" size="lg"
        >
          <MessageCircle className="mr-2 h-4 w-4" /> Chatta con AI
        </Button>
      )}

      {hasKb && (
        <Button variant="outline" onClick={() => router.push(`/chat/new?game=${gameId}`)} className="w-full">
          <ExternalLink className="mr-2 h-4 w-4" /> Apri chat completa
        </Button>
      )}

      {(threads ?? []).length > 0 && (
        <div>
          <h3 className="text-sm font-semibold mb-3">Conversazioni precedenti</h3>
          <div className="space-y-2">
            {(threads ?? []).map(thread => (
              <button
                key={thread.id}
                onClick={() => router.push(`/chat/${thread.id}`)}
                className="w-full text-left p-3 rounded-lg border hover:border-orange-400 transition-colors"
              >
                <div className="font-medium text-sm">{thread.title ?? 'Chat'}</div>
                <div className="text-xs text-muted-foreground">
                  {new Date(thread.createdAt).toLocaleDateString('it-IT')}
                </div>
              </button>
            ))}
          </div>
        </div>
      )}

      {!hasKb && processingCount === 0 && (
        <p className="text-sm text-muted-foreground text-center py-4">
          Carica un PDF del regolamento per attivare la chat AI.
        </p>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: PASS

- [ ] **Step 3: Commit**

```
feat(game): replace AI Chat tab placeholder with real bridge

Shows KB status, quick chat CTA (slide-over), full chat CTA,
and existing conversation list. No more placeholder.
```

---

## Phase 4: Polish

### Task 9: Side Panel Citation Viewer (Full Chat)

**Files:**
- Create: `apps/web/src/components/chat/viewer/PageViewerPanel.tsx`
- Modify: `apps/web/src/components/chat-unified/ChatThreadView.tsx`
- Create: `apps/web/src/__tests__/components/chat/viewer/PageViewerPanel.test.tsx`

- [ ] **Step 1: Write PageViewerPanel test**

```tsx
// apps/web/src/__tests__/components/chat/viewer/PageViewerPanel.test.tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { PageViewerPanel } from '@/components/chat/viewer/PageViewerPanel';

vi.mock('@/lib/api/clients/pdfPageClient', () => ({
  pdfPageApi: {
    getPageText: vi.fn().mockResolvedValue({
      pageNumber: 5,
      text: 'Page 5 content here.',
      documentTitle: 'Regolamento',
      totalPages: 20,
    }),
  },
}));

describe('PageViewerPanel', () => {
  it('renders page text when open', async () => {
    render(<PageViewerPanel pdfId="abc" pageNumber={5} isOpen onClose={vi.fn()} />);
    await waitFor(() => expect(screen.getByText('Page 5 content here.')).toBeInTheDocument());
  });

  it('shows navigation controls', async () => {
    render(<PageViewerPanel pdfId="abc" pageNumber={5} isOpen onClose={vi.fn()} />);
    await waitFor(() => {
      expect(screen.getByLabelText('Pagina precedente')).toBeInTheDocument();
      expect(screen.getByLabelText('Pagina successiva')).toBeInTheDocument();
    });
  });

  it('calls onClose when X clicked', () => {
    const onClose = vi.fn();
    render(<PageViewerPanel pdfId="abc" pageNumber={5} isOpen onClose={onClose} />);
    fireEvent.click(screen.getByLabelText('Chiudi'));
    expect(onClose).toHaveBeenCalled();
  });

  it('returns null when not open', () => {
    const { container } = render(
      <PageViewerPanel pdfId="abc" pageNumber={5} isOpen={false} onClose={vi.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Implement PageViewerPanel**

```tsx
// apps/web/src/components/chat/viewer/PageViewerPanel.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { pdfPageApi, type PdfPageTextResponse } from '@/lib/api/clients/pdfPageClient';
import { Button } from '@/components/ui/button';
import { X, ChevronLeft, ChevronRight, Loader2 } from 'lucide-react';

interface PageViewerPanelProps {
  pdfId: string;
  pageNumber: number;
  isOpen: boolean;
  onClose: () => void;
}

export function PageViewerPanel({ pdfId, pageNumber: initialPage, isOpen, onClose }: PageViewerPanelProps) {
  const [currentPage, setCurrentPage] = useState(initialPage);
  const [pageData, setPageData] = useState<PdfPageTextResponse | null>(null);
  const [isLoading, setIsLoading] = useState(false);

  const fetchPage = useCallback(async (page: number) => {
    setIsLoading(true);
    try {
      const data = await pdfPageApi.getPageText(pdfId, page);
      setPageData(data);
      setCurrentPage(page);
    } catch { /* keep current page data */ }
    finally { setIsLoading(false); }
  }, [pdfId]);

  useEffect(() => {
    if (isOpen) fetchPage(initialPage);
  }, [isOpen, initialPage, fetchPage]);

  useEffect(() => {
    if (!isOpen) return;
    const handleKeyDown = (e: KeyboardEvent) => { if (e.key === 'Escape') onClose(); };
    window.addEventListener('keydown', handleKeyDown);
    return () => window.removeEventListener('keydown', handleKeyDown);
  }, [isOpen, onClose]);

  if (!isOpen) return null;

  return (
    <div className="w-[400px] border-l border-border flex flex-col bg-background h-full">
      <div className="flex items-center justify-between px-4 py-3 border-b border-border">
        <span className="text-sm font-semibold truncate">{pageData?.documentTitle ?? 'Documento'}</span>
        <Button variant="ghost" size="icon" onClick={onClose} aria-label="Chiudi">
          <X className="h-4 w-4" />
        </Button>
      </div>
      <div className="flex items-center justify-center gap-4 px-4 py-2 border-b border-border">
        <Button variant="ghost" size="icon"
          onClick={() => fetchPage(currentPage - 1)}
          disabled={currentPage <= 1 || isLoading}
          aria-label="Pagina precedente">
          <ChevronLeft className="h-4 w-4" />
        </Button>
        <span className="text-sm">
          p.<strong>{currentPage}</strong>{pageData?.totalPages ? ` / ${pageData.totalPages}` : ''}
        </span>
        <Button variant="ghost" size="icon"
          onClick={() => fetchPage(currentPage + 1)}
          disabled={currentPage >= (pageData?.totalPages ?? currentPage) || isLoading}
          aria-label="Pagina successiva">
          <ChevronRight className="h-4 w-4" />
        </Button>
      </div>
      <div className="flex-1 overflow-y-auto p-4">
        {isLoading && (
          <div className="flex items-center justify-center py-8">
            <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
          </div>
        )}
        {!isLoading && pageData && (
          <div className="text-sm leading-relaxed whitespace-pre-wrap">{pageData.text}</div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm vitest run src/__tests__/components/chat/viewer/`
Expected: PASS

- [ ] **Step 4: Integrate PageViewerPanel in ChatThreadView**

Modify `apps/web/src/components/chat-unified/ChatThreadView.tsx`:

```tsx
import { PageViewerPanel } from '@/components/chat/viewer/PageViewerPanel';

// Add state:
const [viewerState, setViewerState] = useState<{ pdfId: string; pageNumber: number } | null>(null);

// Wrap main content in flex:
<div className="flex h-full">
  <div className="flex-1 flex flex-col">
    {/* existing ChatMainArea + ChatInputBar */}
  </div>
  {viewerState && (
    <PageViewerPanel
      pdfId={viewerState.pdfId}
      pageNumber={viewerState.pageNumber}
      isOpen={!!viewerState}
      onClose={() => setViewerState(null)}
    />
  )}
</div>
```

- [ ] **Step 5: Commit**

```
feat(chat): side panel citation viewer for full-screen chat

PageViewerPanel shows extracted text with page navigation.
Opens alongside chat in split view. Escape to close.
```

---

### Task 10: Search Ranking Boost for AI-Ready Games

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/SearchSharedGamesQueryHandler.cs`
- Test file

- [ ] **Step 1: Write test**

```csharp
[Fact]
public async Task Should_Rank_Games_With_KB_Higher_When_No_Explicit_Sort()
{
    var gameWithKb = await SeedSharedGame("Alpha Game", hasKb: true);
    var gameWithoutKb = await SeedSharedGame("Beta Game", hasKb: false);

    var query = new SearchSharedGamesQuery { Search = "Game" };

    var result = await _mediator.Send(query);

    result.Items.First().Id.Should().Be(gameWithKb.Id);
}
```

- [ ] **Step 2: Implement ranking boost**

In `SearchSharedGamesQueryHandler.cs`, modify the default sort case (line 173):

```csharp
// BEFORE:
_ => query.SortDescending
    ? dbQuery.OrderByDescending(g => g.Title)
    : dbQuery.OrderBy(g => g.Title)

// AFTER:
_ => dbQuery
    .OrderBy(g => g.HasIndexedKb ? 0 : 1)
    .ThenBy(g => g.Title)
```

- [ ] **Step 3: Run test**

Run: `cd apps/api && dotnet test --filter "Should_Rank_Games_With_KB_Higher"`
Expected: PASS

- [ ] **Step 4: Commit**

```
feat(search): boost AI-ready games in default search ranking
```

---

### Task 11: Integration Smoke Test + Cleanup

- [ ] **Step 1: Run full frontend test suite**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm test`
Expected: ALL PASS

- [ ] **Step 2: Run full backend test suite**

Run: `cd apps/api && dotnet test`
Expected: ALL PASS

- [ ] **Step 3: Verify no unused imports or dead code**

Run: `cd apps/web && pnpm lint --fix`

- [ ] **Step 4: Final commit if any cleanup**

```
chore: cleanup lint fixes and unused imports
```

- [ ] **Step 5: Create PR to main-dev**

```
gh pr create --base main-dev --title "feat: UX core flow — search, chat, PDF, discovery-to-chat"
```
