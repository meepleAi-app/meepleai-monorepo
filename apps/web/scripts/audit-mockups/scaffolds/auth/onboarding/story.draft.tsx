/**
 * @mockup admin-mockups/design_files/onboarding.html
 *
 * Onboarding argTypes matrix story — DS-17 Phase C-1 (sub-issue #2160).
 *
 * Multi-route mockup covering 4 (auth) routes:
 *   /welcome, /onboarding, /setup, /setup-account
 *
 * Stage axis del mockup (5 step + 1 hub frame, Desktop only Phase C-1; Mobile
 * DEFERRED a Phase 4):
 *   step: 0 (welcome) | 1 (games) | 2 (agents) | 3 (session) | 4 (complete)
 *   state: 'default' | 'selected-min3' | 'all-selected' | 'completion-confetti'
 *
 * CANONICAL COMPONENT PICK (multi-route consolidation, DS-17 Phase C-1 spec § 6):
 *   We render `OnboardingGenericWizard` (apps/web/src/app/(authenticated)/
 *   onboarding/OnboardingGenericWizard.tsx) — the 3-step wizard shipped in
 *   Asse D follow-up P3 (umbrella #1895 sub-issue #1899). The mockup shows the
 *   legacy 5-step page-flow (OnboardingTourClient, deleted), but the wizard is
 *   the current production component.
 *
 *   NOTE on mockup ↔ codebase divergence: The mockup's 5 steps (Welcome →
 *   Games → Agents → Session → Complete) compress to the wizard's 3 steps
 *   (Interests → FirstGame → InviteFriendComingSoon). Documented in
 *   axis-discovery.md. Frame matrix preserves the mockup's 5-step view for
 *   designer review; the rendered hero uses the 3-step wizard.
 *
 *   BGG legal constraint (#1903 ADR): User-side BGG access is blocked for ToS
 *   compliance. `FirstGameStep` searches the internal catalog
 *   (`api.games.getAll`) NOT `useSearchBggGames` (admin-only).
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { OnboardingGenericWizard } from '@/app/(authenticated)/onboarding/OnboardingGenericWizard';

import { mswForState } from '@/__tests__/fixtures/mockup-pilots/auth/onboarding';

import type { Meta, StoryObj } from '@storybook/react';

type OnboardingStep = 0 | 1 | 2 | 3 | 4;
type OnboardingState = 'default' | 'selected-min3' | 'all-selected' | 'completion-confetti';

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

// ── Stage frame canonicals (mapped 1:1 ai 5 step + 1 hub del mockup stage) ──
// Mobile frames DEFERRED to Phase 4 (viewport sweep, Mobile opt-in).

export const Frame01_StepWelcome: Story = {
  name: '01 · Step 0 — Welcome hero + CTA "Inizia"',
  parameters: {
    msw: { handlers: mswForState('default') },
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
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Mockup step 1: 8-card game grid (Catan, Carcassonne, Ticket to Ride, Wingspan, 7 Wonders, Terraforming Mars, Azul, Splendor) with selection state, MIN_SELECTED=3 gate. Real wizard step 1 = InterestsStep (9 board game categories — see Asse D P3 plan).',
      },
    },
  },
};

export const Frame03_StepAgents: Story = {
  name: '03 · Step 2 — Agents toggle (Regole/Strategia/Setup/Cronista)',
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Mockup step 2: 4 agent toggle cards (Regole, Strategia, Setup, Cronista with defaultOn flags). Real wizard SKIPS this step — agents activated per game post-FirstGameStep.',
      },
    },
  },
};

export const Frame04_StepSession: Story = {
  name: '04 · Step 3 — Session CTA (Crea serata / Esplora library / Chat agent)',
  parameters: {
    msw: { handlers: mswForState('default') },
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
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Mockup step 4: completion confetti + brand celebration. Real wizard handleComplete → toast.success("Onboarding completato!") + router.replace("/library").',
      },
    },
  },
};

export const State_FirstGameCompleted: Story = {
  name: 'State · userName=null (generic title)',
  args: { userName: null },
  parameters: {
    msw: { handlers: mswForState('default') },
    docs: {
      description: {
        story:
          'Variant: no userName prop → step 1 shows generic title "Scegli i tuoi interessi" (OnboardingGenericWizard.tsx:72).',
      },
    },
  },
};
