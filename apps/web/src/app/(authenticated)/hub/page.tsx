/**
 * /hub — legacy section index.
 *
 * Issue #2190 (2026-06-13): redirects directly to the canonical multi-tab hub
 * `/games?tab=discover` (Asse D P2 #1899) instead of the retired `/hub/games`
 * list page. The full `/hub/*` retirement is tracked by Draft 13 (Fowler).
 *
 * Kept as a runtime redirect (not pure next.config.js) because `/hub/agents`
 * and `/hub/toolkits` siblings still live under `(authenticated)` and need
 * the UserShell chrome — collapsing this to a config redirect would short-
 * circuit chrome continuity for those siblings.
 */

import { redirect } from 'next/navigation';

export default function HubIndexPage(): never {
  redirect('/games?tab=discover');
}
