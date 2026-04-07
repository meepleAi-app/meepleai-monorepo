'use client';

import { Database, RefreshCw, Server, Settings, Stethoscope } from 'lucide-react';

import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import type { AiServiceStatus } from '@/lib/api/clients/infrastructureClient';

const STATUS_DOT: Record<string, string> = {
  Healthy: 'bg-green-500',
  Degraded: 'bg-yellow-500',
  Down: 'bg-red-500',
  Restarting: 'bg-blue-500',
  Unknown: 'bg-gray-400',
};

interface ServiceCardProps {
  service: AiServiceStatus;
  isSuperAdmin: boolean;
  onSelect: (name: string) => void;
  onCheck: (name: string) => void;
  onRestart: (name: string) => void;
  onConfig: (name: string) => void;
  isCheckPending?: boolean;
}

export function ServiceCard({
  service,
  isSuperAdmin,
  onSelect,
  onCheck,
  onRestart,
  onConfig,
  isCheckPending,
}: ServiceCardProps) {
  const Icon = service.type === 'ai' ? Server : Database;
  const dotColor = STATUS_DOT[service.status] ?? STATUS_DOT.Unknown;

  const cooldownActive =
    service.cooldownRemainingSeconds !== null && service.cooldownRemainingSeconds > 0;
  const restartDisabled = !isSuperAdmin || cooldownActive;

  const restartTooltip = !isSuperAdmin
    ? 'Requires SuperAdmin'
    : cooldownActive
      ? `Cooldown: ${service.cooldownRemainingSeconds}s remaining`
      : 'Restart service';

  const configTooltip = !isSuperAdmin ? 'Requires SuperAdmin' : 'Configure service';

  return (
    <div
      className="group relative cursor-pointer rounded-lg border border-slate-200/60 dark:border-zinc-700/60 bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl p-4 transition-shadow hover:shadow-md"
      onClick={() => onSelect(service.name)}
      role="button"
      tabIndex={0}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          onSelect(service.name);
        }
      }}
    >
      {/* Header */}
      <div className="flex items-center gap-2 mb-3">
        <Icon className="h-4 w-4 text-muted-foreground" />
        <span className="text-sm font-medium truncate">{service.displayName}</span>
        <span className={`ml-auto h-2.5 w-2.5 rounded-full shrink-0 ${dotColor}`} />
      </div>

      {/* Metrics */}
      <div className="grid grid-cols-3 gap-2 text-center mb-3">
        <div>
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Uptime</p>
          <p className="text-xs font-semibold tabular-nums">{service.uptime}</p>
        </div>
        <div>
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Latency</p>
          <p className="text-xs font-semibold tabular-nums">{service.avgLatencyMs}ms</p>
        </div>
        <div>
          <p className="text-[10px] uppercase tracking-wider text-muted-foreground">Err 24h</p>
          <p className="text-xs font-semibold tabular-nums">
            {(service.errorRate24h * 100).toFixed(1)}%
          </p>
        </div>
      </div>

      {/* Actions */}
      <TooltipProvider delayDuration={200}>
        <div
          className="flex items-center gap-1"
          onClick={e => e.stopPropagation()}
          onKeyDown={e => e.stopPropagation()}
        >
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="sm"
                className="h-7 w-7 p-0"
                onClick={() => onCheck(service.name)}
                disabled={isCheckPending}
              >
                <Stethoscope className="h-3.5 w-3.5" />
                <span className="sr-only">Health check</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              Run health check
            </TooltipContent>
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="sm"
                className="h-7 w-7 p-0"
                onClick={() => onRestart(service.name)}
                disabled={restartDisabled}
              >
                <RefreshCw className="h-3.5 w-3.5" />
                <span className="sr-only">Restart</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              {restartTooltip}
            </TooltipContent>
          </Tooltip>

          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                variant="ghost"
                size="sm"
                className="h-7 w-7 p-0"
                onClick={() => onConfig(service.name)}
                disabled={!isSuperAdmin}
              >
                <Settings className="h-3.5 w-3.5" />
                <span className="sr-only">Configure</span>
              </Button>
            </TooltipTrigger>
            <TooltipContent side="bottom" className="text-xs">
              {configTooltip}
            </TooltipContent>
          </Tooltip>
        </div>
      </TooltipProvider>
    </div>
  );
}
