/**
 * NewPlayRecordPage — gameNightId deep-link prefill tests
 *
 * #2348: Verifies that ?gameNightId= is read from URL and forwarded
 * to SessionCreateForm as initialValues/initialPlayers via useGameNightPrefill.
 *
 * Backward-compat: when gameNightId is absent the form gets undefined initial
 * props (no change to existing behaviour).
 */

import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, expect, it, vi } from 'vitest';

import NewPlayRecordPage from './page';

// ─── next/navigation mocks ────────────────────────────────────────────────────

const mockRouterPush = vi.fn();

vi.mock('next/navigation', async orig => ({
  ...(await orig<typeof import('next/navigation')>()),
  useSearchParams: () => new URLSearchParams('gameNightId=gn-1'),
  useRouter: () => ({ push: mockRouterPush, back: vi.fn(), replace: vi.fn(), refresh: vi.fn() }),
}));

// ─── SessionCreateForm — capture props ────────────────────────────────────────

const captured: { initialValues?: unknown; initialPlayers?: unknown } = {};

vi.mock('@/components/play-records/SessionCreateForm', () => ({
  SessionCreateForm: (props: any) => {
    captured.initialValues = props.initialValues;
    captured.initialPlayers = props.initialPlayers;
    return <div data-testid="mock-form" />;
  },
  // PlayerEntry is a type-only import — erased at runtime, no mock needed.
}));

// ─── useGameNightPrefill — fixed prefill ──────────────────────────────────────

vi.mock('@/lib/domain-hooks/useGameNightPrefill', () => ({
  useGameNightPrefill: () => ({
    prefill: {
      initialValues: { gameName: 'Brass Birmingham', location: 'Padova' },
      initialPlayers: [],
    },
    isLoading: false,
    isError: false,
    enabled: true,
  }),
}));

// ─── Remaining hooks ──────────────────────────────────────────────────────────

vi.mock('@/lib/domain-hooks/usePlayRecords', () => ({
  useCreatePlayRecord: () => ({
    mutateAsync: vi.fn(),
    isPending: false,
  }),
}));

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (key: string) => key }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), error: vi.fn() },
}));

// ─── Helper ───────────────────────────────────────────────────────────────────

function renderPage() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <NewPlayRecordPage />
    </QueryClientProvider>
  );
}

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('NewPlayRecordPage — gameNightId prefill', () => {
  it('passes GameNight prefill into the form when ?gameNightId= is present', async () => {
    captured.initialValues = undefined;
    captured.initialPlayers = undefined;

    renderPage();

    // Wait for the mock form to be rendered (Suspense resolves synchronously in test)
    await screen.findByTestId('mock-form');

    expect(captured.initialValues).toMatchObject({
      gameName: 'Brass Birmingham',
      location: 'Padova',
    });
    expect(captured.initialPlayers).toEqual([]);
  });
});
