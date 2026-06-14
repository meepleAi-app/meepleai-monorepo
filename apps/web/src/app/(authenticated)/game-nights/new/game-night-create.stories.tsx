/**
 * @mockup admin-mockups/design_files/sp7-game-night-new.html
 *
 * Game Night Create wizard argTypes matrix story — DS-17 Phase C-1 (sub-issue #2166).
 *
 * Multi-route mockup canonical pick (P245):
 *   - HERO COMPONENT: `GameNightCreateWizard` (4-step orchestrator) is the
 *     hero for the stage matrix. NewGameNightContent (the page-client) wires
 *     the wizard reducer + react-query results; here we stub fixtures.
 *   - The 6 sub-components (DateTimePicker, LocationToggle, PlayerInviteAutocomplete,
 *     GameCandidatesPicker, RSVPCardLivePreview) live under
 *     apps/web/src/components/features/game-night-create/<Component>.tsx and
 *     get individual dedicated stories alongside their files (see scaffold
 *     decision §6 of parent spec, mirrors auth-flow pattern).
 *
 * Stage axis (sp7-game-night-create.jsx STATES array lines 1475-1484 — 10
 * frames including 8 Mobile + 1 Mobile step-flow overview + 1 Desktop split):
 *   step: 1 | 2 | 3 | 4
 *   variant: null | 'warning' | 'empty' | 'typing' | 'filled' | 'decide-group'
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       parent docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2166.
 *
 * NOTE: this is a SCAFFOLD draft. Move to
 *   apps/web/src/app/(authenticated)/game-nights/new/page.stories.tsx
 * (or co-located beside `_content.tsx`) when wiring into the Storybook tree.
 */

import { fn } from 'storybook/test';

import {
  mswForSp7CreateState,
  MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
  MOCK_SP7_CREATE_WIZARD_STATE_STEP2,
  MOCK_SP7_CREATE_WIZARD_STATE_STEP3_TYPING,
  MOCK_SP7_CREATE_WIZARD_STATE_STEP3_FILLED,
  MOCK_SP7_CREATE_WIZARD_STATE_STEP4_DECIDE_GROUP,
  MOCK_SP7_CREATE_LABELS,
  MOCK_SP7_CREATE_LIBRARY_GAMES,
  MOCK_SP7_CREATE_REGULARS,
  MOCK_SP7_CREATE_PLAYER_SEARCH_TYPING,
} from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-create';
import { GameNightCreateWizard } from '@/components/features/game-night-create';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameNightCreateWizard> = {
  title: 'Pages/SP7/Game Night Create',
  component: GameNightCreateWizard,
  parameters: {
    layout: 'fullscreen',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-create.jsx Mobile frames 01-08 + Mobile step-flow overview 09 + Desktop split-form 10. Hero usa `GameNightCreateWizard` (4-step orchestrator). 6 sub-componenti (DateTimePicker, LocationToggle, PlayerInviteAutocomplete, GameCandidatesPicker, RSVPCardLivePreview) hanno dedicated stories accanto ai loro file.',
      },
    },
  },
  argTypes: {
    state: {
      control: false,
      description:
        'WizardState object (step + payload). Use Frame stories below for canonical step variants.',
    },
    isSubmitting: {
      control: 'boolean',
      description: 'Mutation pending state (during create POST).',
    },
    isCheckingConflict: {
      control: 'boolean',
      description: 'Conflict check pending state (Step 1 warning).',
    },
  },
  args: {
    dispatch: fn(),
    onSubmit: fn(),
    onPlayerSearchQueryChange: fn(),
    title: 'Sabato boardgame con i Padovani',
    organizerName: 'Marco R.',
    labels: MOCK_SP7_CREATE_LABELS,
    playerSearchQuery: '',
    playerSearchResults: [],
    isSearchingPlayers: false,
    regulars: MOCK_SP7_CREATE_REGULARS,
    libraryGames: MOCK_SP7_CREATE_LIBRARY_GAMES,
    state: MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
    isSubmitting: false,
    isCheckingConflict: false,
  },
  decorators: [
    Story => (
      <div className="min-h-dvh bg-background p-6">
        <div className="mx-auto max-w-6xl">
          <Story />
        </div>
      </div>
    ),
  ],
};
export default meta;

