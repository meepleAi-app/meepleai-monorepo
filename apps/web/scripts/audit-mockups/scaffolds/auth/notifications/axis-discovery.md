# notifications — Axis Discovery

**Source HTML**: `admin-mockups/design_files/notifications.html`
**JSX twin**: `admin-mockups/design_files/notifications.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup HTML (notifications.html:107-113) advertises "5 screen
interattivi: feed raggruppato, detail drawer, empty state, filtri per entity
e quick-link impostazioni". The JSX twin renders a `.phones-grid`
(notifications.jsx) with PhoneShell wrappers — one per screen.

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `screen` | enum | `feed` \| `detail-drawer` \| `empty` \| `filtered` \| `preferences` | Mockup HTML lead text (line 102), PhoneShell config in jsx | One frame per stage screen |
| `filter` | enum | `all` \| `sessions` \| `agents` \| `events` \| `system` | `FILTERS` registry (jsx:53-57, page.tsx:59-104) | Pill bar; `aria-pressed` per active |
| `state` | enum | `default` \| `empty` \| `loading` \| `error` | `useNotificationStore` selectors (page.tsx:152-158) | Drives MSW handler scenario |

The page itself owns:
- `activeTab` (`'all' | 'unread'`) — legacy Tutte/Non lette tabs (page.tsx:159)
- `filter` (FilterKey, page.tsx:160) — entity-colored pill bar
- `currentPage` (pagination, page.tsx:161)
- `detail` (NotificationDto | null) — opens drawer on click (page.tsx:162)

All state is internal; Storybook `argTypes` are documentation only.

## Frame matrix (Desktop only Phase C-1)

| Frame | Mockup screen label | Canonical content | Axis values |
|-------|---------------------|-------------------|-------------|
| 01 | Feed | Feed default (all filter, all groups visible) | `screen='feed', state='default'` |
| 02 | Detail drawer | Drawer open with notif #1 (session) | `screen='detail-drawer', state='default'` |
| 03 | Empty | No notifications | `screen='empty', state='empty'` |
| 04 | Filtered | Filter "Agenti" selected → subset items | `screen='filtered', filter='agents'` |
| 05 | Preferences quick-link | Mockup-only inline CTA | `screen='preferences'` (divergence) |

## Component mapping (route ↔ canonical)

| Route | Real Client component | File |
|-------|-----------------------|------|
| `/notifications` | `NotificationsPage` (default export) | `apps/web/src/app/(authenticated)/notifications/page.tsx` |
| `/notifications/preferences` | `NotificationsPreferencesPage` | `apps/web/src/app/(authenticated)/notifications/preferences/page.tsx` (likely) |

## Canonical component pick

**Picked**: `apps/web/src/app/(authenticated)/notifications/page.tsx` (default export `NotificationsPage`)

**Why**:
1. Production component, complete feed + filters + drawer + pagination.
2. Already has `@mockup admin-mockups/design_files/notifications.html`
   JSDoc annotation (DS-17-1 #2069, lines 1-9).
3. Uses `useNotificationStore` Zustand store — Storybook just needs MSW
   handlers for `GET /api/v1/notifications` to populate the store.
4. Already wired with Drawer (`@/components/ui/drawer`, asse-B
   primitive) — supports Frame02 demo.

## Mockup ↔ codebase divergences

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | Mockup `FILTERS` (jsx:53-57) uses 5 keys: `all`/`sessions`/`agents`/`events`/`system`. Codebase identical (page.tsx:59). | None — matched exactly. |
| 2 | Mockup `system` filter `entities: ['game','chat','toolkit','kb']` (jsx:57). Codebase `system` filter `types: [...12 system NotificationType values]` (page.tsx:87-103). | Different abstraction (entity vs type). Codebase has finer granularity. Acceptable. |
| 3 | Mockup uses `entity` (game/session/agent/event/kb/player/chat/toolkit). Codebase uses `EntityType` (page.tsx:41 + mapTypeToEntity, line 110-118). | Codebase abstracts; mockup hardcodes per item. Mapping helper. |
| 4 | Mockup shows quick-link to /settings inline in feed. Codebase has no such link. | Frame 05 documents divergence; designer review whether to add. |
| 5 | Mockup uses `formatDistanceToNow` similar pattern. Codebase imports from date-fns with `locale: it` (page.tsx:148). | Implementation parity. |

## JSX evidence (line refs)

- `NOTIFS` sample array (8 entries): `notifications.jsx:11-42`
- `GROUPS` day grouping definitions: `notifications.jsx:46-49`
- `FILTERS` entity-colored pill registry: `notifications.jsx:53-57`
- `role="tablist"` filter bar: `notifications.jsx:360`
- `role="feed"` feed container: `notifications.jsx:494`
- Codebase `FILTERS` (page.tsx:59-104) + mapTypeToEntity (page.tsx:110-118)
- Codebase Drawer wiring (page.tsx:372-405)
