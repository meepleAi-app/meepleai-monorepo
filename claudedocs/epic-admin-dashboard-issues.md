# Admin Dashboard - Issue Specifications

Epic breakdown with detailed specifications for each issue.

---

## Frontend Issues

### Issue #1: Collection Overview Block - Stats Display

**Title**: Implement Collection Overview Block with Real API Integration

**Description**:
Create the Collection Overview block using StatCard components to display key metrics from the admin stats API endpoint.

**Acceptance Criteria**:
- [ ] Block displays 4 stat cards: Shared Games, Community, Approval Rate, Activity
- [ ] Data fetched from `GET /admin/stats` endpoint
- [ ] React Query caching (5min cache)
- [ ] Loading states with skeletons
- [ ] "View Details →" link to `/admin/collection/overview`
- [ ] Responsive layout (1/2/4 columns based on screen size)
- [ ] Trend indicators show correctly
- [ ] Variant colors for approval rate (success >90%, warning <70%)

**Technical Details**:
```typescript
// Component: apps/web/src/components/admin/dashboard/stats-overview.tsx
// API: GET /admin/stats?days=30
// Uses: StatCard component (existing)
// Query: ['admin-stats']
```

**Files**:
- `apps/web/src/components/admin/dashboard/stats-overview.tsx` ✅ Created
- `apps/web/src/lib/api/admin-client.ts` ✅ Created (needs real API)

**Dependencies**: Issue #6 (Backend stats endpoint)

**Estimated**: 2-3 hours

---

### Issue #2: Approval Queue Block - Shared Games Management

**Title**: Implement Approval Queue Block with MeepleCard Grid/List Views

**Description**:
Create the Approval Queue block using MeepleCard components to display pending shared games with approval actions.

**Acceptance Criteria**:
- [ ] Block displays up to 6 games from approval queue
- [ ] Data fetched from `GET /admin/shared-games/approval-queue`
- [ ] Grid/List view toggle (MeepleCard variant)
- [ ] Search by game title (debounced 300ms)
- [ ] Status filter (All, Pending, Urgent 7+ days)
- [ ] Quick actions per card: Approve, Reject
- [ ] Bulk operations: Select multiple, Batch approve/reject
- [ ] "View All →" link to `/admin/shared-games/approvals`
- [ ] Toast notifications for actions
- [ ] Optimistic updates on approve/reject
- [ ] Urgent badge for games 7+ days pending

**Technical Details**:
```typescript
// Component: apps/web/src/components/admin/dashboard/shared-games-block.tsx
// API: GET /admin/shared-games/approval-queue?page=1&pageSize=6
// Uses: MeepleCard (entity="game", variant="grid"|"list")
// Mutations: batchApproveGames, batchRejectGames
```

**Files**:
- `apps/web/src/components/admin/dashboard/shared-games-block.tsx` ✅ Created
- `apps/web/src/lib/api/admin-client.ts` (needs real API)

**Dependencies**: Issue #7 (Backend approval queue endpoint)

**Estimated**: 4-5 hours

---

### Issue #3: User Management Block - User Cards & Detail Panel

**Title**: Implement User Management Block with Detail Panel

**Description**:
Create the User Management block using MeepleCard components for users with a slide-in detail panel showing complete user profile.

**Acceptance Criteria**:
- [ ] Block displays up to 6 users
- [ ] Data fetched from `GET /admin/users`
- [ ] Grid/List view toggle
- [ ] Search by name or email (debounced 300ms)
- [ ] Filters: Role (all, user, admin), Tier (all, free, normal, premium)
- [ ] Click card opens detail panel (Sheet from right)
- [ ] Detail panel shows:
  - [ ] User info card (name, email, role, tier, level, status)
  - [ ] Library statistics (4 stat boxes)
  - [ ] Achievement badges with icons
  - [ ] Quick actions (Email, Change Tier, Suspend, Impersonate)
- [ ] Quick actions on card: View Profile, Suspend/Unsuspend
- [ ] "View All →" link to `/admin/users/management`
- [ ] Toast notifications for actions
- [ ] Optimistic updates

**Technical Details**:
```typescript
// Component: apps/web/src/components/admin/dashboard/user-management-block.tsx
// APIs:
//   - GET /admin/users?page=1&pageSize=6&role=&tier=&search=
//   - GET /admin/users/{id}
//   - GET /admin/users/{userId}/library/stats
//   - GET /admin/users/{userId}/badges
// Uses: MeepleCard (entity="player", variant="grid"|"list")
// Mutations: suspendUser, unsuspendUser
```

