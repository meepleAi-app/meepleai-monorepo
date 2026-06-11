/**
 * SP5 Profile Settings page-mock fixtures (DS-17 Phase C-1 — argTypes matrix
 * pattern).
 *
 * Consumed by `sp5-profile-settings` cluster Storybook story con axis matrix:
 *   tab: 'overview' | 'achievements' | 'activity' | 'settings'
 *   section: SettingsSectionId
 *   wizardStep: null | 'setup' | 'verify' | 'codes'
 *   twoFactorEnabled: boolean
 *
 * Stage axis discovery: grep `Frame label|withModal|tabs|sections` in
 * sp5-profile-settings.jsx (lines 83-88 sections; 152-155 tabs; 917-969 frames).
 *
 * Cross-references handlers in:
 *   apps/web/src/__tests__/mocks/handlers/auth.handlers.ts (2FA flow)
 *   ../settings.ts (shared profile/preferences/api-keys fixtures)
 *
 * Co-located target path:
 *   apps/web/src/__tests__/fixtures/mockup-pilots/auth/sp5-profile-settings.ts
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type Sp5State =
  | 'default'
  | 'tfa-off'
  | 'tfa-on'
  | 'wizard-setup'
  | 'wizard-verify'
  | 'wizard-codes';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── 2FA fixtures (wizard-driven) ──────────────────────────────────────────

export const MOCK_AUTH_SP5_2FA_DISABLED = {
  isEnabled: false,
  enabledAt: null,
  recoveryCodesCount: 0,
  trustedDevices: [],
};

export const MOCK_AUTH_SP5_2FA_ENABLED = {
  isEnabled: true,
  enabledAt: '2026-01-12T14:32:00.000Z',
  recoveryCodesCount: 10,
  trustedDevices: [
    {
      id: 'dev-01',
      name: 'Chrome on macOS',
      lastUsedAt: '2026-06-11T18:45:00.000Z',
    },
  ],
};

export const MOCK_AUTH_SP5_2FA_SETUP_SECRET = {
  secret: 'JBSWY3DPEHPK3PXP',
  qrCodeUrl: 'otpauth://totp/MeepleAI:marco@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
  manualCode: 'MFSA-K7P2-W9NB-4XLQ',
};

export const MOCK_AUTH_SP5_2FA_RECOVERY_CODES = {
  codes: [
    'ABCD-1234-EFGH',
    'IJKL-5678-MNOP',
    'QRST-9012-UVWX',
    'YZAB-3456-CDEF',
    'GHIJ-7890-KLMN',
    'OPQR-1234-STUV',
    'WXYZ-5678-ABCD',
    'EFGH-9012-IJKL',
    'MNOP-3456-QRST',
    'UVWX-7890-YZAB',
  ],
};

// ── Profile + preferences fixtures (shared with settings cluster) ─────────

export const MOCK_AUTH_SP5_PROFILE = {
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

export const MOCK_AUTH_SP5_ACHIEVEMENTS = {
  badges: Array.from({ length: 12 }, (_, i) => ({
    id: `badge-${i + 1}`,
    name: `Badge ${i + 1}`,
    description: 'Achievement description placeholder',
    iconUrl: null,
    earnedAt: '2026-06-01T00:00:00.000Z',
  })),
};

export const MOCK_AUTH_SP5_RECENT_SESSIONS = [
  {
    id: 'sess-1',
    gameName: 'Wingspan',
    startedAt: '2026-06-10T20:00:00.000Z',
    durationMinutes: 90,
    players: 4,
  },
  {
    id: 'sess-2',
    gameName: 'Catan',
    startedAt: '2026-06-08T19:30:00.000Z',
    durationMinutes: 120,
    players: 3,
  },
];

// ── MSW handler switcher (state-driven scenarios) ─────────────────────────

function commonHandlers() {
  return [
    http.get(`${API_BASE}/api/v1/auth/me`, () => HttpResponse.json(MOCK_AUTH_SP5_PROFILE.user)),
    http.get(`${API_BASE}/api/v1/user/profile`, () => HttpResponse.json(MOCK_AUTH_SP5_PROFILE)),
    http.get(`${API_BASE}/api/v1/user/preferences`, () =>
      HttpResponse.json({
        theme: 'system',
        locale: 'it-IT',
        density: 'comfortable',
        reducedMotion: false,
      })
    ),
    http.get(`${API_BASE}/api/v1/user/achievements`, () =>
      HttpResponse.json(MOCK_AUTH_SP5_ACHIEVEMENTS)
    ),
    http.get(`${API_BASE}/api/v1/user/sessions/recent`, () =>
      HttpResponse.json(MOCK_AUTH_SP5_RECENT_SESSIONS)
    ),
    http.get(`${API_BASE}/api/v1/library/stats`, () =>
      HttpResponse.json({ gamesCount: 18, sessionsCount: 42, hoursPlayed: 156 })
    ),
  ];
}

export function mswForState(state: Sp5State) {
  if (state === 'tfa-off') {
    return [
      ...commonHandlers(),
      http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
        HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)
      ),
    ];
  }
  if (state === 'tfa-on') {
    return [
      ...commonHandlers(),
      http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
        HttpResponse.json(MOCK_AUTH_SP5_2FA_ENABLED)
      ),
    ];
  }
  if (state === 'wizard-setup' || state === 'wizard-verify' || state === 'wizard-codes') {
    return [
      ...commonHandlers(),
      http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
        HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)
      ),
      http.post(`${API_BASE}/api/v1/auth/2fa/enable`, () =>
        HttpResponse.json(MOCK_AUTH_SP5_2FA_SETUP_SECRET)
      ),
      http.post(`${API_BASE}/api/v1/auth/2fa/verify-setup`, async ({ request }) => {
        const body = (await request.json()) as { code: string };
        if (!/^\d{6}$/.test(body.code)) {
          return HttpResponse.json({ error: 'Invalid code' }, { status: 400 });
        }
        return HttpResponse.json({
          isEnabled: true,
          enabledAt: new Date().toISOString(),
          recoveryCodes: MOCK_AUTH_SP5_2FA_RECOVERY_CODES.codes,
        });
      }),
    ];
  }
  // default
  return [
    ...commonHandlers(),
    http.get(`${API_BASE}/api/v1/auth/2fa/status`, () =>
      HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)
    ),
  ];
}
