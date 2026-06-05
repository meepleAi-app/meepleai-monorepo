# Asse D — P4 cross-cutting audit (MVP cut)

**Data**: 2026-06-05
**Issue**: [#1899 P4 follow-up](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899)
**Parent umbrella**: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895)
**Branch**: `feature/issue-1899-asse-d-p4-cross-cutting`
**Scope**: P4 audit + scope cut MVP via `/sc:spec-panel` mode critique
**Esperti panel**: Crispin (lead) · Wiegers · Adzic · Gregory · Nygard

---

## Sommario esecutivo

Pre-flight discovery sul P4 "cross-cutting" rivela che:

1. **Scope (a) "E2E auth seeding infra (~3gg)" è MISCOMUNICATO** — `seedAuthSession.ts` ESISTE production-ready (Wave B.1, Issue #633). Il gap reale è **BE entity seeding** (GameNight + Player + Session via factory o API), che è 1+ settimana, NON 3gg.
2. **Scope (b) "QA manual checklist (~1gg)"** legittimo + `docs/for-developers/qa/` net-new dir.
3. **MAJ-11 5 user journey** (spec consolidato): 2/5 BLOCKED su feature non-shipped MVP (DEC-5 notification + asse A polymorphic wire FE), 3/5 PARTIAL coverage via skeleton tolerant esistenti.
4. **5 skeleton E2E già shipped** (asse-b/d/p1/p2/p3 + dashboard-priority-flow) ma NON wirano `seedAuthSession` — sono "tolerant redirect" smoke-only.

**Decisione MVP cut DEC-P4-1..DEC-P4-4 lockate** + 3 follow-up issue separate per residual scope reale.

---

## Discovery findings

### Fact 1 — `seedAuthSession.ts` ESISTE production-ready

**Path**: `apps/web/e2e/_helpers/seedAuthSession.ts` (157 LOC)
**Origine**: Wave B.1 lesson learned, Issue [#633](https://github.com/meepleAi-app/meepleai-monorepo/issues/633)
**API**:
- `seedAuthSession(page, { role: 'user' | 'admin' })` → cookies `meepleai_session` + `meepleai_user_role`
- `mockAuthEndpoints(page, { role, userId, email })` → mock `/api/v1/auth/me` + `/api/v1/auth/session/status`
- Companion: `seedCookieConsent.ts` per banner-dismissal localStorage

**Contract** (deve match `proxy.ts` middleware):
- `PLAYWRIGHT_AUTH_BYPASS=true` env var + `NODE_ENV !== 'production'` → short-circuit BE validation
- Cookies presenti su prima request (no redirect to `/login`)

**Verdetto**: scope (a) come "fixtures FE" è ALREADY DONE. La narrazione "~3gg E2E auth seeding infra" è stata write-time stale.

### Fact 2 — 5 skeleton E2E shipped ma NON wirano fixtures

**Skeleton inventory**:

| File | LOC | Pattern | Wire fixtures? |
|---|---|---|---|
| `asse-b-drawer-stack-flow.spec.ts` | 76 | Tolerant redirect (Promise.race sidebar OR loginForm) | ❌ No |
| `asse-d-p1-polymorphic-scoring.spec.ts` | 39 | Smoke skeleton | ❌ No |
| `asse-d-p2-games-discover-hub.spec.ts` | 123 | Smoke skeleton multi-tab | ❌ No |
| `asse-d-p3-onboarding-wizard.spec.ts` | 38 | Smoke skeleton wizard | ❌ No |
| `dashboard-priority-flow.spec.ts` | 67 | Tolerant redirect (if URL match /login → expect, else verify slot) | ❌ No |
| **Totale** | **343 LOC** | tolerant-only | **5/5 NOT wired** |

**Pattern dichiarato esplicitamente** in dashboard-priority-flow.spec.ts:7-9:
> "full data-driven assertions are deferred until E2E auth seeding lands (consistent with sibling Stage 3 E2E skeletons)"

**Verdetto**: pattern tolerant è acceptable per skeleton smoke, ma NON soddisfa MAJ-11 "5 user journey cross-asse data-driven".

### Fact 3 — `docs/for-developers/qa/` NON ESISTE

Verifica filesystem: solo `docs/for-developers/testing/` adjacent + `audits/` + `specs/` + altri.

**Verdetto**: net-new dir creation per (b) checklist. MVP cut legittimo.

### Fact 4 — 5 user journey MAJ-11 status

Riferimento: [Spec consolidato MAJ-11](../../superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md) sezione 4.

| # | User journey | Status MVP | Coverage attuale | Blocker |
|---|---|---|---|---|
| 1 | Dashboard → drawer GameNight → drawer Player swap → ESC back → backdrop close | **PARTIAL** | asse-b-drawer-stack-flow.spec.ts smoke | data-driven richiede entity seed BE |
| 2 | Dashboard empty → CTA "Crea prima GN" → wizard 3-step → Live mode opt-in | **PARTIAL** | dashboard-priority-flow.spec.ts smoke + onboarding wizard separato | wizard 3-step ≠ /onboarding (diverso wizard /game-nights/new asse D futuro) |
| 3 | Game Detail tab Partite → paginazione inline (NO navigate /sessions) | **NEW** | nessun spec dedicato `/games/[id]` | richiede `/games/[id]` Game Detail page + tab Partite spec |
| 4 | Invitation: Anna login → /notifications → click invito → /game-nights/[id] pending → RSVP confirm → dashboard | **BLOCKED** | nessun | DEC-5 notification system NOT shipped MVP |
| 5 | Session live → toast warning "salva draft con live attiva" → click toast link → switch a live session | **BLOCKED** | nessun | asse A polymorphic wire FE shipped solo P1 editor, no toast switching pattern |

**Verdetto**: 2/5 blocked, 3/5 require additional infra (BE entity seeding). 0/5 full data-driven implementabili in 1 sessione.

---

## Findings panel critique (8 totali)

### 🔴 CRITICAL (3)

| # | Finding | Expert | Disposition |
|---|---|---|---|
| CRIT-P4-1 | scope (a) "E2E auth seeding infra (~3gg)" MISCOMUNICATO — fixtures FE ESISTE già, gap reale = BE entity seeding ~1+ settimana | Crispin + Wiegers | **DEC-P4-2** scope (a) splittato in 3 task distinti |
| CRIT-P4-2 | MAJ-11 5 user journey: 2/5 BLOCKED, 3/5 PARTIAL — nessuno full data-driven implementabile MVP cut | Crispin + Adzic | **DEC-P4-3** journey #1+#2+#3 → follow-up issue separato; #4+#5 deferred su feature wave |
| CRIT-P4-3 | `docs/for-developers/qa/` net-new dir | Gregory | **DEC-P4-1** dir creata + template hybrid Crispin × Adzic |

### 🟡 MAJOR (4)

| # | Finding | Expert | Action |
|---|---|---|---|
| MAJ-P4-1 | 13 route × 5 stati = 65 cell QA matrix — rubber-stamp risk senza scoping | Crispin + Gregory | 4 route reference + template "Future route" copia-incolla |
| MAJ-P4-2 | Acceptance criteria (b) checklist non testabili senza Given/When/Then | Adzic + Wiegers | Hybrid format adottato: tabular 5-state + G/W/T per cell |
| MAJ-P4-3 | No CI policy decision per data-driven E2E — 4 skeleton chromium-only + tolerant, non blocking | Nygard | Sezione "CI gate disposition" nel template QA + audit doc |
| MAJ-P4-4 | #1899 status drift post P1+P2+P3 shipped sess.35-37 | Wiegers | DEC-P4-4 close formal #1899 + 3 follow-up issue |

### 🟢 MINOR (3)

| # | Finding | Expert | Fix |
|---|---|---|---|
| MIN-P4-1 | Template QA include screenshot allegato slot | Gregory | Path `qa-screenshots/YYYY-MM-DD/` + gitignore dir |
| MIN-P4-2 | "Designer approved-by/on" lasciato vuoto con istruzione self-attestation MVP | Wiegers | Self-attestation block con criteri non-rubber-stamp |
| MIN-P4-3 | Spec governance MIN-8 — ogni nuovo journey post-P4 = PR a spec consolidato | Adzic | Reference governance section nel audit doc |

---

## Decisioni operate (DEC-P4-1..DEC-P4-4)

### DEC-P4-1 · QA checklist template hybrid Crispin × Adzic, 4 route reference

**Decisione**: creare `docs/for-developers/qa/2026-06-05-route-state-manual-qa.md` con:
- Tabella matrice 5-stati × 4 route reference (sintetic checkmark)
- Per ogni route: Given/When/Then per ogni dei 5 stati
- 4 route reference: `/dashboard` + `/game-nights/[id]/live` + `/onboarding` + `/games`
- Template "Future route" copia-incolla
- Self-attestation block con criteri non-rubber-stamp (MVP no designer attivo)

**Motivazione**: MAJ-8 spec consolidato + DEC-3 self-attestation pattern asse A/B/C. Hybrid format soddisfa Crispin (tabular scaling) + Adzic (executable spec testabilità).

**Effort**: ~1-1.5h docs-only.

### DEC-P4-2 · Scope (a) E2E auth seeding splittato in 3 task distinti

**Decisione**: il "E2E auth seeding infra (~3gg)" del P4 originale è **3 task collassati**, splittati in 3 follow-up issue separate:

1. **Task A** — Wire `seedAuthSession` in 5 skeleton esistenti (asse-b/d/p1/p2/p3 + dashboard-priority-flow). Refactor tolerant → seeded. ~0.5gg.
2. **Task B** — BE entity seeding infra (GameNight + Player + Session via factory o admin API). Permette skeleton data-driven. ~3-5gg.
3. **Task C** — 3 new cross-asse user journey full data-driven (journey #1+#2+#3 spec consolidato MAJ-11). Gated su Task B complete. ~3-5gg.

**Motivazione**: scope originale ambiguo + write-time stale. Discovery rivela fixtures FE ESISTE già. Effort reale BE entity seeding è il vero costo, NON 3gg.

**Impatto**: 3 issue separate aperte post questa sessione (task #8). #1899 chiuso MVP cut.

### DEC-P4-3 · MAJ-11 5 user journey deferred wave separato

**Decisione**: 5 user journey MAJ-11 NON in scope P4 MVP cut:
- Journey #1+#2+#3 → Task C follow-up issue (gated Task B)
- Journey #4 (Invitation/Notification) → blocked DEC-5 notification system → wave futuro post asse A notification ship
- Journey #5 (Session live toast switching) → blocked asse A polymorphic wire FE complete → wave futuro

**Motivazione**: 0/5 full data-driven implementabili in 1 sessione P4. Honest assessment > stretched scope.

### DEC-P4-4 · #1899 close formal MVP cut

**Decisione**: chiudere #1899 post questa PR con dichiarazione esplicita:
> "P4 MVP cut completato: QA checklist template + audit cross-cutting + 3 follow-up issue separate per residual scope reale. P1 (sess.35) + P2 (sess.36) + P3 (sess.37) + P4 (questa PR) tutti shipped. Umbrella #1895 considerata 80%+ complete con A+B+C+D follow-up MVP shipped."

**Motivazione**: pattern P74 (audit-only chiusura formal) già applicato a Epic #1475 + #1585. Coerente con DEC-P4-2 splitting scope reale in follow-up issue tracking.

**Impatto**: status drift risolto. Future sessione P4 trova issue body marked completed + follow-up roadmap leggibile in audit doc.

---

## CI gate disposition (MAJ-P4-3)

**Skeleton attuali (5 spec)**:
- Trigger: `chromium-only` (skip firefox/webkit per speed)
- Pattern: tolerant redirect (Promise.race sidebar OR loginForm)
- CI policy: **NON blocking** (acceptable smoke level)
- Disposition: mantenere come è. Wave future Task A wire fixtures = stessa CI policy (non-blocking finché data-driven completa Task B).

**Data-driven E2E full journey (futuro wave Task C)**:
- Trigger: full chromium + cross-browser regression
- Pattern: assert data-driven (no tolerant fallback)
- CI policy: **blocking gate** richiesta una volta wired
- Disposition: decisione policy quando Task C inizia, NON ora.

**Manual QA template (questo doc)**:
- Trigger: PR body self-attestation block
- Pattern: human compilator + screenshot allegato + commit SHA traceable
- CI policy: **NON automated** (audit human responsibility)
- Disposition: enforce via PR template review (no CI check)

---

## Acceptance MVP P4

- [x] Pre-flight discovery: fixtures FE ESISTE production-ready (Wave B.1 #633)
- [x] Discovery: 5 skeleton E2E shipped, 0 wirano fixtures, tolerant smoke-only
- [x] Discovery: `docs/for-developers/qa/` net-new dir
- [x] Discovery: MAJ-11 5 user journey 2/5 BLOCKED + 3/5 PARTIAL, 0/5 full data-driven 1-sessione
- [x] DEC-P4-1..DEC-P4-4 lockate via `/sc:spec-panel` critique + AskUserQuestion
- [x] QA checklist template hybrid Crispin × Adzic 4 route reference
- [x] Audit doc P4 (questo file)
- [ ] 3 follow-up issue create (Task A + Task B + Task C)
- [ ] #1899 commento close + status update
- [ ] PR a main-dev (NON main) con body completo

---

## Follow-up roadmap

### Issue Task A — Wire `seedAuthSession` in 5 skeleton esistenti

- **Effort**: ~0.5gg (~4-6h)
- **Scope**: refactor 5 spec (asse-b-drawer-stack + asse-d-p1/p2/p3 + dashboard-priority-flow) per:
  - `import { seedAuthSession, mockAuthEndpoints } from './_helpers/seedAuthSession'`
  - `test.beforeEach(async ({ page }) => { await seedAuthSession(page); await mockAuthEndpoints(page); })`
  - Rimuovere pattern tolerant `Promise.race(sidebar OR loginForm)` → assert direct
- **Gated**: NO — può iniziare immediatamente
- **Output**: skeleton diventano semi-data-driven, CI policy invariata (non-blocking)
- **Acceptance**: 5/5 spec passano in CI con seeded auth, no tolerant fallback

### Issue Task B — BE entity seeding infra E2E

- **Effort**: ~3-5gg
- **Scope**: factory pattern per seedare entity test-side:
  - `seedGameNight({ status, scoringType, playerCount })` → ritorna `GameNightId`
  - `seedSession({ gameNightId, isLive, scoreType })` → ritorna `SessionId`
  - `seedPlayer({ gameNightId, role: 'host' | 'player' | 'guest' })` → ritorna `PlayerId`
  - Admin API endpoint o direct factory `Testcontainers` reuse?
- **Gated**: NO — backend foundation, indipendente
- **Output**: data-driven E2E unblocked
- **Acceptance**: test che seeda 1 GN + 2 player + 1 live session passa + cleanup post-test

### Issue Task C — Cross-asse user journey #1+#2+#3 full data-driven

- **Effort**: ~3-5gg
- **Scope**: 3 nuovi spec data-driven (per spec consolidato MAJ-11):
  - `cross-asse-journey-1-dashboard-drawer-stack.spec.ts` — Dashboard → drawer GN → Player swap → ESC
  - `cross-asse-journey-2-empty-cta-wizard-live.spec.ts` — Dashboard empty → wizard 3-step → Live opt-in
  - `cross-asse-journey-3-game-detail-tab-partite.spec.ts` — Game Detail tab Partite paginazione inline
- **Gated**: ON Task B (entity seeding required)
- **Output**: 3/5 MAJ-11 journey data-driven; CI gate diventa blocking per queste 3
- **Acceptance**: 3 spec passano in CI + screenshot regression baseline (opzionale)

### Journey #4 + #5 deferred wave futuro

- **Journey #4** (Invitation/Notification flow): gated DEC-5 notification system ship (asse A wave)
- **Journey #5** (Session live toast switching): gated asse A polymorphic wire FE completo
- **Disposition**: NON aprire issue ora, tracking solo in spec consolidato MAJ-11. Issue separato quando feature ship.

---

## Spec governance reference (MIN-P4-3)

Ogni nuovo cross-asse journey discovered durante implementazione Task C → PR a spec consolidato `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`:
- Sezione "Nuova invariante journey #N proposta"
- Approver: dev autore PR + 1 reviewer asse interessato
- Update changelog inline

Pattern coerente con Sezione 8 spec consolidato.

---

## References

- Spec consolidato MAJ-11: [`2026-06-04-claude-design-alignment-spec-panel-review.md`](../../superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md)
- Audit D.0 (storico audit MVP cut originale): [`2026-06-05-asse-d-v2-shipped-audit.md`](./2026-06-05-asse-d-v2-shipped-audit.md)
- QA checklist template: [`2026-06-05-route-state-manual-qa.md`](../qa/2026-06-05-route-state-manual-qa.md)
- Fixtures auth (Wave B.1 #633): [`seedAuthSession.ts`](../../../apps/web/e2e/_helpers/seedAuthSession.ts)
- v2 migration matrix: [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)
- Umbrella #1895 sub-issue trail:
  - [#1896 A](https://github.com/meepleAi-app/meepleai-monorepo/issues/1896) shipped sess.32
  - [#1897 B](https://github.com/meepleAi-app/meepleai-monorepo/issues/1897) shipped sess.33
  - [#1898 C](https://github.com/meepleAi-app/meepleai-monorepo/issues/1898) shipped sess.34
  - [#1899 D](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899) MVP cut audit sess.31+P1 sess.35+P2 sess.36+P3 sess.37+P4 sess.38 (questa)

---

## Changelog

- **2026-06-05**: initial audit doc post-discovery + `/sc:spec-panel` critique (8 finding: 3 CRIT + 4 MAJ + 3 MIN). DEC-P4-1..DEC-P4-4 lockate. 3 follow-up issue Task A+B+C identificate. Journey #4+#5 deferred wave futuro. MVP cut chiude #1899.
