'use client';

import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

// NOTE: `export const dynamic = 'force-dynamic'` is retained per spec risk #1
// (known Next.js 16 DOMMatrix bug with client imports in not-found.tsx). If
// `pnpm build` succeeds without it, remove in a follow-up — not in this task.
export const dynamic = 'force-dynamic';

export default function NotFound() {
  const { t } = useTranslation();

  return (
    <main>
      <HeroGradient
        title={
          <>
            <span aria-hidden="true" className="block text-xl font-mono text-muted-foreground mb-2">
              404
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
