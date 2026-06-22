/**
 * @mockup admin-mockups/design_files/sp7-game-night-transition.html
 *
 * Game Night Transition Dialog argTypes matrix story — DS-17 Phase D-1 (sub-issue #2174).
 *
 * Multi-state mockup canonical pick (P245):
 *   The mockup classified `component-mock` in `admin-mockups/MOCKUPS_INDEX.md`
 *   covers a single dialog opened from `/game-nights/[id]/live` via
 *   `NightLiveClientView` (PR #1250). All 6 states are internal variants
 *   driven by:
 *     - `rulesStatus`: 'ok' | 'loading' | 'error'  (KB quick-glance status)
 *     - `submodal`:    null | 'skip' | 'end'       (nested confirm modal)
 *     - `mobile`:      boolean                     (fullscreen vertical stack)
 *
 *   HERO COMPONENT: `GameTransitionDialog` (`apps/web/src/components/features/game-nights/transition/GameTransitionDialog.tsx`)
 *   is the canonical hero. It is purely presentational — the parent
 *   orchestrator owns the network round-trips (transition POST, KB fetch).
 *
 * Stage axis (sp7-game-night-transition.jsx STATES — 6 frames):
 *   01 default 2-col split (recap + preview)
 *   02 KB rules quick-glance loading skeleton
 *   03 KB fetch failed (fallback banner + 2 micro-CTA)
 *   04 nested submodal "Saltare Spirit Island?" (warning amber)
 *   05 nested submodal "Terminare serata qui?" (danger rosso)
 *   06 mobile fullscreen vertical stack
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-11-sp6-7-nano-cluster-design.md,
 *       umbrella #2063, sub-issue #2174 (Phase D-1).
 */

import { fn } from 'storybook/test';

import {
  MOCK_SP7_TRANSITION_LAST_GAME,
  MOCK_SP7_TRANSITION_NEXT_GAME,
  MOCK_SP7_TRANSITION_NIGHT_CODE,
} from '@/__tests__/fixtures/mockup-pilots/sp6-7-nano/sp7-game-night-transition';
import { GameTransitionDialog } from '@/components/features/game-nights/transition';

import type { Meta, StoryObj } from '@storybook/react';

const meta: Meta<typeof GameTransitionDialog> = {
  title: 'Pages/SP7/Game Night Transition',
  component: GameTransitionDialog,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Pixel-faithful matrix di sp7-game-night-transition.jsx 6 frames. Hero usa `GameTransitionDialog` (Screen L) — modal centered 600×500 desktop / fullscreen mobile, 2-col split (recap last | preview next). Mounted da `NightLiveClientView` come overlay del live hub. KB rules quick-glance ha 3 stati (ok/loading/error). 2 submodal nested (skip warning / end danger).',
      },
    },
  },
  argTypes: {
    open: { control: 'boolean', description: 'Dialog open/closed.' },
    rulesStatus: {
      control: 'select',
      options: ['ok', 'loading', 'error'],
      description: 'KB rules quick-glance fetch status.',
    },
    submodal: {
      control: 'select',
      options: [null, 'skip', 'end'],
      description:
        'Nested confirm submodal — null hides, "skip" is warning amber, "end" is danger rosso.',
    },
    mobile: {
      control: 'boolean',
      description: 'Mobile shell mode (fullscreen vertical stack instead of 2-col grid).',
    },
    nightCode: {
      control: 'text',
      description: 'Game night code shown in dialog header (e.g. "#GN-042").',
    },
  },
  args: {
    open: true,
    lastGame: MOCK_SP7_TRANSITION_LAST_GAME,
    nextGame: MOCK_SP7_TRANSITION_NEXT_GAME,
    rulesStatus: 'ok',
    submodal: null,
    mobile: false,
    nightCode: MOCK_SP7_TRANSITION_NIGHT_CODE,
    onClose: fn(),
    onPlayNext: fn(),
    onOpenSubmodal: fn(),
    onConfirmSubmodal: fn(),
    onCancelSubmodal: fn(),
    onRetryRules: fn(),
    onProceedWithoutRules: fn(),
  },
};
export default meta;

type Story = StoryObj<typeof GameTransitionDialog>;

// ── Stage frame canonicals (mapped 1:1 ai 6 frame mockup) ──────────────────

export const Frame01_DefaultTwoColSplit: Story = {
  name: '01 · Default 2-col split · recap Brass + preview Spirit popolati',
  args: {
    rulesStatus: 'ok',
    submodal: null,
    mobile: false,
  },
};

export const Frame02_RulesLoadingSkeleton: Story = {
  name: '02 · KB rules quick-glance in loading · skeleton 3 righe',
  args: {
    rulesStatus: 'loading',
    submodal: null,
    mobile: false,
  },
};

export const Frame03_RulesLoadError: Story = {
  name: "03 · KB fetch failed · fallback 'Rules non disponibili offline' + retry",
  args: {
    rulesStatus: 'error',
    submodal: null,
    mobile: false,
  },
};

export const Frame04_SkipConfirmSubmodal: Story = {
  name: "04 · Submodal nested · 'Saltare Spirit Island?' (warning amber)",
  args: {
    rulesStatus: 'ok',
    submodal: 'skip',
    mobile: false,
  },
};

export const Frame05_EndNightConfirmSubmodal: Story = {
  name: "05 · Submodal nested · 'Terminare serata qui?' (danger rosso)",
  args: {
    rulesStatus: 'ok',
    submodal: 'end',
    mobile: false,
  },
};

export const Frame06_MobileFullscreen: Story = {
  name: '06 · Mobile fullscreen · vertical stack invece di 2-col',
  args: {
    rulesStatus: 'ok',
    submodal: null,
    mobile: true,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Mobile-only frame. Viewport sweep DEFERRED a Phase 4 — story render Desktop layout with `mobile=true` prop for current visual check. Dialog occupa 100% del viewport (no border-radius, no shadow).',
      },
    },
  },
};

// ── Additional state variant (closed) — useful for FSM completeness ────────

export const StateClosed: Story = {
  name: 'State · Closed (open=false → returns null)',
  args: { open: false },
  parameters: {
    docs: {
      description: {
        story:
          'When `open=false` the component returns `null`. Documentation-only frame for completeness.',
      },
    },
  },
};
