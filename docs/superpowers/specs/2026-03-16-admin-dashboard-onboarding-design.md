# Admin Dashboard Redesign & Onboarding Flow

**Date**: 2026-03-16
**Status**: Approved
**Scope**: Frontend only — overview page rework + onboarding page rewrite

## Overview

Redesign the admin overview page (`/admin/overview`) from a technical-focused dashboard to an **operational command center** for daily admin tasks. Additionally, rewrite the onboarding page (`/onboarding`) as a minimal single-screen profile setup.

## Goals

1. Admin sees pending access requests immediately on login
2. Dashboard summarizes shared library and users at a glance
3. Quick actions prioritize daily operations (create game, invite user) over technical ops
4. New users complete a frictionless single-screen onboarding after invite acceptance

## Non-Goals

- No changes to navigation structure (UnifiedShell, AdminTabSidebar stay as-is)
- No changes to invite acceptance flow (`/join/[token]`, `/accept-invite`)
- No changes to middleware or auth provider
- No wizard or multi-step onboarding

## Known Backend Gaps

- `AdminOverviewStats` schema lacks `totalDocuments` and `queueDepth` fields — the Documenti KPI card requires extending `getOverviewStats()` or fetching from a separate KB endpoint
- No `getRecentGames` endpoint exists — use `api.sharedGames.getAll({ pageSize: 5, page: 1 })` as fallback (default sort order), or add a backend sort param later

---

## Part 1: Admin Overview Page

### Route

`/admin/overview` (existing, rewrite in place)

### Layout (vertical sections, top to bottom)

```
┌─────────────────────────────────────────────────┐
│  KPIStatsRow                                     │
│  [Giochi] [Documenti] [Utenti] [Pendenti*]      │
├─────────────────────────────────────────────────┤
│  PendingRequestsBanner (conditional, only if >0) │
│  Email list with inline Approve/Reject           │
├──────────────────────┬──────────────────────────┤
│  LibrarySummaryCard  │  UsersSummaryCard         │
│                      │                           │
├──────────────────────┴──────────────────────────┤
│  QuickActionsGrid (6 operative cards, 3x2)       │
├─────────────────────────────────────────────────┤
│  TechActionsBar (always visible, muted style)    │
└─────────────────────────────────────────────────┘
```

### File Structure

All new components live in `apps/web/src/app/admin/(dashboard)/overview/`:

```
overview/
├── page.tsx                    # Server component, data fetching
├── loading.tsx                 # New — skeleton UI while data loads
├── KPIStatsRow.tsx             # New — compact stat cards row
├── PendingRequestsBanner.tsx   # New — conditional pending requests
├── LibrarySummaryCard.tsx      # New — shared library summary
├── UsersSummaryCard.tsx        # New — users summary
├── QuickActionsGrid.tsx        # Refactor — 6 operative actions
└── TechActionsBar.tsx          # New — muted technical actions
```

### Components

#### KPIStatsRow

Compact row of stat cards. Reuses existing `KPICard` component as-is (accepts its existing styles: `bg-orange-100 text-orange-600` icon box, `text-3xl` value, `bg-card/90` container). No style overrides — consistency with existing admin pages.

| Card | Icon | Value | Subtext | Condition |
|------|------|-------|---------|-----------|
| Giochi | Gamepad2 | count | "nel catalogo condiviso" | Always |
| Documenti | FileText | count | "processati" + badge "N in coda" if queue > 0 | Always (requires backend gap fix — see Known Backend Gaps) |
| Utenti | Users | count | "registrati" | Always |
| Pendenti | UserPlus | count | "richieste di accesso" | Only if count > 0 |

Responsive: 4 columns desktop (3 if no pending), 2 columns mobile.

#### PendingRequestsBanner

Conditional: renders only when `pendingAccessRequests.length > 0`.

- Style: `bg-amber-50 border-amber-200 rounded-2xl p-4`
- Header: AlertTriangle icon + "N richieste di accesso in attesa"
- Body: list of up to 5 emails, each with:
  - Email text
  - **Approva** button (green, solid)
  - **Rifiuta** button (red, outline)
- Footer (if > 5): "Vedi tutte →" link to `/admin/users/access-requests`
- Mutations: `useMutation` calling `api.accessRequests.approveAccessRequest(id)` and `rejectAccessRequest(id)` with optimistic UI or `invalidateQueries` on success

#### LibrarySummaryCard

Glassmorphic card (`bg-white/70 backdrop-blur-sm border-slate-200/60 rounded-2xl`).

