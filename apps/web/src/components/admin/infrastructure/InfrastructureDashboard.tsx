'use client';

import { useCallback, useState } from 'react';

import { AlertTriangle, RefreshCw } from 'lucide-react';
import { toast } from 'sonner';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import {
  useInfraServices,
  useRestartService,
  useTriggerHealthCheck,
} from '@/hooks/admin/use-infrastructure';

import { PipelineTest } from './PipelineTest';
import { RestartModal } from './RestartModal';
import { ServiceDetailPanel } from './ServiceDetailPanel';
import { ServiceGrid } from './ServiceGrid';

interface InfrastructureDashboardProps {
  isSuperAdmin: boolean;
}

export function InfrastructureDashboard({ isSuperAdmin }: InfrastructureDashboardProps) {
  const { data, isLoading, error, refetch } = useInfraServices();
  const healthCheck = useTriggerHealthCheck();
  const restart = useRestartService();

  const [selectedService, setSelectedService] = useState<string | null>(null);
  const [restartTarget, setRestartTarget] = useState<string | null>(null);

  const services = data?.services ?? [];

  const handleCheck = useCallback(
    (name: string) => {
      healthCheck.mutate(name, {
        onSuccess: result => {
          toast.success(`Health check: ${result.status}`, {
            description: result.details ?? `${result.latencyMs}ms`,
          });
        },
        onError: () => {
          toast.error(`Health check failed for ${name}`);
        },
      });
    },
    [healthCheck]
  );

  const handleRestartRequest = useCallback((name: string) => {
    setRestartTarget(name);
  }, []);

  const handleRestartConfirm = useCallback(() => {
    if (!restartTarget) return;
    restart.mutate(restartTarget, {
      onSuccess: () => {
        toast.success(`${restartTarget} is restarting`);
        setRestartTarget(null);
      },
      onError: () => {
        toast.error(`Failed to restart ${restartTarget}`);
      },
    });
  }, [restartTarget, restart]);

  const handleConfigRequest = useCallback((name: string) => {
    setSelectedService(name);
  }, []);

  // Error state
  if (error) {
    return (
      <div className="rounded-lg border border-red-200 dark:border-red-900/50 bg-red-50 dark:bg-red-900/20 p-4 flex items-center gap-3">
        <AlertTriangle className="h-5 w-5 text-red-500 shrink-0" />
        <div className="flex-1">
          <p className="text-sm font-medium text-red-800 dark:text-red-200">
            Infrastructure API unreachable
          </p>
          <p className="text-xs text-red-600 dark:text-red-400 mt-0.5">
            {error instanceof Error ? error.message : 'Unable to fetch service status'}
          </p>
        </div>
        <Button variant="outline" size="sm" onClick={() => refetch()}>
          <RefreshCw className="h-3.5 w-3.5 mr-1.5" />
          Retry
        </Button>
      </div>
    );
  }

  // Loading state
  if (isLoading) {
    return (
      <div className="space-y-5">
        <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
          {Array.from({ length: 4 }).map((_, i) => (
            <Skeleton key={i} className="h-36 rounded-lg" />
          ))}
        </div>
        <Skeleton className="h-20 rounded-lg" />
      </div>
    );
  }

  return (
    <div className="space-y-5">
      <ServiceGrid
        services={services}
        isSuperAdmin={isSuperAdmin}
        onSelect={setSelectedService}
        onCheck={handleCheck}
        onRestart={handleRestartRequest}
        onConfig={handleConfigRequest}
        isCheckPending={healthCheck.isPending}
      />

      <PipelineTest />

      {selectedService && (
        <ServiceDetailPanel
          serviceName={selectedService}
          isSuperAdmin={isSuperAdmin}
          onClose={() => setSelectedService(null)}
        />
      )}

      <RestartModal
        serviceName={restartTarget}
        open={restartTarget !== null}
        onOpenChange={open => {
          if (!open) setRestartTarget(null);
        }}
        onConfirm={handleRestartConfirm}
        isPending={restart.isPending}
      />
    </div>
  );
}
