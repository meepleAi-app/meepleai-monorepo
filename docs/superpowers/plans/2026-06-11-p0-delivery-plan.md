# P0 Delivery Plan — US Validation Sweep 2026-06-11

**Status**: Draft for execution (UPDATED post Gate 0 — 1 P0 retired)
**Created**: 2026-06-11
**Updated**: 2026-06-11 (Gate 0 outcome)
**Origin**: `/sc:spec-panel` triage post US validation sweep (10/10 US, 30 issue, 4 P0 → **3 P0**)
**Owner suggestion**: Tech lead + product owner co-pilot

## 🎯 Gate 0 outcome (2026-06-11)

**#2184 closed as INVALID** post empirical verification with non-admin user (`test@meepleai.com`):
- Code review: `LibraryHub.tsx:485,546` role-gates `onImportBgg` via `isAdminOrAbove`; `LibraryHeroDesktop.tsx:231` conditional render → button NOT rendered for non-admins
- Empirical: DOM scan `/import.*bgg/i` returns 0 elements for User role
- Falso positive during US-10 validation (tested as admin@meepleai.app — admin BGG access is LEGIT per ADR-059)
- **#1975 fix (closed 2026-06-07) is valid and shipped**

### Impact on this plan

| Track | Before Gate 0 | After Gate 0 |
|---|---|---|
| Track B (Compliance) | Active, paired PR + Gate 0 + E2E | **RETIRED** — no live bug |
| #2185 (mockup) | P2 reclassification | Downgrade to P3 — mockup drift minor (mockup shows "admin view" implicit) |
| Total P0 | 4 | **3** (#2168, #2176, #2194 umbrella) |
| Timeline parallel | 7gg | **~5-6gg** |
| Owner Track B (frontend-architect + designer) | Allocated | Reassigned to support Track C (#2176 fix) |

**Lesson learned (added to mistakes log)**: validate role-based UI variations BEFORE opening P0 compliance issues. Fowler's spec-panel warning ("Don't conflate label drift with feature drift") was prescient.

---

## Executive summary

4 P0 issue identificati durante la validazione US (vedi `audits/us-verification-log.md`). Spec-panel critique ha refined severity e DoD per ciascuna + decomposed #2194 in 3 sub-issue. **Parallel execution riduce ETA da ~15gg sequenziali a ~7gg con 4 track concorrenti** + 1 cross-cutting observability stream.

### Priority ranking (locked post spec-panel)

| # | Issue | Refined sev | Effort | Order | Track |
|---|---|---|---|---|---|
| 1 | #2168 — Login open redirect | P0 CRITICAL | S (1-2gg) | 1st | A — Security |
| 2 | #2184 — `/library` BGG ToS LIVE | P0 CRITICAL+LEGAL | S-M (½-2gg) | 2nd | B — Compliance |
| 3 | #2176 — Dashboard data inconsistency | P1 surface / P0 substrate | M (3-5gg) | 3rd | C — Data integrity |
| 4 | #2203/2204/2205 — Game detail tab orphan | P0 BLOCKING decision | L (5-10gg) | 4th | D — Architecture |
| Cross | Observability hooks (Nygard) | P1 | S parallel | — | E — Cross-cutting |

---

## Dependencies & blockers

```
Track A (Security)         : #2168 ─┐
                                    ├─► shared helper url-safety.ts ──► #2182 (P3 sister)
Track B (Compliance)       : Gate 0 (15min prod check)
                              ├─► IF prod-affected: rollback + IR escalation
                              └─► IF dev-only: paired PR #2184 + #2185 (mockup)
Track C (Data integrity)   : #2176 instrument ──► root cause ──► fix + contract test
                                                                  │
                                                                  └─► sister echo: #2184 hero counter
Track D (Architecture)     : #2203 (decision meeting + ADR) ──┬──► #2204 (impl, M-L)
                                                              │
                              #2205 (i18n) ──parallel──────────┘
                                                              ▼
                                                       #2195 BE 404 + #2197 UX + #2198 mockup
                                                       (resolution depends on decision A/B/C)
Track E (Observability)    : Prometheus metrics for silent failures
                              ├─► #2168: meepleai_auth_redirect_rejected_total
                              ├─► #2184: meepleai_bgg_url_attempted_render_total (existing, SLO=0)
                              ├─► #2176: meepleai_dashboard_section_hidden_total
                              └─► #2204: meepleai_api_404_total{expected}
```

---

## Track-by-track plan

### Track A — Security (1-2gg)

**Owner**: security-engineer + frontend-architect
**Blocks**: nothing (start immediately)
**Unblocks**: #2182 (notifications defensive validation)

**Tasks**:
1. **A.1** — Extract `apps/web/src/lib/url-safety.ts` shared helper (½gg)
   - `isSafeRelativeLink(link: string): boolean`
   - Reject 8 attack vectors enumerated by Wiegers
   - Unit test parametrized su 8 vectors
2. **A.2** — Apply helper in #2168 (½gg)
   - `(auth)/login/_content.tsx:62`: replace inline `targetUrl = from` with `targetUrl = isSafeRelativeLink(from) ? from : '/library'`
   - Replicate in `/register`, `/reset-password`, OAuth callback
3. **A.3** — Audit log entry (Nygard) (¼gg)
   - Backend endpoint logs `{event: 'LoginRedirectRejected', reason, fromMasked}`
4. **A.4** — Defense in depth: `Referrer-Policy: strict-origin` middleware (¼gg)
5. **A.5** — E2E test `auth-redirect-safety.spec.ts` con 8 vectors (½gg)
6. **A.6** — Apply helper in #2182 (notifications) — closes sister (¼gg)

**DoD gate**: 8 attack vectors green in CI + audit log entry visible in Seq + 1 PR closes #2168 + #2182

### Track B — Compliance (½-2gg)

**Owner**: frontend-architect + designer (paired PR)
**Blocks**: Gate 0 (15min) BEFORE any code
**Unblocks**: #2185 (mockup reclassification)

**Tasks**:
1. **B.0** — Gate 0 prod/staging check (15min)
   - `gh search code "Importa BGG" --owner=meepleAi-app --branch=main`
   - `gh search code "Importa BGG" --owner=meepleAi-app --branch=main-staging`
   - If LIVE on prod: SECURITY-INCIDENT triggered → rollback IR + freeze track B until cleared
   - If dev-only: proceed B.1
2. **B.1** — Disambiguate label vs feature (Fowler) (½gg)
   - Read click handler → label-only or feature-active?
   - If label-only: B.2 (rename)
   - If feature-active: B.3 (full removal + telemetry)
3. **B.2** — Paired PR: dev + mockup (Newman) — label-only path (1gg)
   - `apps/web/src/app/(authenticated)/library/page.tsx`: rename CTA
   - `admin-mockups/design_files/sp4-library-desktop.{html,jsx}`: rename CTA
   - `sp4-library-desktop.fidelity.json`: reclassify if button structurally changed
4. **B.3** — Full removal path (1-2gg) — only if feature-active
   - Telemetry: confirm 0 historical user clicks (search Seq logs)
   - Same as B.2 + remove handler + reverse code path
5. **B.4** — ESLint extension (Wiegers) (¼gg)
   - Rule `local/no-bgg-host` deve catturare button TEXT `/import.*bgg/i` non solo URL
   - Aggiungere a `pnpm lint:bgg` gate
6. **B.5** — E2E + Prometheus SLO assertion (Adzic) (½gg)
   - `apps/web/e2e/library-bgg-compliance.spec.ts`:
     - No DOM `/import.*bgg/i` text
     - No network call a `*.geekdo.com|*.boardgamegeek.com`
   - Prometheus `meepleai_bgg_url_attempted_render_total` SLO=0

**DoD gate**: Gate 0 cleared + paired PR merged + ESLint extension green + E2E + Prometheus SLO=0

### Track C — Data integrity (3-5gg)

**Owner**: backend-architect + frontend
**Blocks**: nothing (start parallel with A)
**Unblocks**: #2184 hero counter consistency (cross-eco)

**Tasks**:
1. **C.1** — Instrumentation first (Fowler) (1gg)
   - React Query DevTools dump per `useGames` + `useLibraryStats`
   - EF Core query log enable: `Microsoft.EntityFrameworkCore.Database.Command` → Information
   - Compare SQL output (filter, joins, soft-delete predicates)
2. **C.2** — Hypothesis confirmation (Fowler) (1-2gg)
   - Test H1: `useGames` queries shared catalog vs `useLibraryStats` queries personal UserLibrary
   - Test H2: EF Core soft-delete filter disparità
   - Test H3: TanStack Query cache key collision
3. **C.3** — CQRS read-model audit (Newman) (½gg)
   - Cross-context: GameManagement vs UserLibrary 'totalGames' ownership
   - MediatR event flow validation
   - Document in CLAUDE.md § DDD bounded contexts
4. **C.4** — Fix + observability (1gg)
   - Apply fix per root cause confirmed
   - Add dev warning: `console.warn('[SuggestedSection] hidden because gamesQuery returned 0 items, but useLibraryStats reports {totalGames}.')`
   - Add Prometheus metric: `meepleai_dashboard_section_hidden_total{section, reason}`
5. **C.5** — Cross-endpoint contract test (Adzic) (½gg)
   - `Api.Tests.Integration.DashboardConsistencyTests`
   - Given user 3 games → both endpoints + SuggestedSection ≥1 card

**DoD gate**: Root cause documented + fix shipped + contract test green + Prometheus SLO defined

### Track D — Architecture (5-10gg)

**Owner**: product owner (D.1) + tech lead (D.2) + frontend-architect (D.3)
**Blocks**: D.1 BLOCKS D.2; D.3 PARALLEL
**Unblocks**: #2195 BE 404 + #2197 UX + #2198 mockup commission

#### D.1 — #2203 Product decision (1gg)
1. **D.1.1** — Pull analytics: orphan URL hits last 30gg
2. **D.1.2** — Sync decision meeting (1h):
   - Opzione A (route-driven 7 tab): Info/Regole/FAQ/Recensioni/Sessioni/Strategie/Chat → backend wire required, breaks muscle memory
   - Opzione B (UI-driven 7 tab, remove orphan): Info/Regole/FAQ/Partite/Statistiche/Agenti/Documenti → simpler, removes features
   - Opzione C (hybrid 5+dropdown): preserves all, complex
3. **D.1.3** — ADR commit `docs/for-claude/architecture/adr/adr-{N}-game-detail-tab-canonical.md` (2h)
4. **D.1.4** — Decision communicated to designer (for #2198 mockup commission alignment)

#### D.2 — #2204 Implementation (3-7gg, depends on D.1)
1. **D.2.1** — Tab nav config aligned (Opzione A/B/C-specific)
2. **D.2.2** — Routes orphan handled (kept w/ BE wire OR removed OR moved to dropdown)
3. **D.2.3** — Lint rule (Fowler): file-system route vs nav entry parity (½gg)
4. **D.2.4** — E2E test ogni tab navigation
5. **D.2.5** — Visual conformance to #2198 mockup (post-delivery)
6. **D.2.6** — Sister issue resolution: #2195 (close or update), #2197 (coordinated)

#### D.3 — #2205 i18n cleanup (½gg PARALLEL)
1. **D.3.1** — Grep audit: `grep -r 'pages.gameDetail' apps/web/messages/`
2. **D.3.2** — Add missing keys (it.json/en.json)
3. **D.3.3** — Unit test no-missing-key fallback

**DoD gate**: ADR committed + tab nav allineata + lint rule CI gate + sister issues coordinated + i18n keys green

### Track E — Cross-cutting observability (Nygard)

**Owner**: backend-architect (parallel with A/B/C/D)
**Blocks**: nothing (additive)

**Recurring theme spotted dal panel**: 4 P0 share **silent failure modes**. Leverage point = observability hooks at failure boundary.

**Tasks**:
1. **E.1** — Prometheus metrics catalog (½gg)
   - `meepleai_auth_redirect_rejected_total{reason}` (Track A)
   - `meepleai_bgg_url_attempted_render_total` (existing, SLO=0)
   - `meepleai_dashboard_section_hidden_total{section, reason}` (Track C)
   - `meepleai_api_404_total{endpoint, expected}` (Track D)
2. **E.2** — Grafana dashboard `Silent failure monitor` (½gg)
3. **E.3** — Alertmanager rules: SLO breach on any metric (½gg)
4. **E.4** — Documentation: CLAUDE.md § Observability conventions

**DoD gate**: All 4 metrics emitting in dev + Grafana panel + alert rules → silent failures NOT silent anymore

---

## Timeline (parallel execution)

```
Day 0 (today):
  ├─ Track B: Gate 0 prod check (15min) ⚠️
  ├─ Track A: A.1 helper extraction starts
  ├─ Track C: C.1 instrumentation starts
  ├─ Track D: D.1.1 analytics pull + D.1.2 meeting scheduled, D.3 starts parallel
  └─ Track E: E.1 metrics catalog drafted

Day 1:
  ├─ Track A: A.2-A.5 (helper applied + audit log + E2E)
  ├─ Track B: B.1 disambiguate + B.2/B.3 paired PR drafted
  ├─ Track C: C.2 hypothesis confirmation
  ├─ Track D: D.1.2 meeting → D.1.3 ADR drafted
  └─ Track E: E.2 Grafana panel

Day 2:
  ├─ Track A: A.6 (#2182) + PR merge ✅
  ├─ Track B: PR merge + ESLint + E2E ✅
  ├─ Track C: C.3 CQRS audit + C.4 fix start
  ├─ Track D: D.1.3 ADR commit → D.2.1 impl starts
  └─ Track E: E.3 alerts

Day 3-4:
  ├─ Track C: C.4 fix + C.5 contract test ✅
  ├─ Track D: D.2.2-D.2.4 implementation continues
  └─ Track E: E.4 docs + validation

Day 5-7:
  ├─ Track D: D.2.5 visual conformance (post #2198 mockup) + D.2.6 sister resolution ✅
  └─ Final integration + observability verification

Day 7: All 4 P0 closed + 13 sub-issue resolution path clear
```

**Critical path**: Track D (5-10gg post decision) — biggest tail risk.

**Compression vs sequential**: ~7gg parallel vs ~15gg sequential = **53% saving**.

---

## Risk register

| ID | Risk | Probability | Impact | Mitigation |
|---|---|---|---|---|
| R1 | Gate 0 reveals BGG button LIVE on prod | LOW | CRITICAL | IR playbook ready; rollback procedure documented; track B frozen until cleared |
| R2 | #2176 hypothesis H1/H2 wrong (deeper CQRS bug) | MED | MED | Budget +2gg for additional investigation; escalate to backend architect |
| R3 | Product decision #2203 delayed | MED | HIGH | Schedule meeting Day 0; tech lead empowered to lock decision if PM unavailable |
| R4 | #2168 8 attack vectors expose deeper auth weakness | LOW | HIGH | Adversarial testing parametrized; security-engineer review before merge |
| R5 | #2184 paired PR uncoordinated (mockup vs dev drift) | LOW | MED | Single PR enforces atomic delivery |
| R6 | Track A helper extraction breaks existing redirects | LOW | MED | Parametrized regression suite on all redirect entry points |
| R7 | Prometheus metric naming drift vs existing conventions | LOW | LOW | Validate against existing `meepleai_*` metrics naming convention |

---

## Cross-cutting recommendations (Meadows systems pattern)

> Spec-panel rilevò che 4 P0 condividono root cause sistemico: **silent failure modes**. Fixing one-by-one tratta sintomi. Track E è la **leverage intervention**.

1. **Observability-first culture**: ogni nuovo silent fallback DEVE emettere metrica + log.
2. **Architecture review per pattern simili**: identificare altri silent fallback in monorepo (`grep -rn 'return null' apps/web/src/`) — backlog item.
3. **CI gate: silent failure SLO**: nuove regressioni P0 dovrebbero triggerare SLO breach prima del bug report umano.
4. **Spec-panel come quality gate ricorrente**: dopo ogni US validation sweep, refine i P0 con spec-panel critique (pattern validato in questa sessione).

---

## Owner suggestions

| Track | Lead | Support |
|---|---|---|
| A — Security | security-engineer | frontend-architect |
| B — Compliance | frontend-architect | designer |
| C — Data integrity | backend-architect | frontend |
| D.1 — Product decision | product owner | tech lead |
| D.2 — Implementation | frontend-architect | backend-architect |
| D.3 — i18n | i18n-specialist | — |
| E — Observability | backend-architect | platform team |

---

## DoD finale (sign-off criteria)

P0 delivery batch considered complete when:

- [ ] 4 P0 issue marked closed (#2168 #2184 #2176 + #2204 closes #2194 umbrella)
- [ ] 6 sub/sister issue resolution path documented (#2182 #2185 #2195 #2197 #2198 #2205)
- [ ] 4 Prometheus metrics emitting + Grafana panel + alert rules
- [ ] CLAUDE.md updated:
  - § Observability conventions (Track E)
  - § Game detail tab canonical (post #2203 ADR)
  - § BGG enforcement rule extension (Track B)
- [ ] ESLint `local/no-bgg-host` extended con button text rule
- [ ] E2E suite: auth redirect safety + library BGG compliance + dashboard consistency + game detail tab nav
- [ ] All sister P3/P2 issues either closed (defensive fix shipped) or backlog-tagged

---

## Refs

- US verification log: `audits/us-verification-log.md`
- Spec-panel critique (this conversation, 2026-06-11)
- ADR-001 ↔ ADR-010 (DDD bounded contexts)
- CLAUDE.md § Active Freezes (BGG asset ban #2123 + ADR #1903)
- Memory: A11y baseline #1094 (a11y patterns informing #2169)

---

**Sign-off pending**: tech lead + product owner review.
