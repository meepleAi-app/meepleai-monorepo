/**
 * Settings page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `settings` cluster Storybook story con axis matrix:
 *   section: 'profile' | 'security' | 'preferences' | 'notifications' |
 *            'api-keys' | 'ai-consent' | 'services'
 *   state:   'default' | 'loading' | 'error'
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type SettingsState = 'default' | 'loading' | 'error';

export function mswForSettingsState(state: SettingsState) {
  if (state === 'loading') {
    return [
      http.get('*/api/v1/auth/2fa/status', () => new Promise<Response>(() => {})),
      http.get('*/api/v1/admin/api-keys', () => new Promise<Response>(() => {})),
      http.get('*/api/v1/users/me/preferences', () => new Promise<Response>(() => {})),
    ];
  }
  if (state === 'error') {
    return [
      http.get('*/api/v1/auth/2fa/status', () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
      http.get('*/api/v1/admin/api-keys', () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
    ];
  }
  return [
    http.get('*/api/v1/auth/2fa/status', () => HttpResponse.json({ enabled: false, methods: [] })),
    http.get('*/api/v1/admin/api-keys', () => HttpResponse.json({ items: [], total: 0 })),
    http.get('*/api/v1/users/me/preferences', () =>
      HttpResponse.json({
        theme: 'light',
        language: 'it',
        timezone: 'Europe/Rome',
      })
    ),
    http.get('*/api/v1/auth/me', () =>
      HttpResponse.json({
        id: 'usr_meeple_demo',
        email: 'marco@example.com',
        displayName: 'Marco',
        role: 'User' as const,
        emailVerified: true,
      })
    ),
  ];
}
