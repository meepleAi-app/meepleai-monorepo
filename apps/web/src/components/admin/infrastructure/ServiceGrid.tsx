'use client';

import type { AiServiceStatus } from '@/lib/api/clients/infrastructureClient';

import { ServiceCard } from './ServiceCard';

interface ServiceGridProps {
  services: AiServiceStatus[];
  isSuperAdmin: boolean;
  onSelect: (name: string) => void;
  onCheck: (name: string) => void;
  onRestart: (name: string) => void;
  onConfig: (name: string) => void;
  isCheckPending?: boolean;
}

export function ServiceGrid({
  services,
  isSuperAdmin,
  onSelect,
  onCheck,
  onRestart,
  onConfig,
  isCheckPending,
}: ServiceGridProps) {
  return (
    <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
      {services.map(service => (
        <ServiceCard
          key={service.name}
          service={service}
          isSuperAdmin={isSuperAdmin}
          onSelect={onSelect}
          onCheck={onCheck}
          onRestart={onRestart}
          onConfig={onConfig}
          isCheckPending={isCheckPending}
        />
      ))}
    </div>
  );
}
