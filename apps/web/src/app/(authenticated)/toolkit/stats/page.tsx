/**
 * Toolkit Stats Page — server wrapper
 * Uses force-dynamic to avoid DOMMatrix SSR errors during build.
 */
export const dynamic = 'force-dynamic';

import SessionStatsPage from './client';

export default function Page() {
  return <SessionStatsPage />;
}
