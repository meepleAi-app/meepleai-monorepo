# M3.9 — Secondary Public Pages + Error States (Design v2 Migration)

**Issue**: #503
**Parent branch**: `main-dev`
**Status**: Design approved, awaiting plan
**Date**: 2026-04-24

---

## Goal

Migrate 5 existing secondary public pages + 1 new page to the v2 design system,
introducing consistent `HeroGradient` usage, v2 primitives (`Btn`, `Divider`,
`SettingsList/Row`, `ToggleSwitch`), and adding i18n (IT/EN) to error pages that
currently have hardcoded English strings.

Pages in scope:

1. `/faq` — rewrite with `HeroGradient` + flat sections
2. `/how-it-works` — rewrite with `HeroGradient` + steps/features grid
3. `/cookie-settings` — **new route** for GDPR consent preferences
4. `/404` (`not-found.tsx`) — rewrite with i18n + `HeroGradient`
5. `/500` (`error.tsx`) — rewrite with i18n + `HeroGradient` + reset CTA
6. `/offline` — rewrite with i18n + `HeroGradient` + cached stats

Out of scope (not touched):
- `/cookies` policy page — already migrated to `LegalPageLayout` (v2-compat)
- `/privacy`, `/terms` — same, already v2-compat
- Call-site changes beyond these pages

## Architecture

Heavy refactor (Option C): full v2 primitive adoption where applicable, flat
layout over `Card` wrappers where content is not entity-like. `Accordion`
(from `data-display/accordion`) stays — it is the correct UX pattern for FAQ
and has no v2 equivalent.

New shared utility `lib/cookie-consent.ts` extracts consent read/write logic
from `CookieConsentBanner.tsx` so both the banner and the new `/cookie-settings`
page use the same `localStorage` contract. Banner public contract is preserved;
existing banner tests must pass unchanged.

## Tech Stack

- Next.js 16 App Router
- `react-intl` via `useTranslation` hook
- `sonner ^2.0.7` for save feedback toast (already installed)
- v2 primitives: `@/components/ui/v2/{hero-gradient,btn,divider,settings-list,settings-row,toggle-switch}`
- Icons: `lucide-react` (existing)

## File Structure

```
apps/web/src/
├── app/
│   ├── (public)/
│   │   ├── faq/page.tsx                          # REWRITE
│   │   ├── how-it-works/page.tsx                 # REWRITE
│   │   ├── cookie-settings/page.tsx              # NEW
│   │   └── cookies/page.tsx                      # UNTOUCHED
│   ├── not-found.tsx                             # REWRITE
│   ├── error.tsx                                 # REWRITE
│   └── offline/page.tsx                          # REWRITE
├── lib/
│   ├── cookie-consent.ts                         # NEW (extracted)
│   └── __tests__/cookie-consent.test.ts          # NEW
├── components/legal/
│   └── CookieConsentBanner.tsx                   # REFACTOR (import from lib)
└── locales/
    ├── it.json                                   # EXTEND
    └── en.json                                   # EXTEND
```

## Component Designs

### `HeroGradient` usage notes

The existing `HeroGradient` primitive has this API:

```ts
interface HeroGradientProps {
  title: ReactNode;                          // accepts JSX for eyebrow patterns
  subtitle?: string;
  primaryCta?: { label, href?, onClick? };
  secondaryCta?: { label, href?, onClick? };
  className?: string;
  children?: ReactNode;                      // rendered below CTAs with mt-8
}
```

It does **not** have variants. Error-page eyebrows (404/500) are rendered
inside `title` as `ReactNode`:

```tsx
<HeroGradient
  title={
    <>
      <span className="block text-lg font-mono text-muted-foreground mb-2">404</span>
      {t('pages.errors.notFound.title')}
    </>
  }
  subtitle={t('pages.errors.notFound.subtitle')}
  primaryCta={{ label: t('pages.errors.notFound.homeCta'), href: '/' }}
  secondaryCta={{ label: t('pages.errors.notFound.exploreCta'), href: '/games' }}
/>
```

### 1. `/faq` (rewrite)

Layout:

