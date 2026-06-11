# MSW gap analysis — sp7-game-night-summary

**Hero component**: `NightSummaryView` (presentational primitive — no internal fetches)
**Page-client wiring**: `NightSummaryClientView` uses local fixture data per source comment:
> Continuità con mockup M mergiato nel PR #1250 — issue #487
> Fixture data (TODO: replace with backend hook `useGameNightSummary(id)`)

## API endpoints consumed

| Endpoint | Method | Consumer | Existing handler? | Notes |
|----------|--------|----------|-------------------|-------|
| `/api/v1/auth/me` | GET | shared (any Storybook decorator) | shared global handler | Marco host viewer |
| `/api/v1/game-nights/:id/summary` | GET | NOT yet wired (`useGameNightSummary` is TODO) | **gap** — fixture stubs default for future integration | recap payload (night + mvp + games + photos + eventsCount + archived) |
| `/api/v1/game-nights/:id/share` | POST | mutation TBD (Frame 04 toast trigger) | **gap** — fixture echoes share URL + token | |
| `/api/v1/game-nights/:id/archive` | POST | mutation TBD (Frame 05 trigger) | **gap** — fixture echoes archived=true | |
| `/api/v1/game-nights/:id/unarchive` | POST | mutation TBD (Frame 05 inverse) | **gap** — fixture echoes archived=false | |
| `/api/v1/game-nights/:id/photos` | POST | photo upload (Frame 02 placeholder CTA) | **gap** — fixture echoes new photo ID + URL | post-MVP |

## Component is presentational

NightSummaryView takes all data as **props** — no internal hooks. Frame 1-6 stories drive variants via prop manipulation (in args).

The MSW handlers exist primarily for:
1. **Future integration** when `useGameNightSummary(id)` hook lands.
2. **Mutation wiring**: share/archive/unarchive/photo-upload buttons.
3. **Auth context**: `useCurrentUser` for global decorator.

## BGG hygiene check

Cover URLs use inline HSL gradients (e.g. `['hsl(220 35% 28%)', 'hsl(28 60% 38%)']`) — no remote image hosts. Photo gradients also inline HSL. ZERO BGG references.

## Handler structure (recommended)

Use `mswForSp7SummaryState(state)` switcher. States: `'default' | 'loading' | 'error'` (presentational component — most variants driven by props, not API).

## Follow-ups

- **`useGameNightSummary` BE hook**: TODO comment in NightSummaryClientView line 15 — when this lands, replace fixture wiring with real SWR/react-query and wire the handlers above as real fetches.
- **Photo upload (Frame 02 CTA)**: file upload endpoint `/photos` POST — multipart form data; MVP could be deferred per ADR-059 cover handling pattern (R2 + signed URL).
- **Share toast UX**: `ShareSuccessToast` primitive in `@/components/ui/share-success-toast` — verify its story exists.
- **ArchivedBanner**: primitive in `@/components/ui/archived-banner` — verify its story exists.
- **KPIStatGrid**: primitive in `@/components/ui/kpi-stat-grid` — verify its story exists (also used by SP6 dashboard).
