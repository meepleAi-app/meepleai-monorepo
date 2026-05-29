'use client';

/**
 * Play Records Statistics — standalone route.
 *
 * NOTE: next.config.js redirects `/play-records/stats` → `/play-records?tab=stats`
 * (route consolidation #5039), so this route is normally not reached directly.
 * It delegates to the shared StatisticsView, which the index page also renders
 * inline when the `tab=stats` query param is present.
 */

import { StatisticsView } from '@/components/play-records/StatisticsView';

export default function StatisticsPage() {
  return <StatisticsView />;
}
