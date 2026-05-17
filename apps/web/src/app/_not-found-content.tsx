import type { JSX } from 'react';

import { HeroGradient } from '@/components/ui/hero-gradient';
import { getSsrMessages } from '@/lib/i18n/ssr';

const NOT_FOUND_MESSAGES = getSsrMessages('pages.errors.notFound');

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
