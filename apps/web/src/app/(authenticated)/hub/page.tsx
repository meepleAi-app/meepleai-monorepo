/**
 * /hub — section index. Redirects to `/hub/games` (default tab).
 *
 * Authenticated route (moved from `(public)` to `(authenticated)` to fix chrome
 * drift: the Hub voice in `UNIFIED_NAV_ITEMS` is `authOnly` and lives in
 * `TOP_BAR_NAV_IDS` / `BOTTOM_TAB_NAV_IDS`, but the previous public location
 * forced `PublicLayout` / `UnifiedHeader` to render on intra-section navigation,
 * causing the AppTopBar to disappear when crossing `/hub/agents` → `/hub/games`.
 *
 * All three subroutes (`/hub/games`, `/hub/agents`, `/hub/toolkits`) now share
 * the same `UserShell` chrome.
 *
 * Temporary 307 (not permanent) — replaceable with a real section landing
 * (e.g. tabbed switcher) when product owns the spec.
 */

import { redirect } from 'next/navigation';

export default function HubIndexPage(): never {
  redirect('/hub/games');
}
