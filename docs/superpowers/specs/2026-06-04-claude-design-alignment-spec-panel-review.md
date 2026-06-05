# Claude Design Alignment — Spec-Panel Review

**Data**: 2026-06-04
**Issue umbrella**: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895)
**Sub-issue**: [#1896 (A)](https://github.com/meepleAi-app/meepleai-monorepo/issues/1896) · [#1897 (B)](https://github.com/meepleAi-app/meepleai-monorepo/issues/1897) · [#1898 (C)](https://github.com/meepleAi-app/meepleai-monorepo/issues/1898) · [#1899 (D)](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899)
**Origine**: `/sc:spec-panel` mode critique — 6 esperti (Wiegers · Cockburn · Adzic · Fowler · Nygard · Crispin)
**Baseline review**: gap report [38 gap](../../for-developers/audits/2026-06-04-claude-design-gap-report.md) · domain model [20 invarianti](../../for-developers/specs/2026-06-04-gamenight-session-domain-model.md) · prototipo `claude-design-handoff/2026-06-04/`

> Questo documento è il **source of truth post-panel** delle decisioni di scope/architettura/effort assunte sull'umbrella #1895. Va letto prima di iniziare qualsiasi PR su #1896/#1897/#1898/#1899. Le sezioni "Update richiesti" mappano 1:1 sui body delle issue come addendum applicato il 2026-06-04.

---

## Sezione 1 — Quality Assessment baseline

| Asse | Clarity | Completeness | Testability | Consistency | Overall |
|------|---------|--------------|-------------|-------------|---------|
| Umbrella #1895 | 7.5/10 | 6.5/10 | 5.0/10 | 7.0/10 | **6.5** |
| Asse A #1896 (BE) | 8.0/10 | 6.0/10 | 7.0/10 | 7.5/10 | **7.1** |
| Asse B #1897 (UI shell) | 7.5/10 | 6.5/10 | 6.5/10 | 7.0/10 | **6.9** |
| Asse C #1898 (Dashboard) | 8.0/10 | 6.0/10 | 6.5/10 | 7.5/10 | **7.0** |
| Asse D #1899 (Routes) | 7.0/10 | 5.5/10 | 4.5/10 | 6.5/10 | **5.9** |

**Verdetto**: umbrella strutturalmente solida (4 assi A/B/C/D logici) ma 3 blind spot strutturali + 1 sottostima effort ~40% + 1 lacuna operativa critica (no rollback strategy).

---

## Sezione 2 — Decisioni lockate (6)

### DEC-1 · Scoring polimorfico estende asse A

**Driver**: CRIT-1 / CRIT-7 (Fowler + Wiegers).
**Decisione**: aggiungere `ScoreType {Points, BinaryWin, Objectives, Ranking}` + pattern `IScoringStrategy` + migration `sessions.scoring_type` + DTO polymorphic dentro #1896.
**Motivazione**: gap report TOP 10 #4 "flow salva session rotto per ~50% catalog board game" è blocker semantico. Senza ScoreType backend, drawer FE è solo placeholder. Mettere in asse A separato dal D pulisce dependency graph (D drawer editor gated su A complete).
**Impatto**: effort A da L (~5 gg) a XL (~10-14 gg, vedi DEC-6).

### DEC-2 · 3 stub critiche costruite in asse D, 2 deferred

**Driver**: CRIT-2 (Cockburn).
**Decisione**:
- **Costruire dentro #1899**: `/sessions/[id]` (session detail) · `/game-nights/[id]/summary` (GN summary post-completed) · `/games` tab Discover (esplicito).
- **Defer "Coming soon" toast**: `/knowledge-base` · `/toolkit/[id]`.

**Motivazione**: CTA primarie "Vai al riepilogo" e click toolkit/session card sono user-facing path principali. KB hub e toolkit detail sono CTA secondarie, accettabile defer con feedback (toast non-bloccante 4s). Mappa gap TOP 10 #8 (session detail) + gap #13 (summary) + gap #6 (chat agent → out of scope umbrella).
**Impatto**: asse D effort +3-5 giorni.

### DEC-3 · Designer-led review checklist per route

**Driver**: CRIT-3 (Wiegers + Crispin).
**Decisione**: ogni PR asse D include nel body una **Design Review Checklist** ~8-12 item semantici firmata da designer. Tracking matrix nell'umbrella aggregato.
**Motivazione**: criterio "riconoscibile al prototipo" non testabile programmaticamente (visual regression rimossa 2026-05-20). Designer è l'autority finale. Checklist evita bikeshedding.
**Esempio template per route**:
```markdown
## Design Review Checklist — /game-nights/[id]
- [ ] Sidebar 2 voci game-related (Library + Games) ✓
- [ ] Drawer stack pattern usato (no full-page navigate per Player peek)
- [ ] Entity color GameNight (rosa) su CTA primaria
- [ ] Sezione Player & RSVP con stati badge ✓ User / Guest
- [ ] Sezione Sessions self-contained con paginazione
- [ ] 5 stati (default/empty/loading/error/offline) toggleabili in dev
- [ ] CTA contestuali per status (Planned: Modifica RSVP, InProgress: Aggiungi session, Completed: Aggiungi note)
- [ ] Token discipline (no hex hardcoded, var(--c-*) only)
- [ ] Hero matches prototipo screenshot `gn-detail.png` (semantic match, no pixel-perfect)
- [ ] Designer approved-by: @<designer-handle>
- [ ] Designer approved-on: YYYY-MM-DD
```
**Impatto**: umbrella tracking matrix new section.

### DEC-4 · Accept + documented escalation path (no feature flag)

**Driver**: CRIT-5 (Nygard).
**Decisione**: zero infrastructure feature flag. Documentato **rollback playbook** in umbrella.
**Playbook**:
1. **Trigger**: production incident detected (user report / monitoring alert / failed deploy)
2. **SLA**: rollback decision <30 min, execution <2h
3. **Mechanism**: `git revert <PR-merge-commit>` + emergency hotfix PR + merge --admin
4. **On-call**: dev autore del PR + reviewer (rotating)
5. **Escalation**: se >3 rollback in 24h finestra, banner emergency in landing `<EmergencyBanner severity="degraded" />` + freeze merge umbrella
6. **Post-mortem**: required per ogni rollback, output in `docs/for-developers/audits/incident-postmortems/`

**Motivazione**: feature flag avrebbe aggiunto ~30% complessità FE code per 2-4 settimane; team MVP-stage non ha capacity sostenere overhead. Trade-off accettato.
**Impatto**: umbrella aggiunge sezione "Rollback playbook".

### DEC-5 · Notification system: in-app inbox + email transactional

**Driver**: CRIT-6 (Cockburn + Wiegers).
**Decisione**: invariante #17 (RSVP pending in dashboard invitato) richiede notification reale. Scope MVP:
- **In-app inbox**: entity `Notification` + `/notifications` route + bell badge counter sidebar
- **Email transactional**: invio sincrono su `SendInvitationCommand` via **Resend** (o SendGrid se config esistente)
- **OUT of MVP**: push (mobile), webhook external, SMS, digest emails

**Motivazione**: in-app only è troppo passivo (Anna deve aprire app per vedere invito → friction RSVP). Email transactional copre il caso "Anna non aperta app oggi". Push richiede mobile infrastructure → SP8.
**Impatto**: asse A effort +5 giorni (provider integration + template + test). Nuovo secret `RESEND_API_KEY` in `infra/secrets/email.secret`.

### DEC-6 · Effort rebaseline + buffer 20% asse A

**Driver**: CRIT-4 (Nygard).
**Decisione**:

| Asse | Stima originale | Stima rebaseline | Note |
|------|----------------|------------------|------|
| A #1896 (BE semantic) | L (~3-5 gg) | **XL (~15 gg)** | +scoring (DEC-1) +notification (DEC-5) +20% buffer su uncertainty |
| B #1897 (UI shell) | M (~2-3 gg) | **M+ (~8 gg)** | +token additions (MAJ-9) +sidebar 8 voci (CRIT-8) +Storybook |
| C #1898 (Dashboard) | M (~2-3 gg) | **M (~4 gg)** | +empty-state matrix (MAJ-6) +Friends scope (MAJ-5) |
| D #1899 (Routes) | L+ (~5-10 gg) | **XL (~25 gg)** | +3 stub (DEC-2) +designer checklist (DEC-3) +v2-shipped audit (MAJ-10) +cross-asse E2E (MAJ-11) |
| **Totale** | ~3-5 settimane | **~7-8 settimane single-dev / 4-5 settimane parallel** | |

**Critical path**: asse A è il bottleneck (15 gg + blocca D drawer editor e C dashboard live state). Parallelizzare D.1 (library + games detail FE-only) con A late stage.

---

## Sezione 3 — Findings (32 totali)

### 🔴 CRITICAL (8)

| # | Finding | Asse | Expert | Status |
|---|---------|------|--------|--------|
| CRIT-1 | Scoring polimorfico orfano (nessun asse possedeva) | A | Fowler, Wiegers | **Risolto** via DEC-1 |
| CRIT-2 | 5 stub route non assegnate | Umbrella | Cockburn | **Risolto** via DEC-2 |
| CRIT-3 | DoD "riconoscibile" non testabile | Umbrella, D | Wiegers, Crispin | **Risolto** via DEC-3 |
| CRIT-4 | Effort sottostimato ~40% | Tutti | Nygard | **Risolto** via DEC-6 |
| CRIT-5 | No rollback strategy ("UI mista 2-4 weeks" blind spot) | Umbrella | Nygard | **Risolto** via DEC-4 |
| CRIT-6 | Asse A manca invariante #15 esplicita + scope notification ambiguous | A | Cockburn, Wiegers | **Risolto** via DEC-5 + scope additions sezione 4 |
| CRIT-7 | Asse D session editor bloccato da CRIT-1 | D | Fowler | **Risolto** via DEC-1 (asse D drawer gated) |
| CRIT-8 | Sidebar conta 7 voci, claim 8 | B | Doumont | **Aperto** — vedi scope addition asse B |

### 🟡 MAJOR (12)

| # | Finding | Asse | Expert | Action |
|---|---------|------|--------|--------|
| MAJ-1 | Migration SQL circolare (DEFAULT now() + UPDATE updated_at) | A | Hohpe | Riscrivere come 3-step ALTER NULL → UPDATE → ALTER NOT NULL |
| MAJ-2 | Backfill IsInvited=true è product decision silente | A | Wiegers | Documentare backwards-compat decision |
| MAJ-3 | StatePreviewProvider implementation undefined | B | Fowler | Pattern NODE_ENV gate + dynamic import; acceptance "0 prod bytes" |
| MAJ-4 | WizardModal.validate signature undefined | B | Crispin, Fowler | TypeScript signature `validate: () => Promise<{valid: boolean, errors?: ValidationError[]}>` |
| MAJ-5 | Friends feed entity undefined | C | Cockburn | Definire "friend = User-linked player with ≥1 shared GN last 90gg" |
| MAJ-6 | Empty state inconsistent (4 sezioni × 5 stati) | C | Crispin | Matrice 4×5 cell-by-cell |
| MAJ-7 | Paginazione "Recenti" assente | C | Cockburn | Link "Vedi tutti" → `/game-nights?status=completed` |
| MAJ-8 | 5 stati toggleabili — chi testa? | D | Crispin | Manual QA checklist per route in `docs/for-developers/qa/` |
| MAJ-9 | Token tokenization gap #37/#38 non scopato | D, B | Fowler | `--c-warning-ink` + overlay tokens in B; D references |
| MAJ-10 | v2-shipped coordination audit pre-PR missing | D | Crispin, Fowler | Audit `v2-migration-matrix.md` pre-asse-D |
| MAJ-11 | Cross-asse integration testing assente | Umbrella | Crispin, Newman | 3-5 E2E user journey cross-asse |
| MAJ-12 | Sequencing rigid A→B→C→D ignora critical path slack | Umbrella | Nygard | Buffer 20% asse A; parallelizzare D.1 |

### 🟢 MINOR (12)

| # | Finding | Asse | Fix |
|---|---------|------|-----|
| MIN-1 | `MULTIPLE_LIVE_NOT_ALLOWED` → `MaxLiveSessionsExceededException` | A | Rename .NET convention |
| MIN-2 | Suggested algorithm "fixture o naive" senza roadmap | C | Bullet "MVP fixture → V2 algorithm deferred" |
| MIN-3 | Drawer animation no `prefers-reduced-motion` | B | Add a11y clause |
| MIN-4 | PR D.5 mixe auth + onboarding + catalog | D | Split D.5a (catalog) + D.5b (auth/onboarding) |
| MIN-5 | "MVP della serata" KPI undefined | C | "Player con highest score in session più recente" |
| MIN-6 | `Session.OpenLiveMode()` lifecycle unclear | A | Clarify factory vs constructor |
| MIN-7 | sonner library reference verify | B | Check `package.json` |
| MIN-8 | Spec governance amendment process | Umbrella | "PR a spec doc + lock-in pre-merge sub-issue" |

---

## Sezione 4 — Update richiesti per issue

### Umbrella #1895

Aggiungere sezioni:
1. **Rollback playbook** (DEC-4 sopra)
2. **Timeline rebaseline** (DEC-6 tabella sopra)
3. **Designer review tracking matrix** (DEC-3, popolare on-going)
4. **Critical path identification** (asse A bottleneck, parallel D.1)
5. **Spec governance** (MIN-8)

Update Definition of Done:
- Aggiungi: "Spec consolidato spec-panel review aggiornato in `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md`"
- Aggiungi: "Designer review tracking matrix completata per 13 route"
- Aggiungi: "Rollback playbook validato (almeno 1 dry-run su staging)"

### Asse A #1896

**Scope additions**:
- [ ] **Invariante #15 esplicita** etichettata: state machine triggered by first Session creation of any type via `SessionCreatedDomainEvent`
- [ ] **DEC-1 ScoreType polimorfico**:
  - Enum `ScoreType { Points, BinaryWin, Objectives, Ranking }` con `Description` attribute per UI
  - Interface `IScoringStrategy` con metodi `Validate(scoreData)`, `Serialize(scoreData)`, `ComputeWinner()`
  - 4 strategy implementations + factory `ScoringStrategyFactory.GetStrategy(scoring_type)`
  - Migration: `sessions.scoring_type VARCHAR(20) NOT NULL DEFAULT 'Points'` (backfill all existing)
  - DTO: `SaveSessionCommand` accept polymorphic `scoreData: object` (JSON) validated per `scoring_type` via FluentValidation custom rule
  - Domain method `Session.SetScores(scoring_type, data)` con validation per-strategy + domain event `SessionScoresUpdated`
  - 4 unit test class (1 per strategy) + 1 integration test (round-trip via API)
- [ ] **DEC-5 Notification system**:
  - Entity `Notification` con campi (Id, RecipientUserId, Type, Payload JSON, ReadAt nullable, CreatedAt)
  - Bounded context: `UserNotifications` (esistente, vedi CLAUDE.md)
  - REST endpoint: `GET /api/v1/notifications?page=N&size=M` (paginated inbox) + `PATCH /api/v1/notifications/{id}/read`
  - Command: `SendInvitationNotificationCommand` handler emette in-app `Notification` + email via `IEmailSender`
  - Email template `GameNightInvitation` (HTML + plain text)
  - Provider: Resend (preferito, simple API) — concrete `ResendEmailSender : IEmailSender`
  - Secret: `RESEND_API_KEY` in `infra/secrets/email.secret` (nuovo)
  - Integration test: `SendGameNightInvitations_QueuesInAppAndSendsEmail` con mock `IEmailSender`
- [ ] **MAJ-1 fix migration SQL**: pattern 3-step (ALTER NULL → UPDATE → ALTER NOT NULL)
- [ ] **MAJ-2 backwards-compat**: documentare "IsInvited=true backfill = existing player sono già 'invited' (auto-shared pattern legacy)"
- [ ] **MIN-1 rename**: `MaxLiveSessionsExceededException` + error code `MAX_LIVE_SESSIONS_EXCEEDED` (HTTP 409)
- [ ] **MIN-6 Session.OpenLiveMode lifecycle**: chiarire come factory method (preferred) — Session instance già esiste, OpenLiveMode è state transition
- [ ] **Effort updated**: L → **XL (~15 gg dev + 3 gg test/review)**

### Asse B #1897

**Scope additions**:
- [ ] **CRIT-8 sidebar enumeration**: ricontare voci. Aggiungere voce `/notifications` (bell icon sidebar bottom) → 8 voci totali = Dashboard · Library · Games · Sessions · Agents · Game Nights · Notifications · Profile
- [ ] **MAJ-3 StatePreviewProvider** implementation pattern:
  - Dynamic import gated `NODE_ENV !== 'production'`
  - Tree-shaking verify via `next build` + grep output bundle
  - Acceptance: `pnpm build && grep -r 'StatePreviewProvider' .next/static/chunks/` returns 0 matches
- [ ] **MAJ-4 WizardModal API**:
  ```typescript
  interface WizardStep {
    title: string;
    content: ReactNode;
    validate?: () => Promise<{ valid: boolean; errors?: ValidationError[] }>;
    optional?: boolean;
  }
  interface ValidationError {
    field?: string;
    message: string;
  }
  ```
- [ ] **MAJ-9 token additions** in `apps/web/src/styles/design-tokens-canonical.css`:
  - `--c-warning-ink: hsl(38 92% 32%)` (gap #37)
  - `--c-overlay-scrim: hsla(0 0% 0% / 0.6)` (gap #38)
  - `--c-overlay-gradient-end: hsl(25 95% 38%)` (gap #38)
- [ ] **MIN-3 a11y**: drawer animation respects `@media (prefers-reduced-motion: reduce)` → animation duration 0ms
- [ ] **MIN-7 sonner verify**: check `apps/web/package.json` → add `sonner@latest` if missing
- [ ] **Effort updated**: M → **M+ (~7 gg dev + 2 gg Storybook/test)**

### Asse C #1898

**Scope additions**:
- [ ] **MAJ-5 Friends qualification**:
  - Definizione MVP: "friend = User-linked player con almeno 1 shared GameNight negli ultimi 90gg"
  - Query `GetUserFriendsActivityQuery` returns `FriendActivity[]` con campi (FriendUserId, Avatar, Name, Verb, GameOrEventRef, Timestamp)
  - Backend endpoint: `GET /api/v1/dashboard/friends-activity?limit=10`
  - Fallback: empty state "Nessuna attività recente dai tuoi amici"
- [ ] **MAJ-6 empty-state matrix 4×5**:

  | Sezione \ Stato | default | empty | loading | error | offline |
  |---|---|---|---|---|---|
  | Prossimi | 2-3 card | CTA "Crea prima GN" | skeleton 2 card | banner rosso + retry | cache + banner ambra |
  | Recenti | 2-3 card | hidden | skeleton 2 card | banner rosso + retry | cache + banner ambra |
  | Suggested | 4-6 card horizontal | hidden | skeleton 4 card | hidden (error → fallback hidden) | cache |
  | Friends | 2-3 entry | hidden | skeleton 3 entry | hidden | cache |

- [ ] **MAJ-7 paginazione Recenti**: link "Vedi tutti i completati →" footer sezione → `/game-nights?status=completed`
- [ ] **MIN-2 suggested roadmap**: nota "MVP fixture → V2 algorithm spec deferred a sub-issue futura post-MVP"
- [ ] **MIN-5 MVP della serata**: "Player con highest score nella session più recente della GN. Per BinaryWin/Objectives: winner per default. Per Ranking: position 1."
- [ ] **Effort**: M (~3-4 gg dev + 1 gg test) [stima invariata]

### Asse D #1899

**Scope additions**:
- [ ] **DEC-2 build 3 stub critici** (gating su PR sequence):
  - `/sessions/[id]` (session detail live+summary) — in PR D.4 con `/sessions`
  - `/game-nights/[id]/summary` (GN summary post-completed) — in PR D.3 con `/game-nights/[id]`
  - `/games` con tab Discover (esplicito) — in PR D.5a
- [ ] **DEC-2 toast "Coming soon"** per `/knowledge-base` (in `/games/[id]` tab KB) e `/toolkit/[id]` (in `/discover` toolkit card)
- [ ] **DEC-3 Design Review Checklist** per ogni route (template sopra). Tracking aggregato in umbrella.
- [ ] **MAJ-8 5-state test plan**: manual QA checklist per route in `docs/for-developers/qa/2026-06-04-route-state-manual-qa.md` (creato pre-PR D.1)
- [ ] **MAJ-10 v2-shipped audit**: pre-asse-D, audit `docs/for-developers/frontend/v2-migration-matrix.md` — output lista esplicita routes v2-shipped affected (commit in PR D.0 audit, prima di D.1)
- [ ] **MAJ-11 cross-asse E2E**: 3-5 user journey Playwright in `apps/web/e2e/cross-asse-flows.spec.ts`:
  1. Dashboard → drawer GameNight → drawer Player swap → ESC back → backdrop close
  2. Dashboard empty → CTA "Crea prima GN" → wizard 3-step → Live mode opt-in
  3. Game Detail tab Partite → paginazione inline (NO navigate /sessions)
  4. Invitation flow: Anna login → /notifications → click invito → /game-nights/[id] pending → RSVP confirm → dashboard normale
  5. Session live mode → toast warning "salva draft con live attiva" → click toast link → switch a live session
- [ ] **MIN-4 PR sequence aggiornato** (6 PR invece di 5):
  1. **PR D.0** — v2-migration-matrix audit + state QA checklist (`docs/` only)
  2. **PR D.1** — `/library` + `/games/[id]` (collection + detail)
  3. **PR D.2** — `/game-nights` + `/game-nights/new` (index + wizard)
  4. **PR D.3** — `/game-nights/[id]` + `/game-nights/[id]/live` + **`/game-nights/[id]/summary`** (detail + live + summary)
  5. **PR D.4** — `/sessions` + **`/sessions/[id]`** + `/agents` + `/agents/[id]` (archive + agent stack + session detail)
  6. **PR D.5a** — `/games` con tab Discover + tab Discover content (catalog)
  7. **PR D.5b** — `/login` + `/register` + `/onboarding` (auth)
- [ ] **Effort updated**: L+ → **XL (~25 gg distribuiti)**

---

## Sezione 5 — Sequence rebaseline + critical path

```
Settimana 1: Asse A start (semantic foundations + Migration #11/#15 + Notification entity skeleton)
                              |
Settimana 2: Asse B start (PR per primitives) — parallel ad A
             Asse A continue (ScoreType + IScoringStrategy)
                              |
Settimana 3: Asse A continue (Notification email integration + tagging/RSVP)
             Asse B PR merged
             Asse D PR D.0 (audit doc only)
                              |
Settimana 4: Asse A complete + merged
             Asse C start (depends su A status + B drawer)
             Asse D PR D.1 (library + game detail FE-only, no live state) — parallel
                              |
Settimana 5: Asse C complete + merged
             Asse D PR D.2 (GN index + wizard)
                              |
Settimana 6: Asse D PR D.3 (GN detail + live + summary)
                              |
Settimana 7: Asse D PR D.4 (sessions + agents)
                              |
Settimana 8: Asse D PR D.5a + D.5b (catalog + auth/onboarding)
             Final designer review tracking matrix complete
             Umbrella close
```

**Bottleneck**: asse A in settimane 1-3. Slip qui cascata su C (Dashboard live state) + D.3 (Live mode) + D.4 (session detail polymorphic scoring).

**Parallelization opportunity**: D.1 (library + games detail) FE-only può start settimana 4 senza dipendere da A complete (game detail tab "Partite" mostra cards Session ma il polymorphic scoring solo nella drawer editor di D.3).

---

## Sezione 6 — Rollback playbook (DEC-4)

### Trigger condizioni
- **Production incident detected**:
  - User report via support channel (Discord, email)
  - Monitoring alert (error rate >5% in 5 min finestra)
  - Failed deploy / smoke test fail
- **UX confusion riportata**:
  - >5 support tickets riferiti alla nuova UI in 24h
  - Designer feedback "regressione vs prototipo"

### SLA
- **Decision time**: <30 minuti dal trigger
- **Execution time**: <2 ore (incluso CI verde post-revert)
- **Communication**: aggiornamento status page entro 15 minuti

### Mechanism
```bash
# 1. Identify offending PR merge commit
git log --oneline --merges main-dev | head -5

# 2. Revert (creates new commit, preserves history)
git checkout -b hotfix/revert-pr-XXXX
git revert -m 1 <merge-commit-sha>
git push -u origin hotfix/revert-pr-XXXX

# 3. Emergency PR with admin override
gh pr create --title "hotfix: revert PR #XXXX (incident YYYY-MM-DD)" \
  --body "Rollback per incident <description>. Postmortem TBD." \
  --base main-dev
gh pr merge <pr-number> --squash --admin --delete-branch

# 4. Verify
gh run watch # CI smoke test
# 5. Post-mortem (required)
# Create docs/for-developers/audits/incident-postmortems/YYYY-MM-DD-incident.md
```

### On-call
- **Primary**: dev autore del PR mergeato
- **Secondary**: dev reviewer del PR
- **Rotation**: settimanale, documentata in `docs/for-developers/on-call-schedule.md`

### Escalation
- **>3 rollback in 24h**: deploy `<EmergencyBanner severity="degraded" />` in landing + **freeze merge umbrella** finché root cause identificata
- **>1 rollback per route specifica**: opzione re-introdurre feature flag NEXT_PUBLIC_DEMO_ALIGNMENT_<ROUTE>=false per quella route
- **Critical infra failure (DB migration corrupted, OAuth broken)**: page CTO + product owner

### Post-mortem template
Mandatory entro 5 giorni dal rollback:
- Cosa è successo (timeline)
- Cosa abbiamo imparato
- Cosa cambiamo (action items con owner + deadline)
- Output in `docs/for-developers/audits/incident-postmortems/`

---

## Sezione 7 — Designer Review Tracking Matrix

Popolare on-going durante implementazione asse D.

| Route | PR | Designer approved-by | Date | Status |
|-------|-----|-------|------|--------|
| `/library` | D.1 | TBD | — | pending |
| `/games/[id]` | D.1 | TBD | — | pending |
| `/game-nights` | D.2 | TBD | — | pending |
| `/game-nights/new` | D.2 | TBD | — | pending |
| `/game-nights/[id]` | D.3 | TBD | — | pending |
| `/game-nights/[id]/live` | D.3 | TBD | — | pending |
| `/game-nights/[id]/summary` | D.3 | TBD | — | pending |
| `/sessions` | D.4 | TBD | — | pending |
| `/sessions/[id]` | D.4 | TBD | — | pending |
| `/agents` | D.4 | TBD | — | pending |
| `/agents/[id]` | D.4 | TBD | — | pending |
| `/games` (tab Discover) | D.5a | TBD | — | pending |
| `/login` + `/register` | D.5b | TBD | — | pending |
| `/onboarding` | D.5b | TBD | — | pending |
| `/dashboard` | C (asse separato) | TBD | — | pending |

---

## Sezione 8 — Spec governance (MIN-8)

**Process amendments**:
1. Modifica delle 20 invarianti dominio → PR a `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`
2. Lock-in pre-merge della sub-issue affetta (es. modifica invariante #15 lock-a #1896)
3. Nuove invarianti emerse durante implementazione → PR umbrella con sezione "Nuova invariante #N proposta"
4. Approver: product owner + dev autore PR + 1 reviewer asse interessato
5. Update `docs/superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md` con changelog inline

**Changelog spec consolidato**:
- 2026-06-04: initial spec-panel review (DEC-1..DEC-6 + 32 findings)
- 2026-06-05: asse A v2.1 implementation COMPLETE via subagent-driven sessione 32 — branch `feature/issue-1896-semantic-alignment`, ~15 commit (12 feat + 2 fix + 3 docs). 80+ unit test added. WP4 audit-only (T11+T12+T13 già shipped upstream #2053+#1629+#5005). Effort reale ~10.5gg (vs v2 stima 12gg, v1 stima 18gg → -42% rispetto plan iniziale post-discovery). Security: 1 HIGH IDOR finding catched + fixato in `c1efb4fb6`. 0 regression su SessionTracking 472/472 + Scoring 113/113 suites.
- 2026-06-05: asse B v2 implementation COMPLETE via subagent-driven sessione 33 — branch `feature/issue-1897-ui-shell-pattern`, ~8 commit (7 feat/fix + 1 docs). ~120+ unit test added (T1 design tokens 8 + T2 MainSidebar 37 + T3 cascade-store extend 12 + T4 WizardModal 38 + T5 StatePreview 13 + T6 SSE counter 12 = ~120). 0 regression. Effort actual ~6gg (vs v1 stima 9gg → -33% post-discovery). cascade-store + Drawer + sonner già shipped upstream pre-asse-B.
- 2026-06-05: asse C v2 implementation COMPLETE via subagent-driven sessione 34 — branch `feature/issue-1898-dashboard-priority-driven`, ~9 commit. WP1 BE FriendsActivity endpoint + WP2 ProssimiSection + WP3 RecentiSection + WP4 SuggestedSection + WP5 FriendsActivitySection + WP6 GameNightDrawerContent + WP7 DashboardClient refactor in-place (DEC-1) + E2E skeleton `apps/web/e2e/dashboard-priority-flow.spec.ts`. ~75 unit test added across T2-T6 sections + 7 in updated DashboardClient orchestrator smoke test. 0 regression. Refactor in-place /dashboard (5 entity sections legacy Games/Players/Agents/Sessions/Events → 4 priority sections fixed order Prossimi→Recenti→Suggested→Friends). DashboardHero + KPI grid preserved as entry surface. Recenti BE endpoint per completed-GN list NOT yet wired → RecentiSection renderable con empty array `null` silent fallback fino al BE wave successivo.

---

## Riferimenti

- Gap report 38 gap: [`2026-06-04-claude-design-gap-report.md`](../../for-developers/audits/2026-06-04-claude-design-gap-report.md)
- Domain model 20 invarianti: [`2026-06-04-gamenight-session-domain-model.md`](../../for-developers/specs/2026-06-04-gamenight-session-domain-model.md)
- Prototipo runnable: `claude-design-handoff/2026-06-04/` (gitignored)
- CLAUDE.md § Domain Model — GameNight / Session
- v2 migration matrix: [`v2-migration-matrix.md`](../../for-developers/frontend/v2-migration-matrix.md)
- ADR-054 DevOps Multi-Branch Strategy: [`adr-054-devops-multi-branch-strategy.md`](../../for-developers/architecture/adr/adr-054-devops-multi-branch-strategy.md)
