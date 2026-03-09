'use client';

import { useState, useEffect } from 'react';

import { ServiceHealthMatrix } from '@/components/admin/ServiceHealthMatrix';
import { api } from '@/lib/api';
import type { ServiceHealthStatus } from '@/lib/api';

export function InfrastructureTab() {
  const [services, setServices] = useState<ServiceHealthStatus[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    api.admin
      .getInfrastructureDetails()
      .then(details => {
        setServices(details?.services ?? []);
      })
      .catch(() => {
        setServices([]);
      })
      .finally(() => {
        setLoading(false);
      });
  }, []);

  return (
    <div className="space-y-6">
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Infrastructure Health
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Real-time status of backend services and dependencies.
        </p>
      </div>
      <ServiceHealthMatrix services={services} loading={loading} />
    </div>
  );
}
