/**
 * KB Hub route — /library/[gameId]/kb (Issue #1481).
 *
 * Server component: validates params and renders the client orchestrator.
 * The actual page logic lives in `_content.tsx` to allow direct testing
 * of the orchestrator without route-level params resolution.
 */

import { KbHubContent } from './_content';

export default async function KbHubRoute({ params }: { params: Promise<{ gameId: string }> }) {
  const { gameId } = await params;
  return <KbHubContent gameId={gameId} />;
}
