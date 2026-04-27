/**
 * @vitest-environment jsdom
 *
 * Tests for `RecalcProgressDrawer` (ADR-051 Sprint 2 / Task 21).
 *
 * The drawer is mounted by the dashboard whenever there is an `activeJobId`
 * (returned by `RecalcAllButton.onJobStarted`). It:
 *  - Polls `useRecalcJobStatus(jobId)` (the hook owns the 2s `refetchInterval`
 *    and stops on terminal status — see useRecalcJobStatus.test.tsx).
 *  - Renders progress bar = `processed / total`, plus counters (failed,
 *    skipped, consecutiveFailures), ETA, lastError tail, current heartbeat.
 *  - Renders a "Cancel" button while the job is non-terminal; the button
 *    fires `useCancelRecalcJob().mutate(jobId)` and is disabled afterwards
 *    until the worker honours the flag (i.e. while `cancellationRequested`
 *    is true OR the cancel mutation itself is pending).
 *  - On terminal `Completed` status: green toast `Recalculated N analyses`.
 *  - On terminal `Failed` status: red toast with `lastError`.
 *  - On terminal `Cancelled`: blue/info toast with cancellation acknowledgement.
 *  - Does NOT fire the same terminal toast twice across re-renders — terminal
 *    detection is gated behind a status transition (so polling re-runs that
 *    return the same Completed snapshot don't re-toast).
 *
 * Open/close: the drawer is rendered when `jobId !== null`; the parent page
 * is responsible for unmounting it (passing a null jobId) when the user
 * dismisses. Test mounts it directly with a defined jobId.
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, act } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import type { RecalcJobStatusDto } from '@/lib/api/schemas/admin-mechanic-extractor-validation.schemas';

const statusState = vi.hoisted(() => ({
  data: null as RecalcJobStatusDto | null,
  isLoading: false,
  error: null as Error | null,
}));
const mockCancelMutate = vi.hoisted(() => vi.fn());
const cancelState = vi.hoisted(() => ({ isPending: false }));

vi.mock('@/hooks/admin/useRecalcJobStatus', () => ({
  useRecalcJobStatus: (_jobId: string | null) => ({
    data: statusState.data,
    isLoading: statusState.isLoading,
    error: statusState.error,
  }),
}));

vi.mock('@/hooks/admin/useCancelRecalcJob', () => ({
  useCancelRecalcJob: () => ({
    mutate: mockCancelMutate,
    isPending: cancelState.isPending,
  }),
}));

vi.mock('sonner', () => ({
  toast: { success: vi.fn(), info: vi.fn(), error: vi.fn() },
}));

import { toast } from 'sonner';

import { RecalcProgressDrawer } from '../RecalcProgressDrawer';

const JOB_ID = '77777777-7777-7777-7777-777777777777';
const USER_ID = '88888888-8888-8888-8888-888888888888';

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

function makeStatus(overrides: Partial<RecalcJobStatusDto> = {}): RecalcJobStatusDto {
  return {
    id: JOB_ID,
    status: 'Running',
    triggeredByUserId: USER_ID,
    total: 100,
    processed: 25,
    failed: 1,
    skipped: 0,
    consecutiveFailures: 0,
    lastError: null,
    cancellationRequested: false,
    createdAt: '2026-04-25T12:00:00.000+00:00',
    startedAt: '2026-04-25T12:00:01.000+00:00',
    completedAt: null,
    heartbeatAt: '2026-04-25T12:01:00.000+00:00',
    etaSeconds: 60,
    ...overrides,
  };
}

describe('RecalcProgressDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    statusState.data = null;
    statusState.isLoading = false;
    statusState.error = null;
    cancelState.isPending = false;
  });

  it('renders a loading affordance while the first poll is in flight', () => {
    statusState.isLoading = true;
    statusState.data = null;
    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    expect(screen.getByText(/Loading job status/i)).toBeInTheDocument();
  });

  it('renders progress fraction and counters once status loads', () => {
    statusState.data = makeStatus({
      total: 200,
      processed: 50,
      failed: 3,
      skipped: 1,
    });

    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    // "50 / 200" or similar fraction surface
    expect(screen.getByText(/50\s*\/\s*200/)).toBeInTheDocument();
    // Counters surfaced for failed and skipped — labels and values may live
    // in adjacent `<dt>/<dd>` nodes, so we assert both via text-node lookup.
    expect(screen.getByText(/failed/i)).toBeInTheDocument();
    expect(screen.getByText(/skipped/i)).toBeInTheDocument();
    // The values 3 (failed) and 1 (skipped) appear in their own <dd> nodes.
    // We can find them via a tighter custom matcher (exact match on a <dd>).
    const dds = Array.from(document.querySelectorAll('dd')).map(dd => dd.textContent ?? '');
    expect(dds).toEqual(expect.arrayContaining(['3', '1']));
  });

  it('renders an ETA label when etaSeconds is provided', () => {
    statusState.data = makeStatus({ etaSeconds: 120 });

    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    // Doesn't insist on exact format — just that ETA surface is present.
    expect(screen.getByText(/ETA/i)).toBeInTheDocument();
  });

  it('shows a Cancel button while the job is non-terminal and fires cancel on click', async () => {
    statusState.data = makeStatus({ status: 'Running' });
    const user = userEvent.setup();

    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    const cancelBtn = screen.getByRole('button', { name: /Cancel/i });
    expect(cancelBtn).toBeEnabled();

    await user.click(cancelBtn);
    expect(mockCancelMutate).toHaveBeenCalledWith(JOB_ID);
  });

  it('disables the Cancel button once cancellationRequested is true', () => {
    statusState.data = makeStatus({
      status: 'Running',
      cancellationRequested: true,
    });

    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    // Once the worker has been asked to stop, the affordance flips to a
    // "Cancelling…" disabled state — clicking again would just re-set the
    // flag (idempotent server-side) but the UI prevents a double-fire.
    const button = screen.getByRole('button', { name: /Cancelling/i });
    expect(button).toBeDisabled();
  });

  it('does not show a Cancel button once the job is in a terminal status', () => {
    statusState.data = makeStatus({ status: 'Completed', processed: 100 });

    renderWithClient(<RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />);

    expect(screen.queryByRole('button', { name: /Cancel/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /Cancelling/i })).not.toBeInTheDocument();
  });

  it('fires a success toast exactly once when the job transitions to Completed', () => {
    // Initial mount: still running.
    statusState.data = makeStatus({ status: 'Running', processed: 50 });
    const { rerender } = renderWithClient(
      <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
    );
    expect(toast.success).not.toHaveBeenCalled();

    // First re-render: status flips to Completed.
    statusState.data = makeStatus({ status: 'Completed', processed: 100 });
    act(() => {
      rerender(
        <QueryClientProvider
          client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
        >
          <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
        </QueryClientProvider>
      );
    });
    expect(toast.success).toHaveBeenCalledTimes(1);
    expect(toast.success).toHaveBeenCalledWith(expect.stringMatching(/100/));
  });

  it('fires an error toast with lastError when the job transitions to Failed', () => {
    statusState.data = makeStatus({ status: 'Running' });
    const { rerender } = renderWithClient(
      <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
    );

    statusState.data = makeStatus({
      status: 'Failed',
      lastError: 'connection reset by peer',
    });
    act(() => {
      rerender(
        <QueryClientProvider
          client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
        >
          <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
        </QueryClientProvider>
      );
    });
    expect(toast.error).toHaveBeenCalledTimes(1);
    expect(toast.error).toHaveBeenCalledWith(expect.stringMatching(/connection reset by peer/));
  });

  it('fires an info toast when the job transitions to Cancelled', () => {
    statusState.data = makeStatus({ status: 'Running', cancellationRequested: true });
    const { rerender } = renderWithClient(
      <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
    );

    statusState.data = makeStatus({ status: 'Cancelled', processed: 30 });
    act(() => {
      rerender(
        <QueryClientProvider
          client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}
        >
          <RecalcProgressDrawer jobId={JOB_ID} onClose={() => {}} />
        </QueryClientProvider>
      );
    });
    expect(toast.info).toHaveBeenCalledTimes(1);
    expect(toast.info).toHaveBeenCalledWith(expect.stringMatching(/cancelled/i));
  });

  it('renders nothing when jobId is null', () => {
    const { container } = renderWithClient(
      <RecalcProgressDrawer jobId={null} onClose={() => {}} />
    );
    // No drawer body, no progress bar, no buttons.
    expect(container.querySelector('[data-testid="recalc-progress-drawer"]')).toBeNull();
  });
});
