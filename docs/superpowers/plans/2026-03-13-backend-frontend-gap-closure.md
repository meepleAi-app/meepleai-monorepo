# Backend ↔ Frontend Gap Closure — Master Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close all identified gaps between backend API endpoints and frontend UI — bringing 6 orphaned backend features to full frontend integration.

**Architecture:** Each epic is independent and self-contained. Frontend follows existing patterns: Zod schema → API client → React Query hook → page/component. Backend endpoints already exist — this plan is frontend-focused with minimal backend changes (feature flags endpoint fix). Each epic ships independently and can be merged separately.

**Tech Stack:** Next.js 16 (App Router), React 19, TypeScript, Zod, TanStack React Query, Zustand, Tailwind 4, shadcn/ui, Vitest, Playwright

**Gap Analysis Source:** Spec Panel analysis session 2026-03-13

---

## Epic Overview

| # | Epic | Severity | Est. Tasks | Dependencies |
|---|------|----------|:----------:|:------------:|
| 1 | Playlists (Game Night Planning) | 🔴 Critical | 8 | Game Nights feature |
| 2 | Wishlist Page & Hooks | 🟡 Major | 5 | Library feature |
| 3 | Play Records Integration | 🟡 Major | 4 | Sessions feature |
| 4 | Feature Flags (Server-Driven) | 🟡 Major | 5 | None |
| 5 | User Alerts Page | 🟡 Major | 4 | Notifications feature |
| 6 | Game Reviews Mutations | 🟢 Minor | 3 | Games feature |

**Excluded from this plan:**
- Business Simulations (confirmed future release — no frontend work now)
- API Keys advanced features (low priority, power user)
- Specialized agent direct endpoints (working via unified SSE)

---

## File Structure

### Epic 1: Playlists

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/playlists.schemas.ts` | Zod schemas for playlist DTOs |
| `apps/web/src/lib/api/clients/playlistsClient.ts` | HTTP client for `/api/v1/playlists` endpoints |
| `apps/web/src/hooks/queries/usePlaylists.ts` | React Query hooks (CRUD, reorder, share) |
| `apps/web/src/app/(authenticated)/library/playlists/page.tsx` | Playlists list page |
| `apps/web/src/app/(authenticated)/library/playlists/[id]/page.tsx` | Playlist detail page |
| `apps/web/src/app/(authenticated)/library/playlists/shared/[token]/page.tsx` | Public shared playlist page |
| `apps/web/src/components/playlists/PlaylistCard.tsx` | Playlist card component |
| `apps/web/src/components/playlists/PlaylistDetail.tsx` | Playlist detail with drag-reorder |
| `apps/web/src/components/playlists/PlaylistForm.tsx` | Create/edit form (name + scheduled date) |
| `apps/web/src/components/playlists/PlaylistShareDialog.tsx` | Share token dialog |
| `apps/web/src/components/playlists/__tests__/PlaylistCard.test.tsx` | Unit tests |
| `apps/web/src/components/playlists/__tests__/PlaylistDetail.test.tsx` | Unit tests |
| `apps/web/src/components/playlists/__tests__/PlaylistForm.test.tsx` | Unit tests |
| `apps/web/src/hooks/queries/__tests__/usePlaylists.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register playlists client |
| `apps/web/src/app/(authenticated)/library/layout.tsx` | Add "Playlists" tab in library nav |

### Epic 2: Wishlist

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/wishlist.schemas.ts` | Zod schemas for wishlist DTOs |
| `apps/web/src/lib/api/clients/wishlistClient.ts` | HTTP client for `/api/v1/wishlist` endpoints |
| `apps/web/src/hooks/queries/useWishlist.ts` | React Query hooks (list, add, update, remove, highlights) |
| `apps/web/src/app/(authenticated)/library/wishlist/page.tsx` | Wishlist page |
| `apps/web/src/components/wishlist/WishlistCard.tsx` | Wishlist item card |
| `apps/web/src/components/wishlist/AddToWishlistDialog.tsx` | Add game to wishlist dialog |
| `apps/web/src/components/wishlist/__tests__/WishlistCard.test.tsx` | Unit tests |
| `apps/web/src/hooks/queries/__tests__/useWishlist.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register wishlist client |
| `apps/web/src/app/(authenticated)/library/layout.tsx` | Add "Wishlist" tab in library nav |
| `apps/web/src/components/game-detail/GameDetailActions.tsx` | Add "Add to Wishlist" button |

### Epic 3: Play Records

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/play-records.schemas.ts` | Zod schemas for play record DTOs |
| `apps/web/src/lib/api/clients/playRecordsClient.ts` | HTTP client for `/api/v1/play-records` |
| `apps/web/src/hooks/queries/usePlayRecords.ts` | React Query hooks (record, stats) |
| `apps/web/src/components/sessions/RecordPlayDialog.tsx` | Record a play dialog |
| `apps/web/src/components/sessions/PlayStats.tsx` | Play statistics display |
| `apps/web/src/components/sessions/__tests__/RecordPlayDialog.test.tsx` | Unit tests |
| `apps/web/src/hooks/queries/__tests__/usePlayRecords.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register playRecords client |
| `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` | Add play stats section |

### Epic 4: Feature Flags (Server-Driven)

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/feature-flags.schemas.ts` | Zod schemas for feature flag DTOs |
| `apps/web/src/lib/api/clients/featureFlagsClient.ts` | HTTP client for `/api/v1/flags` |
| `apps/web/src/hooks/queries/useFeatureFlags.ts` | React Query hooks (list, get by name) |
| `apps/web/src/providers/FeatureFlagProvider.tsx` | Context provider fetching flags from backend |
| `apps/web/src/hooks/useFeatureFlag.ts` | `useFeatureFlag(name)` consumer hook |
| `apps/web/src/providers/__tests__/FeatureFlagProvider.test.tsx` | Provider tests |
| `apps/web/src/hooks/__tests__/useFeatureFlag.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register featureFlags client |
| `apps/web/src/components/ui/gates/FeatureGate.tsx` | Refactor to use server-driven flags |
| `apps/web/src/app/(authenticated)/layout.tsx` | Wrap with FeatureFlagProvider |

### Epic 5: User Alerts

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/user-alerts.schemas.ts` | Zod schemas for alert DTOs |
| `apps/web/src/lib/api/clients/userAlertsClient.ts` | HTTP client for `/api/v1/alerts` |
| `apps/web/src/hooks/queries/useAlerts.ts` | React Query hooks (list, config, resolve) |
| `apps/web/src/app/(authenticated)/notifications/alerts/page.tsx` | User alerts page |
| `apps/web/src/components/alerts/AlertCard.tsx` | Alert display card |
| `apps/web/src/components/alerts/AlertConfigPanel.tsx` | Alert preferences panel |
| `apps/web/src/components/alerts/__tests__/AlertCard.test.tsx` | Unit tests |
| `apps/web/src/hooks/queries/__tests__/useAlerts.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register userAlerts client |

