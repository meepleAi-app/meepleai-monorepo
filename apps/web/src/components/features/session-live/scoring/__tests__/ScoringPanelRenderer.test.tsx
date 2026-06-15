/**
 * ScoringPanelRenderer — polymorphic dispatcher + host/viewer role gate.
 *
 * Issue #2373 — sub-issue G5a of epic #2354 (T6, centerpiece).
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T6
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type {
  BinaryWinPanelData,
  ObjectivesPanelData,
  PointsPanelData,
  RankingPanelData,
  ScoringPanelData,
} from '../types';
import { ScoringPanelRenderer } from '../ScoringPanelRenderer';
import type { ScoringPanelRendererLabels } from '../ScoringPanelRenderer';

// ─── Mock PolymorphicScoreEditor ─────────────────────────────────────────────
//
// The real editor wires `useUpdateSessionScores` + 4 strategy sub-components.
// For unit tests we mock it with a sentinel <div> exposing the props we care
// about (`scoringType`, `players` size, `availableObjectives` count) so we can
// assert HOST gate composition without exercising the editor's own logic.

vi.mock('@/components/sessions/PolymorphicScoreEditor', () => ({
  PolymorphicScoreEditor: (props: {
    readonly scoringType: string;
    readonly players: readonly { readonly id: string }[];
    readonly availableObjectives?: readonly string[];
  }) => (
    <div
      data-testid="polymorphic-score-editor"
      data-scoring-type={props.scoringType}
      data-players-count={props.players.length}
      data-objectives-count={props.availableObjectives?.length ?? 0}
    />
  ),
}));

// ─── Fixtures ────────────────────────────────────────────────────────────────

const LABELS: ScoringPanelRendererLabels = {
  empty: {
    title: 'Nessun punteggio',
    message: 'Il punteggio apparirà qui',
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

function pointsData(): PointsPanelData {
  return {
    scoringType: 'Points',
    players: [
      { id: 'p-viewer', displayName: 'Marco', score: 35 },
      { id: 'p-other', displayName: 'Luca', score: 42 },
    ],
  };
}

function rankingData(): RankingPanelData {
  return {
    scoringType: 'Ranking',
    ranking: [
      { id: 'p-1', displayName: 'Luca', rank: 1 },
      { id: 'p-2', displayName: 'Marco', rank: 2 },
    ],
  };
}

function binaryWinData(): BinaryWinPanelData {
  return {
    scoringType: 'BinaryWin',
    collective: {
      goalLabel: 'Cure',
      goalValue: 2,
      goalMax: 4,
      failLabel: 'Focolai',
      failValue: 5,
      failMax: 8,
    },
    categories: [],
  };
}

function objectivesData(): ObjectivesPanelData {
  return {
    scoringType: 'Objectives',
    objectives: [
      { id: 'o-1', label: 'Recluta 3 lavoratori', done: true },
      { id: 'o-2', label: 'Costruisci 2 edifici', done: false },
    ],
  };
}

const VIEWER_ID = 'p-viewer';
const SESSION_ID = 'session-123';

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — null data', () => {
  it('renders ScoringPanelEmpty when data is null', () => {
    render(
      <ScoringPanelRenderer
        data={null}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-empty')).toBeInTheDocument();
    expect(screen.queryByTestId('scoring-panel-points')).not.toBeInTheDocument();
  });

  it('does NOT mount the editor when data is null', () => {
    render(
      <ScoringPanelRenderer
        data={null}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
  });

  it('exposes data-score-type="unknown" on root when data is null', () => {
    render(
      <ScoringPanelRenderer
        data={null}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    const root = screen.getByTestId('scoring-panel');
    expect(root).toHaveAttribute('data-score-type', 'unknown');
  });
});

describe('ScoringPanelRenderer — read-side dispatch', () => {
  it('switches to PointsPanel when scoringType === "Points"', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
    expect(screen.queryByTestId('scoring-panel-ranking')).not.toBeInTheDocument();
  });

  it('switches to RankingPanel when scoringType === "Ranking"', () => {
    render(
      <ScoringPanelRenderer
        data={rankingData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-ranking')).toBeInTheDocument();
  });

  it('switches to BinaryWinPanel when scoringType === "BinaryWin"', () => {
    render(
      <ScoringPanelRenderer
        data={binaryWinData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-binarywin')).toBeInTheDocument();
  });

  it('switches to ObjectivesPanel when scoringType === "Objectives"', () => {
    render(
      <ScoringPanelRenderer
        data={objectivesData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-objectives')).toBeInTheDocument();
  });

  it('exposes data-score-type on root mirroring data.scoringType', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel')).toHaveAttribute('data-score-type', 'Points');
  });
});

describe('ScoringPanelRenderer — host gate (viewerRole === "Host")', () => {
  it('embeds PolymorphicScoreEditor when viewerRole === "Host" + Points', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    const editor = screen.getByTestId('polymorphic-score-editor');
    expect(editor).toHaveAttribute('data-scoring-type', 'Points');
  });

  it('embeds editor for Host on Ranking variant', () => {
    render(
      <ScoringPanelRenderer
        data={rankingData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('polymorphic-score-editor')).toHaveAttribute(
      'data-scoring-type',
      'Ranking'
    );
  });

  it('embeds editor for Host on BinaryWin variant (with editorPlayers roster)', () => {
    render(
      <ScoringPanelRenderer
        data={binaryWinData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        editorPlayers={[
          { id: 'p-1', displayName: 'Marco' },
          { id: 'p-2', displayName: 'Luca' },
        ]}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('polymorphic-score-editor')).toHaveAttribute(
      'data-scoring-type',
      'BinaryWin'
    );
  });

  it('embeds editor for Host on Objectives variant (with editorPlayers + availableObjectives)', () => {
    render(
      <ScoringPanelRenderer
        data={objectivesData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        editorPlayers={[{ id: VIEWER_ID, displayName: 'Marco' }]}
        availableObjectives={['Recluta 3 lavoratori', 'Costruisci 2 edifici']}
        labels={LABELS}
      />
    );
    const editor = screen.getByTestId('polymorphic-score-editor');
    expect(editor).toHaveAttribute('data-scoring-type', 'Objectives');
    expect(editor).toHaveAttribute('data-objectives-count', '2');
  });

  it('Host + BinaryWin/Objectives WITHOUT editorPlayers does NOT mount editor', () => {
    const { rerender } = render(
      <ScoringPanelRenderer
        data={binaryWinData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();

    rerender(
      <ScoringPanelRenderer
        data={objectivesData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
  });

  it('Host editor receives all players (no scoping)', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('polymorphic-score-editor')).toHaveAttribute(
      'data-players-count',
      '2'
    );
  });

  it('Host always shows BOTH read-side panel AND editor', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
    expect(screen.getByTestId('polymorphic-score-editor')).toBeInTheDocument();
  });
});

describe('ScoringPanelRenderer — Player + Points carve-out', () => {
  it('embeds editor for Player on Points scoped to OWN player only', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Player"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    const editor = screen.getByTestId('polymorphic-score-editor');
    expect(editor).toHaveAttribute('data-scoring-type', 'Points');
    expect(editor).toHaveAttribute('data-players-count', '1');
  });

  it('Player + Points still renders read-side panel below editor', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Player"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
    expect(screen.getByTestId('polymorphic-score-editor')).toBeInTheDocument();
  });

  it('Player editor is NOT mounted when viewer is not in the players list', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Player"
        viewerId="p-not-in-list"
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
    // The read-side panel must still render.
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
  });
});

describe('ScoringPanelRenderer — read-only viewers', () => {
  it('Spectator + Points → NO editor mounted', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
  });

  it('Player + Ranking → NO editor (only host can resolve ranks)', () => {
    render(
      <ScoringPanelRenderer
        data={rankingData()}
        viewerRole="Player"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
    expect(screen.getByTestId('scoring-panel-ranking')).toBeInTheDocument();
  });

  it('Player + BinaryWin → NO editor (only host resolves co-op outcome)', () => {
    render(
      <ScoringPanelRenderer
        data={binaryWinData()}
        viewerRole="Player"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
  });

  it('Player + Objectives → NO editor (only host marks objectives)', () => {
    render(
      <ScoringPanelRenderer
        data={objectivesData()}
        viewerRole="Player"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        labels={LABELS}
      />
    );
    expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
  });

  it('Spectator + any variant → NO editor', () => {
    const variants: ReadonlyArray<ScoringPanelData> = [
      pointsData(),
      rankingData(),
      binaryWinData(),
      objectivesData(),
    ];

    variants.forEach(data => {
      const { unmount } = render(
        <ScoringPanelRenderer
          data={data}
          viewerRole="Spectator"
          viewerId={VIEWER_ID}
          labels={LABELS}
        />
      );
      expect(screen.queryByTestId('polymorphic-score-editor')).not.toBeInTheDocument();
      unmount();
    });
  });
});

describe('ScoringPanelRenderer — root contract + a11y', () => {
  it('root has data-testid="scoring-panel"', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel')).toBeInTheDocument();
  });

  it('root has aria-label "Scoring panel" by default', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('scoring-panel')).toHaveAttribute('aria-label', 'Scoring panel');
  });

  it('honors a custom data-testid override prop', () => {
    render(
      <ScoringPanelRenderer
        data={pointsData()}
        viewerRole="Spectator"
        viewerId={VIEWER_ID}
        labels={LABELS}
        data-testid="custom-scoring-panel"
      />
    );
    expect(screen.getByTestId('custom-scoring-panel')).toBeInTheDocument();
  });
});

describe('ScoringPanelRenderer — Objectives editor defensive', () => {
  it('Host + Objectives derives availableObjectives from data when prop missing', () => {
    render(
      <ScoringPanelRenderer
        data={objectivesData()}
        viewerRole="Host"
        viewerId={VIEWER_ID}
        sessionId={SESSION_ID}
        editorPlayers={[{ id: VIEWER_ID, displayName: 'Marco' }]}
        labels={LABELS}
      />
    );
    expect(screen.getByTestId('polymorphic-score-editor')).toHaveAttribute(
      'data-objectives-count',
      '2'
    );
  });
});
