'use client';

import { ArrowRight } from 'lucide-react';
import Link from 'next/link';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { useInfraServices } from '@/hooks/admin/use-infrastructure';
import type { AiServiceStatus } from '@/lib/api/clients/infrastructureClient';

const STATUS_COLORS: Record<string, string> = {
  Healthy: 'bg-green-500',
  Degraded: 'bg-yellow-500',
  Down: 'bg-red-500',
  Restarting: 'bg-blue-500',
  Unknown: 'bg-gray-400',
};

function StatusDot({ service }: { service: AiServiceStatus }) {
  const color = STATUS_COLORS[service.status] ?? STATUS_COLORS.Unknown;
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <span
          className={`inline-block h-2.5 w-2.5 rounded-full ${color}`}
          aria-label={`${service.displayName}: ${service.status}`}
        />
      </TooltipTrigger>
      <TooltipContent side="bottom" className="text-xs">
        {service.displayName} &mdash; {service.status}
      </TooltipContent>
    </Tooltip>
  );
}

function LoadingSkeleton() {
  return (
    <div className="flex items-center gap-1.5">
      {Array.from({ length: 8 }).map((_, i) => (
        <span
          key={i}
          className="inline-block h-2.5 w-2.5 rounded-full bg-gray-300 dark:bg-gray-600 animate-pulse"
        />
      ))}
    </div>
  );
}

export function InfraStatusBar() {
  const { data, isLoading } = useInfraServices();

  const services = data?.services ?? [];
  const healthyCount = services.filter(s => s.status === 'Healthy').length;
  const total = services.length;

  return (
    <div className="flex items-center gap-3 rounded-lg border border-slate-200/60 dark:border-zinc-700/60 bg-white/80 dark:bg-zinc-800/80 px-3 py-2">
      <TooltipProvider delayDuration={200}>
        {isLoading ? (
          <LoadingSkeleton />
        ) : (
          <div className="flex items-center gap-1.5">
            {services.map(svc => (
              <StatusDot key={svc.name} service={svc} />
            ))}
          </div>
        )}
      </TooltipProvider>

      <span className="text-xs font-medium text-muted-foreground whitespace-nowrap">
        {isLoading ? '...' : `${healthyCount}/${total} Healthy`}
      </span>

      <Link
        href="/admin/agents/infrastructure"
        className="ml-auto flex items-center gap-1 text-xs font-medium text-muted-foreground hover:text-foreground transition-colors"
      >
        Manage <ArrowRight className="h-3 w-3" />
      </Link>
    </div>
  );
}