### Epic 6: Game Reviews Mutations

#### New Files
| File | Responsibility |
|------|---------------|
| `apps/web/src/lib/api/schemas/reviews.schemas.ts` | Zod schemas for review DTOs |
| `apps/web/src/lib/api/clients/reviewsClient.ts` | HTTP client for game reviews |
| `apps/web/src/hooks/queries/useReviews.ts` | React Query hooks (list, create, update, delete) |
| `apps/web/src/components/reviews/ReviewForm.tsx` | Create/edit review form |
| `apps/web/src/components/reviews/__tests__/ReviewForm.test.tsx` | Unit tests |
| `apps/web/src/hooks/queries/__tests__/useReviews.test.ts` | Hook tests |

#### Modified Files
| File | Change |
|------|--------|
| `apps/web/src/lib/api/clients/index.ts` | Register reviews client |
| `apps/web/src/app/(authenticated)/games/[id]/reviews/page.tsx` | Add review form |

---

## Chunk 1: Playlists (Game Night Planning)

### Task 1.1: Playlist Zod Schemas

**Files:**
- Create: `apps/web/src/lib/api/schemas/playlists.schemas.ts`

- [ ] **Step 1: Create Zod schemas matching backend DTOs**

```typescript
import { z } from 'zod';

// Matches PlaylistGameDto from backend
export const playlistGameSchema = z.object({
  sharedGameId: z.string().uuid(),
  position: z.number().int().min(1),
  addedAt: z.string().datetime(),
  // Denormalized for display
  title: z.string().optional(),
  imageUrl: z.string().url().nullable().optional(),
});

export const playlistSchema = z.object({
  id: z.string().uuid(),
  name: z.string().min(1).max(100),
  scheduledDate: z.string().datetime().nullable(),
  creatorUserId: z.string().uuid(),
  isShared: z.boolean(),
  shareToken: z.string().nullable(),
  isDeleted: z.boolean(),
  games: z.array(playlistGameSchema),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
});

export const playlistListSchema = z.object({
  playlists: z.array(playlistSchema),
  total: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export const createPlaylistRequestSchema = z.object({
  name: z.string().min(1).max(100),
  scheduledDate: z.string().datetime().nullable().optional(),
});

export const addGameToPlaylistRequestSchema = z.object({
  sharedGameId: z.string().uuid(),
  position: z.number().int().min(1).optional(), // auto-append if omitted
});

export const reorderGamesRequestSchema = z.object({
  gameIds: z.array(z.string().uuid()),
});

// Types
export type Playlist = z.infer<typeof playlistSchema>;
export type PlaylistGame = z.infer<typeof playlistGameSchema>;
export type PlaylistList = z.infer<typeof playlistListSchema>;
export type CreatePlaylistRequest = z.infer<typeof createPlaylistRequestSchema>;
export type AddGameToPlaylistRequest = z.infer<typeof addGameToPlaylistRequestSchema>;
export type ReorderGamesRequest = z.infer<typeof reorderGamesRequestSchema>;
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/lib/api/schemas/playlists.schemas.ts
git commit -m "feat(playlists): add Zod schemas for playlist DTOs"
```

### Task 1.2: Playlist API Client

**Files:**
- Create: `apps/web/src/lib/api/clients/playlistsClient.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts`

**Reference:** Follow pattern in `apps/web/src/lib/api/clients/collectionsClient.ts` for factory pattern.

- [ ] **Step 1: Create playlists API client**

```typescript
import type { HttpClient } from '../core/httpClient';
import {
  playlistSchema,
  playlistListSchema,
  type CreatePlaylistRequest,
  type AddGameToPlaylistRequest,
  type ReorderGamesRequest,
  type Playlist,
  type PlaylistList,
} from '../schemas/playlists.schemas';

export function createPlaylistsClient({ httpClient }: { httpClient: HttpClient }) {
  const BASE = '/api/v1/playlists';

  return {
    /** List user's playlists (paginated) */
    async list(page = 1, pageSize = 20): Promise<PlaylistList> {
      return httpClient.get(`${BASE}?page=${page}&pageSize=${pageSize}`, {
        schema: playlistListSchema,
      });
    },

    /** Get playlist by ID */
    async get(id: string): Promise<Playlist> {
      return httpClient.get(`${BASE}/${id}`, { schema: playlistSchema });
    },

    /** Create new playlist */
    async create(data: CreatePlaylistRequest): Promise<Playlist> {
      return httpClient.post(BASE, { body: data, schema: playlistSchema });
    },

    /** Update playlist (name/date) */
    async update(id: string, data: Partial<CreatePlaylistRequest>): Promise<Playlist> {
      return httpClient.put(`${BASE}/${id}`, { body: data, schema: playlistSchema });
    },

    /** Soft delete playlist */
    async remove(id: string): Promise<void> {
      return httpClient.delete(`${BASE}/${id}`);
    },

    /** Add game to playlist */
    async addGame(playlistId: string, data: AddGameToPlaylistRequest): Promise<Playlist> {
      return httpClient.post(`${BASE}/${playlistId}/games`, {
        body: data,
        schema: playlistSchema,
      });
    },

    /** Remove game from playlist */
    async removeGame(playlistId: string, gameId: string): Promise<Playlist> {
      return httpClient.delete(`${BASE}/${playlistId}/games/${gameId}`, {
        schema: playlistSchema,
      });
    },

    /** Reorder games in playlist */
    async reorderGames(playlistId: string, data: ReorderGamesRequest): Promise<Playlist> {
      return httpClient.put(`${BASE}/${playlistId}/games/reorder`, {
        body: data,
        schema: playlistSchema,
      });
    },

    /** Generate share token */
    async share(playlistId: string): Promise<Playlist> {
      return httpClient.post(`${BASE}/${playlistId}/share`, { schema: playlistSchema });
    },

    /** Revoke share token */
    async unshare(playlistId: string): Promise<void> {
      return httpClient.delete(`${BASE}/${playlistId}/share`);
    },

    /** Get shared playlist (public, no auth) */
    async getShared(token: string): Promise<Playlist> {
      return httpClient.get(`${BASE}/shared/${token}`, { schema: playlistSchema });
    },
  };
}
```

- [ ] **Step 2: Register client in index.ts**

In `apps/web/src/lib/api/clients/index.ts`, add:
```typescript
import { createPlaylistsClient } from './playlistsClient';
// In the createApiClient factory, add:
playlists: createPlaylistsClient({ httpClient }),
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/playlistsClient.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(playlists): add API client for playlist CRUD and sharing"
```

