/**
 * Tests for GameNightListCard (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import type { GameNightVM, StatusKey, RoleKey } from '@/lib/game-nights/view-model';

import { GameNightListCard, type GameNightListCardLabels } from '../GameNightListCard';

const labels: GameNightListCardLabels = {
  status: {
    confirmed: 'Confermata',
    planned: 'Programmata',
    cancelled: 'Annullata',
    completed: 'Completata',
  },
  organizingBadge: 'Organizzo',
  participants: n => `${n} partecipanti`,
  cta: {
    edit: 'Modifica',
    viewSummary: 'Vedi summary →',
    reschedule: 'Riprogramma',
    accept: '✓ Partecipo',
    maybe: 'Forse',
  },
  monthAbbrev: 'Mar',
};

function makeVM(
  statusKey: StatusKey,
  role: RoleKey,
  overrides: Partial<GameNightVM> = {}
): GameNightVM {
  return {
    id: 'gn-1',
    title: 'Serata X',
    scheduledAtIso: '2026-03-15T20:30:00Z',
    day: 15,
    month: 2,
    year: 2026,
    timeLabel: '20:30',
    durationLabel: '',
    location: 'Casa Marco',
    gameIds: [],
    playerIds: [],
    role,
    statusKey,
    ...overrides,
  };
}

describe('GameNightListCard', () => {
  it('renders title, date, time, location, status pill', () => {
    render(<GameNightListCard vm={makeVM('planned', 'invited')} labels={labels} />);
    expect(screen.getByRole('heading', { level: 3 })).toHaveTextContent('Serata X');
    expect(screen.getByText('Mar')).toBeInTheDocument();
    expect(screen.getByText('15')).toBeInTheDocument();
    expect(screen.getByText('20:30')).toBeInTheDocument();
    expect(screen.getByText('Casa Marco')).toBeInTheDocument();
    expect(screen.getByText('Programmata')).toBeInTheDocument();
  });

  it('applies line-through on cancelled title', () => {
    render(<GameNightListCard vm={makeVM('cancelled', 'invited')} labels={labels} />);
    expect(screen.getByRole('heading', { level: 3 }).className).toContain('line-through');
  });

  it('shows organizing badge when role=organizer', () => {
    render(<GameNightListCard vm={makeVM('confirmed', 'organizer')} labels={labels} />);
    expect(screen.getByText('Organizzo')).toBeInTheDocument();
  });

  it('does NOT show organizing badge when role=invited', () => {
    render(<GameNightListCard vm={makeVM('confirmed', 'invited')} labels={labels} />);
    expect(screen.queryByText('Organizzo')).not.toBeInTheDocument();
  });

  it('shows game chip when gameTitle provided', () => {
    render(
      <GameNightListCard vm={makeVM('planned', 'invited')} labels={labels} gameTitle="Catan" />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
  });

  it('does NOT show game chip when gameTitle missing', () => {
    render(<GameNightListCard vm={makeVM('planned', 'invited')} labels={labels} />);
    expect(screen.queryByText('Catan')).not.toBeInTheDocument();
  });

  describe('CTA branches', () => {
    it('completed → viewSummary', () => {
      const onAction = vi.fn();
      render(
        <GameNightListCard
          vm={makeVM('completed', 'invited')}
          labels={labels}
          onAction={onAction}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Vedi summary →' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'viewSummary');
    });

    it('cancelled → reschedule', () => {
      const onAction = vi.fn();
      render(
        <GameNightListCard
          vm={makeVM('cancelled', 'organizer')}
          labels={labels}
          onAction={onAction}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Riprogramma' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'reschedule');
    });

    it('confirmed + organizer → edit', () => {
      const onAction = vi.fn();
      render(
        <GameNightListCard
          vm={makeVM('confirmed', 'organizer')}
          labels={labels}
          onAction={onAction}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Modifica' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'edit');
    });

    it('planned + invited → accept + maybe', () => {
      const onAction = vi.fn();
      render(
        <GameNightListCard vm={makeVM('planned', 'invited')} labels={labels} onAction={onAction} />
      );
      fireEvent.click(screen.getByRole('button', { name: '✓ Partecipo' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'accept');
      fireEvent.click(screen.getByRole('button', { name: 'Forse' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'maybe');
    });
  });
});
