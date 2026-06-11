# MSW gap analysis — sp7-game-night-detail-rsvp

**Hero component**: `GameNightDetailView` (page-client)
**Wiring**: `useGameNightDetail(id, viewer?.id)` + `useSharedGames` + mutations.

## API endpoints consumed

Cross-referenced with `GameNightDetailView.tsx` imports + `hooks/queries/useGameNightDetail.ts` + `hooks/queries/useGameNights.ts`.

| Endpoint | Method | Consumer | Existing handler? | Per-state variants |
|----------|--------|----------|-------------------|--------------------|
| `/api/v1/auth/me` | GET | `useCurrentUser` | shared global handler | viewer toggle: host (Marco) vs invitee (Davide) |
| `/api/v1/game-nights/:id` | GET | `useGameNightDetail` | **gap** — fixture stubs per state | status (Published / Cancelled / Completed) + rsvps array |
| `/api/v1/shared-games` | GET | `useSharedGames` (only when `isDraft`) | **gap** — fixture stubs default | always returns same 3 games |
| `/api/v1/game-nights/:id/rsvp` | POST | RSVP mutation | **gap** — fixture stubs success | echoes Accepted |
| `/api/v1/game-nights/:id/cancel` | POST | `useCancelGameNight` | **gap** — fixture stubs success | echoes Cancelled |
| `/api/v1/game-nights/:id/publish` | POST | `usePublishGameNight` | **gap** — fixture stubs success | echoes Published |

## State machine wiring

The page-client uses `RsvpResponse` type from `lib/game-nights/rsvp-state-machine.ts`:
- `RsvpResponse = Exclude<RsvpStatus, 'Pending'>` → `'Accepted' | 'Declined'`
- Transition rules mirror BE in `GameNightInvitation.cs`: same response = no-op, switching Accepted⇄Declined = 409, terminal Expired/Cancelled = 410.

Fixture covers happy-path; error-state coverage (409 conflict on switch, 410 gone on terminal, 429 rate-limit) DEFERRED to PublicRsvpForm.stories.tsx (which already handles these per its error-states/ folder structure).

## BGG hygiene check

ZERO references to BGG hosts. Game cover URLs are `thumbnailUrl: null` in fixture (resolver chain falls back to deterministic placeholder via `cover-utils.ts` per ADR-059).

## Handler structure (recommended)

Use `mswForSp7DetailRsvpState(state)` switcher pattern. States cover the 9 happy-path frames (1-4, 7-9 + 11) plus 2 transverse loading/error and 3 documentation-only (voting + chat — frame 5, 6, 10, 12 still get a handler that returns Published+host-pending baseline; the FE just won't render the tab itself).

## Follow-ups

- **Voting/Chat tabs**: post-MVP; not exposed by current page-client. Frame 05, 06, 10, 12 stories ship with `docs.description.story` flagging documentation-only.
- **RSVP error states** (409/410/429): already covered by error-states/ sibling components (`InvalidTokenError`, `ExpiredOrCancelledError`, `RateLimitedError`, `GenericError`) which have dedicated stories.
- **InProgress + Completed CTAs** (Frames 08, 09): `InProgressCTA` / `CompletedCTA` placeholder components mentioned in `index.ts` comment block lines 9-13 but NOT yet shipped. Confirm rendering matches mockup or defer follow-up.
- **Bottom-sheet RSVP CTA** (Frame 03 `sticky:'rsvp-pending'`): mobile-only — viewport addon required. Confirms decision 3c mobile sticky CTA deferred per `GameNightDetailView.tsx` line 16.