### Task 1.3: Playlist React Query Hooks

**Files:**
- Create: `apps/web/src/hooks/queries/usePlaylists.ts`

**Reference:** Follow pattern in `apps/web/src/hooks/queries/useCollections.ts`

- [ ] **Step 1: Write hook tests**

Create `apps/web/src/hooks/queries/__tests__/usePlaylists.test.ts`:

```typescript
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { usePlaylists, usePlaylist, useCreatePlaylist, useDeletePlaylist } from '../usePlaylists';
import { createTestQueryWrapper } from '@/lib/__tests__/test-utils';

// Mock API client
vi.mock('@/lib/api', () => ({
  api: {
    playlists: {
      list: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      remove: vi.fn(),
    },
  },
}));

describe('usePlaylists', () => {
  const wrapper = createTestQueryWrapper();

  it('should fetch playlists list', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.playlists.list).mockResolvedValue({
      playlists: [{ id: '1', name: 'Game Night', games: [], isShared: false }],
      total: 1,
      page: 1,
      pageSize: 20,
    });

    const { result } = renderHook(() => usePlaylists(), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.playlists).toHaveLength(1);
  });

  it('should fetch single playlist', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.playlists.get).mockResolvedValue({
      id: '1',
      name: 'Game Night',
      games: [],
      isShared: false,
    });

    const { result } = renderHook(() => usePlaylist('1'), { wrapper });
    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(result.current.data?.name).toBe('Game Night');
  });
});
```

- [ ] **Step 2: Run tests — expect fail (hooks don't exist yet)**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/usePlaylists.test.ts
```

Expected: FAIL — module not found

- [ ] **Step 3: Implement hooks**

Create `apps/web/src/hooks/queries/usePlaylists.ts`:

```typescript
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { CreatePlaylistRequest, AddGameToPlaylistRequest, ReorderGamesRequest } from '@/lib/api/schemas/playlists.schemas';

export const playlistKeys = {
  all: ['playlists'] as const,
  lists: () => [...playlistKeys.all, 'list'] as const,
  list: (page: number) => [...playlistKeys.lists(), page] as const,
  details: () => [...playlistKeys.all, 'detail'] as const,
  detail: (id: string) => [...playlistKeys.details(), id] as const,
  shared: (token: string) => [...playlistKeys.all, 'shared', token] as const,
};

export function usePlaylists(page = 1, pageSize = 20, enabled = true) {
  return useQuery({
    queryKey: playlistKeys.list(page),
    queryFn: () => api.playlists.list(page, pageSize),
    enabled,
  });
}

export function usePlaylist(id: string, enabled = true) {
  return useQuery({
    queryKey: playlistKeys.detail(id),
    queryFn: () => api.playlists.get(id),
    enabled: enabled && !!id,
  });
}

export function useSharedPlaylist(token: string, enabled = true) {
  return useQuery({
    queryKey: playlistKeys.shared(token),
    queryFn: () => api.playlists.getShared(token),
    enabled: enabled && !!token,
  });
}

export function useCreatePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreatePlaylistRequest) => api.playlists.create(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.lists() }),
  });
}

export function useUpdatePlaylist(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<CreatePlaylistRequest>) => api.playlists.update(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: playlistKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: playlistKeys.lists() });
    },
  });
}

export function useDeletePlaylist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.playlists.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.lists() }),
  });
}

export function useAddGameToPlaylist(playlistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddGameToPlaylistRequest) => api.playlists.addGame(playlistId, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) }),
  });
}

export function useRemoveGameFromPlaylist(playlistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (gameId: string) => api.playlists.removeGame(playlistId, gameId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) }),
  });
}

export function useReorderPlaylistGames(playlistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReorderGamesRequest) => api.playlists.reorderGames(playlistId, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) }),
  });
}

export function useSharePlaylist(playlistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.playlists.share(playlistId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) }),
  });
}

export function useUnsharePlaylist(playlistId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.playlists.unshare(playlistId),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: playlistKeys.detail(playlistId) }),
  });
}
```

- [ ] **Step 4: Run tests — expect pass**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/usePlaylists.test.ts
```

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/queries/usePlaylists.ts apps/web/src/hooks/queries/__tests__/usePlaylists.test.ts
git commit -m "feat(playlists): add React Query hooks with tests"
```

### Task 1.4: Playlist Components

**Files:**
- Create: `apps/web/src/components/playlists/PlaylistCard.tsx`
- Create: `apps/web/src/components/playlists/PlaylistForm.tsx`
- Create: `apps/web/src/components/playlists/PlaylistDetail.tsx`
- Create: `apps/web/src/components/playlists/PlaylistShareDialog.tsx`

**Reference:** Follow MeepleCard patterns from `apps/web/src/components/ui/data-display/meeple-card/`

- [ ] **Step 1: Write PlaylistCard component test**

Create `apps/web/src/components/playlists/__tests__/PlaylistCard.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { PlaylistCard } from '../PlaylistCard';

