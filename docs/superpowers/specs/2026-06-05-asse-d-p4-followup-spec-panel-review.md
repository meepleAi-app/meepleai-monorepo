# Asse D — P4 Follow-up Spec-Panel Review (#1928 Task B + #1929 Task C)

**Data**: 2026-06-05 (sessione 39)
**Issue parents**: [#1928 Task B](https://github.com/meepleAi-app/meepleai-monorepo/issues/1928) + [#1929 Task C](https://github.com/meepleAi-app/meepleai-monorepo/issues/1929)
**Umbrella**: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895) Claude Design alignment
**Pipeline**: `/sc:spec-panel --mode critique --focus requirements,architecture,testing`
**Panel**: Karl Wiegers (lead) · Gojko Adzic · Alistair Cockburn · Martin Fowler · Michael Nygard · Lisa Crispin
**Pattern**: P181 spec-panel-on-fresh-umbrella (esteso a follow-up scope)

---

## Sommario esecutivo

Sessione 39 applica `/sc:spec-panel` mode critique sequenziale a #1928 (Task B BE entity seeding) + #1929 (Task C cross-asse journey), seguendo il modello P181 già validato su umbrella #1895 (sessione 32) + P4 audit (sessione 38).

**Output aggregato**:
- **#1928 Task B**: 10 findings (3 CRIT + 4 MAJ + 3 MIN) + 6 DEC lockate
- **#1929 Task C**: 11 findings (4 CRIT + 4 MAJ + 3 MIN) + 7 DEC (4 lockate via AskUserQuestion + 3 assumed best-practice)
- **2 SPEC ERROR codebase-confirmed**: wizard 3-step → 4-step + paginazione inline → rail+navigate

