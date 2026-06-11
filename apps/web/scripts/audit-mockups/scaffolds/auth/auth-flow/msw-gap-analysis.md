# auth-flow — MSW Gap Analysis

**Cross-referenced handler file**: `apps/web/src/__tests__/mocks/handlers/auth.handlers.ts`

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/auth/login` | POST | ✅ `auth.handlers.ts:46-71` | None | Already covers invalid credentials (401) + role-based responses |
| `/api/v1/auth/register` | POST | ✅ `auth.handlers.ts:18-44` | None | Already covers 409 email-exists conflict |
| `/api/v1/auth/logout` | POST | ✅ `auth.handlers.ts:73-83` | None | — |
| `/api/v1/auth/me` | GET | ✅ `auth.handlers.ts:85-93` | None | — |
| `/api/v1/auth/session/status` | GET | ✅ `auth.handlers.ts:95-…` | None | — |
| `/api/v1/auth/session/extend` | POST | ✅ likely covered | Verify in `handlers/auth.handlers.ts` full read | — |
| `/api/v1/auth/password-reset/request` | POST | ⚠️ Gap | ADD | Used by `ForgotPasswordScreen` → real route `/reset-password` |
| `/api/v1/auth/password-reset/confirm` | POST | ⚠️ Gap | ADD | Used by `ResetPasswordScreen` |
| `/api/v1/auth/verification/resend` | POST | ⚠️ Gap | ADD | Used by `VerifyEmailScreen` → real route `/verification-pending` |
| `/api/v1/auth/verification/verify` | POST | ⚠️ Gap | ADD | Used by `/verify-email?token=…` route |
| `/api/v1/auth/2fa/setup` | POST | ⚠️ Verify | Check `auth.handlers.ts` for 2FA section | TwoFactorSetup component |
| `/api/v1/auth/2fa/verify-setup` | POST | ⚠️ Verify | Check `auth.handlers.ts` for 2FA section | Used by inline PIN confirm |
| `/api/v1/auth/2fa/login-verify` | POST | ⚠️ Verify | Used by `LoginPageContent.handle2FAVerify` | — |
| `/api/v1/oauth/{provider}/callback` | GET | ⚠️ Verify | Verify in handlers; used by `/oauth-callback?provider=…` route | — |

## Recommended new handlers

Add the following 4 minimum handlers to `auth.handlers.ts` (or new
`auth-flow.handlers.ts` colocated):

```ts
// POST /api/v1/auth/password-reset/request
http.post(`${API_BASE}/api/v1/auth/password-reset/request`, async ({ request }) => {
  const body = await request.json() as { email: string };
  if (!body.email || !body.email.includes('@')) {
    return HttpResponse.json({ error: 'Invalid email' }, { status: 400 });
  }
  return HttpResponse.json({ success: true, message: 'Email inviata' });
}),

// POST /api/v1/auth/password-reset/confirm
http.post(`${API_BASE}/api/v1/auth/password-reset/confirm`, async ({ request }) => {
  const body = await request.json() as { token: string; password: string };
  if (!body.token || body.token === 'invalid') {
    return HttpResponse.json({ error: 'Invalid or expired token' }, { status: 400 });
  }
  return HttpResponse.json({ success: true });
}),

// POST /api/v1/auth/verification/resend
http.post(`${API_BASE}/api/v1/auth/verification/resend`, async () => {
  return HttpResponse.json({ success: true, cooldownSeconds: 30 });
}),

// POST /api/v1/auth/verification/verify
http.post(`${API_BASE}/api/v1/auth/verification/verify`, async ({ request }) => {
  const body = await request.json() as { token: string };
  if (!body.token || body.token === 'invalid') {
    return HttpResponse.json({ error: 'Invalid token', type: 'invalid' }, { status: 400 });
  }
  if (body.token === 'expired') {
    return HttpResponse.json({ error: 'Token expired', type: 'expired' }, { status: 410 });
  }
  return HttpResponse.json({ verified: true, redirectUrl: '/library' });
}),
```

## API contract notes

- All auth endpoints emit `X-Correlation-Id` header per existing pattern
  (auth.handlers.ts:42, 68, 78, 88).
- 2FA flow: login returns `{ requiresTwoFactor: true, tempSessionToken }` →
  client calls `/api/v1/auth/2fa/login-verify` with tempSessionToken +
  6-digit code. Verify handler exists.

## Storybook-specific MSW notes

- Fixture's `mswForState('default')` returns success responses for the 4
  most-used endpoints (login + register + password-reset/request +
  verification/resend) — sufficient for the 6 mockup frames.
- Loading state uses `new Promise<Response>(() => {})` to hold the request
  indefinitely (mirror Phase 2.5 library pattern, `_content.stories.tsx:37`).
- Error state returns 401 (login) + 409 (register) to surface the LoginForm
  error banner without form-level validation.
