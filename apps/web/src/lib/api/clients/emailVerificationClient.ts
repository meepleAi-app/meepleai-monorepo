/**
 * Email Verification Client (Issue #3076)
 *
 * Modular client for Email Verification operations.
 * Backend endpoints from Issue #3071.
 */

import {
  VerifyEmailResponseSchema,
  ResendVerificationResponseSchema,
  type VerifyEmailResponse,
  type ResendVerificationResponse,
} from '../schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateEmailVerificationClientParams {
  httpClient: HttpClient;
}

/**
 * Email Verification API client with Zod validation
 */
export function createEmailVerificationClient({
  httpClient,
}: CreateEmailVerificationClientParams) {
  return {
    /**
     * Verify email with token
     * POST /api/v1/auth/email/verify
     *
     * @param token - The verification token from the email link
     * @returns VerifyEmailResponse with ok status and message
     * @throws ApiError if token is invalid, expired, or already used
     */
    async verifyEmail(token: string): Promise<VerifyEmailResponse> {
      return httpClient.post(
        '/api/v1/auth/email/verify',
        { token },
        VerifyEmailResponseSchema
      );
    },

    /**
     * Resend verification email
     * POST /api/v1/auth/email/resend
     *
     * Rate limited to 1 request per minute.
     *
     * @param email - The email address to resend verification to
     * @returns ResendVerificationResponse with ok status and message
     * @throws ApiError if rate limited (429) or other server error
     */
    async resendVerificationEmail(email: string): Promise<ResendVerificationResponse> {
      return httpClient.post(
        '/api/v1/auth/email/resend',
        { email },
        ResendVerificationResponseSchema
      );
    },
  };
}

export type EmailVerificationClient = ReturnType<typeof createEmailVerificationClient>;
