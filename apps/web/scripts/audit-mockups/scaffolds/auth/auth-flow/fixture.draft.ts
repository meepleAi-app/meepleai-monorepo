/**
 * Auth Flow page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `auth-flow` cluster Storybook story con axis matrix:
 *   screen: 'login' | 'register' | 'forgot' | 'reset' | 'verify' | '2fa'
 *   state:  'default' | 'loading' | 'error' | 'validation'
 *
 * Stage axis discovery: grep `id:|label:|SCREENS` in auth-flow.jsx (config
 * array lines 778-785: 6 PhoneShell entries).
 *
 * Cross-references handlers in apps/web/src/__tests__/mocks/handlers/auth.handlers.ts
 * (existing endpoints — see msw-gap-analysis.md for gap matrix).
 *
 * Co-located target path:
 *   apps/web/src/__tests__/fixtures/mockup-pilots/auth/auth-flow.ts
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

export type AuthFlowState = 'default' | 'loading' | 'error' | 'validation';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Auth response fixtures (mirror createMockAuthResponse signature) ──────

export const MOCK_AUTH_FLOW_LOGIN_DEFAULT = {
  user: {
    id: 'usr_meeple_demo',
    email: 'marco@example.com',
    displayName: 'Marco',
    role: 'User' as const,
    emailVerified: true,
  },
  expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
};

export const MOCK_AUTH_FLOW_LOGIN_ERROR = {
  error: 'Invalid credentials',
};

export const MOCK_AUTH_FLOW_REGISTER_SUCCESS = {
  user: {
    id: 'usr_meeple_new',
    email: 'nuovo@example.com',
    displayName: 'Nuovo Utente',
    role: 'User' as const,
    emailVerified: false,
  },
  expiresAt: new Date(Date.now() + 86_400_000).toISOString(),
};

export const MOCK_AUTH_FLOW_REGISTER_CONFLICT = {
  error: 'Email already registered',
};

export const MOCK_AUTH_FLOW_PASSWORD_RESET_REQUEST = {
  success: true,
  message: 'Email inviata. Controlla la tua casella.',
};

export const MOCK_AUTH_FLOW_VERIFY_EMAIL_PENDING = {
  email: 'marco@example.com',
  resendAvailable: true,
  cooldownSeconds: 0,
};

export const MOCK_AUTH_FLOW_VERIFY_EMAIL_SUCCESS = {
  verified: true,
  redirectUrl: '/library',
};

// ── MSW handler switcher (state-driven scenarios) ─────────────────────────

export function mswForState(state: AuthFlowState) {
  if (state === 'loading') {
    return [
      http.post(`${API_BASE}/api/v1/auth/login`, () => new Promise<Response>(() => {})),
      http.post(`${API_BASE}/api/v1/auth/register`, () => new Promise<Response>(() => {})),
    ];
  }
  if (state === 'error') {
    return [
      http.post(`${API_BASE}/api/v1/auth/login`, () =>
        HttpResponse.json(MOCK_AUTH_FLOW_LOGIN_ERROR, { status: 401 })
      ),
      http.post(`${API_BASE}/api/v1/auth/register`, () =>
        HttpResponse.json(MOCK_AUTH_FLOW_REGISTER_CONFLICT, { status: 409 })
      ),
    ];
  }
  if (state === 'validation') {
    // Client-side validation only; no server roundtrip expected.
    // Form-level Zod schema enforces required + format; returned handler is no-op.
    return [
      http.post(`${API_BASE}/api/v1/auth/login`, () =>
        HttpResponse.json({ error: 'Validation should have caught this' }, { status: 400 })
      ),
    ];
  }
  return [
    http.post(`${API_BASE}/api/v1/auth/login`, () =>
      HttpResponse.json(MOCK_AUTH_FLOW_LOGIN_DEFAULT)
    ),
    http.post(`${API_BASE}/api/v1/auth/register`, () =>
      HttpResponse.json(MOCK_AUTH_FLOW_REGISTER_SUCCESS)
    ),
    http.post(`${API_BASE}/api/v1/auth/password-reset/request`, () =>
      HttpResponse.json(MOCK_AUTH_FLOW_PASSWORD_RESET_REQUEST)
    ),
    http.post(`${API_BASE}/api/v1/auth/verification/resend`, () =>
      HttpResponse.json({ success: true, cooldownSeconds: 30 })
    ),
  ];
}
