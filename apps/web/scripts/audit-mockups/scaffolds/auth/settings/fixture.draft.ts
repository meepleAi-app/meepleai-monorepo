/**
 * Settings page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `settings` cluster Storybook story con axis matrix:
 *   section: SettingsSectionId (profile/security/preferences/ai-consent/
 *            api-keys/notifications/services)
 *   view: 'desktop' | 'mobile'
 *   state: 'default' | 'loading' | 'error'
 *
 * Stage axis discovery: grep `MENU|sections|id:` in settings.jsx
 * (lines 824-829 desktop menu; 1066-1085 mobile PhoneShell config).
 *
 * Cross-references handlers in:
 *   apps/web/src/__tests__/mocks/handlers/auth.handlers.ts (2FA, sessions)
 *   apps/web/src/__tests__/mocks/handlers/admin.handlers.ts (api keys)
 *
 * Co-located target path:
 *   apps/web/src/__tests__/fixtures/mockup-pilots/auth/settings.ts
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type SettingsState = 'default' | 'loading' | 'error';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── 2FA status fixture (drives SecuritySection) ───────────────────────────

export const MOCK_AUTH_SETTINGS_2FA_DISABLED = {
  isEnabled: false,
  enabledAt: null,
  recoveryCodesCount: 0,
  trustedDevices: [],
};

export const MOCK_AUTH_SETTINGS_2FA_ENABLED = {
  isEnabled: true,
  enabledAt: '2026-01-12T14:32:00.000Z',
  recoveryCodesCount: 10,
  trustedDevices: [
    {
      id: 'dev-01',
      name: 'Chrome on macOS',
      lastUsedAt: '2026-06-10T18:45:00.000Z',
    },
  ],
};

// ── Profile fixture ───────────────────────────────────────────────────────

export const MOCK_AUTH_SETTINGS_PROFILE_DEFAULT = {
  user: {
    id: 'usr_meeple_demo',
    email: 'marco@example.com',
    displayName: 'Marco',
    username: 'meepler_42',
    bio: 'Boardgamer entusiasta · Catan, Wingspan, 7 Wonders',
    locale: 'it-IT',
    timezone: 'Europe/Rome',
    avatarUrl: null,
    role: 'User' as const,
  },
};

// ── Preferences fixture ───────────────────────────────────────────────────

export const MOCK_AUTH_SETTINGS_PREFERENCES_DEFAULT = {
  theme: 'system' as const,
  locale: 'it-IT' as const,
  density: 'comfortable' as const,
  reducedMotion: false,
};

// ── API Keys fixture (drives ApiKeysSection) ──────────────────────────────

export const MOCK_AUTH_SETTINGS_API_KEYS_DEFAULT = [
  {
    id: 1,
    name: 'Produzione',
    createdAt: '2026-01-12T00:00:00.000Z',
    lastUsedAt: '2026-04-19T00:00:00.000Z',
    keyPrefix: 'mai_live_xK9p',
  },
  {
    id: 2,
    name: 'Sviluppo locale',
    createdAt: '2026-02-28T00:00:00.000Z',
    lastUsedAt: '2026-04-17T00:00:00.000Z',
    keyPrefix: 'mai_dev_3nQ7',
  },
  {
    id: 3,
    name: 'Webhook CI',
    createdAt: '2026-03-05T00:00:00.000Z',
    lastUsedAt: '2026-04-11T00:00:00.000Z',
    keyPrefix: 'mai_ci_w0Lm',
  },
];

// ── MSW handler switcher (state-driven scenarios) ─────────────────────────

export function mswForState(state: SettingsState) {
  if (state === 'loading') {
    return [
      http.get(`${API_BASE}/api/v1/auth/2fa/status`, () => new Promise<Response>(() => {})),
      http.get(`${API_BASE}/api/v1/user/profile`, () => new Promise<Response>(() => {})),
      http.get(`${API_BASE}/api/v1/user/preferences`, () => new Promise<Response>(() => {})),
      http.get(`${API_BASE}/api/v1/user/api-keys`, () => new Promise<Response>(() => {})),
    ];
  }
  if (state === 'error') {
    return [
      http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
      http.get(`${API_BASE}/api/v1/user/profile`, () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
      http.get(`${API_BASE}/api/v1/user/preferences`, () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
      http.get(`${API_BASE}/api/v1/user/api-keys`, () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
    ];
  }
  return [
    http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_2FA_DISABLED)
    ),
    http.get(`${API_BASE}/api/v1/user/profile`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_PROFILE_DEFAULT)
    ),
    http.get(`${API_BASE}/api/v1/user/preferences`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_PREFERENCES_DEFAULT)
    ),
    http.get(`${API_BASE}/api/v1/user/api-keys`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_API_KEYS_DEFAULT)
    ),
    // Mutations
    http.put(`${API_BASE}/api/v1/user/profile`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_PROFILE_DEFAULT)
    ),
    http.put(`${API_BASE}/api/v1/user/preferences`, () =>
      HttpResponse.json(MOCK_AUTH_SETTINGS_PREFERENCES_DEFAULT)
    ),
    http.post(`${API_BASE}/api/v1/user/api-keys`, async ({ request }) => {
      const body = (await request.json()) as { name: string };
      return HttpResponse.json({
        id: Date.now(),
        name: body.name,
        createdAt: new Date().toISOString(),
        lastUsedAt: null,
        key: `mai_${Date.now()}_secret`,
        keyPrefix: `mai_${Date.now()}`,
      });
    }),
    http.delete(`${API_BASE}/api/v1/user/api-keys/:id`, () => HttpResponse.json({ success: true })),
  ];
}
