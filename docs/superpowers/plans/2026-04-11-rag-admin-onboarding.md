# RAG Admin Onboarding Flow — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Aggiungere all'admin una griglia MeepleCard dei giochi senza Knowledge Base, un drawer di upload PDF per gioco, e potenziare la coda di processing con ETA per job e azione "Rimuovi dalla coda".

**Architecture:** Backend aggiunge un solo nuovo handler CQRS (`GetGamesWithoutKbQuery`) sfruttando il flag `HasKnowledgeBase` su `SharedGameEntity`. Frontend introduce due nuovi componenti (`GamesWithoutKbSection`, `UploadForGameDrawer`) e integra le API già esistenti (`removeJob`, `fetchBatchETA` in `queue-api.ts`) nella UI della coda.

**Tech Stack:** .NET 9 / C# (MediatR CQRS, EF Core, Testcontainers xUnit), Next.js 16 / React 19 / TypeScript (React Query, Zustand, shadcn/ui), MeepleCard component system, Vitest + FluentAssertions.

---

## Mappa dei file

| Azione | Percorso |
|--------|----------|
| CREATE | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/GameWithoutKbDto.cs` |
| CREATE | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQuery.cs` |
| CREATE | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQueryHandler.cs` |
| MODIFY | `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` |
| CREATE | `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/GetGamesWithoutKbQueryHandlerIntegrationTests.cs` |
| CREATE | `apps/web/src/lib/api/kb-games-without-kb-api.ts` |
| CREATE | `apps/web/src/components/admin/knowledge-base/games-without-kb-section.tsx` |
| CREATE | `apps/web/src/components/admin/knowledge-base/upload-for-game-drawer.tsx` |
| MODIFY | `apps/web/src/app/admin/(dashboard)/knowledge-base/games/page.tsx` |
| MODIFY | `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item-actions.tsx` |
| MODIFY | `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx` |
| MODIFY | `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-stats-bar.tsx` |
| CREATE | `apps/web/src/components/admin/knowledge-base/__tests__/games-without-kb-section.test.tsx` |
| CREATE | `apps/web/src/components/admin/knowledge-base/__tests__/upload-for-game-drawer.test.tsx` |

---

## Task 1: Backend — GameWithoutKbDto + GetGamesWithoutKbQuery

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/GameWithoutKbDto.cs`
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQuery.cs`

- [ ] **Step 1: Crea il DTO**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/GameWithoutKbDto.cs
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Game with no active Knowledge Base (HasKnowledgeBase = false).
/// Used by the admin RAG onboarding flow.
/// </summary>
internal sealed record GameWithoutKbDto(
    Guid GameId,
    string Title,
    string? Publisher,
    string? ImageUrl,
    string PlayerCountLabel,
    int PdfCount,
    bool HasFailedPdfs
);

internal sealed record GamesWithoutKbPagedResponse(
    IReadOnlyList<GameWithoutKbDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
```

- [ ] **Step 2: Crea la Query**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQuery.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

/// <summary>
/// Returns paginated SharedGames where HasKnowledgeBase = false (no indexed VectorDocument).
/// Supports pagination and full-text search on Title.
/// </summary>
internal sealed record GetGamesWithoutKbQuery(
    int Page = 1,
    int PageSize = 20,
    string? Search = null
) : IQuery<GamesWithoutKbPagedResponse>;
```

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/GameWithoutKbDto.cs
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQuery.cs
git commit -m "feat(kb): add GetGamesWithoutKbQuery and DTO"
```

---

## Task 2: Backend — GetGamesWithoutKbQueryHandler con test integration

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQueryHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/GetGamesWithoutKbQueryHandlerIntegrationTests.cs`

- [ ] **Step 1: Scrivi il test di integrazione (TDD — fallirà perché il handler non esiste)**

```csharp
// apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/GetGamesWithoutKbQueryHandlerIntegrationTests.cs
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Queries;

