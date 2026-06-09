/**
 * /hub — section index. Redirects to `/hub/games` (default tab).
 *
 * Issue #2043 Bug 2: a direct visit to `/hub` returned 404 because no `page.tsx`
 * existed at the section root — only the `games/` subroute was defined. The hub
 * is currently a single-section landing (public catalog); when more sections
 * (toolkits, agents, kb) land here, this redirect can be replaced by a real
 * index page or a server-side decision based on the visitor's profile.
 *
 * Temporary 307 (not permanent) — see ADR / discussion if/when section landing
 * becomes a real page so browsers don't cache 308 forever.
 */

import { redirect } from 'next/navigation';

export default function HubIndexPage(): never {
  redirect('/hub/games');
}
