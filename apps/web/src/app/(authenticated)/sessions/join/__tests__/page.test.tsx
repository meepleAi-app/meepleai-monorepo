/**
 * Join Session Page — #4156: a live-session code opens the live session page; the fallback is a
 * SessionTracking session (the games of a game night, D7), which has no page under
 * `/sessions/{id}` and does not carry the night id, so the page confirms the join instead of
 * navigating to a 404.
 */

import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { RateLimitError } from '@/lib/api/core/errors';

import JoinSessionPage from '../page';

const routerPush = vi.hoisted(() => vi.fn());
// #4939: the page reads the PUBLIC lobby (id and nothing of the players' identities); the full session by
// code is for participants only, and whoever is on this page is not one yet.
const getLiveByCode = vi.hoisted(() => vi.fn());
const getLiveFullByCode = vi.hoisted(() => vi.fn());
const joinTrackingByCode = vi.hoisted(() => vi.fn());

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: routerPush }) }));
vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: { getPublicByCode: getLiveByCode, getByCode: getLiveFullByCode },
    sessionTracking: { joinByCode: joinTrackingByCode },
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

  it('never asks for the full session by code: that read is for participants (#4939)', async () => {
    getLiveByCode.mockResolvedValue({ id: 'live-1' });
    join('ABC123');
    await waitFor(() => expect(routerPush).toHaveBeenCalled());
    expect(getLiveFullByCode).not.toHaveBeenCalled();
  });

  it('confirms the join of a game-night session instead of opening /sessions/{id}', async () => {
    getLiveByCode.mockRejectedValue(new Error('not a live session'));
    joinTrackingByCode.mockResolvedValue(undefined);
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

  // #4912: the code routes answer 429 past the per-user limit. That is not a wrong code, and the
  // fallback must not spend the join bucket on a guess the server has already refused to look at.
  it('a throttled lookup says how long to wait and does not try the fallback', async () => {
    getLiveByCode.mockRejectedValue(
      new RateLimitError({ message: 'Rate limit exceeded', retryAfter: 42 })
    );
    join('ABC123');
    expect(
      await screen.findByText('Troppi tentativi: riprova tra 42 secondi.')
    ).toBeInTheDocument();
    expect(joinTrackingByCode).not.toHaveBeenCalled();
    expect(routerPush).not.toHaveBeenCalled();
  });

  it('a throttled join is not reported as a code that does not exist', async () => {
    getLiveByCode.mockRejectedValue(new Error('not a live session'));
    joinTrackingByCode.mockRejectedValue(new RateLimitError({ message: 'Rate limit exceeded' }));
    join('XYZ789');
    expect(
      await screen.findByText('Troppi tentativi: aspetta un minuto prima di riprovare.')
    ).toBeInTheDocument();
    expect(screen.queryByText(/Sessione non trovata/)).not.toBeInTheDocument();
  });
});
