/**
 * @mockup admin-mockups/design_files/public.html
 *
 * Public Landing argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Single-route mockup covering 1 route:
 *   `/` (landing page, public)
 *
 * Stage axis del mockup (4 page-state frames embedded in stage, Desktop only
 * Phase C-1; Mobile DEFERRED a Phase 4):
 *   page: 'landing' | 'pricing' | 'about' | 'contact'
 *   state: 'default' | 'mobile-drawer-open'
 *
 * CANONICAL COMPONENT PICK (single-route, DS-17 Phase C-1 spec § 6):
 *   We render `LandingPage` (default export from
 *   apps/web/src/app/(public)/page.tsx). It's a Server Component composing
 *   `WelcomeHero` + `HowItWorksSteps` + `RulesQuickDemo` + `SocialProofBar`
 *   + `WelcomeCTA` from `@/components/landing`.
 *
 * NOTE: `LandingPage` does `redirect('/library')` if user is authenticated
 * (server-side). Storybook MSW handlers must respond to `getServerUser()`
 * with null (anonymous) to avoid redirect. We mock `/api/v1/auth/me` 401.
 *
 * An existing story `apps/web/src/app/(public)/page.stories.tsx` already
 * covers Default + MobileFlow + TabletFlow viewports for Chromatic. This
 * scaffold provides the **mockup matrix** semantic (frame-per-section
 * documentation) per DS-17 Phase C-1 pattern.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import LandingPage from '@/app/(public)/page';

import { mswForState } from '@/__tests__/fixtures/mockup-pilots/auth/public';

import type { Meta, StoryObj } from '@storybook/react';

type PublicPage = 'landing' | 'pricing' | 'about' | 'contact';
type PublicState = 'default' | 'mobile-drawer-open';

const meta: Meta<typeof LandingPage> = {
  title: 'Pages/Auth/Public Landing',
  component: LandingPage,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di public.jsx LandingPage sections + PricingPage subroute (Desktop only Phase C-1; Mobile + drawer deferred Phase 4). Mockup nav state lives inside `PublicNav`; routing handled by Next.js (`/`, `/pricing`, `/about`, `/contact`). Real LandingPage server-renders 5 sections.',
      },
    },
  },
  argTypes: {
    // LandingPage is a server component with no props.
  },
  args: {},
};
export default meta;

type Story = StoryObj<typeof LandingPage>;

// ── Stage frame canonicals (mapped 1:1 ai 4 page sections del mockup) ─────
// Mobile drawer frame DEFERRED to Phase 4 (viewport sweep, Mobile opt-in).
// Pricing/About/Contact are separate routes — frames document the mockup
// stage but the rendered hero is /landing.

export const Frame01_LandingHero: Story = {
  name: '01 · Landing — hero + features + how-it-works',
  parameters: { msw: { handlers: mswForState('default') } },
};

export const Frame02_PricingPage: Story = {
  name: '02 · Pricing — 3-tier card grid + comparison matrix',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /pricing is a separate route (`apps/web/src/app/(public)/pricing/page.tsx`). Real component covered by dedicated story; this frame preserves mockup matrix view for designer cross-reference.',
      },
    },
  },
};

export const Frame03_AboutPage: Story = {
  name: '03 · About — team + mission',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /about is a separate route. Mockup matrix preserves view; real component is route-owned.',
      },
    },
  },
};

export const Frame04_ContactPage: Story = {
  name: '04 · Contact — form + footer',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /contact is a separate route. Mockup matrix preserves view; real component is route-owned.',
      },
    },
  },
};

// ── State variants ─────────────────────────────────────────────────────────

export const Frame05_MobileDrawerOpen: Story = {
  name: '05 · Mobile · drawer aperto (hamburger nav)',
  parameters: {
    msw: { handlers: mswForState('default') },
    viewport: { defaultViewport: 'mobile1' },
    docs: {
      description: {
        story:
          'Mobile-only frame: hamburger nav opens `.mob-drawer` (public.jsx:43-56). Real `PublicHeader` handles responsive nav. DEFERRED Phase 4 baseline (Mobile viewport opt-in).',
      },
    },
  },
};
