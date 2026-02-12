'use client';

import { useQuery } from '@tanstack/react-query';
import { Users, Package, TrendingUp, CheckCircle } from 'lucide-react';
import Link from 'next/link';
import { StatCard } from '@/components/ui/data-display/stat-card';
import { adminClient } from '@/lib/api/admin-client';

export function StatsOverview() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-stats'],
    queryFn: () => adminClient.getStats(),
  });

  return (
    <div className="space-y-6">
      {/* Block Header with Navigation */}
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-semibold text-foreground">
          Collection Overview
        </h2>
        <Link
          href="/admin/collection/overview"
          className="text-sm text-primary hover:text-primary/80 font-medium transition-colors"
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
