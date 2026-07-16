# Mockups Index

> **Purpose**: navigation-first index of every file in `design_files/`, classified by
> type and mapped to user-reachable Next.js routes. Companion to the rich design
> handoff doc in [`README.md`](./README.md) (which is narrative).
>
> **Audience**: developers looking for "which mockup file do I need for route X?".
>
> **Last updated**: 2026-05-31. Keep in sync with
> [`docs/for-developers/frontend/v2-migration-matrix.md`](../docs/for-developers/frontend/v2-migration-matrix.md)
> Route Index section.

## Classification

| Type | Meaning |
|------|---------|
| **page-mock** | Full-screen reference for a single user-reachable route. |
| **component-mock** | Sub-view, overlay, drawer, or shared component used inside a page-mock. Not a standalone route. |
| **dev-fixture** | Design-system reference, prototype, dataset, or token file. Not for production cloning. |

> The two are equivalent: HTML for browser preview, JSX for codebase clone. The index lists the
> HTML file as canonical when both exist.

> **State variants (DS-17 Phase 1, #2071)**: a mockup that ships multiple states uses the
> `<base>-state-NN-<label>.{html,jsx}` pattern with a fixed `NN` slot per state. See the
> dedicated section ["State variants"](./README.md#state-variants-2071-ds-17-phase-1)
> in `README.md` for the canonical table. Quick reference:
>
> | Suffix | State |
> |---|---|
> | _(none — bare `<base>.html`)_ | `01-default` |
> | `-state-02-empty` | empty / zero-data |
> | `-state-03-loading` | skeleton / loading |
> | `-state-04-error` | error / failure |
> | `-state-05-sse` | live stream (opt-in) |
> | `-state-06-offline` | offline / PWA fallback (opt-in) |
>
> Enforcement: `pnpm lint:mockup-state-naming` fails CI when a `*-state-*` file violates
> the pattern (missing `NN`, unknown label, or `NN` outside the catalog).

## Dev fixtures (design system, prototype, tokens)

| File | Type | Note |
|------|------|------|
| `00-hub.html` | dev-fixture | Navigation hub between the 5 design pages |
| `01-screens.html` | dev-fixture | 24 mobile screens in phone frames |
| `02-desktop-patterns.html` | dev-fixture | 3 desktop layout patterns side-by-side |
| `03-drawer-variants.html` | dev-fixture | 6 drawer variants compared |
| `04-design-system.html` | dev-fixture | Live design system playground |
| `05-dark-mode.html` | dev-fixture | Light vs dark side-by-side, 7 surfaces |
| `components.css` | dev-fixture | Shared component CSS (phone frame, nav, cards) |
| `data.js` | dev-fixture | Fake dataset, 9 cross-referenced entities |
| `mobile-app.jsx` | dev-fixture | Full mobile-app React prototype (~870 lines) |
| `sp4-play-records-data.js` | dev-fixture | Fake dataset for `sp4-play-records-*` page-mocks (shared across 5 frames) |
| `tokens.css` | dev-fixture | **Source of truth for design tokens** (port first) |
| `state-matrix.html` | dev-fixture | State matrix cross-route (8 route × 5 stati = 40 cell) — riusabile per Phase 2/3 |

## Auth & onboarding

| File | Type | Mapped routes |
|------|------|---------------|
| `notifications.html` | page-mock | _(SP1 legacy archive — superseded by `sp7-notifications-*`, #2028)_ |

## SP3 — Public surfaces & invitations

| File | Type | Mapped routes |
|------|------|---------------|

## SP4 — Authenticated core (Wave 1+2+3+4)

| File | Type | Mapped routes |
|------|------|---------------|
| `sp4-citation-pdf-viewer.html` | component-mock | Citation overlay used by game-chat tabs (`GameChatTab.tsx` → `CitationModal.tsx`), mounted in `/games/[id]` and `/library/[gameId]` (`?tab=aiChat`). NOT `/chat/[threadId]` — that route renders `PageViewerPanel.tsx`, a plain extracted-text side panel with no Snippet/PDF tabs or ownership gating. `/chat/[threadId]` full-screen has no page-mock (known gap, issue #491, see `docs/for-developers/audits/2026-05-22-mockup-gaps.md`). |
| `sp4-game-chat-tab.html` | component-mock | Chat tab embedded in `/library/[gameId]/agent`, `/games/[id]` |
| `sp4-game-detail-tab-rules.html` | sub-tab placeholder (#2148) | `/games/[id]/rules` — AI placeholder, designer review required |
| `sp4-game-detail-tab-reviews.html` | sub-tab placeholder (#2148) | `/games/[id]/reviews` — friend-first M1 variant, designer review required |
| `sp4-game-detail-tab-strategies.html` | sub-tab placeholder (#2148) | `/games/[id]/strategies` — AI placeholder, designer review required |
| `sp4-game-detail-tab-chat.html` | sub-tab placeholder (#2148) | `/games/[id]/chat` — standalone (was partial via composite), designer review required |
| `sp4-game-detail-tab-faqs.html` | sub-tab placeholder (#2148) | `/games/[id]/faqs` — game-scoped variant of public FAQ, designer review required |
| `sp4-library-wishlist.html` | page-mock | `/library/wishlist` — personal wishlist (priority Alta/Media/Bassa, target price, notes); filters + sort + add/edit dialog. Issue #1491 |
| `sp4-parts-common.jsx` | component-mock | Shared mockup runtime — `window.MAI` (entity helpers, StateBlock/Shimmer/SseBanner, fake dataset). Re-derived for sessions consolidation (2026-05-31). Replace with codebase modules at integration time. |
| `sp4-session-catan-data.jsx` | component-mock | Catan-specific dataset (hex board 19-tiles, resources, settlements/cities/roads, dev cards). Premium #3/7. |
| `sp4-session-catan-flavor.jsx` | component-mock | Catan flavor components — `HexBoard`, `RobberOverlay`, `DiceDisplay`, `TradePanel`, `DevCardsPanel`, `ResourceHandBar`. |
| `sp4-session-catan-live.html` | page-mock | `/sessions/[id]/live` Catan demo (medium euro + trading, 3-4 players, ~75-90 min). Extends skeleton with Catan-specific panels. |
| `sp4-session-catan-live.jsx` | component-mock | Root component for `sp4-session-catan-live.html` — wires skeleton + hex board + RightColumnTabs (Scoring, Trade, Dev, Build, Chat). |
| `sp4-session-catan-parts.jsx` | component-mock | Shared parts for Catan — player rail with hand counts + dev cards + pieces remaining. |
| `sp4-session-catan-summary.html` | page-mock | `/sessions/[id]` Catan post-game (final VP: settlements + cities + Longest Road + Largest Army + VP dev cards). Premium #3/7. |
| `sp4-session-catan-summary.jsx` | component-mock | Root component for Catan summary — hero + tabs (Scoreboard / Final Board / Stats). |
| `sp4-session-codenames-bodies.jsx` | component-mock | Codenames body layouts — DesktopBody + MobileBody composition. |
| `sp4-session-codenames-data.jsx` | component-mock | Codenames-specific dataset (5x5 word grid, key card pattern, team agents 9/8, clue history). Premium #7/7. |
| `sp4-session-codenames-flavor.jsx` | component-mock | Codenames flavor components — `WordGrid`, `WordCard` (5 states: covered/red/blue/neutral/assassin), `SpymasterKeyCardOverlay`, `TeamPanel`, `CurrentCluePanel`, `ClueHistoryTimeline`, `RoleAvatar`. |
| `sp4-session-codenames-live.html` | page-mock | `/sessions/[id]/live` Codenames demo (team deduction party game, 2-8+ players in 2 teams, ~15 min). Extends skeleton with Codenames-specific panels + accordion-extended. |
| `sp4-session-codenames-live.jsx` | component-mock | Root component for `sp4-session-codenames-live.html` — wires skeleton + word grid + RightColumnTabs (Scoring Ranking, Board, Clue history, Teams, Chat). |
| `sp4-session-codenames-parts.jsx` | component-mock | Shared parts for Codenames — SectionCard accordion, helper sec(id). Mirrors skeleton + Power Grid extension. |
| `sp4-session-codenames-summary.html` | page-mock | `/sessions/[id]` Codenames post-game (WINNER team banner + Red/Blue agents found + assassin status + clue analysis). Premium #7/7. |
| `sp4-session-codenames-summary.jsx` | component-mock | Root component for Codenames summary — hero + tabs (Scoreboard / Final Board / Clue Analysis / Stats). |
| `sp4-session-paleo-data.jsx` | component-mock | Paleo-specific dataset (tribe state, day phases, mission deck, BinaryWin co-op simultaneous). Premium #6/7. |
| `sp4-session-paleo-flavor.jsx` | component-mock | Paleo flavor components — `TribePanel`, `DayPhaseIndicator`, `CardsDeckPanel`, `PlayerHandPanel`, `ActionRevealOverlay`, `CavePaintingProgress`, `SkullCluster`. |
| `sp4-session-paleo-live.html` | page-mock | `/sessions/[id]/live` Paleo demo (co-op preistorico Simultaneous, 1-4 players, ~45-60 min). Extends skeleton with accordion-extended for game panels. |
| `sp4-session-paleo-live.jsx` | component-mock | Root component for `sp4-session-paleo-live.html` — wires skeleton + Paleo flavor + RightColumnTabs (Scoring, Tribe, Cards, Skills, Chat). |
| `sp4-session-paleo-parts.jsx` | component-mock | Shared parts for Paleo — SectionCard accordion (11 utilizzi), helper sec(id) per accordion state machine. Mirrors Power Grid pattern. |
| `sp4-session-paleo-summary.html` | page-mock | `/sessions/[id]` Paleo post-game (VICTORY 5 cave paintings / DEFEAT 5 skulls or tribe extinct + cause-of-loss + tribe journey). Premium #6/7. |
| `sp4-session-paleo-summary.jsx` | component-mock | Root component for Paleo summary — hero VICTORY/DEFEAT + tabs (Scoreboard / Tribe Journey / Cards played / Stats). |
| `sp4-session-power-grid-data.jsx` | component-mock | Power Grid-specific dataset (Elektro, 4 resources market, 8 plants, 5 phases, 3 game steps). Premium #4/7. |
| `sp4-session-power-grid-flavor.jsx` | component-mock | Power Grid flavor components — `PhaseTimeline`, `PowerPlantMarket`, `ResourceMarket`, `AuctionOverlay`, `NetworkMap`, `PlantsRail`, `TurnOrderStrip` (reverse-aware). |
| `sp4-session-power-grid-live.html` | page-mock | `/sessions/[id]/live` Power Grid demo (heavy euro auction + network, 2-6 players, ~120 min). Extends skeleton with PG-specific panels + accordion-extended for game sections (auction/market/resources). |
| `sp4-session-power-grid-live.jsx` | component-mock | Root component for `sp4-session-power-grid-live.html` — wires skeleton + PG flavor + RightColumnTabs (Scoring, Market, Network, Plants, Chat). |
| `sp4-session-power-grid-parts.jsx` | component-mock | Shared parts for Power Grid — `SectionCard` accordion, player rail, helper `sec(id)` for accordion state machine across 5 phases. |
| `sp4-session-power-grid-summary.html` | page-mock | `/sessions/[id]` Power Grid post-game (final: cities powered + Elektro tiebreaker + plants capacity). Premium #4/7. |
| `sp4-session-power-grid-summary.jsx` | component-mock | Root component for PG summary — hero + tabs (Scoreboard / Network snapshot / Step transitions / Stats). |
| `sp4-session-puerto-rico-data.jsx` | component-mock | Puerto Rico-specific dataset (role-selection state, 5 goods, plantations + buildings grids). Premium #2/7. |
| `sp4-session-puerto-rico-flavor.jsx` | component-mock | Puerto Rico flavor components — `RoleSelectionBoard`, `PlantationGrid`, `BuildingGrid`, `GalleonsShipping`, `TradingHouseSlots`, `ColonistShip`. |
| `sp4-session-puerto-rico-live.html` | page-mock | `/sessions/[id]/live` Puerto Rico demo (heavy euro role-selection, 3-5 players, ~120min). Extends skeleton (mockup #1) with PR-specific panels. |
| `sp4-session-puerto-rico-live.jsx` | component-mock | Root component for `sp4-session-puerto-rico-live.html` — wires skeleton + PR flavor + RightColumnTabs (Scoring, Roles, Trade, Ship, Chat). |
| `sp4-session-puerto-rico-parts.jsx` | component-mock | Shared parts for Puerto Rico — player mat, role cards, role action flow. |
| `sp4-session-puerto-rico-summary.html` | page-mock | `/sessions/[id]` Puerto Rico post-game (final VP breakdown: buildings + shipped goods + large building bonuses). Premium #2/7. |
| `sp4-session-puerto-rico-summary.jsx` | component-mock | Root component for PR summary — hero + tabs (Scoreboard / Final Board / Round Recap / Stats). |
| `sp4-session-skeleton-data.jsx` | component-mock | Demo datasets (Wingspan + Paleo) for the generic session skeleton — `window.SkelData`. Used only inside `sp4-session-skeleton-*` mockup to validate polymorphic rendering side-by-side. |
| `sp4-session-skeleton-parts.jsx` | component-mock | Shared building blocks for the skeleton (TopBar, ChatAgentPanel, ActionLog, RightColumnTabs container, DesktopFrame, PhoneShell side-by-side wrapper). Game-agnostic. |
| `sp4-session-skeleton-renderers.jsx` | component-mock | **Polymorphic renderers** — `ScoringPanelRenderer` (switch on ScoreType: Points/Ranking/BinaryWin/Objectives), `TurnIndicatorRenderer` (switch on TurnOrderType: 7 variants), `WidgetRenderer` (6 WidgetType dispatch). Zero game-specific code. Mirrors FE renderers shipped in PR #1763 (B19-4a). |
| `sp4-session-wingspan-live-parts.jsx` | component-mock | Sub-components of `/sessions/[id]/live` Wingspan demo — `window.LiveSessionParts1`. **Wingspan-specific** (scoring categories hard-coded). Generic skeleton tracked in B19. |
| `sp4-session-wingspan-live-tabs.jsx` | component-mock | `window.LiveTabs` — 4 new consolidated tabs (scores · photos · agent · players) × 5 stati each (default · empty · loading · error · sse). **Wingspan-flavored content**. See consolidation ADR `claudedocs/2026-05-31-sessions-consolidation-adr.md` + spike `claudedocs/2026-05-31-spike-toolkit-ai-generation.md`. |
| `sp4-session-wingspan-live.html` | page-mock | `/sessions/[id]/live` Wingspan demo + consolidated tabs `?tab=scores\|photos\|agent\|players\|chat\|tools\|notes` (was 4 separate sub-routes pre-2026-05-31, see ADR). Also reuses for `/sessions/live/[sessionId]/*`. **Wingspan-specific** — generic session skeleton in B19. |
| `sp4-session-wingspan-summary-parts.jsx` | component-mock | Sub-components of `/sessions/[id]` Wingspan demo — `window.SummaryParts`. **Wingspan-flavored**. |
| `sp4-session-wingspan-summary-sections.jsx` | component-mock | Celebrative body sections of `/sessions/[id]` Wingspan demo (podium, KPI, diary, photos, chat highlights, share) — unchanged in 2026-05-31 consolidation. **Wingspan-flavored content**. |
| `sp4-session-wingspan-summary-tabs.jsx` | component-mock | `window.SummaryReviewTabs` — 3 new consolidated tabs (scoreboard · notes · players) × 5 stati each (default · empty · loading · error · offline). **Wingspan scoring categories hard-coded**. See consolidation ADR. |
| `sp4-session-wingspan-summary.html` | page-mock | `/sessions/[id]` Wingspan demo + consolidated tabs `?tab=scoreboard\|notes\|players` (was 3 separate sub-routes pre-2026-05-31, see ADR). **Wingspan-specific**. |
| `sp4-session-zombicide-data.jsx` | component-mock | Zombicide Green Horde-specific dataset (survivors w/ skill trees, zombie counts per type, scenario objectives). Premium #5/7. |
| `sp4-session-zombicide-flavor.jsx` | component-mock | Zombicide GH flavor components — `SurvivorCard` (skill tree Blue→Yellow→Orange→Red + equipment + wounds + AP), `BoardStatePanel`, `CombatDicePanel`, `SpawnDeckIndicator`, `PhaseTimeline` (3-phase round), `MapTilesGrid`. |
| `sp4-session-zombicide-live.html` | page-mock | `/sessions/[id]/live` Zombicide GH demo (co-op miniatures dungeon-crawler, 1-6 players, ~60-90 min/scenario). Extends skeleton + accordion-extended for game panels. |
| `sp4-session-zombicide-live.jsx` | component-mock | Root component for `sp4-session-zombicide-live.html` — wires skeleton + Zombicide flavor + RightColumnTabs (Scoring, Dice, Board, Equip, Chat). |
| `sp4-session-zombicide-parts.jsx` | component-mock | Shared parts for Zombicide — SectionCard accordion (16 utilizzi), helper `sec(id)` per accordion state machine across survivors/board/spawn panels. |
| `sp4-session-zombicide-summary.html` | page-mock | `/sessions/[id]` Zombicide post-game (VICTORY/DEFEAT banner + mission objectives + survivors final state + XP totals + kill stats). Premium #5/7. |
| `sp4-session-zombicide-summary.jsx` | component-mock | Root component for Zombicide summary — hero + tabs (Scoreboard / Survivors / Board final / Stats). |

## SP5 — Admin & Profile settings

| File | Type | Mapped routes |
|------|------|---------------|

## SP6 — Libro-game (Nanolith dogfood Iter 1+4)

> **Note 2026-06-08 (#2025 cleanup)**: 3 JSX Sara obsoleti eliminati
> (`sp6-libro-game-{play-session,translation-viewer,glossary-editor}.jsx`).
> Canonici sostitutivi: `librogame-runthrough-{play-session,translate-viewer,glossary-editor}.html`
> (persona Aaron, IA consolidata post #871). Vedi audit
> `docs/for-developers/audits/2026-06-08-mockup-portfolio-review.md` Cluster B/C/E.

_The `sp6-libro-game-*` family was retired by #2152 (#2025 cleanup completion).
Canonical equivalents live under `librogame-runthrough-*` (Aaron Iter 1 cluster
below). Only `librogame-runthrough-quota-credits.html` (checkout/quota overlay,
`CheckoutModal.tsx` + `QuotaWidget`/`SoftWarningCredits`, issue #953) actually
renders inside `/gamebook`. `librogame-runthrough-library-search.html` maps to
`/library` instead — do NOT use it for `/gamebook`. The historic `/gamebook`
index page-mock (`sp6-libro-game-index.jsx`) was deleted and never replaced:
the `GamebookHero` + `GamebookCard` grid has no page-mock coverage (gap)._

## SP7 — Game nights

| File | Type | Mapped routes |
|------|------|---------------|
| `sp7-game-night-transition.html` | component-mock | Modal opened from `/game-nights/[id]/live` (issue #487 screen #5) |

## Chat

| File | Type | Mapped routes |
|------|------|---------------|

## Nanolith — Runthrough storyboard (Aaron Iter 1)

| File | Type | Mapped routes |
|------|------|---------------|
| `librogame-game-night-storyboard.html` | dev-fixture | Meta-storyboard: embeds the `librogame-runthrough-*` mockups via `<iframe>` as a pre-implementation validation timeline (was `nanolith-game-night-storyboard.html` pre-rename #871, sync inline 2026-06-08 #2025). Not a page render-surface — route `/game-nights/[id]` is covered by `game-night-detail-rsvp.stories.tsx`. Scoped out of `lint:storybook-states` per #2970 (Option B). |
| `primitive-nav-bottom-mobile.html` | component-mock | Mobile bottom-nav primitive (global, was `nanolith-nav-bottom-mobile.html` pre-#2152) |
| `primitive-nav-chat-panel.html` | component-mock | Chat slide-over panel (used globally via `useChatPanel`, was `nanolith-nav-chat-panel.html` pre-#2152) |
| `primitive-nav-topbar.html` | component-mock | Top-bar primitive (global, was `nanolith-nav-topbar.html` pre-#2152) |
| `librogame-runthrough-error-states.html` | component-mock | Trasversale: chat (N1/N2) · translate (N3) · encounter — stream-timeout / OCR-fail / LLM-503 / segmentation-fail (PR #1056) |
| `librogame-runthrough-glossary-editor.html` | component-mock | `GlossaryEditorModal.tsx` (issue #952) — designed to float over `TranslateViewer` on `/library/[gameId]/play/[campaignId]/translate`; component built but NOT mounted in any live route (only imported by its own story/tests). NOT the mockup for `.../encounter` — that route renders `EncounterCheatsheetView` + the read-only `GlossaryLookupModal`, which has no mockup coverage (gap). |
| `librogame-runthrough-library-search.html` | component-mock | In-library game search overlay for `/library` (own header states "Route: /library"). NOT `/gamebook` — `GamebookIndexView.tsx` uses no such search / `MeepleCard`-grid pattern. |
| `librogame-runthrough-quota-credits.html` | component-mock | Quota/credits + checkout overlay, shipped inside `/gamebook` (`GamebookIndexView.tsx` → `QuotaWidget`/`SoftWarningCredits`/`CheckoutModal.tsx`, SP6 Iter 1.B #953). File header's claimed migration to `/library/.../play` + `/account/checkout/success` (#871) was never built — no `/account` or `/checkout` route exists; treat that comment as stale. |

## Summary

| Type | Count |
|------|------:|
| page-mock | 67 |
| component-mock | 48 |
| dev-fixture | 13 |
| **Total** | **128** |

> **Updated 2026-06-08** (#2025): 3 component-mock JSX Sara obsoleti eliminati
> (`sp6-libro-game-{play-session,translation-viewer,glossary-editor}.jsx`).
> Companion: file `nanolith-game-night-storyboard.html` rinominato in
> `librogame-game-night-storyboard.html` (sync con filesystem post-IA #871).

> The `*.jsx` twins of `*.html` files are not double-counted (the JSX is the
> implementation companion of the HTML reference). Listing them separately
> would inflate the count to ~110 without adding signal.

## Gaps (routes without a mockup)

See [`docs/for-developers/audits/2026-05-12-mockup-gaps.md`](../docs/for-developers/audits/2026-05-12-mockup-gaps.md)
for the audit of 5 user-reachable routes lacking mockup coverage as of 2026-05-12.
