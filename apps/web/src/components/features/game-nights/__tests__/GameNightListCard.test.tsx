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
    decline: 'Declina',
  },
  pendingBadge: 'Da confermare',
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
    myRsvpStatus: null,
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

    it('invited with an RSVP → 3 RSVP buttons (accept/maybe/decline) all wired', () => {
      const onAction = vi.fn();
      render(
        <GameNightListCard
          vm={makeVM('planned', 'invited', { myRsvpStatus: 'Pending' })}
          labels={labels}
          onAction={onAction}
        />
      );
      fireEvent.click(screen.getByRole('button', { name: '✓ Partecipo' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'accept');
      fireEvent.click(screen.getByRole('button', { name: 'Forse' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'maybe');
      fireEvent.click(screen.getByRole('button', { name: 'Declina' }));
      expect(onAction).toHaveBeenCalledWith('gn-1', 'decline');
    });
  });

  // #2978 (invariante #17): pending-invitee treatment + non-pending RSVP wiring.
  describe('pending-invitee treatment (#2978)', () => {
    it('shows the "Da confermare" badge when invited and RSVP is Pending', () => {
      render(
        <GameNightListCard
          vm={makeVM('planned', 'invited', { myRsvpStatus: 'Pending' })}
          labels={labels}
        />
      );
      expect(screen.getByTestId('game-nights-pending-badge')).toHaveTextContent('Da confermare');
    });

    it('marks the card data-pending=true when invited and RSVP is Pending', () => {
      render(
        <GameNightListCard
          vm={makeVM('planned', 'invited', { myRsvpStatus: 'Pending' })}
          labels={labels}
        />
      );
      expect(screen.getByTestId('game-nights-list-card')).toHaveAttribute('data-pending', 'true');
    });

    it('drops the pending badge once the invitee has confirmed (Accepted)', () => {
      render(
        <GameNightListCard
          vm={makeVM('planned', 'invited', { myRsvpStatus: 'Accepted' })}
          labels={labels}
        />
      );
      expect(screen.queryByTestId('game-nights-pending-badge')).not.toBeInTheDocument();
      expect(screen.getByTestId('game-nights-list-card')).toHaveAttribute('data-pending', 'false');
    });

    it('marks the current RSVP response as selected', () => {
      render(
        <GameNightListCard
          vm={makeVM('planned', 'invited', { myRsvpStatus: 'Accepted' })}
          labels={labels}
        />
      );
      expect(screen.getByRole('button', { name: '✓ Partecipo' })).toHaveAttribute(
        'data-selected',
        'true'
      );
    });

    it('shows no RSVP buttons for a non-invitee (myRsvpStatus null)', () => {
      render(<GameNightListCard vm={makeVM('planned', 'invited')} labels={labels} />);
      expect(screen.queryByRole('button', { name: '✓ Partecipo' })).not.toBeInTheDocument();
    });

    it('shows no pending treatment for the organizer', () => {
      render(<GameNightListCard vm={makeVM('planned', 'organizer')} labels={labels} />);
      expect(screen.queryByTestId('game-nights-pending-badge')).not.toBeInTheDocument();
    });
  });
});
