/**
 * @vitest-environment jsdom
 *
 * SerataResumeButton unit tests — SI-4 (#2635, step 3).
 *
 * On the gamebook play page, when a campaign is attached to a resumable game-night, the organizer
 * can open a NEW live sitting via Attach. Covers the pure gating predicate + the CTA behaviour
 * (success routing, distinct max-live 409 / 403 feedback).
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { ConflictError, ForbiddenError } from '@/lib/api/core/errors';
import type { GamebookCampaignSpine } from '@/lib/api/gamebook-campaigns';
import { MAX_LIVE_SESSIONS_EXCEEDED } from '@/lib/game-nights/hooks/useStartNextGame';

import { SerataResumeButton, isSerataResumable } from '../SerataResumeButton';

const pushMock = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({ useRouter: () => ({ push: pushMock }) }));

const attachMock = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/gameNightSessionClient', () => ({
  gameNightSessionClient: { attachGamebookCampaign: attachMock },
}));

const ORG = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const OTHER = 'cccccccc-cccc-4ccc-8ccc-cccccccccccc';
const GN = 'dddddddd-dddd-4ddd-8ddd-dddddddddddd';
const CAMPAIGN = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

function spine(overrides: Partial<GamebookCampaignSpine> = {}): GamebookCampaignSpine {
  return {
    gameNightId: GN,
    gameNightTitle: 'Serata da Marco',
    organizerId: ORG,
    gameNightStatus: 'InProgress',
    totalSessions: 1,
    completedSessions: 1,
    hasLiveSession: false,
    campaignStatus: 'Resumable',
    ...overrides,
  };
}

function renderButton() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <SerataResumeButton gameNightId={GN} campaignId={CAMPAIGN} />
    </QueryClientProvider>
  );
}

describe('isSerataResumable', () => {
  it('is true for the organizer on a resumable night with no live session', () => {
    expect(isSerataResumable(spine(), ORG)).toBe(true);
    expect(isSerataResumable(spine({ gameNightStatus: 'Published' }), ORG)).toBe(true);
  });

  it('is false when the viewer is not the organizer', () => {
    expect(isSerataResumable(spine(), OTHER)).toBe(false);
  });

  it('is false when a live session already exists', () => {
    expect(isSerataResumable(spine({ hasLiveSession: true }), ORG)).toBe(false);
  });

  it('is false for a terminal / non-resumable night status', () => {
    expect(isSerataResumable(spine({ gameNightStatus: 'Completed' }), ORG)).toBe(false);
    expect(isSerataResumable(spine({ gameNightStatus: 'Cancelled' }), ORG)).toBe(false);
    expect(isSerataResumable(spine({ gameNightStatus: 'Draft' }), ORG)).toBe(false);
  });

  it('is false for a null spine or a missing viewer id', () => {
    expect(isSerataResumable(null, ORG)).toBe(false);
    expect(isSerataResumable(spine(), undefined)).toBe(false);
  });
});

describe('SerataResumeButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('opens a live sitting and routes to the new session on success', async () => {
    attachMock.mockResolvedValue({
      sessionId: 's99',
      gameNightSessionId: 'gns',
      sessionCode: 'ABC',
      playOrder: 2,
    });
    renderButton();

    fireEvent.click(screen.getByRole('button', { name: /riprendi la serata/i }));

    await waitFor(() => expect(attachMock).toHaveBeenCalledWith(GN, CAMPAIGN));
    await waitFor(() => expect(pushMock).toHaveBeenCalledWith('/sessions/s99'));
  });

  it('shows a distinct message on a max-live 409 and does not route', async () => {
    attachMock.mockRejectedValue(
      new ConflictError({ message: 'blocked', code: MAX_LIVE_SESSIONS_EXCEEDED })
    );
    renderButton();

    fireEvent.click(screen.getByRole('button', { name: /riprendi la serata/i }));

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(/già una partita live/i)
    );
    expect(pushMock).not.toHaveBeenCalled();
  });

  it('shows a distinct message on a 403 (non-organizer) and does not route', async () => {
    attachMock.mockRejectedValue(new ForbiddenError({ message: 'nope' }));
    renderButton();

    fireEvent.click(screen.getByRole('button', { name: /riprendi la serata/i }));

    await waitFor(() => expect(screen.getByRole('alert')).toHaveTextContent(/organizzatore/i));
    expect(pushMock).not.toHaveBeenCalled();
  });
});
