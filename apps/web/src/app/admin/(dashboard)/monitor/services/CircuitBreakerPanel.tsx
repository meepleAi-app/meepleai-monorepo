'use client';

import { useQuery } from '@tanstack/react-query';

import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';
import type { CircuitBreakerState } from '@/lib/api/schemas/admin/admin-circuit-breakers.schemas';

const STATE_COLORS: Record<string, string> = {
  Closed: 'bg-green-100 text-green-700',
  Open: 'bg-red-100 text-red-700',
  HalfOpen: 'bg-amber-100 text-amber-700',
};

export function CircuitBreakerPanel() {
  const { data: states, isLoading } = useQuery({
    queryKey: ['admin', 'circuit-breakers'],
    queryFn: () => api.admin.getCircuitBreakerStates(),
    staleTime: 15_000,
    refetchInterval: 30_000,
  });

  if (isLoading)
    return (
      <div className="text-sm text-muted-foreground">Loading circuit breaker states...</div>
    );
  if (!states?.length)
    return <div className="text-sm text-muted-foreground">No circuit breakers registered.</div>;

  const hasIssues = states.some((s: CircuitBreakerState) => s.state !== 'Closed');

  return (
    <div className="space-y-3">
      <div className="flex items-center gap-2">
        <h2 className="font-semibold text-lg">Circuit Breakers</h2>
        {hasIssues && <Badge variant="destructive">Issues detected</Badge>}
      </div>
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3">
        {states.map((cb: CircuitBreakerState) => (
          <div
            key={cb.serviceName}
            className={`border rounded-lg p-3 ${
              cb.state !== 'Closed' ? 'border-red-300 bg-red-50/30 dark:bg-red-950/10' : ''
            }`}
          >
            <div className="flex items-center justify-between mb-2">
              <span className="font-medium text-sm">{cb.serviceName}</span>
              <Badge className={STATE_COLORS[cb.state] ?? 'bg-gray-100 text-gray-700'}>
                {cb.state}
              </Badge>
            </div>
            <div className="text-xs text-muted-foreground space-y-0.5">
              <div>Trips: {cb.tripCount}</div>
              {cb.lastTrippedAt && (
                <div>Last trip: {new Date(cb.lastTrippedAt).toLocaleString('it-IT')}</div>
              )}
              {cb.lastError && (
                <div className="text-red-600 truncate" title={cb.lastError}>
                  Error: {cb.lastError}
                </div>
              )}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}