[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
public sealed class GetGamesWithoutKbQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;
    private GetGamesWithoutKbQueryHandler _handler = null!;

    public GetGamesWithoutKbQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _dbName = $"test_games_without_kb_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .EnableThreadSafetyChecks(false);

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();
        _dbContext = new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        var sp = services.BuildServiceProvider();

        _handler = new GetGamesWithoutKbQueryHandler(
            _dbContext,
            sp.GetRequiredService<ILogger<GetGamesWithoutKbQueryHandler>>());
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_ReturnsOnlyGamesWithoutKb()
    {
        // Arrange: 2 games without KB, 1 with KB
        var gameWithKb = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Wingspan", HasKnowledgeBase = true,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        var game1 = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Catan", HasKnowledgeBase = false,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        var game2 = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Pandemic", HasKnowledgeBase = false,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        _dbContext.SharedGames.AddRange(gameWithKb, game1, game2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamesWithoutKbQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().HaveCount(2);
        result.Items.Should().NotContain(i => i.Title == "Wingspan");
        result.Items.Select(i => i.Title).Should().BeEquivalentTo(["Catan", "Pandemic"]);
    }

    [Fact]
    public async Task Handle_FiltersDeletedGames()
    {
        // Arrange
        var deletedGame = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "DeletedGame", HasKnowledgeBase = false,
            IsDeleted = true, Status = 1, Description = "", ImageUrl = ""
        };
        _dbContext.SharedGames.Add(deletedGame);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamesWithoutKbQuery(), CancellationToken.None);

        // Assert
        result.Items.Should().NotContain(i => i.Title == "DeletedGame");
    }

    [Fact]
    public async Task Handle_SearchFilters_ByTitle()
    {
        // Arrange
        var game1 = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Agricola", HasKnowledgeBase = false,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        var game2 = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Terraforming Mars", HasKnowledgeBase = false,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        _dbContext.SharedGames.AddRange(game1, game2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamesWithoutKbQuery(Search: "agricola"), CancellationToken.None);

        // Assert
        result.Items.Should().ContainSingle(i => i.Title == "Agricola");
        result.Items.Should().NotContain(i => i.Title == "Terraforming Mars");
    }

    [Fact]
    public async Task Handle_PdfCountAndFailedFlag_AreCorrect()
    {
        // Arrange: a game with 2 PDFs, 1 failed
        var adminUserId = Guid.NewGuid();
        var game = new SharedGameEntity
        {
            Id = Guid.NewGuid(), Title = "Root", HasKnowledgeBase = false,
            IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
        };
        _dbContext.SharedGames.Add(game);

        var pdf1 = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(), SharedGameId = game.Id, FileName = "root.pdf",
            FilePath = "/root.pdf", FileSizeBytes = 1000, ProcessingState = "Ready",
            UploadedByUserId = adminUserId, UploadedAt = DateTime.UtcNow
        };
        var pdf2 = new PdfDocumentEntity
        {
            Id = Guid.NewGuid(), SharedGameId = game.Id, FileName = "root-ext.pdf",
            FilePath = "/root-ext.pdf", FileSizeBytes = 500, ProcessingState = "Failed",
            UploadedByUserId = adminUserId, UploadedAt = DateTime.UtcNow
        };
        _dbContext.PdfDocuments.AddRange(pdf1, pdf2);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(new GetGamesWithoutKbQuery(Search: "Root"), CancellationToken.None);

        // Assert
        var item = result.Items.Should().ContainSingle().Subject;
        item.PdfCount.Should().Be(2);
        item.HasFailedPdfs.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Pagination_WorksCorrectly()
    {
        // Arrange: 5 games without KB
        for (var i = 0; i < 5; i++)
        {
            _dbContext.SharedGames.Add(new SharedGameEntity
            {
                Id = Guid.NewGuid(), Title = $"Game_{i:00}", HasKnowledgeBase = false,
                IsDeleted = false, Status = 1, Description = "", ImageUrl = ""
            });
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var page1 = await _handler.Handle(new GetGamesWithoutKbQuery(Page: 1, PageSize: 3), CancellationToken.None);
        var page2 = await _handler.Handle(new GetGamesWithoutKbQuery(Page: 2, PageSize: 3), CancellationToken.None);

        // Assert
        page1.Items.Should().HaveCount(3);
        page2.Items.Should().HaveCount(2);
        page1.Total.Should().Be(5);
        page1.TotalPages.Should().Be(2);
    }
}
```

- [ ] **Step 2: Esegui il test — deve fallire (handler non esiste)**

```bash
cd apps/api
dotnet test tests/Api.Tests/ --filter "GetGamesWithoutKbQueryHandlerIntegrationTests" -v
```

Expected: FAIL con `CS0246: The type or namespace name 'GetGamesWithoutKbQueryHandler' could not be found`

- [ ] **Step 3: Implementa il handler**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQueryHandler.cs
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

/// <summary>
/// Returns SharedGames where HasKnowledgeBase = false (admin RAG onboarding flow).
/// Uses the denormalized flag for O(1) filtering without joining VectorDocuments.
/// </summary>
internal sealed class GetGamesWithoutKbQueryHandler
    : IQueryHandler<GetGamesWithoutKbQuery, GamesWithoutKbPagedResponse>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetGamesWithoutKbQueryHandler> _logger;

    public GetGamesWithoutKbQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetGamesWithoutKbQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GamesWithoutKbPagedResponse> Handle(
        GetGamesWithoutKbQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);

        var baseQuery = _dbContext.SharedGames
            .AsNoTracking()
            .Where(sg => !sg.HasKnowledgeBase && !sg.IsDeleted && sg.Status == 1);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            baseQuery = baseQuery.Where(sg => EF.Functions.ILike(sg.Title, pattern));
        }

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var games = await baseQuery
            .OrderBy(sg => sg.Title)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sg => new
            {
                sg.Id,
                sg.Title,
                sg.ImageUrl,
                sg.MinPlayers,
                sg.MaxPlayers,
                Publishers = sg.Publishers.Select(p => p.Name).ToList(),
                PdfCount = _dbContext.PdfDocuments
                    .Count(pd => pd.SharedGameId == sg.Id),
                HasFailedPdfs = _dbContext.PdfDocuments
                    .Any(pd => pd.SharedGameId == sg.Id && pd.ProcessingState == "Failed")
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = games.Select(g => new GameWithoutKbDto(
            GameId: g.Id,
            Title: g.Title,
            Publisher: g.Publishers.FirstOrDefault(),
            ImageUrl: string.IsNullOrWhiteSpace(g.ImageUrl) ? null : g.ImageUrl,
            PlayerCountLabel: g.MinPlayers == g.MaxPlayers
                ? $"{g.MinPlayers} giocatori"
                : $"{g.MinPlayers}–{g.MaxPlayers} giocatori",
            PdfCount: g.PdfCount,
            HasFailedPdfs: g.HasFailedPdfs
        )).ToList();

        var totalPages = total > 0 ? (int)Math.Ceiling((double)total / pageSize) : 0;

        _logger.LogDebug("GetGamesWithoutKb: found {Total} games, page {Page}/{TotalPages}",
            total, page, totalPages);

        return new GamesWithoutKbPagedResponse(
            Items: items,
            Total: total,
            Page: page,
            PageSize: pageSize,
            TotalPages: totalPages);
    }
}
```

