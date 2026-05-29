/**
 * Classifica — Task 2 (Issue #1488 / Epic #1475 Phase D).
 *
 * Ranked player list with avatar, progress bar relative to top, and score.
 *
 * AC-2.5: Classifica ranked by totalScore con avatar entity-tinted, barra progresso relativa al top
 * AC-2.7 EC-1 cooperative: no winner badge
 * AC-2.9 EC-4/5 spectator: banner neutrale "Vittoria di X"
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { Classifica, type ClassificaRow } from '../Classifica';

const ROWS_COMPETITIVE: ClassificaRow[] = [
  { playerId: 'p-1', userId: 'u-1', name: 'Marco', totalScore: 85, isWinner: true },
  { playerId: 'p-2', userId: 'u-2', name: 'Anna', totalScore: 72, isWinner: false },
  { playerId: 'p-3', userId: null, name: 'Guest 3', totalScore: 61, isWinner: false },
];

const ROWS_COOP: ClassificaRow[] = [
  { playerId: 'p-1', userId: 'u-1', name: 'Mario', totalScore: 0, isWinner: false },
  { playerId: 'p-2', userId: null, name: 'Luisa', totalScore: 0, isWinner: false },
];

describe('Classifica', () => {
  it('renders data-slot="classifica"', () => {
    const { container } = render(<Classifica rows={ROWS_COMPETITIVE} isCooperative={false} />);
    expect(container.querySelector('[data-slot="classifica"]')).not.toBeNull();
  });

  it('renders all players', () => {
    render(<Classifica rows={ROWS_COMPETITIVE} isCooperative={false} />);
    expect(screen.getByText('Marco')).toBeTruthy();
    expect(screen.getByText('Anna')).toBeTruthy();
    expect(screen.getByText('Guest 3')).toBeTruthy();
  });

  it('shows scores for all players', () => {
    render(<Classifica rows={ROWS_COMPETITIVE} isCooperative={false} />);
    expect(screen.getByText('85')).toBeTruthy();
    expect(screen.getByText('72')).toBeTruthy();
    expect(screen.getByText('61')).toBeTruthy();
  });

  it('shows winner badge for first place in competitive mode', () => {
    render(<Classifica rows={ROWS_COMPETITIVE} isCooperative={false} />);
    const winnerEl = document.querySelector('[data-slot="winner-badge"]');
    expect(winnerEl).not.toBeNull();
  });

  it('EC-1 cooperative: no winner badge rendered', () => {
    render(<Classifica rows={ROWS_COOP} isCooperative={true} />);
    const winnerEl = document.querySelector('[data-slot="winner-badge"]');
    expect(winnerEl).toBeNull();
  });

  it('renders progress bars relative to top score', () => {
    const { container } = render(<Classifica rows={ROWS_COMPETITIVE} isCooperative={false} />);
    const bars = container.querySelectorAll('[data-slot="score-bar"]');
    expect(bars.length).toBe(3);
    // First (highest) bar should be 100%
    const firstBar = bars[0] as HTMLElement;
    expect(firstBar.style.width).toBe('100%');
  });

  it('shows "—" for null totalScore', () => {
    const rowsWithNull: ClassificaRow[] = [
      {
        playerId: 'p-x',
        userId: 'u-x',
        name: 'Senza punteggio',
        totalScore: null,
        isWinner: false,
      },
    ];
    render(<Classifica rows={rowsWithNull} isCooperative={false} />);
    expect(screen.getByText('—')).toBeTruthy();
  });
});
