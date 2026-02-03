/**
 * useEmailVerification Hook Tests - Issue #3026 (Frontend 85% Coverage)
 *
 * Coverage: Email verification state and operations
 * - verifyEmail: Verify email with token
 * - resendVerificationEmail: Resend with rate limiting
 * - clearError: Clear error state
 * - Cooldown timer management
 * - Error type parsing
 */

import { renderHook, act, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

import { useEmailVerification } from '../useEmailVerification';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    emailVerification: {
      verifyEmail: vi.fn(),
      resendVerificationEmail: vi.fn(),
    },
  },
}));

// Import after mock setup
import { api } from '@/lib/api';

const mockVerifyEmail = vi.mocked(api.emailVerification.verifyEmail);
const mockResendVerificationEmail = vi.mocked(api.emailVerification.resendVerificationEmail);

describe('useEmailVerification - Issue #3026', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  describe('Initial State', () => {
    it('should have correct initial state', () => {
      const { result } = renderHook(() => useEmailVerification());

      expect(result.current.isLoading).toBe(false);
      expect(result.current.isResending).toBe(false);
      expect(result.current.error).toBeNull();
      expect(result.current.errorType).toBeNull();
      expect(result.current.isVerified).toBe(false);
      expect(result.current.cooldownSeconds).toBe(0);
    });
  });

  describe('verifyEmail', () => {
    it('should verify email successfully', async () => {
      mockVerifyEmail.mockResolvedValue({ ok: true, message: 'Verified' });

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.verifyEmail('valid-token');
      });

      expect(success!).toBe(true);
      expect(result.current.isVerified).toBe(true);
      expect(result.current.error).toBeNull();
      expect(result.current.isLoading).toBe(false);
      expect(mockVerifyEmail).toHaveBeenCalledWith('valid-token');
    });

    it('should set loading state during verification', async () => {
      let resolvePromise: (value: { ok: boolean; message: string }) => void;
      mockVerifyEmail.mockImplementation(
        () => new Promise((resolve) => { resolvePromise = resolve; })
      );

      const { result } = renderHook(() => useEmailVerification());

      let verifyPromise: Promise<boolean>;
      act(() => {
        verifyPromise = result.current.verifyEmail('token');
      });

      expect(result.current.isLoading).toBe(true);

      await act(async () => {
        resolvePromise!({ ok: true, message: 'Done' });
        await verifyPromise;
      });

      expect(result.current.isLoading).toBe(false);
    });

    it('should handle verification failure with ok: false', async () => {
      mockVerifyEmail.mockResolvedValue({ ok: false, message: 'Token expired' });

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.verifyEmail('expired-token');
      });

      expect(success!).toBe(false);
      expect(result.current.isVerified).toBe(false);
      expect(result.current.error).toBe('Token expired');
      expect(result.current.errorType).toBe('expired');
    });

    it('should handle verification failure with Error exception', async () => {
      mockVerifyEmail.mockRejectedValue(new Error('Invalid token'));

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.verifyEmail('invalid-token');
      });

      expect(success!).toBe(false);
      expect(result.current.error).toBe('Invalid token');
      expect(result.current.errorType).toBe('invalid');
    });

    it('should handle unknown exception type', async () => {
      mockVerifyEmail.mockRejectedValue('string error');

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.verifyEmail('token');
      });

      expect(success!).toBe(false);
      expect(result.current.error).toBe('Email verification failed. Please try again.');
    });

    it('should clear error before new verification attempt', async () => {
      mockVerifyEmail.mockRejectedValueOnce(new Error('First error'));
      mockVerifyEmail.mockResolvedValueOnce({ ok: true, message: 'Success' });

      const { result } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.verifyEmail('token1');
      });

      expect(result.current.error).toBe('First error');

      await act(async () => {
        await result.current.verifyEmail('token2');
      });

      expect(result.current.error).toBeNull();
      expect(result.current.isVerified).toBe(true);
    });
  });

  describe('resendVerificationEmail', () => {
    it('should resend email successfully', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.resendVerificationEmail('test@example.com');
      });

      expect(success!).toBe(true);
      expect(result.current.isResending).toBe(false);
      expect(mockResendVerificationEmail).toHaveBeenCalledWith('test@example.com');
    });

    it('should start cooldown after successful resend', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.resendVerificationEmail('test@example.com');
      });

      expect(result.current.cooldownSeconds).toBe(60);
    });

    it('should countdown the cooldown timer', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.resendVerificationEmail('test@example.com');
      });

      expect(result.current.cooldownSeconds).toBe(60);

      // Advance time by 1 second
      await act(async () => {
        vi.advanceTimersByTime(1000);
      });

      expect(result.current.cooldownSeconds).toBe(59);

      // Advance time by 5 more seconds
      await act(async () => {
        vi.advanceTimersByTime(5000);
      });

      expect(result.current.cooldownSeconds).toBe(54);
    });

    it('should reset cooldown to 0 after completion', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.resendVerificationEmail('test@example.com');
      });

      // Advance time by full cooldown period
      await act(async () => {
        vi.advanceTimersByTime(60000);
      });

      expect(result.current.cooldownSeconds).toBe(0);
    });

    it('should prevent resend during cooldown', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result } = renderHook(() => useEmailVerification());

      // First resend - should work
      await act(async () => {
        await result.current.resendVerificationEmail('test@example.com');
      });

      expect(mockResendVerificationEmail).toHaveBeenCalledTimes(1);

      // Second resend during cooldown - should be blocked
      let success: boolean;
      await act(async () => {
        success = await result.current.resendVerificationEmail('test@example.com');
      });

      expect(success!).toBe(false);
      expect(mockResendVerificationEmail).toHaveBeenCalledTimes(1); // Still 1
    });

    it('should set isResending during operation', async () => {
      let resolvePromise: (value: { ok: boolean; message: string }) => void;
      mockResendVerificationEmail.mockImplementation(
        () => new Promise((resolve) => { resolvePromise = resolve; })
      );

      const { result } = renderHook(() => useEmailVerification());

      let resendPromise: Promise<boolean>;
      act(() => {
        resendPromise = result.current.resendVerificationEmail('test@example.com');
      });

      expect(result.current.isResending).toBe(true);

      await act(async () => {
        resolvePromise!({ ok: true, message: 'Done' });
        await resendPromise;
      });

      expect(result.current.isResending).toBe(false);
    });

    it('should handle resend failure with ok: false', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: false, message: 'User not found' });

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.resendVerificationEmail('unknown@example.com');
      });

      expect(success!).toBe(false);
      expect(result.current.error).toBe('User not found');
      expect(result.current.errorType).toBe('not_found');
    });

    it('should handle rate limit error and start cooldown', async () => {
      mockResendVerificationEmail.mockRejectedValue(new Error('Rate limit exceeded 429'));

      const { result } = renderHook(() => useEmailVerification());

      let success: boolean;
      await act(async () => {
        success = await result.current.resendVerificationEmail('test@example.com');
      });

      expect(success!).toBe(false);
      expect(result.current.errorType).toBe('rate_limited');
      expect(result.current.cooldownSeconds).toBe(60); // Cooldown started despite error
    });
  });

  describe('clearError', () => {
    it('should clear error and errorType', async () => {
      mockVerifyEmail.mockRejectedValue(new Error('Some error'));

      const { result } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.verifyEmail('token');
      });

      expect(result.current.error).toBe('Some error');
      expect(result.current.errorType).not.toBeNull();

      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBeNull();
      expect(result.current.errorType).toBeNull();
    });
  });

  describe('Error Type Parsing', () => {
    const errorCases = [
      { message: 'Token expired', expectedType: 'expired' },
      { message: 'Il token è scaduto', expectedType: 'expired' },
      { message: 'Invalid token provided', expectedType: 'invalid' },
      { message: 'Token non valido', expectedType: 'invalid' },
      { message: 'Email already verified', expectedType: 'already_verified' },
      { message: 'Email già verificata', expectedType: 'already_verified' },
      { message: 'User not found', expectedType: 'not_found' },
      { message: 'Utente non trovato', expectedType: 'not_found' },
      { message: 'Rate limit exceeded', expectedType: 'rate_limited' },
      { message: 'Troppi tentativi', expectedType: 'rate_limited' },
      { message: 'Error code 429', expectedType: 'rate_limited' },
      { message: 'Something went wrong', expectedType: 'unknown' },
    ];

    errorCases.forEach(({ message, expectedType }) => {
      it(`should parse "${message}" as "${expectedType}"`, async () => {
        mockVerifyEmail.mockRejectedValue(new Error(message));

        const { result } = renderHook(() => useEmailVerification());

        await act(async () => {
          await result.current.verifyEmail('token');
        });

        expect(result.current.errorType).toBe(expectedType);
      });
    });
  });

  describe('Cleanup', () => {
    it('should clear interval on unmount', async () => {
      mockResendVerificationEmail.mockResolvedValue({ ok: true, message: 'Sent' });

      const { result, unmount } = renderHook(() => useEmailVerification());

      await act(async () => {
        await result.current.resendVerificationEmail('test@example.com');
      });

      expect(result.current.cooldownSeconds).toBe(60);

      // Unmount the hook
      unmount();

      // Advance time - should not throw errors or update state
      await act(async () => {
        vi.advanceTimersByTime(5000);
      });

      // No assertions needed - just verifying no errors occur
    });
  });
});
