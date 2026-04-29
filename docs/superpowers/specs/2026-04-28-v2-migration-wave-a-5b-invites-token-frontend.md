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
| **Reuse A.4** | `apps/web/src/components/ui/data-display/meeple-card.tsx` | **Reuse** con `variant="compact"` (56×56 cover, consistente con A.3b/A.4 grid/list/featured pattern — NO new component) |
| Component index | `apps/web/src/components/ui/v2/invites/index.ts` | **Create** (barrel — 8 component, NO meeple-card/auth-card-shell/desktop-split) |
| Inline page-client | `auth-card-shell` + `desktop-split-layout` | **Inline** in `page-client.tsx` (single-use, defer abstraction per YAGNI — promote a `ui/v2/shared/*` solo se A.6+ ne hanno bisogno) |
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

  // SSR fetch — fault-tolerant: NotFound surface non è error, è UX state.
  // NOTA: A.5a backend GET ritorna 200 con `status: "Expired"|"Cancelled"` nel payload (NON 410).
  // 410 è esclusivo del POST .../respond endpoint. Solo 404 va catchato come UX state qui.
  const result = await getInvitation(token).catch(err => ({ kind: 'error' as const, error: err }));

  if (result.kind === 'error') {
    if (result.error.status === 404) return <InvitesPageClient initialState="token-invalid" token={token} />;
    throw result.error;  // 5xx/network → 500 surface via Next.js error.tsx boundary
  }

  // Expired/Cancelled detection avviene in deriveState() via data.status (no early return necessario)
  return <InvitesPageClient initialData={result.data} stateOverride={sp.state} token={token} />;
}
```

### 3.3 State derivation (page-client)

```typescript
type InviteState =
  | 'default'           // token valido, !session.user
  | 'logged-in'         // token valido, session.user (CTA copy + display name pre-fill)
  | 'accepted-success'  // POST 200, just accepted (confetti one-shot)
  | 'declined'          // POST 200 OR alreadyRespondedAs='Declined'
  | 'token-expired'     // status in {Expired, Cancelled} o POST 410 Gone
  | 'token-invalid'     // GET 404
  | 'already-accepted'; // GET 200 + alreadyRespondedAs='Accepted' (no recent mutation)

// Contract: data is NEVER null when this function runs. Null cases (404/5xx) are handled
// by page.tsx via initialState parameter and short-circuit before page-client mount.
function deriveState(
  data: PublicGameNightInvitation,
  session: Session | null,
  mutationResult: RespondToInvitationResult | null,
  initialState: InviteState | null,
  stateOverride: string | null,
): InviteState {
  // 1. Server-side initial state (404 → token-invalid; 5xx already threw to error.tsx)
  if (initialState) return initialState;

  // 2. Test override (production: dead-code via NODE_ENV constant-fold)
  if (process.env.NODE_ENV !== 'production' && stateOverride) return stateOverride as InviteState;

  // 3. Recent mutation outcomes (precedenza su entity status — l'utente ha appena cliccato)
  if (mutationResult?.kind === 'success' && mutationResult.action === 'Accepted') return 'accepted-success';
  if (mutationResult?.kind === 'success' && mutationResult.action === 'Declined') return 'declined';
  if (mutationResult?.kind === 'gone') return 'token-expired';
  // 409 conflict: handled by re-fetch + fall-through to alreadyRespondedAs branch below

  // 4. Entity status from GET (terminal states)
  if (data.status === 'Expired' || data.status === 'Cancelled') return 'token-expired';

  // 5. Pre-existing response (alreadyRespondedAs from server, no recent mutation)
  if (data.alreadyRespondedAs === 'Accepted') return 'already-accepted';
  if (data.alreadyRespondedAs === 'Declined') return 'declined';

  // 6. Pending invitation (default vs logged-in differs by CTA copy + pre-fill)
  return session?.user ? 'logged-in' : 'default';
}
```

**Logged-in delta UX** (rispetto a `default`):
| Aspect | `default` (anonymous) | `logged-in` (authenticated) |
|---|---|---|
| CTA Accept copy | "Accetto, ci sarò →" | "Accetto, ci sarò → (come {displayName})" |
| Display name | not shown | pre-fill nella confirmation card |
| Step skip | nessuno (click esplicito sempre richiesto) | nessuno (idempotency contract) |

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

- [ ] `GET /invites/{validToken}` ritorna 200 + render `<InvitesPageClient>` con `data.status="Pending"` quando `ALPHA_MODE=true`. Verificato via E2E `e2e/v2-states/invites-token.spec.ts` test `default mobile` + `default desktop`.
- [ ] `GET /invites/{unknownToken}` ritorna 200 + render `token-invalid` state (NOT 404 page Next.js) — verificato via E2E test `token-invalid` × 2 viewport.
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