**Decisioni unificate strategiche**:
1. **Task B Opt A canonical** (admin endpoint MediatR + triple gate) ← coerente con `AdminCatalogSeedEndpoints.cs` pattern + CQRS rule
2. **Task C primary actor fissa Anna host** + 4 initial state variants per journey
3. **Sequencing dipendenza**: Task B golden test (Journey #1 dashboard drawer stack) = handoff a Task C journey #1
4. **CI policy promotion**: non-blocking main-dev + blocking main-staging (audit P4 allineato)

**Sequencing operativo proposto**:
\`\`\`
Sessione 40+ (BE foundation, ~4-6gg)
  Task B → 4 MediatR commands + admin endpoint + triple gate + TS factory + demo spec
            golden test handoff = Journey #1 dashboard drawer stack pre-flight pass
            
Sessione 41+ (FE cross-asse, ~4-7gg distribuito 3 PR sequenziali)
  Task C → PR baseline shared helpers
         → PR Journey #1 dashboard-drawer-stack
         → PR Journey #2 wizard 4-step + live opt-in
         → PR Journey #3 rail Storico partite navigate
\`\`\`

---

## Discovery findings (pre-critique)

### Fact 1 — `seedAuthSession.ts` FE production-ready (Wave B.1 #633)

Path: `apps/web/e2e/_helpers/seedAuthSession.ts` (172 LOC).

API:
- `seedAuthSession(page, { role?: 'user' | 'admin' })` → cookies meepleai_session + meepleai_user_role
- `mockAuthEndpoints(page, { role, userId, email, onboardingCompleted })` → mock `/api/v1/auth/me` + `/api/v1/auth/session/status`
- Companion: `seedCookieConsent.ts`

Contract: `PLAYWRIGHT_AUTH_BYPASS=true` env + `NODE_ENV !== 'production'` + cookies pre-seeded.

**Verdetto**: Task B può riusare admin role pattern via `seedAuthSession(page, { role: 'admin' })`.

### Fact 2 — `AdminCatalogSeedEndpoints.cs` Opt A reference concreto

Path: `apps/api/src/Api/Routing/Admin/AdminCatalogSeedEndpoints.cs` (298 LOC).

Pattern confermato:
- `RequireAdminSessionFilter` group-level (auth pre-flag)
- `ICatalogSeedFeatureFlag` runtime kill-switch → 503
- CQRS via `IMediator.Send()` (8 endpoint, 0 direct service injection)
- Conditional registration via flag check at startup

**Verdetto**: Task B Opt A = stesso pattern + triple gate (env+ASPNETCORE+AdminFilter). NON runtime feature flag, ENV gate startup-time fail-fast per evitare prod accidental enable.

### Fact 3 — `/game-nights/new` wizard è 4-step (NOT 3-step)

Path: `apps/web/src/app/(authenticated)/game-nights/new/_content.tsx:41`:
\`\`\`typescript
const _TOTAL_STEPS = 4 as const;
\`\`\`

Companion: `apps/web/src/lib/game-nights/wizard-fixture.ts`, `wizard-reducer.ts`, `wizard-types.ts`, `wizard-validators.ts`.

**Verdetto**: Spec #1929 Journey #2 "wizard 3-step compila" è ERRATA. Step shipped: 1=Quando+Dove / 2=Invita / 3=Game / 4=Recap+Submit. CRIT-C-3a confermato.

### Fact 4 — `GameDetailSessionsRail` shipped naviga a `/sessions` (NOT paginazione inline)

Path: `apps/web/src/components/features/game-detail/GameDetailSessionsRail.tsx`.

Contract:
- Prop `sessions: SessionPreview[]` (max preview count internal)
- Prop `viewAllHref: string` (e.g., `/games/g/sessions`)
- Tests: `expect(screen.queryByRole('link', { name: 'Storico partite' })).not.toBeInTheDocument()` quando 0 session, presente con N+ session

**Verdetto**: Spec #1929 Journey #3 "paginazione inline (NO navigate /sessions)" CONFLITTO con design shipped. CRIT-C-3b confermato. DEC-C-3 rescope mantiene rail+navigate.

### Fact 5 — Testcontainers BE-only (Opt B non riusabile da Playwright)

Path: `apps/api/tests/Api.Tests/Infrastructure/SharedTestcontainersFixture.cs` + `IntegrationWebApplicationFactory.cs`.

**Verdetto**: Testcontainers spin up postgres container BE-side per integration tests xUnit. Playwright browser context NON può accedere senza process IPC custom. Opt B scope creep significativo. Confirmed Opt A canonical.

---

## Issue #1928 Task B — Critique completo

### Findings (10 totali)

#### 🔴 CRITICAL (3)

**CRIT-B-1 (Fowler + Cockburn)** — Factory API ownership layer ambigua

L'interfaccia proposta:
\`\`\`typescript
export async function seedGameNight(opts: {...}): Promise<{ gameNightId: string }>;
\`\`\`

è TypeScript-side ma manipola entity backend .NET. Non specifica TRANSPORT: HTTP fetch? gRPC? Playwright `page.request`? direct DB? Senza ownership chiaro, drift impl certa.

**Risoluzione**: DEC-B-1 (Opt A admin endpoint MediatR) + DEC-B-2 (TS factory wrapper fa `page.request.post(...)` verso admin endpoint).

**CRIT-B-2 (Wiegers)** — AC #1 non misurabile

"Factory functions per 3 entity (GameNight + Session + Player) + cleanup" non specifica:
- Quanti optional params?
- Return shape esatto?
- `cleanupTestEntities(testRunId)` per-test (`afterEach`) o per-suite (`afterAll`)?
- Idempotenza?
- Cleanup parziale se fallisce mid-cascade?

**Risoluzione**: DEC-B-3 (per-test afterEach + testRunId scoped) + DEC-B-5 (testRunId forzato API).

**CRIT-B-3 (Nygard)** — Failure modes non specificati

5 modes critici:
1. Test crash mid-seed → orphan rows
2. Parallel test runs stesso testRunId → collision
3. DB state leak inter-spec
4. Cleanup fails silent vs loud
5. **Env-gate bypass se `E2E_SEEDING_ENABLED=true` deployato prod per errore**

**Risoluzione**: DEC-B-4 (triple gate startup fail-fast) + DEC-B-5 (testRunId enforcement) + DEC-B-3 (loud cleanup failure).

#### 🟡 MAJOR (4)

**MAJ-B-1 (Crispin)** — Demo spec target vague

"1 spec demo che seedea 1 GN + 2 player + 1 live session" non specifica entity combo + golden test.

**Risoluzione**: DEC-B-6 (Cross-asse Journey #1 dashboard-drawer-stack come pre-flight golden test handoff a Task C).

**MAJ-B-2 (Adzic)** — GWT mancanti

Senza Given/When/Then, due dev implementano cleanup semantics diversi.

**Risoluzione**: 5 GWT canonical aggiunti addendum #1928 (seed happy path, cleanup cascade, admin auth required, env-gate prod refusal, parallel safety).

**MAJ-B-3 (Fowler)** — Opt A + CQRS rule

CLAUDE.md: "endpoints use ONLY IMediator.Send() — ZERO direct service injection". Audit raccomanda Opt A senza specificare MediatR commands.

**Risoluzione**: DEC-B-1 → 4 MediatR commands (`SeedTestGameNightCommand`, `SeedTestSessionCommand`, `SeedTestPlayerCommand`, `CleanupTestEntitiesCommand`) + FluentValidation. Pattern `AdminCatalogSeedEndpoints.cs`.

**MAJ-B-4 (Wiegers + Nygard)** — Sicurezza Opt A insufficiente con env var alone

`PLAYWRIGHT_AUTH_BYPASS` pattern usa double check (env + NODE_ENV); `AdminCatalogSeedEndpoints` usa runtime flag. E2E seeding triple gate necessario.

**Risoluzione**: DEC-B-4 (triple gate: env var + ASPNETCORE startup fail-fast + AdminFilter runtime).

#### 🟢 MINOR (3)

**MIN-B-1 (Hightower)** — Observability mancante
→ Structured log `{ testRunId, entityType, entityId, callerSpec, durationMs }` per seed call.

**MIN-B-2 (Crispin)** — Docs scope incompleto
→ 5 sezioni: API ref + Opt A rationale + GWT canonical + CI gate ops runbook + env failure recovery.

**MIN-B-3 (Adzic)** — Task C unblock checklist non esplicita
→ Demo spec Journey #1 pre-flight = handoff requirement.

### Decisioni lockate (DEC-B-1..6)

| DEC | Decisione | Rationale |
|---|---|---|
| **DEC-B-1** | Opt A admin endpoint MediatR | CQRS rule + pattern AdminCatalogSeedEndpoints proven |
| **DEC-B-2** | TypeScript factory wrapper via `page.request` | No direct DB from Playwright (architectural boundary) |
| **DEC-B-3** | Per-test afterEach + testRunId scoped | Parallel safe + deterministic + loud failure |
| **DEC-B-4** | Triple gate (env + ASPNETCORE + AdminFilter) | Defense-in-depth, startup fail-fast prevents prod accidental enable |
| **DEC-B-5** | testRunId forzato via factory API | Parallel safety + cleanup determinismo + audit trail |
| **DEC-B-6** | Demo spec = Journey #1 cross-asse | Golden test handoff a Task C (no isolated standalone spec) |

### AC riformulati Task B

1. 4 MediatR commands + FluentValidation + xUnit tests
2. 4 admin endpoint registrati conditional, triple gate verificato (env=Prod → app refuse, env=Test+no env → 404, env=Test+env+no auth → 401, env=Test+env+non-admin → 403)
3. TypeScript factory `apps/web/e2e/_helpers/seedEntities.ts` + testRunId enforcement
4. Demo spec `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` PRE-FLIGHT pass (golden test handoff Task C)
5. Docs `docs/for-developers/testing/e2e-entity-seeding.md` 5 sezioni
6. CI workflow `E2E_SEEDING_ENABLED=true` solo per Playwright E2E job
7. Structured logging per seed call

### Effort revised Task B

- **Originale**: 3-5gg
- **Revised**: 4-6gg

---

## Issue #1929 Task C — Critique completo

### Findings (11 totali)

#### 🔴 CRITICAL (4 — 2 nuovi codebase-confirmed)

**CRIT-C-1 (Cockburn)** — Primary actor non identificato
→ DEC-C-1: Anna persona fissa + 4 initial state per journey.

**CRIT-C-2 (Wiegers)** — "Full data-driven" ambiguo
→ DEC-C-2: hybrid taxonomy strict (state discreto) + functional (state continuo) + banditi pattern tolerant.

**CRIT-C-3a (Cockburn + Wiegers)** — SPEC ERROR Journey #2 wizard step count
- Spec: "3-step" → ACTUAL: 4-step (`_TOTAL_STEPS = 4`)
→ DEC-C-3: correct a "4-step compila (Quando+Dove / Invita / Game / Recap)".

**CRIT-C-3b (Fowler + Cockburn)** — SPEC ERROR Journey #3 paginazione vs rail
- Spec: "paginazione inline NO navigate" → ACTUAL `GameDetailSessionsRail` ha `viewAllHref` → naviga a `/games/[id]/sessions`
→ DEC-C-3: rescope a "rail + link Storico partite + clic naviga + filter persistence".

**CRIT-C-4 (Nygard)** — Resilience mancante
→ DEC-C-6: retry 1x + 500ms backoff + loud fail + prefers-reduced-motion variant mandatory Journey #1.

#### 🟡 MAJOR (4)

**MAJ-C-1 (Adzic)** — GWT non eseguibili
→ GWT canonical eseguibili aggiunti per 3 journey con Given/When/Then completo.

**MAJ-C-2 (Crispin)** — Edge cases mancanti
→ DEC-C-7: matrix edge case inline per journey (boundary + race + validation per step).

**MAJ-C-3 (Wiegers)** — CI policy ambigua
→ DEC-C-4: non-blocking main-dev + blocking main-staging required + cross-browser main.

**MAJ-C-4 (Adzic + Fowler)** — testRunId dipendenza implicit
→ AC #2 esplicita: `testRunId` format propagated da Task B DEC-B-5.

#### 🟢 MINOR (3)

**MIN-C-1 (Adzic)** — Designer checklist vago
→ Per-journey concrete checklist nel PR body (vs DEC-3 self-attestation generica).

**MIN-C-2 (Crispin)** — Screenshot baseline scope creep risk
→ AC esplicita "Visual regression OUT OF SCOPE".

**MIN-C-3 (Cockburn)** — Sequencing PR
→ DEC-C-5: 3 PR sequenziali su shared baseline branch.

### Decisioni lockate (DEC-C-1..7)

| DEC | Decisione | Rationale |
|---|---|---|
| **DEC-C-1** | Persona fissa Anna host + 4 initial state variant | Tracciabile, riproducibile, narrative-coherent |
| **DEC-C-2** | Hybrid assertion taxonomy (strict + functional) | Bilancia precisione e robustezza, banditi pattern tolerant |
| **DEC-C-3** | Spec corrections: wizard 4-step + rail rescope | Codebase reality + no scope creep refactor |
| **DEC-C-4** | Non-blocking main-dev + blocking main-staging | Velocity feature dev + safety promotion |
| **DEC-C-5** | 3 PR sequenziali su shared baseline branch | Learning iteration + small PR review-friendly + isolation |
| **DEC-C-6** | Retry 1x + 500ms backoff + loud fail | Transient flake mitigation senza mascherare bugs |
| **DEC-C-7** | Edge case matrix inline per journey | Coverage esplicita + boundary deterministico |

### AC riformulati Task C

1. 3 spec file `cross-asse-journey-{1,2,3}-*.spec.ts`
2. 3/3 spec import `seedEntities` + `seedAuthSession` + `annaPersona` + `withRetry`
3. 3/3 spec passano con assertion taxonomy DEC-C-2
4. CI policy non-blocking main-dev + blocking main-staging
5. Designer checklist per-journey concrete nel PR body
6. Edge case matrix verificata per journey
7. Shared baseline branch `feature/issue-1929-cross-asse-journey` con helpers
8. 3 PR sequenziali <300 LOC each
9. Journey #2 wizard 4-step verified (DEC-C-3 correction)
10. Journey #3 rail+navigate verified (DEC-C-3 rescope, NO refactor)

### Effort revised Task C

- **Originale**: 3-5gg
- **Revised**: 4-7gg distribuito 3 PR sequenziali

---

## Sequencing aggregato Task B + Task C

\`\`\`
Sessione N (Task B BE foundation, ~4-6gg)
├─ T1: 4 MediatR commands (Seed* + Cleanup) + FluentValidation
├─ T2: Admin endpoint group conditional registration + triple gate
├─ T3: Startup integration test (env=Prod refuse) + auth scenarios
├─ T4: TypeScript factory `seedEntities.ts` + testRunId enforcement
├─ T5: Demo spec Journey #1 pre-flight (golden test handoff)
├─ T6: Docs `e2e-entity-seeding.md` 5 sezioni
└─ T7: CI workflow E2E_SEEDING_ENABLED + structured logging

Handoff: Task B Demo spec Journey #1 PASS → Task C unblocked

Sessione N+1 (Task C FE cross-asse, ~4-7gg, 3 PR sequenziali)
├─ PR baseline: shared helpers (annaPersona + dataAssertionUtils + resilienceWrappers)
├─ PR Journey #1: dashboard-drawer-stack + ESC cascade + prefers-reduced-motion
├─ PR Journey #2: empty CTA → wizard 4-step → live opt-in (gated PR #1 merge)
└─ PR Journey #3: rail navigate /sessions + filter persistence (gated PR #2 merge)
\`\`\`

**Effort totale aggregato**: 8-13gg distribuito 2 sessioni.

---

## Tensioni produttive risolte (panel debate)

### Tensione 1: Loud vs Silent failure (Nygard ⇄ Wiegers)

- **Nygard**: triple gate startup fail-fast (app refuses to boot in prod)
- **Wiegers**: graceful degradation (warning + 503 runtime)

**Resolution**: hybrid based on environment
- env=Production → loud throw `InvalidOperationException` (DEC-B-4)
- env=Staging/Testing → conditional registration (no endpoint) + 404 silent
- env=Development → conditional registration + 503 runtime se admin auth ma flag off

### Tensione 2: Strict vs Functional assertions (Wiegers ⇄ Crispin)

- **Wiegers**: strict literal (drawer stack `=== 2`, URL exact match)
- **Crispin**: functional (focus visible, drawer dismissed)

**Resolution**: hybrid taxonomy
- State discreto enumerable → strict literal
- State continuo derivable → functional
- Banditi tolerant fallback patterns (`Promise.race`, optional chaining)

### Tensione 3: PR mono vs sequenziali (Cockburn ⇄ Crispin)

- **Cockburn**: 1 PR mono (~3-5gg) for narrative coherence
- **Crispin**: 3 PR sequenziali (~4-7gg) for review-friendly

**Resolution**: 3 PR sequenziali su shared baseline branch
- Shared helpers committed baseline before journey impl
- Learning iteration tra journey (insights PR #1 → applied PR #2/#3)
- Small PR <300 LOC review-friendly

---

## Cross-framework convergent insights

✅ **Task B + Task C entrambi convergono su testRunId**:
- DEC-B-5: testRunId forzato via factory API per parallel safety
- DEC-C-1: Anna persona seeded with testRunId scoped per journey
- DEC-C-3 GWT: testRunId formato `e2e-{testId}-{timestamp}` propagated

✅ **Admin endpoint pattern unico**: Task B Opt A + Task C admin role login both use `RequireAdminSessionFilter` chain.

✅ **Demo spec Journey #1 è pivot**: Task B AC-4 e Task C PR Journey #1 wirano la stessa spec — Task B impl skeleton + Task C completa data-driven assertions.

✅ **Banditi pattern tolerant fallback**: DEC-C-2 esplicita + audit P4 dichiara skeleton tolerant smoke-only NON acceptable per Task C journey.

⚠️ **Blind spots residui**:
1. Multi-tenant testRunId isolation NOT addressed (single-tenant MVP)
2. Performance SLA per journey NOT enforced MVP
3. Cross-browser CI (firefox/webkit) deferred wave futuro
4. Quartz orphan cleanup background job deferred (test crash frequenza TBD)

---

## Strategic questions (Socratic — per future sessioni)

1. **Quando testRunId scope multi-tenant diventa rilevante?** Quando feature multi-tenant ship (Issue #N TBD).
2. **Cross-browser CI sufficient firefox o richiede mobile context?** Decisione separate post mobile feature wave.
3. **Performance SLA quale threshold?** Baseline current: ~3-5s per journey acceptable.
4. **Orphan cleanup background job: Quartz daily o on-demand?** Decisione separate post 30+ giorni metriche orphan frequency.

---

## Spec governance (MIN-P4-3 inherited)

Ogni nuovo cross-asse journey discovered durante implementazione Task C → PR addendum a [spec consolidato MAJ-11](./2026-06-04-claude-design-alignment-spec-panel-review.md):
- Sezione "Nuova invariante journey #N proposta"
- Approver: dev autore PR + 1 reviewer asse interessato
- Update changelog inline

Pattern coerente con Sezione 8 spec consolidato MAJ-11.

---

## Pattern usage (sessione 39)

**P181 spec-panel-on-fresh-umbrella** (esteso a follow-up scope):
- Applicato a #1928 + #1929 (follow-up issue, NON fresh umbrella)
- 5 esperti + critique mode + AskUserQuestion DEC lock + addendum body
- Costo: ~1.5h totale per 2 issue (vs ~1.5h per 1 umbrella, scala lineare)
- Beneficio: 2 SPEC ERROR codebase-confirmed PRIMA di implementation (wizard step count + rail navigate)

**P124 pre-decomposition full search**: applicato pre-critique per validare absent issue duplicate.

**P67 verify-baseline-then-rerun**: NOT needed (no CI involved questa sessione).

**P74 audit-only chiusura**: NOT applicable (issue ancora OPEN, no closure).

**Memory pattern**: questa sessione documenta nel memory file `epic-1895-asse-d-p4-shipped.md` come "sessione 39 spec-panel critique B+C".

---

## References

- Audit P4: [`2026-06-05-asse-d-p4-cross-cutting-audit.md`](../../for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md)
- Spec consolidato MAJ-11: [`2026-06-04-claude-design-alignment-spec-panel-review.md`](./2026-06-04-claude-design-alignment-spec-panel-review.md)
- QA checklist template: [`2026-06-05-route-state-manual-qa.md`](../../for-developers/qa/2026-06-05-route-state-manual-qa.md)
- Fixtures auth (Wave B.1 #633): [`seedAuthSession.ts`](../../../apps/web/e2e/_helpers/seedAuthSession.ts)
- Admin endpoint reference: [`AdminCatalogSeedEndpoints.cs`](../../../apps/api/src/Api/Routing/Admin/AdminCatalogSeedEndpoints.cs)
- Wizard reference: [`game-nights/new/_content.tsx`](../../../apps/web/src/app/(authenticated)/game-nights/new/_content.tsx)
- Rail reference: [`GameDetailSessionsRail.tsx`](../../../apps/web/src/components/features/game-detail/GameDetailSessionsRail.tsx)
- Umbrella: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895)

---

## Changelog

- **2026-06-05 sessione 39**: initial spec-panel critique sequenziale #1928 + #1929. Output: 21 findings + 13 DEC lockate + 2 SPEC ERROR codebase-confirmed + sequencing aggregato 8-13gg distribuito 2 sessioni. Pattern P181 esteso a follow-up scope.
