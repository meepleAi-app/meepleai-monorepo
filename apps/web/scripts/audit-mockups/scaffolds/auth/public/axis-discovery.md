# public — Axis Discovery

**Source HTML**: `admin-mockups/design_files/public.html`
**JSX twin**: `admin-mockups/design_files/public.jsx`
**Phase B classification**: `design_intent: current` · no `pair_disagreement`
**Mockup canonical**: HTML (per MOCKUPS_INDEX pairing rule)

## Mockup stage layout

The mockup renders a single full-page demo (no `.phones-grid`) — visitors
navigate between 4 client-side "pages" via `PublicNav` (jsx:7-58) with
`page` state owned by root `PublicApp` (jsx:705-718):
- `landing` — Hero + Features + How-it-works + Pricing teaser
- `pricing` — 3-tier card grid + comparison matrix
- `about` — Team + mission
- `contact` — Form + footer

In real Next.js routing each is a separate route under `/(public)/`. The
mockup matrix view collapses them into one client-side switcher.

## Axis (canonical)

| Axis | Type | Values | Source | Notes |
|------|------|--------|--------|-------|
| `page` | enum | `landing` \| `pricing` \| `about` \| `contact` | `links` array (public.jsx:9-14), `page === 'landing'` switch (jsx:715-717) | Mockup-only client-side nav; codebase uses Next.js routes |
| `state` | enum | `default` \| `mobile-drawer-open` | `menuOpen` state (public.jsx:8, 31) | Hamburger menu visibility |

## Frame matrix (Desktop only Phase C-1, Mobile drawer = 1 frame for Phase 4 plan)

| Frame | Mockup page | Codebase route | Canonical content |
|-------|-------------|----------------|-------------------|
| 01 | landing | `/` | LandingPage (page.tsx) composing 5 sections |
| 02 | pricing | `/pricing` | PricingPage (separate route) |
| 03 | about | `/about` | AboutPage (separate route) |
| 04 | contact | `/contact` | ContactPage (separate route) |
| 05 | landing + mobile drawer | `/` w/ viewport=mobile1 | LandingPage + responsive header (Mobile deferred Phase 4) |

## Component mapping (route ↔ canonical)

| Route | Real component | File |
|-------|----------------|------|
| `/` | `LandingPage` (Server Component) | `apps/web/src/app/(public)/page.tsx` |
| `/pricing` | (separate route) | `apps/web/src/app/(public)/pricing/page.tsx` |
| `/about` | (separate route) | `apps/web/src/app/(public)/about/page.tsx` |
| `/contact` | (separate route) | `apps/web/src/app/(public)/contact/page.tsx` |

## Canonical component pick

**Picked**: `apps/web/src/app/(public)/page.tsx` (`LandingPage`, default export)

**Why**:
1. Production component, already mockup-annotated (page.tsx:1-9 has the
   `@mockup admin-mockups/design_files/public.html` JSDoc).
2. Server Component composes 5 marketing sections — single render renders
   the entire mockup landing view.
3. Existing story `page.stories.tsx` (Pages/LandingPage) covers
   Chromatic Default + MobileFlow + TabletFlow viewports.
4. **This scaffold adds mockup-matrix semantic** (`Pages/Auth/Public Landing`
   title) — distinct from the existing pure visual regression story.

## Mockup ↔ codebase divergences

| # | Divergence | Resolution |
|---|------------|------------|
| 1 | Mockup `currentPage` client-side switch; codebase uses Next.js multi-route. | Storybook frame matrix collapses for designer review; real routes are separate stories. |
| 2 | Mockup CTAs use `<a href="auth-flow.html">` (demo nav); codebase uses `<Link href="/login">` / `/register`. | Routing parity intentional. |
| 3 | Mockup pricing inside switcher; codebase route-owned. | Same as #1. |
| 4 | Mockup has BGG ToS reference in pricing matrix? **Check**. | If BGG appears in pricing copy → flag for legal review (#1903 ADR). |
| 5 | Mockup `LandingPage` (jsx:194-204) renders Hero + Features only on `landing` page. Codebase composes 5 sections (page.tsx:153-157): WelcomeHero + HowItWorksSteps + RulesQuickDemo + SocialProofBar + WelcomeCTA. | Codebase has richer composition. Mockup view is documentation-only. |

## Server-side redirect handling

CRITICAL: `LandingPage` is a Server Component (page.tsx:133) that calls
`await getServerUser()` and `redirect('/library')` if authenticated
(line 135-137). For Storybook rendering:
- MSW must return 401 on `/api/v1/auth/me` → `getServerUser()` returns null.
- Storybook 10.4 may not fully resolve Server Component flow — verify in
  Phase 2 iteration. Fallback: convert to a wrapped Client Component
  scaffold if SSR fails.

## JSX evidence (line refs)

- `PublicNav` with 4-link nav: `public.jsx:7-58`
- `links` array: `public.jsx:10-14`
- `HeroSection` (landing): `public.jsx:61-104`
- `FeatureGrid`: `public.jsx:108-150`
- `LandingPage` composition: `public.jsx:194-204`
- `PricingCard`: `public.jsx:207-238`
- `PricingMatrix`: `public.jsx:244-294`
- `PricingPage`: `public.jsx:298-321`
- Mobile drawer (`mob-drawer-inner`): `public.jsx:43-56`
- `currentPage` client switch in `PublicApp`: `public.jsx:705-718`
