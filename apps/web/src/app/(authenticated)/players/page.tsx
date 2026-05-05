/**
 * Players page — /players
 *
 * Wave 4 D1 (Issue #682): thin client shell.
 * All rendering, data-fetching, and FSM logic live in PlayersLibraryView.
 *
 * @see apps/web/src/app/(authenticated)/players/_components/PlayersLibraryView.tsx
 */

'use client';

import { PlayersLibraryView } from './_components/PlayersLibraryView';

export default function PlayersPage() {
  return <PlayersLibraryView />;
}
