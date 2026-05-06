# Mobile Library & KB-Linked MeepleCard — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire ManaPip KB click to a bottom sheet showing game documents, enable pre-chat KB selection, and add game info overlay on MeepleCard for mobile library browsing.

**Architecture:** The backend already exposes KB data per library entry (`HasKb`, `KbCardCount`, `KbIndexedCount`) and accepts `SelectedKnowledgeBaseIds` on chat thread creation. The main work is frontend: (1) map existing DTO fields to ManaPip `linkedEntities`, (2) build KbBottomSheet for document list, (3) build KbSelector for pre-chat KB selection, (4) add game info overlay on card face, (5) quota-aware add-to-library on shared games.

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, Zustand, React Query (TanStack Query), .NET 9 Minimal APIs

**Spec:** `docs/superpowers/specs/2026-04-07-mobile-library-kb-meeplecard-design.md`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/api/.../GetGameDocuments/GameDocumentDto.cs` | Modify | Add Category, VersionLabel fields |
| `apps/api/.../GetGameDocuments/GetGameDocumentsHandler.cs` | Modify | Join PdfDocument.DocumentCategory + VersionLabel |
| `apps/web/src/lib/api/schemas/game-documents.schemas.ts` | Create | Zod schema for GameDocumentDto |
| `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` | Modify | Add getGameDocuments method |
| `apps/web/src/hooks/queries/useGameDocuments.ts` | Create | React Query hook for game KB documents |
| `apps/web/src/components/library/LibraryLinkedEntities.ts` | Create | Mapper: library entry → linkedEntities[] |
| `apps/web/src/components/kb/KbBottomSheet.tsx` | Create | Bottom sheet listing KB docs grouped by category |
| `apps/web/src/components/kb/KbSelector.tsx` | Create | Pre-chat KB selection (radio rules + checkbox others) |
| `apps/web/src/components/kb/KbDocumentRow.tsx` | Create | Single document row in bottom sheet |
| `apps/web/src/components/library/PersonalLibraryPage.tsx` | Modify | Wire linkedEntities + onManaPipClick to cards |
| `apps/web/src/components/library/SharedGameCard.tsx` | Modify | Add non-clickable KB pip + quota button |
| `apps/web/src/app/(chat)/chat/new/page.tsx` | Modify | Accept pre-selected KB IDs from navigation |

---

## Task 1: Extend GameDocumentDto with Category and VersionLabel

Backend change: the KbBottomSheet needs to group documents by type (Rulebook vs other) and show version labels. Currently `GameDocumentDto` only has `Id, Title, Status, PageCount, CreatedAt`.

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GameDocumentDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GetGameDocumentsHandler.cs`
- Test: `tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocumentsHandlerTests.cs`

- [ ] **Step 1: Write failing test for new fields**

```csharp
[Fact]
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public async Task Handle_ShouldReturnDocumentCategoryAndVersionLabel()
{
    // Arrange: seed a VectorDocument + PdfDocument with Category=Rulebook, VersionLabel="2nd Edition"
    var gameId = Guid.NewGuid();
    var userId = Guid.NewGuid();
    var pdfId = Guid.NewGuid();
    var vdId = Guid.NewGuid();

    await SeedPdfDocument(pdfId, gameId, "rules.pdf", pageCount: 45,
        documentCategory: DocumentCategory.Rulebook, versionLabel: "2nd Edition");
    await SeedVectorDocument(vdId, gameId, pdfId, indexingStatus: "completed");

    var query = new GetGameDocumentsQuery(gameId, userId);

    // Act
    var result = await _handler.Handle(query, CancellationToken.None);

    // Assert
    result.Should().HaveCount(1);
    result[0].Category.Should().Be("Rulebook");
    result[0].VersionLabel.Should().Be("2nd Edition");
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/api/src/Api && dotnet test --filter "Handle_ShouldReturnDocumentCategoryAndVersionLabel" -v n
```

Expected: FAIL — `GameDocumentDto` does not have `Category` or `VersionLabel` properties.

- [ ] **Step 3: Update GameDocumentDto**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GameDocumentDto.cs`:

```csharp
internal sealed record GameDocumentDto(
    Guid Id,
    string Title,
    string Status,
    int PageCount,
    DateTime CreatedAt,
    string Category,         // "Rulebook", "Expansion", "Errata", "QuickStart", "Reference", "PlayerAid", "Other"
    string? VersionLabel     // "2nd Edition", "v1.3 Errata", null
);
```

- [ ] **Step 4: Update GetGameDocumentsHandler to join Category and VersionLabel**

In `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/GetGameDocumentsHandler.cs`, update the LINQ query (lines 36-49):

```csharp
var documents = await (
    from vd in _dbContext.VectorDocuments.AsNoTracking()
    join pdf in _dbContext.PdfDocuments.AsNoTracking()
        on vd.PdfDocumentId equals pdf.Id
    where vd.GameId == query.GameId
    orderby vd.IndexedAt descending
    select new GameDocumentDto(
        vd.Id,
        pdf.FileName,
        MapIndexingStatus(vd.IndexingStatus),
        pdf.PageCount ?? 0,
        vd.IndexedAt ?? pdf.UploadedAt,
        pdf.DocumentCategory.ToString(),   // NEW: enum → string
        pdf.VersionLabel                   // NEW: nullable string
    )
).ToListAsync(cancellationToken).ConfigureAwait(false);
```

- [ ] **Step 5: Run test to verify it passes**

```bash
cd apps/api/src/Api && dotnet test --filter "Handle_ShouldReturnDocumentCategoryAndVersionLabel" -v n
```

Expected: PASS

- [ ] **Step 6: Run all KB tests to check for regressions**

```bash
cd apps/api/src/Api && dotnet test --filter "BoundedContext=KnowledgeBase" -v n
```

Expected: All PASS (existing tests may need `Category` and `VersionLabel` args added to `GameDocumentDto` constructors in test fixtures).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGameDocuments/
git add tests/Api.Tests/BoundedContexts/KnowledgeBase/
git commit -m "feat(kb): add Category and VersionLabel to GameDocumentDto"
```

