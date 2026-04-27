'use client';

import { useEffect } from 'react';

import { WifiOff } from 'lucide-react';

import { Divider } from '@/components/ui/v2/divider';
import { HeroGradient } from '@/components/ui/v2/hero-gradient';
import { useTranslation } from '@/hooks/useTranslation';
import { usePWA } from '@/lib/domain-hooks/usePWA';

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
              <Stat label={t('pages.errors.offline.sessionsLabel')} value={storageStats.sessions} />
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
