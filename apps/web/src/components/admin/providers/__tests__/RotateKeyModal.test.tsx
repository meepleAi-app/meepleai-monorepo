/**
 * RotateKeyModal tests (#1859 Phase 11)
 *
 * Coverage:
 * - Trigger button disabled for non-superadmin (preserves original behavior)
 * - Trigger button enabled + opens modal for superadmin
 * - Typed-confirm input gating: submit disabled until name matches
 * - API key length validation (10-512 chars)
 * - Mutation invoked with correct payload
 * - 401 step_up_required → opens StepUpTwoFactorModal
 * - Step-up success automatically retries rotation
 * - 403/409/502/400 errors show toast
 * - Success state shows fingerprint
 */

import type { ReactNode } from 'react';

import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithIntl } from '../../../../__tests__/fixtures/common-fixtures';
import { RotateKeyModal } from '../RotateKeyModal';

// ── Mocks ─────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

const mockRotateMutate = vi.fn();
const mockRotateIsPending = { value: false };

vi.mock('@/hooks/queries/useRotateProviderKey', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/queries/useRotateProviderKey')>(
    '@/hooks/queries/useRotateProviderKey'
  );
  return {
    ...actual,
    useRotateProviderKey: () => ({
      mutateAsync: mockRotateMutate,
      isPending: mockRotateIsPending.value,
    }),
  };
});