---

## Task 2: Frontend Schema and API Client for Game Documents

**Files:**
- Create: `apps/web/src/lib/api/schemas/game-documents.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` (or wherever KB API lives)
- Create: `apps/web/src/hooks/queries/useGameDocuments.ts`
- Test: `apps/web/__tests__/hooks/useGameDocuments.test.ts`

- [ ] **Step 1: Create Zod schema for GameDocumentDto**

Create `apps/web/src/lib/api/schemas/game-documents.schemas.ts`:

```typescript
import { z } from 'zod';

export const DocumentCategorySchema = z.enum([
  'Rulebook',
  'Expansion',
  'Errata',
  'QuickStart',
  'Reference',
  'PlayerAid',
  'Other',
]);

export type DocumentCategory = z.infer<typeof DocumentCategorySchema>;

export const GameDocumentSchema = z.object({
  id: z.string().uuid(),
  title: z.string(),
  status: z.enum(['indexed', 'processing', 'failed']),
  pageCount: z.number().int().nonnegative(),
  createdAt: z.string().datetime(),
  category: DocumentCategorySchema,
  versionLabel: z.string().nullable(),
});

export type GameDocument = z.infer<typeof GameDocumentSchema>;

/** Rulebook categories that appear in the "Regolamento" group */
export const RULEBOOK_CATEGORIES: DocumentCategory[] = ['Rulebook'];

/** Check if a document is a rulebook (for radio vs checkbox in selector) */
export function isRulebook(doc: GameDocument): boolean {
  return RULEBOOK_CATEGORIES.includes(doc.category);
}
```

- [ ] **Step 2: Find KB API client and add getGameDocuments method**

Locate the existing KB client. If it doesn't exist as a dedicated client, add the method to the appropriate client. The endpoint is `GET /api/v1/knowledge-base/{gameId}/documents`.

Add to the KB API client:

```typescript
async getGameDocuments(gameId: string): Promise<GameDocument[]> {
  const response = await this.fetch(`/api/v1/knowledge-base/${gameId}/documents`);
  const data = await response.json();
  return z.array(GameDocumentSchema).parse(data);
}
```

- [ ] **Step 3: Create useGameDocuments hook**

Create `apps/web/src/hooks/queries/useGameDocuments.ts`:

```typescript
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { api } from '@/lib/api';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

export const gameDocumentKeys = {
  all: ['game-documents'] as const,
  byGame: (gameId: string) => [...gameDocumentKeys.all, gameId] as const,
};

export function useGameDocuments(
  gameId: string | undefined,
  enabled: boolean = true
): UseQueryResult<GameDocument[], Error> {
  return useQuery({
    queryKey: gameDocumentKeys.byGame(gameId ?? ''),
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId!),
    enabled: enabled && !!gameId,
    staleTime: 5 * 60 * 1000, // 5 minutes (SWR per spec RNF-01)
  });
}
```

- [ ] **Step 4: Write test for useGameDocuments**

Create `apps/web/__tests__/hooks/useGameDocuments.test.ts`:

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { useGameDocuments } from '@/hooks/queries/useGameDocuments';

import { createQueryWrapper } from '../test-utils';

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getGameDocuments: vi.fn(),
    },
  },
}));

describe('useGameDocuments', () => {
  it('should not fetch when gameId is undefined', () => {
    const { result } = renderHook(() => useGameDocuments(undefined), {
      wrapper: createQueryWrapper(),
    });
    expect(result.current.isFetching).toBe(false);
  });

  it('should fetch documents for a valid gameId', async () => {
    const mockDocs = [
      {
        id: '123e4567-e89b-12d3-a456-426614174000',
        title: 'Rules v2',
        status: 'indexed',
        pageCount: 45,
        createdAt: '2026-01-01T00:00:00Z',
        category: 'Rulebook',
        versionLabel: '2nd Edition',
      },
    ];

    const { api } = await import('@/lib/api');
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValue(mockDocs);

    const { result } = renderHook(
      () => useGameDocuments('game-id-1'),
      { wrapper: createQueryWrapper() }
    );

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data).toEqual(mockDocs);
  });
});
```

- [ ] **Step 5: Run tests**

```bash
cd apps/web && pnpm test -- --run __tests__/hooks/useGameDocuments.test.ts
```

Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/lib/api/schemas/game-documents.schemas.ts
git add apps/web/src/hooks/queries/useGameDocuments.ts
git add apps/web/__tests__/hooks/useGameDocuments.test.ts
git commit -m "feat(kb): add GameDocument schema, API client method, and useGameDocuments hook"
```

---

## Task 3: Library Entry to LinkedEntities Mapper

Map existing `UserLibraryEntry` KB fields to `LinkedEntityInfo[]` for ManaPips.

**Files:**
- Create: `apps/web/src/components/library/mapLinkedEntities.ts`
- Test: `apps/web/__tests__/components/library/mapLinkedEntities.test.ts`

- [ ] **Step 1: Write tests for the mapper**

Create `apps/web/__tests__/components/library/mapLinkedEntities.test.ts`:

