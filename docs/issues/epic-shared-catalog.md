# Epic: Shared Catalog - Community Game Browse

## Overview
Public/user browse of shared game catalog with advanced filters, community stats, and add to library functionality.

## Design Mockup
`docs/design-proposals/meepleai-style/complete-mockups.html` (Tab 2)

## Key Features
- Advanced filters (Players, Complexity, Duration, Categories)
- Search + sort (Popular, Recent, Rating, A-Z)
- Game cards with community stats (rating, plays, contributors)
- "Add to Library" CTA with hover overlay (orange)
- "Already in Library" badge (green)
- Pagination controls

## Implementation Issues (7 total)

### Backend (2)
1. GetSharedCatalogQuery (advanced filters, pagination) - **May exist partially**
2. AddToLibraryCommand

### Frontend (4)
3. Advanced filter panel component
4. Catalog game cards with community stats
5. Add to Library overlay + optimistic UI
6. Pagination component

### Testing (1)
7. Catalog E2E (browse, filter, add to library flow)

## Existing Issues
Check #2743-2752 for related functionality - avoid duplicates

## Timeline
2 weeks | Priority: HIGH

## Labels
`epic`, `shared-catalog`, `frontend`, `backend`