type Story = StoryObj<typeof GameNightCreateWizard>;

// ── Stage frame canonicals (mapped 1:1 ai 10 frame mockup — Desktop only Phase
// C-1; Mobile state-09 overview + Mobile state-01..08 DEFERRED a Phase 4
// viewport sweep) ──────────────────────────────────────────────────────────

export const Frame01_Step1Quando: Story = {
  name: '01 · Step 1 — Quando? (default sab 17 mag · 21:00 · 3h)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT, isCheckingConflict: false },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame02_Step1Warning: Story = {
  name: '02 · Step 1 — Conflitto rilevato (warning amber)',
  args: {
    state: MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT,
    isCheckingConflict: false,
  },
  parameters: {
    msw: { handlers: mswForSp7CreateState('conflict') },
    docs: {
      description: {
        story:
          'Conflict banner triggered by `useGameNightConflictCheck` returning a colliding event. Mockup line 388-427.',
      },
    },
  },
};

export const Frame03_Step2Location: Story = {
  name: '03 · Step 2 — Dove? (Casa host + 4 opzioni + mappa)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_STEP2 },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame04_Step3Empty: Story = {
  name: '04 · Step 3 — Chi? (empty + suggested regulars)',
  args: {
    state: { ...MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT, step: 3 as const },
  },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame05_Step3Typing: Story = {
  name: '05 · Step 3 — Autocomplete dropdown attivo ("fede")',
  args: {
    state: MOCK_SP7_CREATE_WIZARD_STATE_STEP3_TYPING,
    playerSearchQuery: 'fede',
    playerSearchResults: MOCK_SP7_CREATE_PLAYER_SEARCH_TYPING,
    isSearchingPlayers: false,
  },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame06_Step3Filled: Story = {
  name: '06 · Step 3 — 6 invitati (5 reg + 1 email NEW)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_STEP3_FILLED },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame07_Step4Games: Story = {
  name: '07 · Step 4 — Cosa? (3 candidati selected)',
  args: {
    state: {
      ...MOCK_SP7_CREATE_WIZARD_STATE_STEP3_FILLED,
      step: 4 as const,
    },
  },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

export const Frame08_Step4DecideGroup: Story = {
  name: '08 · Step 4 — Lascia decidere al gruppo',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_STEP4_DECIDE_GROUP },
  parameters: { msw: { handlers: mswForSp7CreateState('default') } },
};

// ── Mobile step-flow overview (state-09 in mockup) — documentation-only ────
export const Frame09_MobileStepFlow: Story = {
  name: '09 · Mobile · Step-flow overview (1→4 affiancati)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT },
  parameters: {
    msw: { handlers: mswForSp7CreateState('default') },
    docs: {
      description: {
        story:
          'Documentation-only frame. Mockup line 1438-1470: stub side-by-side per QA visiva. Storybook viewport Mobile deferred a Phase 4; render hero qui resta Desktop split.',
      },
    },
  },
};

// ── Desktop split-form (state-10 — canonical render mode) ──────────────────
export const Frame10_DesktopSplit: Story = {
  name: '10 · Desktop · Step 1 + RSVP live preview (8-col / 4-col)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_DEFAULT },
  parameters: {
    msw: { handlers: mswForSp7CreateState('default') },
    docs: {
      description: {
        story:
          'Mockup state-10: split-form 12-col (form 8-col + RSVPCardLivePreview 4-col sticky right). Hero GameNightCreateWizard wraps both panes via internal grid `grid-cols-[2fr_1fr]`.',
      },
    },
  },
};

// ── State variant frames (axis = SubmitState) ──────────────────────────────

export const Loading: Story = {
  name: 'State · Submitting (POST in flight)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_STEP4_DECIDE_GROUP, isSubmitting: true },
  parameters: { msw: { handlers: mswForSp7CreateState('submitting') } },
};

export const ErrorState: Story = {
  name: 'State · Submit error (409 conflict)',
  args: { state: MOCK_SP7_CREATE_WIZARD_STATE_STEP4_DECIDE_GROUP },
  parameters: { msw: { handlers: mswForSp7CreateState('error') } },
};
