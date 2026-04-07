import { type Metadata } from 'next';

import { InfrastructureDashboard } from '@/components/admin/infrastructure/InfrastructureDashboard';

export const metadata: Metadata = {
  title: 'AI Infrastructure',
  description: 'Monitor and manage AI services infrastructure',
};

export default function InfrastructurePage() {
  return (
    <div className="space-y-5 p-4 sm:p-6">
      <div>
        <h1 className="font-quicksand text-xl sm:text-2xl font-bold">AI Infrastructure</h1>
        <p className="mt-0.5 text-sm text-muted-foreground">
          Monitor service health, test connectivity, and manage AI infrastructure
        </p>
      </div>
      <InfrastructureDashboard isSuperAdmin={true} />
    </div>
  );
}
