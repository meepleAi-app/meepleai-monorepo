import { Suspense } from 'react';
import { Metadata } from 'next';
import AdminDashboardV2Client from './dashboard-v2-client';

export const metadata: Metadata = {
  title: 'Admin Dashboard v2 | MeepleAI',
  description: 'Dashboard amministrativa MeepleAI con panoramica sistema e azioni rapide',
};

export default function AdminDashboardV2Page() {
  return (
    <Suspense
      fallback={
        <div className="flex items-center justify-center min-h-screen">
          <div className="animate-pulse text-muted-foreground">
            Caricamento dashboard...
          </div>
        </div>
      }
    >
      <AdminDashboardV2Client />
    </Suspense>
  );
}
