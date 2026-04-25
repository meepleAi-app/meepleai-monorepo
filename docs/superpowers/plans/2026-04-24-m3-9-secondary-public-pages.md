# M3.9 — Secondary Public Pages + Error States Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Migrate 5 existing secondary public pages (`/faq`, `/how-it-works`, `/404`, `/500`, `/offline`) + add 1 new page (`/cookie-settings`) to the v2 design system, introducing `HeroGradient`, v2 primitives (`Btn`, `Divider`, `SettingsList/Row`, `ToggleSwitch`), and full IT/EN i18n for error pages.

**Architecture:** Heavy refactor (Option C). Extract shared cookie-consent logic into `lib/cookie-consent.ts` so the existing `CookieConsentBanner` and the new `/cookie-settings` page share a single localStorage contract. Banner public surface (exported types, hook, component props, rendered DOM) is preserved — existing banner tests must pass unchanged. Error pages migrate from hardcoded English strings to `useTranslation`.

**Tech Stack:** Next.js 16 App Router · React 19 · `react-intl` via `useTranslation` hook · `sonner ^2.0.7` (already installed) · v2 primitives from `@/components/ui/v2/*` · `lucide-react` · Vitest + RTL for unit tests

**Parent branch:** `main-dev` · **Feature branch:** `feature/issue-503-m3-9-secondary-public-pages` (already created) · **Spec:** `docs/superpowers/specs/2026-04-24-m3-9-secondary-public-pages-design.md`

---

## Task 1: Extract `lib/cookie-consent.ts` (TDD, lib-first)

Extract the read/write localStorage contract into a standalone module. Banner and `/cookie-settings` will both consume it. This is the foundation — everything else builds on it.

**Files:**
- Create: `apps/web/src/lib/cookie-consent.ts`
- Create: `apps/web/src/lib/__tests__/cookie-consent.test.ts`

- [ ] **Step 1: Write the failing tests**

```ts
// apps/web/src/lib/__tests__/cookie-consent.test.ts
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  CONSENT_KEY,
  CONSENT_VERSION,
  clearStoredConsent,
  getStoredConsent,
  setStoredConsent,
  type CookieConsent,
} from '../cookie-consent';

describe('lib/cookie-consent', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.unstubAllGlobals();
  });

  describe('getStoredConsent', () => {
    it('returns null when no consent is stored', () => {
      expect(getStoredConsent()).toBeNull();
    });

    it('returns the parsed consent when stored with current version', () => {
      const consent: CookieConsent = {
        version: CONSENT_VERSION,
        essential: true,
        analytics: true,
        functional: false,
        timestamp: '2026-04-24T10:00:00.000Z',
      };
      localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));

      expect(getStoredConsent()).toEqual(consent);
    });

    it('returns null when stored consent has a different version', () => {
      localStorage.setItem(
        CONSENT_KEY,
        JSON.stringify({
          version: '0.9',
          essential: true,
          analytics: true,
          functional: true,
          timestamp: '2026-04-24T10:00:00.000Z',
        })
      );

      expect(getStoredConsent()).toBeNull();
    });

    it('returns null when stored value is malformed JSON', () => {
      localStorage.setItem(CONSENT_KEY, '{not-json');
      expect(getStoredConsent()).toBeNull();
    });

    it('returns null when window is undefined (SSR safety)', () => {
      const originalWindow = globalThis.window;
      // @ts-expect-error intentional SSR simulation
      delete globalThis.window;
      try {
        expect(getStoredConsent()).toBeNull();
      } finally {
        globalThis.window = originalWindow;
      }
    });
  });

  describe('setStoredConsent', () => {
    it('writes a consent payload with version, essential=true, and ISO timestamp', () => {
      setStoredConsent({ analytics: true, functional: false });

      const raw = localStorage.getItem(CONSENT_KEY);
      expect(raw).not.toBeNull();
      const parsed = JSON.parse(raw!) as CookieConsent;
      expect(parsed.version).toBe(CONSENT_VERSION);
      expect(parsed.essential).toBe(true);
      expect(parsed.analytics).toBe(true);
      expect(parsed.functional).toBe(false);
      expect(() => new Date(parsed.timestamp)).not.toThrow();
      expect(new Date(parsed.timestamp).toISOString()).toBe(parsed.timestamp);
    });

    it('overwrites previous consent on repeated calls', () => {
      setStoredConsent({ analytics: true, functional: true });
      setStoredConsent({ analytics: false, functional: false });

      const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
      expect(parsed.analytics).toBe(false);
      expect(parsed.functional).toBe(false);
    });

    it('is a no-op when window is undefined (SSR safety)', () => {
      const originalWindow = globalThis.window;
      // @ts-expect-error intentional SSR simulation
      delete globalThis.window;
      try {
        expect(() => setStoredConsent({ analytics: true, functional: true })).not.toThrow();
      } finally {
        globalThis.window = originalWindow;
      }
    });
  });

  describe('clearStoredConsent', () => {
    it('removes the stored consent key', () => {
      setStoredConsent({ analytics: true, functional: true });
      expect(localStorage.getItem(CONSENT_KEY)).not.toBeNull();

      clearStoredConsent();

      expect(localStorage.getItem(CONSENT_KEY)).toBeNull();
    });

    it('is a no-op when no consent is stored', () => {
      expect(() => clearStoredConsent()).not.toThrow();
    });
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/cookie-consent.test.ts`

Expected: FAIL — module `../cookie-consent` does not exist.

- [ ] **Step 3: Write the minimal implementation**

```ts
// apps/web/src/lib/cookie-consent.ts
/**
 * Cookie consent storage contract (GDPR, ePrivacy Art. 5(3)).
 *
 * Source of truth for both `CookieConsentBanner` (first-visit prompt)
 * and `/cookie-settings` (explicit preferences page).
 */

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

export function setStoredConsent(next: { analytics: boolean; functional: boolean }): void {
  if (typeof window === 'undefined') return;
  const consent: CookieConsent = {
    version: CONSENT_VERSION,
    essential: true,
    analytics: next.analytics,
    functional: next.functional,
    timestamp: new Date().toISOString(),
  };
  try {
    localStorage.setItem(CONSENT_KEY, JSON.stringify(consent));
  } catch {
    // Storage quota exceeded or unavailable — matches banner's existing behavior.
  }
}

export function clearStoredConsent(): void {
  if (typeof window === 'undefined') return;
  try {
    localStorage.removeItem(CONSENT_KEY);
  } catch {
    // no-op
  }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/cookie-consent.test.ts`

