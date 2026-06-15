/**
 * ScoringPanelRenderer Storybook stories — visual baselines for the G5a feature.
 *
 * Replaces the Playwright visual-regression baselines originally listed in
 * plan §4 T9 because the visual gate suite was RETIRED 2026-05-20 (CLAUDE.md
 * § "Visual Gate REMOVED 2026-05-20"). Stories cover the same matrix the
 * Playwright spec would have captured:
 *   - 4 read-side variants × Spectator role (pure read)
 *   - 4 read-side variants × Host role (read + editor)
 *   - Player + Points carve-out (own-row scoped editor)
 *   - Empty state (null data)
 *
 * Designer review path: manual gate on PR (per CLAUDE.md L302 "replacement =
 * manual designer review on PRs"). Once approved, the fidelity meta on
 * `admin-mockups/design_files/sp4-session-skeleton-renderers.fidelity.json`
 * gains the `designer_approved_by` + `designer_approved_on` fields.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T9 ↔ Storybook substitute).
 *
 * @mockup admin-mockups/design_files/sp4-session-skeleton-renderers.jsx
 */

import type { PlayerOption } from '@/components/sessions/score-strategies/types';

import {
  BINARY_WIN_PANEL_FIXTURE,
  OBJECTIVES_PANEL_FIXTURE,
  POINTS_PANEL_FIXTURE,
  RANKING_PANEL_FIXTURE,
} from './__fixtures__/scoring-panel-data.fixture';
import { ScoringPanelRenderer, type ScoringPanelRendererLabels } from './ScoringPanelRenderer';

import type { Meta, StoryObj } from '@storybook/react';

// ─── Shared labels ───────────────────────────────────────────────────────────

const LABELS: ScoringPanelRendererLabels = {
  empty: {
    title: 'Nessun punteggio ancora',
    message: 'I punteggi appariranno qui appena la partita inizia',
    trophyAriaLabel: 'Trofeo',
  },
  points: {
    title: 'Punteggi',
    emptyMessage: 'Nessun punteggio',
    leaderAriaSuffix: 'leader',
    categoriesTitle: 'Categorie',
    turnDeltaPrefix: '+',
  },
  ranking: {
    title: 'Classifica',
    emptyMessage: 'Nessuna classifica',
    leaderAriaSuffix: 'vincitore',
    rankAriaLabelTemplate: 'Posizione {rank}',
    trophyAriaLabel: 'Trofeo',
  },
  binaryWin: {
    title: 'Esito collettivo',
    emptyMessage: 'Nessun esito',
    categoriesTitle: 'Condizioni',
    weightWinLabel: 'vince',
    weightLoseLabel: 'perde',
    weightNeutralLabel: 'neutro',
    meterAriaLabelTemplate: 'Progresso {value} su {max}',
  },
  objectives: {
    title: 'Obiettivi',
    emptyMessage: 'Nessun obiettivo',
    completedCounterTemplate: '{done}/{total} completati',
    doneAriaLabel: 'Completato',
    pendingAriaLabel: 'Da completare',
    progressAriaLabelTemplate: 'Progresso {value}',
  },
};

const VIEWER_ID = '00000000-0000-4000-8000-0000000000a1'; // Marco (Host in fixture)
const SESSION_ID = '00000000-0000-4000-8000-000000000d20';

/** Editor roster used by BinaryWin / Objectives variants (no native roster). */
const EDITOR_ROSTER: ReadonlyArray<PlayerOption> = [
  { id: '00000000-0000-4000-8000-0000000000a1', displayName: 'Marco' },
  { id: '00000000-0000-4000-8000-0000000000a2', displayName: 'Anna' },
  { id: '00000000-0000-4000-8000-0000000000a3', displayName: 'Luca' },
  { id: '00000000-0000-4000-8000-0000000000a4', displayName: 'Sara' },
];

// ─── Meta ────────────────────────────────────────────────────────────────────

const meta: Meta<typeof ScoringPanelRenderer> = {
  title: 'Features/SessionLive/ScoringPanelRenderer',
  component: ScoringPanelRenderer,
  parameters: {
    layout: 'centered',
    docs: {
      description: {
        component:
          'Polymorphic dispatcher composing the 4 read-side variant panels with the existing `PolymorphicScoreEditor` (write-side, host gate). Plan: `docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md`.',
      },
    },
  },
  decorators: [
    Story => (
      <div className="min-w-[420px] max-w-[520px] p-4">
        <Story />
      </div>
    ),
  ],
  args: {
    labels: LABELS,
    viewerId: VIEWER_ID,
    sessionId: SESSION_ID,
    onScoreChange: () => undefined,
  },
  argTypes: {
    viewerRole: {
      control: 'radio',
      options: ['Spectator', 'Player', 'Host'],
      description:
        'Drives the canEdit role gate. Host always edits; Player+Points edits own; otherwise read-only.',
    },
  },
  tags: ['autodocs'],
};

