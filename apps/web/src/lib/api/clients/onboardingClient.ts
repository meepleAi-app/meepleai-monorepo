/**
 * Onboarding API Client
 *
 * Client for user onboarding endpoints.
 * Covers: status check, wizard seen, dismiss checklist.
 */

import { z } from 'zod';

import type { HttpClient } from '../core/httpClient';

// --- Zod Schemas ---

export const OnboardingStepsDtoSchema = z.object({
  hasGames: z.boolean(),
  hasSessions: z.boolean(),
  hasCompletedProfile: z.boolean(),
});

export const OnboardingStatusResponseSchema = z.object({
  wizardSeenAt: z.string().nullable(),
  dismissedAt: z.string().nullable(),
  steps: OnboardingStepsDtoSchema,
});

export type OnboardingStepsDto = z.infer<typeof OnboardingStepsDtoSchema>;
export type OnboardingStatusResponse = z.infer<typeof OnboardingStatusResponseSchema>;

export interface CreateOnboardingClientParams {
  httpClient: HttpClient;
}

export type OnboardingClient = ReturnType<typeof createOnboardingClient>;

/**
 * Onboarding API client for first-time user experience
 */
export function createOnboardingClient({ httpClient }: CreateOnboardingClientParams) {
  const BASE = '/api/v1/users/me';

  return {
    /**
     * Get onboarding status for current user
     */
    async getStatus(): Promise<OnboardingStatusResponse> {
      const result = await httpClient.get(
        `${BASE}/onboarding-status`,
        OnboardingStatusResponseSchema
      );
      if (!result) {
        return {
          wizardSeenAt: null,
          dismissedAt: null,
          steps: { hasGames: false, hasSessions: false, hasCompletedProfile: false },
        };
      }
      return result;
    },

    /**
     * Mark onboarding wizard as seen (idempotent)
     */
    async markWizardSeen(): Promise<void> {
      await httpClient.post(`${BASE}/onboarding-wizard-seen`, {});
    },

    /**
     * Dismiss onboarding checklist (idempotent)
     */
    async dismiss(): Promise<void> {
      await httpClient.post(`${BASE}/onboarding-dismiss`, {});
    },
  };
}