Expected: PASS — all ~10 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/cookie-consent.ts apps/web/src/lib/__tests__/cookie-consent.test.ts
git commit -m "feat(m3-9): extract cookie-consent lib from banner"
```

---

## Task 2: Refactor `CookieConsentBanner` to consume `lib/cookie-consent.ts`

Replace banner-internal `CONSENT_KEY`, `CONSENT_VERSION`, `getStoredConsent`, `saveConsent` with imports from the lib. Banner's public surface (exported `CookieConsent` type, `useCookieConsent` hook return shape `{ consent, updateConsent, resetConsent, hasConsented }`, `<CookieConsentBanner />` component props, rendered DOM) MUST remain identical. Existing banner tests must pass unchanged.

**Files:**
- Modify: `apps/web/src/components/legal/CookieConsentBanner.tsx`
- Modify: `apps/web/src/components/legal/index.ts` (re-export `CookieConsent` type from lib to keep path stable)

- [ ] **Step 1: Run the existing banner tests first, capture baseline**

Run: `cd apps/web && pnpm vitest run src/components/legal/__tests__/CookieConsentBanner.test.tsx`

Expected: All existing tests PASS. Note the count — this is the baseline that must remain green after refactor.

- [ ] **Step 2: Refactor the banner to consume the lib**

Replace the top-of-file declarations with imports from the lib. Keep the exported `CookieConsent` type as a re-export for back-compat. Keep `useCookieConsent` hook and its return shape exactly as-is, delegating to the lib internally.

```tsx
// apps/web/src/components/legal/CookieConsentBanner.tsx — relevant changes
'use client';

import { useCallback, useEffect, useState } from 'react';

import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import {
  type CookieConsent,
  CONSENT_VERSION,
  clearStoredConsent,
  getStoredConsent,
  setStoredConsent,
} from '@/lib/cookie-consent';
import { cn } from '@/lib/utils';

// Re-export the type so `import { CookieConsent } from '@/components/legal'` keeps working.
export type { CookieConsent };

export function useCookieConsent() {
  const [consent, setConsent] = useState<CookieConsent | null>(null);

  useEffect(() => {
    setConsent(getStoredConsent());
  }, []);

  const updateConsent = useCallback(
    (partial: { analytics?: boolean; functional?: boolean }) => {
      const analytics = partial.analytics ?? consent?.analytics ?? false;
      const functional = partial.functional ?? consent?.functional ?? false;
      setStoredConsent({ analytics, functional });
      setConsent({
        version: CONSENT_VERSION,
        essential: true,
        analytics,
        functional,
        timestamp: new Date().toISOString(),
      });
    },
    [consent]
  );

  const resetConsent = useCallback(() => {
    clearStoredConsent();
    setConsent(null);
  }, []);

  const hasConsented = consent !== null;

  return { consent, updateConsent, resetConsent, hasConsented };
}

// …rest of CookieConsentBanner component body unchanged, but replace
// every call to internal getStoredConsent/saveConsent with the imports above.
```

Delete the now-unused local `CONSENT_KEY`, `CONSENT_VERSION`, `getStoredConsent`, `saveConsent` definitions from the banner file.

- [ ] **Step 3: Verify banner tests still pass unchanged**

Run: `cd apps/web && pnpm vitest run src/components/legal/__tests__/CookieConsentBanner.test.tsx`

Expected: PASS with the same count as the baseline in Step 1. If any test fails, the refactor broke the public contract — the `useCookieConsent` hook's return shape or the banner's DOM must be preserved exactly.

- [ ] **Step 4: Typecheck & lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint src/components/legal src/lib`