- **Header**: Library icon + "Libreria Condivisa"
- **Stats row**: total games | total documents | configured agents (small inline numbers)
- **Mini list**: last 5 games added (title + date, clickable → `/admin/shared-games/[id]`)
- **Footer**: "Gestisci catalogo →" link to `/admin/shared-games/all`

Data source: `api.admin.getOverviewStats()` for game/user counts. Recent games list: `api.sharedGames.getAll({ pageSize: 5, page: 1 })` (default sort order — see Known Backend Gaps for future improvement). Documents count requires backend gap fix.

#### UsersSummaryCard

Same glassmorphic card style.

- **Header**: Users icon + "Utenti"
- **Stats row**: total users | active 30d (from `getOverviewStats().activeUsers`) | pending invitations (sent by admin, not yet accepted)
- **Mini list**: last 5 registered users (name or email + registration date)
- **Footer**: "Gestisci utenti →" link to `/admin/users`

"Pending invitations" = invitations sent by admin via `api.invitations.getInvitations({ status: 'Pending' })`, distinct from "access requests" (users requesting entry).

#### QuickActionsGrid

Grid of 6 operative action cards. 3 columns desktop, 2 columns mobile.

Each card: amber icon box (`bg-amber-100 text-amber-700 rounded-lg p-2`) + bold label + muted description.

| Action | Icon | Label | Description | Behavior |
|--------|------|-------|-------------|----------|
| Create Game | Plus | "Crea Gioco" | "Aggiungi al catalogo" | Navigate → `/admin/shared-games/new` |
| Invite User | UserPlus | "Invita Utente" | "Invia invito email" | Open `InviteUserDialog` inline |
| Manage Games | Gamepad2 | "Gestisci Giochi" | "Catalogo e filtri" | Navigate → `/admin/shared-games/all` |
| Manage Users | Users | "Gestisci Utenti" | "Lista e ruoli" | Navigate → `/admin/users` |
| Upload PDF | Upload | "Upload PDF" | "Carica regolamento" | Navigate → `/admin/knowledge-base/upload` |
| View Queue | ListOrdered | "Vedi Coda" | "Stato processing" | Navigate → `/admin/knowledge-base/queue` |

"Invita Utente" reuses the existing `InviteUserDialog` component (already built, handles single invite with email + role).

#### TechActionsBar

Always visible, muted style. Single row of flat text buttons.

- Style: `text-sm text-muted-foreground` with `hover:text-foreground hover:bg-slate-100 rounded-md px-3 py-1.5`
- Buttons separated by subtle dividers
- Actions: Clear Cache | Reindex All | System Health | Export Users
- Clear Cache and Reindex: API calls (existing placeholder logic in QuickActionsWidget)
- System Health: navigate → `/admin/monitor` (monitor root lets admin choose their preferred tab; InfrastructureTab exists at `?tab=infra` but root is a better landing point)
- Export Users: API call (existing placeholder)

### Data Fetching Strategy

The page is a server component. It fetches all data in parallel:

```typescript
// overview/page.tsx
const [stats, pendingRequests, recentGames, recentUsers, pendingInvitations] =
  await Promise.all([
    api.admin.getOverviewStats(),
    api.accessRequests.getAccessRequests({ status: 'Pending', pageSize: 5 }),
    api.sharedGames.getAll({ pageSize: 5, page: 1 }),  // default sort — no createdAt sort param available yet
    api.admin.getAllUsers({ limit: 5 }),
    api.invitations.getInvitations({ status: 'Pending', pageSize: 5 }),
  ]);
```

Client components (`PendingRequestsBanner` with mutations, `QuickActionsGrid` with dialog state) are marked `'use client'` individually.

### Responsive Behavior

| Breakpoint | KPI | Summary Cards | Quick Actions | Tech Actions |
|------------|-----|---------------|---------------|--------------|
| `lg+` | 4 cols (or 3) | 2 cols side-by-side | 3x2 grid | single row |
| `md` | 2 cols | stacked | 2x3 grid | single row, wraps |
| `sm` | 2 cols | stacked | 2x3 grid | stacked |

---

## Part 2: Onboarding Page

### Route

`/onboarding` (existing under `(authenticated)` group, rewrite in place)

### Flow Context

```
Admin approves access request
  → System sends email with /join/[token] link
  → User clicks → /join/[token]: sets password (EXISTING, no changes)
  → Redirect to /onboarding
  → /onboarding: profile form (THIS REWRITE)
  → Submit → redirect to / (user home)
```

No changes to: `/join/[token]`, `/accept-invite`, `middleware.ts`, `AuthProvider.tsx`.

### Layout

Centered vertically and horizontally, like other auth pages. Max width `sm` (480px).

