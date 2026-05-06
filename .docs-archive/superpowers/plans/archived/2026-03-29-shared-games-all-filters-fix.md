# Shared-Games/All Filters Fix Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix two bugs on `/admin/shared-games/all`: (1) category filter is dead UI — wire it end-to-end with dynamic categories from DB, (2) verify status filter works correctly with mixed-status data.

**Architecture:** Add `categoryId` (single GUID) to the backend `GetFilteredSharedGamesQuery`, filter via the existing `Categories` navigation property join, expose it as a query param on the admin endpoint. On the frontend, replace hardcoded category dropdown with dynamic categories fetched via existing `useGameCategories()` hook, and pass the selected category ID through to the API call.

**Tech Stack:** .NET 9 (MediatR, EF Core), Next.js (React Query, Zustand), TypeScript, Zod

---

## Task 1: Backend — Add `categoryId` to Query + Handler

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQuery.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQueryHandler.cs`

- [ ] **Step 1: Add `CategoryId` parameter to the query record**

```csharp
// GetFilteredSharedGamesQuery.cs — add CategoryId as last optional param
internal record GetFilteredSharedGamesQuery(
    GameStatus? Status = null,
    string? Search = null,
    int PageNumber = 1,
    int PageSize = 20,
    string? SortBy = null,
    Guid? SubmittedBy = null,
    Guid? CategoryId = null
) : IQuery<PagedResult<SharedGameDto>>;
```

- [ ] **Step 2: Add category filtering in the handler**

In `GetFilteredSharedGamesQueryHandler.cs`, after the `SubmittedBy` filter block (line 61), add:

```csharp
if (query.CategoryId.HasValue)
{
    dbQuery = dbQuery.Where(g =>
        g.Categories.Any(c => c.Id == query.CategoryId.Value));
}
```

Also update the log template (line 35) to include CategoryId:

```csharp
_logger.LogInformation(
    "Getting filtered shared games: Status={Status}, Search={Search}, SubmittedBy={SubmittedBy}, CategoryId={CategoryId}, Page={Page}, PageSize={PageSize}, SortBy={SortBy}",
    query.Status,
    query.Search,
    query.SubmittedBy,
    query.CategoryId,
    query.PageNumber,
    query.PageSize,
    query.SortBy);
```

- [ ] **Step 3: Build and verify compilation**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded, 0 errors

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQuery.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/GetFilteredSharedGamesQueryHandler.cs
git commit -m "feat(shared-games): add categoryId filter to GetFilteredSharedGamesQuery"
```

---

## Task 2: Backend — Expose `categoryId` on Admin Endpoint

**Files:**
- Modify: `apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs`

- [ ] **Step 1: Add `categoryId` query parameter to `HandleListAllGames`**

In `SharedGameCatalogAdminEndpoints.cs`, update the `HandleListAllGames` method signature (~line 430) to add the parameter and pass it to the query:

```csharp
private static async Task<IResult> HandleListAllGames(
    IMediator mediator,
    [FromQuery] string? status = null,
    [FromQuery] string? search = null,
    [FromQuery] string? sortBy = null,
    [FromQuery] Guid? submittedBy = null,
    [FromQuery] Guid? categoryId = null,
    [FromQuery] int pageNumber = 1,
    [FromQuery] int pageSize = 20,
    CancellationToken ct = default)
{
    // Issue #3533: Parse status string to enum
    GameStatus? statusEnum = null;
    if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GameStatus>(status, ignoreCase: true, out var parsedStatus))
    {
        statusEnum = parsedStatus;
    }

    var query = new GetFilteredSharedGamesQuery(statusEnum, search, pageNumber, pageSize, sortBy, submittedBy, categoryId);
    var result = await mediator.Send(query, ct).ConfigureAwait(false);
    return Results.Ok(result);
}
```

- [ ] **Step 2: Build and verify**

