/**
 * GameKbWarning Tests — Game Night MVP Hardening Task 1.3
 *
 * Verifies the soft-filter warning component that informs users a game's PDF
 * is not indexed (so the AI agent is unavailable). The warning is informational
 * only — users can still proceed with the session.
 */

import { createElement, type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useGameKbStatus } from '@/lib/domain-hooks/useGameKbStatus';

import { GameKbWarning } from '../GameKbBadge';

vi.mock('@/lib/domain-hooks/useGameKbStatus');

function wrap(children: ReactNode): ReactNode {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return createElement(QueryClientProvider, { client: qc }, children);
}

describe('GameKbWarning', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders warning when game is not indexed', () => {
    vi.mocked(useGameKbStatus).mockReturnValue({
      isIndexed: false,
      isLoading: false,
      documentCount: 0,
      coverageLevel: 'None',
      error: null,
    });

    render(wrap(createElement(GameKbWarning, { gameId: 'g1' })));

    expect(screen.getByTestId('kb-warning')).toBeInTheDocument();
    expect(screen.getByText(/Agente AI non disponibile/)).toBeInTheDocument();
    expect(screen.getByText(/il PDF di questo gioco non è ancora indicizzato/)).toBeInTheDocument();
    expect(
      screen.getByText(/Puoi comunque iniziare la sessione e usare gli strumenti manuali/)
    ).toBeInTheDocument();
  });

  it('renders with role="alert" for accessibility', () => {
    vi.mocked(useGameKbStatus).mockReturnValue({
      isIndexed: false,
      isLoading: false,
      documentCount: 0,
      coverageLevel: 'None',
      error: null,
    });

    render(wrap(createElement(GameKbWarning, { gameId: 'g1' })));

    expect(screen.getByRole('alert')).toBeInTheDocument();
  });

  it('renders nothing when game is indexed', () => {
    vi.mocked(useGameKbStatus).mockReturnValue({
      isIndexed: true,
      isLoading: false,
      documentCount: 3,
      coverageLevel: 'Standard',
      error: null,
    });

    const { container } = render(wrap(createElement(GameKbWarning, { gameId: 'g1' })));
    expect(container.firstChild).toBeNull();
  });

  it('renders nothing while loading', () => {
    vi.mocked(useGameKbStatus).mockReturnValue({
      isIndexed: false,
      isLoading: true,
      documentCount: 0,
      coverageLevel: 'None',
      error: null,
    });

    const { container } = render(wrap(createElement(GameKbWarning, { gameId: 'g1' })));
    expect(container.firstChild).toBeNull();
  });
});
