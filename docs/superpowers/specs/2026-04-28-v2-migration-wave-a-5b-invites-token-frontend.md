# V2 Migration · Wave A.5b — `/invites/[token]` Greenfield Frontend Spec

**Issue**: #611 (parent: #579 Wave A umbrella)
**Branch**: `feature/issue-611-invites-token-fe-v2` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp3-accept-invite.jsx` (955 LOC, 7 stati mobile + 3 desktop frames)
**Backend prerequisite**: #607 ✅ MERGED (PR #610 squash `42c1a4b8d`) — `GameNightInvitation` aggregate + 3 endpoint pubblici
**Sequential prerequisites**: A.1 ✅ · A.2 ✅ · A.3a ✅ · A.3b ✅ · A.4 ✅ · A.5a ✅
**Status**: DRAFT 2026-04-28 — pending kickoff

---

## 1. Goals

1. **Greenfield route** `/invites/[token]` che consuma backend pubblico A.5a (`GET/POST /api/v1/game-nights/invitations/{token}`).
2. Replicare 1:1 mockup `sp3-accept-invite` con 7 stati pubblici + layout desktop split.
3. Riflettere idempotency contract D2(b) backend nel UX: 409 Conflict → re-fetch + already-responded surface; 410 Gone → token-expired surface.
4. Confetti CSS-only su `accepted-success` (no JS particle library).
5. Bootstrap baselines su CI runner Linux x86-64 (visual-migrated 2 PNG + v2-states 14 PNG = 7 stati × 2 viewport).
6. Pattern emerso A.4 `cookie banner suppression via addInitScript` applicato anche qui.

> **Route path divergence**: il mockup usa `/accept-invite/[token]` ma umbrella #579 + spec A.5a fissano la route target a `/invites/[token]`. La route `/accept-invite` esistente (auth invitation con password setup) resta intatta — feature distinte. Email RSVP URL già allineato in `GameNightInvitation`: `rsvpAcceptUrl = $"{baseUrl}/invites/{token}?action=accept"`.

## 2. Non-goals

- Calendar `.ics` generation (link visivo nella mockup → stub `href="#"` con `data-testid="ics-link"` per future wire-up).
- Mailto host link reale (visivo only, `mailto:` placeholder).
- Login flow integration: "Accetta e accedi" → redirect a `/login?redirect=/invites/{token}` (login flow è feature separata, NON modificare in A.5b).
- `/game-nights/{id}` session detail route (post-Alpha).
- Real-time roster updates via SSE (Alpha: snapshot from API).
- Server-side `?action=accept|decline` query string auto-action (post-Alpha — Alpha: Lazy click required, riflessione UX).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Server page | `apps/web/src/app/(public)/invites/[token]/page.tsx` | **Create** (server component, `generateMetadata`, SSR fetch) |
| Client page | `apps/web/src/app/(public)/invites/[token]/page-client.tsx` | **Create** (state orchestration + override hook) |
| Page test | `apps/web/src/app/(public)/invites/[token]/__tests__/page-client.test.tsx` | **Create** |
| Hook fetch | `apps/web/src/hooks/useGameNightInvitation.ts` | **Create** (TanStack Query con SSR seed via `initialData`) |
| Hook respond | `apps/web/src/hooks/useRespondToInvitation.ts` | **Create** (Mutation con FSM 5-stati: idle/submitting/success/error/conflict) |
| API client | `apps/web/src/lib/api/game-night-invitations.ts` | **Create** (`getInvitation`, `respondToInvitation` + tipo `PublicGameNightInvitationDto`, `GameNightInvitationResponseDto`) |
| Component | `apps/web/src/components/ui/v2/invites/hero.tsx` | **Create** (Hero copy + game-night pill, mobile compact + desktop full) |
| Component | `apps/web/src/components/ui/v2/invites/invite-host-card.tsx` | **Create** (host avatar + welcome message + online dot) |
| Component | `apps/web/src/components/ui/v2/invites/session-meta-grid.tsx` | **Create** (5 row: Data/Ora/Durata/Luogo/Giocatori, mono labels) |
| Component | `apps/web/src/components/ui/v2/invites/accepted-success-shell.tsx` | **Create** (icona + riepilogo + .ics stub + confetti) |
| Component | `apps/web/src/components/ui/v2/invites/declined-shell.tsx` | **Create** (neutral non-shame + undo link) |
| Component | `apps/web/src/components/ui/v2/invites/confetti.tsx` | **Create** (CSS-only 14 particles, useMemo) |
| Component | `apps/web/src/components/ui/v2/invites/error-banner.tsx` | **Create** (variant: token-expired \| token-invalid, riusa pattern Banner A.2) |
| Component | `apps/web/src/components/ui/v2/invites/meeple-card-game-compact.tsx` | **Create** (variant compact di MeepleCardGame, 56×56 cover) |
| Component | `apps/web/src/components/ui/v2/invites/auth-card-shell.tsx` | **Create** (wrapper card 380px max-width, riusabile) |
| Component | `apps/web/src/components/ui/v2/invites/desktop-split-layout.tsx` | **Create** (grid 1.1fr/1fr, LEFT context + RIGHT auth-card) |
| Component index | `apps/web/src/components/ui/v2/invites/index.ts` | **Create** (barrel) |
| Override hook | `apps/web/src/hooks/useStateOverride.ts` (se non esistente) o reuse A.4 pattern | **Reuse o Create** |
| **Reuse Phase 0** | `apps/web/src/lib/utils/entity-hsl.ts` | **Reuse** (helper per host avatar tinta dinamica) |
| **Reuse A.3b** | `apps/web/src/hooks/useUrlHashState.ts` | **Reuse** (potenziale URL persistence per state, opzionale) |
| **Reuse A.4** | `seedCookieConsent` helper Playwright | **Reuse** (pattern cookie banner suppression) |
| Visual-test fixture | `apps/web/src/lib/invites/visual-test-fixture.ts` | **Create** (constant `VISUAL_TEST_FIXTURE_TOKEN = 'VISUAL-TEST-FIXTURE-A5B-22CHARS'`, gate `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED`) |
| i18n IT | `apps/web/src/locales/it.json` § `pages.invites` | **Add** (~40 keys, mockup è IT-only quindi IT canonico) |
| i18n EN | `apps/web/src/locales/en.json` § `pages.invites` | **Add** (~40 keys, traduzione 1:1) |
| Visual test | `apps/web/e2e/visual-migrated/sp3-accept-invite.spec.ts` | **Create** (1 desktop + 1 mobile) |
| State test | `apps/web/e2e/v2-states/invites-token.spec.ts` | **Create** (7 stati × 2 viewport = 14 PNG) |
| Baselines | `apps/web/e2e/visual-migrated/sp3-accept-invite.spec.ts-snapshots/*.png` | **Create** (2 PNG via CI bootstrap) |
| Baselines | `apps/web/e2e/v2-states/invites-token.spec.ts-snapshots/*.png` | **Create** (14 PNG via CI bootstrap) |
| Workflow visual-test env | `.github/workflows/visual-regression-migrated.yml` | **Edit** (aggiungi `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` se non già presente per A.4) |

### 3.2 SSR fetch + state derivation

```typescript
// page.tsx (server component, ISR revalidate=0 — invite lifecycle is short, no caching)
export const dynamic = 'force-dynamic';

export async function generateMetadata({ params }): Promise<Metadata> {
  const { token } = await params;
  // No fetch in metadata — title statico per privacy (token nel path)
  return { title: 'Invito · MeepleAI', robots: { index: false, follow: false } };
}

export default async function InvitesTokenPage({ params, searchParams }) {
  const { token } = await params;
  const sp = await searchParams;

  // Visual-test fixture short-circuit (production = constant-fold dead-code-eliminato)
  const fixture = tryLoadVisualTestFixture(token);
  if (fixture) {
    return <InvitesPageClient initialData={fixture} stateOverride={sp.state} token={token} />;
  }

  // SSR fetch — fault-tolerant: NotFound/Gone surface non sono error, sono UX states
  const result = await getInvitation(token).catch(err => ({ kind: 'error' as const, error: err }));

  if (result.kind === 'error') {
    if (result.error.status === 404) return <InvitesPageClient initialState="token-invalid" token={token} />;
    if (result.error.status === 410) return <InvitesPageClient initialState="token-expired" token={token} />;
    throw result.error;  // 500 surface via Next.js error.tsx boundary
  }

  return <InvitesPageClient initialData={result.data} stateOverride={sp.state} token={token} />;
}
```

### 3.3 State derivation (page-client)

```typescript
type InviteState =
  | 'default'           // token valido, !session.user
  | 'logged-in'         // token valido, session.user
  | 'accepted-success'  // POST 200, just accepted
  | 'declined'          // POST 200, just declined
  | 'token-expired'     // GET 410 o status in {Expired, Cancelled} o pending-past-expiry
  | 'token-invalid'     // GET 404
  | 'already-accepted'; // GET 200 + AlreadyRespondedAs="Accepted"

function deriveState(data: PublicGameNightInvitationDto | null, session, mutationResult): InviteState {
  // Override (production: dead-code via NODE_ENV constant-fold)
  if (process.env.NODE_ENV !== 'production' && stateOverride) return stateOverride as InviteState;

  if (!data) return data === null && status === 404 ? 'token-invalid' : 'token-expired';
  if (mutationResult?.kind === 'success' && mutationResult.action === 'accepted') return 'accepted-success';
  if (mutationResult?.kind === 'success' && mutationResult.action === 'declined') return 'declined';
  if (data.alreadyRespondedAs === 'Accepted') return 'already-accepted';
  if (data.alreadyRespondedAs === 'Declined') return 'declined';
  if (data.status === 'Expired' || data.status === 'Cancelled') return 'token-expired';
  return session.user ? 'logged-in' : 'default';
}
```

### 3.4 Idempotency contract D2(b) — UX mapping

| Backend response | Frontend reaction |
|------------------|-------------------|
| 200 OK (transition Pending → Accepted) | `mutationResult = { kind: 'success', action: 'accepted' }` → state `accepted-success` (confetti) |
| 200 OK (no-op same state) | Same as above (UX non distingue — l'utente può aver clickato 2x) |
| 409 Conflict (switch attempt) | `mutationResult = { kind: 'conflict' }` → re-fetch GET → re-derive → `already-accepted` o `declined` con banner "La risposta non può essere modificata" |
| 410 Gone (terminal/past-expiry) | `mutationResult = { kind: 'gone' }` → state `token-expired` |
| 400 Bad Request | `mutationResult = { kind: 'validation-error' }` → toast inline (edge case, response parse error) |

## 4. Tests (TDD red phase)

### 4.1 Unit tests
- `__tests__/invite-host-card.test.tsx` — render con/senza welcomeMessage, host color HSL applicato.
- `__tests__/session-meta-grid.test.tsx` — 5 row presenti, mono font su Ora/Durata.
- `__tests__/roster-mini.test.tsx` — host pill, ordering by isHost desc.
- `__tests__/confetti.test.tsx` — 14 particles, `aria-hidden="true"`.
- `__tests__/page-client.test.tsx` — state derivation table-driven test (7 states × auth states).
- `__tests__/use-respond-to-invitation.test.tsx` — 409 Conflict → re-fetch trigger + state transition.

### 4.2 Visual-migrated tests
- `e2e/visual-migrated/sp3-accept-invite.spec.ts` — fullPage screenshot mobile 375 + desktop 1440 contro mockup baseline (default state, fixture).

### 4.3 V2-states tests
- `e2e/v2-states/invites-token.spec.ts` — 7 stati × 2 viewport = 14 PNG. Override via `?state=...`. Ogni test:
  ```typescript
  test('default mobile', async ({ page }) => {
    await seedCookieConsent(page);
    await page.goto('/invites/VISUAL-TEST-FIXTURE-A5B-22CHARS?state=default');
    await page.setViewportSize({ width: 375, height: 800 });
    await expect(page).toHaveScreenshot('default-mobile.png', { fullPage: true });
  });
  ```

## 5. Bootstrap workflow

```bash
# Trigger CI bootstrap (Linux x86-64 baselines)
gh workflow run 266963272 --ref feature/issue-611-invites-token-fe-v2 \
  -f mode=bootstrap -f project_filter=both

# After run completes successfully:
gh run download <run-id> -n visual-migrated-baselines -D apps/web/e2e/visual-migrated/sp3-accept-invite.spec.ts-snapshots/
gh run download <run-id> -n v2-states-baselines -D apps/web/e2e/v2-states/invites-token.spec.ts-snapshots/

# Filter copy (non-clobbering altre route)
git add apps/web/e2e/visual-migrated/sp3-accept-invite.spec.ts-snapshots/
git add apps/web/e2e/v2-states/invites-token.spec.ts-snapshots/
git commit -m "test(invites): bootstrap A.5b visual baselines (16 PNG Linux x86-64)"
```

## 6. Commit boundaries

1. **Commit 1** — i18n + types + API client + visual-test fixture (foundation, no UI).
2. **Commit 2** — v2 component family (10 component + index).
3. **Commit 3** — hooks + page-client + page (server + client orchestration).
4. **Commit 4** — unit tests + visual-test specs (TDD red phase, fail without baselines).
5. **Commit 5** — CI bootstrap baselines (16 PNG via workflow run).

## 7. Acceptance Criteria

- [ ] Route `/invites/[token]` accessible su Alpha mode (backend già fuori da Alpha gate per A.5a)
- [ ] 7 stati v2 implementati con visual test (mobile 375 + desktop 1440)
- [ ] Hook `useGameNightInvitation` SSR seed + `useRespondToInvitation` con FSM 5-stati
- [ ] Override `?state=...` guardato da `NODE_ENV !== 'production'` (dead-code-eliminated in prod)
- [ ] Idempotency D2(b) reflected: 409 → re-fetch + already-responded surface
- [ ] i18n keys IT/EN per tutti i copy (~40 keys × 2 locales)
- [ ] Confetti CSS animation only (no JS particle library)
- [ ] Bootstrap baselines on CI runner Linux x86-64 (2 visual-migrated + 14 v2-states)
- [ ] Bundle baseline aggiornato (delta target ≤ +30KB)
- [ ] axe-core a11y check pulito (focus management, aria-live banner, host avatar `aria-hidden`)
- [ ] Cookie banner suppression via `addInitScript` applicato (pattern A.4)

## 8. Performance targets

- SSR fetch p95 < 200ms (single GET endpoint, no cache by design)
- Hydration < 100ms (no heavy components)
- Confetti animation: 14 particles × 1.4-2.4s, GPU-only (`transform`, `opacity`)
- LCP < 2.0s mobile (hero copy + host avatar inline)

## 9. Rollback plan

- Single-PR delivery → revert PR se issues post-merge.
- Backend A.5a immutato (already merged, indipendente).
- Email service `IGameNightEmailService` continuerà a generare URL `/invites/{token}` — se la route fallisce, l'email rimane inviata ma il link rotto fino a fix.

## 10. Decisioni open (richiedenti conferma utente)

D1. **Confetti viewport**: solo `accepted-success` o anche `logged-in` → `accepted-success` transition? → **default proposto**: solo on `accepted-success` mount (one-shot).
D2. **`?action=accept|decline` auto-action**: quando l'utente clicca link email che già contiene action, autosubmit on mount o richiedi click? → **default proposto**: richiedi click (UX safety, evita double-action su email forwarding).
D3. **Logged-in detection**: lettura `meepleai-session` cookie via `cookies()` API server-side, oppure SWR/TanStack on client? → **default proposto**: server-side cookie read in `page.tsx` per evitare hydration flash.
D4. **`.ics` calendar link**: stub `href="#"` o disabilitato visivamente? → **default proposto**: stub con `data-testid="ics-link"` per future wire-up post-Alpha.
D5. **Roster confermati su `already-accepted`**: ✅ **RISOLTA** — `PublicGameNightInvitationDto` espone solo `AcceptedSoFar` count, NO roster names (verificato `apps/api/src/Api/BoundedContexts/GameManagement/Application/DTOs/GameNights/PublicGameNightInvitationDto.cs`). Decisione: per Alpha mostrare aggregate count "✓ {AcceptedSoFar} confermati / {ExpectedPlayers} attesi" senza component `RosterMini`. Mockup `RosterMini` rimosso da scope component map (riga "Component | roster-mini.tsx" → **scartato**). Banner "Hai già confermato" + count + CTA (riusano sezione `default`/`logged-in` con override visuale).

---

**Spec author**: Claude
**Date**: 2026-04-28
**Estimated delivery**: 5 commit, single PR