Run: `cd apps/api/src/Api && dotnet build --no-restore`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Routing/SharedGameCatalog/SharedGameCatalogAdminEndpoints.cs
git commit -m "feat(shared-games): expose categoryId query param on admin list endpoint"
```

---

## Task 3: Frontend — Add `categoryId` to API Client

**Files:**
- Modify: `apps/web/src/lib/api/clients/sharedGamesClient.ts`

- [ ] **Step 1: Add `categoryId` to `getAll()` params**

Update the `getAll` method (~line 252) to accept and send `categoryId`:

```typescript
async getAll(
  params: { status?: string; search?: string; page?: number; pageSize?: number; categoryId?: string } = {}
): Promise<PagedSharedGames> {
  const queryParams = new URLSearchParams();

  if (params.status !== undefined) queryParams.set('status', params.status);
  if (params.search) queryParams.set('search', params.search);
  if (params.categoryId) queryParams.set('categoryId', params.categoryId);
  if (params.page !== undefined) queryParams.set('pageNumber', params.page.toString());
  if (params.pageSize !== undefined) queryParams.set('pageSize', params.pageSize.toString());

  const queryString = queryParams.toString();
  const path = `/api/v1/admin/shared-games${queryString ? `?${queryString}` : ''}`;

  const result = await httpClient.get(path, PagedSharedGamesSchema);
  return result ?? { items: [], total: 0, page: 1, pageSize: 20 };
},
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/clients/sharedGamesClient.ts
git commit -m "feat(shared-games): add categoryId param to admin getAll API client"
```

---

## Task 4: Frontend — Dynamic Categories in GameFilters

**Files:**
- Modify: `apps/web/src/components/admin/shared-games/game-filters.tsx`

- [ ] **Step 1: Replace hardcoded categories with dynamic data**

Replace the entire file content:

```tsx
'use client';

import { useState } from 'react';

import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { useGameCategories } from '@/hooks/queries/useSharedGames';

export interface GameFiltersProps {
  onSearchChange?: (value: string) => void;
  onCategoryChange?: (value: string) => void;
  onStatusChange?: (value: string) => void;
  onPlayersChange?: (value: string) => void;
}

