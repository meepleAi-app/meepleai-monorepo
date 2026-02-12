'use client';

import { useQuery } from '@tanstack/react-query';
import { Users, Package, TrendingUp, CheckCircle } from 'lucide-react';
import Link from 'next/link';
import { StatCard } from '@/components/ui/data-display/stat-card';
// TODO: Switch to real API when available
// import { adminClient } from '@/lib/api/admin-client';
import { adminClientMock as adminClient } from '@/lib/api/admin-client-mock';

export function StatsOverview() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-stats'],
    queryFn: () => adminClient.getStats(),
  });

  return (
    <div className="space-y-4">
      {/* Block Header with Navigation */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="h-1 w-12 bg-gradient-to-r from-amber-500 to-orange-500 rounded-full" />
          <h2 className="font-quicksand font-bold text-2xl text-slate-900">
            Collection Overview
          </h2>
        </div>
        <Link
          href="/admin/collection/overview"
          className="font-nunito text-sm text-amber-600 hover:text-amber-700 font-semibold"
        >
          View Details →
        </Link>
      </div>

      {/* Stats Grid - Using StatCard component */}
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard
          label="Shared Games"
          value={data?.totalGames ?? 0}
          trendValue={data?.publishedGames ? `${data.publishedGames} published` : undefined}
          loading={isLoading}
        />
        <StatCard
          label="Community"
          value={data?.totalUsers ?? 0}
          trendValue={data?.activeUsers ? `${data.activeUsers} active` : undefined}
          loading={isLoading}
        />
        <StatCard
          label="Approval Rate"
          value={data?.approvalRate ? `${data.approvalRate}%` : '0%'}
          variant={
            data?.approvalRate && data.approvalRate > 90
              ? 'success'
              : data?.approvalRate && data.approvalRate < 70
              ? 'warning'
              : 'default'
          }
          trendValue={data?.pendingApprovals ? `${data.pendingApprovals} pending` : undefined}
          loading={isLoading}
        />
        <StatCard
          label="Recent Activity"
          value={data?.recentSubmissions ?? 0}
          trendValue="Last 7 days"
          loading={isLoading}
        />
      </div>
    </div>
  );
}
