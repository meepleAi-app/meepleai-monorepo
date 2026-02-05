import { Suspense } from 'react';
import { Metadata } from 'next';
import AdminDashboardClient from './dashboard-client';

export const metadata: Metadata = {
  title: 'Admin Dashboard | MeepleAI',
  description: 'Dashboard amministrativa MeepleAI con panoramica sistema e azioni rapide',
};

export default function AdminDashboardPage() {
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
      <AdminDashboardClient />
    </Suspense>
  );
}
