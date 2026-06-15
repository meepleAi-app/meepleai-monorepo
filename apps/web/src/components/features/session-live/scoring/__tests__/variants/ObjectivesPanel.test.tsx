/**
 * ObjectivesPanel — read-side checklist for ScoreType=Objectives.
 *
 * Issue #2373 — sub-issue G5a of epic #2354.
 * Plan: docs/superpowers/plans/2026-06-15-issue-2373-scoring-panel-renderer.md §4 T5
 */

import { render, screen, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import type { ObjectivesPanelData } from '../../types';
import { ObjectivesPanel } from '../../variants/ObjectivesPanel';

// ─── Fixtures ────────────────────────────────────────────────────────────────

const LABELS = {
  title: 'Obiettivi',
  emptyMessage: 'Nessun obiettivo',
  /** Template "{done}/{total} completati" — component does .replace(). */
  completedCounterTemplate: '{done}/{total} completati',
  doneAriaLabel: 'Completato',
  pendingAriaLabel: 'Da completare',
  /** Template "Progresso {value}" — component does .replace(). */
  progressAriaLabelTemplate: 'Progresso {value}',
};

function makeData(overrides: Partial<ObjectivesPanelData> = {}): ObjectivesPanelData {
  return {
    scoringType: 'Objectives',
    objectives: [
      { id: 'o-1', label: 'Recluta 3 lavoratori', done: true },
      { id: 'o-2', label: 'Costruisci 2 edifici', done: false, progress: '1/2' },
      { id: 'o-3', label: 'Acquista 5 risorse', done: false },
    ],
    ...overrides,
  };
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('ObjectivesPanel — render shape', () => {
  it('renders data-slot="scoring-panel-objectives"', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('scoring-panel-objectives')).toBeInTheDocument();
  });

  it('renders panel title', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByText('Obiettivi')).toBeInTheDocument();
  });

  it('renders meta line when provided', () => {
    const data = makeData({ meta: 'Round 2 di 4' });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Round 2 di 4')).toBeInTheDocument();
  });

  it('omits meta line when undefined', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    expect(screen.queryByTestId('objectives-meta')).not.toBeInTheDocument();
  });

  it('renders one row per objective', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    expect(screen.getAllByRole('listitem')).toHaveLength(3);
  });
});

describe('ObjectivesPanel — completion counter', () => {
  it('renders "{done}/{total} completati" using template', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    expect(screen.getByTestId('objectives-counter')).toHaveTextContent('1/3 completati');
  });

  it('counter reflects 0 when no objectives done', () => {
    const data = makeData({
      objectives: [
        { id: 'o-1', label: 'A', done: false },
        { id: 'o-2', label: 'B', done: false },
      ],
    });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.getByTestId('objectives-counter')).toHaveTextContent('0/2 completati');
  });

  it('counter reflects all done when complete', () => {
    const data = makeData({
      objectives: [
        { id: 'o-1', label: 'A', done: true },
        { id: 'o-2', label: 'B', done: true },
      ],
    });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.getByTestId('objectives-counter')).toHaveTextContent('2/2 completati');
  });

  it('progress meter has role="progressbar" with valuenow/valuemax', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const bar = screen.getByRole('progressbar');
    expect(bar).toHaveAttribute('aria-valuenow', '1');
    expect(bar).toHaveAttribute('aria-valuemax', '3');
  });
});

describe('ObjectivesPanel — done vs pending rows', () => {
  it('done objective has data-done="true"', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Recluta 3 lavoratori').closest('li');
    expect(row).toHaveAttribute('data-done', 'true');
  });

  it('pending objective has data-done="false"', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Costruisci 2 edifici').closest('li');
    expect(row).toHaveAttribute('data-done', 'false');
  });

  it('done row label uses line-through styling', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const label = screen.getByText('Recluta 3 lavoratori');
    expect(label.className).toContain('line-through');
  });

  it('pending row label does NOT use line-through', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const label = screen.getByText('Costruisci 2 edifici');
    expect(label.className).not.toContain('line-through');
  });

  it('done row checkbox has aria-label "Completato"', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Recluta 3 lavoratori').closest('li');
    expect(within(row!).getByLabelText('Completato')).toBeInTheDocument();
  });

  it('pending row checkbox has aria-label "Da completare"', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Costruisci 2 edifici').closest('li');
    expect(within(row!).getByLabelText('Da completare')).toBeInTheDocument();
  });
});

describe('ObjectivesPanel — progress text', () => {
  it('renders progress fraction with font-mono', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const progress = screen.getByText('1/2');
    expect(progress.className).toContain('font-mono');
  });

  it('omits progress span when entry has no progress', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const row = screen.getByText('Recluta 3 lavoratori').closest('li');
    expect(within(row!).queryByTestId('objectives-progress')).not.toBeInTheDocument();
  });
});

describe('ObjectivesPanel — empty state', () => {
  it('renders empty message when objectives empty', () => {
    const data = makeData({ objectives: [] });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.getByText('Nessun obiettivo')).toBeInTheDocument();
  });

  it('does not render listitems when empty', () => {
    const data = makeData({ objectives: [] });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.queryByRole('listitem')).not.toBeInTheDocument();
  });

  it('does not render counter when empty', () => {
    const data = makeData({ objectives: [] });
    render(<ObjectivesPanel data={data} labels={LABELS} />);
    expect(screen.queryByTestId('objectives-counter')).not.toBeInTheDocument();
  });
});

describe('ObjectivesPanel — token discipline', () => {
  it('root container does NOT use raw HSL', () => {
    render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const panel = screen.getByTestId('scoring-panel-objectives');
    expect(panel.className).not.toMatch(/bg-\[hsl\(/);
  });

  it('does NOT use bg-white / text-gray-* / bg-slate-* utilities', () => {
    const { container } = render(<ObjectivesPanel data={makeData()} labels={LABELS} />);
    const html = container.innerHTML;
    expect(html).not.toMatch(/bg-white\b/);
    expect(html).not.toMatch(/text-gray-/);
    expect(html).not.toMatch(/bg-slate-/);
  });
});
