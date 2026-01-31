/**
 * Email Verification API Schemas (Issue #3076)
 *
 * Zod schemas for validating Email Verification bounded context responses.
 * Backend endpoints from Issue #3071.
 */

import { z } from 'zod';

// ========== Verify Email ==========

/**
 * Schema for verify email response
 * POST /api/v1/auth/email/verify
 */
export const VerifyEmailResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type VerifyEmailResponse = z.infer<typeof VerifyEmailResponseSchema>;

// ========== Resend Verification ==========

/**
 * Schema for resend verification email response
 * POST /api/v1/auth/email/resend
 */
export const ResendVerificationResponseSchema = z.object({
  ok: z.boolean(),
  message: z.string(),
});

export type ResendVerificationResponse = z.infer<typeof ResendVerificationResponseSchema>;

// ========== Error Types ==========

/**
 * Error types for email verification
 * Used by VerificationError component
 */
export const EmailVerificationErrorTypeSchema = z.enum([
  'expired',
  'invalid',
  'already_verified',
  'not_found',
  'rate_limited',
  'unknown',
]);

export type EmailVerificationErrorType = z.infer<typeof EmailVerificationErrorTypeSchema>;
