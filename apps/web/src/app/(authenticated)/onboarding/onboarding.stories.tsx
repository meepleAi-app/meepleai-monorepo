/**
 * @mockup admin-mockups/design_files/onboarding.html
 *
 * Onboarding argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 4 (authenticated) routes:
 *   /welcome, /onboarding, /setup, /setup-account
 *
 * Stage axis (onboarding.jsx STEP_LABELS, 5 step + 1 hub frame, Desktop only
 * Phase C-1; Mobile DEFERRED a Phase 4):
 *   step: 0 (welcome) | 1 (games) | 2 (agents) | 3 (session) | 4 (complete)
 *   state: 'default' | 'selected-min3' | 'all-selected' | 'completion-confetti'
 *
 * CANONICAL COMPONENT PICK (DS-17 Phase C-1 spec § 6 multi-route consolidation):
 *   Renders `OnboardingGenericWizard` (3-step WizardModal shipped Asse D
 *   sub-issue #1899 follow-up P3). The mockup shows the legacy 5-step
 *   page-flow (OnboardingTourClient, deleted), so frame matrix preserves the
 *   mockup view for designer review while hero renders the production wizard.
 *
 * BGG legal constraint (#1903 ADR): User-side BGG access blocked. FirstGameStep
 * uses internal `api.games.getAll`, NOT `useSearchBggGames`.
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { mswForOnboardingState } from '@/__tests__/fixtures/mockup-pilots/auth/onboarding';

import { OnboardingGenericWizard } from './OnboardingGenericWizard';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof OnboardingGenericWizard> = {
  title: 'Pages/Auth/Onboarding',
  component: OnboardingGenericWizard,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di onboarding.jsx stage frames 01-05 (Desktop only Phase C-1; Mobile deferred Phase 4). Mockup shows legacy 5-step page-flow; rendered hero usa OnboardingGenericWizard (3-step WizardModal, Asse D P3). Frame matrix exposes mockup step view via argTypes for designer review.',
      },
    },
  },
  argTypes: {
    userName: {
      control: 'text',
      description:
        'User display name shown in step 1 title ("Ciao {name}, scegli i tuoi interessi"). null = generic title.',
    },
  },
  args: {
    userName: 'Marco',
  },
};
export default meta;

type Story = StoryObj<typeof OnboardingGenericWizard>;

// ── Stage frame canonicals (mapped 1:1 ai 5 step del mockup stage) ─────────
// Mobile frames DEFERRED to Phase 4 (viewport sweep, Mobile opt-in).

export const Frame01_StepWelcome: Story = {
  name: '01 · Step 0 — Welcome hero + CTA "Inizia"',
  parameters: {
    msw: { handlers: mswForOnboardingState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Mockup shows Welcome step with confetti hero + brand mark + CTA "Inizia". Real WizardModal starts at Interests step 1 (no separate welcome).',
      },
    },
  },
};

export const Frame02_StepGames: Story = {
  name: '02 · Step 1 — Games selection (min 3)',
  parameters: {
    msw: { handlers: mswForOnboardingState('default') },
    docs: {
      description: {
        story:
          'Mockup step 1: 8-card game grid with MIN_SELECTED=3 gate. Real wizard step 1 = InterestsStep (9 board game categories).',
      },
    },
  },
};

export const Frame03_StepAgents: Story = {
  name: '03 · Step 2 — Agents toggle (Regole/Strategia/Setup/Cronista)',
  parameters: {
    msw: { handlers: mswForOnboardingState('default') },
    docs: {
      description: {
        story:
          'Mockup step 2: 4 agent toggle cards. Real wizard SKIPS this step — agents activated per game post-FirstGameStep.',
      },
    },
  },
};

export const Frame04_StepSession: Story = {
  name: '04 · Step 3 — Session CTA (Crea serata / Esplora library / Chat agent)',
  parameters: {
    msw: { handlers: mswForOnboardingState('default') },
    docs: {
      description: {
        story:
          'Mockup step 3: 3 action-card grid (Crea la prima serata, Esplora la library, Chatta con un agente). Real wizard step 2 = FirstGameStep (internal catalog search, NO BGG per ADR #1903).',
      },
    },
  },
};

export const Frame05_StepComplete: Story = {
  name: '05 · Step 4 — Confetti + brand celebration',
  parameters: {
    msw: { handlers: mswForOnboardingState('completion-confetti') },
    docs: {
      description: {
        story:
          'Mockup step 4: completion confetti + brand celebration. Real wizard handleComplete → toast.success("Onboarding completato!") + router.replace("/library").',
      },
    },
  },
};

export const State_NoUserName: Story = {
  name: 'State · userName=null (generic title)',
  args: { userName: null },
  parameters: {
    msw: { handlers: mswForOnboardingState('default') },
    docs: {
      description: {
        story:
          'Variant: no userName prop → step 1 shows generic title "Scegli i tuoi interessi" (OnboardingGenericWizard.tsx:72).',
      },
    },
  },
};
