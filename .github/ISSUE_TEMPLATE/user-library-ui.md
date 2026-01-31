---
name: User Library UI Implementation
about: Implement user library management UI (view, add, edit, remove games)
title: 'feat(frontend): implement user library management page'
labels: ['enhancement', 'frontend', 'user-library', 'ui']
assignees: ''
---

## Summary

Implement frontend UI for user library management. Backend API, client, and hooks already exist but there's no user-facing page to manage the library.

## Current State

### ✅ Already Implemented (Backend)

**API Endpoints** (fully functional):
- `GET /api/v1/library` - Get user's library (paginated, filterable)
- `GET /api/v1/library/stats` - Library statistics
- `POST /api/v1/library/games/{gameId}` - Add game to library
- `DELETE /api/v1/library/games/{gameId}` - Remove game from library
- `PUT /api/v1/library/games/{gameId}` - Update library entry (notes, favorite)
- `GET /api/v1/library/games/{gameId}/status` - Check if game in library

**Tested**: All endpoints verified working (2026-01-15)

### ✅ Already Implemented (Frontend Infrastructure)

**API Client**: `apps/web/src/lib/api/clients/libraryClient.ts`
- Full CRUD operations
- Type-safe with Zod schemas
- Error handling

**React Hooks**: `apps/web/src/hooks/queries/useLibrary.ts`
- React Query integration
- Optimistic updates
- Cache invalidation

**Schemas**: `apps/web/src/lib/api/schemas/library.schemas.ts`
- TypeScript types
- Zod validation
- Request/response DTOs

### ❌ Missing (UI Implementation)

**No dedicated library page** - `/library` route doesn't exist

**Dashboard shows games** but no library management:
- Recent games section shows catalog games
- No "My Library" section
- No add/edit/remove UI components

## Requirements

### User Stories

**As a user, I want to**:
1. View all games in my library (paginated list with search/filter)
2. Add a game to my library from the catalog
3. Mark games as favorites
4. Add personal notes to library entries
5. Remove games from my library
6. See library statistics (total games, favorites count)

### UI Components Needed

**1. Library Page** (`/library` or `/my-games`)
```
Location: apps/web/src/app/(dashboard)/library/page.tsx

Components:
- LibraryHeader (title, stats, add game button)
- LibraryFilters (favorites only, search)
- LibraryGrid/List (game cards with actions)
- EmptyState (when library is empty)
```

**2. Game Card Actions**
```
For each game in library:
- ⭐ Toggle favorite
- ✏️ Edit notes
- 🗑️ Remove from library
```

**3. Add Game Flow**
```
From games catalog:
- "Add to Library" button on game cards
- Modal/dialog for notes and favorite status
- Success toast notification
```

### Acceptance Criteria

**Must Have**:
- [ ] `/library` route accessible from dashboard navigation
- [ ] Display user's library games in grid/list view
- [ ] Search and filter library (by title, favorites)
- [ ] Add game to library from catalog page
- [ ] Remove game from library (with confirmation)
- [ ] Mark/unmark games as favorites
- [ ] Empty state when library is empty
- [ ] Loading states for all async operations
- [ ] Error handling with user-friendly messages

**Should Have**:
- [ ] Edit notes for library entries
- [ ] Library statistics (total games, favorites)
- [ ] Sort library (by date added, title, favorite)
- [ ] Responsive design (mobile + desktop)
- [ ] Pagination for large libraries (>20 games)

**Nice to Have**:
- [ ] Bulk operations (mark multiple as favorite, bulk remove)
- [ ] Export library to CSV/JSON
- [ ] Library sharing (public profile)
- [ ] Recently added section in dashboard

### Technical Implementation

**Suggested Approach**:

**1. Create Library Page**
```typescript
// apps/web/src/app/(dashboard)/library/page.tsx
'use client';

import { useLibrary } from '@/hooks/queries/useLibrary';
import { LibraryGrid } from '@/components/library/LibraryGrid';
import { LibraryFilters } from '@/components/library/LibraryFilters';

export default function LibraryPage() {
  const { data, isLoading } = useLibrary();

  return (
    <div>
      <LibraryHeader stats={data?.stats} />
      <LibraryFilters />
      <LibraryGrid entries={data?.entries} />
    </div>
  );
}
```

**2. Add Library Link to Navigation**
```typescript
// apps/web/src/components/layout/app-shell.tsx
<NavItem href="/library" icon={BookOpen}>
  La Mia Libreria
</NavItem>
```

**3. Add "Add to Library" to Game Cards**
```typescript
// apps/web/src/components/games/GameCard.tsx
import { useAddToLibrary } from '@/hooks/queries/useLibrary';

function GameCard({ game }) {
  const { mutate: addToLibrary } = useAddToLibrary();

  return (
    <Card>
      {/* ... */}
      <Button onClick={() => addToLibrary(game.id)}>
        Add to Library
      </Button>
    </Card>
  );
}
```

**4. Library Management Actions**
```typescript
// apps/web/src/components/library/LibraryCard.tsx
import { useRemoveFromLibrary, useUpdateLibraryEntry } from '@/hooks/queries/useLibrary';

function LibraryCard({ entry }) {
  const { mutate: remove } = useRemoveFromLibrary();
  const { mutate: update } = useUpdateLibraryEntry();

  return (
    <Card>
      <GameInfo game={entry} />
      <Button onClick={() => update({ gameId: entry.gameId, isFavorite: !entry.isFavorite })}>
        {entry.isFavorite ? '⭐' : '☆'}
      </Button>
      <Button onClick={() => remove(entry.gameId)}>
        Remove
      </Button>
    </Card>
  );
}
```

### Testing Requirements

**Unit Tests**:
- [ ] LibraryPage component rendering
- [ ] LibraryFilters state management
- [ ] Add/remove/update operations

**Integration Tests**:
- [ ] Full library workflow (add → view → edit → remove)
- [ ] Pagination and filtering
- [ ] Error states (network failures, API errors)

**E2E Tests** (Playwright):
- [ ] User adds game to library from catalog
- [ ] User views library and sees added game
- [ ] User marks game as favorite
- [ ] User removes game from library
- [ ] Library empty state displays correctly

### Design Notes

**Follow Existing Patterns**:
- Use shadcn/ui components (Card, Button, Badge)
- Match games catalog grid/list toggle
- Consistent with dashboard design system
- Italian i18n strings

**Accessibility**:
- ARIA labels for all actions
- Keyboard navigation support
- Screen reader announcements for state changes

## Related

**API Documentation**: `docs/03-api/README.md` (User Library section)
**Test Report**: `docs/claudedocs/user-library-test-report-2026-01-15.md`
**Backend Implementation**: Fully complete and tested
**Client/Hooks**: Already implemented in `apps/web/src/lib/api/clients/libraryClient.ts`

## Priority

**Medium** - Backend is complete, frontend infrastructure ready, just needs UI pages

## Estimated Effort

**2-3 days**:
- Day 1: Library page + navigation integration
- Day 2: Add to library from catalog + library management actions
- Day 3: Testing + polish

---

**Created**: 2026-01-15
**Backend Status**: ✅ Complete and tested
**Frontend Infrastructure**: ✅ Client and hooks ready
**Missing**: UI pages only