```
<main>
  <HeroGradient
    title={t('pages.faq.title')}
    subtitle={t('pages.faq.subtitle')}
  />

  {categories.map((cat, idx) => (
    <Fragment key={cat}>
      {idx > 0 && <Divider />}
      <section className="max-w-3xl mx-auto py-8 px-4">
        <h2>{emoji[cat]} {t(`pages.faq.categories.${cat}`)}</h2>
        <Accordion type="single" collapsible>
          {questions.map(q => (
            <AccordionItem value={`${cat}-${q}`}>
              <AccordionTrigger>
                {t(`pages.faq.items.${cat}.${q}.question`)}
              </AccordionTrigger>
              <AccordionContent>
                {t(`pages.faq.items.${cat}.${q}.answer`)}
              </AccordionContent>
            </AccordionItem>
          ))}
        </Accordion>
      </section>
    </Fragment>
  ))}

  <section className="max-w-3xl mx-auto py-12 flex gap-3 justify-center">
    <Btn variant="primary" asChild>
      <Link href="/contact">{t('pages.faq.contactCta')}</Link>
    </Btn>
    <Btn variant="ghost" asChild>
      <Link href="/how-it-works">{t('pages.faq.howItWorksCta')}</Link>
    </Btn>
  </section>
</main>
```

Categories: `['general', 'usage', 'technical', 'account']`. Questions per
category: `['q1', 'q2', 'q3']` (unchanged from current impl). i18n keys
already exist under `pages.faq.*`.

### 2. `/how-it-works` (rewrite)

Layout:

```
<main>
  <HeroGradient
    title={t('pages.howItWorks.title')}
    subtitle={t('pages.howItWorks.subtitle')}
    primaryCta={{ label: t('pages.howItWorks.ctaRegister'), href: '/register' }}
  />

  <section className="max-w-5xl mx-auto py-12 px-4">
    <h2>{t('pages.howItWorks.stepsHeading')}</h2>
    <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mt-8">
      {['step1','step2','step3'].map((s, idx) => (
        <div key={s} className="text-center">
          <div className="text-5xl mb-3">{emoji[s]}</div>
          <div className="text-sm font-mono text-muted-foreground">
            {idx + 1}
          </div>
          <h3>{t(`pages.howItWorks.${s}.title`)}</h3>
          <p>{t(`pages.howItWorks.${s}.description`)}</p>
        </div>
      ))}
    </div>
  </section>

  <Divider />

  <section className="max-w-5xl mx-auto py-12 px-4">
    <h2>{t('pages.howItWorks.featuresHeading')}</h2>
    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-8">
      {['rag','multilingual','pdfUpload','gameLibrary'].map(f => (
        <div key={f}>
          <div className="text-4xl mb-2">{emoji[f]}</div>
          <h3>{t(`pages.howItWorks.features.${f}.title`)}</h3>
          <p>{t(`pages.howItWorks.features.${f}.description`)}</p>
        </div>
      ))}
    </div>
  </section>

  <section className="flex gap-3 justify-center py-8">
    <Btn variant="primary" asChild>
      <Link href="/about">{t('pages.howItWorks.aboutCta')}</Link>
    </Btn>
    <Btn variant="ghost" asChild>
      <Link href="/faq">{t('pages.howItWorks.faqCta')}</Link>
    </Btn>
  </section>
</main>
```

Feature cards use a **custom lightweight pattern** (div + emoji + h3 + p),
NOT `EntityCard`. `EntityCard` is meant for actual entities (game/session/
agent); using it for static marketing features would be a semantic abuse.

### 3. `/cookie-settings` (new)

Layout:

