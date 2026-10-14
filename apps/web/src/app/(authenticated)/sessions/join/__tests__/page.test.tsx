/**
 * Join Session Page — #4156: a live-session code opens the live session page; the fallback is a
 * SessionTracking session (the games of a game night, D7), which has no page under
 * `/sessions/{id}` and does not carry the night id, so the page confirms the join instead of
 * navigating to a 404.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import JoinSessionPage from '../page';

const routerPush = vi.hoisted(() => vi.fn());
const getLiveByCode = vi.hoisted(() => vi.fn());
const joinTrackingByCode = vi.hoisted(() => vi.fn());
const getTrackingByCode = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: routerPush }) }));
vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: { getByCode: getLiveByCode },
    sessionTracking: { joinByCode: joinTrackingByCode, getByCode: getTrackingByCode },
  },
}));

function join(code: string) {
  render(<JoinSessionPage />);
  fireEvent.change(screen.getByPlaceholderText('Es. K7X2M9'), { target: { value: code } });
  fireEvent.click(screen.getByRole('button', { name: 'Unisciti' }));
}

describe('JoinSessionPage — #4156', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('opens the live session page for a live-session code', async () => {
    getLiveByCode.mockResolvedValue({ id: 'live-1' });
    join('ABC123');
    await waitFor(() => expect(routerPush).toHaveBeenCalledWith('/sessions/live-1'));
    expect(joinTrackingByCode).not.toHaveBeenCalled();
  });

  it('confirms the join of a game-night session instead of opening /sessions/{id}', async () => {
    getLiveByCode.mockRejectedValue(new Error('not a live session'));
    joinTrackingByCode.mockResolvedValue(undefined);
    getTrackingByCode.mockResolvedValue({ id: 'tracking-1' });
    join('XYZ789');
    expect(await screen.findByTestId('joined-night-session')).toBeInTheDocument();
    expect(joinTrackingByCode).toHaveBeenCalledWith('XYZ789');
    expect(routerPush).not.toHaveBeenCalled();
    expect(screen.getByRole('link', { name: 'Vai alle serate' })).toHaveAttribute(
      'href',
      '/game-nights'
    );
  });

  it('reports a code that matches neither kind of session', async () => {
    getLiveByCode.mockRejectedValue(new Error('not a live session'));
    joinTrackingByCode.mockRejectedValue(new Error('not found'));
    join('NOPE00');
    expect(
      await screen.findByText('Sessione non trovata. Controlla il codice e riprova.')
    ).toBeInTheDocument();
    expect(routerPush).not.toHaveBeenCalled();
  });
});
