/**
 * Guest Landing Page — /join/[inviteToken]
 *
 * Game Night Improvvisata — Task 18
 *
 * PUBLIC page (no auth required). Allows guests to join a live session
 * by entering their name. After joining, shows a read-only scoreboard
 * and score proposal form.
 *
 * Middleware note: /join/* is whitelisted in PUBLIC_PREFIXES in middleware.ts.
 */

import { use, Suspense } from 'react';

import { GuestJoinView } from './GuestJoinView';

interface GuestJoinPageProps {
  params: Promise<{ inviteToken: string }>;
}

/**
 * Server-compatible wrapper that unwraps the async `params` with `use()`.
 * The inner GuestJoinView is a 'use client' component containing all the
 * interactive logic. This split makes the inner component independently
 * testable with a plain string prop.
 */
export default function GuestJoinPage({ params }: GuestJoinPageProps) {
  const { inviteToken } = use(params);

  return (
    <Suspense
      fallback={
        <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 flex items-center justify-center">
          <p className="font-nunito text-gray-600">Caricamento sessione...</p>
        </main>
      }
    >
      <GuestJoinView inviteToken={inviteToken} />
    </Suspense>
  );
}
