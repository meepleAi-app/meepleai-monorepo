# Phase 0 — Library SP4 Mockup Conformance Root Cause

**Date**: 2026-06-03
**Owner**: subagent (Task 0.1 — theme investigation)
**Status**: in-progress (Task 0.2 will append the card placeholder section)

## 1. Tema dark vs light — Code analysis

### Theme provider config

**File**: `apps/web/src/components/providers/ThemeProvider.tsx` (lines 21–43)
**Invoked from**: `apps/web/src/app/providers.tsx:148` inside `<AppProviders>`, which is mounted by `apps/web/src/app/layout.tsx:94` (`<AppProviders>{children}</AppProviders>`).

- Provider: `<NextThemesProvider attribute={['class', 'data-theme']} defaultTheme="light" enableSystem disableTransitionOnChange={false} {...props}>` (ThemeProvider.tsx:23–39)
- `defaultTheme`: `"light"` (ThemeProvider.tsx:35; comment line 31–33 explicitly notes "mockup default warm cream (#f7f3ee). Previously dark-first via legacy gaming palette; now the canonical light theme is authoritative.")
- `enableSystem`: `true` (ThemeProvider.tsx:36, bare prop = true). With next-themes semantics this means: on first visit with no localStorage entry, the resolved theme follows `prefers-color-scheme`, falling back to `defaultTheme` only when the OS expresses no preference.
- `attribute`: `['class', 'data-theme']` (ThemeProvider.tsx:34) — applies BOTH `class="dark"` AND `data-theme="dark"` on `<html>` for backwards compatibility with legacy `.dark .foo` selectors AND the canonical `[data-theme="dark"]` mockup convention.
- Forced theme: none. Grep for `defaultTheme|enableSystem|forcedTheme` across `apps/web/src/app/**/*.tsx` returns only `providers.tsx` references — no nested layout overrides ThemeProvider config, in particular `apps/web/src/app/(authenticated)/layout.tsx` (UserShell wrapper) and `apps/web/src/app/(authenticated)/library/layout.tsx` (passthrough fragment) are theme-neutral.
- SSR hint: `apps/web/src/app/layout.tsx:89` renders `<html lang="it" data-theme="light" suppressHydrationWarning>`. Comment line 85–87 confirms this is a FOUC-prevention hint that next-themes rewrites client-side based on user preference / localStorage.

### Canonical tokens default values

**File**: `apps/web/src/styles/design-tokens-canonical.css`
**Note**: `apps/web/src/styles/token-bridge.css` does NOT exist — the bridge layer was removed in DS-16 after the codemod (confirmed by `layout.tsx:20–21` comment: "Bridge layer (token-bridge.css) was removed in DS-16 after the codemod renamed all consumer references to canonical names."). The `:root` block at line 20 contains entity colors / shared HSL triplets only; the actual surface tokens live in the theme-scoped blocks below.

- `:root, :root[data-theme="light"]` block (default light theme, lines 144–155):
  - `--bg`: `#f7f3ee` (cream — matches mockup + CLAUDE.md statement)
  - `--bg-card`: `#ffffff`
  - `--text`: `#2b1f12` (dark brown on cream)
- `:root[data-theme="dark"], [data-theme="dark"]` block (lines 176–187):
  - `--bg`: `#14100a`
  - `--bg-card`: `#1e1710`
  - `--text`: `#f0e4d2`

Tokens agree with CLAUDE.md ("Default theme is light (mockup cream #f7f3ee), dark accessible via user toggle"). No drift.

### Diagnosis

- [x] **(a) User preference persisted**: `defaultTheme="light"` BUT `enableSystem={true}` could pick OS dark mode on first visit. localStorage persists last choice. — Screenshot reproducible only by user choice or OS preference. NOT a regression. Fix: no code change needed for the mockup conformance plan.
- [ ] **(b) Regression in theme provider**: ruled out — `defaultTheme="light"` is explicit and the SSR hint also sets `data-theme="light"`; no forced override exists.
- [ ] **(c) Drift in canonical tokens**: ruled out — `:root[data-theme="light"]` block at lines 144–155 declares `--bg: #f7f3ee` exactly as the mockup states.
- [ ] **(d) Mixed**: N/A.

