import { type APIRequestContext } from '@playwright/test';

import { env } from './onboarding-environment';

export interface CleanupState {
  testUserId?: string;
  agentId?: string;
}

export async function cleanupOnboardingTest(
  request: APIRequestContext,
  state: CleanupState
): Promise<void> {
  const apiURL = env.apiURL;

  if (state.agentId) {
    try {
      await request.delete(`${apiURL}/api/v1/agents/${state.agentId}`);
    } catch (e) {
      console.warn(`Cleanup: failed to delete agent ${state.agentId}`, e);
    }
  }

  if (state.testUserId) {
    try {
      await request.delete(`${apiURL}/api/v1/admin/users/${state.testUserId}`);
    } catch (e) {
      console.warn(`Cleanup: failed to delete user ${state.testUserId}`, e);
    }
  }
}
