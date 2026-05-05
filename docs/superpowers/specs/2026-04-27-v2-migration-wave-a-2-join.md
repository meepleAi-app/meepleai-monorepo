# V2 Migration · Wave A.2 — `/join` Waitlist Alpha Spec

**Issue**: #589 (parent: #579 Wave A, umbrella: #578)
**Branch**: `feature/issue-589-migration-wave-a-2-join` (parent: `main-dev`)
**Mockup**: `admin-mockups/design_files/sp3-join.jsx` (892 LOC)
**Visual baseline**: `apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/sp3-join-mockup-baseline-{desktop,mobile}-linux.png` (PR #575)
**Companion exec plan**: `2026-04-27-v2-migration-phase1-execution.md` §3.2
**Pilot reference**: `2026-04-27-v2-migration-wave-a-1-faq.md` (PR #586 MERGED 2026-04-27)
**Status**: DRAFT 2026-04-27

---

## 1. Goals

1. **Creare** route `/join` v2 (net-new public landing page) seguendo `sp3-join` mockup 1:1.
2. **Riusare workflow PILOTA** validato in Wave A.1: visual-migrated + v2-states + hybrid masking.
3. **Estrarre componenti v2 riusabili** (`GamePreferenceSelect`, `FeatureMiniCard`, `JoinHero`) — il primo è un combobox listbox accessibile, riutilizzabile in eventuali form preferenze post-alpha.
4. **Backend endpoint waitlist** funzionante (creazione tabella, validator, integration test), così l'alpha può raccogliere iscrizioni reali.

## 2. Non-goals

- Migrazione legacy `/join/[inviteToken]` (Wave A.5 separato — invite redemption).
- Migrazione legacy `/join/session/[code]` (fuori Phase 1 — auth-only invite flow).
- Email de-duplication advanced, bot detection (post-alpha).
- Real-time queue position polling (post-alpha; success state mostra position calcolata server-side al submit).
- Refactor `Banner` legacy (riusa esistente `Banner` v2 da auth-flow se compatibile, altrimenti crea wrapper minimal).
- Newsletter consent decoupled storage (per Alpha basta colonna `newsletterOptIn` su `WaitlistEntries`; post-alpha si valuta segregazione consenti GDPR).

## 3. Architecture

### 3.1 File map

| Tipo | Path | Status |
|------|------|--------|
| Route page | `apps/web/src/app/(public)/join/page.tsx` | **Create** |
| Route metadata | `apps/web/src/app/(public)/join/page.tsx` (export `metadata`) | **Create** (IT title, OG image) |
| Route test | `apps/web/src/app/(public)/join/__tests__/page.test.tsx` | **Create** |
| Static data | `apps/web/src/lib/join/games.ts` | **Create** (`TOP_GAMES`, `ALPHA_FEATURES`) |
| Submit hook | `apps/web/src/hooks/useWaitlistSubmit.ts` | **Create** (TanStack Query mutation) |
| Component | `apps/web/src/components/ui/v2/join/join-hero.tsx` | **Create** (BrandMark + AlphaPill + tagline + 3 features) |
| Component | `apps/web/src/components/ui/v2/join/join-form.tsx` | **Create** (5-state finite state machine) |
| Component | `apps/web/src/components/ui/v2/join/game-preference-select.tsx` | **Create** (combobox listbox WAI-ARIA) |
| Component | `apps/web/src/components/ui/v2/join/feature-mini-card.tsx` | **Create** |
| Component | `apps/web/src/components/ui/v2/join/alpha-pill.tsx` | **Create** (riusa pattern già esistente in #568 mockup, ora cristallizzato) |
| Component | `apps/web/src/components/ui/v2/join/join-success-card.tsx` | **Create** (success state isolato per testabilità) |
| Component index | `apps/web/src/components/ui/v2/join/index.ts` | **Create** |
| i18n IT | `apps/web/src/locales/it.json` § `pages.join` | **Add** |
| i18n EN | `apps/web/src/locales/en.json` § `pages.join` | **Add** |
| API client | `apps/web/src/lib/api/waitlist.ts` | **Create** (`postWaitlistEntry`) |
| Visual test | `apps/web/e2e/visual-migrated/sp3-join.spec.ts` | **Create** |
| State test | `apps/web/e2e/v2-states/join.spec.ts` | **Create** (5 stati × 2 viewport = 10 PNG) |
| **Backend** route | `apps/api/src/Api/BoundedContexts/Authentication/Endpoints/WaitlistEndpoints.cs` *or* `MarketingOutreach/...` (TBD §3.5) | **Create** |
| **Backend** command | `BoundedContexts/.../Application/Commands/JoinWaitlistCommand.cs` + handler + validator | **Create** |
| **Backend** entity | `BoundedContexts/.../Domain/Entities/WaitlistEntry.cs` | **Create** |
| **Backend** migration | `BoundedContexts/.../Infrastructure/Persistence/Migrations/{ts}_AddWaitlistEntries.cs` | **Create** |
| **Backend** test | `tests/Api.Tests/.../JoinWaitlistCommandHandlerTests.cs` + integration | **Create** |

### 3.2 Component API

#### `GamePreferenceSelect`

```ts
export interface GamePreferenceSelectProps {
  readonly value: string;                     // game id (e.g. 'g-azul') or '' if unselected
  readonly onChange: (id: string) => void;
  readonly otherText: string;                 // controlled text for "Altro" branch
  readonly onOtherText: (next: string) => void;
  readonly error?: string;                    // i18n string from caller
  readonly games: readonly GamePreference[];  // injected (not hardcoded)
  readonly labels: {
    readonly buttonLabel: string;             // "Quale gioco vorresti un agente per?"
    readonly placeholder: string;             // "Scegli un gioco…"
    readonly otherPlaceholder: string;        // "Es. Terraforming Mars"
    readonly listboxAriaLabel: string;        // "Lista giochi"
  };
  readonly className?: string;
}
```

**A11y contract (binding)**:
- Trigger button: `aria-haspopup="listbox"` + `aria-expanded={open}` + `aria-controls={listboxId}` + visible label via `<label htmlFor>`.
- Popover: `role="listbox"` + `aria-label` + `aria-activedescendant={`option-${activeIdx}`}`.
- Each option: `role="option"` + stable `id` + `aria-selected`.
- **Keyboard** (REQUIRED): Enter/Space/ArrowDown opens; ArrowDown/Up move active descendant; Enter selects + closes + focus returns to button; Escape closes + focus returns; Tab closes + moves focus naturally (not trap-focus, popover dismisses).
- "Altro" row: `borderTop` separator + monospace font + when selected, free-text input below auto-focused.
- Click outside via `mousedown` listener; `useEffect` cleanup.

#### `JoinForm`

```ts
export type JoinFormState = 'default' | 'submitting' | 'success' | 'error' | 'already-on-list';

export interface JoinFormProps {
  readonly stateOverride?: JoinFormState;   // ONLY for visual regression / Storybook; production MUST omit
  readonly onSubmitSuccess?: (result: WaitlistResult) => void;
}

export interface WaitlistResult {
  readonly position: number;
  readonly estimatedWeeks: number;
}
```

**FSM transitions**:
```
default ─submit→ submitting ─{200}→ success
                              ─{409}→ already-on-list (preserves form data)
                              ─{4xx,5xx,timeout}→ error (preserves form data)
already-on-list ─submit→ submitting (re-attempt with corrected email)
error ─submit→ submitting (re-attempt)
success ─"Iscrivi un'altra"→ default (reset form except newsletter=true)
```

**GDPR Art. 7 + EDPB binding**: `newsletterOptIn` initial state MUST be `false`. After success-reset for second submission, copy mockup behavior `news: true` is **rejected** for prod path — keep `false`. Mockup copy is a UX nicety we deliberately deviate on for compliance correctness.

#### `JoinHero`

```ts
export interface JoinHeroProps {
  readonly compact?: boolean;                 // mobile = true (smaller BrandMark, fewer features visible)
  readonly features: readonly AlphaFeature[]; // 3 entries (kb / agent / session)
}
```

#### `FeatureMiniCard`

```ts
export interface FeatureMiniCardProps {
  readonly entity: 'kb' | 'agent' | 'session' | 'toolkit' | 'player' | 'game' | 'event';
  readonly emoji: string;
  readonly title: string;
  readonly description: string;
}
```

Color via `entityHsl(entity, 0.14)` background + `entityHsl(entity)` accent border-left 3px. Riusa helper Phase 0 PR #577.

#### `JoinSuccessCard`

```ts
export interface JoinSuccessCardProps {
  readonly position: number;
  readonly estimatedWeeks: number;
  readonly onResetClick: () => void;
}
```

### 3.3 Data shape

```ts
// lib/join/games.ts
export interface GamePreference {
  readonly id: string;            // 'g-azul'..'g-other'
  readonly label: string;         // i18n-resolved at call site
  readonly emoji: string;
}

export interface AlphaFeature {
  readonly entity: 'kb' | 'agent' | 'session';
  readonly emoji: string;
  readonly titleKey: string;      // i18n key
  readonly descKey: string;       // i18n key
}

export const TOP_GAMES: readonly GamePreference[] = [
  { id: 'g-azul',        label: 'Azul',                emoji: '🔷' },
  { id: 'g-catan',       label: 'I Coloni di Catan',   emoji: '🌾' },
  { id: 'g-wingspan',    label: 'Wingspan',            emoji: '🦜' },
  { id: 'g-brass',       label: 'Brass: Birmingham',   emoji: '🏭' },
  { id: 'g-gloomhaven',  label: 'Gloomhaven',          emoji: '⚔️' },
  { id: 'g-arknova',     label: 'Ark Nova',            emoji: '🦒' },
  { id: 'g-spirit',      label: 'Spirit Island',       emoji: '🌋' },
  { id: 'g-7wonders',    label: '7 Wonders Duel',      emoji: '🏛️' },
  { id: 'g-carcassonne', label: 'Carcassonne',         emoji: '🏰' },
  { id: 'g-ticket',      label: 'Ticket to Ride',      emoji: '🚂' },
  { id: 'g-other',       label: 'Altro…',              emoji: '✦' },
] as const;
```

`label` qui è display fallback IT — preferenza per i18n via `t('pages.join.games.azul')` ecc. al call-site (analogo pattern Wave A.1 `readMoreLabel`). Per Alpha, label statici IT sono accettabili se `messages/en.json` provvede override; rivisitare se locale switcher lo richiede.

### 3.4 i18n keys (binding)

```jsonc
"pages": {
  "join": {
    "metadata": { "title": "...", "description": "..." },
    "hero": {
      "alphaPill": "Alpha privata",
      "tagline": "...",
      "subTagline": "..."
    },
    "form": {
      "emailLabel": "Email",
      "emailPlaceholder": "tu@email.com",
      "emailErrorInvalid": "Email non valida",
      "nameLabel": "Nome",
      "nameOptional": "(opzionale)",
      "namePlaceholder": "Come ti chiami?",
      "nameHint": "Se ce lo dici, ti chiamiamo per nome nelle email",
      "gameLabel": "Quale gioco vorresti un agente per?",
      "gamePlaceholder": "Scegli un gioco…",
      "gameOtherPlaceholder": "Es. Terraforming Mars",
      "gameErrorRequired": "Seleziona un gioco",
      "gameErrorOtherTooShort": "Specifica il gioco",
      "newsletterLabel": "Voglio ricevere aggiornamenti via email...",
      "submitDefault": "Entra in waitlist →",
      "submitting": "Invio in corso…",
      "alreadyHaveInvite": "Già hai un invito?",
      "loginLink": "Accedi"
    },
    "banners": {
      "alreadyOnList": "Questa email è già in lista — sei alla posizione #{position}. Stima ingresso: {weeks} settimane.",
      "errorGeneric": "Qualcosa è andato storto. Controlla la connessione e riprova fra qualche secondo.",
      "errorEmailField": "Errore di rete",
      "alreadyEmailField": "Già registrata"
    },
    "success": {
      "heading": "Sei in lista!",
      "subText": "Ti contattiamo entro 2–3 settimane.",
      "subTextSecond": "Intanto controlla la mail per la conferma.",
      "positionLabel": "La tua posizione",
      "positionEstimate": "stima {weeks} settimane",
      "resetCta": "Iscrivi un'altra email"
    },
    "features": {
      "kb": { "title": "Library smart", "desc": "Aggiungi un gioco e l'AI indicizza il manuale in 60 secondi." },
      "agent": { "title": "Agenti AI", "desc": "Un esperto dedicato per ogni gioco. Cita la pagina del PDF." },
      "session": { "title": "Session live", "desc": "Timer, punteggio, storico — tutto durante la partita." }
    },
    "games": {
      "azul": "Azul", "catan": "I Coloni di Catan", "wingspan": "Wingspan",
      "brass": "Brass: Birmingham", "gloomhaven": "Gloomhaven", "arknova": "Ark Nova",
      "spirit": "Spirit Island", "sevenWonders": "7 Wonders Duel",
      "carcassonne": "Carcassonne", "ticket": "Ticket to Ride", "other": "Altro…"
    }
  }
}
```

### 3.5 Backend choice (TBD — recommend Authentication BC)

**Option A — `Authentication` BC** (RECOMMENDED for Alpha):
- Pros: Already manages user signup/login flows; waitlist is entry funnel into auth.
- Cons: Stretches BC slightly; waitlist entries are pre-account.

**Option B — new `MarketingOutreach` BC**:
- Pros: Clean separation; future-proof for newsletter, drip campaigns.
- Cons: Overkill for single endpoint at Alpha; adds DI registration overhead.

**Decision**: Authentication BC for Alpha. Migration to MarketingOutreach BC deferred until 2nd marketing endpoint added (YAGNI).

#### Endpoint contract

```
POST /api/v1/waitlist
Content-Type: application/json
{
  "email": "string (RFC 5322)",
  "name": "string | null",
  "gamePreferenceId": "string (in TOP_GAMES.id allowlist)",
  "gamePreferenceOther": "string | null (required iff id == 'g-other', min 2 chars)",
  "newsletterOptIn": "boolean"
}

200 OK
{ "position": 247, "estimatedWeeks": 2 }

409 Conflict
{ "error": "ALREADY_ON_LIST", "position": 247, "estimatedWeeks": 2 }

400 Bad Request
{ "error": "VALIDATION", "fieldErrors": { "email": "...", ... } }

429 Too Many Requests   (rate limit per IP, optional Phase 1)
```

#### Entity

```csharp
public class WaitlistEntry
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }              // unique constraint, normalized lower
    public string? Name { get; private set; }
    public string GamePreferenceId { get; private set; }
    public string? GamePreferenceOther { get; private set; }
    public bool NewsletterOptIn { get; private set; }
    public int Position { get; private set; }              // monotonic, assigned on insert
    public DateTime CreatedAt { get; private set; }
    public DateTime? ContactedAt { get; private set; }     // null until ops reaches out

    public static WaitlistEntry Create(string email, string? name, string gameId,
        string? gameOther, bool optIn, int position) { /* factory + validation */ }
}
```

`Position` calcolato server-side via `SELECT COUNT(*) + 1 FROM WaitlistEntries WHERE NOT IsContacted` in transaction (race safe via row-level lock o sequence). `EstimatedWeeks` derivato da position via formula `Math.Ceiling(position / 100)` (calibrabile post-alpha).

#### Validator (FluentValidation)

```csharp
RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
RuleFor(x => x.GamePreferenceId).NotEmpty().Must(id => AllowedGameIds.Contains(id));
When(x => x.GamePreferenceId == "g-other", () =>
    RuleFor(x => x.GamePreferenceOther).NotEmpty().MinimumLength(2).MaximumLength(80));
RuleFor(x => x.Name).MaximumLength(80);
// NewsletterOptIn intentionally not validated — boolean any value acceptable
```

## 4. Test plan (TDD red phase)

### 4.1 Visual contract (RED first)

`e2e/visual-migrated/sp3-join.spec.ts`:
- 1 test desktop @1440 width, 1 test mobile @375 width.
- Renders `/join` against mockup baseline `sp3-join-mockup-baseline-{viewport}-linux.png`.
- Mask zones via `data-dynamic` (none expected — page is static; only AlphaPill animation pulse if any).
- Stub backend via Playwright route: `POST /api/v1/waitlist` → never called in default state.

### 4.2 State coverage (RED first)

`e2e/v2-states/join.spec.ts`:
- 5 states × 2 viewports = 10 PNG.
- States: `default`, `submitting`, `success`, `error`, `already-on-list`.
- Drive via component-level `stateOverride` prop on `JoinForm` exposed in test-only route page `/test/join-state/[state]` *or* via Playwright route mocking + form fill.
- Decision: prefer **route mocking + form fill** for `submitting/error/already-on-list` (real flow); use `stateOverride` for `success` (avoids backend mock + position randomness).

### 4.3 Unit tests

- `game-preference-select.test.tsx`: keyboard nav (12 cases), aria attributes (6), click outside, "Altro" reveal, error display.
- `join-form.test.tsx`: FSM transitions (5), error propagation, GDPR opt-in NEVER pre-flagged (regression test), reset behavior after success.
- `useWaitlistSubmit.test.ts`: 200/409/4xx/5xx/timeout mapping to FSM state.
- `join-hero.test.tsx`: compact mode prop affects feature card count.
- `feature-mini-card.test.tsx`: entity → border-left color via `entityHsl()`.
- `join-success-card.test.tsx`: position renders, reset button calls callback.
- `lib/join/games.test.ts`: TOP_GAMES contains exactly 11 entries with 'g-other' last.

### 4.4 Backend tests

- `JoinWaitlistCommandHandlerTests.cs`: factory creates entity, position monotonic increment, duplicate email returns conflict result.
- `WaitlistEndpoints.IntegrationTests.cs` (Testcontainers PostgreSQL): full flow 200 / 409 / 400 / concurrent insert race (2 parallel POST same email — exactly one 200 + one 409).

## 5. Acceptance criteria (DoD)

- [ ] visual-migrated CI green (route prod ≈ mockup baseline within Playwright tolerance)
- [ ] 10 v2-states baselines committed (Linux x86-64)
- [ ] `pnpm test` 100% pass; new component coverage ≥ 90%
- [ ] `dotnet test --filter "FullyQualifiedName~Waitlist"` 100% pass
- [ ] axe-core: zero violations on default/error/already/success states
- [ ] Bundle delta < +30 KB (target: ~20 KB form + listbox)
- [ ] WAI-ARIA combobox spec compliance verified manually + via test
- [ ] GDPR opt-in defaults `false` — explicit unit + e2e regression
- [ ] PR squash body includes `Closes #589`

## 6. Risks & mitigations

| # | Risk | Mitigation |
|---|------|-----------|
| 1 | Backend race on `Position` assignment | Use serializable transaction or DB sequence; integration test with parallel inserts |
| 2 | Email dedup case sensitivity | Normalize to lowercase before unique constraint check |
| 3 | Mockup uses `setTimeout` for 'submitting' fake delay; real net latency may be < 100 ms breaking visual baseline for `submitting` PNG | v2-states test forces `submitting` via Playwright route delay (`route.fulfill` with `setTimeout`); not real network |
| 4 | `JoinForm` exposes `stateOverride` to production bundle (security/UX risk if exploited) | Restrict to `process.env.NODE_ENV !== 'production'` runtime check; e2e test in production NODE_ENV proves prop is no-op |
| 5 | `(public)/layout.tsx` may inject header that breaks fullscreen mockup | Audit layout; if header always present, accept design deviation OR add `disableHeader` boolean from page metadata |
| 6 | i18n EN strings not yet vetted by native speaker | Alpha is IT-first; EN locale is best-effort; add `// TODO: review EN copy pre-public-launch` comment in en.json |

## 7. Out of scope (explicit)

- `/join/[inviteToken]` (Wave A.5)
- `/join/session/[code]` (auth flow, separate stream)
- Admin tooling to view/export waitlist (post-alpha)
- Email confirmation double-opt-in (post-alpha)
- Webhook to n8n for ops notification (post-alpha)
- Position recalculation when entries are contacted (post-alpha — for Alpha, position is creation-order absolute)

## 8. Sequencing

1. **Spec review** (this doc) — user gate ✋
2. **Backend** scaffolding (entity + migration + endpoint + tests) — RED→GREEN
3. **Frontend lib** (`lib/join/games.ts`, `useWaitlistSubmit`, API client)
4. **Frontend components** (GamePreferenceSelect first, lowest deps; then form, hero, success card)
5. **Frontend route** (`(public)/join/page.tsx` assembles components)
6. **TDD red** (visual-migrated + v2-states specs) — verify they FAIL before impl
7. **Bootstrap baselines** for v2-states (10 PNG via `workflow_dispatch`)
8. **PR open** — Migrated Routes Baseline workflow validates
9. **Code review fixes** if needed
10. **Squash merge** — `--delete-branch` + `Closes #589` in body

---

**End of DRAFT**. Awaits user approval before TDD red phase begins.
