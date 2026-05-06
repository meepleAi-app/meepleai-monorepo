/**
 * Session Detail Page — `/sessions/[id]` (Wave D.3 BIG-BANG, Issue #756).
 *
 * Renders the post-game session summary. The legacy live UI driven by
 * `useSessionStore` has been migrated to `/sessions/[id]/live` (D.2 SHIPPED).
 * The orchestrator handles the brownfield FORK: completed/abandoned sessions
 * render the summary; in-progress/paused/setup sessions redirect to /live.
 *
 * Pattern: thin Suspense shell. All hook composition + URL state SSOT live in
 * the client orchestrator (Wave D.2 blueprint).
 *
 * @see docs/frontend/contracts/sessions-id-summary-hooks.md §1 §2
 */

import { Suspense } from 'react';

import { SessionSummaryView } from './_components/SessionSummaryView';

interface SessionPageProps {
  params: Promise<{ id: string }>;
}

function PageFallback(): React.ReactElement {
  return (
    <div
      data-slot="session-summary-page-fallback"
      role="status"
      aria-live="polite"
      className="mx-auto max-w-[1200px] px-4 py-12"
    >
      <div className="h-48 animate-pulse rounded-lg bg-muted/40" />
    </div>
  );
}

export default async function SessionDetailPage({
  params,
}: SessionPageProps): Promise<React.ReactElement> {
  const { id } = await params;

  return (
    <Suspense fallback={<PageFallback />}>
      <SessionSummaryView sessionId={id} />
    </Suspense>
  );
}
