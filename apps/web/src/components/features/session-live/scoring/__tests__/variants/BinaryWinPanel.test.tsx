/**
 * BinaryWinPanel — read-side collective outcome for ScoreType=BinaryWin.
 *
 * Issue #2373 — sub-issue G5a of epic #2354.
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T4
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { BinaryWinPanelData } from '../../types';
import { BinaryWinPanel } from '../../variants/BinaryWinPanel';

// ─── Fixtures ────────────────────────────────────────────────────────────────

const LABELS = {
  title: 'Esito collettivo',
  emptyMessage: 'Nessun esito',
  categoriesTitle: 'Condizioni',
  weightWinLabel: 'vince',
  weightLoseLabel: 'perde',
  weightNeutralLabel: 'neutro',
  /** Template "{value}/{max}" — component does .replace(). */
  meterAriaLabelTemplate: 'Progresso {value} su {max}',
};

function makeData(overrides: Partial<BinaryWinPanelData> = {}): BinaryWinPanelData {
  return {
    scoringType: 'BinaryWin',
    collective: {
      goalLabel: 'Cure trovate',
      goalValue: 2,
      goalMax: 4,
      goalHint: 'Servono 4 cure per vincere',
      failLabel: 'Focolai',
      failValue: 5,
      failMax: 8,
      failHint: '8 focolai = sconfitta',
    },
    categories: [
      { id: 'cures', label: 'Cure', computation: 'Count', weight: 1 },
      { id: 'epidemics', label: 'Epidemie', computation: 'Count', weight: -1 },
      { id: 'researchers', label: 'Ricercatori', computation: 'Sum', weight: 0 },
    ],
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('BinaryWinPanel — render shape', () => {
  it('renders data-slot="scoring-panel-binarywin"', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('scoring-panel-binarywin')).toBeInTheDocument();
  });

  it('renders panel title', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Esito collettivo')).toBeInTheDocument();
  });

  it('renders both goal and fail meter blocks', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('binarywin-goal-meter')).toBeInTheDocument();
    expect(screen.getByTestId('binarywin-fail-meter')).toBeInTheDocument();
  });
});

describe('BinaryWinPanel — meter values', () => {
  it('goal meter shows value/max', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const meter = screen.getByTestId('binarywin-goal-meter');
    expect(within(meter).getByText('Cure trovate')).toBeInTheDocument();
    expect(within(meter).getByText('2/4')).toBeInTheDocument();
  });

  it('fail meter shows value/max', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const meter = screen.getByTestId('binarywin-fail-meter');
    expect(within(meter).getByText('Focolai')).toBeInTheDocument();
    expect(within(meter).getByText('5/8')).toBeInTheDocument();
  });

  it('renders hints when provided', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Servono 4 cure per vincere')).toBeInTheDocument();
    expect(screen.getByText('8 focolai = sconfitta')).toBeInTheDocument();
  });

  it('omits hint text when missing', () => {
    const data = makeData({
      collective: {
        goalLabel: 'Goal',
        goalValue: 0,
        goalMax: 1,
        failLabel: 'Fail',
        failValue: 0,
        failMax: 1,
      },
    });
    render(<BinaryWinPanel data={data} labels={LABELS} />);
    expect(screen.queryByTestId('binarywin-goal-hint')).not.toBeInTheDocument();
    expect(screen.queryByTestId('binarywin-fail-hint')).not.toBeInTheDocument();
  });

  it('goal meter uses entity-toolkit accent on the bar', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const bar = screen.getByTestId('binarywin-goal-bar');
    expect(bar.className).toContain('bg-entity-toolkit');
  });

  it('fail meter uses entity-event accent on the bar', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const bar = screen.getByTestId('binarywin-fail-bar');
    expect(bar.className).toContain('bg-entity-event');
  });
});

describe('BinaryWinPanel — meter ARIA progressbar contract', () => {
  it('goal meter has role="progressbar" + aria-valuenow / aria-valuemax', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const bars = screen.getAllByRole('progressbar');
    const goal = bars.find(b => b.getAttribute('aria-valuenow') === '2');
    expect(goal).toBeDefined();
    expect(goal).toHaveAttribute('aria-valuemax', '4');
    expect(goal).toHaveAttribute('aria-valuemin', '0');
  });

  it('fail meter has role="progressbar" + aria-valuenow=5 aria-valuemax=8', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const bars = screen.getAllByRole('progressbar');
    const fail = bars.find(b => b.getAttribute('aria-valuenow') === '5');
    expect(fail).toBeDefined();
    expect(fail).toHaveAttribute('aria-valuemax', '8');
  });
});

describe('BinaryWinPanel — categories conditions list', () => {
  it('renders categories title when categories.length > 0', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Condizioni')).toBeInTheDocument();
  });

  it('renders each category label', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Cure')).toBeInTheDocument();
    expect(screen.getByText('Epidemie')).toBeInTheDocument();
    expect(screen.getByText('Ricercatori')).toBeInTheDocument();
  });

  it('weight > 0 → renders "vince" badge', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Cure').closest('li');
    expect(row).not.toBeNull();
    expect(within(row!).getByText('vince')).toBeInTheDocument();
  });

  it('weight < 0 → renders "perde" badge', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Epidemie').closest('li');
    expect(row).not.toBeNull();
    expect(within(row!).getByText('perde')).toBeInTheDocument();
  });

  it('weight === 0 → renders "neutro" badge', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Ricercatori').closest('li');
    expect(row).not.toBeNull();
    expect(within(row!).getByText('neutro')).toBeInTheDocument();
  });

  it('hides categories block when categories array empty', () => {
    const data = makeData({ categories: [] });
    render(<BinaryWinPanel data={data} labels={LABELS} />);
    expect(screen.queryByText('Condizioni')).not.toBeInTheDocument();
  });
});

describe('BinaryWinPanel — token discipline', () => {
  it('root container does NOT use raw HSL', () => {
    render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const panel = screen.getByTestId('scoring-panel-binarywin');
    expect(panel.className).not.toMatch(/bg-\[hsl\(/);
  });

  it('does NOT use bg-white / text-gray-* / bg-slate-* utilities', () => {
    const { container } = render(<BinaryWinPanel data={makeData()} labels={LABELS} />);
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-white\b/);
    expect(html).not.toMatch(/text-gray-/);
    expect(html).not.toMatch(/bg-slate-/);
  });
});
