import { Suspense } from 'react';
import { Metadata } from 'next';
import { StatsOverview } from '@/components/admin/dashboard/stats-overview';
import { SharedGamesBlock } from '@/components/admin/dashboard/shared-games-block';
import { UserManagementBlock } from '@/components/admin/dashboard/user-management-block';

export const metadata: Metadata = {
  title: 'Admin Dashboard | MeepleAI',
  description: 'Manage shared games, users, and platform analytics',
};

export default function AdminDashboardPage() {
  return (
    <div className="flex-1 flex flex-col overflow-hidden bg-background">
      <div className="flex-1 overflow-y-auto">
        <div className="container max-w-7xl mx-auto p-6 space-y-8">
        {/* Page Header */}
        <div className="border-b pb-6">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">
            Dashboard
          </h1>
          <p className="text-muted-foreground mt-2">
            Manage shared games, users, and platform analytics
          </p>
        </div>

        {/* Block 1: Collection Overview (Stats) */}
        <Suspense fallback={<div className="animate-pulse space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
            {[...Array(4)].map((_, i) => (
              <div key={i} className="h-[180px] bg-white/40 backdrop-blur-sm rounded-2xl border border-slate-200/60" />
            ))}
          </div>
        </div>}>
          <StatsOverview />
        </Suspense>

{/* Block 2: Shared Games - Approval Queue */}
        <Suspense fallback={<div className="animate-pulse h-[400px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60" />}>
          <SharedGamesBlock />
        </Suspense>

{/* Block 3: User Management */}
        <Suspense fallback={<div className="animate-pulse h-[400px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60" />}>
          <UserManagementBlock />
        </Suspense>

        {/* Future blocks can be added here */}
        {/* Block 4: Analytics (Future) */}
        {/* Block 5: System Health (Future) */}
        </div>
      </div>
    </div>
  );
}
