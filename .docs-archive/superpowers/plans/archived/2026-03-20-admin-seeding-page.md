# Admin Seeding Page — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Admin page to select skeleton/failed games, enqueue BGG enrichment, download tracking Excel, and monitor progress with auto-refresh.

**Architecture:** New backend endpoint returns lightweight seeding-status DTOs (avoids zod validation issues with zero-filled skeleton fields). Frontend page uses existing `POST /bgg-queue/batch` for enrichment and `GET /tracking-export` for Excel. Polling every 5s for status updates.

**Tech Stack:** .NET 9 (MediatR, EF Core), Next.js 16 (App Router, React 19), Tailwind 4, shadcn/ui, zod

**Branch:** `feature/seed-top-bgg-games` (parent: `main-dev`)

---

## File Structure

| File | Responsibility |
|------|---------------|
| **Backend** | |
| `Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQuery.cs` | Query record |
| `Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQueryHandler.cs` | Handler: returns lightweight DTOs with gameDataStatus |
| `Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs` | Register endpoint |
| **Frontend** | |
| `apps/web/src/lib/api/schemas/seeding.schemas.ts` | Zod schemas for seeding status |
| `apps/web/src/lib/api/clients/sharedGamesClient.ts` | Add seeding API methods |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/page.tsx` | Server component wrapper |
| `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx` | Client component with table + actions |
| `apps/web/src/config/admin-navigation.ts` | Add nav entry |

---

### Task 1: Backend — GetSeedingStatus Query + Handler

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQuery.cs`
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/GetSeedingStatusQueryHandler.cs`

- [ ] **Step 1: Create the DTO and query**

Create `GetSeedingStatusQuery.cs`:

```csharp
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSeedingStatus;

/// <summary>
/// Lightweight DTO for seeding status — avoids full SharedGameDto validation issues with skeleton zeros.
/// </summary>
public sealed record SeedingGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int GameDataStatus,
    string GameDataStatusName,
    int GameStatus,
    string GameStatusName,
    bool HasUploadedPdf,
    DateTime CreatedAt);

/// <summary>
/// Query to get all games with their seeding/enrichment status.
/// </summary>
internal sealed record GetSeedingStatusQuery : IQuery<List<SeedingGameDto>>;
```

- [ ] **Step 2: Create the handler**

Create `GetSeedingStatusQueryHandler.cs`:

```csharp
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSeedingStatus;