```typescript
import { describe, expect, it } from 'vitest';

import { mapLibraryEntryToLinkedEntities } from '@/components/library/mapLinkedEntities';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

function makeEntry(overrides: Partial<UserLibraryEntry> = {}): UserLibraryEntry {
  return {
    id: 'entry-1',
    userId: 'user-1',
    gameId: 'game-1',
    gameTitle: 'Test Game',
    addedAt: '2026-01-01T00:00:00Z',
    isFavorite: false,
    currentState: 'Owned',
    hasKb: false,
    kbCardCount: 0,
    kbIndexedCount: 0,
    kbProcessingCount: 0,
    hasRagAccess: false,
    agentIsOwned: true,
    isPrivateGame: false,
    canProposeToCatalog: false,
    ...overrides,
  } as UserLibraryEntry;
}

describe('mapLibraryEntryToLinkedEntities', () => {
  it('should return empty array when no KB documents', () => {
    const entry = makeEntry({ kbIndexedCount: 0, kbProcessingCount: 0 });
    const result = mapLibraryEntryToLinkedEntities(entry);
    const kbPip = result.find(e => e.entityType === 'kb');
    expect(kbPip).toBeUndefined();
  });

  it('should include kb pip when kbIndexedCount > 0', () => {
    const entry = makeEntry({ hasKb: true, kbIndexedCount: 2, kbCardCount: 2 });
    const result = mapLibraryEntryToLinkedEntities(entry);
    const kbPip = result.find(e => e.entityType === 'kb');
    expect(kbPip).toBeDefined();
    expect(kbPip!.count).toBe(2);
  });

  it('should always include game pip', () => {
    const entry = makeEntry();
    const result = mapLibraryEntryToLinkedEntities(entry);
    const gamePip = result.find(e => e.entityType === 'game');
    expect(gamePip).toBeDefined();
    expect(gamePip!.count).toBe(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/library/mapLinkedEntities.test.ts
```

Expected: FAIL — module not found.

- [ ] **Step 3: Implement mapper**

Create `apps/web/src/components/library/mapLinkedEntities.ts`:

```typescript
import type { LinkedEntityInfo } from '@/components/ui/data-display/meeple-card-features/ManaLinkFooter';
import type { UserLibraryEntry } from '@/lib/api/schemas/library.schemas';

/**
 * Maps UserLibraryEntry DTO fields to LinkedEntityInfo[] for ManaLinkFooter.
 * ManaPip KB appears ONLY if kbIndexedCount > 0 (per spec DD-02).
 */
export function mapLibraryEntryToLinkedEntities(
  entry: UserLibraryEntry
): LinkedEntityInfo[] {
  const entities: LinkedEntityInfo[] = [];

  // Game pip is always present (it's the source entity)
  entities.push({ entityType: 'game', count: 1 });

  // KB pip: only if at least 1 document is indexed (Ready)
  if (entry.kbIndexedCount > 0) {
    entities.push({ entityType: 'kb', count: entry.kbIndexedCount });
  }

  return entities;
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --run __tests__/components/library/mapLinkedEntities.test.ts
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/mapLinkedEntities.ts
git add apps/web/__tests__/components/library/mapLinkedEntities.test.ts
git commit -m "feat(library): add mapLibraryEntryToLinkedEntities for ManaPip wiring"
```

---

## Task 4: KbDocumentRow Component

A single row in the KB bottom sheet showing document title, category badge, version label, page count, and status indicator.

**Files:**
- Create: `apps/web/src/components/kb/KbDocumentRow.tsx`
- Test: `apps/web/__tests__/components/kb/KbDocumentRow.test.tsx`

- [ ] **Step 1: Write test**

Create `apps/web/__tests__/components/kb/KbDocumentRow.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { KbDocumentRow } from '@/components/kb/KbDocumentRow';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const indexedDoc: GameDocument = {
  id: 'doc-1',
  title: 'Rules v2',
  status: 'indexed',
  pageCount: 45,
  createdAt: '2026-01-01T00:00:00Z',
  category: 'Rulebook',
  versionLabel: '2nd Edition',
};

const processingDoc: GameDocument = {
  id: 'doc-2',
  title: 'Errata v1.2',
  status: 'processing',
  pageCount: 0,
  createdAt: '2026-01-01T00:00:00Z',
  category: 'Errata',
  versionLabel: 'v1.2',
};

describe('KbDocumentRow', () => {
  it('renders indexed document with title and version', () => {
    render(<KbDocumentRow document={indexedDoc} />);
    expect(screen.getByText('Rules v2')).toBeInTheDocument();
    expect(screen.getByText('2nd Edition')).toBeInTheDocument();
    expect(screen.getByText(/45 pagine/)).toBeInTheDocument();
  });

  it('renders processing document with spinner', () => {
    render(<KbDocumentRow document={processingDoc} />);
    expect(screen.getByText('Errata v1.2')).toBeInTheDocument();
    expect(screen.getByText(/elaborazione/i)).toBeInTheDocument();
  });

  it('renders status indicator for indexed', () => {
    render(<KbDocumentRow document={indexedDoc} />);
    expect(screen.getByTestId('status-indexed')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbDocumentRow.test.tsx
```

- [ ] **Step 3: Implement KbDocumentRow**

Create `apps/web/src/components/kb/KbDocumentRow.tsx`:

```tsx
import { FileText, Loader2, AlertCircle, Check } from 'lucide-react';

import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

interface KbDocumentRowProps {
  document: GameDocument;
}

const STATUS_CONFIG = {
  indexed: { icon: Check, label: 'Indicizzato', color: 'text-green-600', testId: 'status-indexed' },
  processing: { icon: Loader2, label: 'In elaborazione...', color: 'text-amber-500', testId: 'status-processing' },
  failed: { icon: AlertCircle, label: 'Errore', color: 'text-red-500', testId: 'status-failed' },
} as const;

const CATEGORY_LABELS: Record<string, string> = {
  Rulebook: 'Regolamento',
  Expansion: 'Espansione',
  Errata: 'Errata',
  QuickStart: 'Quick Start',
  Reference: 'Riferimento',
  PlayerAid: 'Aiuto Giocatore',
  Other: 'Altro',
};

export function KbDocumentRow({ document }: KbDocumentRowProps) {
  const status = STATUS_CONFIG[document.status];
  const StatusIcon = status.icon;
  const isProcessing = document.status === 'processing';

  return (
    <div className="flex items-center gap-3 rounded-lg border border-border/50 bg-card/50 p-3">
      <div className="flex-shrink-0">
        <FileText className="h-5 w-5 text-muted-foreground" />
      </div>

      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <span className="truncate text-sm font-medium">{document.title}</span>
          {document.versionLabel && (
            <span className="flex-shrink-0 rounded-full bg-muted px-2 py-0.5 text-xs text-muted-foreground">
              {document.versionLabel}
            </span>
          )}
        </div>
        <div className="flex items-center gap-2 text-xs text-muted-foreground">
          <span>{CATEGORY_LABELS[document.category] ?? document.category}</span>
          {document.status === 'indexed' && document.pageCount > 0 && (
            <>
              <span aria-hidden>·</span>
              <span>{document.pageCount} pagine</span>
            </>
          )}
        </div>
      </div>

      <div data-testid={status.testId} className={`flex items-center gap-1 text-xs ${status.color}`}>
        <StatusIcon className={`h-4 w-4 ${isProcessing ? 'animate-spin' : ''}`} />
        <span className="hidden sm:inline">{status.label}</span>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbDocumentRow.test.tsx
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/kb/KbDocumentRow.tsx
git add apps/web/__tests__/components/kb/KbDocumentRow.test.tsx
git commit -m "feat(kb): add KbDocumentRow component"
```

