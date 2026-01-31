/**
 * useEmailVerification Hook (Issue #3076)
 *
 * Manages email verification state and operations:
 * - Verify email with token
 * - Resend verification email with rate limiting
 * - Cooldown timer management
 */

import { useState, useCallback, useEffect, useRef } from 'react';

import { api } from '@/lib/api';
import type { EmailVerificationErrorType } from '@/lib/api/schemas';

// ============================================================================
// Types
// ============================================================================

export interface UseEmailVerificationReturn {
  /** Whether a verification operation is in progress */
  isLoading: boolean;

  /** Whether a resend operation is in progress */
  isResending: boolean;

  /** Error message if verification failed */
  error: string | null;

  /** Error type for categorized error handling */
  errorType: EmailVerificationErrorType | null;

  /** Whether the email was successfully verified */
  isVerified: boolean;

  /** Seconds remaining before resend is allowed (rate limiting) */
  cooldownSeconds: number;

  /** Verify email with the provided token */
  verifyEmail: (token: string) => Promise<boolean>;

  /** Resend verification email (rate limited) */
  resendVerificationEmail: (email: string) => Promise<boolean>;

  /** Clear error state */
  clearError: () => void;
}

// ============================================================================
// Constants
// ============================================================================

/** Default cooldown period in seconds (matches backend rate limit) */
const RESEND_COOLDOWN_SECONDS = 60;

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Parse error message to determine error type
 */
function parseErrorType(errorMessage: string): EmailVerificationErrorType {
  const lowerMessage = errorMessage.toLowerCase();

  if (lowerMessage.includes('expired') || lowerMessage.includes('scaduto')) {
    return 'expired';
  }
  if (lowerMessage.includes('invalid') || lowerMessage.includes('non valido')) {
    return 'invalid';
  }
  if (
    lowerMessage.includes('already verified') ||
    lowerMessage.includes('già verificat')
  ) {
    return 'already_verified';
  }
  if (lowerMessage.includes('not found') || lowerMessage.includes('non trovato')) {
    return 'not_found';
  }
  if (
    lowerMessage.includes('rate limit') ||
    lowerMessage.includes('troppi tentativi') ||
    lowerMessage.includes('429')
  ) {
    return 'rate_limited';
  }

  return 'unknown';
}

// ============================================================================
// Hook Implementation
// ============================================================================

export function useEmailVerification(): UseEmailVerificationReturn {
  const [isLoading, setIsLoading] = useState(false);
  const [isResending, setIsResending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [errorType, setErrorType] = useState<EmailVerificationErrorType | null>(null);
  const [isVerified, setIsVerified] = useState(false);
  const [cooldownSeconds, setCooldownSeconds] = useState(0);

  // Timer ref for cleanup
  const cooldownTimerRef = useRef<NodeJS.Timeout | null>(null);

  /**
   * Clear cooldown timer on unmount
   */
  useEffect(() => {
    return () => {
      if (cooldownTimerRef.current) {
        clearInterval(cooldownTimerRef.current);
      }
    };
  }, []);

  /**
   * Start cooldown timer
   */
  const startCooldown = useCallback(() => {
    setCooldownSeconds(RESEND_COOLDOWN_SECONDS);

    // Clear existing timer
    if (cooldownTimerRef.current) {
      clearInterval(cooldownTimerRef.current);
    }

    // Start countdown
    cooldownTimerRef.current = setInterval(() => {
      setCooldownSeconds(prev => {
        if (prev <= 1) {
          if (cooldownTimerRef.current) {
            clearInterval(cooldownTimerRef.current);
            cooldownTimerRef.current = null;
          }
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  }, []);

  /**
   * Verify email with token
   *
   * @param token - The verification token from email link
   * @returns true if verification succeeded, false otherwise
   */
  const verifyEmail = useCallback(async (token: string): Promise<boolean> => {
    setIsLoading(true);
    setError(null);
    setErrorType(null);

    try {
      const response = await api.emailVerification.verifyEmail(token);

      if (response.ok) {
        setIsVerified(true);
        return true;
      }

      // Unexpected: ok is false but no error thrown
      const errorMessage = response.message || 'Verification failed';
      setError(errorMessage);
      setErrorType(parseErrorType(errorMessage));
      return false;
    } catch (err: unknown) {
      const errorMessage =
        err instanceof Error
          ? err.message
          : 'Email verification failed. Please try again.';
      setError(errorMessage);
      setErrorType(parseErrorType(errorMessage));
      return false;
    } finally {
      setIsLoading(false);
    }
  }, []);

  /**
   * Resend verification email (rate limited to 1/min)
   *
   * @param email - The email address to resend to
   * @returns true if resend succeeded, false otherwise
   */
  const resendVerificationEmail = useCallback(
    async (email: string): Promise<boolean> => {
      // Prevent if still in cooldown
      if (cooldownSeconds > 0) {
        return false;
      }

      setIsResending(true);
      setError(null);
      setErrorType(null);

      try {
        const response = await api.emailVerification.resendVerificationEmail(email);

        if (response.ok) {
          // Start cooldown timer
          startCooldown();
          return true;
        }

        // Unexpected: ok is false but no error thrown
        const errorMessage = response.message || 'Failed to resend email';
        setError(errorMessage);
        setErrorType(parseErrorType(errorMessage));
        return false;
      } catch (err: unknown) {
        const errorMessage =
          err instanceof Error
            ? err.message
            : 'Failed to resend verification email. Please try again later.';
        setError(errorMessage);
        setErrorType(parseErrorType(errorMessage));

        // If rate limited, start cooldown anyway
        if (
          errorMessage.toLowerCase().includes('rate') ||
          errorMessage.toLowerCase().includes('429')
        ) {
          startCooldown();
        }

        return false;
      } finally {
        setIsResending(false);
      }
    },
    [cooldownSeconds, startCooldown]
  );

  /**
   * Clear error state
   */
  const clearError = useCallback(() => {
    setError(null);
    setErrorType(null);
  }, []);

  return {
    isLoading,
    isResending,
    error,
    errorType,
    isVerified,
    cooldownSeconds,
    verifyEmail,
    resendVerificationEmail,
    clearError,
  };
}