const mockStepUpMutate = vi.fn();
vi.mock('@/hooks/queries/useStepUpTwoFactor', async () => {
  const actual = await vi.importActual<typeof import('@/hooks/queries/useStepUpTwoFactor')>(
    '@/hooks/queries/useStepUpTwoFactor'
  );
  return {
    ...actual,
    useStepUpTwoFactor: () => ({
      mutateAsync: mockStepUpMutate,
      isPending: false,
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

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

const mockedUseCurrentUser = vi.mocked(useCurrentUser);

// ── Helpers ───────────────────────────────────────────────────────────────────

function setUser(role: 'admin' | 'superadmin') {
  mockedUseCurrentUser.mockReturnValue({
    data: { id: 'u1', email: 'admin@x.com', role, tier: 'plus' },
  } as ReturnType<typeof useCurrentUser>);
}

function renderWithClient(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  const Wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
  return renderWithIntl(<Wrapper>{ui}</Wrapper>);
}

const VALID_KEY = 'sk-1234567890abcdef';

// ── Tests ─────────────────────────────────────────────────────────────────────

describe('RotateKeyModal', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockRotateMutate.mockReset();
    mockStepUpMutate.mockReset();
    mockRotateIsPending.value = false;
  });

  describe('Trigger button', () => {
    it('is disabled when user is not superadmin', () => {
      setUser('admin');
      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      const button = screen.getByTestId('rotate-key-button-deepseek');
      expect(button).toBeDisabled();
      expect(button).toHaveAttribute('title', expect.stringMatching(/superadmin/i));
    });

    it('is enabled when user is superadmin and exposes data-be-available=true', () => {
      setUser('superadmin');
      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      const button = screen.getByTestId('rotate-key-button-deepseek');
      expect(button).not.toBeDisabled();
      expect(button).toHaveAttribute('data-be-available', 'true');
    });

    it('opens modal when superadmin clicks the trigger', async () => {
      const user = userEvent.setup();
      setUser('superadmin');
      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));

      expect(screen.getByTestId('rotate-key-modal-deepseek')).toBeInTheDocument();
    });
  });

  describe('Form validation', () => {
    it('disables submit until typed-confirm matches AND key length is valid', async () => {
      const user = userEvent.setup();
      setUser('superadmin');
      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));

      const submit = screen.getByTestId('rotate-key-submit-deepseek');
      expect(submit).toBeDisabled();

      // Type wrong name → still disabled
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'wrong-name');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      expect(submit).toBeDisabled();

      // Clear and type correct name → enabled
      await user.clear(screen.getByTestId('rotate-key-confirm-input-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      expect(submit).not.toBeDisabled();
    });

    it('disables submit when api key is too short (< 10 chars)', async () => {
      const user = userEvent.setup();
      setUser('superadmin');
      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), 'short');

      expect(screen.getByTestId('rotate-key-submit-deepseek')).toBeDisabled();
    });
  });

  describe('Successful rotation', () => {
    it('invokes mutation with the correct payload and shows fingerprint on success', async () => {
      const user = userEvent.setup();
      setUser('superadmin');
      mockRotateMutate.mockResolvedValueOnce({
        providerName: 'deepseek',
        newKeyFingerprint: 'sk-1234ab',
        rotatedAt: '2026-06-06T10:00:00Z',
        previousKeyDisabledAt: '2026-06-06T10:00:00Z',
      });

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(mockRotateMutate).toHaveBeenCalledWith({
          newApiKey: VALID_KEY,
          confirmedProviderName: 'deepseek',
        });
      });

      await waitFor(() => {
        expect(screen.getByTestId('rotate-key-success-deepseek')).toBeInTheDocument();
      });
      expect(screen.getByTestId('rotate-key-fingerprint-deepseek')).toHaveTextContent('sk-1234ab');
      expect(mockToastSuccess).toHaveBeenCalled();
    });
  });

  describe('Error: step_up_required', () => {
    it('opens StepUpTwoFactorModal on 401 + step_up_required', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');
      mockRotateMutate.mockRejectedValueOnce(
        new RotateProviderKeyError('step_up_required', 'Step-up required', 401)
      );

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(screen.getByTestId('step-up-2fa-modal')).toBeInTheDocument();
      });
      // No toast yet — modal is the user-facing signal
      expect(mockToastError).not.toHaveBeenCalled();
    });

    it('automatically retries rotation after step-up success', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');

      // First call → step_up_required, second → success
      mockRotateMutate
        .mockRejectedValueOnce(
          new RotateProviderKeyError('step_up_required', 'Step-up required', 401)
        )
        .mockResolvedValueOnce({
          providerName: 'deepseek',
          newKeyFingerprint: 'sk-retry',
          rotatedAt: '2026-06-06T10:00:00Z',
          previousKeyDisabledAt: '2026-06-06T10:00:00Z',
        });
      mockStepUpMutate.mockResolvedValueOnce({ success: true });

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      // Step-up modal opens
      await waitFor(() => {
        expect(screen.getByTestId('step-up-2fa-modal')).toBeInTheDocument();
      });

      // Type valid TOTP code → triggers step-up + automatic retry
      await user.type(screen.getByTestId('2fa-code-input'), '123456');

      await waitFor(() => {
        expect(mockStepUpMutate).toHaveBeenCalledWith({ code: '123456' });
      });

      // Rotation should be re-invoked with the same payload
      await waitFor(() => {
        expect(mockRotateMutate).toHaveBeenCalledTimes(2);
      });

      // Second call had the correct payload
      expect(mockRotateMutate).toHaveBeenLastCalledWith({
        newApiKey: VALID_KEY,
        confirmedProviderName: 'deepseek',
      });
    });
  });

  describe('Error: other status codes', () => {
    it('shows enroll_required toast on 401 + enroll_required', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');
      mockRotateMutate.mockRejectedValueOnce(
        new RotateProviderKeyError('enroll_required', '2FA not enrolled', 401)
      );

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      const firstCall = mockToastError.mock.calls[0];
      expect(String(firstCall[0])).toMatch(/2FA non configurato/i);
    });

    it('shows forbidden toast on 403', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');
      mockRotateMutate.mockRejectedValueOnce(
        new RotateProviderKeyError('forbidden', 'Forbidden', 403)
      );

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      expect(String(mockToastError.mock.calls[0][0])).toMatch(/superadmin/i);
    });

    it('shows rate_limit toast on 409', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');
      mockRotateMutate.mockRejectedValueOnce(
        new RotateProviderKeyError('rate_limit_exceeded', 'Already rotated recently', 409)
      );

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      expect(String(mockToastError.mock.calls[0][0])).toMatch(/ultime 24 ore/i);
    });

    it('shows provider_probe_failed toast on 502', async () => {
      const user = userEvent.setup();
      setUser('superadmin');

      const { RotateProviderKeyError } = await import('@/hooks/queries/useRotateProviderKey');
      mockRotateMutate.mockRejectedValueOnce(
        new RotateProviderKeyError('provider_probe_failed', 'Probe failed', 502)
      );

      renderWithClient(<RotateKeyModal providerName="deepseek" />);

      await user.click(screen.getByTestId('rotate-key-button-deepseek'));
      await user.type(screen.getByTestId('rotate-key-confirm-input-deepseek'), 'deepseek');
      await user.type(screen.getByTestId('rotate-key-new-input-deepseek'), VALID_KEY);
      await user.click(screen.getByTestId('rotate-key-submit-deepseek'));

      await waitFor(() => {
        expect(mockToastError).toHaveBeenCalled();
      });
      expect(String(mockToastError.mock.calls[0][0])).toMatch(/non funziona/i);
    });
  });
});
