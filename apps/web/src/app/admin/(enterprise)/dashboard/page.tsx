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
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-amber-50/30 to-orange-50/20">
      {/* Subtle paper texture overlay */}
      <div
        className="fixed inset-0 opacity-[0.015] pointer-events-none"
        style={{
          backgroundImage: `url("data:image/svg+xml,%3Csvg width='200' height='200' xmlns='http://www.w3.org/2000/svg'%3E%3Cfilter id='noise'%3E%3CfeTurbulence type='fractalNoise' baseFrequency='0.9' numOctaves='4' /%3E%3C/filter%3E%3Crect width='100%25' height='100%25' filter='url(%23noise)' /%3E%3C/svg%3E")`,
        }}
      />
      <div className="relative max-w-[1600px] mx-auto px-4 sm:px-6 lg:px-8 py-8">
        <div className="space-y-10 pb-12">
        {/* Page Header */}
        <div className="relative">
          <div className="absolute inset-0 bg-gradient-to-r from-amber-500/10 via-orange-500/5 to-transparent rounded-2xl blur-3xl" />
          <div className="relative">
            <h1 className="font-quicksand font-bold text-4xl md:text-5xl text-slate-900 tracking-tight">
              Admin Dashboard
            </h1>
            <p className="mt-2 font-nunito text-lg text-slate-600">
              Manage your community's board game collection and users
            </p>
          </div>
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

        {/* Divider */}
        <div className="border-t border-amber-200/40" />

        {/* Block 2: Shared Games - Approval Queue */}
        <Suspense fallback={<div className="animate-pulse h-[400px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60" />}>
          <SharedGamesBlock />
        </Suspense>

        {/* Divider */}
        <div className="border-t border-amber-200/40" />

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
