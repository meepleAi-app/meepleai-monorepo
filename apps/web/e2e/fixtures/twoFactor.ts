import { Page } from '@playwright/test';

const API_BASE = 'http://localhost:8080';

/**
 * Mock 2FA Status Response
 */
export interface Mock2FAStatus {
  isTwoFactorEnabled: boolean;
  backupCodesCount: number;
}

/**
 * Mock TOTP Setup Response
 */
export interface MockTotpSetup {
  secret: string;
  qrCodeUri: string;
  backupCodes: string[];
}

/**
 * Generate realistic mock backup codes (XXXX-XXXX format)
 */
export function generateMockBackupCodes(count: number = 10): string[] {
  const codes: string[] = [];
  for (let i = 0; i < count; i++) {
    const part1 = Math.random().toString(36).substring(2, 6).toUpperCase();
    const part2 = Math.random().toString(36).substring(2, 6).toUpperCase();
    codes.push(`${part1}-${part2}`);
  }
  return codes;
}

/**
 * Generate realistic TOTP secret (32 chars, Base32)
 */
export function generateMockTotpSecret(): string {
  const base32Chars = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ234567';
  let secret = '';
  for (let i = 0; i < 32; i++) {
    secret += base32Chars.charAt(Math.floor(Math.random() * base32Chars.length));
  }
  return secret;
}

/**
 * Generate mock QR code URI
 */
export function generateMockQRCodeUri(secret: string, email: string = 'test@meepleai.dev'): string {
  return `otpauth://totp/MeepleAI:${encodeURIComponent(email)}?secret=${secret}&issuer=MeepleAI`;
}

/**
 * Setup mock 2FA status endpoint (disabled by default)
 */
export async function mockTwoFactorStatus(
  page: Page,
  status: Partial<Mock2FAStatus> = {}
): Promise<Mock2FAStatus> {
  const mockStatus: Mock2FAStatus = {
    isTwoFactorEnabled: status.isTwoFactorEnabled ?? false,
    backupCodesCount: status.backupCodesCount ?? 0,
  };

  await page.route(`${API_BASE}/api/v1/users/me/2fa/status`, async (route) => {
    await route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify(mockStatus),
    });
  });

  return mockStatus;
}

/**
 * Setup mock 2FA setup endpoint
 */
export async function mockTwoFactorSetup(
  page: Page,
  customSetup?: Partial<MockTotpSetup>
): Promise<MockTotpSetup> {
  const secret = customSetup?.secret || generateMockTotpSecret();
  const backupCodes = customSetup?.backupCodes || generateMockBackupCodes(10);
  const qrCodeUri = customSetup?.qrCodeUri || generateMockQRCodeUri(secret);

  const mockSetup: MockTotpSetup = {
    secret,
    qrCodeUri,
    backupCodes,
  };

  await page.route(`${API_BASE}/api/v1/auth/2fa/setup`, async (route) => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify(mockSetup),
      });
    }
  });

  return mockSetup;
}

/**
 * Setup mock 2FA enable endpoint
 */
export async function mockTwoFactorEnable(
  page: Page,
  options: { shouldFail?: boolean; validCode?: string } = {}
): Promise<void> {
  const { shouldFail = false, validCode = '123456' } = options;

  await page.route(`${API_BASE}/api/v1/auth/2fa/enable`, async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      const code = body?.code;

      if (shouldFail || (validCode && code !== validCode)) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Invalid verification code',
            message: 'The TOTP code is incorrect. Please try again.',
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({}),
        });
      }
    }
  });
}

/**
 * Setup mock 2FA verify endpoint (for login)
 */
