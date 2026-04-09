/**
 * Legacy toolbox page redirect — S4 (library-to-game epic)
 *
 * This route used to render a standalone toolbox view. It's now superseded
 * by the "Toolbox" tab of the unified game detail page. We perform a
 * server-side 307 redirect so bookmarks and shared URLs keep working.
 *
 * Note: the /library/games/[gameId]/toolkit route is NOT redirected — it's a
 * different feature (session template + live session launcher).
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4 CR-I4
 */

import { redirect } from 'next/navigation';

export default async function ToolboxLegacyRedirect({
  params,
}: {
  params: Promise<{ gameId: string }>;
}) {
  const { gameId } = await params;
  redirect(`/library/games/${gameId}?tab=toolbox`);
}