describe('PlaylistCard', () => {
  const mockPlaylist = {
    id: '1',
    name: 'Friday Night Games',
    scheduledDate: '2026-03-20T19:00:00Z',
    games: [
      { sharedGameId: 'g1', position: 1, addedAt: '2026-03-13T10:00:00Z', title: 'Catan' },
      { sharedGameId: 'g2', position: 2, addedAt: '2026-03-13T10:01:00Z', title: 'Ticket to Ride' },
    ],
    isShared: false,
    creatorUserId: 'u1',
    shareToken: null,
    isDeleted: false,
    createdAt: '2026-03-13T10:00:00Z',
    updatedAt: null,
  };

  it('renders playlist name', () => {
    render(<PlaylistCard playlist={mockPlaylist} />);
    expect(screen.getByText('Friday Night Games')).toBeInTheDocument();
  });

  it('renders game count', () => {
    render(<PlaylistCard playlist={mockPlaylist} />);
    expect(screen.getByText('2 games')).toBeInTheDocument();
  });

  it('renders scheduled date when present', () => {
    render(<PlaylistCard playlist={mockPlaylist} />);
    expect(screen.getByText(/mar 20/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect fail**

```bash
cd apps/web && pnpm vitest run src/components/playlists/__tests__/PlaylistCard.test.tsx
```

- [ ] **Step 3: Implement PlaylistCard**

Create `apps/web/src/components/playlists/PlaylistCard.tsx`:

```tsx
'use client';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Calendar, List, Share2 } from 'lucide-react';
import type { Playlist } from '@/lib/api/schemas/playlists.schemas';
import Link from 'next/link';

interface PlaylistCardProps {
  playlist: Playlist;
}

export function PlaylistCard({ playlist }: PlaylistCardProps) {
  const gameCount = playlist.games.length;
  const scheduledDate = playlist.scheduledDate
    ? new Date(playlist.scheduledDate).toLocaleDateString('en-US', {
        month: 'short',
        day: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
      })
    : null;

  return (
    <Link href={`/library/playlists/${playlist.id}`}>
      <Card className="hover:border-primary/50 transition-colors cursor-pointer">
        <CardHeader className="pb-2">
          <div className="flex items-center justify-between">
            <CardTitle className="text-lg">{playlist.name}</CardTitle>
            {playlist.isShared && (
              <Badge variant="secondary" className="gap-1">
                <Share2 className="h-3 w-3" />
                Shared
              </Badge>
            )}
          </div>
        </CardHeader>
        <CardContent className="space-y-2">
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            <List className="h-4 w-4" />
            <span>{gameCount} {gameCount === 1 ? 'game' : 'games'}</span>
          </div>
          {scheduledDate && (
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <Calendar className="h-4 w-4" />
              <span>{scheduledDate}</span>
            </div>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
```

- [ ] **Step 4: Run test — expect pass**

```bash
cd apps/web && pnpm vitest run src/components/playlists/__tests__/PlaylistCard.test.tsx
```

- [ ] **Step 5: Implement PlaylistForm**

Create `apps/web/src/components/playlists/PlaylistForm.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { useCreatePlaylist, useUpdatePlaylist } from '@/hooks/queries/usePlaylists';
import type { Playlist } from '@/lib/api/schemas/playlists.schemas';

interface PlaylistFormProps {
  playlist?: Playlist; // If provided, edit mode
  trigger?: React.ReactNode;
  onSuccess?: () => void;
}

export function PlaylistForm({ playlist, trigger, onSuccess }: PlaylistFormProps) {
  const [open, setOpen] = useState(false);
  const [name, setName] = useState(playlist?.name ?? '');
  const [scheduledDate, setScheduledDate] = useState(
    playlist?.scheduledDate ? playlist.scheduledDate.slice(0, 16) : ''
  );

  const createMutation = useCreatePlaylist();
  const updateMutation = useUpdatePlaylist(playlist?.id ?? '');

  const isEditing = !!playlist;
  const mutation = isEditing ? updateMutation : createMutation;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    await mutation.mutateAsync({
      name,
      scheduledDate: scheduledDate ? new Date(scheduledDate).toISOString() : null,
    });
    setOpen(false);
    onSuccess?.();
  };

  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        {trigger ?? <Button>{isEditing ? 'Edit' : 'New Playlist'}</Button>}
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{isEditing ? 'Edit Playlist' : 'Create Playlist'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="name">Name</Label>
            <Input
              id="name"
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Friday Night Games"
              required
              maxLength={100}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="scheduledDate">Scheduled Date (optional)</Label>
            <Input
              id="scheduledDate"
              type="datetime-local"
              value={scheduledDate}
              onChange={(e) => setScheduledDate(e.target.value)}
            />
          </div>
          <Button type="submit" disabled={mutation.isPending} className="w-full">
            {mutation.isPending ? 'Saving...' : isEditing ? 'Save Changes' : 'Create'}
          </Button>
        </form>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 6: Implement PlaylistDetail with drag-reorder**

Create `apps/web/src/components/playlists/PlaylistDetail.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { GripVertical, Trash2, Share2, LinkIcon } from 'lucide-react';
import {
  usePlaylist,
  useRemoveGameFromPlaylist,
  useReorderPlaylistGames,
} from '@/hooks/queries/usePlaylists';
import { PlaylistForm } from './PlaylistForm';
import { PlaylistShareDialog } from './PlaylistShareDialog';
import type { PlaylistGame } from '@/lib/api/schemas/playlists.schemas';

interface PlaylistDetailProps {
  playlistId: string;
}

export function PlaylistDetail({ playlistId }: PlaylistDetailProps) {
  const { data: playlist, isLoading } = usePlaylist(playlistId);
  const removeGame = useRemoveGameFromPlaylist(playlistId);
  const reorder = useReorderPlaylistGames(playlistId);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

  if (isLoading) return <div className="animate-pulse h-64 bg-muted rounded-lg" />;
  if (!playlist) return <div className="text-center text-muted-foreground">Playlist not found</div>;

  const handleDragStart = (index: number) => setDraggedIndex(index);

  const handleDrop = (targetIndex: number) => {
    if (draggedIndex === null || draggedIndex === targetIndex) return;
    const newOrder = [...playlist.games].sort((a, b) => a.position - b.position);
    const [moved] = newOrder.splice(draggedIndex, 1);
    newOrder.splice(targetIndex, 0, moved);
    reorder.mutate({ gameIds: newOrder.map((g) => g.sharedGameId) });
    setDraggedIndex(null);
  };

  const sortedGames = [...playlist.games].sort((a, b) => a.position - b.position);

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">{playlist.name}</h1>
          {playlist.scheduledDate && (
            <p className="text-muted-foreground">
              {new Date(playlist.scheduledDate).toLocaleDateString('en-US', {
                weekday: 'long',
                month: 'long',
                day: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
              })}
            </p>
          )}
        </div>
        <div className="flex gap-2">
          <PlaylistForm playlist={playlist} />
          <PlaylistShareDialog playlist={playlist} />
        </div>
      </div>

      {sortedGames.length === 0 ? (
        <Card>
          <CardContent className="py-8 text-center text-muted-foreground">
            No games added yet. Add games from the catalog!
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-2">
          {sortedGames.map((game, index) => (
            <Card
              key={game.sharedGameId}
              draggable
              onDragStart={() => handleDragStart(index)}
              onDragOver={(e) => e.preventDefault()}
              onDrop={() => handleDrop(index)}
              className="cursor-grab active:cursor-grabbing"
            >
              <CardContent className="flex items-center gap-3 py-3">
                <GripVertical className="h-5 w-5 text-muted-foreground" />
                <Badge variant="outline" className="min-w-8 justify-center">
                  {game.position}
                </Badge>
                <span className="flex-1 font-medium">{game.title ?? game.sharedGameId}</span>
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => removeGame.mutate(game.sharedGameId)}
                  disabled={removeGame.isPending}
                >
                  <Trash2 className="h-4 w-4 text-destructive" />
                </Button>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 7: Implement PlaylistShareDialog**

Create `apps/web/src/components/playlists/PlaylistShareDialog.tsx`:

```tsx
'use client';

import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Share2, Copy, Check, LinkOff } from 'lucide-react';
import { useSharePlaylist, useUnsharePlaylist } from '@/hooks/queries/usePlaylists';
import type { Playlist } from '@/lib/api/schemas/playlists.schemas';

interface PlaylistShareDialogProps {
  playlist: Playlist;
}

export function PlaylistShareDialog({ playlist }: PlaylistShareDialogProps) {
  const [copied, setCopied] = useState(false);
  const shareMutation = useSharePlaylist(playlist.id);
  const unshareMutation = useUnsharePlaylist(playlist.id);

  const shareUrl = playlist.shareToken
    ? `${window.location.origin}/library/playlists/shared/${playlist.shareToken}`
    : null;

  const handleCopy = async () => {
    if (!shareUrl) return;
    await navigator.clipboard.writeText(shareUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  };

  return (
    <Dialog>
      <DialogTrigger asChild>
        <Button variant="outline" size="icon">
          <Share2 className="h-4 w-4" />
        </Button>
      </DialogTrigger>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Share Playlist</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          {playlist.isShared && shareUrl ? (
            <>
              <div className="flex gap-2">
                <Input value={shareUrl} readOnly className="flex-1" />
                <Button variant="outline" size="icon" onClick={handleCopy}>
                  {copied ? <Check className="h-4 w-4" /> : <Copy className="h-4 w-4" />}
                </Button>
              </div>
              <Button
                variant="destructive"
                onClick={() => unshareMutation.mutate()}
                disabled={unshareMutation.isPending}
                className="w-full"
              >
                <LinkOff className="h-4 w-4 mr-2" />
                Revoke Share Link
              </Button>
            </>
          ) : (
            <Button
              onClick={() => shareMutation.mutate()}
              disabled={shareMutation.isPending}
              className="w-full"
            >
              <Share2 className="h-4 w-4 mr-2" />
              Generate Share Link
            </Button>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
}
```

- [ ] **Step 8: Commit components**

```bash
git add apps/web/src/components/playlists/
git commit -m "feat(playlists): add PlaylistCard, PlaylistForm, PlaylistDetail, PlaylistShareDialog"
```

### Task 1.5: Playlist Pages

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/playlists/page.tsx`
- Create: `apps/web/src/app/(authenticated)/library/playlists/[id]/page.tsx`
- Create: `apps/web/src/app/(authenticated)/library/playlists/shared/[token]/page.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/layout.tsx`

- [ ] **Step 1: Create playlists list page**

Create `apps/web/src/app/(authenticated)/library/playlists/page.tsx`:

```tsx
'use client';

import { usePlaylists, useDeletePlaylist } from '@/hooks/queries/usePlaylists';
import { PlaylistCard } from '@/components/playlists/PlaylistCard';
import { PlaylistForm } from '@/components/playlists/PlaylistForm';
import { Skeleton } from '@/components/ui/skeleton';
import { Plus } from 'lucide-react';
import { Button } from '@/components/ui/button';

export default function PlaylistsPage() {
  const { data, isLoading } = usePlaylists();

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Playlists</h1>
        <PlaylistForm
          trigger={
            <Button>
              <Plus className="h-4 w-4 mr-2" />
              New Playlist
            </Button>
          }
        />
      </div>

      {isLoading ? (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {Array.from({ length: 3 }).map((_, i) => (
            <Skeleton key={i} className="h-36 rounded-lg" />
          ))}
        </div>
      ) : data?.playlists.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <p>No playlists yet. Create one to plan your next game night!</p>
        </div>
      ) : (
        <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          {data?.playlists.map((playlist) => (
            <PlaylistCard key={playlist.id} playlist={playlist} />
          ))}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Create playlist detail page**

Create `apps/web/src/app/(authenticated)/library/playlists/[id]/page.tsx`:

```tsx
'use client';

import { use } from 'react';
import { PlaylistDetail } from '@/components/playlists/PlaylistDetail';

export default function PlaylistDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PlaylistDetail playlistId={id} />;
}
```

- [ ] **Step 3: Create shared playlist page (public)**

Create `apps/web/src/app/(authenticated)/library/playlists/shared/[token]/page.tsx`:

```tsx
'use client';

import { use } from 'react';
import { useSharedPlaylist } from '@/hooks/queries/usePlaylists';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Calendar, List } from 'lucide-react';

export default function SharedPlaylistPage({ params }: { params: Promise<{ token: string }> }) {
  const { token } = use(params);
  const { data: playlist, isLoading, error } = useSharedPlaylist(token);

  if (isLoading) return <div className="animate-pulse h-64 bg-muted rounded-lg" />;
  if (error || !playlist) return <div className="text-center py-12">Playlist not found or link expired.</div>;

  const sortedGames = [...playlist.games].sort((a, b) => a.position - b.position);

  return (
    <div className="max-w-2xl mx-auto space-y-6 p-6">
      <div>
        <h1 className="text-2xl font-bold">{playlist.name}</h1>
        {playlist.scheduledDate && (
          <p className="text-muted-foreground flex items-center gap-2 mt-1">
            <Calendar className="h-4 w-4" />
            {new Date(playlist.scheduledDate).toLocaleDateString('en-US', {
              weekday: 'long', month: 'long', day: 'numeric',
              hour: '2-digit', minute: '2-digit',
            })}
          </p>
        )}
        <p className="text-muted-foreground flex items-center gap-2 mt-1">
          <List className="h-4 w-4" />
          {sortedGames.length} {sortedGames.length === 1 ? 'game' : 'games'}
        </p>
      </div>

      <div className="space-y-2">
        {sortedGames.map((game) => (
          <Card key={game.sharedGameId}>
            <CardContent className="flex items-center gap-3 py-3">
              <Badge variant="outline" className="min-w-8 justify-center">{game.position}</Badge>
              <span className="font-medium">{game.title ?? 'Unknown Game'}</span>
            </CardContent>
          </Card>
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Add Playlists tab to library layout**

In `apps/web/src/app/(authenticated)/library/layout.tsx`, add a "Playlists" navigation item alongside existing library tabs (Games, Private, Proposals). Look for the tab/nav pattern and add:

```tsx
{ label: 'Playlists', href: '/library/playlists', icon: ListOrdered }
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/library/playlists/ apps/web/src/app/\(authenticated\)/library/layout.tsx
git commit -m "feat(playlists): add list, detail, and shared playlist pages"
```

---

## Chunk 2: Wishlist

### Task 2.1: Wishlist Schemas + Client

**Files:**
- Create: `apps/web/src/lib/api/schemas/wishlist.schemas.ts`
- Create: `apps/web/src/lib/api/clients/wishlistClient.ts`
- Modify: `apps/web/src/lib/api/clients/index.ts`

**Note:** Check if `apps/web/src/lib/api/wishlist.ts` or `wishlist-client.ts` already exist and contain partial implementations. If so, consolidate into `wishlistClient.ts` and delete old files.

- [ ] **Step 1: Create Zod schemas**

```typescript
// apps/web/src/lib/api/schemas/wishlist.schemas.ts
import { z } from 'zod';

export const wishlistItemSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid().nullable(),
  sharedGameId: z.string().uuid().nullable(),
  title: z.string(),
  imageUrl: z.string().url().nullable(),
  priority: z.enum(['low', 'medium', 'high']).nullable(),
  notes: z.string().nullable(),
  priceTarget: z.number().nullable(),
  addedAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
});

export const wishlistResponseSchema = z.object({
  items: z.array(wishlistItemSchema),
  total: z.number(),
});

export const wishlistHighlightsSchema = z.object({
  topPriority: z.array(wishlistItemSchema),
  recentlyAdded: z.array(wishlistItemSchema),
  total: z.number(),
});

export const addToWishlistRequestSchema = z.object({
  sharedGameId: z.string().uuid(),
  priority: z.enum(['low', 'medium', 'high']).optional(),
  notes: z.string().optional(),
  priceTarget: z.number().optional(),
});

export type WishlistItem = z.infer<typeof wishlistItemSchema>;
export type WishlistResponse = z.infer<typeof wishlistResponseSchema>;
export type WishlistHighlights = z.infer<typeof wishlistHighlightsSchema>;
export type AddToWishlistRequest = z.infer<typeof addToWishlistRequestSchema>;
```

- [ ] **Step 2: Create wishlist client**

```typescript
// apps/web/src/lib/api/clients/wishlistClient.ts
import type { HttpClient } from '../core/httpClient';
import {
  wishlistResponseSchema,
  wishlistHighlightsSchema,
  wishlistItemSchema,
  type AddToWishlistRequest,
  type WishlistResponse,
  type WishlistHighlights,
  type WishlistItem,
} from '../schemas/wishlist.schemas';

export function createWishlistClient({ httpClient }: { httpClient: HttpClient }) {
  const BASE = '/api/v1/wishlist';

  return {
    async list(): Promise<WishlistResponse> {
      return httpClient.get(BASE, { schema: wishlistResponseSchema });
    },
    async highlights(): Promise<WishlistHighlights> {
      return httpClient.get(`${BASE}/highlights`, { schema: wishlistHighlightsSchema });
    },
    async add(data: AddToWishlistRequest): Promise<WishlistItem> {
      return httpClient.post(BASE, { body: data, schema: wishlistItemSchema });
    },
    async update(id: string, data: Partial<AddToWishlistRequest>): Promise<WishlistItem> {
      return httpClient.put(`${BASE}/${id}`, { body: data, schema: wishlistItemSchema });
    },
    async remove(id: string): Promise<void> {
      return httpClient.delete(`${BASE}/${id}`);
    },
  };
}
```

- [ ] **Step 3: Register in index.ts and commit**

```bash
git add apps/web/src/lib/api/schemas/wishlist.schemas.ts apps/web/src/lib/api/clients/wishlistClient.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(wishlist): add Zod schemas and API client"
```

### Task 2.2: Wishlist Hooks

**Files:**
- Create: `apps/web/src/hooks/queries/useWishlist.ts`

- [ ] **Step 1: Create hooks with tests (same TDD pattern as Task 1.3)**

```typescript
// apps/web/src/hooks/queries/useWishlist.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { AddToWishlistRequest } from '@/lib/api/schemas/wishlist.schemas';

export const wishlistKeys = {
  all: ['wishlist'] as const,
  list: () => [...wishlistKeys.all, 'list'] as const,
  highlights: () => [...wishlistKeys.all, 'highlights'] as const,
};

export function useWishlist(enabled = true) {
  return useQuery({
    queryKey: wishlistKeys.list(),
    queryFn: () => api.wishlist.list(),
    enabled,
  });
}

export function useWishlistHighlights(enabled = true) {
  return useQuery({
    queryKey: wishlistKeys.highlights(),
    queryFn: () => api.wishlist.highlights(),
    enabled,
  });
}

export function useAddToWishlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: AddToWishlistRequest) => api.wishlist.add(data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: wishlistKeys.all }),
  });
}

export function useUpdateWishlistItem(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: Partial<AddToWishlistRequest>) => api.wishlist.update(id, data),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: wishlistKeys.all }),
  });
}

export function useRemoveFromWishlist() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.wishlist.remove(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: wishlistKeys.all }),
  });
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/hooks/queries/useWishlist.ts
git commit -m "feat(wishlist): add React Query hooks"
```

### Task 2.3: Wishlist Page + Components

**Files:**
- Create: `apps/web/src/app/(authenticated)/library/wishlist/page.tsx`
- Create: `apps/web/src/components/wishlist/WishlistCard.tsx`
- Create: `apps/web/src/components/wishlist/AddToWishlistDialog.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/layout.tsx`

- [ ] **Step 1: Create WishlistCard component**

Follow same pattern as PlaylistCard — display game title, priority badge, notes, price target.

- [ ] **Step 2: Create AddToWishlistDialog**

Dialog with: game search/select, priority (low/medium/high radio), notes textarea, price target input.

- [ ] **Step 3: Create Wishlist list page**

Page with: list of WishlistCard items, "Add to Wishlist" button, filter by priority, empty state.

- [ ] **Step 4: Add Wishlist tab to library layout**

```tsx
{ label: 'Wishlist', href: '/library/wishlist', icon: Heart }
```

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/wishlist/ apps/web/src/app/\(authenticated\)/library/wishlist/ apps/web/src/app/\(authenticated\)/library/layout.tsx
git commit -m "feat(wishlist): add wishlist page, cards, and add-to-wishlist dialog"
```

