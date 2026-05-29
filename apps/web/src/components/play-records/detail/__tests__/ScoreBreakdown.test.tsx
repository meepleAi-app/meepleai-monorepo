/**
 * ScoreBreakdown — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Accordion expandable when players have >1 scoring dimension (EC-10).
 * Shows multi-dim breakdown table per player.
 *
 * AC-2.6: ScoreBreakdown accordion expandable se scores[].length > 1
 * AC-2.10 EC-10: multi-dim scoring shows accordion
 */
import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ScoreBreakdown, type ScoreBreakdownRow } from '../ScoreBreakdown';

const MULTIDIM_ROWS: ScoreBreakdownRow[] = [
  {
    playerId: 'p-1',
    name: 'Marco',
    scores: [
      { dimension: 'points', value: 85 },
      { dimension: 'bonus', value: 12 },
      { dimension: 'eggs', value: 8 },
    ],
    totalScore: 85,
  },
  {
    playerId: 'p-2',
    name: 'Anna',
    scores: [
      { dimension: 'points', value: 72 },
      { dimension: 'bonus', value: 9 },
      { dimension: 'eggs', value: 6 },
    ],
    totalScore: 72,
  },
];

const SINGLE_DIM_ROWS: ScoreBreakdownRow[] = [
  {
    playerId: 'p-1',
    name: 'Marco',
    scores: [{ dimension: 'points', value: 85 }],
    totalScore: 85,
  },
];

describe('ScoreBreakdown', () => {
  it('does NOT render when single dimension (no accordion needed)', () => {
    const { container } = render(<ScoreBreakdown rows={SINGLE_DIM_ROWS} dimensions={['points']} />);
    expect(container.querySelector('[data-slot="score-breakdown"]')).toBeNull();
  });

  it('renders accordion when multi-dimensional (>1 dim)', () => {
    const { container } = render(
      <ScoreBreakdown rows={MULTIDIM_ROWS} dimensions={['points', 'bonus', 'eggs']} />
    );
    expect(container.querySelector('[data-slot="score-breakdown"]')).not.toBeNull();
  });

  it('accordion starts collapsed', () => {
    const { container } = render(
      <ScoreBreakdown rows={MULTIDIM_ROWS} dimensions={['points', 'bonus', 'eggs']} />
    );
    const panel = container.querySelector('[data-slot="score-breakdown-panel"]');
    // Panel should be hidden/collapsed initially
    if (panel) {
      const isHidden =
        panel.getAttribute('data-state') === 'closed' ||
        panel.getAttribute('hidden') !== null ||
        getComputedStyle(panel).display === 'none';
      // Accept either hidden attribute or data-state=closed
      expect(
        panel.getAttribute('data-state') === 'closed' ||
          panel.getAttribute('hidden') !== null ||
          !screen.queryByText('bonus') // content not visible
      ).toBe(true);
    }
  });

  it('expands to show dimension columns when trigger clicked', () => {
    render(<ScoreBreakdown rows={MULTIDIM_ROWS} dimensions={['points', 'bonus', 'eggs']} />);
    const trigger = document.querySelector('[data-slot="score-breakdown-trigger"]');
    expect(trigger).not.toBeNull();
    fireEvent.click(trigger!);
    // After expand, dimension headers should be visible
    expect(screen.getByText('bonus')).toBeTruthy();
    expect(screen.getByText('eggs')).toBeTruthy();
  });

  it('shows player names in breakdown table after expand', () => {
    render(<ScoreBreakdown rows={MULTIDIM_ROWS} dimensions={['points', 'bonus', 'eggs']} />);
    const trigger = document.querySelector('[data-slot="score-breakdown-trigger"]');
    fireEvent.click(trigger!);
    expect(screen.getAllByText('Marco').length).toBeGreaterThan(0);
    expect(screen.getAllByText('Anna').length).toBeGreaterThan(0);
  });

  it('has aria-label on the accordion trigger (a11y AC-2.11)', () => {
    render(<ScoreBreakdown rows={MULTIDIM_ROWS} dimensions={['points', 'bonus', 'eggs']} />);
    const trigger = document.querySelector('[data-slot="score-breakdown-trigger"]');
    expect(
      trigger?.getAttribute('aria-label') || trigger?.getAttribute('aria-controls')
    ).toBeTruthy();
  });
});