export function GameFilters({
  onSearchChange,
  onCategoryChange,
  onStatusChange,
  onPlayersChange,
}: GameFiltersProps) {
  const [search, setSearch] = useState('');
  const [category, setCategory] = useState('all');
  const [status, setStatus] = useState('all');
  const [players, setPlayers] = useState('all');

  const { data: categories = [] } = useGameCategories();

  const handleSearchChange = (value: string) => {
    setSearch(value);
    onSearchChange?.(value);
  };

  const handleCategoryChange = (value: string) => {
    setCategory(value);
    onCategoryChange?.(value);
  };

  const handleStatusChange = (value: string) => {
    setStatus(value);
    onStatusChange?.(value);
  };

  const handlePlayersChange = (value: string) => {
    setPlayers(value);
    onPlayersChange?.(value);
  };

  return (
    <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-amber-200/50 dark:border-zinc-700/50 rounded-lg p-4">
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        {/* Search */}
        <div className="md:col-span-2">
          <Label htmlFor="game-search" className="text-sm font-medium mb-2">
            Cerca Giochi
          </Label>
          <Input
            id="game-search"
            type="text"
            placeholder="Cerca per titolo, descrizione..."
            value={search}
            onChange={e => handleSearchChange(e.target.value)}
            className="bg-white dark:bg-zinc-900"
          />
        </div>

        {/* Category — dynamic from DB */}
        <div>
          <Label htmlFor="category" className="text-sm font-medium mb-2">
            Categoria
          </Label>
          <Select value={category} onValueChange={handleCategoryChange}>
            <SelectTrigger id="category" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Seleziona categoria" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tutte</SelectItem>
              {categories.map(cat => (
                <SelectItem key={cat.id} value={cat.id}>
                  {cat.name}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>

        {/* Status */}
        <div>
          <Label htmlFor="status" className="text-sm font-medium mb-2">
            Stato
          </Label>
          <Select value={status} onValueChange={handleStatusChange}>
            <SelectTrigger id="status" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Seleziona stato" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Tutti</SelectItem>
              <SelectItem value="published">Pubblicato</SelectItem>
              <SelectItem value="pending">In Attesa</SelectItem>
              <SelectItem value="draft">Bozza</SelectItem>
              <SelectItem value="archived">Archiviato</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>

      {/* Advanced Filters Row */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4 mt-4">
        {/* Player Count */}
        <div>
          <Label htmlFor="players" className="text-sm font-medium mb-2">
            Giocatori
          </Label>
          <Select value={players} onValueChange={handlePlayersChange}>
            <SelectTrigger id="players" className="bg-white dark:bg-zinc-900">
              <SelectValue placeholder="Qualsiasi" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">Qualsiasi</SelectItem>
              <SelectItem value="1-2">1-2 Giocatori</SelectItem>
              <SelectItem value="3-4">3-4 Giocatori</SelectItem>
              <SelectItem value="5+">5+ Giocatori</SelectItem>
            </SelectContent>
          </Select>
        </div>
      </div>
    </div>
  );
}
```

Key changes:
- Import `useGameCategories` hook
- Fetch categories dynamically: `const { data: categories = [] } = useGameCategories()`
- Category `<SelectItem>` uses `cat.id` (UUID) as value, `cat.name` as label
- `onCategoryChange` now emits a UUID string (or "all")

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/admin/shared-games/game-filters.tsx
git commit -m "feat(shared-games): use dynamic categories from API in game filters"
```

---

## Task 5: Frontend — Wire Category Through GameCatalogGrid

**Files:**
- Modify: `apps/web/src/components/admin/shared-games/game-catalog-grid.tsx`

- [ ] **Step 1: Pass `categoryFilter` to the API call**

In `game-catalog-grid.tsx`, update the `useQuery` call (~line 209) to include `categoryId`:

```typescript
const apiCategoryId = categoryFilter !== 'all' ? categoryFilter : undefined;

const { data, isLoading } = useQuery({
  queryKey: [...sharedGamesKeys.all, 'admin-list', page, PAGE_SIZE, searchQuery, apiStatus, apiCategoryId],
  queryFn: () =>
    api.sharedGames.getAll({
      page,
      pageSize: PAGE_SIZE,
      status: apiStatus,
      search: searchQuery || undefined,
      categoryId: apiCategoryId,
    }),
  staleTime: 2 * 60 * 1000,
});
```

Add `const apiCategoryId = ...` right after the `const apiStatus = ...` line (~line 207).

- [ ] **Step 2: Verify frontend builds**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/admin/shared-games/game-catalog-grid.tsx
git commit -m "feat(shared-games): wire category filter to API call in GameCatalogGrid"
```

---

## Task 6: Manual Verification

- [ ] **Step 1: Start dev environment**

Run: `cd infra && make dev-core`

- [ ] **Step 2: Open browser at http://localhost:3000/admin/shared-games/all**

Verify:
1. Category dropdown shows real categories from the database (not hardcoded "Strategia", "Party", etc.)
2. Selecting a category filters the grid (server-side)
3. Status filter works: selecting "Bozza", "In Attesa", "Archiviato" shows correct games (or empty if no games have that status)
4. Combined filters work: category + status + search together
5. Pagination resets to page 1 when filters change

- [ ] **Step 3: Check if mixed statuses exist in DB**

If all games show as "Pubblicato" regardless of filter, it's a data issue — all seeded games are Published. This is expected behavior, not a bug. To test, manually change one game's status via the admin detail page, then verify the filter correctly shows/hides it.

- [ ] **Step 4: Final commit with all changes**

```bash
git add -A
git commit -m "feat(shared-games): wire category filter end-to-end on admin all-games page

- Added categoryId to GetFilteredSharedGamesQuery and handler
- Exposed categoryId query param on admin endpoint
- Frontend: dynamic categories from API, passed to server-side filter
- Replaced hardcoded category options with real DB categories"
```

---

## Summary of Changes

| Layer | File | Change |
|-------|------|--------|
| **BE Query** | `GetFilteredSharedGamesQuery.cs` | Add `Guid? CategoryId` param |
| **BE Handler** | `GetFilteredSharedGamesQueryHandler.cs` | Add `.Where(g => g.Categories.Any(...))` |
| **BE Endpoint** | `SharedGameCatalogAdminEndpoints.cs` | Add `[FromQuery] Guid? categoryId` |
| **FE Client** | `sharedGamesClient.ts` | Add `categoryId` to `getAll()` params |
| **FE Filters** | `game-filters.tsx` | Dynamic categories via `useGameCategories()` |
| **FE Grid** | `game-catalog-grid.tsx` | Pass `categoryFilter` as `categoryId` to API |