---

## Chunk 3: Play Records

### Task 3.1: Play Records Schema + Client + Hooks

**Files:**
- Create: `apps/web/src/lib/api/schemas/play-records.schemas.ts`
- Create: `apps/web/src/lib/api/clients/playRecordsClient.ts`
- Create: `apps/web/src/hooks/queries/usePlayRecords.ts`

- [ ] **Step 1: Create schemas**

```typescript
// apps/web/src/lib/api/schemas/play-records.schemas.ts
import { z } from 'zod';

export const playRecordSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  playedAt: z.string().datetime(),
  durationMinutes: z.number().nullable(),
  playerCount: z.number().int().min(1),
  notes: z.string().nullable(),
  winner: z.string().nullable(),
  createdAt: z.string().datetime(),
});

export const playStatsSchema = z.object({
  totalPlays: z.number(),
  uniqueGames: z.number(),
  totalDurationMinutes: z.number(),
  averagePlayerCount: z.number(),
  mostPlayedGameId: z.string().uuid().nullable(),
  mostPlayedGameTitle: z.string().nullable(),
  streakDays: z.number(),
});

export const recordPlayRequestSchema = z.object({
  gameId: z.string().uuid(),
  playedAt: z.string().datetime().optional(),
  durationMinutes: z.number().optional(),
  playerCount: z.number().int().min(1),
  notes: z.string().optional(),
  winner: z.string().optional(),
});

export type PlayRecord = z.infer<typeof playRecordSchema>;
export type PlayStats = z.infer<typeof playStatsSchema>;
export type RecordPlayRequest = z.infer<typeof recordPlayRequestSchema>;
```

