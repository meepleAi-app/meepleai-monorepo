import { Suspense } from 'react';
import { Metadata } from 'next';
import { DashboardShell } from '@/components/admin/dashboard/dashboard-shell';
import { StatsOverview } from '@/components/admin/dashboard/stats-overview';
import { SharedGamesSection } from '@/components/admin/dashboard/shared-games-section';
import { UserManagementSection } from '@/components/admin/dashboard/user-management-section';
import { DashboardSkeleton } from '@/components/admin/dashboard/dashboard-skeleton';

export const metadata: Metadata = {
  title: 'Admin Dashboard | MeepleAI',
  description: 'Manage shared games, users, and platform analytics',
};

export default function AdminDashboardPage() {
  return (
    <DashboardShell>
      <div className="space-y-8 pb-12">
        {/* Page Header */}
        <div className="relative">
          <div className="absolute inset-0 bg-gradient-to-r from-amber-500/10 via-orange-500/5 to-transparent rounded-2xl blur-3xl" />
          <div className="relative">
            <h1 className="font-quicksand font-bold text-4xl md:text-5xl text-slate-900 tracking-tight">
              Collection Manager
            </h1>
            <p className="mt-2 font-nunito text-lg text-slate-600">
              Curate your community's board game library
            </p>
          </div>
        </div>

        {/* Stats Overview - Game Box Shelf */}
        <Suspense fallback={<DashboardSkeleton variant="stats" />}>
          <StatsOverview />
        </Suspense>

        {/* Shared Games Management */}
        <Suspense fallback={<DashboardSkeleton variant="table" />}>
          <SharedGamesSection />
        </Suspense>

        {/* User Management */}
        <Suspense fallback={<DashboardSkeleton variant="table" />}>
          <UserManagementSection />
        </Suspense>
      </div>
    </DashboardShell>
  );
}
