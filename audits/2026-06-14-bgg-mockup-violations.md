# BGG Mockup + FE Source Violations — Inventory (DS-17 §2151)

| Field | Value |
|---|---|
| **Date** | 2026-06-14 |
| **Generator** | `pnpm lint:bgg-mockups` (DS-17 §2151) |
| **ADR** | [`adr-059-catalog-seed-legal-posture`](../docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md) |
| **Pattern** | `\bBGG\b|\bBoardGameGeek\b|\bboardgamegeek\b` |
| **Total violations** | 325 |
| **Baseline ceiling** | 0 |

## Scope summary

| Scope | Files scanned | Violations | Files affected | Skipped (admin) | Skipped (well-known) | Skipped (obsolete fidelity) |
|---|---|---|---|---|---|---|
| Mockups (`admin-mockups/design_files/**/*.{html,jsx}`) | 219 | 52 | 17 | _(n/a)_ | _(n/a)_ | 0 |
| FE source (`apps/web/src/**/*.{ts,tsx,js,jsx}`) | 3456 | 273 | 67 | 627 | 6 | _(n/a)_ |

## Violations by file

| File | Count |
|---|---|
| `apps/web/src/lib/api/clients/sharedGamesClient.ts` | 42 |
| `apps/web/src/app/(authenticated)/gamebook/upload/_components/GamebookUploadView.tsx` | 21 |
| `apps/web/src/lib/gamebook-upload/fsm.ts` | 18 |
| `apps/web/src/components/features/gamebook/GameSearchCard.tsx` | 13 |
| `apps/web/src/lib/gamebook-upload/schemas.ts` | 12 |
| `apps/web/src/lib/api/clients/gameNightBggClient.ts` | 11 |
| `apps/web/src/hooks/queries/useBggSearch.ts` | 10 |
| `apps/web/src/lib/gamebook-upload/visual-test-fixture.ts` | 10 |
| `apps/web/src/lib/api/schemas/shared-games.schemas.ts` | 9 |
| `apps/web/src/lib/api/index.ts` | 8 |
| `admin-mockups/design_files/00-hub.html` | 7 |
| `admin-mockups/design_files/sp4-upload-wizard-extended.jsx` | 7 |
| `apps/web/src/components/features/gamebook/GameSearchBar.tsx` | 7 |
| `apps/web/src/components/ui/data-display/rating-stars.tsx` | 7 |
| `apps/web/src/lib/domain-hooks/useBggRateLimit.ts` | 7 |
| `admin-mockups/design_files/settings.jsx` | 6 |
| `admin-mockups/design_files/sp4-game-chat-tab.html` | 6 |
| `apps/web/src/components/features/gamebook/NoResultsPanel.tsx` | 6 |
| `apps/web/src/hooks/queries/useSearchBggGames.ts` | 6 |
| `admin-mockups/design_files/sp4-library-wishlist-ui.jsx` | 5 |
| `apps/web/src/lib/api/schemas/admin-mechanic-extractor-validation.schemas.ts` | 5 |
| `admin-mockups/design_files/sp4-add-game-drawer.jsx` | 4 |
| `admin-mockups/design_files/sp4-library-wishlist.jsx` | 4 |
| `apps/web/src/app/(authenticated)/library/_components/LibraryHub.tsx` | 4 |
| `apps/web/src/components/features/library/EmptyLibrary.tsx` | 4 |
| `apps/web/src/components/ui/data-display/entity-link/entity-link-card.tsx` | 4 |
| `apps/web/src/hooks/admin/useImportBggTags.ts` | 4 |
| `apps/web/src/hooks/wizard/useCheckDuplicate.ts` | 4 |
| `apps/web/src/app/(authenticated)/dashboard/_components/sections/SuggestedSection.tsx` | 3 |
| `apps/web/src/lib/api/clients/admin/adminMechanicExtractorValidationClient.ts` | 3 |
| `apps/web/src/lib/api/schemas/games.schemas.ts` | 3 |
| `admin-mockups/design_files/librogame-runthrough-game-detail.html` | 2 |
| `admin-mockups/design_files/sp4-add-game-drawer.html` | 2 |
| `admin-mockups/design_files/sp4-upload-wizard-extended.html` | 2 |
| `apps/web/src/app/(authenticated)/onboarding/OnboardingGenericWizard.tsx` | 2 |
| `apps/web/src/components/features/gamebook/LibroGameDetailView.tsx` | 2 |
| `apps/web/src/components/game-detail/GameHero.tsx` | 2 |
| `apps/web/src/components/library/add-game-sheet/steps/GameInfoStep.tsx` | 2 |
| `apps/web/src/config/component-registry.ts` | 2 |
| `apps/web/src/hooks/queries/useLibrary.ts` | 2 |
| `apps/web/src/lib/api/clients/gamesClient.ts` | 2 |
| `apps/web/src/lib/library/hybrid-hub.mappers.ts` | 2 |
| `apps/web/src/lib/utils/string-similarity.ts` | 2 |
| `admin-mockups/design_files/02-desktop-patterns.html` | 1 |
| `admin-mockups/design_files/librogame-game-night-storyboard.html` | 1 |
| `admin-mockups/design_files/sp3-how-it-works.jsx` | 1 |
| `admin-mockups/design_files/sp3-shared-game-detail.jsx` | 1 |
| `admin-mockups/design_files/sp4-citation-pdf-viewer.html` | 1 |
| `admin-mockups/design_files/sp5-profile-settings.html` | 1 |
| `admin-mockups/design_files/sp5-profile-settings.jsx` | 1 |
| `apps/web/src/app/(authenticated)/dashboard/_components/sections/GamesCarousel.tsx` | 1 |
| `apps/web/src/app/(authenticated)/dashboard/_components/sections/RecentiSection.tsx` | 1 |
| `apps/web/src/app/(authenticated)/library/AddGameDrawer.tsx` | 1 |
| `apps/web/src/app/(public)/shared-games/[id]/page-client.tsx` | 1 |
| `apps/web/src/app/(public)/shared-games/page-client.tsx` | 1 |
| `apps/web/src/components/catalog/GamesFilterPanel.tsx` | 1 |
| `apps/web/src/components/collection/CollectionGameGrid.tsx` | 1 |
| `apps/web/src/components/features/gamebook/ActionCard.tsx` | 1 |
| `apps/web/src/components/features/hub/HubGameCard.tsx` | 1 |
| `apps/web/src/components/features/library/LibraryHeroDesktop.tsx` | 1 |
| `apps/web/src/components/features/toolkit-detail/Stars.tsx` | 1 |
| `apps/web/src/components/game-detail/GameDetailDesktop.tsx` | 1 |
| `apps/web/src/components/game-night/GameNightWizard.tsx` | 1 |
| `apps/web/src/components/library/AddPrivateGameForm.tsx` | 1 |
| `apps/web/src/components/library/add-game-sheet/AddGameWizardProvider.tsx` | 1 |
| `apps/web/src/components/library/add-game-sheet/steps/GameSearchResults.tsx` | 1 |
| `apps/web/src/components/play-records/GameCombobox.tsx` | 1 |
| `apps/web/src/components/shared-games/SharedGameSearch.tsx` | 1 |
| `apps/web/src/components/showcase/stories/metadata.ts` | 1 |
| `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx` | 1 |
| `apps/web/src/components/ui/data-display/meeple-card/parts/GameCoverPlaceholder.tsx` | 1 |
| `apps/web/src/components/ui/data-display/meeple-card/types.ts` | 1 |
| `apps/web/src/components/ui/data-display/meeple-card/variants/HeroCard.tsx` | 1 |
| `apps/web/src/components/ui/data-display/meeple-info-card.tsx` | 1 |
| `apps/web/src/hooks/admin/useGoldenForGame.ts` | 1 |
| `apps/web/src/hooks/queries/useAdminGameWizard.ts` | 1 |
| `apps/web/src/hooks/queries/useGames.ts` | 1 |
| `apps/web/src/lib/analytics/flywheel-events.ts` | 1 |
| `apps/web/src/lib/api/clients/admin/adminContentClient.ts` | 1 |
| `apps/web/src/lib/api/clients/libraryClient.ts` | 1 |
| `apps/web/src/lib/api/core/httpClient.ts` | 1 |
| `apps/web/src/lib/api/schemas/private-games.schemas.ts` | 1 |
| `apps/web/src/lib/stores/add-game-wizard-store.ts` | 1 |
| `apps/web/src/stores/useGameImportWizardStore.ts` | 1 |