- [ ] **Step 2: Create client and hooks (same TDD pattern)**

- [ ] **Step 3: Register in index.ts, commit**

```bash
git add apps/web/src/lib/api/schemas/play-records.schemas.ts apps/web/src/lib/api/clients/playRecordsClient.ts apps/web/src/hooks/queries/usePlayRecords.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(play-records): add schemas, client, and hooks"
```

### Task 3.2: Play Records UI

**Files:**
- Create: `apps/web/src/components/sessions/RecordPlayDialog.tsx`
- Create: `apps/web/src/components/sessions/PlayStats.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

- [ ] **Step 1: Create RecordPlayDialog**

Form dialog: game (pre-filled if from game page), date picker, duration, player count, notes, winner.

- [ ] **Step 2: Create PlayStats summary card**

Display: total plays, unique games, total hours, most played, streak.

- [ ] **Step 3: Add PlayStats to game detail page**

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/sessions/RecordPlayDialog.tsx apps/web/src/components/sessions/PlayStats.tsx
git commit -m "feat(play-records): add record play dialog and stats display"
```

---

## Chunk 4: Feature Flags (Server-Driven)

### Task 4.1: Feature Flags Schema + Client + Hooks

**Files:**
- Create: `apps/web/src/lib/api/schemas/feature-flags.schemas.ts`
- Create: `apps/web/src/lib/api/clients/featureFlagsClient.ts`
- Create: `apps/web/src/hooks/queries/useFeatureFlags.ts`

