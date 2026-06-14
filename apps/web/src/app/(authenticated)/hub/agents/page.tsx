/**
 * /hub/agents — retired legacy route (Issue #2153).
 *
 * The community agents catalog now lives at the canonical `/agents`. This
 * runtime redirect preserves back-links from chat, email, and external
 * sources. The previous `HubCatalogView<PopularAgent>` implementation was
 * a duplicate of `/agents/page.tsx`; consolidating reduces drift risk.
 *
 * Companion redirects retired in the same sweep:
 *   - /hub               → /games?tab=discover (already in place since #2190)
 *   - /hub/games/[id]    → /games/[id]
 *   - /hub/toolkits      → /toolkits (already in place since #1480)
 *
 * Kept as a runtime redirect (not pure next.config.js) so that the
 * `(authenticated)` chrome stays mounted during the hop — short-circuiting
 * to a config redirect would lose UserShell continuity for users coming
 * from authenticated contexts.
 */

import { redirect } from 'next/navigation';

export default function HubAgentsLegacyRedirect(): never {
  redirect('/agents');
}
