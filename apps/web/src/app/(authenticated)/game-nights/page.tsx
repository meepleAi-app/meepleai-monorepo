/**
 * Game Nights Page
 * Issue #33 — P3 Game Night Frontend
 *
 * Lists upcoming and user-organized game nights with tab navigation.
 */

import { Suspense } from 'react';

import { RequireRole } from '@/components/auth/RequireRole';

import { GameNightsContent, GameNightsLoadingSkeleton } from './_content';

export default function GameNightsPage() {
  return (
    <RequireRole allowedRoles={['User', 'Editor', 'Admin']}>
      <Suspense fallback={<GameNightsLoadingSkeleton />}>
        <GameNightsContent />
      </Suspense>
    </RequireRole>
  );
}
