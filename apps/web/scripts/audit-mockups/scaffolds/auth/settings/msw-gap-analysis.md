# settings — MSW Gap Analysis

**Cross-referenced handler files**:
- `apps/web/src/__tests__/mocks/handlers/auth.handlers.ts` (2FA status, sessions)
- `apps/web/src/__tests__/mocks/handlers/admin.handlers.ts` (api keys, if shared)

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/auth/2fa/status` | GET | ⚠️ Verify | ADD if missing | Drives SecuritySection 2FA toggle state |
| `/api/v1/auth/2fa/enable` | POST | ⚠️ Verify | ADD | Opens 2FA setup wizard |
| `/api/v1/auth/2fa/disable` | POST | ⚠️ Verify | ADD | Disable 2FA flow |
| `/api/v1/auth/2fa/recovery-codes` | GET | ⚠️ Verify | ADD | Recovery codes list |
| `/api/v1/auth/2fa/recovery-codes/regenerate` | POST | ⚠️ Verify | ADD | Regenerate codes |
| `/api/v1/auth/sessions` | GET | ⚠️ Verify | ADD | Active sessions list for SecuritySection |
| `/api/v1/auth/sessions/:id` | DELETE | ⚠️ Verify | ADD | Revoke session |
| `/api/v1/user/profile` | GET | ⚠️ Verify | ADD | ProfileSection data |
| `/api/v1/user/profile` | PUT | ⚠️ Verify | ADD | ProfileSection save |
| `/api/v1/user/preferences` | GET | ⚠️ Verify | ADD | PreferencesSection data (theme/lingua/timezone) |
| `/api/v1/user/preferences` | PUT | ⚠️ Verify | ADD | PreferencesSection save |
| `/api/v1/user/api-keys` | GET | ⚠️ Verify | ADD | ApiKeysSection list |
| `/api/v1/user/api-keys` | POST | ⚠️ Verify | ADD | Create new API key |
| `/api/v1/user/api-keys/:id` | DELETE | ⚠️ Verify | ADD | Revoke API key |
| `/api/v1/user/ai-consent` | GET | ⚠️ Verify | ADD | AiConsentSection GDPR state |
| `/api/v1/user/ai-consent` | PUT | ⚠️ Verify | ADD | Update consent toggles |

## Recommended new handlers

```ts
// GET /api/v1/auth/2fa/status — drives SecuritySection
http.get(`${API_BASE}/api/v1/auth/2fa/status`, () => {
  return HttpResponse.json({
    isEnabled: false,
    enabledAt: null,
    recoveryCodesCount: 0,
    trustedDevices: [],
  });
}),

// POST /api/v1/auth/2fa/enable — opens TwoFactorSetup wizard
http.post(`${API_BASE}/api/v1/auth/2fa/enable`, () => {
  return HttpResponse.json({
    secret: 'JBSWY3DPEHPK3PXP',
    qrCodeUrl: 'otpauth://totp/MeepleAI:marco@example.com?secret=JBSWY3DPEHPK3PXP',
    manualCode: 'MFSA-K7P2-W9NB-4XLQ',
  });
}),

// POST /api/v1/auth/2fa/verify-setup — confirm PIN, enable 2FA
http.post(`${API_BASE}/api/v1/auth/2fa/verify-setup`, async ({ request }) => {
  const body = await request.json() as { code: string };
  if (body.code.length !== 6) {
    return HttpResponse.json({ error: 'Invalid code length' }, { status: 400 });
  }
  return HttpResponse.json({
    isEnabled: true,
    enabledAt: new Date().toISOString(),
    recoveryCodes: [
      'ABCD-1234-EFGH', 'IJKL-5678-MNOP', 'QRST-9012-UVWX',
      'YZAB-3456-CDEF', 'GHIJ-7890-KLMN', 'OPQR-1234-STUV',
      'WXYZ-5678-ABCD', 'EFGH-9012-IJKL', 'MNOP-3456-QRST',
      'UVWX-7890-YZAB',
    ],
  });
}),

// GET /api/v1/auth/sessions — active sessions list
http.get(`${API_BASE}/api/v1/auth/sessions`, () => {
  return HttpResponse.json([
    { id: 's1', device: 'Chrome 124 · macOS Sonoma', meta: '2 min ago · Milano, IT', current: true },
    { id: 's2', device: 'Safari · iPhone 15 · iOS 17.4', meta: '1 day ago · Milano, IT', current: false },
  ]);
}),

// GET /api/v1/user/profile
http.get(`${API_BASE}/api/v1/user/profile`, () => {
  return HttpResponse.json({
    user: {
      id: 'usr_meeple_demo',
      email: 'marco@example.com',
      displayName: 'Marco',
      username: 'meepler_42',
      bio: 'Boardgamer entusiasta',
      locale: 'it-IT',
      timezone: 'Europe/Rome',
      role: 'User',
    },
  });
}),

// GET /api/v1/user/preferences
http.get(`${API_BASE}/api/v1/user/preferences`, () => {
  return HttpResponse.json({
    theme: 'system',
    locale: 'it-IT',
    density: 'comfortable',
    reducedMotion: false,
  });
}),

// GET /api/v1/user/api-keys
http.get(`${API_BASE}/api/v1/user/api-keys`, () => {
  return HttpResponse.json([
    { id: 1, name: 'Produzione', createdAt: '2026-01-12T00:00:00Z', lastUsedAt: '2026-04-19T00:00:00Z', keyPrefix: 'mai_live_xK9p' },
    { id: 2, name: 'Sviluppo locale', createdAt: '2026-02-28T00:00:00Z', lastUsedAt: '2026-04-17T00:00:00Z', keyPrefix: 'mai_dev_3nQ7' },
    { id: 3, name: 'Webhook CI', createdAt: '2026-03-05T00:00:00Z', lastUsedAt: '2026-04-11T00:00:00Z', keyPrefix: 'mai_ci_w0Lm' },
  ]);
}),
```

## API contract notes

- `SettingsTab.tsx:22-25` uses `useQuery({ queryKey: ['2fa-status'], queryFn: ... })`
  — TanStack Query default settings (staleTime ~0, retry on focus) apply.
  Loading state shows skeleton until first response.
- Mutations expect optimistic-update friendly responses (return updated
  full entity, not partial diff).
- 2FA wizard flow (apps/web/src/components/auth/TwoFactorSetup.tsx) is a
  separate primitive — wizards open inline via Drawer (asse-B).

## Storybook-specific MSW notes

- Fixture's `mswForState('default')` covers all 7 section data endpoints
  in a single handler array — sufficient for Frame01-Frame07.
- `'loading'` state holds queries indefinitely → triggers Skeleton in each
  section.
- `'error'` state returns 500 → triggers `SectionErrorCard` component.
- 2FA mutation handlers (enable/disable/verify-setup) included in default
  state for designer to click through wizard flow inside Storybook.

## BGG ToS gating

CRITICAL: NO BGG handler is provided. `services` section in mockup
references BGG; codebase has `services` as `placeholder: true` per ADR
#1903. If designer requests BGG section restoration → escalate to user
(P5 BGG flag, see CLAUDE.md).
