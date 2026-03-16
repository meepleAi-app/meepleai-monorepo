'use client';

import { useEffect, useState } from 'react';

import { Activity, RefreshCw } from 'lucide-react';

type ServiceStatus = 'healthy' | 'degraded' | 'down' | 'unknown';

interface ServiceHealth {
  name: string;
  status: ServiceStatus;
}

interface ApiServiceHealth {
  serviceName: string;
  status: string;
  description?: string | null;
  isCritical: boolean;
}

interface ApiHealthResponse {
  overallStatus: string;
  services: ApiServiceHealth[];
  timestamp: string;
}

const STATUS_CONFIG: Record<ServiceStatus, { dot: string; label: string }> = {
  healthy: { dot: 'bg-emerald-500', label: 'Healthy' },
  degraded: { dot: 'bg-amber-500', label: 'Degraded' },
  down: { dot: 'bg-red-500', label: 'Down' },
  unknown: { dot: 'bg-slate-400', label: 'Unknown' },
};

/** Map ASP.NET HealthStatus string to our UI status */
function mapApiStatus(apiStatus: string): ServiceStatus {
  switch (apiStatus.toLowerCase()) {
    case 'healthy':
      return 'healthy';
    case 'degraded':
      return 'degraded';
    case 'unhealthy':
      return 'down';
    default:
      return 'unknown';
  }
}

/** Display-friendly name for backend service names */
function formatServiceName(name: string): string {
  const nameMap: Record<string, string> = {
    postgres: 'PostgreSQL',
    redis: 'Redis',
    'redis-rate-limiting': 'Redis Rate Limit',
    'shared-catalog-fts': 'Catalog FTS',
    configuration: 'Configuration',
    n8n: 'n8n',
    openrouter: 'OpenRouter',
    'embedding-service': 'Embedding',
    'reranker-service': 'Reranker',
    'unstructured-service': 'Unstructured',
    'smoldocling-service': 'SmolDocling',
    'bgg-api': 'BGG API',
    'oauth-providers': 'OAuth',
    'email-smtp': 'Email SMTP',
    grafana: 'Grafana',
    prometheus: 'Prometheus',
    's3-storage': 'S3 Storage',
  };
  return nameMap[name] ?? name;
}

const REFRESH_INTERVAL_MS = 60_000;

/** Critical services shown first regardless of backend response */
const CRITICAL_SERVICE_NAMES = ['postgres', 'redis'];

export function SystemHealthCard() {
  const [services, setServices] = useState<ServiceHealth[]>([
    { name: 'API', status: 'unknown' },
    { name: 'PostgreSQL', status: 'unknown' },
    { name: 'Redis', status: 'unknown' },
  ]);
  const [lastChecked, setLastChecked] = useState<Date | null>(null);
  const [checking, setChecking] = useState(false);

  async function checkHealth() {
    setChecking(true);
    try {
      const res = await fetch('/api/v1/admin/operations/health', {
        signal: AbortSignal.timeout(10_000),
      });

      if (res.ok) {
        const data: ApiHealthResponse = await res.json();

        // API is reachable — always healthy
        const mapped: ServiceHealth[] = [{ name: 'API', status: 'healthy' }];

        // Add critical services first, then the rest
        const critical = data.services.filter(s => CRITICAL_SERVICE_NAMES.includes(s.serviceName));
        const others = data.services.filter(s => !CRITICAL_SERVICE_NAMES.includes(s.serviceName));

        for (const svc of [...critical, ...others]) {
          mapped.push({
            name: formatServiceName(svc.serviceName),
            status: mapApiStatus(svc.status),
          });
        }

        setServices(mapped);
      } else {
        // API responded but with error — mark API degraded
        setServices(prev =>
          prev.map(s => (s.name === 'API' ? { ...s, status: 'degraded' as const } : s))
        );
      }
    } catch {
      // API unreachable
      setServices(prev =>
        prev.map(s => (s.name === 'API' ? { ...s, status: 'down' as const } : s))
      );
    } finally {
      setLastChecked(new Date());
      setChecking(false);
    }
  }

  useEffect(() => {
    checkHealth();
    const interval = setInterval(checkHealth, REFRESH_INTERVAL_MS);
    return () => clearInterval(interval);
  }, []);

  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2.5">
          <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-emerald-100/80 dark:bg-emerald-900/30">
            <Activity className="h-4 w-4 text-emerald-700 dark:text-emerald-400" />
          </div>
          <p className="font-quicksand text-sm font-semibold text-foreground">System Health</p>
        </div>
        <button
          onClick={() => checkHealth()}
          disabled={checking}
          className="p-1.5 rounded-lg text-muted-foreground hover:text-foreground hover:bg-slate-100 dark:hover:bg-zinc-700/50 transition-colors disabled:opacity-50"
          aria-label="Refresh health status"
        >
          <RefreshCw className={`h-3.5 w-3.5 ${checking ? 'animate-spin' : ''}`} />
        </button>
      </div>

      <div className="space-y-2.5">
        {services.map(service => {
          const config = STATUS_CONFIG[service.status];
          return (
            <div key={service.name} className="flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className={`h-2 w-2 rounded-full ${config.dot}`} />
                <span className="text-xs font-medium text-foreground">{service.name}</span>
              </div>
              <span className="text-[10px] text-muted-foreground">{config.label}</span>
            </div>
          );
        })}
      </div>

      {lastChecked && (
        <p className="mt-3 text-[10px] text-muted-foreground/60">
          Last checked:{' '}
          {lastChecked.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })}
        </p>
      )}
    </div>
  );
}