```
┌──────────────────────────────┐
│  Logo MeepleAI               │
│                              │
│  "Benvenuto! Completa il     │
│   tuo profilo per iniziare"  │
│                              │
│  ┌────────────────────────┐  │
│  │  Avatar (optional)     │  │
│  │  click to upload       │  │
│  └────────────────────────┘  │
│                              │
│  Display Name *              │
│  [____________________]      │
│                              │
│  Email                       │
│  [mario@example.com  🔒]     │
│  (read-only, pre-filled)     │
│                              │
│  [      Entra →         ]    │
└──────────────────────────────┘
```

### File

`apps/web/src/app/(authenticated)/onboarding/page.tsx` — single file rewrite.

### Behavior

- **Email**: pre-filled from session (`useAuth()` hook), field is `disabled` with lock icon
- **Display Name**: required, 2-50 characters, auto-focused
- **Avatar**: circular preview area, click opens file picker, uploads via `api.auth.uploadAvatar(file)`, shows preview after upload
- **Submit**: calls `api.auth.updateProfile(displayName)` then `api.auth.completeOnboarding(false)` (explicit `skipped=false`), then `router.push('/')`
- **Already onboarded guard**: middleware already handles this via `onboarding_completed` cookie — if cookie is `true`, middleware redirects away from `/onboarding`. No additional client-side guard needed. As a safety net, the component checks `useAuth().user?.onboardingCompleted` and calls `router.replace('/')` if true (prevents flash if cookie is stale).
- **Loading states**: button shows spinner during submit, fields disabled

### Styling

- Card: `bg-white/70 backdrop-blur-md border border-slate-200/60 rounded-2xl p-8`
- Heading: `font-quicksand text-2xl font-bold`
- Body text: `font-nunito`
- Button: `bg-amber-500 hover:bg-amber-600 text-white rounded-xl w-full py-3`
- Avatar circle: `w-20 h-20 rounded-full bg-slate-100 border-2 border-dashed border-slate-300`

---

## Files Changed Summary

| File | Action | Notes |
|------|--------|-------|
| `apps/web/src/app/admin/(dashboard)/overview/page.tsx` | Rewrite | Server component, parallel data fetching |
| `apps/web/src/app/admin/(dashboard)/overview/loading.tsx` | Create | Skeleton UI while server component fetches (5 parallel API calls) |
| `apps/web/src/app/admin/(dashboard)/overview/KPIStatsRow.tsx` | Create | Compact stats row |
| `apps/web/src/app/admin/(dashboard)/overview/PendingRequestsBanner.tsx` | Create | Client component with mutations |
| `apps/web/src/app/admin/(dashboard)/overview/LibrarySummaryCard.tsx` | Create | Library summary card |
| `apps/web/src/app/admin/(dashboard)/overview/UsersSummaryCard.tsx` | Create | Users summary card |
| `apps/web/src/app/admin/(dashboard)/overview/QuickActionsGrid.tsx` | Rewrite | Replace 5 tech actions with 6 operative |
| `apps/web/src/app/admin/(dashboard)/overview/TechActionsBar.tsx` | Create | Muted tech actions row |
| `apps/web/src/app/admin/(dashboard)/overview/__tests__/page.test.tsx` | Rewrite | Update tests for new page structure |
| `apps/web/src/app/(authenticated)/onboarding/page.tsx` | Rewrite | Minimal single-screen profile form |

### Existing Components Reused (no modifications)

- `KPICard` — stat card pattern
- `InviteUserDialog` — invite user modal
- `UnifiedShell` + `AdminTabSidebar` — navigation
- `Badge` — status badges
- All API clients (`admin`, `accessRequests`, `invitations`, `auth`)

### Cleanup After Migration

- Verify existing `QuickActionsWidget.tsx` can be deleted after migration (no other pages import it)
- Remove old `QuickActionsPanel.tsx` if also unused

---

## Design Decisions

1. **Unified overview vs separate dashboard**: Rewrite existing `/admin/overview` rather than creating a new route — avoids duplication and nav config changes
2. **Pending requests on dashboard**: Shown inline (max 5) with direct approve/reject — admin doesn't need to navigate away for common case
3. **Two types of "pending"**: Access requests (users asking to join) shown in banner, pending invitations (admin sent, not accepted) shown as stat in UsersSummaryCard — clear distinction
4. **Quick actions**: Operative daily tasks prioritized, technical actions demoted to muted footer — reflects actual admin workflow
5. **Onboarding**: Single screen, no wizard — minimal friction for invited users who just want to start using the app
6. **Minimal backend changes**: Most data available from existing clients. Two gaps identified (document count, recent games sort) — documented in Known Backend Gaps section. Implementation can proceed with fallbacks, backend extensions are optional improvements.
