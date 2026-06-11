# MSW gap analysis — sp7-game-night-create

**Hero component**: `GameNightCreateWizard`
**Page-client wiring**: `_content.tsx` `NewGameNightContent` consumes 6 react-query hooks.

## API endpoints consumed (page-client + hero)

Cross-referenced with `apps/web/src/app/(authenticated)/game-nights/new/_content.tsx` imports + `lib/game-nights/hooks/` files.

| Endpoint | Method | Consumer (file + hook) | Existing handler? |
|----------|--------|------------------------|-------------------|
| `/api/v1/game-nights` | POST | `useCreateGameNight` (hooks/queries/useGameNights.ts) | **gap** — fixture stubs both default + error |
| `/api/v1/game-nights/conflict-check` | GET | `useGameNightConflictCheck` (lib/game-nights/hooks/useGameNightConflictCheck.ts) | **gap** — fixture stubs default+conflict |
| `/api/v1/game-nights/regulars` | GET | `useRegularsForUser` (lib/game-nights/hooks/useRegularsForUser.ts) | **gap** — fixture stubs default |
| `/api/v1/games` | GET | `useLibrary` (hooks/queries/useLibrary.ts) | **gap** — fixture stubs default with `MOCK_SP7_CREATE_LIBRARY_GAMES` |
| `/api/v1/users/search` | GET | `usePlayerSearch` (lib/game-nights/hooks/usePlayerSearch.ts) | **gap** — fixture stubs typing variant ("fede" → Federica) |
| `/api/v1/auth/me` | GET | `useCurrentUser` (hooks/queries/useCurrentUser.ts) | shared global handler in storybook config |

## BGG hygiene check

No BGG endpoint consumed by this story:
- Library catalog uses internal `/api/v1/games` (catalog endpoint), NOT `useSearchBggGames` (admin-only).
- Game cover URLs in fixture use `coverUrl: undefined` placeholder (resolver chain L4→L3→L2→L1 will fall back to deterministic placeholder via `cover-utils.ts` per ADR-059).
- ZERO references to `cf.geekdo-images.com`, `*.boardgamegeek.com`, `images.geekdo.com`, `geekdo-images.com`.

## Handler structure (recommended)

Use the `mswForSp7CreateState(state)` switcher pattern (matches `mswForAuthFlowState` in DS-17-9 pilot fixture). States: `'default' | 'conflict' | 'submitting' | 'error'`.

## Follow-ups

- Wire `useCurrentUser` global mock if Storybook isn't already providing it via decorator.
- Verify `WizardState` shape against `lib/game-nights/wizard-types.ts` — fixture uses `as unknown as WizardState` cast (scaffold-quality); replace with type-correct construction in implementation PR.
- `MOCK_SP7_CREATE_LABELS.step{1..4}` and `preview` typed as `never` (scaffold stub) — implementation PR must construct the full nested label tree (see `buildWizardLabels` in `_content.tsx`).