```tsx
'use client';

export default function CookieSettingsPage() {
  const { t } = useTranslation();
  const [analytics, setAnalytics] = useState(false);
  const [functional, setFunctional] = useState(false);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    const stored = getStoredConsent();
    if (stored) {
      setAnalytics(stored.analytics);
      setFunctional(stored.functional);
    }
    setHydrated(true);
  }, []);

  const save = useCallback((next: { analytics: boolean; functional: boolean }) => {
    setStoredConsent(next);
    setAnalytics(next.analytics);
    setFunctional(next.functional);
    toast.success(t('pages.cookieSettings.savedToast'));
    window.dispatchEvent(new CustomEvent('cookie-consent-updated'));
  }, [t]);

  return (
    <main>
      <HeroGradient
        title={t('pages.cookieSettings.title')}
        subtitle={t('pages.cookieSettings.subtitle')}
      />

      <section className="max-w-2xl mx-auto py-8 px-4">
        <SettingsList>
          <SettingsRow
            label={t('pages.cookieSettings.categories.essential.label')}
            description={t('pages.cookieSettings.categories.essential.description')}
            control={<ToggleSwitch checked={true} disabled onChange={() => {}} />}
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.analytics.label')}
            description={t('pages.cookieSettings.categories.analytics.description')}
            control={
              <ToggleSwitch
                checked={analytics}
                onChange={setAnalytics}
                disabled={!hydrated}
              />
            }
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.functional.label')}
            description={t('pages.cookieSettings.categories.functional.description')}
            control={
              <ToggleSwitch
                checked={functional}
                onChange={setFunctional}
                disabled={!hydrated}
              />
            }
          />
        </SettingsList>

        <div className="flex flex-col sm:flex-row gap-3 mt-6">
          <Btn
            variant="primary"
            onClick={() => save({ analytics, functional })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.save')}
          </Btn>
          <Btn
            variant="ghost"
            onClick={() => save({ analytics: true, functional: true })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.acceptAll')}
          </Btn>
          <Btn
            variant="ghost"
            onClick={() => save({ analytics: false, functional: false })}
            disabled={!hydrated}
          >
            {t('pages.cookieSettings.actions.rejectAll')}
          </Btn>
        </div>

        <div className="mt-8 text-center">
          <Link href="/cookies" className="text-sm underline">
            {t('pages.cookieSettings.policyLink')}
          </Link>
        </div>
      </section>
    </main>
  );
}
```

**Hydration guard**: `hydrated` flag prevents a mismatch between SSR render
(toggles at `false`) and client hydration reading localStorage. Actions are
`disabled` until the first `useEffect` runs.

**`/cookies` policy page reciprocal link**: add a "Gestisci preferenze" link
at the bottom of the policy content pointing to `/cookie-settings`. This is
the **only** modification to `/cookies` — the `LegalPageLayout` and all
markdown content remain untouched.

### 4. `/404` (`not-found.tsx`) rewrite

```tsx
export const dynamic = 'force-dynamic'; // keep if DOMMatrix bug still present

export default function NotFound() {
  const { t } = useTranslation();
  return (
    <main>
      <HeroGradient
        title={
          <>
            <span className="block text-xl font-mono text-muted-foreground mb-2">
              {t('pages.errors.notFound.eyebrow')}
            </span>
            {t('pages.errors.notFound.title')}
          </>
        }
        subtitle={t('pages.errors.notFound.subtitle')}
        primaryCta={{ label: t('pages.errors.notFound.homeCta'), href: '/' }}
        secondaryCta={{ label: t('pages.errors.notFound.exploreCta'), href: '/games' }}
      />
    </main>
  );
}
```

**If removal of `dynamic = 'force-dynamic'` still triggers the DOMMatrix
build failure** (known Next.js 16 issue with client imports in `not-found.tsx`),
the directive stays. Implementation task will verify via `pnpm build`.

### 5. `/500` (`error.tsx`) rewrite

```tsx
'use client';

export default function GlobalError({ error, reset }: {
  error: Error & { digest?: string };
  reset: () => void;
}) {
  const { t } = useTranslation();

  useEffect(() => {
    console.error(error);
  }, [error]);

  return (
    <main>
      <HeroGradient
        title={
          <>
            <span className="block text-xl font-mono text-muted-foreground mb-2">
              {t('pages.errors.serverError.eyebrow')}
            </span>
            {t('pages.errors.serverError.title')}
          </>
        }
        subtitle={t('pages.errors.serverError.subtitle')}
        primaryCta={{ label: t('pages.errors.serverError.retryCta'), onClick: reset }}
        secondaryCta={{ label: t('pages.errors.serverError.homeCta'), href: '/' }}
      />
      {process.env.NODE_ENV !== 'production' && error.digest && (
        <div className="text-center text-xs text-muted-foreground mt-4">
          {t('pages.errors.serverError.digestLabel')}: {error.digest}
        </div>
      )}
    </main>
  );
}
```

### 6. `/offline` rewrite

