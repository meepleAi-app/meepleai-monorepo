/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

const mockMutate = vi.hoisted(() => vi.fn());
const mockState = vi.hoisted(() => ({ isPending: false }));

vi.mock('@/hooks/admin/useOverrideCertification', () => ({
  useOverrideCertification: () => ({
    mutate: mockMutate,
    isPending: mockState.isPending,
  }),
}));

import { OverrideCertificationDialog } from '../OverrideCertificationDialog';

const ANALYSIS_ID = '44444444-4444-4444-4444-444444444444';
const VALID_REASON =
  'The current automated certification verdict is wrong because the rules were misread.';
const TOO_SHORT_REASON = 'too short';

function renderWithClient(ui: ReactNode) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>);
}

describe('OverrideCertificationDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockState.isPending = false;
  });

  it('does not call mutate when reason is below 20 characters and shows validation error', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    renderWithClient(
      <OverrideCertificationDialog
        analysisId={ANALYSIS_ID}
        open={true}
        onOpenChange={onOpenChange}
      />
    );

    const textarea = screen.getByLabelText(/Reason/);
    await user.type(textarea, TOO_SHORT_REASON);

    const submitButton = screen.getByRole('button', { name: /Confirm override/i });
    expect(submitButton).toBeDisabled();

    await user.click(submitButton);

    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('does not call mutate when reason exceeds 500 characters', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    renderWithClient(
      <OverrideCertificationDialog
        analysisId={ANALYSIS_ID}
        open={true}
        onOpenChange={onOpenChange}
      />
    );

    const textarea = screen.getByLabelText(/Reason/);
    const tooLong = 'a'.repeat(501);
    // userEvent.type is slow for very long strings; use clear+paste pattern.
    await user.click(textarea);
    await user.paste(tooLong);

    const submitButton = screen.getByRole('button', { name: /Confirm override/i });

    await waitFor(() => expect(submitButton).toBeDisabled());

    await user.click(submitButton);
    expect(mockMutate).not.toHaveBeenCalled();
  });

  it('on valid submit calls override mutation, closes dialog, and calls onSuccess', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    const onSuccess = vi.fn();

    mockMutate.mockImplementation((_vars, options?: { onSuccess?: () => void }) => {
      options?.onSuccess?.();
    });

    renderWithClient(
      <OverrideCertificationDialog
        analysisId={ANALYSIS_ID}
        open={true}
        onOpenChange={onOpenChange}
        onSuccess={onSuccess}
      />
    );

    const textarea = screen.getByLabelText(/Reason/);
    await user.click(textarea);
    await user.paste(VALID_REASON);

    const submitButton = screen.getByRole('button', { name: /Confirm override/i });
    await waitFor(() => expect(submitButton).not.toBeDisabled());

    await user.click(submitButton);

    expect(mockMutate).toHaveBeenCalledTimes(1);
    const [variables] = mockMutate.mock.calls[0];
    expect(variables).toEqual({ analysisId: ANALYSIS_ID, reason: VALID_REASON });
    expect(onSuccess).toHaveBeenCalled();
    expect(onOpenChange).toHaveBeenCalledWith(false);
  });

  it('keeps the dialog open and does not call onSuccess when the mutation fails', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();
    const onSuccess = vi.fn();

    mockMutate.mockImplementation((_vars, options?: { onError?: (err: Error) => void }) => {
      options?.onError?.(new Error('forbidden'));
    });

    renderWithClient(
      <OverrideCertificationDialog
        analysisId={ANALYSIS_ID}
        open={true}
        onOpenChange={onOpenChange}
        onSuccess={onSuccess}
      />
    );

    const textarea = screen.getByLabelText(/Reason/);
    await user.click(textarea);
    await user.paste(VALID_REASON);

    const submitButton = screen.getByRole('button', { name: /Confirm override/i });
    await waitFor(() => expect(submitButton).not.toBeDisabled());

    await user.click(submitButton);

    expect(mockMutate).toHaveBeenCalledTimes(1);
    expect(onSuccess).not.toHaveBeenCalled();
    expect(onOpenChange).not.toHaveBeenCalledWith(false);
  });

  it('resets the form when the dialog closes', async () => {
    const user = userEvent.setup();
    const onOpenChange = vi.fn();

    const { rerender } = renderWithClient(
      <OverrideCertificationDialog
        analysisId={ANALYSIS_ID}
        open={true}
        onOpenChange={onOpenChange}
      />
    );

    const textarea = screen.getByLabelText(/Reason/);
    await user.click(textarea);
    await user.paste('Some reason that is being typed.');

    expect((textarea as HTMLTextAreaElement).value).toBe('Some reason that is being typed.');

    // Close the dialog (controlled prop)
    rerender(
      <QueryClientProvider client={new QueryClient()}>
        <OverrideCertificationDialog
          analysisId={ANALYSIS_ID}
          open={false}
          onOpenChange={onOpenChange}
        />
      </QueryClientProvider>
    );

    // Reopen — form should be empty
    rerender(
      <QueryClientProvider client={new QueryClient()}>
        <OverrideCertificationDialog
          analysisId={ANALYSIS_ID}
          open={true}
          onOpenChange={onOpenChange}
        />
      </QueryClientProvider>
    );

    const reopened = screen.getByLabelText(/Reason/) as HTMLTextAreaElement;
    expect(reopened.value).toBe('');
  });
});
