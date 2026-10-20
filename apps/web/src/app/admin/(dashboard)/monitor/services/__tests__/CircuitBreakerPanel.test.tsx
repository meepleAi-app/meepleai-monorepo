/**
 * #4513, #4539 — the circuit breaker states keep their own colour in the dark admin shell.
 *
 * Until #4539 the canonical Badge variants carried `dark:bg-primary/90` (and hover equivalents).
 * tailwind-merge dropped `bg-primary` when a consumer passed `bg-green-100`, but kept the classes
 * with another modifier, and in the always-dark admin shell every state turned orange. jsdom
 * applies no Tailwind CSS, so this checks that the badge's background classes are exactly the
 * state's own: none left over from the variant, with any modifier.
 */

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { backgroundClasses } from '@/test-utils/background-classes';

const mockGetCircuitBreakerStates = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: { admin: { getCircuitBreakerStates: mockGetCircuitBreakerStates } },
}));

import { CircuitBreakerPanel } from '../CircuitBreakerPanel';

// The only background classes each badge may carry: the state's own.
const EXPECTED_BACKGROUND: Record<string, string[]> = {
  Closed: ['bg-green-100', 'dark:bg-green-900/30'],
  Open: ['bg-red-100', 'dark:bg-red-900/30'],
  HalfOpen: ['bg-amber-100', 'dark:bg-amber-900/30'],
  Unknown: ['bg-muted'],
};

function breaker(serviceName: string, state: string) {
  return { serviceName, state, tripCount: 0, lastTrippedAt: null, lastError: null };
}

describe('CircuitBreakerPanel (#4513, #4539)', () => {
  it('every state badge carries only its own background, none from the variant', async () => {
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

    for (const [state, expected] of Object.entries(EXPECTED_BACKGROUND)) {
      const badge = await screen.findByText(state);
      expect(backgroundClasses(badge)).toEqual(expected);
    }
  });
});