```tsx
'use client';

export default function OfflinePage() {
  const { t } = useTranslation();
  const { storageStats, isOnline } = usePWA();

  useEffect(() => {
    if (isOnline) window.history.back();
  }, [isOnline]);

  const handleRetry = () => window.location.reload();

  return (
    <main>
      <HeroGradient
        title={
          <span className="inline-flex items-center gap-3">
            <WifiOff className="w-8 h-8" aria-hidden="true" />
            {t('pages.errors.offline.title')}
          </span>
        }
        subtitle={t('pages.errors.offline.subtitle')}
        primaryCta={{
          label: t('pages.errors.offline.retryCta'),
          onClick: handleRetry,
        }}
        secondaryCta={{ label: t('pages.errors.offline.homeCta'), href: '/' }}
      />

      {storageStats && (
        <>
          <Divider />
          <section className="max-w-md mx-auto py-8 px-4">
            <h2 className="text-center">
              {t('pages.errors.offline.statsTitle')}
            </h2>
            <div className="grid grid-cols-3 gap-4 mt-4 text-center">
              <Stat
                label={t('pages.errors.offline.sessionsLabel')}
                value={storageStats.sessions}
              />
              <Stat
                label={t('pages.errors.offline.cachedGamesLabel')}
                value={storageStats.cachedGames}
              />
              <Stat
                label={t('pages.errors.offline.pendingLabel')}
                value={storageStats.pendingActions}
              />
            </div>
          </section>
        </>
      )}

      <div
        role="status"
        aria-live="polite"
        aria-atomic="true"
        className="sr-only"
      >
        {isOnline ? t('pages.errors.offline.onlineStatus') : t('pages.errors.offline.offlineStatus')}
      </div>
    </main>
  );
}
```

`Stat` is a tiny inline component: `<div><span className="text-2xl font-bold">{value}</span><span className="text-xs">{label}</span></div>`. No new shared component.

### `lib/cookie-consent.ts` (new)

```ts
export const CONSENT_KEY = 'meepleai-cookie-consent';
export const CONSENT_VERSION = '1.0';

export interface CookieConsent {
  version: string;
  essential: true;
  analytics: boolean;
  functional: boolean;
  timestamp: string;
}

export function getStoredConsent(): CookieConsent | null {
  if (typeof window === 'undefined') return null;
  try {
    const stored = localStorage.getItem(CONSENT_KEY);
    if (!stored) return null;
    const consent = JSON.parse(stored) as CookieConsent;
    if (consent.version !== CONSENT_VERSION) return null;
    return consent;
  } catch {
    return null;
  }
}

export function setStoredConsent(
  next: { analytics: boolean; functional: boolean }
): void {
  if (typeof window === 'undefined') return;
  const consent: CookieConsent = {
    version: CONSENT_VERSION,
    essential: true,
    analytics: next.analytics,
    functional: next.functional,
    timestamp: new Date().toISOString(),
  };
  localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));
}

export function clearStoredConsent(): void {
  if (typeof window === 'undefined') return;
  localStorage.removeItem(CONSENT_KEY);
}
```

`CookieConsentBanner.tsx` is refactored to import `CONSENT_KEY`,
`CONSENT_VERSION`, `getStoredConsent`, and `setStoredConsent` from this
module. Its public component contract (props, exported types, rendered DOM)
is unchanged — all existing `CookieConsentBanner` tests must pass unchanged.

## i18n Keys

Extended in `apps/web/src/locales/{it,en}.json` under `pages`:

