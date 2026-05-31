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

> **Pairing rule**: most `*.html` files have a `*.jsx` twin (e.g. `sp3-join.html` ↔ `sp3-join.jsx`).
> The two are equivalent: HTML for browser preview, JSX for codebase clone. The index lists the
> HTML file as canonical when both exist.

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
| `auth-flow.html` | page-mock | `/login`, `/register`, `/reset-password`, `/oauth-callback`, `/verify-email`, `/verification-pending`, `/verification-success`, `/invitation-expired` |
| `onboarding.html` | page-mock | `/welcome`, `/onboarding`, `/setup`, `/setup-account` |
| `notifications.html` | page-mock | `/notifications`, `/notifications/preferences` |
| `public.html` | page-mock | `/` (landing) |
| `settings.html` | page-mock | `/settings` + 7 sub-route (`/ai-consent`, `/api-keys`, `/notifications`, `/preferences`, `/profile`, `/security`, `/services`) |

## SP3 — Public surfaces & invitations

| File | Type | Mapped routes |
|------|------|---------------|
| `sp3-accept-invite.html` | page-mock | `/accept-invite`, `/invites/[token]` |
| `sp3-faq-enhanced.html` | page-mock | `/faq`, `/games/[id]/faqs` (reuse) |
| `sp3-how-it-works.html` | page-mock | `/how-it-works` |
| `sp3-join.html` | page-mock | `/join`, `/sessions/join` (reuse) |
| `sp3-legal.html` | page-mock | `/privacy`, `/terms`, `/cookies`, `/cookie-settings` |
| `sp3-library-public.html` | page-mock | `/shared-games` (variant), `/library/shared/[token]` |
| `sp3-shared-game-detail.html` | page-mock | `/shared-games/[id]` (Wave A.3, PR #600/605/612/630) |
| `sp3-shared-games.html` | page-mock | `/shared-games` |

## SP4 — Authenticated core (Wave 1+2+3+4)

| File | Type | Mapped routes |
|------|------|---------------|
| `sp4-add-game-bgg-step.html` | page-mock | `/library/proposals`, `/library/propose` |
| `sp4-add-game-pdf-dedup.html` | page-mock | `/library/private/add`, `/upload` (partial) |
| `sp4-agent-detail.html` | page-mock | `/agents/[id]`, `/library/[gameId]/agent` |
| `sp4-agents-index.html` | page-mock | `/agents`, `/editor/agent-proposals/*` (partial), `/chat/agents/create` (partial) |
| `sp4-citation-pdf-viewer.html` | component-mock | Citation overlay used by `/chat/[threadId]` and game-chat tabs |
| `sp4-dashboard.html` | page-mock | `/dashboard` (forward-design Pre-Stage-3, closes #491) |
| `sp4-discover.html` | page-mock | `/discover` |
| `sp4-game-chat-tab.html` | component-mock | Chat tab embedded in `/library/[gameId]/agent`, `/games/[id]` |
| `sp4-game-detail.html` | page-mock | `/games/[id]`, `/library/[gameId]`, `/private-games/[id]` |
| `sp4-game-nights-index.html` | page-mock | `/game-nights` |
| `sp4-games-index.html` | page-mock | `/games` |
| `sp4-kb-detail.html` | page-mock | `/knowledge-base/[id]` (deferred — G4 v3 pivot) |
| `sp4-kb-hub.html` | page-mock | `/knowledge-base` |
| `sp4-library-desktop.html` | page-mock | `/library` (Wave B.3 done) |
| `sp4-library-mobile.html` | page-mock | `/library` (mobile <768px variant, SP8 brief 2026-05-30, IA semplificata 3 tab + overflow) |
| `sp4-play-records-detail.html` | page-mock | `/play-records/[id]` |
| `sp4-play-records-edit.html` | page-mock | `/play-records/[id]/edit` |
| `sp4-play-records-index.html` | page-mock | `/play-records` |
| `sp4-play-records-new.html` | page-mock | `/play-records/new` |
| `sp4-play-records-stats.html` | page-mock | `/play-records/stats` |
| `sp4-player-detail.html` | page-mock | `/players/[id]`, `/players/[id]/{achievements,games,sessions,stats}` |
| `sp4-players-index.html` | page-mock | `/players` |
| `sp4-session-live-parts.jsx` | component-mock | Sub-components of `/sessions/[id]/live` Foundation sub-PR |
| `sp4-session-live.html` | page-mock | `/sessions/[id]/live`, `/sessions/live/[sessionId]/*` |
| `sp4-session-summary-parts.jsx` | component-mock | Sub-components of `/sessions/[id]` Wave D.3 |
| `sp4-session-summary.html` | page-mock | `/sessions/[id]` |
| `sp4-sessions-index.html` | page-mock | `/sessions`, `/games/[id]/sessions` (reuse) |
| `sp4-toolkit-detail.html` | page-mock | `/toolkit` + sub-routes, `/library/[gameId]/toolbox`, `/library/[gameId]/toolkit`, `/library/private/[id]/toolkit/configure` |
| `sp4-upload-wizard-extended.html` | page-mock | `/upload`, `/gamebook/upload` (partial) |

## SP5 — Admin & Profile settings

| File | Type | Mapped routes |
|------|------|---------------|
| `sp5-profile-settings.html` | page-mock | `/profile?tab=settings`, `/profile?tab=settings&section=<security\|notifications\|preferences\|profile\|api-keys\|services>` (issue #1608, sblocca SP5 S3 cutover) |
| `sp5-profile-settings.jsx` | component-mock | 8 sub-components per `/profile?tab=settings` consolidation: `ProfileTabBar`, `SettingsTab`, `SettingsSubNav`, `TwoFactorStatusCard`, `TwoFactorSetupModal`, `OTPInput6Slot`, `BackupCodesGrid`, `TwoFactorBottomSheet` |

## SP6 — Libro-game (Nanolith dogfood Iter 1+4)

| File | Type | Mapped routes |
|------|------|---------------|
| `sp6-libro-game-glossary-editor.jsx` | component-mock | Glossary editor overlay inside `/library/[gameId]/play/[campaignId]/translate` |
| `sp6-libro-game-index.html` | page-mock | `/gamebook`, `/library/[gameId]/play/[campaignId]` (libro variant) |
| `sp6-libro-game-photo-upload.html` | page-mock | `/library/[gameId]/play/[campaignId]/translate` (camera step) |
| `sp6-libro-game-play-session.jsx` | component-mock | Play-session view embedded in libro-game runthrough |
| `sp6-libro-game-quota-credits.jsx` | component-mock | Quota/credits widget (global modal, not page-level) |
| `sp6-libro-game-resume-state.html` | page-mock | `/library/[gameId]/play` (resume picker variant) |
| `sp6-libro-game-translation-viewer.jsx` | component-mock | Translation viewer inside `/library/[gameId]/play/[campaignId]/translate` |

## SP7 — Game nights

| File | Type | Mapped routes |
|------|------|---------------|
| `sp7-game-night-create.html` | page-mock | `/game-nights/new` |
| `sp7-game-night-detail-rsvp.html` | page-mock | `/game-nights/[id]`, `/game-nights/[id]/edit` |
| `sp7-game-night-live.html` | page-mock | `/game-nights/[id]/live` (issue #487 screen #4+#7) |
| `sp7-game-night-transition.html` | component-mock | Modal opened from `/game-nights/[id]/live` (issue #487 screen #5) |
| `sp7-game-night-summary.html` | page-mock | `/game-nights/[id]/summary` (issue #487 screen #6) |

## Chat

| File | Type | Mapped routes |
|------|------|---------------|
| `chat-fullscreen.html` | page-mock | `/chat/[threadId]`, `/chat/new` (empty state) |

## Nanolith — Runthrough storyboard (Aaron Iter 1)

| File | Type | Mapped routes |
|------|------|---------------|
| `nanolith-game-night-storyboard.html` | page-mock | `/game-nights/[id]` (storyboard variant) |
| `nanolith-nav-bottom-mobile.html` | component-mock | Mobile bottom-nav primitive (global) |
| `nanolith-nav-chat-panel.html` | component-mock | Chat slide-over panel (used globally via `useChatPanel`) |
| `nanolith-nav-topbar.html` | component-mock | Top-bar primitive (global) |
| `librogame-runthrough-encounter-cheatsheet.html` | page-mock | `/library/[gameId]/play/[campaignId]/encounter` (gap-coverage 2026-05-12, PR #1056) |
| `librogame-runthrough-error-states.html` | component-mock | Trasversale: chat (N1/N2) · translate (N3) · encounter — stream-timeout / OCR-fail / LLM-503 / segmentation-fail (PR #1056) |
| `librogame-runthrough-game-detail.html` | page-mock | `/library/[gameId]` (libro variant, PR #1037) |
| `librogame-runthrough-game-onboarding.html` | page-mock | `/library/[gameId]` (libro variant — prereq gate, gap-coverage 2026-05-12, PR #1056) |
| `librogame-runthrough-glossary-editor.html` | component-mock | Glossary editor (mirror of sp6 jsx) |
| `librogame-runthrough-library-search.html` | component-mock | In-library search overlay (not page-level) |
| `librogame-runthrough-play-session.html` | page-mock | `/library/[gameId]/play/[campaignId]` (4 stati v1 congelati + 3 stati SP8 companion: state-05 diary, state-06 paragrafi-drawer, state-07 end-campaign, brief 2026-05-30; jsx twin nuovo con 3 lab interattivi) |
| `librogame-runthrough-quota-credits.html` | component-mock | Quota/credits overlay (global) |
| `librogame-runthrough-resume-picker.html` | page-mock | `/library/[gameId]/play` |
| `librogame-runthrough-session-end.html` | page-mock | `/sessions/live/[sessionId]` (end-state) |
| `librogame-runthrough-setup-chat.html` | page-mock | `/chat/new`, `/chat/[threadId]` (setup variant) |
| `librogame-runthrough-setup-wizard.html` | page-mock | `/sessions/new`, `/library/[gameId]` campaign-setup drawer (PR #1037) |
| `librogame-runthrough-translate-viewer.html` | page-mock | `/library/[gameId]/play/[campaignId]/translate` |

## Summary

| Type | Count |
|------|------:|
| page-mock | 55 |
| component-mock | 16 |
| dev-fixture | 12 |
| **Total** | **83** |

> The `*.jsx` twins of `*.html` files are not double-counted (the JSX is the
> implementation companion of the HTML reference). Listing them separately
> would inflate the count to ~110 without adding signal.

## Gaps (routes without a mockup)

See [`docs/for-developers/audits/2026-05-12-mockup-gaps.md`](../docs/for-developers/audits/2026-05-12-mockup-gaps.md)
for the audit of 5 user-reachable routes lacking mockup coverage as of 2026-05-12.