export default meta;
type Story = StoryObj<typeof ScoringPanelRenderer>;

// ─── Empty state ─────────────────────────────────────────────────────────────

export const Empty: Story = {
  name: 'Empty — null data',
  args: {
    data: null,
    viewerRole: 'Spectator',
  },
};

// ─── Points variant ──────────────────────────────────────────────────────────

export const PointsSpectator: Story = {
  name: 'Points — Spectator (read-only)',
  args: {
    data: POINTS_PANEL_FIXTURE,
    viewerRole: 'Spectator',
  },
};

export const PointsPlayer: Story = {
  name: 'Points — Player carve-out (own-row editor)',
  args: {
    data: POINTS_PANEL_FIXTURE,
    viewerRole: 'Player',
  },
  parameters: {
    docs: {
      description: {
        story:
          "Player role on Points scopes the editor to ONLY the viewer's own player row. Other players appear in the read-side leaderboard but cannot be mutated.",
      },
    },
  },
};

export const PointsHost: Story = {
  name: 'Points — Host (full editor)',
  args: {
    data: POINTS_PANEL_FIXTURE,
    viewerRole: 'Host',
  },
};

// ─── Ranking variant ─────────────────────────────────────────────────────────

export const RankingSpectator: Story = {
  name: 'Ranking — Spectator (read-only)',
  args: {
    data: RANKING_PANEL_FIXTURE,
    viewerRole: 'Spectator',
  },
};

export const RankingHost: Story = {
  name: 'Ranking — Host (DnD editor)',
  args: {
    data: RANKING_PANEL_FIXTURE,
    viewerRole: 'Host',
  },
};

// ─── BinaryWin variant ───────────────────────────────────────────────────────

export const BinaryWinSpectator: Story = {
  name: 'BinaryWin — Spectator (read-only)',
  args: {
    data: BINARY_WIN_PANEL_FIXTURE,
    viewerRole: 'Spectator',
  },
};

export const BinaryWinHost: Story = {
  name: 'BinaryWin — Host (toggles)',
  args: {
    data: BINARY_WIN_PANEL_FIXTURE,
    viewerRole: 'Host',
    editorPlayers: EDITOR_ROSTER,
  },
  parameters: {
    docs: {
      description: {
        story:
          'BinaryWin variant has no native roster on the read-side data — the orchestrator supplies `editorPlayers`. Without it the editor would not mount.',
      },
    },
  },
};

// ─── Objectives variant ──────────────────────────────────────────────────────

export const ObjectivesSpectator: Story = {
  name: 'Objectives — Spectator (read-only)',
  args: {
    data: OBJECTIVES_PANEL_FIXTURE,
    viewerRole: 'Spectator',
  },
};

export const ObjectivesHost: Story = {
  name: 'Objectives — Host (checklist editor)',
  args: {
    data: OBJECTIVES_PANEL_FIXTURE,
    viewerRole: 'Host',
    editorPlayers: EDITOR_ROSTER,
  },
  parameters: {
    docs: {
      description: {
        story:
          'Host editor auto-derives `availableObjectives` from `data.objectives[].label`. Override with the prop when the orchestrator carries a richer list (e.g. unrevealed objectives).',
      },
    },
  },
};

// ─── Role-gate stress: Player + non-Points → editor NOT mounted ─────────────

export const PlayerRankingReadOnly: Story = {
  name: 'Player + Ranking → editor hidden',
  args: {
    data: RANKING_PANEL_FIXTURE,
    viewerRole: 'Player',
  },
  parameters: {
    docs: {
      description: {
        story:
          'Ranking is host-resolved at game end. A Player viewer sees the read-side panel only — no editor mounts even though canEdit might fire for Points.',
      },
    },
  },
};

export const PlayerBinaryWinReadOnly: Story = {
  name: 'Player + BinaryWin → editor hidden',
  args: {
    data: BINARY_WIN_PANEL_FIXTURE,
    viewerRole: 'Player',
  },
};

export const PlayerObjectivesReadOnly: Story = {
  name: 'Player + Objectives → editor hidden',
  args: {
    data: OBJECTIVES_PANEL_FIXTURE,
    viewerRole: 'Player',
  },
};