## How to suppress a legitimate occurrence

1. **Mockup is intentionally obsolete** — set `design_intent: "forward-refactor-obsolete"` in the sibling `<base>.fidelity.json` (reference #1903 ADR).
2. **Admin server-to-server BGG** (per ADR-059 §2) — move the file under `apps/web/src/app/admin/`, `apps/web/src/components/admin/`, or `apps/web/src/app/api/`. Already-known admin paths pass automatically.
3. **Line-level justification** — add `BGG-ALLOWED: <reason>` as a comment on the offending line, or within the 2 preceding lines:
   ```
   /* BGG-ALLOWED: legitimate parser for legacy BGG export TSV */
   const BGG_TSV_HEADER = "boardgamegeek collection export";
   ```

## CI gate semantics

- Default (`pnpm lint:bgg-mockups`) = inventory only, exit 0.
- Strict (`pnpm lint:bgg-mockups --strict --max-baseline N`) = exit 1 when count > N. CI passes N as a frozen ceiling; introducing a NEW BGG copy fails the gate.
- Whitelist is intentional: residual BGG copy in `current` mockups is tracked until later cleanup waves bring the ceiling down. NEW copy is rejected.

## Refs

- Issue: [#2151](https://github.com/meepleAi-app/meepleai-monorepo/issues/2151)
- ADR: [`adr-059-catalog-seed-legal-posture.md`](../docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md)
- Pattern reference: [`lint-tokens-mockups.mjs`](../apps/web/scripts/lint-tokens-mockups.mjs) (DS-17-2 #2070)
- Parent BGG ban: [#2123](https://github.com/meepleAi-app/meepleai-monorepo/issues/2123) (3-plane enforcement)