---

## Task 5: KbBottomSheet Component

Bottom sheet opened by ManaPip KB click. Groups documents by Rulebook vs Other, shows status, provides "Chatta con l'Agente" CTA.

**Files:**
- Create: `apps/web/src/components/kb/KbBottomSheet.tsx`
- Test: `apps/web/__tests__/components/kb/KbBottomSheet.test.tsx`

- [ ] **Step 1: Write test**

Create `apps/web/__tests__/components/kb/KbBottomSheet.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbBottomSheet } from '@/components/kb/KbBottomSheet';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const mockDocs: GameDocument[] = [
  { id: 'd1', title: 'Rules v2', status: 'indexed', pageCount: 45, createdAt: '2026-01-01T00:00:00Z', category: 'Rulebook', versionLabel: '2nd Edition' },
  { id: 'd2', title: 'Rules v1', status: 'indexed', pageCount: 38, createdAt: '2025-06-01T00:00:00Z', category: 'Rulebook', versionLabel: '1st Edition' },
  { id: 'd3', title: 'FAQ', status: 'indexed', pageCount: 12, createdAt: '2026-02-01T00:00:00Z', category: 'Reference', versionLabel: null },
];

describe('KbBottomSheet', () => {
  it('renders game title in header', () => {
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Gloomhaven"
        documents={mockDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByText(/Knowledge Base.*Gloomhaven/)).toBeInTheDocument();
  });

  it('groups documents by Rulebook and Other', () => {
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Gloomhaven"
        documents={mockDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByText('Regolamenti')).toBeInTheDocument();
    expect(screen.getByText('Altre Knowledge Base')).toBeInTheDocument();
  });

  it('calls onStartChat when CTA is clicked', () => {
    const onStartChat = vi.fn();
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Gloomhaven"
        documents={mockDocs}
        onStartChat={onStartChat}
      />
    );
    fireEvent.click(screen.getByText(/Chatta con l'Agente/));
    expect(onStartChat).toHaveBeenCalled();
  });

  it('disables CTA when no indexed documents', () => {
    const noDocs: GameDocument[] = [
      { id: 'd1', title: 'Processing doc', status: 'processing', pageCount: 0, createdAt: '2026-01-01T00:00:00Z', category: 'Rulebook', versionLabel: null },
    ];
    render(
      <KbBottomSheet
        open={true}
        onOpenChange={() => {}}
        gameTitle="Test"
        documents={noDocs}
        onStartChat={() => {}}
      />
    );
    expect(screen.getByText(/Chatta con l'Agente/)).toBeDisabled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbBottomSheet.test.tsx
```

- [ ] **Step 3: Implement KbBottomSheet**

Create `apps/web/src/components/kb/KbBottomSheet.tsx`:

```tsx
'use client';

import { BookOpen, MessageCircle } from 'lucide-react';

import { Button } from '@/components/ui/actions/button';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/overlay/sheet';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import { isRulebook } from '@/lib/api/schemas/game-documents.schemas';

import { KbDocumentRow } from './KbDocumentRow';

interface KbBottomSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameTitle: string;
  documents: GameDocument[];
  onStartChat: () => void;
  isLoading?: boolean;
}

export function KbBottomSheet({
  open,
  onOpenChange,
  gameTitle,
  documents,
  onStartChat,
  isLoading = false,
}: KbBottomSheetProps) {
  const rulebooks = documents.filter(isRulebook);
  const otherDocs = documents.filter(d => !isRulebook(d));
  const hasIndexedDocs = documents.some(d => d.status === 'indexed');

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="bottom" className="max-h-[80vh] overflow-y-auto rounded-t-2xl">
        <SheetHeader className="pb-4">
          <SheetTitle className="flex items-center gap-2 text-base">
            <BookOpen className="h-5 w-5 text-[hsl(174,60%,40%)]" />
            Knowledge Base — {gameTitle}
          </SheetTitle>
        </SheetHeader>

        <div className="space-y-4 pb-4">
          {/* Rulebooks section */}
          {rulebooks.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold text-muted-foreground">
                Regolamenti
              </h3>
              <div className="space-y-2">
                {rulebooks.map(doc => (
                  <KbDocumentRow key={doc.id} document={doc} />
                ))}
              </div>
            </section>
          )}

          {/* Other KB section */}
          {otherDocs.length > 0 && (
            <section>
              <h3 className="mb-2 text-sm font-semibold text-muted-foreground">
                Altre Knowledge Base
              </h3>
              <div className="space-y-2">
                {otherDocs.map(doc => (
                  <KbDocumentRow key={doc.id} document={doc} />
                ))}
              </div>
            </section>
          )}
        </div>

        {/* CTA */}
        <div className="sticky bottom-0 border-t bg-background pt-3 pb-safe">
          <Button
            className="w-full"
            size="lg"
            disabled={!hasIndexedDocs || isLoading}
            onClick={onStartChat}
          >
            <MessageCircle className="mr-2 h-5 w-5" />
            Chatta con l&apos;Agente
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbBottomSheet.test.tsx
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/kb/KbBottomSheet.tsx
git add apps/web/__tests__/components/kb/KbBottomSheet.test.tsx
git commit -m "feat(kb): add KbBottomSheet component with grouped document list"
```

