import type { JSX } from 'react';

import { HeroGradient } from '@/components/ui/hero-gradient';
import itMessages from '@/locales/it.json';

/**
 * SSR-safe i18n source. `IntlProvider` only mounts on the client (see
 * `apps/web/src/app/providers.tsx`), so any component reachable during
 * Next.js SSR — including the global 404 — must NOT use `useTranslation()`,
 * otherwise hydration produces React error #418 (mismatch between the
 * server-rendered message-id key and the client-rendered translation).
 *
 * Pattern mirrors `apps/web/src/app/(public)/shared-games/[id]/page.tsx`
 * (Wave A.4 follow-up — Issue #617). Closes Issue #1076.
 *
 * NOTE: Hardcodes IT locale (project default — `LOCALES.IT`). When SSR
 * locale negotiation is introduced, refactor to read locale from headers
 * and conditionally pick `it.json` vs `en.json` (Issue #1076 AC-4 / TBD).
 */
const NOT_FOUND_MESSAGES = itMessages.pages.errors.notFound;

export function NotFoundContent(): JSX.Element {
  return (
    <main>
      <HeroGradient
        title={
          <>
            <span aria-hidden="true" className="block text-xl font-mono text-muted-foreground mb-2">
              {NOT_FOUND_MESSAGES.eyebrow}
            </span>
            {NOT_FOUND_MESSAGES.title}
          </>
        }
        subtitle={NOT_FOUND_MESSAGES.subtitle}
        primaryCta={{ label: NOT_FOUND_MESSAGES.homeCta, href: '/' }}
        secondaryCta={{ label: NOT_FOUND_MESSAGES.exploreCta, href: '/games' }}
      />
    </main>
  );
}
