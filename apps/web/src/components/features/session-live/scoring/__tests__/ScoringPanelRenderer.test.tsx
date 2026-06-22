/**
 * ScoringPanelRenderer unit tests — G5a polymorphic scoring display (Issue #2375).
 *
 * Coverage:
 *   - Points:     sorted leaderboard rows, leader highlight, score aria-label
 *   - Ranking:    ordinal pill, first-place badge sr-only, sorted by position
 *   - BinaryWin:  win/lose indicators, in-progress banner when all undecided
 *   - Objectives: checklist items, per-player summary, done/pending aria-labels
 *   - axe AA:     one consolidated a11y pass over a composite fixture
 *   - exhaustiveness: assertNever throws on unknown kind (TypeScript runtime guard)
 *
 * Accessibility note: `jest-axe` extended via vitest.setup.tsx (toHaveNoViolations).
 */

import { describe, expect, it } from 'vitest';
import { render, screen, within } from '@testing-library/react';
import { axe } from 'jest-axe';

import {
  ScoringPanelRenderer,
  type ScoringPanelRendererLabels,
  type ScoringPanelData,
} from '../ScoringPanelRenderer';

// ─── Shared fixture labels ─────────────────────────────────────────────────────

const LABELS: ScoringPanelRendererLabels = {
  points: {
    heading: 'Classifica',
    scoreAriaTemplate: 'Punteggio di {name}',
    leaderBadgeLabel: 'in testa',
  },
  ranking: {
    heading: 'Posizioni',
    rankAriaTemplate: 'Posizione di {name}',
    firstPlaceBadgeLabel: 'primo posto',
  },
  binaryWin: {
    heading: 'Esito',
    inProgressLabel: 'Partita in corso',
    winLabel: 'Vince',
    loseLabel: 'Perde',
    outcomeAriaTemplate: '{name}: {result}',
  },
  objectives: {
    heading: 'Obiettivi',
    completedAriaTemplate: 'Completati da {name}',
    doneAriaTemplate: '{label} (completato)',
    pendingAriaTemplate: '{label} (non completato)',
  },
};

// ─── Points ───────────────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — Points', () => {
  const data: ScoringPanelData = {
    kind: 'Points',
    players: [
      { id: 'p1', displayName: 'Marco', score: 42 },
      { id: 'p2', displayName: 'Sara', score: 57 },
      { id: 'p3', displayName: 'Luca', score: 31 },
    ],
  };

  it('renders the section heading', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByText('Classifica')).toBeInTheDocument();
  });

  it('renders all players in descending score order', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="scoring-row"]');
    expect(rows).toHaveLength(3);
    // First row should be Sara (57), second Marco (42), third Luca (31)
    expect(within(rows[0] as HTMLElement).getByText('Sara')).toBeInTheDocument();
    expect(within(rows[1] as HTMLElement).getByText('Marco')).toBeInTheDocument();
    expect(within(rows[2] as HTMLElement).getByText('Luca')).toBeInTheDocument();
  });

  it('applies leader highlight class to first-place row', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="scoring-row"]');
    expect(rows[0]?.className).toContain('bg-entity-session/10');
    expect(rows[1]?.className).not.toContain('bg-entity-session');
  });

  it('renders aria-label for each score using template', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByLabelText('Punteggio di Sara')).toBeInTheDocument();
    expect(screen.getByLabelText('Punteggio di Marco')).toBeInTheDocument();
  });

  it('renders the data-slot="scoring-panel-points" sentinel', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-panel-points"]')).not.toBeNull();
  });
});

// ─── Ranking ──────────────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — Ranking', () => {
  const data: ScoringPanelData = {
    kind: 'Ranking',
    players: [
      { id: 'p1', displayName: 'Marco', position: 2 },
      { id: 'p2', displayName: 'Sara', position: 1 },
      { id: 'p3', displayName: 'Luca', position: 3 },
    ],
  };

  it('renders the section heading', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByText('Posizioni')).toBeInTheDocument();
  });

  it('renders players sorted by position ascending', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="ranking-row"]');
    expect(rows).toHaveLength(3);
    expect(within(rows[0] as HTMLElement).getByText('Sara')).toBeInTheDocument();
    expect(within(rows[1] as HTMLElement).getByText('Marco')).toBeInTheDocument();
    expect(within(rows[2] as HTMLElement).getByText('Luca')).toBeInTheDocument();
  });

  it('applies leader highlight to first-place row', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="ranking-row"]');
    expect(rows[0]?.className).toContain('bg-entity-session/10');
    expect(rows[1]?.className).not.toContain('bg-entity-session');
  });

  it('renders rank aria-label via template for each player', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByLabelText(/Posizione di Sara/)).toBeInTheDocument();
    expect(screen.getByLabelText(/Posizione di Marco/)).toBeInTheDocument();
  });

  it('renders the data-slot="scoring-panel-ranking" sentinel', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-panel-ranking"]')).not.toBeNull();
  });
});

