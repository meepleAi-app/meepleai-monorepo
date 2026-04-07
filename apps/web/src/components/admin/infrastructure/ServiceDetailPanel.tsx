'use client';

import { Loader2, X } from 'lucide-react';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import { Button } from '@/components/ui/primitives/button';
import { useServiceDependencies } from '@/hooks/admin/use-infrastructure';
import type { ServiceDependency } from '@/lib/api/clients/infrastructureClient';

import { ServiceConfigForm } from './ServiceConfigForm';

const DEP_STATUS_DOT: Record<string, string> = {
  Healthy: 'bg-green-500',
  Degraded: 'bg-yellow-500',
  Down: 'bg-red-500',
  Unknown: 'bg-gray-400',
};

interface ServiceDetailPanelProps {
  serviceName: string;
  isSuperAdmin: boolean;
  onClose: () => void;
}

function DependencyRow({ dep }: { dep: ServiceDependency }) {
  const dotColor = DEP_STATUS_DOT[dep.status] ?? DEP_STATUS_DOT.Unknown;
  return (
    <div className="flex items-center justify-between rounded-md border border-slate-200/40 dark:border-zinc-700/40 px-3 py-2">
      <div className="flex items-center gap-2">
        <span className={`h-2 w-2 rounded-full ${dotColor}`} />
        <span className="text-sm">{dep.displayName}</span>
      </div>
      <span className="text-xs text-muted-foreground tabular-nums">{dep.latencyMs}ms</span>
    </div>
  );
}

export function ServiceDetailPanel({
  serviceName,
  isSuperAdmin,
  onClose,
}: ServiceDetailPanelProps) {
  const { data: depsData, isLoading: depsLoading } = useServiceDependencies(serviceName);

  const deps = depsData?.dependencies ?? [];

  return (
    <div className="rounded-lg border border-slate-200/60 dark:border-zinc-700/60 bg-white/90 dark:bg-zinc-800/90 backdrop-blur-xl p-4">
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <h3 className="text-sm font-semibold">{serviceName}</h3>
        <Button variant="ghost" size="sm" className="h-7 w-7 p-0" onClick={onClose}>
          <X className="h-4 w-4" />
          <span className="sr-only">Close</span>
        </Button>
      </div>

      <Tabs defaultValue="dependencies">
        <TabsList className="mb-3">
          <TabsTrigger value="dependencies" className="text-xs">
            Dependencies
          </TabsTrigger>
          <TabsTrigger value="config" className="text-xs">
            Config
          </TabsTrigger>
        </TabsList>

        <TabsContent value="dependencies">
          {depsLoading ? (
            <div className="flex items-center justify-center py-6">
              <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
            </div>
          ) : deps.length === 0 ? (
            <p className="text-sm text-muted-foreground py-4">No dependencies found.</p>
          ) : (
            <div className="space-y-2">
              {deps.map(dep => (
                <DependencyRow key={dep.name} dep={dep} />
              ))}
            </div>
          )}
        </TabsContent>

        <TabsContent value="config">
          <ServiceConfigForm serviceName={serviceName} isSuperAdmin={isSuperAdmin} />
        </TabsContent>
      </Tabs>
    </div>
  );
}
