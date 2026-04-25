/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockMutate = vi.hoisted(() => vi.fn());
const mockState = vi.hoisted(() => ({ isPending: false }));

vi.mock('@/hooks/admin/useCalculateMetrics', () => ({
  useCalculateMetrics: () => ({
    mutate: mockMutate,
    isPending: mockState.isPending,
  }),
}));

import { EvaluateButton } from '../EvaluateButton';

const ANALYSIS_ID = '44444444-4444-4444-4444-444444444444';

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('EvaluateButton', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockState.isPending = false;
  });

  it('renders the idle label by default', () => {
    renderWithClient(<EvaluateButton analysisId={ANALYSIS_ID} />);
    expect(screen.getByRole('button', { name: /Evaluate metrics/i })).toBeInTheDocument();
  });

  it('calls calculateMetrics with analysisId on click', async () => {
    const user = userEvent.setup();
    renderWithClient(<EvaluateButton analysisId={ANALYSIS_ID} />);

    await user.click(screen.getByRole('button', { name: /Evaluate metrics/i }));

    expect(mockMutate).toHaveBeenCalledTimes(1);
    const [variables, options] = mockMutate.mock.calls[0];
    expect(variables).toBe(ANALYSIS_ID);
    expect(options).toBeDefined();
  });

  it('shows pending state with spinner while loading and disables the button', () => {
    mockState.isPending = true;
    renderWithClient(<EvaluateButton analysisId={ANALYSIS_ID} />);

    const button = screen.getByRole('button', { name: /Evaluating/i });
    expect(button).toBeDisabled();
    expect(button.querySelector('.animate-spin')).toBeInTheDocument();
  });

  it('forwards onSuccess callback with the response', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();
    const fakeResponse = { metricsId: '55555555-5555-5555-5555-555555555555' };

    mockMutate.mockImplementation(
      (_id: string, options?: { onSuccess?: (r: typeof fakeResponse) => void }) => {
        options?.onSuccess?.(fakeResponse);
      }
    );

    renderWithClient(<EvaluateButton analysisId={ANALYSIS_ID} onSuccess={onSuccess} />);

    await user.click(screen.getByRole('button', { name: /Evaluate metrics/i }));

    expect(onSuccess).toHaveBeenCalledWith(fakeResponse);
  });
});
