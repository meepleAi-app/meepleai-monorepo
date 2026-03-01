/**
 * Sessions Hub Page
 * Issue #5045 — Sessions + MiniNav + ActionBar
 *
 * Handles tab routing via ?tab= search param:
 *   (default)     → Sessioni attive
 *   ?tab=history  → Storico sessioni
 *
 * Content migration from legacy routes is planned for issue #5053.
 */

import { Suspense } from 'react';

import { SessionsContent } from './_content';

export default function SessionsPage() {
  return (
    <Suspense>
      <SessionsContent />
    </Suspense>
  );
}
