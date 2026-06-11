# sp5-profile-settings — MSW Gap Analysis

**Cross-referenced handler files**:
- `apps/web/src/__tests__/mocks/handlers/auth.handlers.ts` (2FA, sessions)
- `apps/web/src/__tests__/mocks/handlers/admin.handlers.ts` (api keys)
- `apps/web/src/__tests__/mocks/handlers/badges.handlers.ts` (achievements)
- `apps/web/src/__tests__/mocks/handlers/sessions.handlers.ts` (recent sessions)
- `apps/web/src/__tests__/mocks/handlers/library.handlers.ts` (library stats)

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/auth/me` | GET | ✅ `auth.handlers.ts:85-93` | None | useAuth() hydration |
| `/api/v1/auth/2fa/status` | GET | ⚠️ Verify | ADD | SettingsTab calls useQuery('2fa-status') |
| `/api/v1/auth/2fa/enable` | POST | ⚠️ Verify | ADD | Opens setup wizard step 1 |
| `/api/v1/auth/2fa/verify-setup` | POST | ⚠️ Verify | ADD | Wizard step 2 confirm |
| `/api/v1/auth/2fa/disable` | POST | ⚠️ Verify | ADD | D6 → Disable CTA |
| `/api/v1/auth/2fa/recovery-codes` | GET | ⚠️ Verify | ADD | Codes list (D5 + post-enable view) |
| `/api/v1/auth/2fa/recovery-codes/regenerate` | POST | ⚠️ Verify | ADD | "Rigenera codici" CTA |
| `/api/v1/auth/sessions` | GET | ⚠️ Verify | ADD | Active sessions list (D2/D6) |
| `/api/v1/auth/sessions/:id` | DELETE | ⚠️ Verify | ADD | Revoke session |
| `/api/v1/user/profile` | GET | ⚠️ Verify | ADD | Profile section data |
| `/api/v1/user/profile` | PUT | ⚠️ Verify | ADD | EditProfileSheet save |
| `/api/v1/user/preferences` | GET | ⚠️ Verify | ADD | PreferencesSection |
| `/api/v1/user/avatar` | POST | ⚠️ Verify | ADD | AvatarUpload component |
| `/api/v1/user/achievements` | GET | ⚠️ Verify | ADD | AchievementsGrid (Tab Achievements) |
| `/api/v1/user/sessions/recent` | GET | ⚠️ Verify | ADD | ActivityFeed + Overview |
| `/api/v1/library/stats` | GET | ⚠️ Verify | ADD | UserLibraryStats (Overview tab) |

## Recommended new handlers

Many handlers shared with settings cluster — co-locate or import via index.

```ts
// GET /api/v1/auth/2fa/status (twoFactorEnabled axis)
http.get(`${API_BASE}/api/v1/auth/2fa/status`, () => {
  return HttpResponse.json({
    isEnabled: false, // override per-state
    enabledAt: null,
    recoveryCodesCount: 0,
    trustedDevices: [],
  });
}),

// POST /api/v1/auth/2fa/enable (wizard step 1 — returns QR + secret)
http.post(`${API_BASE}/api/v1/auth/2fa/enable`, () => {
  return HttpResponse.json({
    secret: 'JBSWY3DPEHPK3PXP',
    qrCodeUrl: 'otpauth://totp/MeepleAI:marco@example.com?secret=JBSWY3DPEHPK3PXP&issuer=MeepleAI',
    manualCode: 'MFSA-K7P2-W9NB-4XLQ',
  });
}),

// POST /api/v1/auth/2fa/verify-setup (wizard step 2 — confirm PIN)
http.post(`${API_BASE}/api/v1/auth/2fa/verify-setup`, async ({ request }) => {
  const body = await request.json() as { code: string };
  if (!/^\d{6}$/.test(body.code)) {
    return HttpResponse.json({ error: 'Invalid code' }, { status: 400 });
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

// POST /api/v1/auth/2fa/disable
http.post(`${API_BASE}/api/v1/auth/2fa/disable`, async ({ request }) => {
  const body = await request.json() as { password: string; code?: string };
  if (!body.password || body.password === 'wrong') {
    return HttpResponse.json({ error: 'Invalid password' }, { status: 401 });
  }
  return HttpResponse.json({
    isEnabled: false,
    enabledAt: null,
    recoveryCodesCount: 0,
    trustedDevices: [],
  });
}),

// GET /api/v1/auth/sessions
http.get(`${API_BASE}/api/v1/auth/sessions`, () => {
  return HttpResponse.json([
    { id: 's1', device: 'Chrome 124 · macOS Sonoma', lastUsedAt: '2026-06-11T18:43:00Z', ipAddress: '192.168.1.42', location: 'Milano, IT', current: true },
    { id: 's2', device: 'Safari · iPhone 15 · iOS 17.4', lastUsedAt: '2026-06-10T19:00:00Z', ipAddress: '5.91.32.118', location: 'Milano, IT', current: false },
    { id: 's3', device: 'API client · mai-cli/0.4.2', lastUsedAt: '2026-06-08T11:00:00Z', ipAddress: null, location: 'Token tk_live_X3…', current: false },
  ]);
}),

// GET /api/v1/user/profile, /api/v1/user/preferences, /api/v1/user/achievements
// (see settings cluster msw-gap-analysis.md + sessions cluster for handler bodies)
```

## API contract notes

- ProfilePageContent uses `useQuery({ queryKey: userKeys.…, queryFn: api.…})`
  for multiple endpoints — all need handlers to avoid Loading state.
- 2FA wizard 3-step flow:
  1. SecuritySection → "Attiva 2FA" → `POST /enable` → `{ secret, qrCodeUrl, manualCode }`
  2. User scans QR + types code → `POST /verify-setup` → `{ isEnabled, recoveryCodes }`
  3. Codes shown 1-time → user confirms "Ho salvato" → close wizard
- Recovery codes shown 1-time only (security pattern). Fixture returns
  hardcoded codes for visual consistency.

## Storybook-specific MSW notes

- Fixture's `mswForState('default')` covers profile + preferences +
  achievements + recent sessions + 2FA status (disabled) — sufficient for
  Tab Overview/Achievements/Activity stories.
- `'tfa-off'`: same as default — D2 frame
- `'tfa-on'`: 2FA status returns `isEnabled: true` + trustedDevices — D6 frame
- `'wizard-setup'`: enables `POST /enable` mutation handler — D3 frame
  (user must click "Attiva 2FA" in Storybook play() to reach this state)
- `'wizard-verify'`: same as wizard-setup + verify-setup handler — D4 frame
- `'wizard-codes'`: returns 10 codes from verify-setup — D5 frame

## Storybook play() integration

To reach D3/D4/D5 wizard frames programmatically, story can use Storybook
`play` function:

```ts
import { userEvent, within } from '@storybook/test';

export const WizardSetup: Story = {
  parameters: { msw: { handlers: mswForState('wizard-setup') } },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    await userEvent.click(canvas.getByRole('button', { name: /Attiva 2FA/i }));
    // wizard opens, QR shown
  },
};
```

Phase 2 iteration step can refine the play() automation to drive D3 → D4 →
D5 flow within a single story for designer review.
