'use client';

import { usePathname, useSearchParams } from 'next/navigation';

const MONITOR_BASE = '/admin/monitor';

/** Maps ?tab= query param values to display labels. */
const TAB_LABELS: ReadonlyArray<readonly [string, string]> = [
  ['alerts', 'Alerts'],
  ['cache', 'Metrics'],
  ['infra', 'Infrastructure'],
  ['command', 'Command Center'],
  ['testing', 'Testing'],
  ['mau', 'MAU'],
  ['containers', 'Containers'],
  ['logs', 'Logs'],
  ['grafana', 'Grafana'],
  ['export', 'Bulk Export'],
  ['email', 'Email'],
  ['history', 'History'],
  ['events', 'Events'],
];

/** Maps sub-route path segments to display labels. */
const SUBROUTE_LABELS: ReadonlyArray<readonly [string, string]> = [
  [`${MONITOR_BASE}/containers`, 'Containers'],
  [`${MONITOR_BASE}/logs`, 'Logs'],
  [`${MONITOR_BASE}/grafana`, 'Grafana'],
  [`${MONITOR_BASE}/operations`, 'Infrastructure'],
  [`${MONITOR_BASE}/services`, 'Infrastructure'],
  [`${MONITOR_BASE}/service-calls`, 'Infrastructure'],
  [`${MONITOR_BASE}/mau`, 'MAU'],
];

function labelFor(pathname: string, tab: string | null): string {
  // Sub-route match takes precedence over query param
  if (pathname !== MONITOR_BASE) {
    const hit = SUBROUTE_LABELS.find(
      ([href]) => pathname === href || pathname.startsWith(`${href}/`)
    );
    if (hit) return hit[1];
  }

  // Query param match
  if (tab) {
    const hit = TAB_LABELS.find(([id]) => id === tab);
    if (hit) return hit[1];
  }

  // Default: first tab = Alerts
  return 'Alerts';
}

export function MonitorCrumbs() {
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab');
  const label = labelFor(pathname, tab);

  return (
    <nav aria-label="Breadcrumb">
      <div className="mt-0.5 font-mono text-[11px] text-muted-foreground">
        Admin · Monitor · {label}
      </div>
    </nav>
  );
}