Evidence trail for (a): provider declares the right defaults (ThemeProvider.tsx:35), tokens declare the right values (design-tokens-canonical.css:146), AND `enableSystem` is explicitly enabled (ThemeProvider.tsx:36). The only remaining mechanisms able to produce the dark screenshot are (1) developer's localStorage previously set `theme=dark` via the in-app `ThemeToggle`, or (2) the dev OS expresses `prefers-color-scheme: dark` and no localStorage entry exists yet. Both paths are user-side, not codebase-side.

### Implication for PR1/PR2/PR3 scope

The mockup conformance PRs DO NOT need to change theme defaults. Theme handling is out of scope of this initiative because the code already sets `defaultTheme="light"`, the canonical tokens already match the mockup cream `#f7f3ee` for `:root[data-theme="light"]`, and the dark theme observed in the baseline screenshot is a user-side artifact (OS preference resolved by `enableSystem`, or persisted localStorage from a previous toggle). PR1/PR2/PR3 must verify their mockups against `data-theme="light"` (clear localStorage + force-light in test harnesses); any production user landing on `/library` with a fresh session AND a light OS preference will see the cream theme by default.

---

## 2. Card placeholder grandi BB/TM/SI/7W — Code analysis

### Origin of the giant letters (fixture confirmation)

The 4 cards visible in the screenshot are NOT random. They are the first 4 entries of the `FIXTURE_DEFAULT` deterministic dataset used by the visual-regression test build, gated by `IS_VISUAL_TEST_BUILD = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'` (`apps/web/src/lib/library/visual-test-fixture.ts:50`). Mapping title → initials via `extractInitials()` (cover-utils.ts:156–195):

