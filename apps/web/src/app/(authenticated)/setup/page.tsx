/**
 * Setup Guide Page — server wrapper.
 *
 * `/setup` is a protected route (see `@/lib/routing/protected-routes`) —
 * the edge proxy already redirects unauthenticated visitors to `/login`
 * before this component ever renders, so no client-side auth gate is
 * needed here (mirrors `toolkit/history/page.tsx`).
 */
import { SetupView } from './_components/SetupView';

export default function SetupPage() {
  return <SetupView />;
}
