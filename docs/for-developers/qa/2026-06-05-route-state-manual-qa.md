# Route State Manual QA — Reference Template

**Data**: 2026-06-05
**Issue**: [#1899 P4 follow-up](https://github.com/meepleAi-app/meepleai-monorepo/issues/1899)
**Umbrella**: [#1895](https://github.com/meepleAi-app/meepleai-monorepo/issues/1895)
**Origine**: [Spec consolidato MAJ-8](../../superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md) — Crispin (testing lead) + Adzic (spec-by-example) + Gregory (collaborative QA)
**Pattern**: Hybrid Crispin 5-state matrix × Adzic Given/When/Then + DEC-3 self-attestation MVP

---

## Scope

Manual QA checklist per le route asse D affected dal Claude Design alignment umbrella. Output: template usabile per (a) validazione PR future + (b) self-attestation MVP (no designer attivo).

**5 stati canonici** (per ogni route owner):

| Stato | Trigger | Observable |
|---|---|---|
| **default** | Dati presenti, network healthy | Card grid / KPI / drawer renderizzati con contenuto reale |
| **empty** | Dati assenti, query risponde 200 con array vuoto | EmptySection con CTA contestuale (es. "Crea prima GN") |
| **loading** | Query in-flight, primo mount | Skeleton card / shimmer placeholder |
| **error** | Query risponde 4xx/5xx | Banner rosso + retry button + telemetry log |
| **offline** | Network unreachable, ServiceWorker cache hit | Cache rendered + banner ambra "Offline: dati in cache" |

**4 route reference** in questo template (P4 MVP cut):

1. `/dashboard` — asse C just-shipped sessione 34
2. `/game-nights/[id]/live` — asse D core (gated asse A polymorphic wire FE)
3. `/onboarding` — asse D P3 just-shipped sessione 37
4. `/games` — asse D P2 hub just-shipped sessione 36

**Out of scope template**:
- 13 route totali del v2-migration-matrix → questo template è **reference**, non exhaustive
- Future route auditor copia sezione "Template route nuova" → aggiunge nuova route
- Designer-led review checklist (DEC-3) rimane separata in PR body, non in questo file

---

## Quick reference matrix (5-stati × 4 route)

| Route \ Stato | default | empty | loading | error | offline |
|---|---|---|---|---|---|
| `/dashboard` | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| `/game-nights/[id]/live` | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| `/onboarding` | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |
| `/games` | ⬜ | ⬜ | ⬜ | ⬜ | ⬜ |

Compilatore: marca ✅ se scenario passa, ❌ se fail (apri issue di blocking), ➖ se N/A (es. `/onboarding` empty = login state non applicabile, vedi note route).

---

## Route 1 — `/dashboard` (asse C priority-driven)

**Owner asse**: C (#1898 shipped sessione 34)
**Sezioni gestite**: 4 priority sections in ordine fisso (Prossimi → Recenti → Suggested → Friends)
**Hero + KPI grid**: invariati pre-refactor (entry surface)

### Stato `default` — Dati presenti per tutte le sezioni

- **Given**: utente autenticato con ≥1 GameNight upcoming + ≥1 completed last 30gg + suggested fixture pool + ≥1 friend activity last 90gg
- **When**: utente naviga a `/dashboard` post-login
- **Then**:
  - DashboardHero + KPI grid renderizzati (entry surface, asse C preserved)
  - Sezione "Prossimi": 2-3 GameNight card Published+InProgress ASC, "+ Nuova" CTA inline
  - Sezione "Recenti": 2-3 GameNight card Completed DESC, "Vedi tutti i completati →" footer link
  - Sezione "Suggested": 4-6 "Potresti giocare" horizontal cards
  - Sezione "Friends": 2-3 entry verb completed/created/joined + avatar drawer hook
  - Ordine sezioni fisso (no DOM reorder)

**Screenshot allegato**: `qa-screenshots/2026-06-05/dashboard-default.png` (TODO)

### Stato `empty` — Nessun GameNight, nessun friend activity

- **Given**: utente autenticato fresh (no GN owned, no shared GN last 90gg)
- **When**: naviga a `/dashboard`
- **Then**:
  - Prossimi: EmptySection con CTA "Crea prima GameNight" → `/game-nights/new`
  - Recenti: hidden (null silent fallback per MAJ-6 matrix DEC-C)
  - Suggested: hidden (null silent fallback)
  - Friends: hidden (null silent fallback)
  - DashboardHero + KPI grid sempre presenti (zero state KPI = 0)

**Screenshot allegato**: `qa-screenshots/2026-06-05/dashboard-empty.png` (TODO)

### Stato `loading` — Query in-flight primo mount

- **Given**: cache miss + slow 3G simulation (DevTools Network throttling)
- **When**: naviga a `/dashboard`
- **Then**:
  - Prossimi: 2 skeleton card placeholder
  - Recenti: 2 skeleton card placeholder
  - Suggested: 4 skeleton card placeholder (horizontal)
  - Friends: 3 skeleton entry placeholder (avatar + verb shimmer)
  - DashboardHero + KPI grid rendering immediato (no skeleton, dati locali)

**Screenshot allegato**: `qa-screenshots/2026-06-05/dashboard-loading.png` (TODO)

### Stato `error` — Backend 5xx per Friends activity endpoint

- **Given**: BE down per `/api/v1/dashboard/friends-activity`, altri endpoint OK
- **When**: naviga a `/dashboard`
- **Then**:
  - Prossimi/Recenti/Suggested: default state (non bloccate da Friends error)
  - Friends: silent fallback hidden (NON banner rosso bloccante per error secondario, per MAJ-6 matrix)
  - Telemetry log error registrato (verify console + Sentry)

**Screenshot allegato**: `qa-screenshots/2026-06-05/dashboard-error.png` (TODO)

### Stato `offline` — Network unreachable, SW cache hit

- **Given**: precedente visita `/dashboard` con dati cached + DevTools Network offline
- **When**: refresh `/dashboard`
- **Then**:
  - 4 sezioni render con dati cached
  - Banner ambra top "Offline: dati potrebbero essere obsoleti" (verify wording exact)
  - CTA "+ Nuova" disabled in Prossimi (no POST possible offline)

**Screenshot allegato**: `qa-screenshots/2026-06-05/dashboard-offline.png` (TODO)

---

## Route 2 — `/game-nights/[id]/live` (asse D core, GATED asse A wire FE)

**Owner asse**: D core (gated asse A polymorphic ScoreType wire FE — currently shipped via D.P1 polymorphic editor sessione 35)
**Stato shipped sessione 35**: `PolymorphicScoreEditor` (Points/BinaryWin/Objectives/Ranking) + `useUpdateSessionScores` mutation hook + IDOR guard host-only
**Backend asse A SHIPPED sessione 32**: invariante #10 enforcement (`MaxLiveSessionsExceededException` HTTP 409) + invariante #15 wire (`SessionStartedHandler`)

### Stato `default` — Sessione live in corso

- **Given**: GameNight `InProgress` con `Session.StartedAt != null && FinalizedAt == null` (live), user è host
- **When**: naviga a `/game-nights/[id]/live`
- **Then**:
  - Hero session info (game thumbnail + start time + duration counter)
  - `PolymorphicScoreEditor` rendered con `scoringType` corrente (Points fallback default per backward-compat)
  - Player roster con score input live (autosave debounced 500ms)
  - Status badge "LIVE" + pulsante "Termina sessione" host-only

**Screenshot allegato**: `qa-screenshots/2026-06-05/gn-live-default.png` (TODO)

### Stato `empty` — Sessione live ma 0 player ancora joined

- **Given**: GameNight `InProgress`, Session live opened, ma 0 player in roster (tutti pending RSVP)
- **When**: naviga a `/game-nights/[id]/live`
- **Then**:
  - Hero session info presente
  - `PolymorphicScoreEditor` hidden (no player → no scoring possible)
  - EmptySection "Nessun player ancora unito" + CTA "Invita ora" → drawer invitation
  - Status badge "LIVE - In attesa player"

**Screenshot allegato**: `qa-screenshots/2026-06-05/gn-live-empty.png` (TODO)

### Stato `loading` — Initial mount + score data fetch

- **Given**: cache miss + slow network
- **When**: naviga a `/game-nights/[id]/live`
- **Then**:
  - Hero skeleton (game thumbnail placeholder + counter "...")
  - PolymorphicScoreEditor skeleton (4 row placeholder)
  - Roster skeleton (3 avatar placeholder)

**Screenshot allegato**: `qa-screenshots/2026-06-05/gn-live-loading.png` (TODO)

### Stato `error` — IDOR/Forbidden (non-host tenta accesso)

- **Given**: user è player ma non host, GameNight live
- **When**: naviga a `/game-nights/[id]/live`
- **Then**:
  - Backend asse D P1 IDOR guard catches (sessione 35 fix `c1efb4fb6`)
  - 403 Forbidden → redirect a `/game-nights/[id]` summary view (read-only)
  - Toast warning "Solo l'host può accedere alla live mode"

**Screenshot allegato**: `qa-screenshots/2026-06-05/gn-live-error-forbidden.png` (TODO)

### Stato `offline` — Network down durante autosave score

- **Given**: live session in corso + DevTools Network offline
- **When**: host edita score input
- **Then**:
  - Optimistic UI update locale
  - Banner ambra "Offline: modifiche salvate localmente, sync su reconnect"
  - Mutation queue in `useUpdateSessionScores` hook fino a reconnect

**Screenshot allegato**: `qa-screenshots/2026-06-05/gn-live-offline.png` (TODO)

---

## Route 3 — `/onboarding` (asse D P3 wizard 3-step)

**Owner asse**: D P3 (shipped sessione 37)
**Pattern**: `OnboardingGenericWizard` (WizardModal asse-B) con 3 step: InterestsStep + FirstGameStep + InviteFriendComingSoonStep
**Constraint legale BGG**: user-side BGG access bloccato (#1903 ADR) → FirstGameStep usa catalog interno `api.games.getAll`

### Stato `default` — User fresh login, no onboarding completato

- **Given**: user appena registrato (`onboardingCompleted: false`)
- **When**: post-login redirect a `/onboarding`
- **Then**:
  - WizardModal renderizzato con step 1/3 "I tuoi interessi"
  - 3 step nav indicator (Interessi → Primo gioco → Invita)
  - InterestsStep multi-select chips (boardgame/wargame/RPG/etc.)
  - CTA "Avanti" disabled finché ≥1 chip selected

**Screenshot allegato**: `qa-screenshots/2026-06-05/onboarding-default-step1.png` (TODO)

### Stato `empty` — N/A (onboarding NON ha empty state — user mai 0 step)

- **Note**: `/onboarding` è wizard linear flow. "Empty" non si applica — il wizard SEMPRE parte da step 1 con InterestsStep popolato da fixture chips.

➖ **N/A** — verifica wizard NON renderizzato per user con `onboardingCompleted: true` (redirect a `/dashboard`)

### Stato `loading` — Step 2 FirstGameStep query `api.games.getAll`

- **Given**: user su step 2 + slow network
- **When**: FirstGameStep mount
- **Then**:
  - Search input + skeleton list (3 placeholder card)
  - Loading spinner inline
  - "Salta" link top-right sempre visibile (no blocking)

**Screenshot allegato**: `qa-screenshots/2026-06-05/onboarding-loading-step2.png` (TODO)

### Stato `error` — BE down per `api.games.getAll`

- **Given**: step 2 mount + BE 5xx
- **When**: FirstGameStep mount con error
- **Then**:
  - Error banner inline "Impossibile caricare giochi. [Riprova]"
  - Retry button funzionante
  - "Salta" link sempre visibile (degradazione gracefully)

**Screenshot allegato**: `qa-screenshots/2026-06-05/onboarding-error-step2.png` (TODO)

### Stato `offline` — Network down durante wizard

- **Given**: user su step 1 → completa → step 2 + offline
- **When**: tenta navigation step 2 → step 3
- **Then**:
  - Banner ambra "Offline: progresso salvato localmente"
  - Wizard state persistito in localStorage (verifica `useLocalStoragePersist`)
  - Reconnect → flush a backend via `useCompleteOnboarding` mutation

**Screenshot allegato**: `qa-screenshots/2026-06-05/onboarding-offline.png` (TODO)

---

## Route 4 — `/games` (asse D P2 hub multi-tab)

**Owner asse**: D P2 (shipped sessione 36)
**Pattern**: `/games` hub multi-tab orchestrator con 4 tab (`discover` default / `catalogo` / `trending` / `community`)
**3 tab placeholder**: `catalogo`, `trending`, `community` = ComingSoon stub. `discover` è tab default e ha contenuto reale.

### Stato `default` — Tab Discover con dataset

- **Given**: user autenticato + Discover dataset fixture popolato
- **When**: naviga a `/games` (o `/games?tab=discover`)
- **Then**:
  - MiniNav strip 4 tab visibili, "Discover" attivo
  - DiscoverHub renderizzato (DiscoverSection horizontal rows: Popular / New / Friends play)
  - URL `/games?tab=discover` (parseTab fallback a discover su missing param)

**Screenshot allegato**: `qa-screenshots/2026-06-05/games-default.png` (TODO)

### Stato `empty` — Discover dataset vuoto

- **Given**: BE Discover endpoint risponde 200 con `{ sections: [] }`
- **When**: naviga a `/games`
- **Then**:
  - MiniNav sempre presente
  - DiscoverHub EmptySection "Nessun gioco da scoprire al momento" + CTA "Esplora catalogo →"
  - Fallback link catalogo placeholder ComingSoon

**Screenshot allegato**: `qa-screenshots/2026-06-05/games-empty.png` (TODO)

### Stato `loading` — Tab switch Discover→Catalogo

- **Given**: user su Discover, click tab "Catalogo"
- **When**: URL update a `/games?tab=catalogo`
- **Then**:
  - MiniNav indicator slides a "Catalogo"
  - Content area: skeleton (cosa? ComingSoon placeholder ha skeleton intrinseco?)
  - Tab attivo cambia immediatamente (no flicker)

**Screenshot allegato**: `qa-screenshots/2026-06-05/games-loading-tab-switch.png` (TODO)

### Stato `error` — Tab invalid in URL `?tab=foo`

- **Given**: user con bookmark stale `/games?tab=foo`
- **When**: naviga a `/games?tab=foo`
- **Then**:
  - parseTab fallback a `discover` (default safe)
  - URL NON corretto silently (preservato `?tab=foo` ma render Discover)
  - Console warning log per debugging

**Screenshot allegato**: `qa-screenshots/2026-06-05/games-error-invalid-tab.png` (TODO)

### Stato `offline` — Network down su Discover dataset fetch

- **Given**: user prima visita Discover (cache hit) + offline
- **When**: refresh `/games`
- **Then**:
  - Discover cache rendered
  - Banner ambra "Offline: catalogo in cache"
  - Tab placeholder ComingSoon non affected (no fetch)

**Screenshot allegato**: `qa-screenshots/2026-06-05/games-offline.png` (TODO)

---

## Template route nuova (copia per future route)

```markdown
## Route N — `/path/to/route` (descrizione)

**Owner asse**: X (PR #YYYY)
**Pattern**: descrizione architetturale

### Stato `default`
- **Given**: precondizioni
- **When**: azione user
- **Then**: observable atteso

**Screenshot allegato**: `qa-screenshots/YYYY-MM-DD/route-default.png` (TODO)

### Stato `empty`
- **Given**: ...
- **When**: ...
- **Then**: ...
(o ➖ N/A con motivazione)

### Stato `loading`
...

### Stato `error`
...

### Stato `offline`
...
```

---

## Self-attestation (DEC-3 MVP — no designer attivo)

Ogni route validata richiede compilazione **Self-attestation block** in fondo al PR body:

```markdown
## Self-attestation Manual QA — /route/path

**Compilato da**: @<github-handle>
**Data**: YYYY-MM-DD
**Browser**: Chrome 120 / Firefox 121 / Safari 17.2 (specify ALL tested)
**Device**: Desktop 1920×1080 / iPad Pro 11" / iPhone 14 (specify)
**Branch testato**: feature/issue-XXXX (commit SHA: abc12345)

| Stato | Status | Screenshot path | Note |
|---|---|---|---|
| default | ✅ / ❌ / ➖ | path/to/screenshot.png | osservazioni |
| empty | ✅ / ❌ / ➖ | ... | ... |
| loading | ✅ / ❌ / ➖ | ... | ... |
| error | ✅ / ❌ / ➖ | ... | ... |
| offline | ✅ / ❌ / ➖ | ... | ... |

**Issue bloccanti emerse**: <link issue creati per ❌>
**Future improvement opportunity**: <note minor MIN finding>
```

**Criteri self-attestation valid** (no rubber-stamp):
1. ✅ Screenshot allegato per ogni stato (non `➖ N/A`)
2. ✅ Browser + device specified
3. ✅ Commit SHA testato traceable
4. ✅ Issue bloccanti aperte (se ❌) PRIMA del merge
5. ❌ Se compilatore = autore PR → richiede peer review esplicita ("Manual QA cross-validation")

---

## Tooling reference

### Toggle stati in dev (per testabilità)

- **default**: dev server normale
- **empty**: clear DB locale → `make dev-from-snapshot` con fixture vuoto
- **loading**: DevTools Network → "Slow 3G" throttling
- **error**: DevTools Network → block specific endpoint pattern (es. `*friends-activity*`)
- **offline**: DevTools Network → "Offline" checkbox

### Screenshot conservazione

- Path: `docs/for-developers/qa/qa-screenshots/YYYY-MM-DD/<route>-<stato>.png`
- Gitignore: dir `qa-screenshots/` non commit (allegata via PR body link Issue/Slack)
- Compress: WebP preferito per ridurre size

### CI gate disposition (MAJ-P4-3 panel finding)

- **Skeleton E2E asse-b/d/dashboard (4 spec)**: chromium-only + tolerant redirect, NON blocking CI
- **Data-driven E2E full journey** (future P4 wave): blocking gate richiede BE entity seeding infra (vedi follow-up issue separata)
- **Manual QA template (questo doc)**: NON automated, self-attestation in PR body

---

## References

- Spec consolidato: [`2026-06-04-claude-design-alignment-spec-panel-review.md`](../../superpowers/specs/2026-06-04-claude-design-alignment-spec-panel-review.md) MAJ-8 + DEC-3
- Audit D.0: [`2026-06-05-asse-d-v2-shipped-audit.md`](../audits/2026-06-05-asse-d-v2-shipped-audit.md)
- Audit P4 cross-cutting: [`2026-06-05-asse-d-p4-cross-cutting-audit.md`](../audits/2026-06-05-asse-d-p4-cross-cutting-audit.md)
- v2 migration matrix: [`v2-migration-matrix.md`](../frontend/v2-migration-matrix.md)
- Fixtures auth (FE pre-existing): [`apps/web/e2e/_helpers/seedAuthSession.ts`](../../../apps/web/e2e/_helpers/seedAuthSession.ts)
- 4 skeleton asse-b/d/dashboard:
  - [`asse-b-drawer-stack-flow.spec.ts`](../../../apps/web/e2e/asse-b-drawer-stack-flow.spec.ts)
  - [`asse-d-p1-polymorphic-scoring.spec.ts`](../../../apps/web/e2e/asse-d-p1-polymorphic-scoring.spec.ts)
  - [`asse-d-p2-games-discover-hub.spec.ts`](../../../apps/web/e2e/asse-d-p2-games-discover-hub.spec.ts)
  - [`asse-d-p3-onboarding-wizard.spec.ts`](../../../apps/web/e2e/asse-d-p3-onboarding-wizard.spec.ts)
  - [`dashboard-priority-flow.spec.ts`](../../../apps/web/e2e/dashboard-priority-flow.spec.ts)

---

## Changelog

- **2026-06-05**: initial template — 4 route reference (P4 MVP cut), hybrid Crispin 5-state × Adzic G/W/T format, self-attestation MVP pattern (DEC-3 no designer attivo).