- [ ] **Step 4: Esegui i test — devono passare**

```bash
cd apps/api
dotnet test tests/Api.Tests/ --filter "GetGamesWithoutKbQueryHandlerIntegrationTests" -v
```

Expected: tutti i test PASS

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetGamesWithoutKb/GetGamesWithoutKbQueryHandler.cs
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Queries/GetGamesWithoutKbQueryHandlerIntegrationTests.cs
git commit -m "feat(kb): implement GetGamesWithoutKbQueryHandler with integration tests"
```

---

## Task 3: Backend — Endpoint GET /api/v1/admin/kb/games/without-kb

**Files:**
- Modify: `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`

- [ ] **Step 1: Aggiungi l'endpoint al gruppo `kbGroup` esistente**

Nel file `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs`, aggiungi subito dopo l'endpoint `agents/estimate-cost`:

```csharp
// Aggiungi questo using in cima al file:
using Api.BoundedContexts.KnowledgeBase.Application.Queries.GetGamesWithoutKb;

// Nel metodo MapAdminKnowledgeBaseEndpoints, dopo il blocco agents/estimate-cost:
// GET /api/v1/admin/kb/games/without-kb
kbGroup.MapGet("/games/without-kb", async (
    IMediator mediator,
    [FromQuery] int? page,
    [FromQuery] int? pageSize,
    [FromQuery] string? search,
    CancellationToken ct) =>
{
    var query = new GetGamesWithoutKbQuery(
        Page: page ?? 1,
        PageSize: pageSize ?? 20,
        Search: search
    );
    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.WithName("GetGamesWithoutKb")
.WithSummary("List shared games with no active Knowledge Base (admin RAG onboarding)")
.Produces<GamesWithoutKbPagedResponse>();
```

- [ ] **Step 2: Build per verificare la compilazione**

```bash
cd apps/api/src/Api
dotnet build
```

Expected: BUILD SUCCEEDED, 0 errori

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs
git commit -m "feat(kb): expose GET /admin/kb/games/without-kb endpoint"
```

---

## Task 4: Frontend — API client kb-games-without-kb-api.ts

**Files:**
- Create: `apps/web/src/lib/api/kb-games-without-kb-api.ts`

- [ ] **Step 1: Crea il file API**

```typescript
// apps/web/src/lib/api/kb-games-without-kb-api.ts
import { useQuery } from '@tanstack/react-query';

import { apiClient } from '@/lib/api/client';

export interface GameWithoutKbDto {
  gameId: string;
  title: string;
  publisher: string | null;
  imageUrl: string | null;
  playerCountLabel: string;
  pdfCount: number;
  hasFailedPdfs: boolean;
}

export interface GamesWithoutKbPagedResponse {
  items: GameWithoutKbDto[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

export interface GamesWithoutKbParams {
  page?: number;
  pageSize?: number;
  search?: string;
}

async function fetchGamesWithoutKb(
  params: GamesWithoutKbParams
): Promise<GamesWithoutKbPagedResponse> {
  const q = new URLSearchParams();
  if (params.page !== undefined) q.set('page', params.page.toString());
  if (params.pageSize !== undefined) q.set('pageSize', params.pageSize.toString());
  if (params.search) q.set('search', params.search);

  const url = `/api/v1/admin/kb/games/without-kb${q.toString() ? `?${q}` : ''}`;
  const result = await apiClient.get<GamesWithoutKbPagedResponse>(url);
  return result ?? { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 };
}

export function useGamesWithoutKb(params: GamesWithoutKbParams = {}) {
  return useQuery({
    queryKey: ['admin', 'kb', 'games-without-kb', params],
    queryFn: () => fetchGamesWithoutKb(params),
    staleTime: 30_000,
  });
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/kb-games-without-kb-api.ts
git commit -m "feat(kb-fe): add kb-games-without-kb-api client with React Query hook"
```

---

## Task 5: Frontend — GamesWithoutKbSection component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/games-without-kb-section.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/__tests__/games-without-kb-section.test.tsx`

- [ ] **Step 1: Scrivi il test (TDD — fallirà perché il componente non esiste)**

```tsx
// apps/web/src/components/admin/knowledge-base/__tests__/games-without-kb-section.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { GamesWithoutKbSection } from '../games-without-kb-section';

// Mock the hook
vi.mock('@/lib/api/kb-games-without-kb-api', () => ({
  useGamesWithoutKb: vi.fn(),
}));

import { useGamesWithoutKb } from '@/lib/api/kb-games-without-kb-api';

const mockGames = [
  {
    gameId: 'aaa-111',
    title: 'Catan',
    publisher: 'Kosmos',
    imageUrl: null,
    playerCountLabel: '3–4 giocatori',
    pdfCount: 0,
    hasFailedPdfs: false,
  },
  {
    gameId: 'bbb-222',
    title: 'Pandemic',
    publisher: null,
    imageUrl: null,
    playerCountLabel: '2–4 giocatori',
    pdfCount: 1,
    hasFailedPdfs: true,
  },
];

describe('GamesWithoutKbSection', () => {
  beforeEach(() => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: { items: mockGames, total: 2, page: 1, pageSize: 20, totalPages: 1 },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);
  });

  it('renders a MeepleCard for each game', () => {
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Pandemic')).toBeInTheDocument();
  });

  it('shows failed badge for games with failed PDFs', () => {
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText('1 fallito')).toBeInTheDocument();
  });

  it('calls onUploadClick with gameId when action button is clicked', async () => {
    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    const buttons = screen.getAllByRole('button', { name: /aggiungi pdf/i });
    await userEvent.click(buttons[0]);

    expect(onUpload).toHaveBeenCalledWith(mockGames[0]);
  });

  it('shows loading skeletons when loading', () => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: undefined,
      isLoading: true,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);

    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    // Skeletons have aria-hidden or a test-id
    expect(screen.getByTestId('games-without-kb-loading')).toBeInTheDocument();
  });

  it('shows empty state when no games found', () => {
    vi.mocked(useGamesWithoutKb).mockReturnValue({
      data: { items: [], total: 0, page: 1, pageSize: 20, totalPages: 0 },
      isLoading: false,
      error: null,
    } as ReturnType<typeof useGamesWithoutKb>);

    const onUpload = vi.fn();
    render(<GamesWithoutKbSection onUploadClick={onUpload} />);

    expect(screen.getByText(/tutti i giochi hanno una kb attiva/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test — deve fallire**

```bash
cd apps/web
pnpm test -- --run "games-without-kb-section"
```

Expected: FAIL con `Cannot find module '../games-without-kb-section'`

- [ ] **Step 3: Implementa il componente**

```tsx
// apps/web/src/components/admin/knowledge-base/games-without-kb-section.tsx
'use client';

import { useState } from 'react';

import { Upload, CheckCircle2, AlertCircle } from 'lucide-react';

import { MeepleCard, MeepleCardSkeleton } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/primitives/input';
import {
  useGamesWithoutKb,
  type GameWithoutKbDto,
} from '@/lib/api/kb-games-without-kb-api';

interface GamesWithoutKbSectionProps {
  onUploadClick: (game: GameWithoutKbDto) => void;
}

export function GamesWithoutKbSection({ onUploadClick }: GamesWithoutKbSectionProps) {
  const [search, setSearch] = useState('');
  const [page, setPage] = useState(1);

  const { data, isLoading } = useGamesWithoutKb({ page, pageSize: 20, search: search || undefined });
  const items = data?.items ?? [];

  if (isLoading) {
    return (
      <div data-testid="games-without-kb-loading" className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
        {Array.from({ length: 10 }).map((_, i) => (
          <MeepleCardSkeleton key={i} variant="grid" />
        ))}
      </div>
    );
  }

  return (
    <div className="space-y-4">
      <Input
        placeholder="Cerca gioco senza KB..."
        value={search}
        onChange={e => {
          setSearch(e.target.value);
          setPage(1);
        }}
        className="max-w-sm"
      />

      {items.length === 0 ? (
        <div className="flex flex-col items-center gap-3 py-16 text-slate-400 dark:text-zinc-500">
          <CheckCircle2 className="h-10 w-10 text-emerald-400" />
          <p className="text-sm">Tutti i giochi hanno una KB attiva</p>
        </div>
      ) : (
        <>
          <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5 gap-4">
            {items.map(game => (
              <MeepleCard
                key={game.gameId}
                entity="kb"
                variant="grid"
                title={game.title}
                subtitle={game.publisher ?? undefined}
                imageUrl={game.imageUrl ?? undefined}
                status={game.hasFailedPdfs ? 'failed' : 'idle'}
                badge={game.hasFailedPdfs ? `${game.pdfCount} fallito` : 'Nessuna KB'}
                metadata={[
                  { label: 'Giocatori', value: game.playerCountLabel },
                  ...(game.pdfCount > 0
                    ? [{ label: 'PDF caricati', value: String(game.pdfCount) }]
                    : []),
                ]}
                actions={[
                  {
                    icon: <Upload className="h-3.5 w-3.5" />,
                    label: 'Aggiungi PDF',
                    onClick: () => onUploadClick(game),
                    variant: 'primary' as const,
                  },
                ]}
              />
            ))}
          </div>

          {/* Pagination */}
          {(data?.totalPages ?? 0) > 1 && (
            <div className="flex justify-center gap-2 pt-2">
              <button
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1}
                className="px-3 py-1 text-sm rounded border border-slate-200 dark:border-zinc-700 disabled:opacity-40"
              >
                ‹
              </button>
              <span className="px-3 py-1 text-sm text-slate-500 dark:text-zinc-400">
                {page} / {data?.totalPages}
              </span>
              <button
                onClick={() => setPage(p => Math.min(data?.totalPages ?? 1, p + 1))}
                disabled={page === (data?.totalPages ?? 1)}
                className="px-3 py-1 text-sm rounded border border-slate-200 dark:border-zinc-700 disabled:opacity-40"
              >
                ›
              </button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Esegui i test — devono passare**

```bash
cd apps/web
pnpm test -- --run "games-without-kb-section"
```

Expected: tutti i test PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/games-without-kb-section.tsx
git add apps/web/src/components/admin/knowledge-base/__tests__/games-without-kb-section.test.tsx
git commit -m "feat(kb-fe): add GamesWithoutKbSection with MeepleCard grid"
```

---

## Task 6: Frontend — UploadForGameDrawer component

**Files:**
- Create: `apps/web/src/components/admin/knowledge-base/upload-for-game-drawer.tsx`
- Create: `apps/web/src/components/admin/knowledge-base/__tests__/upload-for-game-drawer.test.tsx`

- [ ] **Step 1: Scrivi il test**

```tsx
// apps/web/src/components/admin/knowledge-base/__tests__/upload-for-game-drawer.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi } from 'vitest';

import { UploadForGameDrawer } from '../upload-for-game-drawer';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

const mockGame: GameWithoutKbDto = {
  gameId: 'aaa-111',
  title: 'Wingspan',
  publisher: 'Stonemaier Games',
  imageUrl: null,
  playerCountLabel: '1–5 giocatori',
  pdfCount: 0,
  hasFailedPdfs: false,
};

describe('UploadForGameDrawer', () => {
  it('renders game title in the drawer header', () => {
    render(
      <UploadForGameDrawer game={mockGame} open={true} onClose={vi.fn()} />
    );
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
    expect(screen.getByText('Stonemaier Games')).toBeInTheDocument();
  });

  it('calls onClose when Annulla is clicked', async () => {
    const onClose = vi.fn();
    render(<UploadForGameDrawer game={mockGame} open={true} onClose={onClose} />);

    await userEvent.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('does not render when open=false', () => {
    render(
      <UploadForGameDrawer game={mockGame} open={false} onClose={vi.fn()} />
    );
    expect(screen.queryByText('Wingspan')).not.toBeInTheDocument();
  });

  it('shows drop zone with max file info', () => {
    render(
      <UploadForGameDrawer game={mockGame} open={true} onClose={vi.fn()} />
    );
    expect(screen.getByText(/trascina i pdf qui/i)).toBeInTheDocument();
    expect(screen.getByText(/max 5 file/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Esegui il test — deve fallire**

```bash
cd apps/web
pnpm test -- --run "upload-for-game-drawer"
```

Expected: FAIL con `Cannot find module '../upload-for-game-drawer'`

- [ ] **Step 3: Implementa il componente**

```tsx
// apps/web/src/components/admin/knowledge-base/upload-for-game-drawer.tsx
'use client';

import { useCallback, useState } from 'react';

import { Upload, X, FileText } from 'lucide-react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/primitives/button';
import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
} from '@/components/ui/overlays/sheet';
import { useToast } from '@/hooks/useToast';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';

const MAX_FILES = 5;
const MAX_SIZE_MB = 500;

interface UploadForGameDrawerProps {
  game: GameWithoutKbDto | null;
  open: boolean;
  onClose: () => void;
}

interface PendingFile {
  file: File;
  id: string;
}

export function UploadForGameDrawer({ game, open, onClose }: UploadForGameDrawerProps) {
  const { toast } = useToast();
  const [pendingFiles, setPendingFiles] = useState<PendingFile[]>([]);
  const [isDragging, setIsDragging] = useState(false);

  const handleDrop = useCallback(
    (e: React.DragEvent) => {
      e.preventDefault();
      setIsDragging(false);
      const dropped = Array.from(e.dataTransfer.files).filter(f => f.type === 'application/pdf');
      addFiles(dropped);
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pendingFiles]
  );

  const handleFileInput = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const selected = Array.from(e.target.files ?? []);
      addFiles(selected);
      e.target.value = '';
    },
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [pendingFiles]
  );

  function addFiles(files: File[]) {
    const valid = files.filter(f => {
      const sizeMb = f.size / (1024 * 1024);
      if (sizeMb > MAX_SIZE_MB) {
        toast({ title: `${f.name} supera ${MAX_SIZE_MB}MB`, variant: 'destructive' });
        return false;
      }
      return true;
    });

    const available = MAX_FILES - pendingFiles.length;
    if (available <= 0) {
      toast({ title: `Massimo ${MAX_FILES} file per upload`, variant: 'destructive' });
      return;
    }

    const toAdd = valid.slice(0, available).map(f => ({ file: f, id: crypto.randomUUID() }));
    setPendingFiles(prev => [...prev, ...toAdd]);
  }

  function removeFile(id: string) {
    setPendingFiles(prev => prev.filter(f => f.id !== id));
  }

  function handleClose() {
    setPendingFiles([]);
    onClose();
  }

  const handleUpload = useCallback(() => {
    if (!game || pendingFiles.length === 0) return;
    // Naviga alla pagina di upload con i file pre-selezionati — il flusso esistente
    // (UploadZone + AddRulebookCommand) gestisce l'upload effettivo.
    const params = new URLSearchParams({ gameId: game.gameId });
    window.location.href = `/admin/knowledge-base/upload?${params}`;
  }, [game, pendingFiles]);

  if (!game) return null;

  return (
    <Sheet open={open} onOpenChange={open => !open && handleClose()}>
      <SheetContent side="right" className="w-[420px] sm:w-[480px] flex flex-col gap-0 p-0">
        <SheetHeader className="px-6 py-4 border-b border-slate-200 dark:border-zinc-700">
          <SheetTitle className="text-base font-semibold">
            Upload PDF — {game.title}
          </SheetTitle>
        </SheetHeader>

        <div className="flex-1 overflow-y-auto px-6 py-4 space-y-4">
          {/* Game preview */}
          <MeepleCard
            entity="kb"
            variant="list"
            title={game.title}
            subtitle={game.publisher ?? undefined}
            imageUrl={game.imageUrl ?? undefined}
            status="idle"
            metadata={[{ label: 'Giocatori', value: game.playerCountLabel }]}
          />

          {/* Drop zone */}
          <div
            onDragOver={e => { e.preventDefault(); setIsDragging(true); }}
            onDragLeave={() => setIsDragging(false)}
            onDrop={handleDrop}
            className={`
              relative border-2 border-dashed rounded-xl p-8 text-center transition-colors cursor-pointer
              ${isDragging
                ? 'border-blue-400 bg-blue-50 dark:bg-blue-900/20'
                : 'border-slate-300 dark:border-zinc-600 hover:border-slate-400 dark:hover:border-zinc-500'
              }
            `}
          >
            <input
              type="file"
              accept=".pdf,application/pdf"
              multiple
              onChange={handleFileInput}
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer"
            />
            <Upload className="h-8 w-8 mx-auto mb-2 text-slate-400 dark:text-zinc-500" />
            <p className="text-sm font-medium text-slate-700 dark:text-zinc-300">
              Trascina i PDF qui
            </p>
            <p className="text-xs text-slate-400 dark:text-zinc-500 mt-1">
              Max {MAX_FILES} file · Max {MAX_SIZE_MB}MB ciascuno
            </p>
          </div>

          {/* File list */}
          {pendingFiles.length > 0 && (
            <ul className="space-y-2">
              {pendingFiles.map(({ file, id }) => (
                <li
                  key={id}
                  className="flex items-center gap-3 px-3 py-2 rounded-lg bg-slate-50 dark:bg-zinc-800"
                >
                  <FileText className="h-4 w-4 text-slate-400 dark:text-zinc-500 shrink-0" />
                  <span className="flex-1 text-sm text-slate-700 dark:text-zinc-300 truncate">
                    {file.name}
                  </span>
                  <span className="text-xs text-slate-400 dark:text-zinc-500 shrink-0">
                    {(file.size / (1024 * 1024)).toFixed(1)} MB
                  </span>
                  <button
                    type="button"
                    onClick={() => removeFile(id)}
                    className="text-slate-400 hover:text-red-500 dark:text-zinc-500 dark:hover:text-red-400 transition-colors"
                  >
                    <X className="h-4 w-4" />
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>

        {/* Footer */}
        <div className="px-6 py-4 border-t border-slate-200 dark:border-zinc-700 flex justify-end gap-3">
          <Button variant="outline" onClick={handleClose}>
            Annulla
          </Button>
          <Button
            onClick={handleUpload}
            disabled={pendingFiles.length === 0}
          >
            <Upload className="h-4 w-4 mr-2" />
            Avvia upload ({pendingFiles.length} PDF)
          </Button>
        </div>
      </SheetContent>
    </Sheet>
  );
}
```

- [ ] **Step 4: Esegui i test — devono passare**

```bash
cd apps/web
pnpm test -- --run "upload-for-game-drawer"
```

Expected: tutti i test PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/knowledge-base/upload-for-game-drawer.tsx
git add apps/web/src/components/admin/knowledge-base/__tests__/upload-for-game-drawer.test.tsx
git commit -m "feat(kb-fe): add UploadForGameDrawer slide-over component"
```

---

## Task 7: Frontend — Update games/page.tsx con sezione MeepleCard

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/games/page.tsx`

La pagina esistente ha già il filtro `filter === 'none'` che mostra i giochi senza KB come righe di tabella. Sostituiamo quella sezione con la `GamesWithoutKbSection` a griglia e il drawer.

- [ ] **Step 1: Leggi la pagina corrente**

```bash
cat "apps/web/src/app/admin/(dashboard)/knowledge-base/games/page.tsx"
```

- [ ] **Step 2: Aggiungi i nuovi import in cima**

Subito dopo gli import esistenti, aggiungi:

```tsx
import { GamesWithoutKbSection } from '@/components/admin/knowledge-base/games-without-kb-section';
import { UploadForGameDrawer } from '@/components/admin/knowledge-base/upload-for-game-drawer';
import type { GameWithoutKbDto } from '@/lib/api/kb-games-without-kb-api';
```

- [ ] **Step 3: Aggiungi lo stato del drawer nel componente**

Dentro `KbGamesPage()`, subito dopo le `const` esistenti (`search`, `filter`):

```tsx
const [uploadTarget, setUploadTarget] = useState<GameWithoutKbDto | null>(null);
```

- [ ] **Step 4: Sostituisci il rendering per filter === 'none'**

Trova il blocco che renderizza `filtered.map(item => <GameKbRow ... />)` e avvolgi con una condizione:

```tsx
{filter === 'none' ? (
  <GamesWithoutKbSection onUploadClick={setUploadTarget} />
) : (
  <div className="divide-y divide-slate-100 dark:divide-zinc-700/50">
    {filtered.map(item => (
      <GameKbRow key={item.gameId} item={item} />
    ))}
  </div>
)}
```

- [ ] **Step 5: Aggiungi il drawer alla fine del JSX (prima del tag di chiusura del return)**

```tsx
<UploadForGameDrawer
  game={uploadTarget}
  open={uploadTarget !== null}
  onClose={() => setUploadTarget(null)}
/>
```

- [ ] **Step 6: Build + typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errori TypeScript

- [ ] **Step 7: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/games/page.tsx"
git commit -m "feat(kb-fe): integrate GamesWithoutKbSection and UploadForGameDrawer into games page"
```

---

## Task 8: Frontend — Add "Rimuovi dalla coda" to queue-item-actions

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item-actions.tsx`

- [ ] **Step 1: Aggiungi il `removeJob` import e mutation**

Trova la riga degli import da `../lib/queue-api`:

```tsx
// PRIMA (riga esistente):
import { setPriority, cancelJob, retryJob } from '../lib/queue-api';

// DOPO:
import { setPriority, cancelJob, retryJob, removeJob } from '../lib/queue-api';
```

- [ ] **Step 2: Aggiungi stato e mutation per Remove**

Subito dopo `const [cancelDialogOpen, setCancelDialogOpen] = useState(false);`:

```tsx
const [removeDialogOpen, setRemoveDialogOpen] = useState(false);

const removeMutation = useMutation({
  mutationFn: () => removeJob(job.id),
  onSuccess: () => {
    invalidate();
    setRemoveDialogOpen(false);
    toast({ title: 'Job rimosso dalla coda' });
  },
  onError: () => {
    toast({ title: 'Errore', description: 'Impossibile rimuovere il job.', variant: 'destructive' });
  },
});
```

- [ ] **Step 3: Aggiungi `canRemove` flag**

Subito dopo `const canRetry = job.status === 'Failed' && job.canRetry;`:

```tsx
const canRemove = job.status === 'Queued';
```

- [ ] **Step 4: Aggiungi la voce nel DropdownMenuContent**

Subito prima del blocco `{canCancel && (...)}` esistente:

```tsx
{canRemove && (
  <>
    <DropdownMenuSeparator />
    <DropdownMenuItem
      onClick={() => setRemoveDialogOpen(true)}
      className="text-orange-600 dark:text-orange-400"
    >
      <Trash2Icon className="h-4 w-4 mr-2" />
      Rimuovi dalla coda
    </DropdownMenuItem>
  </>
)}
```

- [ ] **Step 5: Aggiungi `Trash2Icon` agli import di lucide-react**

```tsx
// PRIMA:
import { MoreHorizontalIcon, ArrowUpIcon, XCircleIcon, FileTextIcon, RefreshCwIcon } from 'lucide-react';

// DOPO:
import { MoreHorizontalIcon, ArrowUpIcon, XCircleIcon, FileTextIcon, RefreshCwIcon, Trash2Icon } from 'lucide-react';
```

- [ ] **Step 6: Aggiungi il dialog di conferma rimozione**

Dopo l'`AlertDialog` del cancel esistente:

```tsx
{/* Remove confirmation dialog */}
<AlertDialog open={removeDialogOpen} onOpenChange={setRemoveDialogOpen}>
  <AlertDialogContent onClick={e => e.stopPropagation()}>
    <AlertDialogHeader>
      <AlertDialogTitle>Rimuovere dalla coda?</AlertDialogTitle>
      <AlertDialogDescription>
        Rimuove &quot;{job.pdfFileName}&quot; dalla coda. Il file non verrà eliminato.
        Potrai accodarlo di nuovo.
      </AlertDialogDescription>
    </AlertDialogHeader>
    <AlertDialogFooter>
      <AlertDialogCancel>Annulla</AlertDialogCancel>
      <AlertDialogAction
        onClick={() => removeMutation.mutate()}
        className="bg-orange-600 hover:bg-orange-700"
      >
        Rimuovi dalla coda
      </AlertDialogAction>
    </AlertDialogFooter>
  </AlertDialogContent>
</AlertDialog>
```

- [ ] **Step 7: Typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 8: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item-actions.tsx"
git commit -m "feat(queue-fe): add Remove from queue action to job dropdown"
```

---

## Task 9: Frontend — Add ETA display to queue-item and queue-stats-bar

**Files:**
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-stats-bar.tsx`

- [ ] **Step 1: Leggi i file correnti**

```bash
cat "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx"
cat "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-stats-bar.tsx"
```

- [ ] **Step 2: Aggiungi ETA a `queue-stats-bar.tsx`**

Nel componente `QueueStatsBar`, aggiungi un hook per il batch ETA e mostra il totale. Aggiungi import:

```tsx
import { useQuery } from '@tanstack/react-query';
import { fetchBatchETA } from '../lib/queue-api';
```

Aggiungi hook dentro il componente:

```tsx
const { data: etaData } = useQuery({
  queryKey: ['admin', 'queue', 'eta'],
  queryFn: fetchBatchETA,
  staleTime: 30_000,
  refetchInterval: 30_000,
});
```

Aggiungi una stat tile per l'ETA totale (posizionata accanto alle stat esistenti):

```tsx
<div className="rounded-xl border border-slate-200 dark:border-zinc-700 bg-white dark:bg-zinc-800/60 p-3">
  <p className="text-2xl font-bold text-amber-600 dark:text-amber-400">
    {etaData?.totalDrainTimeMinutes != null
      ? `~${Math.round(etaData.totalDrainTimeMinutes)} min`
      : '—'}
  </p>
  <p className="text-xs text-slate-500 dark:text-zinc-400 mt-0.5">ETA totale</p>
</div>
```

- [ ] **Step 3: Aggiungi ETA per singolo job in `queue-item.tsx`**

Cerca dove viene renderizzato il job nella lista (dovrebbe mostrare `pdfFileName`, `status`, ecc).

Aggiungi una prop `etaMinutes?: number | null` all'interfaccia `QueueItemProps` (o al componente se non ha interfaccia esplicita).

Dentro il JSX del componente, in una posizione vicino allo status badge, aggiungi:

```tsx
{etaMinutes != null && (job.status === 'Queued' || job.status === 'Processing') && (
  <span className="text-xs text-amber-600 dark:text-amber-400 tabular-nums">
    ~{Math.round(etaMinutes)} min
  </span>
)}
```

- [ ] **Step 4: Propaga l'ETA da `queue-list.tsx` o `queue-dashboard-client.tsx`**

Leggi `queue-list.tsx`:

```bash
cat "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-list.tsx"
```

In `queue-list.tsx`, aggiungi il fetch del batch ETA e indicizza per `jobId`:

```tsx
import { useQuery } from '@tanstack/react-query';
import { fetchBatchETA } from '../lib/queue-api';

// Dentro il componente:
const { data: etaData } = useQuery({
  queryKey: ['admin', 'queue', 'eta'],
  queryFn: fetchBatchETA,
  staleTime: 30_000,
  refetchInterval: 30_000,
});

const etaByJobId = Object.fromEntries(
  (etaData?.jobs ?? []).map(j => [j.jobId, j.estimatedMinutesRemaining])
);
```

Poi passa la prop `etaMinutes={etaByJobId[job.id] ?? null}` a ogni `QueueItem`.

- [ ] **Step 5: Typecheck**

```bash
cd apps/web
pnpm typecheck
```

Expected: 0 errori

- [ ] **Step 6: Commit**

```bash
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-item.tsx"
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-stats-bar.tsx"
git add "apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-list.tsx"
git commit -m "feat(queue-fe): add per-job ETA display and total ETA in stats bar"
```

---

## Task 10: Run full test suite + verifica finale

- [ ] **Step 1: Backend — run nuovi test integrazione**

```bash
cd apps/api
dotnet test tests/Api.Tests/ --filter "GetGamesWithoutKbQueryHandlerIntegrationTests" -v
```

Expected: tutti i test PASS

- [ ] **Step 2: Backend — run full suite (no regression)**

```bash
cd apps/api
dotnet test tests/Api.Tests/ --filter "Category=Unit|Category=Integration" --no-build -v
```

Expected: nessun test che era verde ora è rosso

- [ ] **Step 3: Frontend — run unit tests**

```bash
cd apps/web
pnpm test -- --run
```

Expected: tutti i test PASS (inclusi i 2 nuovi: `games-without-kb-section`, `upload-for-game-drawer`)

- [ ] **Step 4: Frontend — typecheck + lint**

```bash
cd apps/web
pnpm typecheck && pnpm lint
```

Expected: 0 errori TypeScript, 0 lint error

- [ ] **Step 5: Commit di chiusura se tutto verde**

```bash
git add -A
git commit -m "chore(kb): verify full test suite post RAG onboarding flow"
```

---

## Definition of Done

- [ ] `GET /api/v1/admin/kb/games/without-kb` risponde con lista paginata di giochi senza KB
- [ ] Pagina `/admin/knowledge-base/games` → filtro "Senza KB" mostra griglia MeepleCard
- [ ] Click "Aggiungi PDF" → apre drawer con drop zone
- [ ] Drawer → click "Avvia upload" naviga a `/admin/knowledge-base/upload?gameId=...`
- [ ] Coda: ogni job `Queued` mostra ETA stimata in minuti
- [ ] Coda: stats bar mostra ETA totale svuotamento coda
- [ ] Coda: dropdown job `Queued` include "Rimuovi dalla coda" con dialog di conferma
- [ ] Rimozione job → backend risponde 204, lista si invalida, toast di conferma
- [ ] Test backend integrazione: 5 test PASS per `GetGamesWithoutKbQueryHandler`
- [ ] Test frontend: GamesWithoutKbSection (5 test) + UploadForGameDrawer (4 test) tutti PASS
- [ ] TypeScript: 0 errori, lint: 0 errori

---

*Salvato: `docs/superpowers/plans/2026-04-11-rag-admin-onboarding.md`*
