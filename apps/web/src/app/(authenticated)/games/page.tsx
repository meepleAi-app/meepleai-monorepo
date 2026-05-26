/**
 * /games — redirect to canonical `/library` (Issue #1521).
 *
 * `/games` was a multi-tab hub (library / catalog / kb). The `library` tab was
 * the only one backed by the SP4 mockup (`sp4-games-index` = the library view);
 * `/library` (LibraryHub) is the canonical route for that surface. The catalog
 * and kb tabs were placeholders (kb already redirected to /library elsewhere).
 *
 * Per the route-consolidation decision (mirrors #1480 /hub/toolkits → /toolkits),
 * `/games` now redirects to `/library`. The `/games/[id]` game-detail subroute is
 * unaffected (separate route segment).
 */

import { redirect } from 'next/navigation';

export default function GamesLegacyRedirect(): never {
  redirect('/library');
}
