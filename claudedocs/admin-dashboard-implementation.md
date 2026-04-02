# Admin Dashboard Implementation

## Overview

Production-ready admin dashboard for MeepleAI with **Premium Board Game Collection** aesthetic. Combines professional data management with the warmth and tactile quality of a curated game library.

**Issue**: Dashboard Integration (#TBD)
**Design Philosophy**: Game collection shelf meets executive dashboard
**Stack**: Next.js 16, React 19, Tailwind 4, shadcn/ui, React Query, Zustand

---

## Design Aesthetic

### Visual Theme
- **Inspiration**: Premium board game collection on wooden shelves
- **Colors**: Warm amber/orange gradients, wood-tone neutrals, subtle paper textures
- **Typography**:
  - Headings: Quicksand (bold, display weight)
  - Body: Nunito (refined, accessible)
- **Layout**: Asymmetric grid inspired by game box spines
- **Depth**: Layered glassmorphism with subtle shadows

### Key Design Elements
1. **Game Box Stats Cards**: Stats displayed as game boxes on a shelf with colored spines
2. **Inventory Tables**: Data tables styled like collection inventory lists
3. **Badge System**: Achievement-style badges with playful icons
4. **Paper Texture**: Subtle noise overlay for warmth
5. **Amber Accents**: Consistent use of amber/orange for CTAs and highlights

---

## File Structure

```
apps/web/src/
├── app/admin/(enterprise)/dashboard/
│   └── page.tsx                           # Main dashboard page
├── components/admin/dashboard/
│   ├── dashboard-shell.tsx                # Layout wrapper with texture overlay
│   ├── stats-overview.tsx                 # Game box-style stat cards
│   ├── shared-games-section.tsx           # Approval queue table
│   ├── user-management-section.tsx        # User table + detail panel
│   └── dashboard-skeleton.tsx             # Loading states
├── lib/api/
│   └── admin-client.ts                    # API client for admin endpoints
└── types/
    └── admin-dashboard.ts                 # Shared TypeScript types
```

---

## Components

### 1. DashboardShell
**Purpose**: Layout wrapper with background effects
**Features**:
- Gradient background (slate → amber → orange)
- Subtle paper texture overlay
- Responsive container (max-width: 1600px)

### 2. StatsOverview
**Purpose**: Hero stats cards row (4 cards)
**Features**:
- Game box spine effect (colored left border)
- Glassmorphic cards with amber accents
- Staggered slide-in animation
- Icon badges in amber gradient containers
- Hover effects with gradient overlay

**Data**: `GET /admin/stats`

**Stats Displayed**:
1. Shared Games (total, published, pending)
2. Community (total users, active this month)
3. Approvals (rate %, pending count)
4. Activity (submissions last 7 days)

### 3. SharedGamesSection
**Purpose**: Approval queue management
**Features**:
- Search and filters (status, date range)
- Sortable table columns
- Bulk selection with checkboxes
- Quick actions: Approve, Reject, View Details
- Batch operations: Approve all, Reject all
- Pagination with page indicator

**Data**: `GET /admin/shared-games/approval-queue`

**Table Columns**:
- Checkbox (bulk select)
- Game Title
- Submitted By (user + date)
- Days Pending (badge with urgency color)
- PDF Count
- Actions (View, Approve, Reject)

### 4. UserManagementSection
**Purpose**: User table with detail panel
**Features**:
- Search and filters (role, tier)
- User table with inline actions
- Click row to open detail panel
- Slide-in detail sheet with:
  - User profile card
  - Library statistics (4 stat boxes)
  - Achievement badges
  - Quick actions (Email, Change Tier, Suspend, Impersonate)

**Data**:
- `GET /admin/users` (table)
- `GET /admin/users/{id}` (detail)
- `GET /admin/users/{userId}/library/stats` (stats)
- `GET /admin/users/{userId}/badges` (badges)

**Table Columns**:
- User (name + ID preview)
- Email
- Role (badge)
- Tier (colored badge)
- Level
- Status (Active/Suspended)
- Actions (View, Suspend/Unsuspend)

### 5. DashboardSkeleton
**Purpose**: Loading states
**Variants**:
- `stats`: 4 card skeletons with staggered animation
- `table`: Header, filters, and table row skeletons

---

## API Integration

### Admin Client (`lib/api/admin-client.ts`)

**Stats**:
```typescript
adminClient.getStats({ days?: number }): Promise<AdminStats>
```

**Shared Games**:
```typescript
adminClient.getApprovalQueue({
  page?: number;
  pageSize?: number;
  status?: string;
  search?: string;
}): Promise<PagedResult<ApprovalQueueItem>>

adminClient.batchApproveGames(gameIds: string[]): Promise<void>
adminClient.batchRejectGames(gameIds: string[]): Promise<void>
```

**User Management**:
```typescript
adminClient.getUsers({
  page?: number;
  pageSize?: number;
  role?: string;
  tier?: string;
  search?: string;
}): Promise<PagedResult<User>>

adminClient.getUserDetail(userId: string): Promise<User>
adminClient.getUserLibraryStats(userId: string): Promise<UserLibraryStats>
adminClient.getUserBadges(userId: string): Promise<UserBadge[]>

adminClient.suspendUser(userId: string): Promise<void>
adminClient.unsuspendUser(userId: string): Promise<void>
adminClient.updateUserTier(userId: string, tier: string): Promise<void>
adminClient.resetUserPassword(userId: string): Promise<void>
adminClient.impersonateUser(userId: string): Promise<{ sessionToken: string }>
```

### React Query Setup

**Queries**:
- `['admin-stats']` - Dashboard statistics (cached 5min)
- `['approval-queue', page, status, search]` - Approval queue with filters
- `['admin-users', page, role, tier, search]` - User list with filters
- `['user-detail', userId]` - User detail
- `['user-library-stats', userId]` - User library stats
- `['user-badges', userId]` - User badges

**Mutations**:
- `batchApproveGames` - Approve multiple games
- `batchRejectGames` - Reject multiple games
- `suspendUser` - Suspend user account
- `unsuspendUser` - Unsuspend user account

**Optimistic Updates**:
- All mutations invalidate relevant queries
- Toast notifications for user feedback

---

## Responsive Design

### Breakpoints
- **Mobile** (< 768px): Single column, collapsible sections
- **Tablet** (768px - 1024px): 2-column stats, stacked tables
- **Desktop** (> 1024px): 4-column stats, full tables

### Mobile Optimizations
- Stats cards: 1 column
- Tables: Horizontal scroll with sticky headers
- Filters: Stacked vertically
- Detail panel: Full-screen sheet
- Reduced padding and font sizes

---

## Accessibility

### ARIA Labels
- All interactive elements have labels
- Tables use proper semantic markup
- Form inputs have associated labels

### Keyboard Navigation
- Tab order follows visual flow
- Enter/Space for actions
- Escape to close modals
- Arrow keys for table navigation

### Screen Readers
- Status announcements for mutations
- Loading states announced
- Error messages read aloud

---

## Performance Optimizations

### Data Fetching
- React Query caching (5min default)
- Pagination to limit data transfer
- Search debouncing (300ms)
- Optimistic updates for instant UI feedback

### Rendering
- Suspense boundaries for code splitting
- Virtualized tables for large datasets (future)
- Memoized components where beneficial
- Skeleton screens for perceived performance

### Bundle Size
- Tree-shaken imports
- Dynamic imports for heavy components
- Optimized images and icons

---

## Error Handling

### Error Boundaries
- Component-level boundaries for resilience
- Fallback UI for broken components

### Toast Notifications
- Success: Green toast with checkmark
- Error: Red toast with error details
- Info: Blue toast for informational messages

### API Errors
- Network errors: Retry with exponential backoff
- 401 Unauthorized: Redirect to login
- 403 Forbidden: Show permission error
- 404 Not Found: Show not found message
- 500 Server Error: Show generic error + support link

---

## Testing Strategy

### Unit Tests
- Component rendering
- User interactions (click, type, select)
- State management (filters, selections)
- API client functions

### Integration Tests
- Full user flows (approve game, suspend user)
- Error scenarios
- Loading states
- Responsive behavior

### E2E Tests (Playwright)
- Critical admin workflows
- Cross-browser compatibility
- Accessibility audits

---

## Future Enhancements

### Phase 2 (Planned)
- **Real-time updates**: SSE for live approval queue
- **Advanced filters**: Date ranges, multi-select
- **Bulk user operations**: Export, role changes
- **Analytics charts**: Trends over time
- **Activity timeline**: System-wide activity feed

### Phase 3 (Future)
- **Virtualized tables**: Handle 1000+ rows efficiently
- **Advanced search**: Full-text search with highlighting
- **Customizable dashboards**: Drag-and-drop widgets
- **Export capabilities**: CSV, PDF reports
- **Audit logs**: Detailed admin action history

---

## Usage Instructions

### Development
```bash
cd apps/web
pnpm dev
# Navigate to: http://localhost:3000/admin/dashboard
```

### Required API Endpoints
Ensure backend implements:
- `GET /admin/stats`
- `GET /admin/shared-games/approval-queue`
- `POST /admin/shared-games/batch-approve`
- `POST /admin/shared-games/batch-reject`
- `GET /admin/users`
- `GET /admin/users/{id}`
- `GET /admin/users/{userId}/library/stats`
- `GET /admin/users/{userId}/badges`
- `POST /admin/users/{userId}/suspend`
- `POST /admin/users/{userId}/unsuspend`

### Environment Variables
```bash
NEXT_PUBLIC_API_URL=http://localhost:8080/api/v1
```

---

## Design Tokens

### Colors
```css
/* Primary Palette */
--amber-50: #fffbeb;
--amber-100: #fef3c7;
--amber-200: #fde68a;
--amber-500: #f59e0b;
--amber-900: #78350f;

--orange-50: #fff7ed;
--orange-100: #ffedd5;
--orange-500: #f97316;

/* Neutrals */
--slate-50: #f8fafc;
--slate-600: #475569;
--slate-700: #334155;
--slate-900: #0f172a;

/* Glassmorphic */
background: rgba(255, 255, 255, 0.7);
backdrop-filter: blur(12px);
border: 1px solid rgba(251, 191, 36, 0.6); /* amber-200/60 */
```

### Typography
```css
/* Headings */
font-family: 'Quicksand', sans-serif;
font-weight: 700; /* bold */
letter-spacing: -0.025em; /* tight */

/* Body */
font-family: 'Nunito', sans-serif;
font-weight: 400; /* normal */
```

### Spacing
```css
/* Card Padding */
padding: 1.5rem; /* p-6 */

/* Section Gaps */
gap: 2rem; /* gap-8 */

/* Grid Gaps */
gap: 1.5rem; /* gap-6 */
```

---

## Troubleshooting

### Issue: Stats not loading
**Solution**: Check API endpoint `/admin/stats` is implemented and accessible

### Issue: Table pagination not working
**Solution**: Verify backend returns `totalPages` in PagedResult

### Issue: Detail panel not opening
**Solution**: Check React Query key `['user-detail', userId]` is unique

### Issue: Animations not smooth
**Solution**: Ensure `tailwind.config.js` has proper animation settings

### Issue: Glassmorphic effect not visible
**Solution**: Verify `backdrop-blur-md` is supported (requires modern browser)

---

## Credits

**Design**: Premium Board Game Collection aesthetic
**Implementation**: MeepleAI Development Team
**Date**: 2026-02-12
**Version**: 1.0.0

---

## Related Documentation

- [CLAUDE.md](../CLAUDE.md) - Project development guide
- [Epic #3689](../memory/epic-3689.md) - Enterprise Admin Dashboard
- [API Documentation](http://localhost:8080/scalar/v1) - Backend API reference
- [Tailwind Docs](https://tailwindcss.com/docs) - Utility classes reference
- [React Query Docs](https://tanstack.com/query/latest) - Data fetching patterns