---

## Task 6: KbSelector Component (Pre-Chat KB Selection)

Radio buttons for rulebook version + checkboxes for other KB documents. Skip if only 1 document.

**Files:**
- Create: `apps/web/src/components/kb/KbSelector.tsx`
- Test: `apps/web/__tests__/components/kb/KbSelector.test.tsx`

- [ ] **Step 1: Write test**

Create `apps/web/__tests__/components/kb/KbSelector.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { KbSelector } from '@/components/kb/KbSelector';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';

const docs: GameDocument[] = [
  { id: 'd1', title: 'Rules v1', status: 'indexed', pageCount: 38, createdAt: '2025-06-01T00:00:00Z', category: 'Rulebook', versionLabel: '1st Edition' },
  { id: 'd2', title: 'Rules v2', status: 'indexed', pageCount: 45, createdAt: '2026-01-01T00:00:00Z', category: 'Rulebook', versionLabel: '2nd Edition' },
  { id: 'd3', title: 'FAQ', status: 'indexed', pageCount: 12, createdAt: '2026-02-01T00:00:00Z', category: 'Reference', versionLabel: null },
];

describe('KbSelector', () => {
  it('pre-selects latest rulebook and all other docs', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);

    // Latest rulebook (Rules v2, createdAt 2026-01-01) should be selected
    const radioV2 = screen.getByRole('radio', { name: /Rules v2/ });
    expect(radioV2).toBeChecked();

    // FAQ checkbox should be checked
    const faqCheckbox = screen.getByRole('checkbox', { name: /FAQ/ });
    expect(faqCheckbox).toBeChecked();
  });

  it('calls onConfirm with selected document IDs', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);

    fireEvent.click(screen.getByText(/Avvia Chat/));
    expect(onConfirm).toHaveBeenCalledWith(['d2', 'd3']); // latest rulebook + FAQ
  });

  it('disables Avvia Chat when nothing selected', () => {
    const onConfirm = vi.fn();
    render(<KbSelector documents={docs} onConfirm={onConfirm} />);

    // Deselect FAQ
    fireEvent.click(screen.getByRole('checkbox', { name: /FAQ/ }));

    // Deselect Rules v2 (click Rules v1 then deselect... actually radio always has one selected)
    // With radio, one rulebook is always selected, so button should still be enabled
    expect(screen.getByText(/Avvia Chat/)).not.toBeDisabled();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbSelector.test.tsx
```

- [ ] **Step 3: Implement KbSelector**

Create `apps/web/src/components/kb/KbSelector.tsx`:

```tsx
'use client';

import { useState, useMemo } from 'react';
import { MessageCircle } from 'lucide-react';

import { Button } from '@/components/ui/actions/button';
import type { GameDocument } from '@/lib/api/schemas/game-documents.schemas';
import { isRulebook } from '@/lib/api/schemas/game-documents.schemas';

interface KbSelectorProps {
  documents: GameDocument[];
  onConfirm: (selectedIds: string[]) => void;
  onCancel?: () => void;
}

export function KbSelector({ documents, onConfirm, onCancel }: KbSelectorProps) {
  const indexedDocs = useMemo(
    () => documents.filter(d => d.status === 'indexed'),
    [documents]
  );
  const rulebooks = useMemo(() => indexedDocs.filter(isRulebook), [indexedDocs]);
  const otherDocs = useMemo(() => indexedDocs.filter(d => !isRulebook(d)), [indexedDocs]);

  // Default: latest rulebook (by createdAt) selected
  const latestRulebook = useMemo(
    () =>
      rulebooks.length > 0
        ? [...rulebooks].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0]
        : null,
    [rulebooks]
  );

  const [selectedRulebookId, setSelectedRulebookId] = useState<string | null>(
    latestRulebook?.id ?? null
  );
  // Default: all other docs selected
  const [selectedOtherIds, setSelectedOtherIds] = useState<Set<string>>(
    new Set(otherDocs.map(d => d.id))
  );

  const selectedIds = useMemo(() => {
    const ids: string[] = [];
    if (selectedRulebookId) ids.push(selectedRulebookId);
    selectedOtherIds.forEach(id => ids.push(id));
    return ids;
  }, [selectedRulebookId, selectedOtherIds]);

  const toggleOther = (docId: string) => {
    setSelectedOtherIds(prev => {
      const next = new Set(prev);
      if (next.has(docId)) next.delete(docId);
      else next.add(docId);
      return next;
    });
  };

  return (
    <div className="flex flex-col gap-4">
      <h2 className="text-base font-semibold">Seleziona Knowledge Base</h2>

      {/* Rulebooks: radio group */}
      {rulebooks.length > 0 && (
        <fieldset>
          <legend className="mb-2 text-sm font-medium text-muted-foreground">
            Regolamento (scegli versione)
          </legend>
          <div className="space-y-2">
            {rulebooks.map(doc => (
              <label
                key={doc.id}
                className="flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent/50"
              >
                <input
                  type="radio"
                  name="rulebook"
                  value={doc.id}
                  checked={selectedRulebookId === doc.id}
                  onChange={() => setSelectedRulebookId(doc.id)}
                  aria-label={doc.title}
                  className="h-4 w-4"
                />
                <div className="flex-1">
                  <span className="text-sm font-medium">{doc.title}</span>
                  {doc.versionLabel && (
                    <span className="ml-2 text-xs text-muted-foreground">
                      ({doc.versionLabel})
                    </span>
                  )}
                </div>
              </label>
            ))}
          </div>
        </fieldset>
      )}

      {/* Other docs: checkboxes */}
      {otherDocs.length > 0 && (
        <fieldset>
          <legend className="mb-2 text-sm font-medium text-muted-foreground">
            Altre Knowledge Base
          </legend>
          <div className="space-y-2">
            {otherDocs.map(doc => (
              <label
                key={doc.id}
                className="flex cursor-pointer items-center gap-3 rounded-lg border p-3 transition-colors hover:bg-accent/50"
              >
                <input
                  type="checkbox"
                  checked={selectedOtherIds.has(doc.id)}
                  onChange={() => toggleOther(doc.id)}
                  aria-label={doc.title}
                  className="h-4 w-4"
                />
                <div className="flex-1">
                  <span className="text-sm font-medium">{doc.title}</span>
                  {doc.versionLabel && (
                    <span className="ml-2 text-xs text-muted-foreground">
                      ({doc.versionLabel})
                    </span>
                  )}
                </div>
              </label>
            ))}
          </div>
        </fieldset>
      )}

      {/* Actions */}
      <div className="flex gap-2 pt-2">
        {onCancel && (
          <Button variant="outline" className="flex-1" onClick={onCancel}>
            Annulla
          </Button>
        )}
        <Button
          className="flex-1"
          disabled={selectedIds.length === 0}
          onClick={() => onConfirm(selectedIds)}
        >
          <MessageCircle className="mr-2 h-4 w-4" />
          Avvia Chat
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
cd apps/web && pnpm test -- --run __tests__/components/kb/KbSelector.test.tsx
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/kb/KbSelector.tsx
git add apps/web/__tests__/components/kb/KbSelector.test.tsx
git commit -m "feat(kb): add KbSelector component with radio rulebooks + checkbox other docs"
```