internal sealed class GetSeedingStatusQueryHandler
    : IRequestHandler<GetSeedingStatusQuery, List<SeedingGameDto>>
{
    private static readonly Dictionary<int, string> DataStatusNames = new()
    {
        [0] = "Skeleton", [1] = "EnrichmentQueued", [2] = "Enriching",
        [3] = "Enriched", [4] = "PdfDownloading", [5] = "Complete", [6] = "Failed"
    };

    private static readonly Dictionary<int, string> GameStatusNames = new()
    {
        [0] = "Draft", [1] = "PendingApproval", [2] = "Published", [3] = "Archived"
    };

    private readonly MeepleAiDbContext _context;

    public GetSeedingStatusQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<List<SeedingGameDto>> Handle(
        GetSeedingStatusQuery query, CancellationToken cancellationToken)
    {
        var games = await _context.SharedGames
            .AsNoTracking()
            .Where(g => !g.IsDeleted)
            .OrderBy(g => g.Title)
            .Select(g => new
            {
                g.Id, g.BggId, g.Title, g.GameDataStatus,
                g.Status, g.HasUploadedPdf, g.CreatedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return games.Select(g => new SeedingGameDto(
            g.Id,
            g.BggId,
            g.Title,
            g.GameDataStatus,
            DataStatusNames.GetValueOrDefault(g.GameDataStatus, "Unknown"),
            g.Status,
            GameStatusNames.GetValueOrDefault(g.Status, "Unknown"),
            g.HasUploadedPdf,
            g.CreatedAt
        )).ToList();
    }
}
```

- [ ] **Step 3: Register the endpoint**

In `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`, add inside `Map()`:

```csharp
        // Get seeding status for all games (lightweight DTO for admin seeding page)
        group.MapGet("/admin/shared-games/seeding-status", HandleGetSeedingStatus)
            .RequireAuthorization("AdminOrEditorPolicy")
            .WithName("GetSeedingStatus")
            .WithSummary("Get seeding/enrichment status for all games (Admin/Editor)")
            .WithDescription("Returns lightweight DTOs with gameDataStatus for the admin seeding management page.")
            .Produces<List<GetSeedingStatus.SeedingGameDto>>();
```

Add handler method:

```csharp
    private static async Task<IResult> HandleGetSeedingStatus(
        IMediator mediator,
        CancellationToken cancellationToken)
    {
        var result = await mediator.Send(
            new GetSeedingStatus.GetSeedingStatusQuery(),
            cancellationToken).ConfigureAwait(false);
        return Results.Ok(result);
    }
```

Add using:
```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSeedingStatus;
```

- [ ] **Step 4: Build**

Run: `cd apps/api/src/Api && dotnet build --no-restore`

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetSeedingStatus/
git add apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs
git commit -m "feat(shared-games): add GET seeding-status endpoint for admin seeding page"
```

---

### Task 2: Frontend — Zod Schema + API Client Methods

**Files:**
- Create: `apps/web/src/lib/api/schemas/seeding.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts`

- [ ] **Step 1: Create seeding schemas**

Create `apps/web/src/lib/api/schemas/seeding.schemas.ts`:

```typescript
import { z } from 'zod';

export const SeedingGameDtoSchema = z.object({
  id: z.string().uuid(),
  bggId: z.number().int().nullable(),
  title: z.string(),
  gameDataStatus: z.number().int(),
  gameDataStatusName: z.string(),
  gameStatus: z.number().int(),
  gameStatusName: z.string(),
  hasUploadedPdf: z.boolean(),
  createdAt: z.string(),
});

export type SeedingGameDto = z.infer<typeof SeedingGameDtoSchema>;

export const SeedingGameListSchema = z.array(SeedingGameDtoSchema);
export type SeedingGameList = z.infer<typeof SeedingGameListSchema>;
```

- [ ] **Step 2: Add API client methods to sharedGamesClient.ts**

In `apps/web/src/lib/api/clients/sharedGamesClient.ts`, add imports at the top:

```typescript
import { SeedingGameListSchema, type SeedingGameList } from '../schemas/seeding.schemas';
```

Add methods inside the returned object (find the `admin` section or add near `bulkImport`):

```typescript
    /** Get seeding/enrichment status for all games */
    async getSeedingStatus(): Promise<SeedingGameList> {
      return httpClient.get<SeedingGameList>(
        '/api/v1/shared-game-catalog/admin/shared-games/seeding-status',
        SeedingGameListSchema
      );
    },

    /** Enqueue selected games for BGG enrichment */
    async enqueueBggEnrichment(bggIds: number[]): Promise<unknown> {
      return httpClient.post(
        '/api/v1/admin/bgg-queue/batch',
        { bggIds },
        z.unknown()
      );
    },

    /** Download tracking export Excel (returns blob URL) */
    async downloadTrackingExport(): Promise<Blob> {
      const response = await fetch(
        `${httpClient.baseUrl}/api/v1/shared-game-catalog/admin/shared-games/tracking-export`,
        {
          headers: httpClient.getAuthHeaders(),
        }
      );
      if (!response.ok) throw new Error(`Export failed: ${response.status}`);
      return response.blob();
    },
```

NOTE: The `downloadTrackingExport` uses raw `fetch` because the httpClient JSON-parses responses, but this endpoint returns a binary blob. Check how `httpClient` exposes `baseUrl` and auth headers — you may need to use `getApiBase()` from `'@/lib/api'` and get the token from the auth store. Read the httpClient implementation to determine the correct approach.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/schemas/seeding.schemas.ts
git add apps/web/src/lib/api/clients/sharedGamesClient.ts
git commit -m "feat(frontend): add seeding schemas and API client methods"
```

---

### Task 3: Frontend — Admin Seeding Page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`
- Modify: `apps/web/src/config/admin-navigation.ts`

- [ ] **Step 1: Create server component**

Create `apps/web/src/app/admin/(dashboard)/shared-games/seeding/page.tsx`:

```tsx
import { type Metadata } from 'next';

import { SeedingPageClient } from './client';

export const metadata: Metadata = {
  title: 'Game Seeding',
  description: 'Manage BGG game enrichment and track seeding progress',
};

export default function SeedingPage() {
  return <SeedingPageClient />;
}
```

- [ ] **Step 2: Create client component**

Create `apps/web/src/app/admin/(dashboard)/shared-games/seeding/client.tsx`.

This is the main component. Read existing admin client pages for patterns (e.g., `apps/web/src/app/admin/(dashboard)/shared-games/[id]/client.tsx` or `apps/web/src/app/admin/(dashboard)/shared-games/wizard/CatalogWizard.tsx`).

The component should:

1. **State**: `selectedIds: Set<string>`, `loading`, `enriching`, `games: SeedingGameDto[]`
2. **Data fetch**: call `api.sharedGames.getSeedingStatus()` on mount and every 5s (polling)
3. **Table**: columns — Checkbox, Title, BGG ID, Data Status (colored badge), Has PDF, Created At
4. **Select all / Select filtered**: checkbox in header to toggle all visible, filter dropdown for status
5. **Actions bar** (sticky top):
   - "Enrich Selected (N)" button — calls `api.sharedGames.enqueueBggEnrichment(bggIds)`, shows estimated time `~{n} seconds at 1 req/sec`
   - "Download Excel" button — calls the tracking export and triggers browser download
   - Status filter dropdown: All / Skeleton / EnrichmentQueued / Enriching / Enriched / Complete / Failed
6. **Rate limit info**: show text `"BGG rate limit: 1 request/second. Enriching {n} games will take ~{n} seconds."`
7. **Disable enrich button**: if no games selected or if selected games have no bggId

Key UI elements (use shadcn components):
- `<Table>` for the game list
- `<Badge>` with variant colors for status
- `<Checkbox>` for selection
- `<Button>` for actions
- `<Select>` for status filter

Badge colors by gameDataStatus:
- 0 Skeleton → `destructive` (red)
- 1 EnrichmentQueued → `secondary` (gray)
- 2 Enriching → `default` (blue)
- 3 Enriched → `outline` with yellow text
- 5 Complete → green variant
- 6 Failed → orange/destructive

```tsx
'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { api } from '@/lib/api';
import type { SeedingGameDto } from '@/lib/api/schemas/seeding.schemas';

// Import shadcn components as used in other admin pages
// Check which are available: Button, Table, Checkbox, Badge, Select, etc.

export function SeedingPageClient() {
  const [games, setGames] = useState<SeedingGameDto[]>([]);
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [statusFilter, setStatusFilter] = useState<number | null>(null);
  const [loading, setLoading] = useState(true);
  const [enriching, setEnriching] = useState(false);

  const fetchGames = useCallback(async () => {
    try {
      const data = await api.sharedGames.getSeedingStatus();
      setGames(data);
    } catch (err) {
      console.error('Failed to fetch seeding status:', err);
    } finally {
      setLoading(false);
    }
  }, []);

  // Initial fetch + polling every 5s
  useEffect(() => {
    fetchGames();
    const interval = setInterval(fetchGames, 5000);
    return () => clearInterval(interval);
  }, [fetchGames]);

  const filteredGames = useMemo(() => {
    if (statusFilter === null) return games;
    return games.filter((g) => g.gameDataStatus === statusFilter);
  }, [games, statusFilter]);

  const selectedGames = useMemo(
    () => games.filter((g) => selectedIds.has(g.id)),
    [games, selectedIds]
  );

  const enrichableSelected = useMemo(
    () => selectedGames.filter((g) => g.bggId !== null && (g.gameDataStatus === 0 || g.gameDataStatus === 6)),
    [selectedGames]
  );

  const handleEnrich = async () => {
    if (enrichableSelected.length === 0) return;
    setEnriching(true);
    try {
      const bggIds = enrichableSelected.map((g) => g.bggId!);
      await api.sharedGames.enqueueBggEnrichment(bggIds);
      await fetchGames(); // Refresh immediately
    } catch (err) {
      console.error('Failed to enqueue enrichment:', err);
    } finally {
      setEnriching(false);
    }
  };

  const handleDownloadExcel = async () => {
    try {
      const blob = await api.sharedGames.downloadTrackingExport();
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `SharedGames_Tracking_${new Date().toISOString().split('T')[0]}.xlsx`;
      a.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      console.error('Failed to download export:', err);
    }
  };

  const toggleSelectAll = () => {
    if (selectedIds.size === filteredGames.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(filteredGames.map((g) => g.id)));
    }
  };

  const toggleSelect = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  };

  // ... render table with the above state and handlers
  // Use the shadcn Table, Badge, Checkbox, Button, Select components
  // Follow patterns from existing admin pages
}
```

IMPORTANT: Read existing admin page components to understand the exact import paths for shadcn components (e.g., `@/components/ui/button`, `@/components/ui/table`, etc.) and match the styling patterns.

- [ ] **Step 3: Add navigation entry**

In `apps/web/src/config/admin-navigation.ts`, add after the "Bulk Import" entry (around line 171):

```typescript
      {
        href: '/admin/shared-games/seeding',
        label: 'Seeding & Enrichment',
        icon: SproutIcon, // or DatabaseIcon — check what's imported from lucide-react
      },
```

Add the icon import at the top if needed.

- [ ] **Step 4: Verify frontend builds**

Run: `cd apps/web && pnpm build`

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/admin/\(dashboard\)/shared-games/seeding/
git add apps/web/src/config/admin-navigation.ts
git commit -m "feat(frontend): add admin seeding page with game selection and enrichment"
```

---

### Task 4: Verification

- [ ] **Step 1: Backend build**

Run: `cd apps/api/src/Api && dotnet build`

- [ ] **Step 2: Frontend build**

Run: `cd apps/web && pnpm build`

- [ ] **Step 3: Verify git status is clean**

Run: `git status`
