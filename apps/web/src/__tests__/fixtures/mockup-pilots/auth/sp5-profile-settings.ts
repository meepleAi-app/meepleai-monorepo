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
    { id: 'dev-01', name: 'Chrome on macOS', lastUsedAt: '2026-06-11T18:45:00.000Z' },
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

export const MOCK_AUTH_SP5_PROFILE = {
  id: 'usr_meeple_demo',
  email: 'marco@example.com',
  displayName: 'Marco',
  username: 'meepler_42',
  bio: 'Boardgamer entusiasta · Catan, Wingspan, 7 Wonders',
  locale: 'it-IT',
  timezone: 'Europe/Rome',
  avatarUrl: null,
  role: 'User' as const,
};

function commonHandlers() {
  return [
    http.get('*/api/v1/auth/me', () => HttpResponse.json(MOCK_AUTH_SP5_PROFILE)),
    http.get('*/api/v1/user/preferences', () =>
      HttpResponse.json({
        theme: 'system',
        locale: 'it-IT',
        density: 'comfortable',
        reducedMotion: false,
      })
    ),
    http.get('*/api/v1/library/stats', () =>
      HttpResponse.json({ gamesCount: 18, sessionsCount: 42, hoursPlayed: 156 })
    ),
  ];
}

export function mswForSp5State(state: Sp5State) {
  if (state === 'tfa-off') {
    return [
      ...commonHandlers(),
      http.get('*/api/v1/auth/2fa/status', () => HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)),
    ];
  }
  if (state === 'tfa-on') {
    return [
      ...commonHandlers(),
      http.get('*/api/v1/auth/2fa/status', () => HttpResponse.json(MOCK_AUTH_SP5_2FA_ENABLED)),
    ];
  }
  if (state === 'wizard-setup' || state === 'wizard-verify' || state === 'wizard-codes') {
    return [
      ...commonHandlers(),
      http.get('*/api/v1/auth/2fa/status', () => HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)),
      http.post('*/api/v1/auth/2fa/enable', () =>
        HttpResponse.json(MOCK_AUTH_SP5_2FA_SETUP_SECRET)
      ),
      http.post('*/api/v1/auth/2fa/verify-setup', async ({ request }) => {
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
  return [
    ...commonHandlers(),
    http.get('*/api/v1/auth/2fa/status', () => HttpResponse.json(MOCK_AUTH_SP5_2FA_DISABLED)),
  ];
}