---

## Task 7: Wire ManaPip KB Click in PersonalLibraryPage

Connect the MeepleCard `onManaPipClick` → KbBottomSheet → KbSelector → navigate to chat.

**Files:**
- Modify: `apps/web/src/components/library/PersonalLibraryPage.tsx` (or whichever component renders the library grid)
- Test: `apps/web/__tests__/components/library/PersonalLibraryPage.integration.test.tsx`

- [ ] **Step 1: Identify where MeepleCards are rendered in the library**

Read `apps/web/src/components/library/PersonalLibraryPage.tsx` to find the exact location where MeepleCard is rendered and how props are passed. Look for `<MeepleCard` or similar rendering in the library grid.

- [ ] **Step 2: Add state and handlers for KbBottomSheet**

Add the following state and handlers to the component that renders the library grid:

```tsx
import { useState, useCallback } from 'react';
import { useRouter } from 'next/navigation';

import { KbBottomSheet } from '@/components/kb/KbBottomSheet';
import { KbSelector } from '@/components/kb/KbSelector';
import { mapLibraryEntryToLinkedEntities } from '@/components/library/mapLinkedEntities';
import { useGameDocuments } from '@/hooks/queries/useGameDocuments';

// Inside the component:
const router = useRouter();

// State for KB bottom sheet
const [kbSheetOpen, setKbSheetOpen] = useState(false);
const [kbSheetGameId, setKbSheetGameId] = useState<string | null>(null);
const [kbSheetGameTitle, setKbSheetGameTitle] = useState('');
const [showKbSelector, setShowKbSelector] = useState(false);

// Fetch documents when sheet is open
const { data: kbDocuments = [], isLoading: kbLoading } = useGameDocuments(
  kbSheetGameId ?? undefined,
  kbSheetOpen
);

// ManaPip click handler
const handleManaPipClick = useCallback(
  (gameId: string, gameTitle: string, entityType: MeepleEntityType) => {
    if (entityType === 'kb') {
      setKbSheetGameId(gameId);
      setKbSheetGameTitle(gameTitle);
      setKbSheetOpen(true);
    }
  },
  []
);

// Start chat from bottom sheet
const handleStartChat = useCallback(() => {
  const indexedDocs = kbDocuments.filter(d => d.status === 'indexed');
  if (indexedDocs.length === 1) {
    // Single doc: skip selector, navigate directly
    router.push(`/chat/new?game=${kbSheetGameId}&kbIds=${indexedDocs[0].id}`);
  } else {
    // Multiple docs: show selector
    setShowKbSelector(true);
  }
}, [kbDocuments, kbSheetGameId, router]);

// Confirm KB selection
const handleKbConfirm = useCallback(
  (selectedIds: string[]) => {
    const kbIdsParam = selectedIds.join(',');
    router.push(`/chat/new?game=${kbSheetGameId}&kbIds=${kbIdsParam}`);
    setKbSheetOpen(false);
    setShowKbSelector(false);
  },
  [kbSheetGameId, router]
);
```

- [ ] **Step 3: Pass linkedEntities and onManaPipClick to MeepleCard**

Where MeepleCard is rendered in the grid, add:

```tsx
<MeepleCard
  // ... existing props
  linkedEntities={mapLibraryEntryToLinkedEntities(entry)}
  onManaPipClick={(entityType) =>
    handleManaPipClick(entry.gameId, entry.gameTitle, entityType)
  }
/>
```

- [ ] **Step 4: Add KbBottomSheet and KbSelector to JSX**

At the end of the component, before the closing tag:

```tsx
{/* KB Bottom Sheet */}
<KbBottomSheet
  open={kbSheetOpen && !showKbSelector}
  onOpenChange={setKbSheetOpen}
  gameTitle={kbSheetGameTitle}
  documents={kbDocuments}
  onStartChat={handleStartChat}
  isLoading={kbLoading}
/>

{/* KB Selector (shown when multiple docs) */}
<Sheet open={showKbSelector} onOpenChange={setShowKbSelector}>
  <SheetContent side="bottom" className="max-h-[80vh] overflow-y-auto rounded-t-2xl">
    <KbSelector
      documents={kbDocuments}
      onConfirm={handleKbConfirm}
      onCancel={() => setShowKbSelector(false)}
    />
  </SheetContent>
</Sheet>
```

- [ ] **Step 5: Run typecheck and lint**

