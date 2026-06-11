# MSW gap analysis — sp7-game-night-live

**Hero component**: `NightLiveHub` (presentational primitive — no internal fetches)
**Page-client wiring**: `NightLiveClientView` uses local fixture data per source comment:
> Continuità con mockup K/L mergiati nel PR #1250 — issue #487
> Fixture data (TODO: replace with backend hook `useGameNightLive(id)`)

## API endpoints consumed

| Endpoint | Method | Consumer | Existing handler? | Notes |
|----------|--------|----------|-------------------|-------|
| `/api/v1/auth/me` | GET | shared (any Storybook decorator) | shared global handler | Marco host |
| `/api/v1/game-nights/:id/live` | GET | NOT yet wired (`useGameNightLive` is TODO) | **gap** — fixture stubs default for future integration | server-side hub state |
| `/api/v1/game-nights/:id/pause` | POST | mutation TBD | **gap** — fixture echoes paused | |
| `/api/v1/game-nights/:id/resume` | POST | mutation TBD | **gap** — fixture echoes live | |
| `/api/v1/game-nights/:id/transition` | POST | mutation TBD | **gap** — fixture echoes transition | post-MVP |
| `/api/v1/game-nights/:id/end` | POST | mutation TBD | **gap** — fixture echoes completed | |
| `/api/v1/game-nights/:id/diary` | POST | Diary event capture | **gap** — TODO | post-MVP, auto-save toast trigger Frame 10 |

## Component is presentational

NightLiveHub takes all data as **props** — no internal hooks. Frame 1-10 stories drive variants via prop manipulation (in args + parameters.msw for the surrounding page-client wiring).

The MSW handlers exist primarily for:
1. **Future integration** when `useGameNightLive(id)` hook lands.
2. **Auth context**: `useCurrentUser` for global decorator.

## BGG hygiene check

Cover URLs use inline HSL gradients (e.g. `['hsl(220 35% 28%)', 'hsl(28 60% 38%)']`) — no remote image hosts. ZERO BGG references.

## Handler structure (recommended)

Use `mswForSp7LiveState(state)` switcher. States: `'live' | 'paused' | 'transition' | 'loading' | 'error'`.

## Follow-ups

- **Auto-save toast (Frame 10)**: client-side timer state, not API-driven. Handled by NightLiveHub internal `<AutoSaveToast>` component (line 14 import). Story drives via `autoSaveToast` prop.
- **DiaryInlineWidget (Frame 06)**: dedicated component, separate story expected.
- **GameTransitionDialog (Frame 03)**: modal sibling primitive in `transition/` folder, separate story.
- **`useGameNightLive` BE hook**: TODO comment in NightLiveClientView line 26 — when this lands, replace fixture wiring with real SWR/react-query.
