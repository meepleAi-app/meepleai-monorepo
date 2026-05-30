'use client';

/**
 * Play Records Page — Lista partite giocate
 *
 * Mobile-first layout: MobileHeader + PlayHistory + sticky GradientButton.
 */

import { Suspense, useState } from 'react';

import { BarChart3 } from 'lucide-react';
import dynamic from 'next/dynamic';
import { useRouter, useSearchParams } from 'next/navigation';

import { NewPlayRecordSheet } from '@/components/play-records/NewPlayRecordSheet';
import { StatisticsView } from '@/components/play-records/StatisticsView';
// PlayHistory usa uno Zustand store con `persist` (localStorage) — SSR disabilitato
// per evitare crash React 19 useSyncExternalStore durante hydration.
const PlayHistory = dynamic(
  () => import('@/components/play-records/PlayHistory').then(m => ({ default: m.PlayHistory })),
  { ssr: false }
);
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { useTranslation } from '@/hooks/useTranslation';

/**
 * Records list view (default tab). Split out so the page can branch on the
 * `tab` query param without paying for useSearchParams on the stats branch.
 */
function RecordsListView() {
  const router = useRouter();
  const { t } = useTranslation();
  const [sheetOpen, setSheetOpen] = useState(false);

  return (
    <div className="flex flex-col min-h-full bg-[var(--bg)]">
      <MobileHeader
        title={t('playRecords.index.headerTitle')}
        onBack={() => router.back()}
        rightActions={
          <button
            type="button"
            aria-label={t('playRecords.index.viewStatsLabel')}
            onClick={() => router.push('/play-records?tab=stats')}
            className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--text-sec)] hover:bg-card/5"
          >
            <BarChart3 className="h-5 w-5" />
          </button>
        }
      />

      {/* Lista */}
      <div className="flex-1 px-4 pt-3 pb-28">
        <PlayHistory />
      </div>

      {/* CTA sticky sopra la bottom nav */}
      <div className="fixed bottom-[calc(var(--size-mobile-nav,56px)+8px)] left-0 right-0 px-4 z-20">
        <GradientButton
          fullWidth
          size="lg"
          onClick={() => setSheetOpen(true)}
          data-testid="new-play-record-btn"
        >
          {t('playRecords.index.hero.cta')}
        </GradientButton>
      </div>

      <NewPlayRecordSheet open={sheetOpen} onOpenChange={setSheetOpen} />
    </div>
  );
}

function PlayRecordsContent() {
  const searchParams = useSearchParams();
  // Canonical stats entry per route-consolidation #5039: the standalone
  // /play-records/stats route redirects here with ?tab=stats.
  if (searchParams?.get('tab') === 'stats') {
    return <StatisticsView />;
  }
  return <RecordsListView />;
}

export default function PlayRecordsPage() {
  return (
    <Suspense fallback={<div className="min-h-full bg-[var(--bg)]" />}>
      <PlayRecordsContent />
    </Suspense>
  );
}
