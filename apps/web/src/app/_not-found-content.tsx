'use client';

import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';

export function NotFoundContent() {
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