export async function mockTwoFactorVerify(
  page: Page,
  options: {
    shouldFail?: boolean;
    validCode?: string;
    rateLimitAfter?: number;
  } = {}
): Promise<{ attemptCount: number }> {
  const { shouldFail = false, validCode = '123456', rateLimitAfter = 3 } = options;
  let attemptCount = 0;

  await page.route(`${API_BASE}/api/v1/auth/2fa/verify`, async (route) => {
    if (route.request().method() === 'POST') {
      attemptCount++;
      const body = route.request().postDataJSON();
      const code = body?.code;

      // Rate limiting
      if (attemptCount > rateLimitAfter) {
        await route.fulfill({
          status: 429,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Too many requests',
            message: 'Too many verification attempts. Please try again later.',
          }),
        });
        return;
      }

      if (shouldFail || (validCode && code !== validCode)) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Invalid verification code',
            message: 'The TOTP code is incorrect. Please try again.',
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: {
              id: 'user-test-id',
              email: 'test@meepleai.dev',
              displayName: 'Test User',
              role: 'User',
            },
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      }
    }
  });

  return { attemptCount };
}

/**
 * Setup mock 2FA disable endpoint
 */
export async function mockTwoFactorDisable(
  page: Page,
  options: {
    shouldFail?: boolean;
    validPassword?: string;
    validCode?: string;
  } = {}
): Promise<void> {
  const { shouldFail = false, validPassword = 'ValidPass123!', validCode = '123456' } = options;

  await page.route(`${API_BASE}/api/v1/auth/2fa/disable`, async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      const password = body?.password;
      const code = body?.code;

      if (
        shouldFail ||
        (validPassword && password !== validPassword) ||
        (validCode && code !== validCode)
      ) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Invalid credentials',
            message: 'Failed to disable 2FA. Check your password and code.',
          }),
        });
      } else {
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({}),
        });
      }
    }
  });
}

/**
 * Setup comprehensive 2FA mocks for a complete flow
 * This is a convenience function that sets up all common mocks
 */
export async function setupTwoFactorMocks(
  page: Page,
  options: {
    initiallyEnabled?: boolean;
    backupCodesCount?: number;
    validCode?: string;
    validPassword?: string;
  } = {}
): Promise<{
  status: Mock2FAStatus;
  setup: MockTotpSetup;
}> {
  const { initiallyEnabled = false, backupCodesCount = 10, validCode = '123456', validPassword = 'ValidPass123!' } = options;

  // Setup status
  const status = await mockTwoFactorStatus(page, {
    isTwoFactorEnabled: initiallyEnabled,
    backupCodesCount: initiallyEnabled ? backupCodesCount : 0,
  });

  // Setup other endpoints
  const setup = await mockTwoFactorSetup(page);
  await mockTwoFactorEnable(page, { validCode });
  await mockTwoFactorVerify(page, { validCode });
  await mockTwoFactorDisable(page, { validCode, validPassword });

  return { status, setup };
}

/**
 * Mock expired temp session (5 minutes)
 */
export async function mockExpiredTempSession(page: Page): Promise<void> {
  await page.route(`${API_BASE}/api/v1/auth/2fa/verify`, async (route) => {
    if (route.request().method() === 'POST') {
      await route.fulfill({
        status: 401,
        contentType: 'application/json',
        body: JSON.stringify({
          error: 'Session expired',
          message: 'Your verification session has expired. Please log in again.',
        }),
      });
    }
  });
}

/**
 * Mock backup code usage (single-use enforcement)
 */
export async function mockBackupCodeUsage(
  page: Page,
  usedCodes: Set<string>
): Promise<void> {
  await page.route(`${API_BASE}/api/v1/auth/2fa/verify`, async (route) => {
    if (route.request().method() === 'POST') {
      const body = route.request().postDataJSON();
      const code = body?.code;

      if (usedCodes.has(code)) {
        await route.fulfill({
          status: 400,
          contentType: 'application/json',
          body: JSON.stringify({
            error: 'Invalid backup code',
            message: 'This backup code has already been used.',
          }),
        });
      } else {
        usedCodes.add(code);
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({
            user: {
              id: 'user-test-id',
              email: 'test@meepleai.dev',
              displayName: 'Test User',
              role: 'User',
            },
            expiresAt: new Date(Date.now() + 60 * 60 * 1000).toISOString(),
          }),
        });
      }
    }
  });
}