- `Catan` → `"C"` (but `Brass: Birmingham` → `"BB"`, present at index #5)
- `Terraforming Mars` → `"TM"` (index #1)
- `Spirit Island` → `"SI"` (index #7)
- `7 Wonders` is NOT in the fixture; the closest match is `"7W"` derived from a `7 Wonders` title. **None of the fixture entries map to `7W`**. The screenshot therefore either (a) was taken with a NON-fixture build hitting the real API where one game's title starts with "7 Wonders…", or (b) is showing a subset including `Brass: Birmingham (BB)`, `Terraforming Mars (TM)`, `Spirit Island (SI)`, and a fourth game we cannot identify from this fixture alone. The exact 4 letters are not crucial — what matters is the rendering path that produces them.

All 12 fixture entries have `gameImageUrl: null` (visual-test-fixture.ts:64) → `imageUrl` is undefined → `shouldUsePlaceholder()` returns true → the placeholder branch is taken for every card.

### MeepleCard primitive entry

**File**: `apps/web/src/components/ui/data-display/meeple-card/MeepleCard.tsx` (props barrel) and `index.ts`.
**Grid variant**: `apps/web/src/components/ui/data-display/meeple-card/variants/GridCard.tsx` (105 lines).
**Sibling variants**: `ListCard.tsx`, `CompactCard.tsx`, `FeaturedCard.tsx`, `HeroCard.tsx`, `FocusCard.tsx` (all under `variants/`).
**Relevant props for cover rendering** (`types.ts` — referenced by GridCard.tsx:18–37): `entity`, `id`, `title`, `subtitle`, `imageUrl`, `rating`/`ratingMax`, `metadata`, `tags`, `status`, `badge`, `actions`, `manaPips`, `showQuickActions`. **There is NO `coverEmoji` prop** in the primitive surface.

### Cover fallback logic

**File**: `apps/web/src/lib/games/cover-utils.ts` (not `lib/cover-utils.ts` — actual path is under `lib/games/`).
- `BLOCKED_IMAGE_HOSTS` (lines 21–25): `['cf.geekdo-images.com', 'geekdo-images.com', 'images.geekdo.com']`.
- `shouldUsePlaceholder(imageUrl)` (lines 38–55): returns true when `imageUrl` is null/undefined/empty, fails URL parsing, OR resolves to a blocked host. Data URLs and same-origin `/relative` paths bypass the check.
- `extractInitials(title)` (lines 156–195): splits on whitespace+punctuation, drops EN/IT stop words ("the", "il", "della", …), takes first letter of up to 3 significant tokens, uppercased. Falls back to `'?'` for empty input.
- `hashToHue(input)` (lines 81–83): DJB2 fold → `[0, 359]` deterministic hue per game id.

**Placeholder rendering** is performed by `apps/web/src/components/ui/data-display/meeple-card/parts/Cover.tsx`:
- Line 43: `usePlaceholder = hasImgError || shouldUsePlaceholder(imageUrl)`.
- Line 48: `showRichPlaceholder = usePlaceholder && entity === 'game' && !!gameId` — gates the **rich game-cover placeholder** ONLY for `entity === 'game'` AND when an `id` is passed. Library entries always pass `id={item.id}` (LibraryHybridGrid.tsx:122), so this branch fires for every game card.
- Lines 53–54: when `showRichPlaceholder` → renders `<GameCoverPlaceholder gameId={...} title={alt ?? ''} />`.
- Lines 55–62 (else): non-game entities fall back to `<div>` with `entityIcon[entity]` at `text-5xl opacity-50` — the legacy single-icon swatch.

`GameCoverPlaceholder.tsx` (lines 30–74) renders:
- A `<div>` with a `linear-gradient(135deg, hsl(${hue} 55% 32%), hsl(${secondaryHue} 65% 22%))` background driven by the hashed hue.
- A faint decorative meeple SVG silhouette at 12% opacity (lines 51–61).
- A centered `<span>` with the initials at `font-size: clamp(1.5rem, 14cqi, 4rem)` — **THIS is the giant-letter element**. The `14cqi` (container query inches) scales with the card width, producing the oversized "BB"/"TM"/"SI" visible in the screenshot.

### Gap vs mockup MeepleCardGrid (jsx:657–749)

| Element | Mockup spec | Current primitive | Gap |
|---|---|---|---|
| Container article | `bg-card border rounded-lg overflow-hidden flex flex-col` | GridCard.tsx:44–52: `rounded-2xl border bg-[var(--mc-bg-card)] flex flex-col overflow-hidden cursor-pointer` + glow outline + shadow + 350ms hover lift | partial — extra hover lift/glow/blur not in mockup; `rounded-2xl` vs mockup `var(--r-lg)` (likely smaller); semantically still a `<div role="button">` not `<article tabIndex={0}>` |
| Top accent bar 3px entity-colored | `position:absolute, top:0, left:0, right:0, height:3, background:entityHsl(ent), zIndex:2` (jsx:670–674) | AccentBorder.tsx:9–15: `absolute bottom-0 left-0 top-0 w-[3px] z-[5]` — **VERTICAL bar on the left edge**, not horizontal top bar | **missing/diverged** — primitive uses a left vertical accent stripe (legacy v2 motif), mockup wants a horizontal top stripe |
| Cover gradient + emoji 38px centered | `height:100, background:entity.cover, fontSize:38, drop-shadow, coverEmoji \|\| DS.EC[ent].em` (jsx:676–681) | Cover.tsx:50–72: aspect-`[7/10]` (NOT fixed 100px), either `<img>` OR placeholder branch (initials over hashed-hue gradient OR legacy icon at `text-5xl opacity-50`) — **NEVER renders an emoji glyph centered** | **missing** — mockup uses big emoji (🎲 / 🎯 / ⚙️ / 🤖) over per-entity gradient; primitive uses big game-initials over per-game hashed-hue gradient (for entity=game) or single small icon (other entities). No code path to emit the mockup's `entity.coverEmoji` |
| Entity badge top-left | pill `top-2 left-2 rounded-full bg-white/85 backdrop-blur-md` with `font-mono fontSize:8.5 fontWeight:800` + entity-colored emoji + UPPERCASE LABEL (jsx:683–693) | EntityBadge.tsx:17–28: `rounded-md px-2 py-0.5 text-[9px] uppercase tracking-wide text-white shadow-sm` + `background: entityHsl(entity)` (SOLID entity color, white text). NO emoji, NO `bg-white/85` glass effect, NO mono font, NO entity-colored text on white. Wrapped in GridCard.tsx:59–65 in a `flex flex-col gap-1` stack at `top-2 left-2.5 z-10` with optional StatusBadge below | **missing/diverged** — color scheme inverted (solid entity bg vs glass-white bg + entity text), shape `rounded-md` vs `rounded-full pill`, no emoji prefix, no mono font |
| 3-dot menu top-right (hover) | `top:8, right:8, 24×24, rounded-sm, bg-white/85, backdropBlur, '⋯', .mai-card-menu class (hover-visible)` (jsx:709–721) | **NOT IMPLEMENTED** — GridCard.tsx renders `<QuickActions actions={actions} />` only when `showQuickActions && actions.length > 0` (line 76), and the QuickActions slot is NOT a 3-dot overflow trigger (it's a row of icon buttons per `parts/QuickActions.tsx`). Library wrapper does not pass `actions` or `showQuickActions` (LibraryHybridGrid.tsx:119–128) | **missing** |
| Body p-3 title/subtitle | `p:12, flex-col gap-4`, h3 `font-display fontSize:13.5 fontWeight:800 line-clamp-2`, subtitle `fontSize:11 color:text-muted line-clamp-2` (jsx:724–734) | GridCard.tsx:78–94: `px-3.5 py-2.5 pb-2` (asymmetric padding ≠ mockup p-3) + gap-`[3px]`, h3 `font-[var(--font-quicksand)] text-[0.95rem] font-bold leading-tight` (no explicit line-clamp), subtitle `text-[0.78rem] leading-tight` (no line-clamp). Title has sibling top-right badge slot mockup doesn't have | partial — font sizes within ~0.5px tolerance but spacing different, **no line-clamp** on title or subtitle so long titles will overflow vs mockup spec |
| Footer status dot + badge | `flex items-center gap-5, padding 5px 0 0, borderTop 1px solid var(--border-light), marginTop:4` + `<StatusDot status={entity.status}/>` + uppercase mono badge (jsx:736–745) | **NOT IMPLEMENTED** — no footer with StatusDot in GridCard. There is a `<StatusBadge>` placed in the top-left badge-stack (GridCard.tsx:64) and a `<Rating>` + `<MetaChips>` block inside body (lines 95–96), but no bottom border-top footer with dot + uppercase mono status text | **missing** — entire footer slot absent |

### Diagnosis

**(c) Structural drift — confirmed.** The primitive markup diverges from the mockup at 4+ structural elements (top accent bar orientation, cover content paradigm initials-vs-emoji, entity badge styling glass-vs-solid, 3-dot menu missing, footer slot missing). Even if PR2 fixed the cover (option b) by piping `coverEmoji` from the wrapper, the cards would still not match because:
- (i) the top accent would stay vertical-left instead of horizontal-top,
- (ii) the entity badge would stay solid-color instead of glass-pill with mono entity-colored text,
- (iii) the 3-dot hover menu would still be absent,
- (iv) the footer status-dot row would still be absent.

The "giant letters" symptom is a direct consequence of `GameCoverPlaceholder` being the wired fallback for `entity === 'game' && !imageUrl`. The mockup never expected this fallback — it expected `coverEmoji` from data + `entity.cover` gradient (per-entity, NOT per-game-hashed-hue). So fixing only the wrapper to pass `coverEmoji` would help (gives the centered emoji glyph) but still requires a NEW code path in `Cover.tsx` to accept and render the emoji, since today `Cover` accepts only `imageUrl`. This is necessarily a primitive change — making this scope **(c) Structural drift** with elements of **(a)** (initials fallback exists where mockup wants emoji).

Marking only ONE box:

- [ ] (a) Initials fallback in primitive — true but downstream symptom of (c).
- [ ] (b) BGG blocked + missing emoji prop — false; the primitive does not accept `coverEmoji` at all.
- [x] (c) Structural drift — **confirmed**: primitive markup is structurally different from mockup at 4+ elements (top accent orientation, cover content, entity badge style, 3-dot menu, footer).
- [ ] (d) Mixed — would also be valid but (c) subsumes the relevant subset.

### Implication for PR2 scope

**PR2 MUST change the MeepleCard primitive** because at least 4 structural elements (top accent bar orientation, cover content paradigm, entity-badge style, footer status-dot row) cannot be patched from the wrapper. The cover-emoji injection ALONE would also require a new `coverEmoji?: string` prop on `MeepleCardProps` + a new render branch inside `Cover.tsx` between lines 52–62.

**Cross-cutting impact**: a grep for the primitive surface yields **72 non-test consumer files** (excluding `__tests__/`, `/showcase/`, `/dev/` paths). Representative subset:
`MeepleAgentCard.tsx`, `MeepleChatCard.tsx`, `MeepleKbCard.tsx`, `MeepleGameCatalogCard.tsx`, `MeepleSessionCard.tsx`, `MeeplePlayerCard.tsx`, `MeepleUserLibraryCard.tsx`, `MeepleGameNightCard.tsx`, `MeepleEventCard.tsx`, `GameCarousel.tsx`, `GameDetailDesktop.tsx`, `HomeFeed.tsx`, `CollectionGameGrid.tsx`, `AgentsResultsGrid.tsx`, `PlayersResultsGrid.tsx`, `GamesResultsGrid.tsx`, plus the admin surfaces (`game-catalog-grid.tsx`, `vector-game-card.tsx`, `EntityCardsScene.tsx`) and dashboard zones.

Any structural change to GridCard ripples to all 72 consumers — including admin and showcase. Per CLAUDE.md cluster pattern P164 (axe-rule suppression with tracked follow-up) and the design-system de-versioning policy, structural primitive changes of this scale should NOT be inlined inside a feature PR.

### Follow-up issue recommendation

**STOP — recommend a dedicated follow-up issue for the primitive reskin.** Justification:

- Cross-cutting impact: 72 non-test consumers across library, agents, players, games, sessions, dashboard, admin, catalog, chat, kb, game-night, collection.
- Risk of visual regressions on surfaces unrelated to `/library` (especially admin scenes and the dashboard EntityZone).
- The follow-up needs an own a11y audit (axe heading-order interaction with `<article>` vs `<div role="button">` semantics — see P164 precedent in MEMORY).
- Possible interaction with #1842 MeepleCard heading-level prop (P164 follow-up): both PRs touch the same primitive surface and should be sequenced.

**Proposed split for PR2**:
- PR2 (wrapper-only quick wins): in `LibraryHybridGrid.tsx`, derive `coverEmoji` from item type if/when the primitive eventually accepts it; meanwhile, ensure correct `entity`, `id`, `title`, `subtitle` propagation (already correct per LibraryHybridGrid.tsx:119–128). Status: minimal change because the wrapper already does the right thing — there is nothing the wrapper can do alone to fix the mockup gap.
- **NEW follow-up issue** `feat(meeple-card): SP4 MeepleCardGrid mockup conformance reskin` — implements: (1) horizontal top accent bar, (2) `coverEmoji?: string` prop + render branch in Cover.tsx, (3) glass-pill EntityBadge variant with mono entity-colored text, (4) hover-visible 3-dot menu slot, (5) footer status-dot + uppercase mono badge row, (6) `line-clamp-2` on title and subtitle in GridCard, (7) regression audit of all 72 consumers (snapshots / visual review). Block PR2 until this follow-up lands OR scope PR2 down to mockup gaps NOT requiring primitive change (e.g., LibraryHeroDesktop, LibraryTabs, CrossEntityFilters, which are non-MeepleCard surfaces).

---

## Phase 0 Decision (Task 0.3 — Controller)

**Date**: 2026-06-03

### Theme
No code change required. Confirmed: default `light` theme is correctly bound; baseline screenshot dark is user preference (OS dark mode picked up via `enableSystem`). PRs 1/2/3 will be authored assuming `data-theme="light"` runtime.

### MeepleCard primitive
**Decision**: Task 2.2 (MeepleCard primitive reskin) is **DEFERRED** from PR2 scope and tracked as a separate follow-up issue. Rationale:
- 72 non-test consumer surfaces consume the primitive → cross-cutting impact requires dedicated review
- 5 structural gaps (accent bar direction, cover content, badge style, 3-dot menu, footer) span multiple files (`MeepleCard.tsx`, `variants/GridCard.tsx`, `parts/Cover.tsx`, `parts/AccentBorder.tsx`, `parts/EntityBadge.tsx`, `parts/GameCoverPlaceholder.tsx`)
- Wrapper-only fix not viable: primitive has no `coverEmoji` prop
- Plan-anticipated escalation per Task 2.2 step 3

**Follow-up issue**: https://github.com/meepleAi-app/meepleai-monorepo/issues/1856

### PR2 scope (revised)
PR2 will execute Task 2.1 (branch hygiene) + Task 2.3 (LibraryHybridGrid layout) + Task 2.4 (RecentActivityRail) + Task 2.5 (EmptyLibrary) + Task 2.6 (verify+open). Cards in /library will continue to render with current placeholder pattern until follow-up issue lands.

**Visual conformity scope statement**: /library cards visual conformity is BLOCKED by the follow-up issue. PR2 alone will NOT fully resolve the mockup gap for the grid area; it only aligns layout containers + non-card surface (rail + empty).

---
