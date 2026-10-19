/**
 * #4513 — the circuit breaker states keep their own colour in the dark admin shell.
 *
 * The canonical Badge variants carry `dark:bg-primary/90` (and the secondary/destructive
 * equivalents). tailwind-merge drops `bg-primary` when a consumer passes `bg-green-100`, but keeps
 * `dark:bg-primary/90`, which has another modifier: in the always-dark admin shell every state
 * turned orange. jsdom applies no Tailwind CSS, so this checks the classes.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetCircuitBreakerStates = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: { admin: { getCircuitBreakerStates: mockGetCircuitBreakerStates } },
}));

import { CircuitBreakerPanel } from '../CircuitBreakerPanel';

const VARIANT_DARK_BG = /(?:^|\s)dark:bg-(?:primary|secondary|destructive)\/90(?=\s|$)/;

function breaker(serviceName: string, state: string) {
  return { serviceName, state, tripCount: 0, lastTrippedAt: null, lastError: null };
}

describe('CircuitBreakerPanel (#4513)', () => {
  it('every state badge has its own dark background, not the variant one', async () => {
    mockGetCircuitBreakerStates.mockResolvedValue([
      breaker('embedding', 'Closed'),
      breaker('reranker', 'Open'),
      breaker('unstructured', 'HalfOpen'),
      breaker('smoldocling', 'Unknown'),
    ]);
    const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

    render(
      <QueryClientProvider client={client}>
        <CircuitBreakerPanel />
      </QueryClientProvider>
    );

    for (const state of ['Closed', 'Open', 'HalfOpen', 'Unknown']) {
      const badge = await screen.findByText(state);
      expect(badge.className).not.toMatch(VARIANT_DARK_BG);
      expect(badge.className).toMatch(/(?:^|\s)dark:bg-/);
    }
  });
});
