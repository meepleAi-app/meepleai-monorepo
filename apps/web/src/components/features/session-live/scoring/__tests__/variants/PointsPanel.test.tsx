/**
 * PointsPanel — read-side leaderboard for ScoreType=Points.
 *
 * Issue #2373 — sub-issue G5a of epic #2354.
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T2
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { PointsPanelData } from '../../types';
import { PointsPanel } from '../../variants/PointsPanel';

// ─── Fixtures ────────────────────────────────────────────────────────────────

const LABELS = {
  title: 'Punteggi',
  emptyMessage: 'Nessun punteggio',
  leaderAriaSuffix: 'leader',
  categoriesTitle: 'Categorie',
  turnDeltaPrefix: '+',
};

function makeData(overrides: Partial<PointsPanelData> = {}): PointsPanelData {
  return {
    scoringType: 'Points',
    players: [
      { id: 'p-1', displayName: 'Marco', score: 35 },
      { id: 'p-2', displayName: 'Luca', score: 42 },
      { id: 'p-3', displayName: 'Anna', score: 18 },
    ],
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('PointsPanel — render shape', () => {
  it('renders data-slot="scoring-panel-points"', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('scoring-panel-points')).toBeInTheDocument();
  });

  it('renders panel title', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
  });

  it('renders players sorted desc by score (Luca → Marco → Anna)', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(3);
    expect(within(rows[0]).getByText('Luca')).toBeInTheDocument();
    expect(within(rows[1]).getByText('Marco')).toBeInTheDocument();
    expect(within(rows[2]).getByText('Anna')).toBeInTheDocument();
  });

  it('renders score values with tabular-nums class', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const scores = screen.getAllByTestId('points-score-value');
    expect(scores).toHaveLength(3);
    scores.forEach(s => expect(s).toHaveClass('tabular-nums'));
  });

  it('renders scores in the descending order matching sort', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const scores = screen.getAllByTestId('points-score-value');
    expect(scores.map(s => s.textContent)).toEqual(['42', '35', '18']);
  });
});

describe('PointsPanel — leader styling', () => {
  it('first row has data-leader="true"', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows[0]).toHaveAttribute('data-leader', 'true');
  });

  it('non-leader rows have data-leader="false"', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows[1]).toHaveAttribute('data-leader', 'false');
    expect(rows[2]).toHaveAttribute('data-leader', 'false');
  });

  it('leader row name uses text-entity-toolkit accent class', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    const leaderName = within(rows[0]).getByText('Luca');
    expect(leaderName.className).toContain('text-entity-toolkit');
  });
});

describe('PointsPanel — turnDelta badge', () => {
  it('renders +N badge when turnDelta > 0', () => {
    const data = makeData({
      players: [{ id: 'p-1', displayName: 'Marco', score: 35, turnDelta: 7 }],
    });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.getByText('+7')).toBeInTheDocument();
  });

  it('omits +N badge when turnDelta is undefined', () => {
    const data = makeData({
      players: [{ id: 'p-1', displayName: 'Marco', score: 35 }],
    });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.queryByTestId('points-turn-delta')).not.toBeInTheDocument();
  });

  it('omits +N badge when turnDelta is 0', () => {
    const data = makeData({
      players: [{ id: 'p-1', displayName: 'Marco', score: 35, turnDelta: 0 }],
    });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.queryByTestId('points-turn-delta')).not.toBeInTheDocument();
  });
});

describe('PointsPanel — categories breakdown', () => {
  it('renders categories title when categories.length > 0', () => {
    const data = makeData({
      categories: [
        { id: 'birds', label: 'Uccelli', computation: 'Count' },
        { id: 'eggs', label: 'Uova', computation: 'Sum' },
      ],
    });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Categorie')).toBeInTheDocument();
  });

  it('renders each category row with label + computation badge', () => {
    const data = makeData({
      categories: [
        { id: 'birds', label: 'Uccelli', computation: 'Count' },
        { id: 'eggs', label: 'Uova', computation: 'Sum' },
      ],
    });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Uccelli')).toBeInTheDocument();
    expect(screen.getByText('Uova')).toBeInTheDocument();
    expect(screen.getByText('Count')).toBeInTheDocument();
    expect(screen.getByText('Sum')).toBeInTheDocument();
  });

  it('hides categories block when categories array is undefined', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
  });

  it('hides categories block when categories array is empty', () => {
    const data = makeData({ categories: [] });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.queryByText('Categorie')).not.toBeInTheDocument();
  });
});

describe('PointsPanel — empty state', () => {
  it('renders empty message when players array is empty', () => {
    const data = makeData({ players: [] });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Nessun punteggio')).toBeInTheDocument();
  });

  it('does not render the player list when empty', () => {
    const data = makeData({ players: [] });
    render(<PointsPanel data={data} labels={LABELS} />);
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });
});

describe('PointsPanel — token discipline (CLAUDE.md § Token Canonicalization)', () => {
  it('root container does NOT use raw HSL', () => {
    render(<PointsPanel data={makeData()} labels={LABELS} />);
    const panel = screen.getByTestId('scoring-panel-points');
    expect(panel.className).not.toMatch(/bg-\[hsl\(/);
  });

  it('does NOT use bg-white / text-gray-* utilities', () => {
    const { container } = render(<PointsPanel data={makeData()} labels={LABELS} />);
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-white\b/);
    expect(html).not.toMatch(/text-gray-/);
    expect(html).not.toMatch(/bg-slate-/);
  });
});
