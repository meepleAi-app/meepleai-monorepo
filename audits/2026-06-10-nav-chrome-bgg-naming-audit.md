# Nav-chrome + BGG + Naming Consistency Audit — DS-17 Phase B addendum #2

**Date**: 2026-06-10
**Method**: `/sc:spec-panel` critique with 5 experts (Fowler lead · Newman · Wiegers · Cockburn · Adzic)
**Inputs**: 224 mockup files + 130 codebase routes + 47 BGG mentions + 6 nav primitives
**Companion**: `audits/2026-06-10-mockup-coverage-gap-report.md` (addendum #1)

This is the SECOND spec-panel addendum to the Phase B audit (sub-issue #2127). Addendum #1 covered US-coverage gaps; this addendum covers STRUCTURAL consistency:
1. Nav-chrome (navbar/sidebar/drawer) usage across mockups vs codebase
2. BGG rule enforcement (per #1903 ADR)
3. Mockup file naming + route naming conventions

---

## Quantitative summary

| Domain | Metric | Value |
|---|---|---|
| Nav-chrome | Page-mocks reusing canonical nav primitives | **0 of ~80** (total drift) |
| Nav-chrome | Mockups MISSING chrome despite being primary nav destinations | 2 (sp4-players-index, sp4-sessions-index) |
| BGG | Total mentions (mockup + codebase + docs) | 47 |
| BGG | Forbidden (user-side BGG access) | **12** (7 mockups beyond Phase B + 2 codebase + 3 doc rot) |
| BGG | Ambiguous (still labels "BGG" to user) | 8 |
| BGG | Compliant (admin-gated or informational) | 27 |
| Naming | Mockup→route name mismatches | 15+ |
| Naming | Dynamic param variants (codebase) | 10 (should be 2-3) |
| Naming | Route namespace duplications | 4 |

---

## Section A — Nav-chrome consistency (Fowler)

### A.1 CRITICAL: Canonical nav primitives never consumed (3-way drift)

| Layer | Canonical Pattern | Actual State |
|---|---|---|
| Nav primitives (`nanolith-nav-*.html`) | Document search-pill ⌘K, LiveSessionPill (D5), dynamic-slot-3 (D6), chat slide-over (D7) | Self-documenting only |
| Page-mocks (`sp4/sp3/sp7-*.jsx`) | Should reuse primitives | Each re-implements inline `DesktopAuthNav` / `PhoneTopNav` / `MobileBottomBar` |
| Runtime (`AppTopBar.tsx`) | Should match primitives | Lacks search-pill, LiveSessionPill, dynamic-slot-3 |

**Resolution path**: tracked in **Draft 14**.

### A.2 HIGH: 2 primary destinations omit chrome entirely

- `sp4-players-index.jsx` — in `TOP_BAR_NAV_IDS` but NO nav-chrome
- `sp4-sessions-index.jsx` — in `TOP_BAR_NAV_IDS` but NO nav-chrome

Other index mockups (`sp4-dashboard`, `sp4-games-index`, `sp4-agents-index`, `sp4-hub-*`) DO render 5-voice nav.

### A.3 HIGH: Desktop topbar split (detail vs hub) contradicts runtime

Detail mockups (`sp4-game-detail`, `sp4-toolkit-detail`, `sp4-discover`, `sp4-game-nights-index`, `sp4-player-detail`, `sp4-agent-detail`, `sp4-kb-detail`) drop the 5-voice row. Runtime AppTopBar always shows 5 voices → detail mockups are LIVE-divergent.

### A.4 MEDIUM: SideDrawer + 8-voice sidebar invisible in mockup pipeline

`MAIN_NAV_ITEMS` (8 voices Asse B #1897) lives only in mobile SideDrawer (per #1977/F18). **No mockup documents the OPEN drawer contents.**

### A.5 LOW: Search-pill + LiveSessionPill + dynamic-slot-3 absent from runtime

Documented in `nanolith-nav-topbar.html` (decisions D5/D6) but NOT shipped in `AppTopBar.tsx`. Either deprecate primitives OR backfill.

---

## Section B — BGG rule violations (Newman + Wiegers)

### B.1 CRITICAL: 7 mockups carry forbidden user-side BGG surfaces (beyond Phase B flag)

| Mockup | Line(s) | Forbidden surface | Phase B status |
|---|---|---|---|
| `sp4-upload-wizard-extended.{html,jsx}` | 4, 102-106, 938 | Step 0 source picker: "Da BoardGameGeek" card → links to forbidden mockup | NOT flagged |
| `sp4-library-desktop.jsx` | 118, 1051, 1064, 1448, 1524 | Hero "↓ Importa BGG" CTA + empty-state CTA links to forbidden mockup | NOT flagged |
| `sp4-game-chat-tab.html` | 27, 564-569, 622-629 | Low-confidence chat fallback: "BGG forum thread" citation + "🔗 BGG · ricerca esterna" chip | NOT flagged |
| `sp5-profile-settings.{html,jsx}` | 1681 + 88 | "Connected services" panel exposes BGG as OAuth target | NOT flagged |
| `sp3-how-it-works.jsx` | 2549-2550 | Onboarding card: "Connetti BGG — Sincronizza la tua collezione BoardGameGeek — OAuth · 30s" | NOT flagged |
| `settings.jsx` | 244, 738-740, 747, 829 | BggIcon SVG + Bio "BGG rank: 1.492" + Connected services BGG entry | NOT flagged |
| `sp7-game-night-live.jsx` | 561 | Add-game CTA: `window.location.href = 'sp4-add-game-bgg-step.html'` | NOT flagged |

### B.2 CRITICAL: 2 codebase files carry user-facing BGG surfaces (no admin gate)

| File | Line | Issue |
|---|---|---|
| `apps/web/src/components/dashboard/QuickActionCards.tsx` | 63 | "Cerca nel catalogo BGG" displayed to ALL users (no admin gate). Component appears orphan but is in bundle. |
| `apps/web/src/components/features/settings/settings-sections.ts` | 77 | User settings sidebar item subtitle still says "BGG, Discord" (Phase 1 spec flagged, not addressed) |

### B.3 AMBIGUOUS: 8 surfaces still use "BGG" label (Phase 2 spec required rename to "Community")

- `apps/web/src/locales/{it,en}.json` — `pages.gameDetail.info.specsRatingBgg` ("Rating BGG" / "BGG Rating")
- `apps/web/src/components/features/game-detail/buildSpecsItems.ts` — wires `specsRatingBgg` to `/games/[id]`
- `apps/web/src/components/features/gamebook/LibroGameDetailView.tsx:94,113` — `BGG #${bggId}` + `<MetaStat value={rating} label="BGG" />`
- `apps/web/src/lib/api/clients/gameNightBggClient.ts` — dormant client (no callers) but registered in `lib/api/index.ts:325,466`
- `sp4-library-wishlist-ui.jsx:39,46` — wishlist "BGG" rating chip
- `sp4-game-detail.jsx:390` — "Rating BGG" specs row
- `sp3-shared-game-detail.jsx:383,471` — hero stats "BGG" label
- `librogame-runthrough-game-detail.html:323,383,471` + `librogame-game-night-storyboard.html:992`

### B.4 RECOMMENDATION: Add `pnpm lint:bgg-mockups` with whitelist-incremental gate

**Resolution path**: tracked in **Draft 15**.

---

## Section C — Naming inconsistencies (Adzic + Cockburn)

### C.1 CRITICAL: `sp4-*` prefix overloaded (dual semantic)

`sp4-*` mockups split into two unrelated meanings:
- Authenticated core (~30 files)
- Session game-specific demos (~40 files: catan/codenames/paleo/power-grid/puerto-rico/wingspan/zombicide/skeleton variants)

Same prefix conveys two unrelated concepts.

### C.2 CRITICAL: Twin family confusion `librogame-runthrough-*` vs `sp6-libro-game-*`

Two parallel families for Aaron-persona libro-game domain. `#2025` cleanup deleted 3 of the sp6 twins; 4 still survive:
- `sp6-libro-game-resume-state.{html,jsx}` ↔ `librogame-runthrough-resume-picker.html` (documented duplicate)
- `sp6-libro-game-index.{html,jsx}` — survives without librogame twin
- `sp6-libro-game-photo-upload.{html,jsx}` — survives without librogame twin

### C.3 HIGH: CRUD verb inconsistency (`-create` vs `-new`)

| Mockup | Route | Issue |
|---|---|---|
| `sp7-game-night-create.html` | `/game-nights/new` | mismatch |
| `sp4-editor-proposals-create.html` | `/editor/agent-proposals/create` | mockup OK with route, but inconsistent with rest |
| `sp4-play-records-new.html` | `/play-records/new` | aligned ✓ |

App-wide convention split. Routes prefer `/new` (Next.js convention).

### C.4 HIGH: Dynamic param sprawl

10 distinct variants where 2-3 should suffice:
- `[id]` 43 uses — canonical ✓
- `[gameId]`, `[sessionId]`, `[campaignId]`, `[privateGameId]` — disambiguators (OK when nesting, redundant otherwise)
- **`[token]` (2) vs `[code]` (2) vs `[inviteToken]` (1)** — 3 names for 2 conceptual entities

Recommended canonical:
- `[token]` for opaque secrets
- `[code]` for user-readable short codes
- Delete `[inviteToken]` (rename `/join/[inviteToken]` → `/join/[token]`)
- Audit `[privateGameId]` → consider collapse to `[id]`

### C.5 HIGH: Route namespace duplications

| Namespace A | Namespace B | Recommendation |
|---|---|---|
| `/sessions/[id]/{live,notes,scoreboard,join,play,players}` (8 routes) | `/sessions/live/[sessionId]/{agent,photos,players,scores}` (5 routes) | Consolidate to A; deprecate B |
| `/toolkit/*` (6 singular) | `/toolkits/[id]` (2 plural) | Pick plural (Next.js entity-list convention) |
| `/hub/{games,agents,toolkits}` (4) | `/{games,agents,toolkits}` indexes | Already tracked in Draft 13 |
| `/settings/*` (7 sub-routes) | `/profile?tab=settings&section=*` (6 sub) | Pick one (Phase B already flagged) |

### C.6 MEDIUM: Language drift inside filename

- `sp4-kb-globale.html` (Italian "globale") → `/knowledge-base/global` (English route)

### C.7 MEDIUM: `nanolith-` prefix doesn't telegraph "primitive"

3 files (`nanolith-nav-{topbar,bottom-mobile,chat-panel}.html`) function as global nav primitives but the prefix reads like a brand name.

### C.8 MEDIUM: Subcomponent suffix sprawl (13 distinct suffixes, no documented vocabulary)

`-summary`, `-live`, `-parts`, `-data`, `-flavor`, `-renderers`, `-tabs`, `-ui`, `-stats`, `-bodies`, `-sections`, `-tools`, `-dice`

### C.9 LOW: Single-file prefix `chat-`

Only `chat-fullscreen.html` uses this prefix. Should be `sp4-chat-fullscreen.html` for consistency.

---

## Tracking issues drafted (post designer sign-off)

New drafts appended to `audits/tracking-issues-drafts.md`:

- **Draft 14** (Fowler — architecture): Nav-chrome 3-way drift — primitives never consumed by page-mocks or runtime
- **Draft 15** (Newman+Wiegers — BGG enforcement): Add `pnpm lint:bgg-mockups` whitelist-incremental gate + flag 7 new mockup violations + 2 codebase findings
- **Draft 16** (Adzic — naming consistency): Standardize CRUD verbs + dynamic params + complete #2025 cleanup + suffix vocabulary doc

---

## Quality scores

| Score | Value | Notes |
|---|---|---|
| Nav-chrome consistency | 3.5/10 | 3-way drift, 2 mockups missing, 7 detail mocks diverge from runtime |
| BGG ToS compliance | 6.5/10 | Phase B flagged 1 mockup; 7+ violations remain undetected |
| Naming consistency | 5.0/10 | Critical prefix overload + twin families + verb split + param sprawl |
| Route architecture | 6.0/10 | 4 namespace duplications surface in mockup mappings |
| **Overall structural consistency** | **5.5/10** | foundational drift between primitives/mockups/runtime |

---

## Refs

- Phase B audit: `audits/2026-06-10-mockup-design-intent-audit.json`
- Addendum #1 (coverage gaps): `audits/2026-06-10-mockup-coverage-gap-report.md`
- Sub-issue: #2127
- Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
- BGG ADR: #1903
- Stage 3 closure (hub/* retirement): #1026
- Asse B nav primitives: #1897, #1977 (MainSidebar removal)
- Asse D P2 (games multi-tab): #1899