**Files**:
- `apps/web/src/components/admin/dashboard/user-management-block.tsx` ✅ Created
- `apps/web/src/lib/api/admin-client.ts` (needs real API)

**Dependencies**: Issues #8, #9, #10 (Backend user endpoints)

**Estimated**: 6-8 hours

---

### Issue #4: Detail Pages - Full Management Interfaces

**Title**: Create Detail Pages for Collection, Approvals, and Users

**Description**:
Create full-page management interfaces that blocks link to, with complete functionality for managing large datasets.

**Acceptance Criteria**:

**Page 1: Collection Overview** (`/admin/collection/overview`)
- [ ] Comprehensive stats dashboard
- [ ] Charts and trends (optional Phase 2)
- [ ] Quick filters and search
- [ ] Link to approval queue

**Page 2: Approvals Full List** (`/admin/shared-games/approvals`)
- [ ] Paginated table/grid with all pending games
- [ ] Advanced filters (date range, submitter, category)
- [ ] Bulk operations (select all, approve all, reject all)
- [ ] Game detail view modal
- [ ] Export to CSV (optional Phase 2)

**Page 3: Users Management** (`/admin/users/management`)
- [ ] Paginated table/grid with all users
- [ ] Advanced filters (registration date, activity level)
- [ ] Bulk operations (export, role change, suspend)
- [ ] User detail modal or page
- [ ] Activity timeline
- [ ] Badge management

**Technical Details**:
```typescript
// Pages:
//   - apps/web/src/app/admin/collection/overview/page.tsx
//   - apps/web/src/app/admin/shared-games/approvals/page.tsx
//   - apps/web/src/app/admin/users/management/page.tsx
// Uses: DataTable, MeepleCard, StatCard, Charts (optional)
```

**Files** (to create):
- `apps/web/src/app/admin/collection/overview/page.tsx`
- `apps/web/src/app/admin/shared-games/approvals/page.tsx`
- `apps/web/src/app/admin/users/management/page.tsx`

