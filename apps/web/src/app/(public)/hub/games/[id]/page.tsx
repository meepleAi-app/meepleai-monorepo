/**
 * /hub/games/[id] — redirect to canonical detail at /shared-games/[id].
 *
 * Issue #2043 Bug 3: clicking a card in `/hub/games` (the public catalog)
 * was producing 404 because `HubGameCard` linked to `/games/{id}` and no
 * route handled `/hub/games/{id}`. The canonical SP3 detail page already
 * lives at `/shared-games/[id]` (Wave A.4 #603), so this route is a thin
 * forwarder that keeps the `/hub/games/...` URL family consistent with
 * the catalog without rewriting the existing detail surface.
 *
 * 307 (not permanent) so we can promote `/hub/games/[id]` to the canonical
 * path later without browsers caching a 308 forever.
 */

import { redirect } from 'next/navigation';

interface PageProps {
  readonly params: Promise<{ id: string }>;
}

export default async function HubGameDetailRedirectPage({ params }: PageProps): Promise<never> {
  const { id } = await params;
  redirect(`/shared-games/${id}`);
}
