# Card decision-table — which component for which DTO × context

**Issue #2858 (C1).** Two canonical tiers. When you port a mockup that shows a
"card": **pick the tier from the context** (list/grid → DISPLAY; drawer/detail
page → DETAIL), **then the adapter from the DTO**. If no adapter exists, create
one that composes the canonical card — **never** a standalone renderer.

- **DISPLAY tier** = `MeepleCard` (`ui/data-display/meeple-card/`), 5 variants
  (`grid` · `list` · `compact` · `featured` · `hero`). Consumed via a
  DTO→props adapter (mappers in `lib/card-mappers/`).
- **DETAIL tier** = `ExtraMeepleCard` (`ui/data-display/extra-meeple-card/`),
  the 600×900 tabbed drawer/detail card. Consumed via `ExtraMeepleCardDrawer`
  (cascade-navigation-store).

> This table is enforced by `apps/web/src/__tests__/card-decision-table.test.ts`:
> every production file that renders `<MeepleCard>` must have an exported
> component listed below (coverage), and every adapter named below must exist
> (no dangling rows). Keep it in sync — a missing row breaks the build.

## DISPLAY tier — MeepleCard adapters

| Context / route | Primary DTO | Entity | Adapter | Typical variant |
|---|---|---|---|---|
| `/shared-games` | shared tile | game | `MeepleCardGame` | grid |
| `/games?tab=discover`, `/games?tab=catalog`, `/games?tab=trending` | `SharedGame` | game | `MeepleGameCatalogCard` | grid · featured · hero |
| `/games` catalog (legacy Game API), dashboard recent | `Game` | game | `MeepleGameCard` | grid · compact |
| `/library?tab=games` (owned) | `UserLibraryEntry` | game | `MeepleUserLibraryCard` | grid |
| `/library?tab=games`, dashboard, home feed | `UserLibraryEntry` | game | `MeepleLibraryGameCard` | grid · compact · list |
| `/agents`, `/library?tab=agents`, `/hub/agents` | `AgentDto` / `AgentSummary` | agent | `MeepleAgentCard` | grid |
| `/library?tab=kb`, game detail KB list | `PdfDocumentDto` | kb | `MeepleKbCard` | grid |
| `/library?tab=sessions`, sessions grid | `GameSessionDto` | session | `MeepleSessionCard` | grid |
| `/library?tab=chat`, dashboard recent chats | `ChatSessionSummaryDto` | chat | `MeepleChatCard` | grid |
| `/dashboard#Prossimi`, `/dashboard#Recenti` | `GameNightSummary` | event | `MeepleEventCard` | list · compact |
| session participant lists | `SessionPlayer` | player | `MeeplePlayerCard` | compact |
| `/library/wishlist` | `WishlistItemDto` | game | `MeepleWishlistCard` | list |
| game toolbox list (epic #412, not yet route-wired) | `ToolboxDto` | toolkit | `ToolboxKitCard` | grid |
| `/shared-games/[id]` contributors section | `GameContributorDto` | player | `MeepleContributorCard` | list |
| resume-session entry point (`/sessions/[id]/scoreboard`, not yet route-wired) | ad hoc props (session id/name/players) | session | `MeepleResumeSessionCard` | list |
| `/library/[gameId]/toolkit/[sessionId]` scoreboard | `Participant` | player | `MeepleParticipantCard` | compact · grid |
| `/library/private/[id]` (via `PrivateGameHub`) | `PausedSession` | session | `MeeplePausedSessionCard` | list |
| `/game-nights` (via `GameNightList`) | `GameNightSummary` | event | `MeepleGameNightCard` | grid |
| `/game-nights/[id]` planning layout — dealt-card hand | `GameNightGame` | game | `MeepleDealtGameCard` | compact |
| `/game-nights/[id]` planning layout — AI suggestions panel (Draft state) | ad hoc `Suggestion[]` props | agent | `MeepleAISuggestionCard` | featured |
| game-detail chat panel (`AgentChatPanel`) PDF citations (test-only, no confirmed route) | ad hoc `PdfReference` props | kb | `MeeplePdfReferenceCard` | list |
| `/library/[gameId]` mobile hero (`game-detail-mobile.tsx`) | `LibraryGameDetail` | game | `FocusedGameCard` | hero |
| `/admin/knowledge-base/vectors` | `VectorGameBreakdown` | kb | `VectorGameCard` | grid |

> **Excluded — not an adapter**: `apps/web/src/components/admin/ui-library/scenes/EntityCardsScene.tsx`
> exports `EntityCardsScene` (name contains "Card" via "Cards", so it is picked
> up by the coverage scan even though it is not a `*Card` component). It is an
> admin UI-library **showcase page** with hardcoded mock data (four static
> entity examples used to preview `MeepleCard` variants), not a reusable
> DTO→props adapter — it is mentioned here only so the coverage test recognizes
> it as accounted for; do not treat it as a decision-table row or copy its
> pattern for a real adapter. Future showcase-only files should live under a
> `dev/` or `showcase/` path (already excluded by the test's glob ignores)
> instead.

## DETAIL tier — ExtraMeepleCard adapters (drawer / detail)

| Context | Primary DTO | Entity | Adapter |
|---|---|---|---|
| cascade drawer (game) | `GameDetailData` | game | `GameExtraMeepleCard` |
| cascade drawer (chat) | `ChatDetailData` | chat | `ChatExtraMeepleCard` |
| cascade drawer (kb) | `KbDetailData` | kb | `KbExtraMeepleCard` |
| admin shared-game detail | `SharedGameDetail` | game | `SharedGameExtraMeepleCard` |

## Rule of thumb

- Need a new list/grid card for entity X? Write `Meeple<X>Card` as an adapter
  that returns `<MeepleCard entity="x" … />`. Do **not** import from
  `meeple-card/parts/` or `meeple-card/variants/` (ESLint
  `local/no-standalone-card-renderer` forbids it) and do **not** hand-roll
  cover/stars/badge (C4 body-gate, #2861).
- Need a detail/drawer surface? Add an `ExtraMeepleCard` entity variant.
