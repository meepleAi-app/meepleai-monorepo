/**
 * AgentDisputeTabContent — REST hydration tests (#3391, finding C8)
 *
 * The Arbitro tab previously read disputes ONLY from SignalR-populated store state, so on a
 * page reload the dispute history was empty until a new SignalR event arrived. These tests
 * exercise the real pipeline (REST client → hydration hook → live-session store → DisputeHistory)
 * to prove the history is restored on mount.
 *
 * Unlike AgentDisputeTabContent.test.tsx, this file uses the REAL store (not a mock) so the
 * hydration effect's setDisputes call is observable through the rendered DisputeHistory.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import type { ReactElement } from 'react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { AgentDisputeTabContent } from '../AgentDisputeTabContent';

import { useLiveSessionStore } from '@/lib/stores/live-session-store';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const getDisputesMock = vi.fn();

vi.mock('@/lib/api', () => ({
  api: {
    liveSessions: {
      submitDispute: vi.fn(),
      getDisputes: (sessionId: string) => getDisputesMock(sessionId),
    },
  },
}));

// ─── Helpers ──────────────────────────────────────────────────────────────────

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Anna' },
];

function renderContent(sessionId = 'sess-001'): ReturnType<typeof render> {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  const ui: ReactElement = (
    <QueryClientProvider client={queryClient}>
      <AgentDisputeTabContent sessionId={sessionId} players={PLAYERS} />
    </QueryClientProvider>
  );
  return render(ui);
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('AgentDisputeTabContent — REST hydration on reload (#3391)', () => {
  beforeEach(() => {
    useLiveSessionStore.getState().reset();
    getDisputesMock.mockReset();
    getDisputesMock.mockResolvedValue([]);
  });

  afterEach(() => {
    useLiveSessionStore.getState().reset();
  });

  it('hydrates the dispute history from REST on mount (store starts empty, as after reload)', async () => {
    getDisputesMock.mockResolvedValue([
      {
        id: 'd1',
        description: 'Può giocare questa carta?',
        verdict: 'No, non può.',
        ruleReferences: ['Rule 5.3'],
        raisedByPlayerName: 'Marco',
        timestamp: '2026-06-30T10:00:00Z',
      },
    ]);

    renderContent('sess-001');

    // DisputeHistory renders the collapsed toggle only when disputes exist → proves the store
    // was hydrated from the REST snapshot after mount (it started empty).
    expect(
      await screen.findByRole('button', { name: /Verdetti precedenti \(1\)/i })
    ).toBeInTheDocument();

    expect(getDisputesMock).toHaveBeenCalledWith('sess-001');
  });

  it('shows no history when the session has no persisted disputes', async () => {
    getDisputesMock.mockResolvedValue([]);

    renderContent('sess-002');

    // Give the query a chance to resolve, then assert the toggle never appears.
    // eslint-disable-next-line @typescript-eslint/no-unused-expressions
    await screen.findByRole('button', { name: /Arbitro/i }); // tab is rendered
    expect(screen.queryByRole('button', { name: /Verdetti precedenti/i })).not.toBeInTheDocument();
  });

  // Regression guard for the review finding: on a remount served by a stale/empty REST cache,
  // hydration must NOT drop a dispute already appended live via SignalR (store singleton).
  it('preserves a live SignalR-appended dispute when hydrating a REST snapshot that lacks it', async () => {
    // A dispute already in the store (as if addDispute fired from a SignalR 'DisputeResolved').
    useLiveSessionStore.getState().addDispute({
      id: 'live-1',
      description: 'Live dispute via SignalR',
      verdict: 'Sì.',
      ruleReferences: [],
      raisedByPlayerName: 'Alice',
      timestamp: '2026-06-30T12:05:00Z',
    });
    // REST snapshot with a DIFFERENT persisted dispute but NOT the live one (e.g. a cache
    // predating the SignalR append). A blind replace would drop 'live-1'; a merge keeps both.
    // Returning a distinct id makes the clobber observable stably (waiting on 'rest-1' proves
    // hydration applied before we assert 'live-1' survived).
    getDisputesMock.mockResolvedValue([
      {
        id: 'rest-1',
        description: 'Persisted dispute',
        verdict: 'No.',
        ruleReferences: ['p.1'],
        raisedByPlayerName: 'Bob',
        timestamp: '2026-06-30T12:00:00Z',
      },
    ]);

    renderContent('sess-003');

    // Wait until hydration has applied the REST snapshot...
    await waitFor(() =>
      expect(useLiveSessionStore.getState().disputes.map((d) => d.id)).toContain('rest-1')
    );
    // ...then the live SignalR dispute must still be present (merge, not blind replace).
    expect(useLiveSessionStore.getState().disputes.map((d) => d.id)).toContain('live-1');
  });
});
