## Contesto

Follow-up da [#1899 P4 cross-cutting audit](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899) MVP cut (sessione 38, 2026-06-05).

Discovery rivela: `seedAuthSession.ts` FE ESISTE production-ready, ma NON c'è infra per seedare entity (GameNight, Player, Session) backend-side. Il gap reale del "E2E auth seeding infra ~3gg" del P4 originale era questo, non le fixtures FE.

Riferimento audit: [`docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md`](https://github.com/meepleAi-app/meepleai-monorepo/blob/main-dev/docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md) DEC-P4-2 Task B.

## Scope

Implementare factory pattern entity seeding test-side per E2E data-driven:

### Factory API proposed

\`\`\`typescript
// apps/web/e2e/_helpers/seedEntities.ts (new file)

export async function seedGameNight(opts: {
  status: 'Draft' | 'Published' | 'InProgress' | 'Completed';
  scoringType?: 'Points' | 'BinaryWin' | 'Objectives' | 'Ranking';
  playerCount?: number;
  ownerId?: string;
}): Promise<{ gameNightId: string }>;

export async function seedSession(opts: {
  gameNightId: string;
  isLive: boolean;
  scoreType?: ScoreType;
}): Promise<{ sessionId: string }>;

export async function seedPlayer(opts: {
  gameNightId: string;
  role: 'host' | 'player' | 'guest';
  userId?: string;
}): Promise<{ playerId: string }>;

export async function cleanupTestEntities(testRunId: string): Promise<void>;
\`\`\`

### Implementation options (da decidere)

**Opt A**: Admin API endpoint dedicato test-only (env-gated `E2E_SEEDING_ENABLED=true`)
- Pro: clean separation, no test code in prod
- Con: 5+ nuovi endpoint da implementare + secure

**Opt B**: Direct DB factory via Testcontainers reuse (existing Api.Tests pattern)
- Pro: no new endpoint, riusa pattern testato
- Con: richiede separate test DB connection da Playwright

**Opt C**: gRPC admin tools per seeding (riusa esistente admin tooling)
- Pro: leveraging existing
- Con: scope creep

**Decisione consigliata**: Opt A — admin endpoint env-gated. Coerente con `PLAYWRIGHT_AUTH_BYPASS` pattern esistente.

## Acceptance

- [ ] Factory functions per 3 entity (GameNight + Session + Player) + cleanup
- [ ] Test wrapper: 1 spec demo che seedea 1 GN + 2 player + 1 live session passa + cleanup post-test
- [ ] CI policy: env-gated `E2E_SEEDING_ENABLED=true` solo in CI test env
- [ ] Docs: `docs/for-developers/testing/e2e-entity-seeding.md` con API reference + opt-A rationale

## Effort

~3-5gg — BE foundation lavoro, indipendente.

## Gated

NO — può iniziare in parallelo a Task A. Blocca Task C.

## Out of scope

- Wire skeleton esistenti (Task A separato)
- New cross-asse user journey spec (Task C, gated questo)

## References

- Audit P4: `docs/for-developers/audits/2026-06-05-asse-d-p4-cross-cutting-audit.md`
- Spec consolidato MAJ-11: `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`
- Fixtures FE pattern: `apps/web/e2e/_helpers/seedAuthSession.ts`
- Umbrella: #1895

---

## Spec-Panel Review Addendum (2026-06-05 sessione 39)

**Pipeline**: `/sc:spec-panel --mode critique --focus requirements,architecture,testing`
**Panel**: Wiegers (lead) · Adzic · Cockburn · Fowler · Nygard · Crispin
**Output**: 10 findings (3 CRIT + 4 MAJ + 3 MIN) + 6 DEC lockate via AskUserQuestion
**Spec doc consolidato**: [`docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md`](../blob/main-dev/docs/superpowers/specs/2026-06-05-asse-d-p4-followup-spec-panel-review.md)

### Findings critique mode

#### 🔴 CRITICAL (3)

| # | Finding | Expert |
|---|---|---|
| **CRIT-B-1** | Factory API ownership layer ambigua. `seedGameNight(opts)` è TypeScript-side ma manipola entity backend .NET. Non specifica TRANSPORT. Senza ownership chiaro, impl drift certa. | Fowler + Cockburn |
| **CRIT-B-2** | AC #1 "Factory + cleanup" non misurabile. Quanti optional params? Return shape esatto? `cleanupTestEntities(testRunId)` è per-test (`afterEach`) o per-suite (`afterAll`)? Idempotenza? | Wiegers |
| **CRIT-B-3** | Failure modes critici non specificati: ① test crash mid-seed orphan rows ② parallel runs collision ③ DB state leak inter-spec ④ cleanup fail silent vs loud ⑤ env-gate prod bypass risk. Necessita STARTUP-time refusal. | Nygard |

#### 🟡 MAJOR (4)

| # | Finding | Expert |
|---|---|---|
| **MAJ-B-1** | AC #2 "1 spec demo" vague. Quale entity combo? Suggerisce wire al Journey #1 Task C come pre-flight golden test. | Crispin |
| **MAJ-B-2** | Mancano GWT canonical per factory contract → due dev implementeranno cleanup semantics diversi. | Adzic |
| **MAJ-B-3** | Opt A admin endpoint richiede MediatR commands (CQRS rule CLAUDE.md). Pattern `AdminCatalogSeedEndpoints.cs` reference concreto. | Fowler |
| **MAJ-B-4** | Sicurezza Opt A: solo env var insufficiente. Triple gate necessario (env + ASPNETCORE!=Prod + AdminFilter). | Wiegers + Nygard |

#### 🟢 MINOR (3)

| # | Finding | Expert |
|---|---|---|
| **MIN-B-1** | Observability mancante. Structured log `{ testRunId, entityType, entityId, callerSpec, durationMs }`. | Hightower |
| **MIN-B-2** | Docs scope incompleto. Aggiungere GWT, testRunId convention, CI ops runbook, env failure recovery. | Crispin |
| **MIN-B-3** | Task C unblock checklist non esplicita. Demo spec = golden test handoff. | Adzic |

### Decisioni lockate (DEC-B-1..6)

#### DEC-B-1 · Implementation: **Opt A admin endpoint MediatR**

CQRS-compliant via 4 commands + endpoint group:
- `SeedTestGameNightCommand` → `POST /api/v1/admin/test/seed/game-night`
- `SeedTestSessionCommand` → `POST /api/v1/admin/test/seed/session`
- `SeedTestPlayerCommand` → `POST /api/v1/admin/test/seed/player`
- `CleanupTestEntitiesCommand` → `POST /api/v1/admin/test/cleanup`

FluentValidation + MediatR dispatcher. Pattern reference: `AdminCatalogSeedEndpoints.cs` (RequireAdminSessionFilter + endpoint group + conditional registration).

#### DEC-B-2 · API contract layer: **TypeScript factory wrapper**

`apps/web/e2e/_helpers/seedEntities.ts` esporta funzioni TS che fanno `page.request.post(...)` verso admin endpoint, con admin session pre-seeded via `seedAuthSession(page, { role: 'admin' })`. **NO direct DB access da Playwright** — disallowed by architectural boundary.

#### DEC-B-3 · Cleanup semantics: **Per-test `afterEach` + `testRunId` scoped**

\`\`\`typescript
test.beforeEach(async ({ page }) => {
  const testRunId = `e2e-${test.info().testId}-${Date.now()}`;
  page.context().testRunId = testRunId;
  await seedAuthSession(page, { role: 'admin' });
});

test.afterEach(async ({ page }) => {
  await cleanupTestEntities({ testRunId: page.context().testRunId });
});
\`\`\`

Parallel safe (testRunId univoco per test). Deterministic (cleanup obbligatorio).

#### DEC-B-4 · Env-gate: **Triple gate** (startup fail-fast + endpoint registration + admin filter)

\`\`\`csharp
// Program.cs startup
if (builder.Environment.IsProduction()
    && builder.Configuration.GetValue<bool>("E2E_SEEDING_ENABLED"))
{
    throw new InvalidOperationException(
        "E2E_SEEDING_ENABLED=true is FORBIDDEN in Production environment. Refusing to start.");
}

// Endpoint group conditional registration
if (!builder.Environment.IsProduction()
    && builder.Configuration.GetValue<bool>("E2E_SEEDING_ENABLED"))
{
    app.MapGroup("/api/v1/admin/test/seed")
       .AddEndpointFilter<RequireAdminSessionFilter>()
       .MapAdminTestSeedEndpoints();
}
\`\`\`

Coerente con `PLAYWRIGHT_AUTH_BYPASS` + `AdminCatalogSeedEndpoints` pattern.

#### DEC-B-5 · Idempotenza & parallel safety: **`testRunId` forzato via factory API**

Tutte le factory functions richiedono `testRunId: string` (validation: non-empty, formato `e2e-{testId}-{timestamp}`). Endpoint BE valida `testRunId` formato + scope isolato (no cross-spec lookup). Parallel-safe per default.

#### DEC-B-6 · Demo spec target: **Cross-asse journey #1 dashboard-drawer-stack**

Pre-flight wire di Journey #1 (Task C). AC Task B include:
- Seed 1 GameNight Published + 2 player roster
- Login admin → naviga `/dashboard` → assert GN card present in Prossimi section
- Cleanup determinismo verificato (0 row remaining post-test)

Questo è il **golden test handoff** a Task C: se demo passa, Journey #1 può iniziare full implementation.

### GWT canonical (Adzic MAJ-B-2)

**GWT-1**: Seed GameNight Published happy path
\`\`\`
Given test setup with testRunId="e2e-abc123-456"
When seedGameNight({ testRunId, status: 'Published', playerCount: 2 }) is called
Then response contains { gameNightId: UUID, ownerId: UUID, playerIds: [UUID, UUID] }
  AND DB contains 1 GN + 1 owner Player + 2 roster Player + 0 Session + 0 RSVP
  AND all rows have testRunId column = "e2e-abc123-456"
\`\`\`

**GWT-2**: Cleanup cascade determinismo
\`\`\`
Given testRunId="e2e-abc123-456" seeded 1 GN Published + 2 player + 1 live Session
When cleanupTestEntities({ testRunId: "e2e-abc123-456" }) returns
Then 0 rows remain in game_nights/players/sessions/rsvps WHERE testRunId="e2e-abc123-456"
  AND OTHER testRunId rows are NOT touched (scope isolation)
\`\`\`

**GWT-3**: Admin auth required
\`\`\`
Given E2E_SEEDING_ENABLED=true && env=Development
When unauthenticated POST /api/v1/admin/test/seed/game-night
Then response = 401 Unauthorized (RequireAdminSessionFilter)

When authenticated non-admin user POST same endpoint
Then response = 403 Forbidden
\`\`\`

**GWT-4**: Env-gate prod refusal (startup fail-fast)
\`\`\`
Given env=Production && E2E_SEEDING_ENABLED=true
When app starts
Then app refuses to boot with InvalidOperationException
  AND error message includes "FORBIDDEN in Production environment"
  AND exit code != 0 (container marked unhealthy)
\`\`\`

**GWT-5**: Parallel safety
\`\`\`
Given 4 Playwright workers running in parallel
When each worker seeds with unique testRunId
Then no collision occurs
  AND each worker's cleanup affects only its testRunId scope
  AND total DB row count returns to baseline after all workers finish
\`\`\`

### AC riformulati (post-spec-panel)

- [ ] **AC-1**: 4 MediatR commands implementati + FluentValidation + xUnit unit tests (handler + validator)
- [ ] **AC-2**: 4 admin endpoint registrati conditional, triple gate verificato via integration test (env=Prod → app refuse, env=Test+no env var → 404 endpoint, env=Test+env+no auth → 401, env=Test+env+non-admin → 403)
- [ ] **AC-3**: TypeScript factory `apps/web/e2e/_helpers/seedEntities.ts` con 4 funzioni + testRunId enforcement client-side
- [ ] **AC-4**: Demo spec `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` PRE-FLIGHT pass (seed GN + login admin + dashboard assert + cleanup verify) — golden test handoff a Task C
- [ ] **AC-5**: Docs `docs/for-developers/testing/e2e-entity-seeding.md` con 5 sezioni: API ref + Opt A rationale + GWT canonical + CI gate ops runbook + env failure recovery
- [ ] **AC-6**: CI workflow (`dev-async.yml` o nuovo E2E job) set `E2E_SEEDING_ENABLED=true` solo per Playwright E2E job (env locked a Development/Testing)
- [ ] **AC-7**: Structured logging `{ testRunId, entityType, entityId, callerSpec, durationMs }` per ogni seed call

### Effort revised

- **Originale**: 3-5gg
- **Revised post-critique**: **4-6gg** (+1gg per triple gate + integration tests + GWT)
  - BE commands + handlers + validators + endpoint registration + filter chain: ~2gg
  - Triple gate startup + integration test prod refusal + cleanup contract: ~0.5gg
  - TS factory wrapper + testRunId enforcement: ~0.5gg
  - Demo spec pre-flight (Journey #1 skeleton seeded): ~0.5gg
  - Docs (5 sezioni completi): ~1gg
  - Structured logging + observability: ~0.5gg
  - Buffer code review + CI iteration: ~0.5-1gg

### Out of scope (confermato)

- ❌ Test entity beyond GameNight/Session/Player (no Tag/Notification/AchievementUnlock seeding)
- ❌ Visual regression baseline (deferred wave futuro)
- ❌ Performance benchmarks (~latency seed-cleanup)
- ❌ Quartz orphan cleanup background job (può essere follow-up se test crash diventa frequente)
- ❌ Multi-tenant testRunId isolation (single-tenant MVP)

### Risk mitigation summary

| Risk | Mitigation | DEC ref |
|---|---|---|
| Prod accidental enable | Triple gate startup fail-fast | DEC-B-4 |
| Test pollution inter-spec | testRunId per-test + afterEach cleanup | DEC-B-3, DEC-B-5 |
| Parallel worker collision | testRunId formato univoco enforced | DEC-B-5 |
| Cleanup silent failure | Loud throw + retry once + then test failure | DEC-B-3 |
| Impl drift FE↔BE contract | TypeScript factory wrapper unico entrypoint | DEC-B-2 |
| Task C blocked by Task B incomplete | Demo spec golden handoff requirement | DEC-B-6 |
