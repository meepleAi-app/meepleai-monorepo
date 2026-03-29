/**
 * Guest Session View — /join/session/[code]
 *
 * Phase 5: Game Night — Task 5
 *
 * PUBLIC page (no auth required). Loads a live session by session code
 * and shows a read-only view with game name, scoreboard, and basic info.
 * Designed for QR code joiners scanning from the host's phone.
 */

import { use, Suspense } from 'react';

import { GuestSessionView } from './guest-session-view';

interface GuestSessionPageProps {
  params: Promise<{ code: string }>;
}

export default function GuestSessionPage({ params }: GuestSessionPageProps) {
  const { code } = use(params);

  return (
    <Suspense
      fallback={
        <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 dark:from-slate-900 dark:to-slate-800 flex items-center justify-center">
          <p className="font-nunito text-gray-600 dark:text-gray-400">Caricamento sessione...</p>
        </main>
      }
    >
      <GuestSessionView code={code} />
    </Suspense>
  );
}
