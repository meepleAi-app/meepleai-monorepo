/**
 * RankingPanel — read-side ordered ranking for ScoreType=Ranking.
 *
 * Issue #2373 — sub-issue G5a of epic #2354.
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T3
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { RankingPanelData } from '../../types';
import { RankingPanel } from '../../variants/RankingPanel';

// ─── Fixtures ────────────────────────────────────────────────────────────────

const LABELS = {
  title: 'Classifica',
  emptyMessage: 'Nessuna classifica',
  leaderAriaSuffix: 'vincitore',
  rankAriaLabelTemplate: 'Posizione {rank}',
  trophyAriaLabel: 'Trofeo',
};

function makeData(overrides: Partial<RankingPanelData> = {}): RankingPanelData {
  return {
    scoringType: 'Ranking',
    ranking: [
      { id: 'p-1', displayName: 'Luca', rank: 1, sub: '42 punti' },
      { id: 'p-2', displayName: 'Marco', rank: 2, sub: '35 punti' },
      { id: 'p-3', displayName: 'Anna', rank: 3, sub: '18 punti' },
    ],
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('RankingPanel — render shape', () => {
  it('renders data-slot="scoring-panel-ranking"', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('scoring-panel-ranking')).toBeInTheDocument();
  });

  it('renders panel title', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Classifica')).toBeInTheDocument();
  });

  it('renders meta line when provided', () => {
    const data = makeData({ meta: 'Round 3 di 5' });
    render(<RankingPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Round 3 di 5')).toBeInTheDocument();
  });

  it('omits meta line when undefined', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.queryByTestId('ranking-meta')).not.toBeInTheDocument();
  });

  it('renders ranking entries in the order provided (rank ASC)', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows).toHaveLength(3);
    expect(within(rows[0]).getByText('Luca')).toBeInTheDocument();
    expect(within(rows[1]).getByText('Marco')).toBeInTheDocument();
    expect(within(rows[2]).getByText('Anna')).toBeInTheDocument();
  });
});

describe('RankingPanel — rank pills', () => {
  it('renders rank number for each row', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const pills = screen.getAllByTestId('ranking-rank-pill');
    expect(pills).toHaveLength(3);
    expect(pills.map(p => p.textContent)).toEqual(['1', '2', '3']);
  });

  it('leader pill (rank=1) uses entity-toolkit accent + Trophy icon', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    const leaderPill = within(rows[0]).getByTestId('ranking-rank-pill');
    expect(leaderPill.className).toContain('bg-entity-toolkit');
    expect(within(rows[0]).getByLabelText('Trofeo')).toBeInTheDocument();
  });

  it('non-leader pills use muted styling without Trophy icon', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    const secondPill = within(rows[1]).getByTestId('ranking-rank-pill');
    expect(secondPill.className).not.toContain('bg-entity-toolkit');
    expect(within(rows[1]).queryByLabelText('Trofeo')).not.toBeInTheDocument();
  });
});

describe('RankingPanel — data-leader + sub line', () => {
  it('leader row has data-leader="true"', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows[0]).toHaveAttribute('data-leader', 'true');
  });

  it('non-leader rows have data-leader="false"', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const rows = screen.getAllByRole('listitem');
    expect(rows[1]).toHaveAttribute('data-leader', 'false');
    expect(rows[2]).toHaveAttribute('data-leader', 'false');
  });

  it('renders sub line when present on the entry', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('42 punti')).toBeInTheDocument();
    expect(screen.getByText('35 punti')).toBeInTheDocument();
  });

  it('omits sub line when entry has no sub', () => {
    const data = makeData({
      ranking: [{ id: 'p-1', displayName: 'Luca', rank: 1 }],
    });
    render(<RankingPanel data={data} labels={LABELS} />);
    expect(screen.queryByTestId('ranking-sub')).not.toBeInTheDocument();
  });
});

describe('RankingPanel — aria + a11y', () => {
  it('rank pill aria-label uses template', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByLabelText('Posizione 1')).toBeInTheDocument();
    expect(screen.getByLabelText('Posizione 2')).toBeInTheDocument();
    expect(screen.getByLabelText('Posizione 3')).toBeInTheDocument();
  });

  it('panel has aria-label matching title', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByRole('region', { name: 'Classifica' })).toBeInTheDocument();
  });
});

describe('RankingPanel — empty state', () => {
  it('renders empty message when ranking is empty', () => {
    const data = makeData({ ranking: [] });
    render(<RankingPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Nessuna classifica')).toBeInTheDocument();
  });

  it('does not render listitems when empty', () => {
    const data = makeData({ ranking: [] });
    render(<RankingPanel data={data} labels={LABELS} />);
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });
});

describe('RankingPanel — token discipline (CLAUDE.md § Token Canonicalization)', () => {
  it('root container does NOT use raw HSL', () => {
    render(<RankingPanel data={makeData()} labels={LABELS} />);
    const panel = screen.getByTestId('scoring-panel-ranking');
    expect(panel.className).not.toMatch(/bg-\[hsl\(/);
  });

  it('does NOT use bg-white / text-gray-* / bg-slate-* utilities', () => {
    const { container } = render(<RankingPanel data={makeData()} labels={LABELS} />);
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-white\b/);
    expect(html).not.toMatch(/text-gray-/);
    expect(html).not.toMatch(/bg-slate-/);
  });
});