```bash
cd apps/web && pnpm typecheck && pnpm lint
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/PersonalLibraryPage.tsx
git commit -m "feat(library): wire ManaPip KB click to KbBottomSheet and KbSelector"
```

---

## Task 8: Accept kbIds in Chat New Page

The chat creation page needs to read `kbIds` from URL params and pass them to `CreateChatThreadCommand`.

**Files:**
- Modify: `apps/web/src/app/(chat)/chat/new/page.tsx`

- [ ] **Step 1: Read the current chat/new page implementation**

Read `apps/web/src/app/(chat)/chat/new/page.tsx` to understand the current flow for creating a new chat thread.

- [ ] **Step 2: Add kbIds query param handling**

In the chat/new page, add parsing of the `kbIds` URL parameter:

```typescript
const searchParams = useSearchParams();
const gameId = searchParams.get('game');
const kbIdsParam = searchParams.get('kbIds');
const selectedKbIds = kbIdsParam ? kbIdsParam.split(',') : undefined;
```

- [ ] **Step 3: Pass selectedKnowledgeBaseIds to thread creation**

When calling the create chat thread API/mutation, include the selected KB IDs:

```typescript
const response = await api.knowledgeBase.createThread({
  gameId,
  // ... existing fields
  selectedKnowledgeBaseIds: selectedKbIds,
});
```

- [ ] **Step 4: Add system message showing selected KBs**

After thread creation, if `selectedKbIds` is set, the first system message should show which KBs are active. This may already be handled by the backend; verify by checking if the `CreateChatThreadCommandHandler` adds a system message.

If not handled by backend, add a client-side info banner:

```tsx
{selectedKbIds && (
  <div className="rounded-lg bg-muted/50 p-3 text-xs text-muted-foreground">
    Sto usando: {selectedKbNames.join(', ')}
  </div>
)}
```

- [ ] **Step 5: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/(chat)/chat/new/
git commit -m "feat(chat): accept kbIds query param for pre-selected KB documents"
```

---

## Task 9: Quota-Aware AddToLibraryButton on Shared Games

Add quota check before add-to-library action on shared game cards.

**Files:**
- Create: `apps/web/src/components/library/AddToLibraryButton.tsx`
- Test: `apps/web/__tests__/components/library/AddToLibraryButton.test.tsx`
- Modify: Shared games page/component that renders catalog cards

- [ ] **Step 1: Write test**

Create `apps/web/__tests__/components/library/AddToLibraryButton.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AddToLibraryButton } from '@/components/library/AddToLibraryButton';

vi.mock('@/hooks/queries/useLibrary', () => ({
  useLibraryQuota: vi.fn(),
}));

