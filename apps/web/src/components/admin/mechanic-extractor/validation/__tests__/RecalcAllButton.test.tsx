/**
 * @vitest-environment jsdom
 *
 * Tests for `RecalcAllButton` (ADR-051 Sprint 2 / Task 21).
 *
 * The button is the dashboard's entry point into the async mass-recalculation
 * flow. On click:
 *  - Fires `useEnqueueRecalcAll().mutate()` (no args).
 *  - On success, surfaces the new `jobId` to the parent via `onJobStarted` so
 *    the page can mount the `RecalcProgressDrawer`.
 *  - Stays disabled while the mutation is in flight (prevents double-enqueue
 *    races — Sprint 2 supports a single concurrent job per actor).
 *
 * Toast policy is owned by the underlying hook (no success toast on enqueue,
 * the drawer owns terminal toasts). This component only forwards the jobId.
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockMutate = vi.hoisted(() => vi.fn());
const mockState = vi.hoisted(() => ({ isPending: false }));

vi.mock('@/hooks/admin/useEnqueueRecalcAll', () => ({
  useEnqueueRecalcAll: () => ({
    mutate: mockMutate,
    isPending: mockState.isPending,
  }),
}));

import { RecalcAllButton } from '../RecalcAllButton';

const JOB_ID = '11111111-1111-1111-1111-111111111111';

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('RecalcAllButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockState.isPending = false;
  });

  it('renders the idle label by default', () => {
    renderWithClient(<RecalcAllButton />);
    expect(screen.getByRole('button', { name: /Recalculate all/i })).toBeInTheDocument();
  });

  it('fires the mutation on click with no variables', async () => {
    const user = userEvent.setup();
    renderWithClient(<RecalcAllButton />);

    await user.click(screen.getByRole('button', { name: /Recalculate all/i }));

    expect(mockMutate).toHaveBeenCalledTimes(1);
    // useMutation<TData, TError, void> — `mutate()` is called with `undefined`
    // as the variables argument; some callers pass nothing at all.
    const [variables, options] = mockMutate.mock.calls[0];
    expect(variables).toBeUndefined();
    expect(options).toBeDefined();
  });

  it('forwards the new jobId to onJobStarted on success', async () => {
    const user = userEvent.setup();
    const onJobStarted = vi.fn();

    mockMutate.mockImplementation(
      (_vars: void, options?: { onSuccess?: (response: { jobId: string }) => void }) => {
        options?.onSuccess?.({ jobId: JOB_ID });
      }
    );

    renderWithClient(<RecalcAllButton onJobStarted={onJobStarted} />);

    await user.click(screen.getByRole('button', { name: /Recalculate all/i }));

    expect(onJobStarted).toHaveBeenCalledWith(JOB_ID);
  });

  it('shows pending state and disables the button while a job is being enqueued', () => {
    mockState.isPending = true;
    renderWithClient(<RecalcAllButton />);

    // Label switches to a pending affordance and the button is disabled to
    // prevent double-enqueue while the 202 round-trip is in flight.
    const button = screen.getByRole('button', { name: /Enqueueing|Starting/i });
    expect(button).toBeDisabled();
    expect(button.querySelector('.animate-spin')).toBeInTheDocument();
  });
});
