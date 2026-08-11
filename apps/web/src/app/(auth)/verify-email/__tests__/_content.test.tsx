/**
 * Verify-email page (_content.tsx) smoke tests — #2773 props migration.
 *
 * Covers the refactored contract: `token`/`emailParam` now flow as PROPS from
 * the async page.tsx instead of via `useSearchParams`. These are focused smoke
 * tests for the prop wiring, not full behavioural coverage of the hook.
 */

import { render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

// ---------------------------------------------------------------------------
// Mocks (must be registered before component import)
// ---------------------------------------------------------------------------

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k }),
}));

const pushMock = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: pushMock }),
}));

const verifyEmailMock = vi.fn();
const resendMock = vi.fn().mockResolvedValue(undefined);
const hookState = {
  isLoading: false,
  isResending: false,
  error: null as string | null,
  errorType: null as string | null,
  isVerified: false,
  cooldownSeconds: 0,
};

vi.mock('@/hooks/useEmailVerification', () => ({
  useEmailVerification: () => ({
    isLoading: hookState.isLoading,
    isResending: hookState.isResending,
    error: hookState.error,
    errorType: hookState.errorType,
    isVerified: hookState.isVerified,
    cooldownSeconds: hookState.cooldownSeconds,
    verifyEmail: verifyEmailMock,
    resendVerificationEmail: resendMock,
  }),
}));

// Import AFTER mocks
import { VerifyEmailContent } from '../_content';

beforeEach(() => {
  vi.clearAllMocks();
  sessionStorage.clear();
  hookState.isLoading = false;
  hookState.isResending = false;
  hookState.error = null;
  hookState.errorType = null;
  hookState.isVerified = false;
  hookState.cooldownSeconds = 0;
});

describe('VerifyEmailContent — #2773 props contract', () => {
  it('renders the no-token branch when the token prop is absent and never verifies', () => {
    render(<VerifyEmailContent token={null} emailParam={null} />);

    expect(screen.getByTestId('verify-email-page')).toBeInTheDocument();
    expect(verifyEmailMock).not.toHaveBeenCalled();
  });

  it('verifies the token supplied via the prop on mount', async () => {
    render(<VerifyEmailContent token="tok-abc" emailParam="user@example.com" />);

    await waitFor(() => {
      expect(verifyEmailMock).toHaveBeenCalledWith('tok-abc');
    });
    expect(screen.getByTestId('verify-email-page')).toBeInTheDocument();
  });
});
