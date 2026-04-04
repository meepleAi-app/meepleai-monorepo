'use client';

import { useQuery } from '@tanstack/react-query';
import { useRouter } from 'next/navigation';

import { cn } from '@/lib/utils';

interface Alert {
  id: string;
  isActive: boolean;
  severity: string;
}

/**
 * ServiceHealthBadge - Compact indicator showing count of active system alerts.
 *
 * Color coding:
 * - Green:  0 active alerts
 * - Yellow: 1–2 active alerts
 * - Red:    3+ active alerts
 *
 * Polls every 60 seconds. Navigates to /admin/monitor/services on click.
 */
export function ServiceHealthBadge() {
  const router = useRouter();

  const { data: alerts = [] } = useQuery<Alert[]>({
    queryKey: ['admin', 'active-alerts'],
    queryFn: async () => {
      const res = await fetch('/api/v1/admin/alerts?activeOnly=true');
      if (!res.ok) return [];
      return res.json();
    },
    refetchInterval: 60_000,
    retry: false,
  });

  const activeCount = alerts.length;

  const dotColor =
    activeCount === 0 ? 'bg-green-500' : activeCount <= 2 ? 'bg-yellow-500' : 'bg-red-500';

  return (
    <button
      onClick={() => router.push('/admin/monitor/services')}
      className="flex items-center gap-1.5 rounded-md px-2 py-1 transition-colors hover:bg-muted/50"
      title={`${activeCount} active alert${activeCount !== 1 ? 's' : ''}`}
      data-testid="service-health-badge"
    >
      <span className={cn('h-2 w-2 rounded-full', dotColor)} />
      {activeCount > 0 && (
        <span className="text-xs font-medium text-muted-foreground">{activeCount}</span>
      )}
    </button>
  );
}