- [ ] **Step 1: Create schemas**

```typescript
// apps/web/src/lib/api/schemas/feature-flags.schemas.ts
import { z } from 'zod';

export const featureFlagSchema = z.object({
  name: z.string(),
  enabled: z.boolean(),
  description: z.string().nullable(),
  updatedAt: z.string().datetime().nullable(),
});

export const featureFlagsResponseSchema = z.object({
  flags: z.array(featureFlagSchema),
});

export type FeatureFlag = z.infer<typeof featureFlagSchema>;
export type FeatureFlagsResponse = z.infer<typeof featureFlagsResponseSchema>;
```

- [ ] **Step 2: Create client**

```typescript
// apps/web/src/lib/api/clients/featureFlagsClient.ts
import type { HttpClient } from '../core/httpClient';
import {
  featureFlagsResponseSchema,
  featureFlagSchema,
  type FeatureFlagsResponse,
  type FeatureFlag,
} from '../schemas/feature-flags.schemas';

export function createFeatureFlagsClient({ httpClient }: { httpClient: HttpClient }) {
  return {
    async list(): Promise<FeatureFlagsResponse> {
      return httpClient.get('/api/v1/flags', { schema: featureFlagsResponseSchema });
    },
    async get(name: string): Promise<FeatureFlag> {
      return httpClient.get(`/api/v1/flags/${name}`, { schema: featureFlagSchema });
    },
  };
}
```

- [ ] **Step 3: Create hooks, register, commit**

```bash
git add apps/web/src/lib/api/schemas/feature-flags.schemas.ts apps/web/src/lib/api/clients/featureFlagsClient.ts apps/web/src/hooks/queries/useFeatureFlags.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(feature-flags): add schemas, client, and hooks for server-driven flags"
```

### Task 4.2: FeatureFlagProvider + Refactor FeatureGate

**Files:**
- Create: `apps/web/src/providers/FeatureFlagProvider.tsx`
- Create: `apps/web/src/hooks/useFeatureFlag.ts`
- Modify: `apps/web/src/components/ui/gates/FeatureGate.tsx`
- Modify: `apps/web/src/app/(authenticated)/layout.tsx`

- [ ] **Step 1: Create FeatureFlagProvider**

```tsx
// apps/web/src/providers/FeatureFlagProvider.tsx
'use client';

import { createContext, useContext } from 'react';
import { useFeatureFlags } from '@/hooks/queries/useFeatureFlags';
import type { FeatureFlag } from '@/lib/api/schemas/feature-flags.schemas';

interface FeatureFlagContextValue {
  flags: FeatureFlag[];
  isLoading: boolean;
  isEnabled: (name: string) => boolean;
}

const FeatureFlagContext = createContext<FeatureFlagContextValue>({
  flags: [],
  isLoading: true,
  isEnabled: () => false,
});

export function FeatureFlagProvider({ children }: { children: React.ReactNode }) {
  const { data, isLoading } = useFeatureFlags();
  const flags = data?.flags ?? [];

  const isEnabled = (name: string): boolean => {
    const flag = flags.find((f) => f.name === name);
    return flag?.enabled ?? false;
  };

  return (
    <FeatureFlagContext.Provider value={{ flags, isLoading, isEnabled }}>
      {children}
    </FeatureFlagContext.Provider>
  );
}

export function useFeatureFlagContext() {
  return useContext(FeatureFlagContext);
}
```

- [ ] **Step 2: Create useFeatureFlag consumer hook**

```typescript
// apps/web/src/hooks/useFeatureFlag.ts
'use client';

import { useFeatureFlagContext } from '@/providers/FeatureFlagProvider';

export function useFeatureFlag(name: string): boolean {
  const { isEnabled } = useFeatureFlagContext();
  return isEnabled(name);
}
```

- [ ] **Step 3: Refactor FeatureGate to use server-driven flags**

In `apps/web/src/components/ui/gates/FeatureGate.tsx`, replace local config lookup with:

```tsx
import { useFeatureFlag } from '@/hooks/useFeatureFlag';

interface FeatureGateProps {
  feature: string;
  children: React.ReactNode;
  fallback?: React.ReactNode;
}

export function FeatureGate({ feature, children, fallback = null }: FeatureGateProps) {
  const isEnabled = useFeatureFlag(feature);
  return isEnabled ? <>{children}</> : <>{fallback}</>;
}
```

- [ ] **Step 4: Add FeatureFlagProvider to authenticated layout**

In `apps/web/src/app/(authenticated)/layout.tsx`, wrap children:

