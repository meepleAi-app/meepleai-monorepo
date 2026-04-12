'use client';

/**
 * Play Records Page — Lista partite giocate
 *
 * Mobile-first layout: MobileHeader + PlayHistory + sticky GradientButton.
 */

import { useState } from 'react';

import { BarChart3 } from 'lucide-react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';

import { NewPlayRecordSheet } from '@/components/play-records/NewPlayRecordSheet';
// PlayHistory usa uno Zustand store con `persist` (localStorage) — SSR disabilitato
// per evitare crash React 19 useSyncExternalStore durante hydration.
const PlayHistory = dynamic(
  () => import('@/components/play-records/PlayHistory').then(m => ({ default: m.PlayHistory })),
  { ssr: false }
);
import { GradientButton } from '@/components/ui/buttons/GradientButton';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';

export default function PlayRecordsPage() {
  const router = useRouter();
  const [sheetOpen, setSheetOpen] = useState(false);

  return (
    <div className="flex flex-col min-h-full bg-[var(--gaming-bg-base)]">
      <MobileHeader
        title="Partite Giocate"
        onBack={() => router.back()}
        rightActions={
          <button
            type="button"
            aria-label="Vai alle statistiche"
            onClick={() => router.push('/play-records/stats')}
            className="flex h-9 w-9 items-center justify-center rounded-full text-[var(--gaming-text-secondary)] hover:bg-white/5"
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
          + Registra partita
        </GradientButton>
      </div>

      <NewPlayRecordSheet open={sheetOpen} onOpenChange={setSheetOpen} />
    </div>
  );
}