```json
{
  "pages": {
    "errors": {
      "notFound": {
        "eyebrow": "404",
        "title": "Pagina non trovata",
        "subtitle": "La pagina che stai cercando non esiste o è stata spostata.",
        "homeCta": "Torna alla home",
        "exploreCta": "Esplora i giochi"
      },
      "serverError": {
        "eyebrow": "500",
        "title": "Qualcosa è andato storto",
        "subtitle": "Si è verificato un errore imprevisto. Riprova o torna alla home.",
        "retryCta": "Riprova",
        "homeCta": "Torna alla home",
        "digestLabel": "ID errore"
      },
      "offline": {
        "title": "Sei offline",
        "subtitle": "Non preoccuparti, puoi ancora accedere ai contenuti salvati in cache.",
        "retryCta": "Riprova connessione",
        "homeCta": "Torna alla home",
        "statsTitle": "Contenuti disponibili offline",
        "sessionsLabel": "Sessioni",
        "cachedGamesLabel": "Giochi in cache",
        "pendingLabel": "Azioni in attesa",
        "onlineStatus": "Connessione ripristinata",
        "offlineStatus": "Connessione assente"
      }
    },
    "cookieSettings": {
      "title": "Preferenze cookie",
      "subtitle": "Gestisci il tuo consenso ai cookie. Puoi modificare queste impostazioni in qualsiasi momento.",
      "categories": {
        "essential": {
          "label": "Cookie essenziali",
          "description": "Necessari per il funzionamento del sito (sessione, preferenze di base). Non possono essere disattivati."
        },
        "analytics": {
          "label": "Cookie analitici",
          "description": "Ci aiutano a capire come usi il sito in forma aggregata e anonima."
        },
        "functional": {
          "label": "Cookie funzionali",
          "description": "Migliorano l'esperienza ricordando le tue scelte (tema, lingua)."
        }
      },
      "actions": {
        "save": "Salva preferenze",
        "acceptAll": "Accetta tutti",
        "rejectAll": "Rifiuta opzionali"
      },
      "savedToast": "Preferenze salvate",
      "policyLink": "Leggi la cookie policy completa"
    }
  }
}
```

EN mirror uses direct translations with the same structural shape.

Existing `pages.faq.*` and `pages.howItWorks.*` keys are **reused unchanged**
where possible. If the rewrite references a key not currently present (e.g.
`pages.howItWorks.stepsHeading`, `pages.howItWorks.featuresHeading`), those
keys are added during implementation — this is expected and is part of task
scope, not a new concern.

## Data Flow

### Cookie consent lifecycle

```
┌──────────────────────┐         ┌──────────────────────┐
│ CookieConsentBanner  │         │  /cookie-settings    │
│ (first visit)        │         │  (explicit settings) │
└────────┬─────────────┘         └──────────┬───────────┘
         │                                   │
         │ getStoredConsent() / setStoredConsent()
         ▼                                   ▼
    ┌────────────────────────────────────────────┐
    │     lib/cookie-consent.ts (source of truth) │
    └────────────────────────────────────────────┘
                         │
                         ▼
                ┌──────────────────┐
                │ localStorage     │
                │ meepleai-cookie- │
                │ consent          │
                └──────────────────┘
```

The `cookie-consent-updated` CustomEvent is dispatched from both the banner
and `/cookie-settings` after `setStoredConsent`, allowing either UI to react
(e.g. the banner auto-hides if it was visible and the user saves from the
settings page).

## Error Handling

- `getStoredConsent`: returns `null` on any parse/localStorage error (existing behavior)
- `setStoredConsent`: no-op on SSR (no `window`); storage quota errors propagate (matches current behavior, not masking)
- `error.tsx`: logs error to console in `useEffect`; shows `error.digest` only in non-production
- `not-found.tsx`: static render, no error paths
- `offline/page.tsx`: if `storageStats` is unavailable (PWA hook not ready), stats section simply doesn't render

## Testing Strategy

### Unit tests (Vitest + RTL)

| File | Scenarios |
|------|-----------|
| `lib/__tests__/cookie-consent.test.ts` | getStored empty / valid / invalid version / malformed JSON; setStored writes correct shape; clearStored removes key; SSR safety (typeof window undefined via mock) |
| `app/(public)/faq/__tests__/page.test.tsx` | renders `HeroGradient`, 4 category headings, accordion interaction (expand/collapse), footer CTAs link to correct hrefs |
| `app/(public)/how-it-works/__tests__/page.test.tsx` | renders hero with primary CTA to `/register`, 3 steps, 4 features, footer CTAs |
| `app/(public)/cookie-settings/__tests__/page.test.tsx` | **7 scenarios**: (1) mount w/o consent → toggles false, (2) mount w/ stored consent → toggles reflect stored values, (3) toggle analytics changes state, (4) save writes to localStorage w/ correct shape + emits event, (5) acceptAll sets both true and saves, (6) rejectAll sets both false and saves, (7) essential toggle is disabled+checked |
| `app/__tests__/not-found.test.tsx` | renders localized title + subtitle, both CTAs present with correct hrefs |
| `app/__tests__/error.test.tsx` | renders localized title + subtitle, retry button calls `reset`, console.error called w/ error object, digest shown in dev only |
| `app/offline/__tests__/page.test.tsx` | renders localized content, retry triggers reload, stats render when provided, auto-redirect on `isOnline` true, `usePWA` mocked |

