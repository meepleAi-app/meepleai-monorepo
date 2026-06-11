/**
 * @mockup admin-mockups/design_files/public.html
 *
 * Public Landing argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Single-route mockup covering 1 user-facing route: `/` (landing page, public).
 *
 * Stage axis (public.jsx PublicNav routing, Desktop only Phase C-1; Mobile
 * + drawer DEFERRED a Phase 4):
 *   page: 'landing' | 'pricing' | 'about' | 'contact'
 *   state: 'default' | 'mobile-drawer-open'
 *
 * CANONICAL COMPONENT PICK: `WelcomeHero` from @/components/landing.
 * The real `LandingPage` (apps/web/src/app/(public)/page.tsx) is an async
 * Server Component that redirects authenticated users to /library — Storybook
 * Webpack cannot render server components directly. WelcomeHero is the visual
 * anchor section of the mockup and is a pure client primitive.
 *
 * Pricing / About / Contact are separate routes (apps/web/src/app/(public)/
 * pricing/, about/, contact/) — documented here for designer cross-reference.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { mswForPublicState } from '@/__tests__/fixtures/mockup-pilots/auth/public';
import { WelcomeHero } from '@/components/landing/WelcomeHero';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof WelcomeHero> = {
  title: 'Pages/Auth/Public Landing',
  component: WelcomeHero,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di public.jsx PublicNav sections (Desktop only Phase C-1; Mobile + drawer deferred Phase 4). LandingPage is a Server Component composing 5 sections (WelcomeHero + HowItWorksSteps + RulesQuickDemo + SocialProofBar + WelcomeCTA); this story renders WelcomeHero as the canonical anchor.',
      },
    },
  },
  args: {},
};
export default meta;

type Story = StoryObj<typeof WelcomeHero>;

export const Frame01_LandingHero: Story = {
  name: '01 · Landing — hero + features + how-it-works',
  parameters: { msw: { handlers: mswForPublicState('default') } },
};

export const Frame02_PricingPage: Story = {
  name: '02 · Pricing — 3-tier card grid + comparison matrix',
  parameters: {
    msw: { handlers: mswForPublicState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /pricing is a separate route. Real component covered by dedicated story; this frame preserves mockup matrix view for designer cross-reference.',
      },
    },
  },
};

export const Frame03_AboutPage: Story = {
  name: '03 · About — team + mission',
  parameters: {
    msw: { handlers: mswForPublicState('default') },
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
    msw: { handlers: mswForPublicState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. /contact is a separate route. Mockup matrix preserves view; real component is route-owned.',
      },
    },
  },
};

export const Frame05_MobileDrawerOpen: Story = {
  name: '05 · Mobile · drawer aperto (hamburger nav)',
  parameters: {
    msw: { handlers: mswForPublicState('default') },
    viewport: { defaultViewport: 'mobile1' },
    docs: {
      description: {
        story:
          'Mobile-only frame: hamburger nav opens .mob-drawer (public.jsx:43-56). Real PublicHeader handles responsive nav. DEFERRED Phase 4 baseline (Mobile viewport opt-in).',
      },
    },
  },
};