```tsx
import { FeatureFlagProvider } from '@/providers/FeatureFlagProvider';
// Inside layout return:
<FeatureFlagProvider>{children}</FeatureFlagProvider>
```

- [ ] **Step 5: Run existing FeatureGate tests to verify no regression**

```bash
cd apps/web && pnpm vitest run --grep "FeatureGate"
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/providers/FeatureFlagProvider.tsx apps/web/src/hooks/useFeatureFlag.ts apps/web/src/components/ui/gates/FeatureGate.tsx apps/web/src/app/\(authenticated\)/layout.tsx
git commit -m "feat(feature-flags): server-driven FeatureFlagProvider replacing local config"
```

---

## Chunk 5: User Alerts

### Task 5.1: User Alerts Schema + Client + Hooks

**Files:**
- Create: `apps/web/src/lib/api/schemas/user-alerts.schemas.ts`
- Create: `apps/web/src/lib/api/clients/userAlertsClient.ts`
- Create: `apps/web/src/hooks/queries/useAlerts.ts`

- [ ] **Step 1: Create schemas matching backend Alert DTOs**

```typescript
// apps/web/src/lib/api/schemas/user-alerts.schemas.ts
import { z } from 'zod';

export const alertSchema = z.object({
  id: z.string().uuid(),
  alertType: z.string(),
  severity: z.enum(['info', 'warning', 'error', 'critical']),
  title: z.string(),
  message: z.string(),
  isResolved: z.boolean(),
  resolvedAt: z.string().datetime().nullable(),
  createdAt: z.string().datetime(),
});

export const alertConfigSchema = z.object({
  emailOnCritical: z.boolean(),
  emailOnWarning: z.boolean(),
  pushEnabled: z.boolean(),
  mutedAlertTypes: z.array(z.string()),
});

export const alertsResponseSchema = z.object({
  alerts: z.array(alertSchema),
  total: z.number(),
});

export type Alert = z.infer<typeof alertSchema>;
export type AlertConfig = z.infer<typeof alertConfigSchema>;
export type AlertsResponse = z.infer<typeof alertsResponseSchema>;
```

- [ ] **Step 2: Create client, hooks, register, commit**

```bash
git add apps/web/src/lib/api/schemas/user-alerts.schemas.ts apps/web/src/lib/api/clients/userAlertsClient.ts apps/web/src/hooks/queries/useAlerts.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(alerts): add user-facing alerts schemas, client, and hooks"
```

### Task 5.2: Alerts Page + Components

**Files:**
- Create: `apps/web/src/app/(authenticated)/notifications/alerts/page.tsx`
- Create: `apps/web/src/components/alerts/AlertCard.tsx`
- Create: `apps/web/src/components/alerts/AlertConfigPanel.tsx`

- [ ] **Step 1: Create AlertCard**

Display: severity icon/color, title, message, timestamp, resolve button.

- [ ] **Step 2: Create AlertConfigPanel**

Checkboxes for: email on critical, email on warning, push enabled, muted alert types.

- [ ] **Step 3: Create alerts page**

Tabs: Active | Resolved. List of AlertCard. Settings gear → AlertConfigPanel.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/alerts/ apps/web/src/app/\(authenticated\)/notifications/alerts/
git commit -m "feat(alerts): add user alerts page with config panel"
```

---

## Chunk 6: Game Reviews Mutations

### Task 6.1: Reviews Schema + Client + Hooks

**Files:**
- Create: `apps/web/src/lib/api/schemas/reviews.schemas.ts`
- Create: `apps/web/src/lib/api/clients/reviewsClient.ts`
- Create: `apps/web/src/hooks/queries/useReviews.ts`

- [ ] **Step 1: Create schemas**

```typescript
// apps/web/src/lib/api/schemas/reviews.schemas.ts
import { z } from 'zod';

export const reviewSchema = z.object({
  id: z.string().uuid(),
  gameId: z.string().uuid(),
  userId: z.string().uuid(),
  userDisplayName: z.string(),
  rating: z.number().min(1).max(10),
  title: z.string().nullable(),
  body: z.string(),
  createdAt: z.string().datetime(),
  updatedAt: z.string().datetime().nullable(),
  helpfulCount: z.number(),
});

export const reviewsResponseSchema = z.object({
  reviews: z.array(reviewSchema),
  total: z.number(),
  averageRating: z.number().nullable(),
});

export const createReviewRequestSchema = z.object({
  rating: z.number().min(1).max(10),
  title: z.string().optional(),
  body: z.string().min(10),
});

export type Review = z.infer<typeof reviewSchema>;
export type ReviewsResponse = z.infer<typeof reviewsResponseSchema>;
export type CreateReviewRequest = z.infer<typeof createReviewRequestSchema>;
```

- [ ] **Step 2: Create client + hooks + register, commit**

```bash
git add apps/web/src/lib/api/schemas/reviews.schemas.ts apps/web/src/lib/api/clients/reviewsClient.ts apps/web/src/hooks/queries/useReviews.ts apps/web/src/lib/api/clients/index.ts
git commit -m "feat(reviews): add schemas, client, and mutation hooks"
```

### Task 6.2: Review Form Component

**Files:**
- Create: `apps/web/src/components/reviews/ReviewForm.tsx`
- Modify: `apps/web/src/app/(authenticated)/games/[id]/reviews/page.tsx`

- [ ] **Step 1: Create ReviewForm**

Form with: star rating (1-10), title input (optional), body textarea (min 10 chars), submit button.

- [ ] **Step 2: Add ReviewForm to reviews page**

In the existing reviews page, add the form at top (if user hasn't reviewed yet) or edit button on user's existing review.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/reviews/ReviewForm.tsx apps/web/src/app/\(authenticated\)/games/\[id\]/reviews/page.tsx
git commit -m "feat(reviews): add review form with create and edit support"
```

---

## Pre-Implementation Checklist

Before starting any epic:

- [ ] Verify backend endpoints are accessible: `curl http://localhost:8080/api/v1/{endpoint}` (with auth)
- [ ] Check if any of the old utility files (`wishlist.ts`, `wishlist-client.ts`, `play-records.api.ts`) need cleanup/consolidation
- [ ] Run existing tests to establish baseline: `cd apps/web && pnpm test`
- [ ] Verify `apps/web/src/lib/api/clients/index.ts` factory pattern before adding new clients

## Post-Implementation Validation

After each epic:

- [ ] All new tests pass: `cd apps/web && pnpm vitest run`
- [ ] TypeScript compiles: `cd apps/web && pnpm typecheck`
- [ ] Lint passes: `cd apps/web && pnpm lint`
- [ ] Manual verification: navigate to new pages in browser
- [ ] Create PR to `main-dev` branch