### Regression coverage

- `CookieConsentBanner` existing tests **must pass unchanged** after the
  refactor that extracts logic to `lib/cookie-consent.ts`. If any break, the
  public contract was violated — bloccante.

### Manual QA

1. Navigate to `/cookie-settings`, toggle analytics on, click "Salva
   preferenze", reload page → toggle still on ✓
2. Clear localStorage, navigate to `/` → banner appears; click "Accept" →
   banner dismisses; navigate to `/cookie-settings` → analytics toggle is
   on ✓
3. Navigate to `/404-does-not-exist` → localized 404 page ✓
4. Force a runtime error in dev → error boundary shows localized 500 page;
   click "Riprova" → page re-renders ✓
5. Toggle browser offline, navigate to `/offline` → localized offline page
   with stats ✓
6. Toggle browser online → auto-redirects back ✓

### Acceptance Criteria (DoD)

1. All 6 target pages adopt v2 primitives **where applicable**: `HeroGradient` on all 6; `Btn` wherever CTAs exist; `Divider` between sections on `/faq`, `/how-it-works`, `/offline`; `SettingsList/Row` + `ToggleSwitch` on `/cookie-settings`
2. No hardcoded English strings remain in `not-found.tsx`, `error.tsx`, `offline/page.tsx` — all via `useTranslation`
3. `/cookie-settings` route works end-to-end: load stored consent → toggle → save → persists across reload
4. Existing `CookieConsentBanner` tests pass unchanged after refactor
5. `pnpm typecheck` clean
6. `pnpm lint` clean (including `local/no-dual-connection-source` and dev-tools isolation)
7. `pnpm test` — all new + existing tests green; no flaky tests introduced
8. Bundle size regression ≤ 3% vs current baseline; if exceeded, justify (new page + i18n keys)
9. CI green: Frontend Build & Test, E2E Critical Paths, dev-tools isolation, GitGuardian
10. `/cookies` policy page has zero diffs except the new "Gestisci preferenze" link to `/cookie-settings`
11. Reciprocal linking: `/cookies` → `/cookie-settings` and `/cookie-settings` → `/cookies` both present

## Risks & Mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| `not-found.tsx` DOMMatrix bug returns after directive change | Medium | Keep `export const dynamic = 'force-dynamic'` if build fails without it |
| `HeroGradient` layout doesn't accommodate ReactNode eyebrow well | Low | Eyebrow fits in title via `<span className="block ...">`; if visual regression, keep eyebrow outside via `className` override |
| Existing `CookieConsentBanner` tests rely on inline logic internals | Medium | Extract lib with identical behavior; run banner tests first to verify; if fails, adjust lib API to match |
| SSR hydration mismatch on `/cookie-settings` | Medium | Hydration guard (`hydrated` flag) + disable actions until first `useEffect` runs |
| Bundle size +new page exceeds 3% | Low | New page is small (~2KB); i18n keys are text-only; measured during implementation |
| `sonner` not configured globally | Low | Already installed (v2.0.7); add `<Toaster />` to root layout if missing (single-line addition) |

## Out of Scope

- Redesigning `/cookies`, `/privacy`, `/terms` (already `LegalPageLayout` v2-compat)
- Adding i18n to other existing pages (only the 3 error pages in this PR)
- Cookie consent backend sync (localStorage only, matches current banner behavior)
- Removing `export const dynamic = 'force-dynamic'` from `not-found.tsx` if it causes build failure
- Creating a v2 `Accordion` primitive (FAQ continues to use `data-display/accordion`)
- Migrating call-sites beyond these 6 pages
- E2E tests in Playwright — unit tests are MUST, E2E is optional time-permitting

## References

- Issue #503 — Secondary public pages + Error states
- PR #516 — LegalPageLayout v2 migration (context for `/cookies` policy)
- PR #552 — Step 2 ConnectionChip migration (sibling redesign v2 work)
- `apps/web/src/components/ui/v2/hero-gradient/hero-gradient.tsx` — primitive API
- `apps/web/src/components/legal/CookieConsentBanner.tsx` — source of extracted logic
- `apps/web/src/hooks/usePWA.ts` — hook consumed by `/offline`
- `apps/web/src/hooks/useTranslation.ts` — react-intl wrapper