// ─── BinaryWin ────────────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — BinaryWin', () => {
  it('renders the section heading', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByText('Esito')).toBeInTheDocument();
  });

  it('renders win/lose outcome labels per player', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const rows = document.querySelectorAll('[data-slot="binary-win-row"]');
    expect(rows).toHaveLength(2);
    expect(screen.getByText('Vince')).toBeInTheDocument();
    expect(screen.getByText('Perde')).toBeInTheDocument();
  });

  it('renders in-progress banner when all players are undecided', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: false },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByRole('status')).toBeInTheDocument();
    expect(screen.getByText('Partita in corso')).toBeInTheDocument();
  });

  it('does NOT render in-progress banner when at least one winner is set', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('renders outcome aria-labels via template', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByLabelText('Marco: Vince')).toBeInTheDocument();
    expect(screen.getByLabelText('Sara: Perde')).toBeInTheDocument();
  });

  it('renders data-slot="scoring-panel-binary-win" sentinel', () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [{ id: 'p1', displayName: 'Marco', isWinner: false }],
    };
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-panel-binary-win"]')).not.toBeNull();
  });
});

// ─── Objectives ───────────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — Objectives', () => {
  const data: ScoringPanelData = {
    kind: 'Objectives',
    players: [
      { id: 'p1', displayName: 'Marco', completedObjectives: ['obj-1'] },
      { id: 'p2', displayName: 'Sara', completedObjectives: ['obj-1', 'obj-2'] },
    ],
    objectives: [
      { id: 'obj-1', label: 'Raccogliere 10 risorse', done: true },
      { id: 'obj-2', label: 'Costruire una fortezza', done: true },
      { id: 'obj-3', label: 'Liberare il villaggio', done: false },
    ],
  };

  it('renders the section heading', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    // First occurrence (heading)
    const headings = screen.getAllByText('Obiettivi');
    expect(headings.length).toBeGreaterThanOrEqual(1);
  });

  it('renders all objective items', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByText('Raccogliere 10 risorse')).toBeInTheDocument();
    expect(screen.getByText('Costruire una fortezza')).toBeInTheDocument();
    expect(screen.getByText('Liberare il villaggio')).toBeInTheDocument();
  });

  it('applies done styling (line-through) to completed objectives', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const doneItems = document.querySelectorAll('[data-slot="objective-item"]');
    // First two objectives are done
    const firstLabel = within(doneItems[0] as HTMLElement).getByText('Raccogliere 10 risorse');
    expect(firstLabel.className).toContain('line-through');
    // Third is pending
    const pendingLabel = within(doneItems[2] as HTMLElement).getByText('Liberare il villaggio');
    expect(pendingLabel.className).not.toContain('line-through');
  });

  it('renders done/pending aria-labels via templates', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByLabelText('Raccogliere 10 risorse (completato)')).toBeInTheDocument();
    expect(screen.getByLabelText('Liberare il villaggio (non completato)')).toBeInTheDocument();
  });

  it('renders per-player completion aria-labels', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(screen.getByLabelText('Completati da Marco')).toBeInTheDocument();
    expect(screen.getByLabelText('Completati da Sara')).toBeInTheDocument();
  });

  it('renders data-slot="scoring-panel-objectives" sentinel', () => {
    render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-panel-objectives"]')).not.toBeNull();
  });
});

// ─── axe AA accessibility ─────────────────────────────────────────────────────

describe('ScoringPanelRenderer — axe AA', () => {
  it('Points variant has 0 axe violations', async () => {
    const data: ScoringPanelData = {
      kind: 'Points',
      players: [
        { id: 'p1', displayName: 'Marco', score: 42 },
        { id: 'p2', displayName: 'Sara', score: 57 },
      ],
    };
    const { container } = render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('Ranking variant has 0 axe violations', async () => {
    const data: ScoringPanelData = {
      kind: 'Ranking',
      players: [
        { id: 'p1', displayName: 'Marco', position: 2 },
        { id: 'p2', displayName: 'Sara', position: 1 },
      ],
    };
    const { container } = render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('BinaryWin variant has 0 axe violations (undecided)', async () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: false },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    const { container } = render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('BinaryWin variant has 0 axe violations (decided)', async () => {
    const data: ScoringPanelData = {
      kind: 'BinaryWin',
      players: [
        { id: 'p1', displayName: 'Marco', isWinner: true },
        { id: 'p2', displayName: 'Sara', isWinner: false },
      ],
    };
    const { container } = render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('Objectives variant has 0 axe violations', async () => {
    const data: ScoringPanelData = {
      kind: 'Objectives',
      players: [{ id: 'p1', displayName: 'Marco', completedObjectives: ['obj-1'] }],
      objectives: [
        { id: 'obj-1', label: 'Raccogliere risorse', done: true },
        { id: 'obj-2', label: 'Costruire la base', done: false },
      ],
    };
    const { container } = render(<ScoringPanelRenderer data={data} labels={LABELS} />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});

// ─── Exhaustiveness ───────────────────────────────────────────────────────────

describe('ScoringPanelRenderer — exhaustiveness', () => {
  it('assertNever throws on unknown kind (TypeScript runtime guard)', () => {
    const invalid = { kind: 'UnknownScoreType', players: [] } as unknown as ScoringPanelData;
    expect(() => render(<ScoringPanelRenderer data={invalid} labels={LABELS} />)).toThrow(
      /unhandled scoring kind/
    );
  });
});
