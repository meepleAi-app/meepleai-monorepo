## Contesto

Follow-up da [#1899 P4 cross-cutting audit](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899) MVP cut (sessione 38, 2026-06-05).

Spec consolidato MAJ-11 (Claude Design alignment review) prescrive 5 user journey cross-asse data-driven. Status MVP:
- #1+#2+#3: 3/5 PARTIAL coverage via skeleton tolerant → richiedono full data-driven
- #4 (Invitation/Notification): BLOCKED su DEC-5 notification system ship
- #5 (Session live toast switching): BLOCKED su asse A polymorphic wire FE completo

Riferimento audit: [`docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md`](https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md) DEC-P4-2 Task C + DEC-P4-3.

## Scope

3 nuovi spec full data-driven (journey #4 + #5 deferred wave futuro):

### Journey #1 — Dashboard → drawer GN → Player swap → ESC

File: `apps/web/e2e/cross-asse-journey-1-dashboard-drawer-stack.spec.ts`

Flow:
1. Seed 1 GN Published + 2 player roster
2. Login + naviga `/dashboard`
3. Click GN card in Prossimi section → drawer GN apre (level 1)
4. Click Player avatar inside drawer → Player drawer push (level 2)
5. ESC → Player drawer chiude, GN drawer rimane (level 1)
6. ESC → GN drawer chiude (level 0)
7. Riapri GN → backdrop click → closeAll

Assertions: cascade stack depth, focus management, prefers-reduced-motion.

### Journey #2 — Dashboard empty → CTA wizard → Live opt-in

File: `apps/web/e2e/cross-asse-journey-2-empty-cta-wizard-live.spec.ts`

Flow:
1. Seed fresh user (no GN)
2. Login + naviga `/dashboard`
3. Verify Prossimi EmptySection con CTA "Crea prima GameNight"
4. Click CTA → naviga `/game-nights/new`
5. Wizard 3-step compila (Quando+Dove → Invita → Game suggested)
6. Submit → GN creata + redirect a `/game-nights/[id]`
7. Click "Apri live mode" → opt-in toast → confirm
8. Naviga a `/game-nights/[id]/live` con session creata

Assertions: wizard validation per step, GN creation API, live mode state transition.

### Journey #3 — Game Detail tab Partite paginazione inline

File: `apps/web/e2e/cross-asse-journey-3-game-detail-tab-partite.spec.ts`

Flow:
1. Seed 1 Game + 15 Session completed (per testare paginazione)
2. Login + naviga `/games/[id]` tab Partite
3. Verify primi 10 session card visibili
4. Click "Carica altri" → 5 session aggiuntive appended (NO navigate /sessions)
5. Verify URL invariato (no route change)
6. Verify focus management su nuove card

Assertions: paginazione inline (no navigation), focus management, performance.

## Acceptance

- [ ] 3 nuovi spec file in `apps/web/e2e/cross-asse-journey-*.spec.ts`
- [ ] 3/3 spec wirano seedEntities (Task B factory) + seedAuthSession
- [ ] 3/3 spec passano in CI con full data-driven assertions (no tolerant fallback)
- [ ] CI policy: 3/3 spec diventano blocking (decisione separata da CI policy change su skeleton esistenti)
- [ ] Designer review checklist per ogni journey nel PR body (DEC-3 self-attestation MVP)
- [ ] Screenshot baseline allegato per regression future (opzionale)

## Effort

~3-5gg dipendente da Task B complete.

## Gated

ON Task B — entity seeding factory required.

## Out of scope

- Journey #4 (Invitation/Notification flow): deferred wave futuro post DEC-5 notification system ship
- Journey #5 (Session live toast switching): deferred wave futuro post asse A polymorphic wire FE complete
- CI policy change per skeleton tolerant esistenti (Task A separato)

## References

- Audit P4: `docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md`
- Spec consolidato MAJ-11: `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`
- QA checklist template: `docs/for-developers/qa/2026-06-05-route-state-manual-qa.md`
- Umbrella: #1895

---

## Spec-Panel Review Addendum (2026-06-05 sessione 39)

**Pipeline**: `/sc:spec-panel --mode critique --focus requirements,architecture,testing`
**Panel**: Wiegers (lead) · Adzic · Cockburn · Fowler · Nygard · Crispin
**Output**: 11 findings (4 CRIT + 4 MAJ + 3 MIN) + 7 DEC (4 lockate via AskUserQuestion + 3 assumed best-practice)
**Spec doc consolidato**: [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../blob/main-dev/docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md)

### Findings critique mode

#### 🔴 CRITICAL (4 — 2 nuovi confermati via codebase discovery)

| # | Finding | Expert |
|---|---|---|
| **CRIT-C-1** | Primary actor non identificato per ogni journey. "Login + naviga" ambiguo: user role? `onboardingCompleted`? `Library size`? Senza primary actor + initial state, AC non riproducibili. | Cockburn |
| **CRIT-C-2** | AC #3 "full data-driven (no tolerant fallback)" ambiguo. Cosa NON è tolerant? Drawer stack literal `=== 2`? URL `URL.toString()` compare? Focus `expect(focusedElement).toBe(...)`? | Wiegers |
| **CRIT-C-3a** | **SPEC ERROR codebase-confirmed: Journey #2 "wizard 3-step" è ERRATO**. Actual `apps/web/src/app/(authenticated)/game-nights/new/_content.tsx:41`: `const _TOTAL_STEPS = 4 as const`. Quattro step shipped. | Cockburn + Wiegers |
| **CRIT-C-3b** | **SPEC ERROR codebase-confirmed: Journey #3 "paginazione inline NO navigate" CONFLITTO con `GameDetailSessionsRail` shipped**. Rail ha link "Storico partite" che NAVIGA a `/games/[id]/sessions`. Spec richiede pattern NUOVO inline = scope creep non dichiarato. | Fowler + Cockburn |
| **CRIT-C-4** | Resilience policy mancante: ① seedBE transient fail → retry/skip? ② wizard step race conditions? ③ drawer cascade depth race? ④ prefers-reduced-motion variant mandatory? | Nygard |

#### 🟡 MAJOR (4)

| # | Finding | Expert |
|---|---|---|
| **MAJ-C-1** | GWT step descrittivi ma non eseguibili. "ESC → Player drawer chiude" manca: Given drawer stack=2 / When ESC / Then stack=1 AND focus on GN trap AND Player DOM unmounted. | Adzic |
| **MAJ-C-2** | Edge cases mancanti: #1 backdrop vs ESC semantica diversa? #2 wizard validation per-step? #3 boundary total=10 (no button)? total=11? | Crispin |
| **MAJ-C-3** | AC #4 "CI policy 3/3 blocking" ambiguo. "Blocking" = main-dev required? main-staging? Decisione "separata" inside questa issue? | Wiegers |
| **MAJ-C-4** | Dipendenza testRunId (Task B DEC-B-5) implicit. AC #2 non esplicita formato + propagation pattern. | Adzic + Fowler |

#### 🟢 MINOR (3)

| # | Finding | Expert |
|---|---|---|
| **MIN-C-1** | Designer review checklist nel PR body vago. Per-journey concrete checklist mancano. | Adzic |
| **MIN-C-2** | Screenshot baseline opzionale = scope creep risk. AC esplicita "Visual regression OUT OF SCOPE". | Crispin |
| **MIN-C-3** | Sequencing tra 3 journey: 1 PR vs 3 PR. Trade-off velocity vs learning iteration. | Cockburn |

### Decisioni lockate (DEC-C-1..7)

#### DEC-C-1 · Primary actor: **Persona fissa Anna host + initial state esplicito per journey**

Tutti i journey usano **persona Anna** (role=`User`, host of test GN, `onboardingCompleted=true`, email=`anna.host@meepleai.test`).

**Initial state varia per journey**:

| Journey | Anna's GN count | Anna's library | Anna's roster | Other entities |
|---|---|---|---|---|
| #1 | 1 GN Published | irrelevant | 2 player tagged | 0 session, 0 RSVP |
| #2 | 0 GN | 1 game library (per wizard step 3 suggestion) | 0 roster | 0 session |
| #3 | irrelevant (game-focused) | 1 game | irrelevant | 15 session completed for that game |

Anna è seeded via Task B factory + `testRunId` scoped. Login via `seedAuthSession(page, { role: 'user' })` + Anna's userId injected.

#### DEC-C-2 · Data-driven assertion taxonomy: **Hybrid (strict per state discreto + functional per state continuo)**

**Strict literal assertions** (state discreto):
- Drawer stack depth: `expect(stack).toHaveLength(2)`
- URL: `expect(page.url()).toBe(expectedURL)` (or `.toMatch(/regex/)` per dynamic IDs)
- DB cleanup verify: `expect(rowCount).toBe(0)`
- Element count: `expect(cards).toHaveCount(10)`

**Functional assertions** (state continuo):
- Focus management: `expect(page.locator(':focus')).toMatchSelector('[data-trap="drawer-gn"]')`
- Scroll position: `expect(scrollY).toBeGreaterThan(0)` (no literal pixel)
- Animation completion: `await page.waitForFunction(() => element.getAnimations().length === 0)`
- Toast visibility: `expect(toast).toBeVisible()` (no exact text match unless content critical)

**NO tolerant fallback patterns** (banditi):
- ❌ `Promise.race([sidebar, loginForm])`
- ❌ `if (page.url().includes('/login')) expect(...)` con else divergenti
- ❌ Optional chaining `page.locator(...)?.click()`

#### DEC-C-3 · SPEC CORRECTIONS: **Journey #2 wizard 4-step + Journey #3 rail+filter rescoped**

**Journey #2 correction**:
- Step originale "Wizard 3-step compila (Quando+Dove → Invita → Game suggested)" → **WRONG**
- Step corretto: **Wizard 4-step compila** (1: Quando+Dove → 2: Invita → 3: Game suggested → 4: Recap+Submit)
- Reference codebase: `_content.tsx:41` `_TOTAL_STEPS = 4`

**Journey #3 rescope**:
- Step originale "paginazione inline (NO navigate /sessions)" → **CONFLITTO con design shipped**
- Step corretto: **Verify GameDetailSessionsRail mostra N session preview (max 5) + link "Storico partite" + clic naviga a `/games/[id]/sessions` con filter persistence (sortBy=date, dir=desc)**
- Acceptance updated: NO refactor rail, verify navigation pattern + filter URL params persisted
- Reference codebase: `GameDetailSessionsRail.tsx` `viewAllHref` + tests expect `/games/g/sessions` navigation

#### DEC-C-4 · CI policy: **Non-blocking main-dev + blocking main-staging**

| Branch promotion | Policy |
|---|---|
| feature/* → main-dev | Non-blocking (smoke tolerated, velocity priority). 3 journey eseguiti ma fail OK per merge. |
| main-dev → main-staging | **Blocking required job**. 3 journey + Task A 5 skeleton TUTTI green = required pre-merge. |
| main-staging → main | Blocking + cross-browser (chromium+firefox+webkit). |

Audit P4 CI gate disposition section già allineata. Decisione policy promotion fatta inline a questa issue, NON separate.

#### DEC-C-5 · Sequencing: **3 PR sequenziali su shared baseline branch**

Branch tree:
\`\`\`
main-dev
  └── feature/issue-1929-cross-asse-journey (baseline, shared helpers)
        ├── PR #N journey-1: cross-asse-journey-1-dashboard-drawer-stack
        ├── PR #N+1 journey-2: cross-asse-journey-2-empty-cta-wizard-live (gated PR #N merge)
        └── PR #N+2 journey-3: cross-asse-journey-3-game-detail-tab-partite (gated PR #N+1 merge)
\`\`\`

**Shared helpers in baseline branch**:
- `apps/web/e2e/_helpers/anna-persona.ts` — DEC-C-1 persona fixture
- `apps/web/e2e/_helpers/dataAssertionUtils.ts` — DEC-C-2 strict/functional helpers
- `apps/web/e2e/_helpers/resilienceWrappers.ts` — DEC-C-6 retry/timeout helpers

**Vantaggi**:
- Learning iteration tra journey (insights PR #1 → applied PR #2/#3)
- Small PR review-friendly (1 journey = ~150-300 LOC vs mono PR 500-900 LOC)
- Baseline branch isolato (no churn main-dev se journey breaks)
- Shared helpers commit baseline before journey impl

#### DEC-C-6 · Resilience model: **Retry 1x con backoff 500ms, poi loud fail**

\`\`\`typescript
// apps/web/e2e/_helpers/resilienceWrappers.ts
export async function withRetry<T>(
  fn: () => Promise<T>,
  options: { reason: string; backoffMs?: number } = { reason: 'unknown' }
): Promise<T> {
  try {
    return await fn();
  } catch (firstError) {
    await new Promise(r => setTimeout(r, options.backoffMs ?? 500));
    try {
      return await fn();
    } catch (secondError) {
      throw new Error(
        `Test action failed twice (reason: ${options.reason}). ` +
        `First: ${firstError.message}. Second: ${secondError.message}.`
      );
    }
  }
}
\`\`\`

**Applicato a**:
- `seedGameNight/Session/Player` calls (transient network)
- Wizard step transitions (race condition mitigation)
- Drawer cascade push (level N+1 settle wait)

**NON applicato a**:
- Login flow (seedAuthSession sync via cookie addCookies)
- Pure DOM assertions (no retry, fail fast)

**`prefers-reduced-motion` variant**:
- **Mandatory** per Journey #1 (drawer cascade ha animation). Use `test.use({ ...devices['Desktop Chrome'], reducedMotion: 'reduce' })` o `page.emulateMedia({ reducedMotion: 'reduce' })`.
- Opt-in per Journey #2/#3 (animation peripheral).

#### DEC-C-7 · Edge case coverage: **Matrix inline per journey + smoke negative**

**Journey #1 edge cases**:
- ✅ Happy path: dashboard → drawer GN → drawer Player → ESC ESC backdrop closeAll
- ⚠️ Backdrop click vs ESC semantica: **Same behavior** (closeOne livello corrente) — assert verifying esplicito
- ⚠️ Double-click rapid level 2 push: race condition — `withRetry` mitigation
- ⚠️ ESC su drawer stack vuoto: no-op (no error toast)

**Journey #2 edge cases**:
- ✅ Happy path: empty dashboard → CTA → wizard 4-step → submit → redirect → live opt-in
- ⚠️ Step 1 validation: timestamp future required (`scheduledAt > now`)
- ⚠️ Step 2 validation: minimum 1 player invited
- ⚠️ Step 3 validation: game selectable (suggested OR library OR search)
- ⚠️ Step 4 recap immutability: no edit, only "Modifica" back navigation
- ⚠️ Wizard cancel mid-flow: confirm modal + state preservation in URL `?step=N`

**Journey #3 edge cases**:
- ✅ Happy path: 15 session → rail mostra preview 5 + link Storico partite → naviga `/games/[id]/sessions`
- ⚠️ Boundary: 0 session → rail hidden (no link "Storico partite")
- ⚠️ Boundary: 1-5 session → rail mostra all, NO link "Storico partite" (no overflow)
- ⚠️ Boundary: 6+ session → rail mostra primi 5 + link presente
- ⚠️ Filter persistence: navigate `/sessions?sortBy=date&dir=desc` (params preservati)

### GWT canonical eseguibili (Adzic MAJ-C-1)

**Journey #1 GWT**:
\`\`\`
Scenario: ESC chiude drawer top of stack
Given Anna is logged in
  AND testRunId="e2e-j1-{timestamp}" seeded 1 GN Published "Sera Catan" + 2 player tagged
  AND Anna is on /dashboard
  AND GN card "Sera Catan" is visible in Prossimi section
When Anna clicks the GN card
  AND drawer GN opens (stack depth == 1, focus trap on drawer GN)
  AND Anna clicks Player avatar inside drawer
  AND Player drawer pushes (stack depth == 2, focus trap on drawer Player)
  AND Anna presses ESC once
Then drawer stack depth == 1
  AND Player drawer DOM is unmounted
  AND focus is restored to drawer GN trap
  AND no error toast appears
\`\`\`

**Journey #2 GWT**:
\`\`\`
Scenario: Empty dashboard → wizard 4-step → live mode opt-in
Given Anna is logged in
  AND testRunId="e2e-j2-{timestamp}" seeded user (0 GN) + 1 library game "Catan"
  AND Anna is on /dashboard
When Anna sees Prossimi EmptySection
Then CTA "Crea prima GameNight" is visible and clickable

When Anna clicks CTA
Then URL navigates to /game-nights/new
  AND wizard step 1 (Quando+Dove) is active (header "Step 1 di 4")

When Anna fills step 1 (date=tomorrow, location="Casa Anna") and clicks Avanti
Then wizard step 2 (Invita) is active

When Anna invites 1 regular player and clicks Avanti
Then wizard step 3 (Game suggested) is active
  AND "Catan" is suggested from Library

When Anna selects "Catan" and clicks Avanti
Then wizard step 4 (Recap) is active

When Anna clicks Submit
Then GN is created via POST /api/v1/game-nights
  AND URL redirects to /game-nights/{newGameNightId}
  AND "Apri live mode" CTA is visible

When Anna clicks "Apri live mode" CTA
Then opt-in toast appears with confirm button
  AND Anna clicks confirm

Then URL navigates to /game-nights/{newGameNightId}/live
  AND session is created via POST /api/v1/sessions
  AND session status is "InProgress"
\`\`\`

**Journey #3 GWT** (rescoped DEC-C-3):
\`\`\`
Scenario: Game Detail rail Storico partite navigation
Given Anna is logged in
  AND testRunId="e2e-j3-{timestamp}" seeded 1 game "Catan" + 15 session completed (sortBy=date desc)
  AND Anna is on /games/{catanGameId} tab Partite
When Anna sees GameDetailSessionsRail
Then rail shows max 5 session preview cards (most recent)
  AND link "Storico partite" is visible (Anna has 15 > 5 sessions)
  AND focus order: card[0] → card[1] → ... → card[4] → link

When Anna clicks "Storico partite" link
Then URL navigates to /games/{catanGameId}/sessions?sortBy=date&dir=desc
  AND filter params persist (DEC-C-3)
  AND focus management: focus on h1 page header after navigation
\`\`\`

### AC riformulati (post-spec-panel)

- [ ] **AC-1**: 3 spec file in `apps/web/e2e/cross-asse-journey-*.spec.ts` (#1+#2+#3)
- [ ] **AC-2**: 3/3 spec import `seedEntities` (Task B factory) + `seedAuthSession` + `annaPersona` fixture + `withRetry` resilience wrapper
- [ ] **AC-3**: 3/3 spec passano in CI con assertion taxonomy DEC-C-2 (strict per discreto + functional per continuo), no tolerant fallback
- [ ] **AC-4**: CI policy: non-blocking main-dev + blocking main-staging (DEC-C-4)
- [ ] **AC-5**: Designer review checklist per journey nel PR body (per-journey concrete vs vago DEC-3 self-attestation)
- [ ] **AC-6**: Edge case matrix verificata (DEC-C-7) per ogni journey
- [ ] **AC-7**: Shared baseline branch `feature/issue-1929-cross-asse-journey` con helpers in `apps/web/e2e/_helpers/` (annaPersona + dataAssertionUtils + resilienceWrappers)
- [ ] **AC-8**: 3 PR sequenziali (DEC-C-5) journey #1 → #2 → #3, ogni PR <300 LOC review-friendly
- [ ] **AC-9**: Journey #2 wizard 4-step verified (DEC-C-3 correction)
- [ ] **AC-10**: Journey #3 rail+navigate verified (DEC-C-3 rescope, NO refactor)

### Effort revised

- **Originale**: 3-5gg
- **Revised post-critique**: **4-7gg** distribuito 3 PR sequenziali
  - PR baseline (shared helpers + persona Anna): ~0.5gg
  - PR Journey #1 (drawer cascade + ESC + prefers-reduced-motion): ~1-1.5gg
  - PR Journey #2 (wizard 4-step + live opt-in + redirect): ~1.5-2gg (più complex flow)
  - PR Journey #3 (rail + navigate + filter persistence): ~0.5-1gg (rescoped semplificato vs original)
  - Buffer code review + CI iteration: ~0.5-1gg

### Out of scope (confermato)

- ❌ Journey #4 (Invitation/Notification): wave futuro post DEC-5 notification ship
- ❌ Journey #5 (Session live toast switching): wave futuro post asse A polymorphic wire FE
- ❌ Visual regression baseline screenshot (MIN-C-2 deferred)
- ❌ Cross-browser CI run (chromium-only MVP, firefox/webkit wave futuro)
- ❌ Performance benchmarks per journey (~3-5s SLA non enforced MVP)
- ❌ `GameDetailSessionsRail` refactor (DEC-C-3 rescope mantiene rail+link)
- ❌ Wizard 4-step expansion to 5-step (out of scope, current design honored)

### Risk mitigation summary

| Risk | Mitigation | DEC ref |
|---|---|---|
| Spec rot post-shipment | Codebase discovery PRE-implementation (CRIT-C-3a/b validati grep) | DEC-C-3 |
| Drift dev impl primary actor | Anna persona fissa + fixture canonical | DEC-C-1 |
| Tolerant fallback creep | Assertion taxonomy esplicita + banditi pattern | DEC-C-2 |
| CI bloat feature merge | Non-blocking main-dev preserves velocity | DEC-C-4 |
| PR review burden | 3 PR sequenziali <300 LOC each | DEC-C-5 |
| Transient flake | withRetry 1x + 500ms backoff | DEC-C-6 |
| Edge case miss | Matrix inline per journey + boundary tests | DEC-C-7 |
| Task B blocked Task C unstart | Demo spec Journey #1 (DEC-B-6) handoff | Task B addendum |
