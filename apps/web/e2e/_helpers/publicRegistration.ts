/**
 * Public registration for the specs that register a real user (#4248).
 *
 * A fresh database is invite-only: `Registration:PublicEnabled` defaults to false and the
 * backend enforces it on `POST /api/v1/auth/register` (`RequirePublicRegistration`), so
 * simulating the mode in the browser is not enough. This helper asks the backend; when
 * registration is closed it opens it as admin (`PUT /api/v1/admin/settings/registration-mode`)
 * and hands back a `restore` that closes it again. Without admin credentials the caller
 * skips with a `PREVISTO:` reason that says how to enable the test.
 *
 * The admin session lives in its own request context: it never reaches the browser, where
 * the test registers as a new user.
 *
 * Shared state: the mode is global, and outside CI Playwright runs files in parallel
 * (`workers: 2`). Two files using this helper would race: the one that opened the mode
 * closes it when it ends, under the other that found it already open. So ONE file uses it,
 * `auth-email-registration-flow.spec.ts`. A second consumer needs a serial project first.
 */

import { request } from '@playwright/test';

import { REAL_ADMIN_EMAIL, REAL_ADMIN_PASSWORD, hasRealAdminCredentials } from './realAdminAuth';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const PUBLIC_REGISTRATION_UNAVAILABLE_REASON =
  'PREVISTO: la registrazione pubblica è chiusa e mancano le credenziali per aprirla: imposta E2E_ADMIN_EMAIL e E2E_ADMIN_PASSWORD (in locale, un utente admin da infra/secrets/admin.secret), oppure apri la registrazione da /admin/config';

export type PublicRegistration =
  | { readonly available: true; readonly restore: () => Promise<void> }
  | { readonly available: false; readonly reason: string };

export async function ensurePublicRegistration(): Promise<PublicRegistration> {
  const api = await request.newContext({ baseURL: API_BASE });
  try {
    const mode = await api.get('/api/v1/auth/registration-mode');
    if (!mode.ok()) {
      throw new Error(`GET /auth/registration-mode answered ${mode.status()}`);
    }
    const { publicRegistrationEnabled } = (await mode.json()) as {
      publicRegistrationEnabled: boolean;
    };
    if (publicRegistrationEnabled) {
      await api.dispose();
      return { available: true, restore: async () => {} };
    }

    if (!hasRealAdminCredentials) {
      await api.dispose();
      return { available: false, reason: PUBLIC_REGISTRATION_UNAVAILABLE_REASON };
    }

    const login = await api.post('/api/v1/auth/login', {
      data: { email: REAL_ADMIN_EMAIL, password: REAL_ADMIN_PASSWORD },
    });
    if (!login.ok()) {
      throw new Error(
        `Admin login answered ${login.status()}: check E2E_ADMIN_* and the rate limit`
      );
    }
    const setMode = (enabled: boolean) =>
      api.put('/api/v1/admin/settings/registration-mode', { data: { enabled } });

    const opened = await setMode(true);
    if (!opened.ok()) {
      throw new Error(`Opening public registration answered ${opened.status()}`);
    }

    return {
      available: true,
      restore: async () => {
        try {
          await setMode(false);
        } finally {
          await api.dispose();
        }
      },
    };
  } catch (error) {
    await api.dispose();
    throw error;
  }
}

/** An address no earlier run has used. */
export function uniqueRegistrationEmail(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.floor(Math.random() * 1e6)}@meepleai.test`;
}

/**
 * A password the backend accepts: it rejects common ones (`TestPassword123!` is on its list
 * of compromised passwords, answered with 400).
 */
export function uncommonRegistrationPassword(): string {
  return `Meeple-${Date.now().toString(36)}-e2e!Zq`;
}
