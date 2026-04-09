/**
 * Legacy agent page redirect — S4 (library-to-game epic)
 *
 * This route used to render a standalone agent configuration + chat view.
 * It's now superseded by the "AI Chat" tab of the unified game detail page.
 * We perform a server-side 307 redirect so bookmarks and shared URLs keep
 * working while directing users to the new location.
 *
 * Reference: docs/superpowers/specs/2026-04-09-library-to-game-epic-design.md §4.4 CR-I4
 */

import { redirect } from 'next/navigation';

export default async function AgentLegacyRedirect({
  params,
}: {
  params: Promise<{ gameId: string }>;
}) {
  const { gameId } = await params;
  redirect(`/library/games/${gameId}?tab=aiChat`);
}
