/**
 * /hub/games/[id] — retired legacy detail route (Issue #2153).
 *
 * The previous implementation (#2118) was a thin mirror of
 * `/shared-games/[id]` rendered inside the authenticated shell to preserve
 * chrome continuity during intra-`/hub/*` navigation. With the broader
 * `/hub/*` retirement (this issue), the chrome-continuity justification
 * disappears — the canonical authenticated game detail lives at
 * `/games/[id]` (private library + shared catalog merged view).
 *
 * Companion redirects retired in the same sweep:
 *   - /hub               → /games?tab=discover (since #2190)
 *   - /hub/agents        → /agents
 *   - /hub/toolkits      → /toolkits (since #1480)
 *
 * Kept as a runtime redirect (not pure next.config.js) so the
 * `(authenticated)` UserShell stays mounted during the hop. The `[id]`
 * param is forwarded transparently — `/games/[id]` performs its own
 * `notFound()` on missing detail.
 */

import { redirect } from 'next/navigation';

interface PageProps {
  readonly params: Promise<{ readonly id: string }>;
}

export default async function HubGameDetailLegacyRedirect({ params }: PageProps): Promise<never> {
  const { id } = await params;
  redirect(`/games/${id}`);
}
