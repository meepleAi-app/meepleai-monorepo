# onboarding — MSW Gap Analysis

**Cross-referenced handler files**:
- `apps/web/src/__tests__/mocks/handlers/games.handlers.ts` (catalog for FirstGameStep)
- `apps/web/src/__tests__/mocks/handlers/auth.handlers.ts` (auth/onboarding completion)

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/games` | GET | ✅ `games.handlers.ts:29-35` | None | List all games — used by `FirstGameStep` catalog search |
| `/api/v1/games/:id` | GET | ✅ `games.handlers.ts:38-…` | None | Game detail card |
| `/api/v1/library/games` | POST | ⚠️ Verify | ADD if missing | Used by `FirstGameStep.onGameAdded` to add game to user library |
| `/api/v1/auth/me` | GET | ✅ `auth.handlers.ts:85-93` | None | `useAuth().refreshUser()` after completion |
| `/api/v1/auth/onboarding/complete` | POST | ⚠️ Gap | ADD | Called from `handleComplete` (OnboardingGenericWizard.tsx:55) |
| `/api/v1/user/preferences/interests` | PATCH | ⚠️ Gap | ADD if used by InterestsStep | Persists selected categories |
| `/api/v1/user/preferences/avatar` | POST | ⚠️ Verify | Not used by wizard (deferred to settings) | — |

## Recommended new handlers

```ts
// POST /api/v1/auth/onboarding/complete
http.post(`${API_BASE}/api/v1/auth/onboarding/complete`, async ({ request }) => {
  const body = await request.json() as { skipped?: boolean };
  return HttpResponse.json({
    user: {
      id: 'usr_meeple_demo',
      email: 'marco@example.com',
      displayName: 'Marco',
      role: 'User' as const,
      emailVerified: true,
      onboardingCompleted: true,
      onboardingSkipped: body.skipped ?? false,
    },
  });
}),

// PATCH /api/v1/user/preferences/interests
http.patch(`${API_BASE}/api/v1/user/preferences/interests`, async ({ request }) => {
  const body = await request.json() as { categories: string[] };
  if (!Array.isArray(body.categories)) {
    return HttpResponse.json({ error: 'Invalid categories' }, { status: 400 });
  }
  return HttpResponse.json({ success: true, savedCategories: body.categories });
}),

// POST /api/v1/library/games (if not covered by library.handlers.ts)
http.post(`${API_BASE}/api/v1/library/games`, async ({ request }) => {
  const body = await request.json() as { gameId: string };
  return HttpResponse.json({ success: true, addedGameId: body.gameId });
}),
```

## API contract notes

- `OnboardingGenericWizard.handleComplete` (line 55) calls
  `api.auth.completeOnboarding(false)` then `refreshUser()`. The `false`
  argument indicates "fully completed" (not skipped); `true` would mean
  "skipped via X button". Backend `RegistrationMode` config-aware.
- `FirstGameStep.onGameAdded` triggers `setFirstGameCompleted(true)` —
  gates `validate()` so the wizard advances only after the user adds a
  game OR clicks skip explicitly.
- `InterestsStep.onSkip` and `FirstGameStep.onSkip` both set the
  corresponding completion flag → wizard treats skip as completion.

## Storybook-specific MSW notes

- Fixture `mswForState('default')` returns 8-game catalog + onboarding
  complete success. Sufficient for all 5 mockup frames.
- Selection state (`'selected-min3'`, `'all-selected'`) does not require
  different server responses — selection is client-side only. State flag
  is documentation for designer review.
- `completion-confetti` state could trigger a toast.success rendering in
  Storybook decorator (sonner Provider already wired in Phase 4 prelude).

## BGG handler exclusion

CRITICAL: NO BGG search handler is provided in the fixture. The wizard's
`FirstGameStep` does NOT call `useSearchBggGames` (admin-only, ADR #1903).
The handler MUST NOT mock `/api/v1/admin/bgg/*` endpoints for user-side
flows.
