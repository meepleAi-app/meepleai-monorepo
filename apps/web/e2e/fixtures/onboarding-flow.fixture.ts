import { test as base, type Page, type BrowserContext } from '@playwright/test';

import { env } from '../helpers/onboarding-environment';
import { LoginPage } from '../pages/auth/LoginPage';

export interface OnboardingFlowState {
  adminPage: Page;
  adminContext: BrowserContext;
  adminCredentials: { email: string; password: string };
  invitationToken: string;
  invitationUrl: string;
  testUserEmail: string;
  userPassword: string;
  userPage: Page;
  userContext: BrowserContext;
  testUserId: string;
  gameId: string;
  gameTitle: string;
  pdfReady: boolean;
  agentId: string;
  gameSessionId: string;
  failureReason?: string; // Propagates root cause to downstream test skip messages
}

export async function ensureAdminAuth(
  page: Page,
  credentials: { email: string; password: string }
): Promise<void> {
  const response = await page.request.get(`${env.apiURL}/api/v1/auth/me`);

  if (response.status() === 401) {
    const loginPage = new LoginPage(page);
    await loginPage.goto();
    await loginPage.login(credentials.email, credentials.password);
    await page.waitForURL('**/admin/**', { timeout: 10_000 });
  }
}

export const test = base;
export { expect } from '@playwright/test';
