# Redirect Map — User Route Consolidation

**Issue**: #5039 — Consolidate User Routes
**Epic**: #5033 — Layout & Navigation System Redesign
**Status**: Active (implemented in `next.config.js`)

## Overview

Issue #5039 consolidates ~200 legacy user routes into ~10 canonical URL hubs
using query-param tabs, powered by the MiniNav component (#5037).

All old URLs remain working via 301 permanent redirects configured in
`apps/web/next.config.js`.

---

## Library Routes

| Old URL | New Canonical URL | Notes |
|---------|-------------------|-------|
| `/library` | `/library` | Unchanged — main collection tab |
| `/library/wishlist` | `/library?tab=wishlist` | Query-param tab |
| `/library/private` | `/library?tab=private` | Query-param tab |
| `/library/proposals` | `/discover?tab=proposals` | Moved to community section |
| `/library/propose` | `/discover/propose` | Moved to community section |
| `/library/games/:id` | `/library/:id` | Flatten URL — remove `/games/` segment |
| `/library/games/:id/agent` | `/library/:id?tab=agent` | Sub-page → tab |
| `/library/games/:id/toolkit` | `/library/:id?tab=toolkit` | Sub-page → tab |
| `/library/games/:id/faqs` | `/library/:id?tab=faq` | Sub-page → tab |
| `/library/games/:id/reviews` | `/library/:id?tab=reviews` | Sub-page → tab |
| `/library/games/:id/rules` | `/library/:id?tab=rules` | Sub-page → tab |
| `/library/games/:id/sessions` | `/library/:id?tab=sessions` | Sub-page → tab |
| `/library/games/:id/strategies` | `/library/:id?tab=strategies` | Sub-page → tab |

---

## Profile & Settings Routes

| Old URL | New Canonical URL | Notes |
|---------|-------------------|-------|
| `/profile` | `/profile` | Now the canonical profile page (was redirecting to /settings) |
| `/profile/achievements` | `/profile?tab=achievements` | Query-param tab |
| `/badges` | `/profile?tab=badges` | Moved under profile |
| `/settings` | `/profile?tab=settings` | Merged into profile hub |
| `/settings/notifications` | `/profile?tab=settings&section=notifications` | Double query param |
| `/settings/security` | `/profile?tab=settings&section=security` | Double query param |

**Note**: The legacy `/profile` → `/settings` redirect (Issue #1672) has been
**removed**. `/profile` is now the canonical profile page, and `/settings`
redirects to `/profile?tab=settings` instead.

---

## Community / Discover Routes

| Old URL | New Canonical URL | Notes |
|---------|-------------------|-------|
| `/games/catalog` | `/discover` | New community catalog hub |
| `/games/:id` | `/discover/:id` | Community game detail |
| `/games/:id/faqs` | `/discover/:id?tab=faq` | Sub-page → tab |
| `/games/:id/reviews` | `/discover/:id?tab=reviews` | Sub-page → tab |
| `/games/:id/rules` | `/discover/:id?tab=rules` | Sub-page → tab |
| `/games/:id/sessions` | `/discover/:id?tab=sessions` | Sub-page → tab |
| `/games/:id/strategies` | `/discover/:id?tab=strategies` | Sub-page → tab |

---

## Agents Routes

| Old URL | New Canonical URL | Notes |
|---------|-------------------|-------|
| `/agents` | `/agents` | Unchanged |
| `/agent/slots` | `/agents?tab=slots` | Merged into agents hub |

---

## Sessions & Play Records Routes

| Old URL | New Canonical URL | Notes |
|---------|-------------------|-------|
| `/sessions` | `/sessions` | Unchanged |
| `/sessions/history` | `/sessions?tab=history` | Query-param tab |
| `/play-records` | `/play-records` | Unchanged |
| `/play-records/stats` | `/play-records?tab=stats` | Query-param tab |

---

## Unchanged Routes

These routes are **not** being consolidated in #5039:

- `/notifications` — remains as dedicated notification centre
- `/chat`, `/chat/new`, `/chat/[threadId]` — chat hub (future consolidation)
- `/sessions/[id]` — session detail (direct access)
- `/sessions/join/[token]` — join flow
- `/play-records/new`, `/play-records/[id]` — record creation / detail
- `/agents/[id]` — agent detail
- `/dashboard` — personal dashboard (separate from `/` landing)
- `/admin/*` — admin routes (handled in Issue #5040)

---

## Implementation Files

| File | Change |
|------|--------|
| `apps/web/next.config.js` | Added 301 redirect rules |
| `apps/web/src/lib/navigation/index.ts` | `getNavigationLinks()` — canonical link helpers |
| `apps/web/src/app/(authenticated)/discover/page.tsx` | New community catalog page |
| `apps/web/src/app/(authenticated)/discover/[gameId]/page.tsx` | New community game detail page |
| `apps/web/src/app/(authenticated)/library/[gameId]/page.tsx` | New library game detail (re-export) |
| `apps/web/src/app/(authenticated)/profile/page.tsx` | Updated — tab-aware stub |
| `apps/web/src/config/library-navigation.ts` | Updated hrefs to query params |
| `apps/web/src/config/navigation.ts` | Catalog: `/games` → `/discover` |

---

## Usage

```ts
// Use canonical links — never hardcode paths
import { getNavigationLinks } from '@/lib/navigation';

const links = getNavigationLinks();

// Navigate to wishlist
router.push(links.libraryWishlist); // '/library?tab=wishlist'

// Navigate to game detail
router.push(links.libraryGame('abc123')); // '/library/abc123'

// Navigate to agent tab
router.push(links.libraryGameTab('abc123', 'agent')); // '/library/abc123?tab=agent'
```
