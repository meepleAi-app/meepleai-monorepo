/**
 * Guest Landing Page — /join/[token]
 *
 * Game Night Improvvisata — Task 18
 *
 * PUBLIC page (no auth required). Allows guests to join a live session
 * by entering their name. After joining, shows a read-only scoreboard
 * and score proposal form.
 *
 * Middleware note: /join/* is whitelisted in PUBLIC_PREFIXES in middleware.ts.
 *
 * Issue #2152 Bundle C: dynamic-param vocabulary alignment. Folder slug
 * renamed `[inviteToken]` → `[token]` to match the broader "[token] =
 * opaque secret" convention. The `InviteTokenResponse.inviteToken` DTO
 * field is preserved (API contract — separate scope).
 */

import { use, Suspense } from 'react';

import { GuestJoinView } from './GuestJoinView';

interface GuestJoinPageProps {
  params: Promise<{ token: string }>;
}

/**
 * Server-compatible wrapper that unwraps the async `params` with `use()`.
 * The inner GuestJoinView is a 'use client' component containing all the
 * interactive logic. This split makes the inner component independently
 * testable with a plain string prop.
 */
export default function GuestJoinPage({ params }: GuestJoinPageProps) {
  const { token } = use(params);

  return (
    <Suspense
      fallback={
        <main className="min-h-screen bg-gradient-to-br from-amber-50 to-orange-50 flex items-center justify-center">
          <p className="font-nunito text-muted-foreground">Caricamento sessione...</p>
        </main>
      }
    >
      <GuestJoinView token={token} />
    </Suspense>
  );
}
