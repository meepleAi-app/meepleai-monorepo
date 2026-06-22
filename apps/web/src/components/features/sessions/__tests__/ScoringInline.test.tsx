/**
 * ScoringInline unit tests — Wave D.1 (Issue #735).
 *
 * 8 tests:
 * 1. Renders data-slot="scoring-inline"
 * 2. Shows player names for each score entry
 * 3. Shows score values when scores are non-zero
 * 4. Hides score chips when all scores are zero (v1 carryover placeholder)
 * 5. Winner entry has 🏆 prefix
 * 6. Compact mode: shows max 3 + overflow indicator
 * 7. Returns null when scores array is empty
 * 8. Accepts custom className
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ScoringInline } from '../ScoringInline';
import type { ScoringInlineProps } from '../ScoringInline';

const LABELS: ScoringInlineProps['labels'] = {
  winnerAriaLabel: 'Punteggi giocatori',
  overflowTemplate: '+{count} altri',
};

const SCORES_WITH_VALUES = [
  { name: 'Marco', score: 89, winner: true },
  { name: 'Anna', score: 76 },
  { name: 'Luca', score: 64 },
] as const;

const SCORES_ALL_ZERO = [
  { name: 'Marco', score: 0 },
  { name: 'Anna', score: 0 },
  { name: 'Luca', score: 0 },
] as const;

describe('ScoringInline', () => {
  it('renders data-slot="scoring-inline"', () => {
    render(<ScoringInline scores={SCORES_WITH_VALUES} labels={LABELS} />);
    expect(document.querySelector('[data-slot="scoring-inline"]')).not.toBeNull();
  });

  it('shows player names for each score entry', () => {
    render(<ScoringInline scores={SCORES_WITH_VALUES} labels={LABELS} />);
    expect(screen.getByText('Marco')).toBeTruthy();
    expect(screen.getByText('Anna')).toBeTruthy();
    expect(screen.getByText('Luca')).toBeTruthy();
  });

  it('shows score chips when scores are non-zero', () => {
    render(<ScoringInline scores={SCORES_WITH_VALUES} labels={LABELS} />);
    expect(screen.getByText('89')).toBeTruthy();
    expect(screen.getByText('76')).toBeTruthy();
    expect(screen.getByText('64')).toBeTruthy();
  });

  it('hides score chips when all scores are zero (v1 schema carryover)', () => {
    render(<ScoringInline scores={SCORES_ALL_ZERO} labels={LABELS} />);
    // Names should still appear
    expect(screen.getByText('Marco')).toBeTruthy();
    // Score values "0" should NOT appear as chip text
    expect(screen.queryAllByText('0')).toHaveLength(0);
  });

  it('winner entry has 🏆 prefix aria-hidden element', () => {
    render(<ScoringInline scores={SCORES_WITH_VALUES} labels={LABELS} />);
    // The winner (Marco, winner:true) should have the trophy emoji
    const container = document.querySelector('[data-slot="scoring-inline"]')!;
    // Find text "🏆" in the container
    expect(container.textContent).toContain('🏆');
  });

  it('compact mode shows max 3 scores and overflow indicator for 5 entries', () => {
    const fiveScores = [
      { name: 'P1', score: 10, winner: true },
      { name: 'P2', score: 9 },
      { name: 'P3', score: 8 },
      { name: 'P4', score: 7 },
      { name: 'P5', score: 6 },
    ];
    render(<ScoringInline scores={fiveScores} compact labels={LABELS} />);
    // First 3 visible
    expect(screen.getByText('P1')).toBeTruthy();
    expect(screen.getByText('P2')).toBeTruthy();
    expect(screen.getByText('P3')).toBeTruthy();
    // P4, P5 not visible (overflow)
    expect(screen.queryByText('P4')).toBeNull();
    // overflow indicator: +2 altri
    expect(screen.getByText('+2 altri')).toBeTruthy();
  });

  it('returns null when scores array is empty', () => {
    const { container } = render(<ScoringInline scores={[]} labels={LABELS} />);
    expect(container.firstChild).toBeNull();
  });

  it('accepts custom className', () => {
    render(<ScoringInline scores={SCORES_WITH_VALUES} labels={LABELS} className="my-class" />);
    const el = document.querySelector('[data-slot="scoring-inline"]');
    expect(el!.classList.contains('my-class')).toBe(true);
  });
});
