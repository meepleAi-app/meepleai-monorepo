# Epic: Personal Library - Collection Management

## Overview
User's personal game library with search, filters, bulk operations, grid/list views, and favorites management.

## Design Mockup
`docs/design-proposals/meepleai-style/complete-mockups.html` (Tab 1)

## Key Features
- Search by game title (debounced)
- Filter chips: All, Favorites, Nuovo, In Prestito, Wishlist
- Sort: Recent, A-Z, Most Played, Favorite
- Grid/List view toggle
- Bulk selection mode with floating action bar
- Game cards with stats (plays, win rate)
- Quick actions: Edit notes, Change state, Remove
- Quota indicator (sticky header)

## Implementation Issues (8 total)

### Backend (3)
1. GetUserLibraryQuery (with filters, sort, pagination)
2. UpdateGameNotesCommand
3. RemoveFromLibraryCommand (soft delete)

### Frontend (4)
4. Library page with search + filters
5. Game cards (grid + list views)
6. Bulk selection mode + floating action bar
7. Quota sticky header component

### Testing (1)
8. Library E2E tests (search, filter, bulk operations)

## Timeline
2-3 weeks | Priority: HIGH

## Labels
`epic`, `personal-library`, `frontend`, `backend`