**Dependencies**: All backend issues (#6-10)

**Estimated**: 8-10 hours

---

### Issue #5: API Client Integration - Production Ready

**Title**: Integrate Real API Client and Remove Mock Data

**Description**:
Switch from mock client to real API client, add error handling, and prepare for production.

**Acceptance Criteria**:
- [ ] Replace `adminClientMock` with `adminClient` in all components
- [ ] Add comprehensive error handling
- [ ] Configure API base URL from environment variables
- [ ] Add request/response interceptors for auth
- [ ] Implement retry logic for failed requests
- [ ] Add request cancellation for search queries
- [ ] Toast notifications for API errors
- [ ] Fallback UI for network errors
- [ ] Loading states for all async operations

**Technical Details**:
```typescript
// File: apps/web/src/lib/api/admin-client.ts
// Environment: NEXT_PUBLIC_API_URL=http://localhost:8080/api/v1
// Error handling: Try/catch with user-friendly messages
// Auth: Include session token in headers
```

**Files**:
- `apps/web/src/lib/api/admin-client.ts` ✅ Created (needs real implementation)
- `apps/web/src/components/admin/dashboard/*.tsx` (update imports)

**Dependencies**: All backend issues (#6-10)

**Estimated**: 3-4 hours

---

## Backend Issues

### Issue #6: Admin Stats Endpoint - Dashboard Metrics

**Title**: Implement GET /admin/stats Endpoint for Dashboard Metrics

**Description**:
Create endpoint to aggregate platform statistics for the admin dashboard overview.

**Acceptance Criteria**:
- [ ] Endpoint: `GET /api/v1/admin/stats?days=30`
- [ ] Returns AdminStatsDto with 9 metrics
- [ ] Caching with HybridCache (5min TTL)
- [ ] Query filters by date range (default 30 days)
- [ ] Authorization: Admin role required
- [ ] Performance: < 500ms response time
- [ ] Unit tests with mocked repositories
- [ ] Integration tests with test database

**Technical Details**:
```csharp
// Query: GetAdminStatsQuery (days parameter)
// Handler: GetAdminStatsQueryHandler
// DTO: AdminStatsDto
// Bounded Context: Administration
// Caching: HybridCache with key "admin-stats-{days}"
```

**Response Schema**:
```json
{
  "totalGames": 1247,
  "publishedGames": 1156,
  "pendingGames": 23,
  "totalUsers": 8542,
  "activeUsers": 3891,
  "newUsers": 156,
  "approvalRate": 94,
  "pendingApprovals": 23,
  "recentSubmissions": 47
}
```

**Files** (to create/update):
- `BoundedContexts/Administration/Application/Queries/GetAdminStatsQuery.cs`
- `BoundedContexts/Administration/Application/Handlers/GetAdminStatsQueryHandler.cs` (may exist)
- `BoundedContexts/Administration/Application/DTOs/AdminStatsDto.cs`
- `Routing/AdminResourcesEndpoints.cs` (add endpoint mapping)

**Dependencies**: None

**Estimated**: 3-4 hours

---

### Issue #7: Approval Queue Endpoint - Pending Games with Metadata

**Title**: Implement Approval Queue Endpoint with Filtering and Pagination

**Description**:
Create endpoint to retrieve pending shared games with submission metadata for admin review.

**Acceptance Criteria**:
- [ ] Endpoint: `GET /api/v1/admin/shared-games/approval-queue`
- [ ] Pagination support (page, pageSize)
- [ ] Filters: status (pending, urgent), search (game title)
- [ ] Returns ApprovalQueueItemDto with game + metadata
- [ ] Sorted by days pending (oldest first)
- [ ] Authorization: Admin role required
- [ ] Performance: < 800ms response time
- [ ] Unit + integration tests

**Technical Details**:
```csharp
// Query: GetApprovalQueueQuery
// Handler: GetApprovalQueueQueryHandler (may exist)
// DTO: ApprovalQueueItemDto
// Bounded Context: SharedGameCatalog
// Filters: Status, SearchTerm
```

**Response Schema**:
```json
{
  "items": [
    {
      "gameId": "guid",
      "title": "string",
      "submittedBy": "string",
      "submittedAt": "datetime",
      "daysPending": 5,
      "pdfCount": 3
    }
  ],
  "totalCount": 23,
  "page": 1,
  "pageSize": 10,
  "totalPages": 3
}
```

**Files** (check if exist, else create):
- `BoundedContexts/SharedGameCatalog/Application/Queries/GetApprovalQueueQuery.cs` (may exist)
- `BoundedContexts/SharedGameCatalog/Application/Handlers/GetApprovalQueueQueryHandler.cs` (may exist)
- `BoundedContexts/SharedGameCatalog/Application/DTOs/ApprovalQueueItemDto.cs`
- `Routing/SharedGameCatalogEndpoints.cs` (endpoint may exist)

**Dependencies**: None

**Estimated**: 2-3 hours

---

### Issue #8: User Management Endpoints - CRUD + Actions

**Title**: Implement User Management Endpoints for Dashboard

**Description**:
Create or verify user management endpoints for listing, filtering, and performing actions on users.

**Acceptance Criteria**:
- [ ] `GET /api/v1/admin/users` - List with pagination
- [ ] `GET /api/v1/admin/users/{id}` - User detail
- [ ] `POST /api/v1/admin/users/{id}/suspend` - Suspend account
- [ ] `POST /api/v1/admin/users/{id}/unsuspend` - Unsuspend account
- [ ] Pagination and filtering support
- [ ] Authorization: Admin role required
- [ ] Validation for all inputs
- [ ] Unit + integration tests

**Technical Details**:
```csharp
// Queries: SearchUsersQuery, GetUserByIdQuery (may exist)
// Commands: SuspendUserCommand, UnsuspendUserCommand
// Handlers: Respective query/command handlers
// Bounded Context: Administration
```

**Files** (check if exist):
- `BoundedContexts/Administration/Application/Queries/SearchUsersQuery.cs` (may exist)
- `BoundedContexts/Administration/Application/Commands/SuspendUserCommand.cs`
- `BoundedContexts/Administration/Application/Commands/UnsuspendUserCommand.cs`
- `Routing/AdminUserEndpoints.cs` (endpoints may exist)

**Dependencies**: None

**Estimated**: 4-5 hours

---

### Issue #9: User Library Stats Endpoint

**Title**: Implement User Library Statistics Endpoint

**Description**:
Create endpoint to retrieve user's library statistics for the admin detail panel.

**Acceptance Criteria**:
- [ ] Endpoint: `GET /api/v1/admin/users/{userId}/library/stats`
- [ ] Returns UserLibraryStatsDto
- [ ] Calculates: games owned, total plays, wishlist count, average rating
- [ ] Authorization: Admin role required
- [ ] Performance: < 500ms response time
- [ ] Caching with HybridCache (1min TTL)
- [ ] Unit + integration tests

**Technical Details**:
```csharp
// Query: GetUserLibraryStatsQuery
// Handler: GetUserLibraryStatsQueryHandler (may exist)
// DTO: UserLibraryStatsDto
// Bounded Context: Administration or UserLibrary
```

**Response Schema**:
```json
{
  "gamesOwned": 47,
  "totalPlays": 234,
  "wishlistCount": 12,
  "averageRating": 8.4,
  "favoriteCategory": "Strategy"
}
```

**Files** (check if exist):
- `BoundedContexts/Administration/Application/Queries/GetUserLibraryStatsQuery.cs`
- `BoundedContexts/Administration/Application/Handlers/GetUserLibraryStatsQueryHandler.cs` (may exist)
- `BoundedContexts/Administration/Application/DTOs/UserLibraryStatsDto.cs`

**Dependencies**: UserLibrary bounded context

**Estimated**: 3-4 hours

---

### Issue #10: User Badges Endpoint

**Title**: Implement User Achievement Badges Endpoint

**Description**:
Create endpoint to retrieve user's earned achievement badges for the admin detail panel.

**Acceptance Criteria**:
- [ ] Endpoint: `GET /api/v1/admin/users/{userId}/badges`
- [ ] Returns array of UserBadgeDto
- [ ] Includes badge metadata (name, description, icon, earned date)
- [ ] Sorted by earned date (newest first)
- [ ] Authorization: Admin role required
- [ ] Performance: < 300ms response time
- [ ] Unit + integration tests

**Technical Details**:
```csharp
// Query: GetUserBadgesQuery
// Handler: GetUserBadgesQueryHandler (may exist)
// DTO: UserBadgeDto
// Bounded Context: SharedGameCatalog (gamification features)
```

**Response Schema**:
```json
[
  {
    "id": "guid",
    "name": "Early Adopter",
    "description": "Joined in the first year",
    "icon": "🎖️",
    "earnedAt": "2024-01-20T00:00:00Z"
  }
]
```

**Files** (check if exist):
- `BoundedContexts/SharedGameCatalog/Application/Queries/GetUserBadges/GetUserBadgesQuery.cs` (may exist)
- `BoundedContexts/SharedGameCatalog/Application/Queries/GetUserBadges/GetUserBadgesQueryHandler.cs` (may exist)

**Dependencies**: Badge system (if exists)

**Estimated**: 2-3 hours

---

## Testing Issues

### Issue #11: Frontend Tests - Dashboard Components

**Title**: Write Unit Tests for Dashboard Components

**Description**:
Create comprehensive unit tests for all dashboard components using Vitest and React Testing Library.

**Acceptance Criteria**:
- [ ] StatsOverview component tests (loading, data display, links)
- [ ] SharedGamesBlock tests (grid/list toggle, search, filters, actions)
- [ ] UserManagementBlock tests (grid/list, filters, detail panel)
- [ ] DashboardShell tests (layout, background)
- [ ] Mock React Query for consistent testing
- [ ] Test user interactions (clicks, typing, selections)
- [ ] Test error states and loading states
- [ ] Coverage: 85%+ for dashboard components

**Technical Details**:
```typescript
// Test files:
//   - apps/web/src/components/admin/dashboard/__tests__/stats-overview.test.tsx
//   - apps/web/src/components/admin/dashboard/__tests__/shared-games-block.test.tsx
//   - apps/web/src/components/admin/dashboard/__tests__/user-management-block.test.tsx
// Uses: Vitest, React Testing Library, MSW for API mocking
```

**Dependencies**: Issues #1, #2, #3

**Estimated**: 4-5 hours

---

### Issue #12: E2E Tests - Admin Workflows

**Title**: Write E2E Tests for Critical Admin Workflows

**Description**:
Create end-to-end tests using Playwright for critical admin workflows.

**Acceptance Criteria**:
- [ ] Workflow 1: Approve game from queue
- [ ] Workflow 2: Reject game from queue
- [ ] Workflow 3: Bulk approve multiple games
- [ ] Workflow 4: Suspend user account
- [ ] Workflow 5: View user detail panel
- [ ] Workflow 6: Search and filter games
- [ ] Workflow 7: Search and filter users
- [ ] Workflow 8: Toggle grid/list views
- [ ] Tests run in CI/CD pipeline
- [ ] Cross-browser testing (Chrome, Firefox)

**Technical Details**:
```typescript
// Test file: apps/web/tests/e2e/admin/dashboard.spec.ts
// Uses: Playwright
// Fixtures: Seed test database with games and users
```

**Dependencies**: Issues #1-5, #6-10 (all functionality must be complete)

**Estimated**: 6-8 hours

---

### Issue #13: API Tests - Endpoint Integration Tests

**Title**: Write Integration Tests for Admin API Endpoints

**Description**:
Create integration tests for all admin dashboard API endpoints using xUnit and Testcontainers.

**Acceptance Criteria**:
- [ ] Test GET /admin/stats (with date filters)
- [ ] Test GET /admin/shared-games/approval-queue (pagination, filters)
- [ ] Test POST /admin/shared-games/batch-approve
- [ ] Test POST /admin/shared-games/batch-reject
- [ ] Test GET /admin/users (pagination, filters)
- [ ] Test GET /admin/users/{id}
- [ ] Test GET /admin/users/{userId}/library/stats
- [ ] Test GET /admin/users/{userId}/badges
- [ ] Test POST /admin/users/{userId}/suspend
- [ ] Test POST /admin/users/{userId}/unsuspend
- [ ] Authorization tests (403 for non-admin)
- [ ] Validation tests (400 for invalid input)
- [ ] Coverage: 90%+ for endpoints

**Technical Details**:
```csharp
// Test files:
//   - tests/Api.Tests/BoundedContexts/Administration/GetAdminStatsQueryHandlerTests.cs
//   - tests/Api.Tests/BoundedContexts/SharedGameCatalog/GetApprovalQueueQueryHandlerTests.cs
//   - tests/Api.Tests/Integration/AdminDashboardEndpointsTests.cs
// Uses: xUnit, Testcontainers, FluentAssertions
```

**Dependencies**: Issues #6-10 (all endpoints must exist)

**Estimated**: 6-8 hours

---

## Summary

| Issue | Title | Type | Est. Hours | Dependencies |
|-------|-------|------|------------|--------------|
| #1 | Collection Overview Block | Frontend | 2-3 | #6 |
| #2 | Approval Queue Block | Frontend | 4-5 | #7 |
| #3 | User Management Block | Frontend | 6-8 | #8, #9, #10 |
| #4 | Detail Pages | Frontend | 8-10 | #6-10 |
| #5 | API Client Integration | Frontend | 3-4 | #6-10 |
| #6 | Admin Stats Endpoint | Backend | 3-4 | None |
| #7 | Approval Queue Endpoint | Backend | 2-3 | None |
| #8 | User Management Endpoints | Backend | 4-5 | None |
| #9 | User Library Stats Endpoint | Backend | 3-4 | None |
| #10 | User Badges Endpoint | Backend | 2-3 | None |
| #11 | Frontend Tests | Testing | 4-5 | #1-3 |
| #12 | E2E Tests | Testing | 6-8 | #1-10 |
| #13 | API Tests | Testing | 6-8 | #6-10 |

**Total Estimated**: 56-72 hours (7-9 days for 1 developer)

---

## Implementation Order

### Parallel Track 1 (Backend)
1. Issue #6 (Stats) → 2-3h
2. Issue #7 (Approval Queue) → 2-3h
3. Issues #8, #9, #10 (User endpoints) → 8-10h in parallel

### Parallel Track 2 (Frontend)
1. Issue #1 (Stats block) → 2-3h
2. Issue #2 (Approval block) → 4-5h
3. Issue #3 (User block) → 6-8h

### Sequential (After both tracks)
4. Issue #5 (API Integration) → 3-4h
5. Issue #4 (Detail pages) → 8-10h
6. Issues #11, #12, #13 (Testing) → 16-21h in parallel

**Optimized Timeline**: 3-4 days with parallel development

---

## Risk Assessment

**Medium Risks**:
- API endpoint implementations may reveal data model gaps
- Performance optimization may be needed for large datasets
- Existing endpoints may need modifications

**Mitigation**:
- Backend-first approach to validate data models
- Incremental testing with small datasets first
- API documentation review before implementation

---

**Ready for /sc:pm orchestration!** 🚀
