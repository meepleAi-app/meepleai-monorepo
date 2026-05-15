/**
 * Tests for DayDetailDrawer (SP4 #1170 commit 2).
 */

import { fireEvent, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { GameNightVM } from '@/lib/game-nights/view-model';

import { DayDetailDrawer, type DayDetailDrawerLabels } from '../DayDetailDrawer';

const labels: DayDetailDrawerLabels = {
  title: '15 marzo 2026',
  subtitle: '3 serate in programma',
  close: 'Chiudi',
  addOnDay: '+ Aggiungi serata in questo giorno',
  monthAbbrev: 'Mar',
  card: {
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
      viewSummary: 'Vedi summary',
      reschedule: 'Riprogramma',
      accept: 'Partecipo',
      maybe: 'Forse',
    },
    monthAbbrev: 'Mar',
  },
};

function makeVM(id: string): GameNightVM {
  return {
    id,
    title: `Serata ${id}`,
    scheduledAtIso: '2026-03-15T20:30:00Z',
    day: 15,
    month: 2,
    year: 2026,
    timeLabel: '20:30',
    durationLabel: '',
    location: 'Casa',
    gameIds: [],
    playerIds: [],
    role: 'invited',
    statusKey: 'planned',
  };
}

describe('DayDetailDrawer', () => {
  it('renders nothing when open=false', () => {
    const { container } = render(
      <DayDetailDrawer open={false} day={15} events={[]} labels={labels} onClose={() => {}} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('renders dialog with aria-modal and aria-labelledby when open', () => {
    render(<DayDetailDrawer open day={15} events={[]} labels={labels} onClose={() => {}} />);
    const dialog = screen.getByRole('dialog');
    expect(dialog.getAttribute('aria-modal')).toBe('true');
    expect(dialog.getAttribute('aria-labelledby')).toBeTruthy();
  });

  it('renders one GameNightListCard per event', () => {
    render(
      <DayDetailDrawer
        open
        day={15}
        events={[makeVM('a'), makeVM('b'), makeVM('c')]}
        labels={labels}
        onClose={() => {}}
      />
    );
    expect(screen.getAllByTestId('game-nights-list-card')).toHaveLength(3);
  });

  it('invokes onClose when backdrop is clicked', () => {
    const onClose = vi.fn();
    render(<DayDetailDrawer open day={15} events={[]} labels={labels} onClose={onClose} />);
    fireEvent.click(screen.getByTestId('game-nights-day-detail-backdrop'));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('does NOT close when clicking inside the dialog', () => {
    const onClose = vi.fn();
    render(<DayDetailDrawer open day={15} events={[]} labels={labels} onClose={onClose} />);
    fireEvent.click(screen.getByRole('dialog'));
    expect(onClose).not.toHaveBeenCalled();
  });

  it('invokes onClose when close button is clicked', () => {
    const onClose = vi.fn();
    render(<DayDetailDrawer open day={15} events={[]} labels={labels} onClose={onClose} />);
    fireEvent.click(screen.getByRole('button', { name: 'Chiudi' }));
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('invokes onClose on Escape key', () => {
    const onClose = vi.fn();
    render(<DayDetailDrawer open day={15} events={[]} labels={labels} onClose={onClose} />);
    fireEvent.keyDown(window, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('traps Tab focus within the drawer when open', async () => {
    const user = userEvent.setup();
    render(
      <DayDetailDrawer
        open
        day={15}
        events={[makeVM('a'), makeVM('b')]}
        labels={labels}
        onClose={() => {}}
        onAddOnDay={() => {}}
      />
    );

    const closeBtn = screen.getByRole('button', { name: 'Chiudi' });
    expect(closeBtn).toHaveFocus();

    // Tab through all interactive elements; cycle should stay inside the dialog.
    for (let i = 0; i < 8; i++) {
      // eslint-disable-next-line no-await-in-loop
      await user.tab();
    }

    const dialog = screen.getByRole('dialog');
    expect(document.activeElement).toBeInstanceOf(HTMLElement);
    expect(dialog.contains(document.activeElement)).toBe(true);
  });

  it('renders "add on day" button only when onAddOnDay is provided', () => {
    const { rerender } = render(
      <DayDetailDrawer open day={15} events={[]} labels={labels} onClose={() => {}} />
    );
    expect(
      screen.queryByRole('button', { name: '+ Aggiungi serata in questo giorno' })
    ).not.toBeInTheDocument();
    rerender(
      <DayDetailDrawer
        open
        day={15}
        events={[]}
        labels={labels}
        onClose={() => {}}
        onAddOnDay={() => {}}
      />
    );
    expect(
      screen.getByRole('button', { name: '+ Aggiungi serata in questo giorno' })
    ).toBeInTheDocument();
  });
});
