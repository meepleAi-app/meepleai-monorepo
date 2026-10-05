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
      {/*
        🔴 #4060 — due difetti in una `bottom`, e il secondo spiega il primo.

        (1) Il banner cookie (`fixed bottom-0 z-50`) copriva questa CTA: misurato con
            `elementFromPoint` sia a 1366x768 sia a 390x844. A z-20 non c'e` ordine di
            DOM che la salvi.

        (2) `--size-mobile-nav` **non esiste**: il token definito in design-tokens.css e`
            `--size-mobile-nav-height` (4.5rem). Il `var()` ricadeva quindi SEMPRE su
            56px mentre la barra ne misura 68 — la CTA finiva 4px dentro la barra.
            Misurato a 390x844: nav top = 776, CTA bottom = 780.
      */}
      <div className="fixed bottom-[calc(var(--size-mobile-nav-height,4.5rem)+8px+var(--cookie-banner-height,0px))] left-0 right-0 px-4 z-20">
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
