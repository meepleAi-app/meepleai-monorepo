# Epic: User Dashboard - Personal Gaming Hub

## Overview
Implement user dashboard as personal hub after login with personalized greeting, library quota, active sessions, recent games, chat history, and quick actions.

## Business Value
- **User Engagement**: Central hub for all user activities post-login
- **Data Visibility**: Quick overview of library status, sessions, recent activity
- **Discovery**: Surface recent games from catalog and personal library
- **Quick Actions**: Streamlined access to primary user flows (add game, start chat)

## Design Mockup
`docs/design-proposals/meepleai-style/user-dashboard.html`

**Design Features**:
- Personalized greeting (time-based: Buongiorno/Pomeriggio/Sera)
- Library quota widget with animated progress bar
- Active sessions panel (ongoing games)
- Recently added to library (6 games, 2x3 grid)
- Recent games from catalog (trending/popular)
- Chat history (5 most recent threads)
- Quick actions (Add Game, New Chat)
- Responsive: Top nav (desktop) + Bottom nav (mobile)

## Technical Architecture

### Backend (CQRS + DDD)
```
BoundedContexts/UserLibrary/
└── Application/
    ├── Queries/
    │   ├── GetUserDashboardQuery.cs
    │   ├── GetLibraryQuotaQuery.cs
    │   └── GetActiveSessionsQuery.cs
    └── DTOs/
        ├── UserDashboardDto.cs
        ├── LibraryQuotaDto.cs
        └── ActiveSessionDto.cs
```

### Frontend (Next.js 14)
```
apps/web/src/
├── app/
│   └── (public)/
│       └── dashboard/
│           ├── page.tsx
│           └── components/
│               ├── GreetingSection.tsx (existing)
│               ├── LibraryQuotaWidget.tsx
│               ├── ActiveSessionsPanel.tsx
│               ├── RecentlyAddedSection.tsx (existing)
│               ├── RecentGamesSection.tsx (existing)
│               ├── ChatHistorySection.tsx (existing)
│               └── QuickActionsPanel.tsx (existing)
└── lib/
    └── api/
        └── dashboard-api.ts
```

## Success Criteria
- [ ] GetUserDashboardQuery returns all dashboard data in single query
- [ ] Library quota displays with color-coded progress (green/yellow/red)
- [ ] Active sessions show with "Continue" CTA
- [ ] Recently added games (6 cards) from user library
- [ ] Recent catalog games (6 cards) with "Add to Library" CTA
- [ ] Chat history (5 threads) with continue chat action
- [ ] Quick actions navigate to correct pages
- [ ] Responsive: Top nav (desktop) + Bottom nav (mobile)
- [ ] Skeleton loading states during data fetch
- [ ] TanStack Query caching (5 min stale time)

## Implementation Issues

### Backend (3 issues)
1. **GetUserDashboardQuery** - Aggregate query for all dashboard data
2. **GetLibraryQuotaQuery** - User library usage vs limits
3. **GetActiveSessionsQuery** - Currently playing games

### Frontend (4 issues)
4. **LibraryQuotaWidget** - Animated progress bar with color coding
5. **ActiveSessionsPanel** - Display ongoing games with Continue CTA
6. **Dashboard API Integration** - TanStack Query hooks for all data
7. **Responsive Navigation** - Top nav (desktop) + Bottom nav (mobile)

### Testing (2 issues)
8. **Dashboard Component Tests** - All widgets isolated
9. **Dashboard E2E Tests** - Full user journey (login → dashboard → actions)

**Total**: 9 issues

## Dependencies
- User Authentication system
- Game catalog API
- AI chat system (for history)
- Session tracking system

## Timeline Estimate
**Duration**: 2-3 weeks (1 developer)
- Week 1: Backend queries + DTOs
- Week 2: Frontend components + integration
- Week 3: Testing + polish

## Priority
**HIGH** - Central user experience, high-traffic page

## Labels
`epic`, `user-dashboard`, `frontend`, `backend`, `testing`

---

**Status**: Ready for Issue Creation
**Mockup**: ✅ Complete
**Design System**: ✅ MeepleAI style
