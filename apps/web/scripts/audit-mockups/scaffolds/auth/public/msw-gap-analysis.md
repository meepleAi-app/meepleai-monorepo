# public — MSW Gap Analysis

**Cross-referenced handler file**: `apps/web/src/__tests__/mocks/handlers/auth.handlers.ts`

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/auth/me` | GET | ✅ `auth.handlers.ts:85-93` | Override in fixture | Default returns `mockUserAuth()`; for public landing we OVERRIDE to 401 (anonymous) to avoid `redirect('/library')`. |
| `/api/v1/auth/session/status` | GET | ✅ exists | Override in fixture | Same — must return `{ authenticated: false }`. |
| `/api/v1/landing/stats` | GET | ⚠️ Verify | OPTIONAL | If `WelcomeHero` shows live "10K+ users" stat, add handler. Currently SSR'd from constants. |
| `/api/v1/landing/testimonials` | GET | ⚠️ Verify | OPTIONAL | Probably SSR constants (no client fetch). |

## Recommended handlers (already in fixture)

```ts
// GET /api/v1/auth/me — anonymous (forces no redirect)
http.get(`${API_BASE}/api/v1/auth/me`, () =>
  HttpResponse.json({ error: 'Unauthorized' }, { status: 401 })
),

// GET /api/v1/auth/session/status — anonymous
http.get(`${API_BASE}/api/v1/auth/session/status`, () =>
  HttpResponse.json({ authenticated: false }, { status: 200 })
),
```

## API contract notes

- `getServerUser()` (called in page.tsx:135) wraps the `/api/v1/auth/me`
  cookie-based session check. In server context it reads from
  `cookies()` (Next.js) — Storybook's MSW intercepts HTTP fetch only.
  Server Components in Storybook 10.4 may need explicit wrapping.
- `LandingPage` SEO metadata (page.tsx:38-101) is statically exported —
  no API calls needed.
- `structuredData` JSON-LD is hardcoded (page.tsx:103-125) — no fetch.

## Storybook-specific MSW notes

- All 5 frames use the **same handler config** (anonymous user). State
  variants (`mobile-drawer-open`) are viewport-only.
- If Storybook 10.4 Server Component rendering FAILS (intl context, RSC
  flow): fallback is wrapping `LandingPage` in a thin Client decorator
  that mocks the redirect skip:

  ```tsx
  decorators: [
    (Story) => (
      <div className="min-h-dvh bg-background">
        {/* Stub PublicHeader since (public)/layout.tsx is not rendered */}
        <header className="border-b">…</header>
        <Story />
      </div>
    ),
  ]
  ```

- Marketing components (WelcomeHero, etc.) live in `@/components/landing`
  — verify imports do NOT require browser-only APIs (e.g. `window`,
  `IntersectionObserver`). If present → flag in Phase 2 iteration.

## Server Component compatibility risk

**RISK (Phase 2 catch)**: Storybook 10.4 may not fully support async Server
Components. Symptoms:
- "Cannot read properties of undefined (reading 'tag')" runtime error
- Hydration mismatch warnings
- Suspense fallback never resolves

**Mitigation**:
1. Verify in Phase 2 first iteration (run `pnpm storybook` on
   `Pages/Auth/Public Landing/Frame01_LandingHero`).
2. If fails → convert story to render `<WelcomeHero />` directly +
   document in story header that full LandingPage matrix requires the
   existing `Pages/LandingPage/Default` story (Chromatic-only).
3. Phase 4 prelude `IntlProvider` hardening already provides the
   `react-intl` context — verify Storybook decorator chain in
   `.storybook/preview.tsx`.