Expected: 0 errors.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/legal/CookieConsentBanner.tsx apps/web/src/components/legal/index.ts
git commit -m "refactor(m3-9): CookieConsentBanner consumes lib/cookie-consent"
```

---

## Task 3: Add Italian i18n keys

Add new keys under `pages.errors.*`, `pages.cookieSettings.*`, and any missing `pages.howItWorks.*` (stepsHeading, featuresHeading). Existing `pages.faq.*` keys are reused — do not duplicate.

**Files:**
- Modify: `apps/web/src/locales/it.json`

- [ ] **Step 1: Read the current `pages` block to preserve structure**

Run: Use the `Read` tool on `apps/web/src/locales/it.json` and locate the `pages` object.

- [ ] **Step 2: Add the new keys under the existing `pages` object**

Insert the following subtrees. If `pages.howItWorks` already exists, merge `stepsHeading` and `featuresHeading` into it without touching existing keys.

```jsonc
{
  "pages": {
    // ...existing keys preserved...
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

For `pages.howItWorks` (merge, do not replace):

```jsonc
"howItWorks": {
  // ...existing keys preserved...
  "stepsHeading": "Come funziona",
  "featuresHeading": "Funzionalità principali",
  "aboutCta": "Scopri di più su MeepleAI",
  "faqCta": "Consulta le FAQ"
}
```

For `pages.cookies` (reciprocal link label, used in Task 11):

```jsonc
"cookies": {
  // ...existing keys preserved...
  "managePreferencesCta": "Gestisci preferenze"
}
```

- [ ] **Step 3: Verify JSON is valid**

Run: `cd apps/web && node -e "require('./src/locales/it.json')"`

Expected: no output (silent success). A SyntaxError means the JSON is malformed.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/locales/it.json
git commit -m "feat(m3-9): add IT i18n keys for error pages and cookie-settings"
```

---

## Task 4: Add English i18n keys (EN mirror)

Mirror every key added in Task 3 under the same paths in `en.json`. Direct translations, same structural shape.

**Files:**
- Modify: `apps/web/src/locales/en.json`

- [ ] **Step 1: Add mirrored keys**

```jsonc
{
  "pages": {
    // ...existing keys preserved...
    "errors": {
      "notFound": {
        "eyebrow": "404",
        "title": "Page not found",
        "subtitle": "The page you're looking for doesn't exist or has been moved.",
        "homeCta": "Back to home",
        "exploreCta": "Explore games"
      },
      "serverError": {
        "eyebrow": "500",
        "title": "Something went wrong",
        "subtitle": "An unexpected error occurred. Please retry or return home.",
        "retryCta": "Retry",
        "homeCta": "Back to home",
        "digestLabel": "Error ID"
      },
      "offline": {
        "title": "You're offline",
        "subtitle": "Don't worry — you can still access content saved in cache.",
        "retryCta": "Retry connection",
        "homeCta": "Back to home",
        "statsTitle": "Available offline",
        "sessionsLabel": "Sessions",
        "cachedGamesLabel": "Cached games",
        "pendingLabel": "Pending actions",
        "onlineStatus": "Connection restored",
        "offlineStatus": "No connection"
      }
    },
    "cookieSettings": {
      "title": "Cookie preferences",
      "subtitle": "Manage your cookie consent. You can update these settings at any time.",
      "categories": {
        "essential": {
          "label": "Essential cookies",
          "description": "Required for the site to function (session, core preferences). They cannot be disabled."
        },
        "analytics": {
          "label": "Analytics cookies",
          "description": "Help us understand how you use the site, in aggregated and anonymous form."
        },
        "functional": {
          "label": "Functional cookies",
          "description": "Improve your experience by remembering your choices (theme, language)."
        }
      },
      "actions": {
        "save": "Save preferences",
        "acceptAll": "Accept all",
        "rejectAll": "Reject optional"
      },
      "savedToast": "Preferences saved",
      "policyLink": "Read the full cookie policy"
    }
  }
}
```

For `pages.howItWorks` and `pages.cookies` (merge):

```jsonc
"howItWorks": {
  // ...existing keys preserved...
  "stepsHeading": "How it works",
  "featuresHeading": "Main features",
  "aboutCta": "Learn more about MeepleAI",
  "faqCta": "Browse the FAQ"
},
"cookies": {
  // ...existing keys preserved...
  "managePreferencesCta": "Manage preferences"
}
```

- [ ] **Step 2: Verify JSON is valid & key parity with IT**

Run: `cd apps/web && node -e "require('./src/locales/en.json')"` then run a quick key-parity script:

```bash
cd apps/web && node -e "
const it = require('./src/locales/it.json');
const en = require('./src/locales/en.json');
const keys = (o, p = []) => Object.entries(o).flatMap(([k, v]) =>
  typeof v === 'object' && v !== null ? keys(v, [...p, k]) : [[...p, k].join('.')]);
const itKeys = new Set(keys(it));
const enKeys = new Set(keys(en));
const missingInEn = [...itKeys].filter(k => !enKeys.has(k));
const missingInIt = [...enKeys].filter(k => !itKeys.has(k));
if (missingInEn.length || missingInIt.length) {
  console.error('Missing in EN:', missingInEn);
  console.error('Missing in IT:', missingInIt);
  process.exit(1);
}
console.log('IT/EN parity OK, ' + itKeys.size + ' keys each');
"
```

Expected: `IT/EN parity OK, N keys each` — no missing keys on either side.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/locales/en.json
git commit -m "feat(m3-9): add EN i18n keys mirroring IT additions"
```

---

## Task 5: Build `/cookie-settings` page (new route, TDD)

**Files:**
- Create: `apps/web/src/app/(public)/cookie-settings/page.tsx`
- Create: `apps/web/src/app/(public)/cookie-settings/__tests__/page.test.tsx`

- [ ] **Step 1: Write failing tests covering the 7 mandatory scenarios**

```tsx
// apps/web/src/app/(public)/cookie-settings/__tests__/page.test.tsx
import { act, render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import CookieSettingsPage from '../page';
import { CONSENT_KEY, CONSENT_VERSION, type CookieConsent } from '@/lib/cookie-consent';

// Toast is a visual side-effect; assert it was called, don't render it.
vi.mock('sonner', () => ({
  toast: { success: vi.fn() },
}));

// Mirror the test pattern used by existing locale-aware pages in this repo:
// useTranslation returns t(key) => key so assertions hit the key paths directly.
vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('CookieSettingsPage', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('mounts with both toggles off when no consent is stored', async () => {
    await act(async () => {
      render(<CookieSettingsPage />);
    });
    // Essential is disabled-checked; analytics/functional reflect false.
    const analytics = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.analytics.label',
    });
    const functional = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.functional.label',
    });
    expect(analytics).toHaveAttribute('aria-checked', 'false');
    expect(functional).toHaveAttribute('aria-checked', 'false');
  });

  it('mounts with toggles reflecting stored consent', async () => {
    const stored: CookieConsent = {
      version: CONSENT_VERSION,
      essential: true,
      analytics: true,
      functional: false,
      timestamp: '2026-04-24T10:00:00.000Z',
    };
    localStorage.setItem(CONSENT_KEY, JSON.stringify(stored));

    await act(async () => {
      render(<CookieSettingsPage />);
    });

    expect(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    ).toHaveAttribute('aria-checked', 'true');
    expect(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.functional.label' })
    ).toHaveAttribute('aria-checked', 'false');
  });

  it('toggling analytics updates the control state before saving', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    const analytics = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.analytics.label',
    });
    await user.click(analytics);
    expect(analytics).toHaveAttribute('aria-checked', 'true');
  });

  it('Save writes the correct shape to localStorage and emits cookie-consent-updated', async () => {
    const user = userEvent.setup();
    const dispatchSpy = vi.spyOn(window, 'dispatchEvent');

    await act(async () => {
      render(<CookieSettingsPage />);
    });

    await user.click(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    );
    await user.click(screen.getByRole('button', { name: 'pages.cookieSettings.actions.save' }));

    const raw = localStorage.getItem(CONSENT_KEY);
    expect(raw).not.toBeNull();
    const parsed = JSON.parse(raw!) as CookieConsent;
    expect(parsed.version).toBe(CONSENT_VERSION);
    expect(parsed.essential).toBe(true);
    expect(parsed.analytics).toBe(true);
    expect(parsed.functional).toBe(false);

    const events = dispatchSpy.mock.calls
      .map(([e]) => e as Event)
      .filter((e) => e.type === 'cookie-consent-updated');
    expect(events.length).toBeGreaterThanOrEqual(1);
  });

  it('Accept all sets both analytics and functional to true', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    await user.click(
      screen.getByRole('button', { name: 'pages.cookieSettings.actions.acceptAll' })
    );

    const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
    expect(parsed.analytics).toBe(true);
    expect(parsed.functional).toBe(true);
  });

  it('Reject optional sets both analytics and functional to false', async () => {
    const user = userEvent.setup();
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    // Flip analytics on first, then reject — proves reject truly writes false, not just retains state.
    await user.click(
      screen.getByRole('switch', { name: 'pages.cookieSettings.categories.analytics.label' })
    );
    await user.click(
      screen.getByRole('button', { name: 'pages.cookieSettings.actions.rejectAll' })
    );

    const parsed = JSON.parse(localStorage.getItem(CONSENT_KEY)!) as CookieConsent;
    expect(parsed.analytics).toBe(false);
    expect(parsed.functional).toBe(false);
  });

  it('essential toggle is always checked and disabled', async () => {
    await act(async () => {
      render(<CookieSettingsPage />);
    });

    const essential = screen.getByRole('switch', {
      name: 'pages.cookieSettings.categories.essential.label',
    });
    expect(essential).toHaveAttribute('aria-checked', 'true');
    expect(essential).toBeDisabled();
  });
});
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/cookie-settings`

Expected: FAIL — module `../page` does not exist.

- [ ] **Step 3: Write the page implementation**

```tsx
// apps/web/src/app/(public)/cookie-settings/page.tsx
'use client';

import { useCallback, useEffect, useState } from 'react';

import Link from 'next/link';
import { toast } from 'sonner';

import { Btn } from '@/components/ui/v2/btn';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { SettingsList } from '@/components/ui/v2/settings-list';
import { SettingsRow } from '@/components/ui/v2/settings-row';
import { ToggleSwitch } from '@/components/ui/v2/toggle-switch';
import { useTranslation } from '@/hooks/useTranslation';
import { getStoredConsent, setStoredConsent } from '@/lib/cookie-consent';

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

  const save = useCallback(
    (next: { analytics: boolean; functional: boolean }) => {
      setStoredConsent(next);
      setAnalytics(next.analytics);
      setFunctional(next.functional);
      toast.success(t('pages.cookieSettings.savedToast'));
      window.dispatchEvent(new CustomEvent('cookie-consent-updated'));
    },
    [t]
  );

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
            trailing={
              <ToggleSwitch
                checked={true}
                disabled
                onCheckedChange={() => {}}
                ariaLabel={t('pages.cookieSettings.categories.essential.label')}
              />
            }
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.analytics.label')}
            description={t('pages.cookieSettings.categories.analytics.description')}
            trailing={
              <ToggleSwitch
                checked={analytics}
                onCheckedChange={setAnalytics}
                disabled={!hydrated}
                ariaLabel={t('pages.cookieSettings.categories.analytics.label')}
              />
            }
          />
          <SettingsRow
            label={t('pages.cookieSettings.categories.functional.label')}
            description={t('pages.cookieSettings.categories.functional.description')}
            trailing={
              <ToggleSwitch
                checked={functional}
                onCheckedChange={setFunctional}
                disabled={!hydrated}
                ariaLabel={t('pages.cookieSettings.categories.functional.label')}
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

Verify that the v2 `<Toaster />` is already mounted in the root layout. If it isn't, add it now — this is the single-line risk #6 from the spec.

Run: `cd apps/web && pnpm grep -R "<Toaster" src/app/layout.tsx src/app/providers.tsx 2>/dev/null || true`

If no Toaster is found in the root layout tree, add one to `apps/web/src/app/layout.tsx` (single line import + placement) next to other global providers.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/cookie-settings`

Expected: PASS — all 7 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(public\)/cookie-settings/
# Include layout.tsx only if Toaster was added in Step 3.
git commit -m "feat(m3-9): add /cookie-settings page with GDPR preference controls"
```

---

## Task 6: Rewrite `/faq` with v2 primitives

Replace the current implementation with `HeroGradient` + `Divider`-separated category sections + the existing `Accordion` primitive. Accordion stays — there is no v2 equivalent and it's the correct UX.

**Files:**
- Modify: `apps/web/src/app/(public)/faq/page.tsx`
- Create: `apps/web/src/app/(public)/faq/__tests__/page.test.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
// apps/web/src/app/(public)/faq/__tests__/page.test.tsx
import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import FaqPage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('FaqPage', () => {
  it('renders the hero with title and subtitle', () => {
    render(<FaqPage />);
    expect(screen.getByRole('heading', { name: 'pages.faq.title', level: 1 })).toBeInTheDocument();
    expect(screen.getByText('pages.faq.subtitle')).toBeInTheDocument();
  });

  it('renders 4 category sections (general, usage, technical, account)', () => {
    render(<FaqPage />);
    expect(screen.getByRole('heading', { name: /pages\.faq\.categories\.general/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /pages\.faq\.categories\.usage/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /pages\.faq\.categories\.technical/ })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /pages\.faq\.categories\.account/ })).toBeInTheDocument();
  });

  it('expands an accordion item on click to reveal the answer', async () => {
    const user = userEvent.setup();
    render(<FaqPage />);
    const firstTrigger = screen.getAllByRole('button', { name: /pages\.faq\.items\.general\.q1\.question/ })[0];
    await user.click(firstTrigger);
    expect(screen.getByText('pages.faq.items.general.q1.answer')).toBeVisible();
  });

  it('footer CTAs link to /contact and /how-it-works', () => {
    render(<FaqPage />);
    const contactLink = screen.getByRole('link', { name: 'pages.faq.contactCta' });
    const howLink = screen.getByRole('link', { name: 'pages.faq.howItWorksCta' });
    expect(contactLink).toHaveAttribute('href', '/contact');
    expect(howLink).toHaveAttribute('href', '/how-it-works');
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/faq`

Expected: FAIL — the current FAQ page doesn't match the new assertions (structure changed).

- [ ] **Step 3: Rewrite the page**

```tsx
// apps/web/src/app/(public)/faq/page.tsx
'use client';

import { Fragment } from 'react';

import Link from 'next/link';

import {
  Accordion,
  AccordionContent,
  AccordionItem,
  AccordionTrigger,
} from '@/components/ui/data-display/accordion';
import { Btn } from '@/components/ui/v2/btn';
import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

const CATEGORIES = ['general', 'usage', 'technical', 'account'] as const;
const QUESTIONS = ['q1', 'q2', 'q3'] as const;
const CATEGORY_EMOJI: Record<(typeof CATEGORIES)[number], string> = {
  general: '💡',
  usage: '🎲',
  technical: '⚙️',
  account: '👤',
};

export default function FaqPage() {
  const { t } = useTranslation();

  return (
    <main>
      <HeroGradient title={t('pages.faq.title')} subtitle={t('pages.faq.subtitle')} />

      {CATEGORIES.map((cat, idx) => (
        <Fragment key={cat}>
          {idx > 0 && <Divider />}
          <section className="max-w-3xl mx-auto py-8 px-4">
            <h2 className="text-2xl font-bold mb-6">
              <span aria-hidden="true" className="mr-2">
                {CATEGORY_EMOJI[cat]}
              </span>
              {t(`pages.faq.categories.${cat}`)}
            </h2>
            <Accordion type="single" collapsible>
              {QUESTIONS.map((q) => (
                <AccordionItem key={q} value={`${cat}-${q}`}>
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

      <section className="max-w-3xl mx-auto py-12 flex flex-col sm:flex-row gap-3 justify-center items-center">
        <Btn variant="primary" asChild>
          <Link href="/contact">{t('pages.faq.contactCta')}</Link>
        </Btn>
        <Btn variant="ghost" asChild>
          <Link href="/how-it-works">{t('pages.faq.howItWorksCta')}</Link>
        </Btn>
      </section>
    </main>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/faq`

Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(public\)/faq/
git commit -m "refactor(m3-9): rewrite /faq with HeroGradient + v2 primitives"
```

---

## Task 7: Rewrite `/how-it-works` with v2 primitives

**Files:**
- Modify: `apps/web/src/app/(public)/how-it-works/page.tsx`
- Create: `apps/web/src/app/(public)/how-it-works/__tests__/page.test.tsx`

- [ ] **Step 1: Write the failing tests**

```tsx
// apps/web/src/app/(public)/how-it-works/__tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import HowItWorksPage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('HowItWorksPage', () => {
  it('renders hero with a primary CTA to /register', () => {
    render(<HowItWorksPage />);
    const cta = screen.getByRole('link', { name: 'pages.howItWorks.ctaRegister' });
    expect(cta).toHaveAttribute('href', '/register');
  });

  it('renders 3 steps (step1, step2, step3)', () => {
    render(<HowItWorksPage />);
    expect(screen.getByRole('heading', { name: 'pages.howItWorks.step1.title' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'pages.howItWorks.step2.title' })).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'pages.howItWorks.step3.title' })).toBeInTheDocument();
  });

  it('renders 4 feature cards (rag, multilingual, pdfUpload, gameLibrary)', () => {
    render(<HowItWorksPage />);
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.rag.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.multilingual.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.pdfUpload.title' })
    ).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: 'pages.howItWorks.features.gameLibrary.title' })
    ).toBeInTheDocument();
  });

  it('footer CTAs link to /about and /faq', () => {
    render(<HowItWorksPage />);
    expect(screen.getByRole('link', { name: 'pages.howItWorks.aboutCta' })).toHaveAttribute(
      'href',
      '/about'
    );
    expect(screen.getByRole('link', { name: 'pages.howItWorks.faqCta' })).toHaveAttribute(
      'href',
      '/faq'
    );
  });
});
```

- [ ] **Step 2: Run tests to verify failure**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/how-it-works`

Expected: FAIL — current page doesn't match the new structure.

- [ ] **Step 3: Rewrite the page**

```tsx
// apps/web/src/app/(public)/how-it-works/page.tsx
'use client';

import Link from 'next/link';

import { Btn } from '@/components/ui/v2/btn';
import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

const STEPS = ['step1', 'step2', 'step3'] as const;
const STEP_EMOJI: Record<(typeof STEPS)[number], string> = {
  step1: '📚',
  step2: '💬',
  step3: '🎯',
};

const FEATURES = ['rag', 'multilingual', 'pdfUpload', 'gameLibrary'] as const;
const FEATURE_EMOJI: Record<(typeof FEATURES)[number], string> = {
  rag: '🔎',
  multilingual: '🌐',
  pdfUpload: '📄',
  gameLibrary: '🎲',
};

export default function HowItWorksPage() {
  const { t } = useTranslation();

  return (
    <main>
      <HeroGradient
        title={t('pages.howItWorks.title')}
        subtitle={t('pages.howItWorks.subtitle')}
        primaryCta={{ label: t('pages.howItWorks.ctaRegister'), href: '/register' }}
      />

      <section className="max-w-5xl mx-auto py-12 px-4">
        <h2 className="text-2xl font-bold text-center">
          {t('pages.howItWorks.stepsHeading')}
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-8 mt-8">
          {STEPS.map((s, idx) => (
            <div key={s} className="text-center">
              <div className="text-5xl mb-3" aria-hidden="true">
                {STEP_EMOJI[s]}
              </div>
              <div className="text-sm font-mono text-muted-foreground mb-1">
                {String(idx + 1).padStart(2, '0')}
              </div>
              <h3 className="text-lg font-semibold mb-2">
                {t(`pages.howItWorks.${s}.title`)}
              </h3>
              <p className="text-muted-foreground">
                {t(`pages.howItWorks.${s}.description`)}
              </p>
            </div>
          ))}
        </div>
      </section>

      <Divider />

      <section className="max-w-5xl mx-auto py-12 px-4">
        <h2 className="text-2xl font-bold text-center">
          {t('pages.howItWorks.featuresHeading')}
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mt-8">
          {FEATURES.map((f) => (
            <div key={f} className="p-4">
              <div className="text-4xl mb-2" aria-hidden="true">
                {FEATURE_EMOJI[f]}
              </div>
              <h3 className="text-lg font-semibold mb-1">
                {t(`pages.howItWorks.features.${f}.title`)}
              </h3>
              <p className="text-muted-foreground">
                {t(`pages.howItWorks.features.${f}.description`)}
              </p>
            </div>
          ))}
        </div>
      </section>

      <section className="flex flex-col sm:flex-row gap-3 justify-center items-center py-8">
        <Btn variant="primary" asChild>
          <Link href="/about">{t('pages.howItWorks.aboutCta')}</Link>
        </Btn>
        <Btn variant="ghost" asChild>
          <Link href="/faq">{t('pages.howItWorks.faqCta')}</Link>
        </Btn>
      </section>
    </main>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/how-it-works`

Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/\(public\)/how-it-works/
git commit -m "refactor(m3-9): rewrite /how-it-works with HeroGradient + v2 primitives"
```

---

## Task 8: Rewrite `not-found.tsx` with i18n + HeroGradient

Keep `export const dynamic = 'force-dynamic'` if and only if removing it breaks the build (DOMMatrix issue — per spec risk #1). The implementation step below preserves the directive; Step 4 verifies the build. If the build passes without it, remove it in a follow-up — not in this task.

**Files:**
- Modify: `apps/web/src/app/not-found.tsx`
- Create: `apps/web/src/app/__tests__/not-found.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/app/__tests__/not-found.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import NotFound from '../not-found';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('NotFound', () => {
  it('renders the localized 404 title, subtitle, and both CTAs', () => {
    render(<NotFound />);

    expect(screen.getByText('404')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.notFound\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.notFound.subtitle')).toBeInTheDocument();

    const homeCta = screen.getByRole('link', { name: 'pages.errors.notFound.homeCta' });
    const exploreCta = screen.getByRole('link', { name: 'pages.errors.notFound.exploreCta' });
    expect(homeCta).toHaveAttribute('href', '/');
    expect(exploreCta).toHaveAttribute('href', '/games');
  });

  it('contains zero hardcoded English strings in rendered output', () => {
    const { container } = render(<NotFound />);
    const text = container.textContent ?? '';
    // Prior implementation shipped these exact strings; they must be gone post-migration.
    expect(text).not.toMatch(/Page not found/);
    expect(text).not.toMatch(/Back to home/);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/__tests__/not-found.test.tsx`

Expected: FAIL — the current page contains hardcoded English strings, does not use `useTranslation`.

- [ ] **Step 3: Rewrite the page**

```tsx
// apps/web/src/app/not-found.tsx
'use client';

// NOTE: `export const dynamic = 'force-dynamic'` is retained per spec risk #1
// (known Next.js 16 DOMMatrix bug with client imports in not-found.tsx). If
// `pnpm build` succeeds without it, remove in a follow-up — not in this task.
export const dynamic = 'force-dynamic';

import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

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

- [ ] **Step 4: Run tests + build to verify both**

Run:
```bash
cd apps/web && pnpm vitest run src/app/__tests__/not-found.test.tsx
pnpm build
```

Expected: unit tests PASS; `pnpm build` completes without the DOMMatrix error. If build fails with DOMMatrix-related error, keep `dynamic = 'force-dynamic'` (already present).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/not-found.tsx apps/web/src/app/__tests__/not-found.test.tsx
git commit -m "refactor(m3-9): localize not-found.tsx with i18n + HeroGradient"
```

---

## Task 9: Rewrite `error.tsx` with i18n + HeroGradient + reset CTA

**Files:**
- Modify: `apps/web/src/app/error.tsx`
- Create: `apps/web/src/app/__tests__/error.test.tsx`

- [ ] **Step 1: Write the failing test**

```tsx
// apps/web/src/app/__tests__/error.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import GlobalError from '../error';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

describe('GlobalError', () => {
  let consoleSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});
  });

  afterEach(() => {
    consoleSpy.mockRestore();
    vi.unstubAllEnvs();
  });

  it('renders localized title and subtitle', () => {
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={() => {}} />);

    expect(screen.getByText('500')).toBeInTheDocument();
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.serverError\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.serverError.subtitle')).toBeInTheDocument();
  });

  it('retry button invokes the reset callback', async () => {
    const user = userEvent.setup();
    const reset = vi.fn();
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={reset} />);

    await user.click(screen.getByRole('button', { name: 'pages.errors.serverError.retryCta' }));
    expect(reset).toHaveBeenCalledTimes(1);
  });

  it('logs the error to console.error on mount', () => {
    const err = new Error('boom') as Error & { digest?: string };
    render(<GlobalError error={err} reset={() => {}} />);
    expect(consoleSpy).toHaveBeenCalledWith(err);
  });

  it('shows error digest only in non-production environments', () => {
    vi.stubEnv('NODE_ENV', 'development');
    const err = Object.assign(new Error('boom'), { digest: 'abc123' }) as Error & {
      digest?: string;
    };
    const { unmount } = render(<GlobalError error={err} reset={() => {}} />);
    expect(screen.getByText(/abc123/)).toBeInTheDocument();
    unmount();

    vi.stubEnv('NODE_ENV', 'production');
    render(<GlobalError error={err} reset={() => {}} />);
    expect(screen.queryByText(/abc123/)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/__tests__/error.test.tsx`

Expected: FAIL — the current page has hardcoded English strings.

- [ ] **Step 3: Rewrite the page**

```tsx
// apps/web/src/app/error.tsx
'use client';

import { useEffect } from 'react';

import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

export default function GlobalError({
  error,
  reset,
}: {
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

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/app/__tests__/error.test.tsx`

Expected: PASS — all 4 tests green.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/app/error.tsx apps/web/src/app/__tests__/error.test.tsx
git commit -m "refactor(m3-9): localize error.tsx with i18n + HeroGradient"
```

---

## Task 10: Rewrite `/offline` with i18n + HeroGradient + cached stats

**Files:**
- Modify: `apps/web/src/app/offline/page.tsx`
- Create: `apps/web/src/app/offline/__tests__/page.test.tsx`

- [ ] **Step 1: Inspect existing `usePWA` hook signature**

Run: `grep -n "storageStats\|isOnline" apps/web/src/hooks/usePWA.ts | head -20`

Expected: confirms `storageStats: { sessions, cachedGames, pendingActions } | null` and `isOnline: boolean` are present. If the field names differ, adjust the test mock accordingly.

- [ ] **Step 2: Write the failing test**

```tsx
// apps/web/src/app/offline/__tests__/page.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import OfflinePage from '../page';

vi.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({ t: (k: string) => k, locale: 'it' }),
}));

const usePWAMock = vi.fn();
vi.mock('@/hooks/usePWA', () => ({
  usePWA: () => usePWAMock(),
}));

describe('OfflinePage', () => {
  let reloadSpy: ReturnType<typeof vi.fn>;
  let historyBackSpy: ReturnType<typeof vi.fn>;

  beforeEach(() => {
    reloadSpy = vi.fn();
    historyBackSpy = vi.fn();
    // jsdom's window.location.reload is non-configurable; override via defineProperty.
    Object.defineProperty(window, 'location', {
      configurable: true,
      value: { ...window.location, reload: reloadSpy },
    });
    window.history.back = historyBackSpy;
  });

  afterEach(() => {
    vi.clearAllMocks();
  });

  it('renders localized offline content', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    expect(
      screen.getByRole('heading', { name: /pages\.errors\.offline\.title/ })
    ).toBeInTheDocument();
    expect(screen.getByText('pages.errors.offline.subtitle')).toBeInTheDocument();
  });

  it('retry CTA triggers window.location.reload', async () => {
    const user = userEvent.setup();
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);

    await user.click(screen.getByRole('button', { name: 'pages.errors.offline.retryCta' }));
    expect(reloadSpy).toHaveBeenCalledTimes(1);
  });

  it('renders stats section when storageStats is provided', () => {
    usePWAMock.mockReturnValue({
      storageStats: { sessions: 3, cachedGames: 5, pendingActions: 2 },
      isOnline: false,
    });
    render(<OfflinePage />);

    expect(screen.getByText('pages.errors.offline.statsTitle')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('5')).toBeInTheDocument();
    expect(screen.getByText('2')).toBeInTheDocument();
  });

  it('does not render stats section when storageStats is null', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    expect(screen.queryByText('pages.errors.offline.statsTitle')).not.toBeInTheDocument();
  });

  it('auto-navigates back when isOnline becomes true', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: true });
    render(<OfflinePage />);
    expect(historyBackSpy).toHaveBeenCalledTimes(1);
  });

  it('exposes a polite live region announcing the connection status', () => {
    usePWAMock.mockReturnValue({ storageStats: null, isOnline: false });
    render(<OfflinePage />);
    const live = screen.getByText('pages.errors.offline.offlineStatus');
    expect(live.closest('[role="status"]')).toHaveAttribute('aria-live', 'polite');
  });
});
```

- [ ] **Step 3: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/app/offline`

Expected: FAIL — current page has hardcoded English strings and different structure.

- [ ] **Step 4: Rewrite the page**

```tsx
// apps/web/src/app/offline/page.tsx
'use client';

import { useEffect } from 'react';

import { WifiOff } from 'lucide-react';

import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { usePWA } from '@/hooks/usePWA';
import { useTranslation } from '@/hooks/useTranslation';

function Stat({ label, value }: { label: string; value: number }) {
  return (
    <div className="flex flex-col items-center">
      <span className="text-2xl font-bold">{value}</span>
      <span className="text-xs text-muted-foreground">{label}</span>
    </div>
  );
}

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
            <h2 className="text-center text-lg font-semibold">
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

      <div role="status" aria-live="polite" aria-atomic="true" className="sr-only">
        {isOnline
          ? t('pages.errors.offline.onlineStatus')
          : t('pages.errors.offline.offlineStatus')}
      </div>
    </main>
  );
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/app/offline`

Expected: PASS — all 6 tests green.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/app/offline/
git commit -m "refactor(m3-9): localize /offline with i18n + HeroGradient + stats"
```

---

## Task 11: Add reciprocal "Gestisci preferenze" link on `/cookies`

Single-line change on the cookies policy page. The rest of the page is untouched (spec DoD #10).

**Files:**
- Modify: `apps/web/src/app/(public)/cookies/page.tsx`

- [ ] **Step 1: Read the current page to find the right insertion point**

Run: use the `Read` tool on `apps/web/src/app/(public)/cookies/page.tsx` and locate the bottom of the `LegalPageLayout` content — the natural spot is after the last body section or inside a `footer`-like area of the layout.

- [ ] **Step 2: Add the link**

Add the following import:

```tsx
import Link from 'next/link';
```

Add the following at the bottom of the page content (inside `LegalPageLayout` body, after the final paragraph):

```tsx
<div className="mt-8">
  <Link href="/cookie-settings" className="text-sm underline">
    {t('pages.cookies.managePreferencesCta')}
  </Link>
</div>
```

If the page doesn't already call `useTranslation`, add:

```tsx
import { useTranslation } from '@/hooks/useTranslation';
// inside the component:
const { t } = useTranslation();
```

- [ ] **Step 3: Verify `/cookies` existing tests still pass**

Run: `cd apps/web && pnpm vitest run src/app/\(public\)/cookies`

Expected: PASS — new link is additive, existing structure unchanged. If no tests exist for `/cookies`, run typecheck instead: `pnpm typecheck`.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/app/\(public\)/cookies/page.tsx
git commit -m "feat(m3-9): add 'Gestisci preferenze' link on /cookies → /cookie-settings"
```

---

## Task 12: Full-suite validation + bundle size check + CI gates

Final sweep before opening the PR. All acceptance criteria from the spec (DoD items 1-11) must be satisfied.

**Files:** no code changes.

- [ ] **Step 1: Full test suite**

Run: `cd apps/web && pnpm test`

Expected: all tests PASS — including pre-existing `CookieConsentBanner` tests (DoD #4). No flaky tests introduced.

- [ ] **Step 2: Typecheck + lint**

Run: `cd apps/web && pnpm typecheck && pnpm lint`

Expected: 0 errors, 0 warnings. Includes `local/no-dual-connection-source` and dev-tools isolation (DoD #5, #6).

- [ ] **Step 3: Production build**

Run: `cd apps/web && pnpm build`

Expected: build succeeds. Note any new route hashes and bundle sizes in the output.

- [ ] **Step 4: Bundle size regression check**

Run: compare the produced `.next/` bundle size total (or existing baseline JSON if the repo enforces one) against the pre-M3.9 baseline.

```bash
cd apps/web && du -sb .next | awk '{print $1}'
```

Expected: ≤ 3% regression vs the pre-M3.9 baseline (DoD #8). If exceeded, review diffs — the new `/cookie-settings` route + i18n keys are the only legitimate additions. If size still breaches 3%, document the justification in the PR description; do not silently accept.

- [ ] **Step 5: Manual QA spot-check (optional but recommended)**

Start the dev server and walk through the manual QA steps from the spec:

```bash
cd apps/web && pnpm dev
```

Then in a browser:
1. `/cookie-settings` → toggle analytics on → Save → reload → toggle still on ✓
2. Clear localStorage → `/` → banner appears → Accept → `/cookie-settings` → analytics on ✓
3. `/any-404-path` → localized 404 ✓
4. Force a dev error → localized 500 page with digest ✓
5. DevTools offline mode → `/offline` → localized content + stats ✓
6. DevTools online mode → auto-redirects back ✓

- [ ] **Step 6: Push + open PR to `main-dev`**

```bash
git push -u origin feature/issue-503-m3-9-secondary-public-pages
gh pr create --base main-dev --title "M3.9: secondary public pages + error states v2 migration (#503)" --body "$(cat <<'EOF'
## Summary
- Migrate `/faq`, `/how-it-works`, `/404`, `/500`, `/offline` to v2 design system (HeroGradient + Btn + Divider).
- Add new `/cookie-settings` route with GDPR consent controls (SettingsList + SettingsRow + ToggleSwitch).
- Extract cookie-consent storage to `lib/cookie-consent.ts`; banner refactored to consume it (public contract preserved).
- Add IT + EN i18n keys for error pages (eliminating hardcoded English strings).
- Add reciprocal "Gestisci preferenze" link on `/cookies`.

Closes #503.

## Test plan
- [x] Unit tests green: 7 new test files, all `CookieConsentBanner` tests still pass unchanged.
- [x] `pnpm typecheck` + `pnpm lint` clean.
- [x] `pnpm build` succeeds; bundle regression ≤ 3%.
- [x] Manual QA: cookie-settings persistence, banner reciprocity, error pages localized, offline stats render.
- [x] CI: Frontend Build & Test, E2E Critical Paths, dev-tools isolation, GitGuardian.
EOF
)"
```

Expected: PR opened against `main-dev` (NOT `main` — per CLAUDE.md rule). CI runs and all gates green.

- [ ] **Step 7: Await review + merge**

Address review feedback if any, squash-merge when approved. After merge:

```bash
git checkout main-dev && git pull
git branch -D feature/issue-503-m3-9-secondary-public-pages
git remote prune origin
```

---

## Appendix — v2 primitive API quick reference

Captured from the actual primitive sources on `main-dev` (verified 2026-04-24) — plan authors used these signatures; divergence means the plan is wrong, not the primitive.

- **`HeroGradient`**: `{ title: ReactNode, subtitle?, primaryCta?: {label, href?, onClick?}, secondaryCta?, className?, children? }` — no variants; eyebrows go inside `title` as `ReactNode` (`<span className="block">...</span>`).
- **`Btn`**: `{ variant?: 'primary'|'secondary'|'outline'|'ghost'|'destructive', size?, entity?, asChild?, ...standard }` — `asChild` wraps via Radix `Slot` for Next.js `<Link>` composition.
- **`SettingsRow`**: `{ icon?, label, description?, trailing?, onClick?, href?, destructive?, entity?, disabled?, className? }` — uses **`trailing`** (not `control`).
- **`ToggleSwitch`**: `{ checked, onCheckedChange, disabled?, entity?, size?, ariaLabel?, ariaLabelledBy?, className?, id? }` — uses **`onCheckedChange`** (not `onChange`); requires `ariaLabel` or `ariaLabelledBy` (dev-time warning otherwise).
- **`Divider`**: drop-in horizontal rule, no required props.
- **`SettingsList`**: wrapper that groups `SettingsRow` children with dividers between them.

## Appendix — `useCookieConsent` hook preservation contract

The hook is re-exported from `components/legal/index.ts`, making it public API surface even though no external consumers exist today. After Task 2's refactor:

- Return shape MUST remain: `{ consent: CookieConsent | null, updateConsent, resetConsent, hasConsented: boolean }`.
- Import paths that already work (`@/components/legal` and `@/components/legal/CookieConsentBanner`) MUST keep working.
- The `CookieConsent` type MUST remain importable from `@/components/legal` (done via re-export from the lib).

Any implementation choice that violates the above fails Task 2 Step 3 (existing banner tests) and must be rolled back.
