'use client';

import { InfrastructureDashboard } from '@/components/admin/infrastructure/InfrastructureDashboard';
import { useAdminRole } from '@/hooks/useAdminRole';

export default function InfrastructurePage() {
  const { isSuperAdmin } = useAdminRole();

  return (
    <div className="space-y-5 p-4 sm:p-6">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold">AI Infrastructure</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Monitor service health, test connectivity, and manage AI infrastructure
        </p>
      </div>
      <InfrastructureDashboard isSuperAdmin={isSuperAdmin} />
    </div>
  );
}