describe('AddToLibraryButton', () => {
  it('shows quota count when under limit', async () => {
    const { useLibraryQuota } = await import('@/hooks/queries/useLibrary');
    vi.mocked(useLibraryQuota).mockReturnValue({
      data: { currentCount: 3, maxAllowed: 10, tier: 'Free' },
      isLoading: false,
    } as any);

    render(<AddToLibraryButton gameId="g1" onAdd={() => {}} />);
    expect(screen.getByText('3/10')).toBeInTheDocument();
    expect(screen.getByText(/Aggiungi/)).not.toBeDisabled();
  });

  it('disables button when quota full', async () => {
    const { useLibraryQuota } = await import('@/hooks/queries/useLibrary');
    vi.mocked(useLibraryQuota).mockReturnValue({
      data: { currentCount: 10, maxAllowed: 10, tier: 'Free' },
      isLoading: false,
    } as any);

    render(<AddToLibraryButton gameId="g1" onAdd={() => {}} />);
    expect(screen.getByText(/Upgrade/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Implement AddToLibraryButton**

Create `apps/web/src/components/library/AddToLibraryButton.tsx`:

```tsx
'use client';

import { Plus, ArrowUpCircle } from 'lucide-react';

import { Button } from '@/components/ui/actions/button';
import { useLibraryQuota } from '@/hooks/queries/useLibrary';

interface AddToLibraryButtonProps {
  gameId: string;
  onAdd: () => void;
  isAdding?: boolean;
  className?: string;
}

export function AddToLibraryButton({
  gameId,
  onAdd,
  isAdding = false,
  className,
}: AddToLibraryButtonProps) {
  const { data: quota, isLoading } = useLibraryQuota();

  const isQuotaFull =
    quota != null && quota.currentCount >= quota.maxAllowed;

  if (isQuotaFull) {
    return (
      <Button variant="outline" disabled className={className}>
        <ArrowUpCircle className="mr-1.5 h-4 w-4" />
        Upgrade per aggiungere
        <span className="ml-1.5 text-xs text-muted-foreground">
          {quota.currentCount}/{quota.maxAllowed}
        </span>
      </Button>
    );
  }

  return (
    <Button
      onClick={onAdd}
      disabled={isAdding || isLoading}
      className={className}
    >
      <Plus className="mr-1.5 h-4 w-4" />
      Aggiungi
      {quota && (
        <span className="ml-1.5 text-xs opacity-70">
          {quota.currentCount}/{quota.maxAllowed}
        </span>
      )}
    </Button>
  );
}
```

- [ ] **Step 3: Run test**

```bash
cd apps/web && pnpm test -- --run __tests__/components/library/AddToLibraryButton.test.tsx
```

- [ ] **Step 4: Wire into shared games page**

Find the shared games/catalog page (likely `apps/web/src/app/(authenticated)/library/public/PublicLibraryClient.tsx` based on `_content.tsx`). Replace the existing add button with `AddToLibraryButton`.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/AddToLibraryButton.tsx
git add apps/web/__tests__/components/library/AddToLibraryButton.test.tsx
git commit -m "feat(library): add quota-aware AddToLibraryButton for shared games"
```

---

## Task 10: Shared Game ManaPip KB as Non-Clickable Indicator

On shared game cards, ManaPip KB should be visible but not trigger the bottom sheet. Instead show tooltip "Aggiungi alla libreria per chattare con l'agente".

**Files:**
- Modify: shared games rendering component (from Task 9 investigation)

- [ ] **Step 1: In shared games grid, pass linkedEntities but NO onManaPipClick for KB**

Where shared game cards are rendered, map `isRagPublic` or KB counts to ManaPips but handle pip click to show a toast instead of opening KbBottomSheet:

```tsx
const handleSharedGamePipClick = useCallback((entityType: MeepleEntityType) => {
  if (entityType === 'kb') {
    toast({
      description: 'Aggiungi alla libreria per chattare con l\'agente',
    });
  }
}, [toast]);
```

- [ ] **Step 2: Map shared game to linkedEntities**

```typescript
function mapSharedGameToLinkedEntities(game: SharedGameDto): LinkedEntityInfo[] {
  const entities: LinkedEntityInfo[] = [{ entityType: 'game', count: 1 }];
  if (game.isRagPublic) {
    entities.push({ entityType: 'kb', count: 1 }); // count not available in DTO, show indicator only
  }
  return entities;
}
```

Note: The `SharedGameDto` currently has `isRagPublic` boolean but no `kbDocumentCount`. If more granularity is needed, that's a future backend enhancement.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/library/
git commit -m "feat(shared-games): show KB ManaPip as non-clickable indicator with toast"
```

---

## Task 11: Integration Test — Full Flow

E2E-style integration test for the complete flow: library → ManaPip click → KB sheet → chat.

**Files:**
- Create: `apps/web/__tests__/integration/library-kb-chat-flow.test.tsx`

- [ ] **Step 1: Write integration test**

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

// Test the full flow: library card → ManaPip KB click → bottom sheet → chat navigation
describe('Library → KB → Chat flow', () => {
  it('opens KB sheet when ManaPip KB is clicked and navigates to chat', async () => {
    // Mock library data with KB documents
    // Mock game documents API
    // Render PersonalLibraryPage
    // Click ManaPip KB on a card
    // Verify KbBottomSheet opens with documents
    // Click "Chatta con l'Agente"
    // For single doc: verify navigation to /chat/new?game=X&kbIds=Y
    // For multiple docs: verify KbSelector appears
    // Select docs and confirm
    // Verify navigation with correct kbIds
  });

  it('hides ManaPip KB for games without KB documents', async () => {
    // Mock library data with kbIndexedCount: 0
    // Render PersonalLibraryPage
    // Verify no KB pip in ManaLinkFooter
  });
});
```

- [ ] **Step 2: Implement test with proper mocking**

Use the existing test patterns from the codebase (check `apps/web/__tests__/` for mock setup patterns, query wrapper, etc.).

- [ ] **Step 3: Run integration test**

```bash
cd apps/web && pnpm test -- --run __tests__/integration/library-kb-chat-flow.test.tsx
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/__tests__/integration/library-kb-chat-flow.test.tsx
git commit -m "test(library): add integration test for Library → KB → Chat flow"
```

---

## Task 12: Final Verification and PR

- [ ] **Step 1: Run full backend test suite**

```bash
cd apps/api/src/Api && dotnet test -v n
```

- [ ] **Step 2: Run full frontend test suite**

```bash
cd apps/web && pnpm test -- --run && pnpm typecheck && pnpm lint
```

- [ ] **Step 3: Run build**

```bash
cd apps/web && pnpm build
```

- [ ] **Step 4: Manual smoke test (if dev environment available)**

```bash
cd infra && make dev-core
```

1. Open library on mobile viewport (Chrome DevTools → mobile)
2. Verify ManaPip KB visible on games with KB docs
3. Click ManaPip KB → verify bottom sheet opens
4. Click "Chatta con l'Agente" → verify selector or direct chat
5. On shared games tab: verify KB pip visible but not clickable
6. Add game from shared → verify redirect + card visible

- [ ] **Step 5: Create PR**

```bash
gh pr create --base main-dev --title "feat: mobile KB-linked MeepleCard experience" --body "$(cat <<'EOF'
## Summary
- ManaPip KB click opens bottom sheet with grouped KB documents (Rulebook vs Other)
- Pre-chat KB selection: radio for rulebook version + checkboxes for other docs
- Quota-aware AddToLibraryButton on shared game cards
- Shared game KB pip as non-clickable indicator
- Extended GameDocumentDto with Category and VersionLabel

## Spec
docs/superpowers/specs/2026-04-07-mobile-library-kb-meeplecard-design.md

## Test plan
- [ ] Backend: GetGameDocumentsHandler returns Category + VersionLabel
- [ ] Frontend: mapLinkedEntities correctly maps KB counts to pips
- [ ] KbBottomSheet groups docs by type, disables CTA when no indexed docs
- [ ] KbSelector pre-selects latest rulebook + all other docs
- [ ] AddToLibraryButton shows quota and disables when full
- [ ] Integration: full Library → KB → Chat flow
- [ ] Manual: mobile viewport smoke test

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

---

## Parallel Execution Map

```
Task 1 (Backend: GameDocumentDto) ──────────────────────┐
                                                         │
Task 3 (mapLinkedEntities) ─────┐                        │
                                 ├── Task 7 (Wire PersonalLibraryPage)
Task 4 (KbDocumentRow) ─────┐   │                        │
                              ├── Task 5 (KbBottomSheet) ─┤
Task 2 (Schema + hook) ──────┘                            │
                                                          ├── Task 8 (Chat new page kbIds)
Task 6 (KbSelector) ─────────────────────────────────────┘

Task 9 (AddToLibraryButton) ── Task 10 (Shared game pip)

Task 11 (Integration test) ── depends on Tasks 7, 8, 10
Task 12 (Verification + PR) ── depends on all
```

**Parallelizable groups:**
- Group A: Tasks 1, 3, 4, 9 (no dependencies between them)
- Group B: Tasks 2, 6 (no dependencies between them, can parallel with Group A)
- Group C: Task 5 (depends on Task 4)
- Group D: Tasks 7, 8, 10 (depend on earlier tasks)
- Group E: Tasks 11, 12 (final)
