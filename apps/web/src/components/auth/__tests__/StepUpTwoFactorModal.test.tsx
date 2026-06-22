/**
 * StepUpTwoFactorModal tests (#1859 Phase 10)
 *
 * Coverage:
 * - Renders TwoFactorVerification body when isOpen
 * - Does not render when isOpen=false
 * - Calls onSuccess after successful step-up verification
 * - Closes (calls onClose) on Cancel
 * - Surfaces invalid_code error inline in the form
 * - Surfaces locked_out → toast + close
 * - Surfaces unavailable → toast + close
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithIntl } from '../../../__tests__/fixtures/common-fixtures';
import { StepUpTwoFactorModal } from '../StepUpTwoFactorModal';

// ── Mocks ─────────────────────────────────────────────────────────────────────

const mockMutateAsync = vi.fn();
const mockIsPending = { value: false };

vi.mock('@/hooks/queries/useStepUpTwoFactor', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/queries/useStepUpTwoFactor')>(
    '@/hooks/queries/useStepUpTwoFactor'
  );
  return {
    ...actual,
    useStepUpTwoFactor: () => ({
      mutateAsync: mockMutateAsync,
      isPending: mockIsPending.value,
    }),
  };
});

const mockToastSuccess = vi.fn();
const mockToastError = vi.fn();

vi.mock('sonner', () => ({
  toast: {
    success: (...args: unknown[]) => mockToastSuccess(...args),
    error: (...args: unknown[]) => mockToastError(...args),
  },
}));

// ── Helpers ───────────────────────────────────────────────────────────────────

function renderModal(ui: React.ReactElement, options?: Parameters<typeof renderWithIntl>[1]) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return renderWithIntl(<Wrapper>{ui}</Wrapper>, options);
}

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('StepUpTwoFactorModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockMutateAsync.mockReset();
    mockIsPending.value = false;
  });

  describe('Rendering', () => {
    it('renders TwoFactorVerification when isOpen=true', () => {
      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={vi.fn()} onSuccess={vi.fn()} />);

      expect(screen.getByTestId('step-up-2fa-modal')).toBeInTheDocument();
      expect(screen.getByTestId('two-factor-verification')).toBeInTheDocument();
      expect(screen.getByTestId('2fa-code-input')).toBeInTheDocument();
    });

    it('does not render dialog content when isOpen=false', () => {
      renderModal(<StepUpTwoFactorModal isOpen={false} onClose={vi.fn()} onSuccess={vi.fn()} />);

      expect(screen.queryByTestId('step-up-2fa-modal')).not.toBeInTheDocument();
      expect(screen.queryByTestId('two-factor-verification')).not.toBeInTheDocument();
    });

    it('shows the default reason when none is provided', () => {
      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={vi.fn()} onSuccess={vi.fn()} />);

      expect(screen.getByText(/verifica 2FA recente/i)).toBeInTheDocument();
    });

    it('shows the custom reason when provided', () => {
      renderModal(
        <StepUpTwoFactorModal
          isOpen={true}
          onClose={vi.fn()}
          onSuccess={vi.fn()}
          reason="Per ruotare la chiave del provider serve una verifica 2FA."
        />
      );

      expect(
        screen.getByText('Per ruotare la chiave del provider serve una verifica 2FA.')
      ).toBeInTheDocument();
    });

    it('renders title in Italian', () => {
      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={vi.fn()} onSuccess={vi.fn()} />);

      expect(screen.getByText('Verifica 2FA richiesta')).toBeInTheDocument();
    });
  });

  describe('Success flow', () => {
    it('calls onSuccess after a successful step-up', async () => {
      const user = userEvent.setup();
      const onSuccess = vi.fn();
      const onClose = vi.fn();
      mockMutateAsync.mockResolvedValueOnce({ success: true });

      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={onClose} onSuccess={onSuccess} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '123456');

      await waitFor(() => {
        expect(mockMutateAsync).toHaveBeenCalledWith({ code: '123456' });
      });

      await waitFor(() => {
        expect(onSuccess).toHaveBeenCalledTimes(1);
      });
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });

  describe('Cancel flow', () => {
    it('calls onClose when cancel button is clicked', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();

      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={onClose} onSuccess={vi.fn()} />);

      const cancelButton = screen.getByTestId('2fa-cancel-button');
      await user.click(cancelButton);

      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Error handling', () => {
    it('surfaces invalid_code as inline error in the form', async () => {
      const user = userEvent.setup();
      const { StepUpTwoFactorError } = await import('@/hooks/queries/useStepUpTwoFactor');
      mockMutateAsync.mockRejectedValueOnce(
        new StepUpTwoFactorError('invalid_code', 'Invalid code', 401)
      );

      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={vi.fn()} onSuccess={vi.fn()} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '111111');

      await waitFor(() => {
        expect(screen.getByTestId('2fa-error')).toBeInTheDocument();
      });
      expect(screen.getByText(/codice non valido/i)).toBeInTheDocument();
    });

    it('shows toast and closes on locked_out', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      const { StepUpTwoFactorError } = await import('@/hooks/queries/useStepUpTwoFactor');
      mockMutateAsync.mockRejectedValueOnce(
        new StepUpTwoFactorError('locked_out', 'Too many attempts', 401, 900)
      );

      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={onClose} onSuccess={vi.fn()} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '222222');

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      expect(onClose).toHaveBeenCalledTimes(1);
    });

    it('shows toast and closes on service unavailable (503)', async () => {
      const user = userEvent.setup();
      const onClose = vi.fn();
      const { StepUpTwoFactorError } = await import('@/hooks/queries/useStepUpTwoFactor');
      mockMutateAsync.mockRejectedValueOnce(
        new StepUpTwoFactorError('unavailable', 'Service down', 503)
      );

      renderModal(<StepUpTwoFactorModal isOpen={true} onClose={onClose} onSuccess={vi.fn()} />);

      const input = screen.getByTestId('2fa-code-input');
      await user.type(input, '333333');

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      expect(onClose).toHaveBeenCalledTimes(1);
    });
  });
});
