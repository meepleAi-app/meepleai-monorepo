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
            <span aria-hidden="true" className="block text-xl font-mono text-muted-foreground mb-2">
              500
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
