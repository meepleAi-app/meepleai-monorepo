'use client';

import { useQuery } from '@tanstack/react-query';
import { ArrowLeft, BarChart3, TrendingUp, Package, Users } from 'lucide-react';
import Link from 'next/link';

import { StatCard } from '@/components/ui/data-display/stat-card';
import { adminClient } from '@/lib/api/admin-client';

export default function CollectionOverviewPage() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-stats', 'overview'],
    queryFn: () => adminClient.getStats({ days: 30 }),
    staleTime: 5 * 60 * 1000,
  });

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 via-amber-50/30 to-orange-50/20 p-8">
      <div className="max-w-[1600px] mx-auto">
        {/* Header */}
        <div className="mb-8">
          <Link
            href="/admin"
            className="inline-flex items-center gap-1.5 text-sm text-slate-500 hover:text-amber-700 transition-colors mb-4"
          >
            <ArrowLeft className="w-4 h-4" />
            Back to Dashboard
          </Link>
          <h1 className="font-quicksand font-bold text-4xl text-slate-900 mb-2">
            Collection Overview
          </h1>
          <p className="font-nunito text-lg text-slate-600">
            Comprehensive statistics and trends for the shared game catalog
          </p>
        </div>

        {/* Primary Stats Grid */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6 mb-8">
          <StatCard
            label="Total Games"
            value={data?.totalGames ?? 0}
            icon={Package}
            trend={data?.publishedGames ? 'neutral' : undefined}
            trendValue={data?.publishedGames ? `${data.publishedGames} published` : undefined}
            loading={isLoading}
          />
          <StatCard
            label="Total Users"
            value={data?.totalUsers ?? 0}
            icon={Users}
            trend={data?.activeUsers ? 'up' : undefined}
            trendValue={data?.activeUsers ? `${data.activeUsers} active` : undefined}
            loading={isLoading}
          />
          <StatCard
            label="Approval Rate"
            value={data?.approvalRate ? `${data.approvalRate}%` : '0%'}
            icon={TrendingUp}
            variant={
              data?.approvalRate && data.approvalRate > 90
                ? 'success'
                : data?.approvalRate && data.approvalRate < 70
                ? 'warning'
                : 'default'
            }
            trend={data?.pendingApprovals ? 'neutral' : undefined}
            trendValue={data?.pendingApprovals ? `${data.pendingApprovals} pending` : undefined}
            loading={isLoading}
          />
          <StatCard
            label="Recent Submissions"
            value={data?.recentSubmissions ?? 0}
            icon={BarChart3}
            trend="neutral"
            trendValue="Last 30 days"
            loading={isLoading}
          />
        </div>

        {/* Secondary Stats */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          {/* Publishing Pipeline */}
          <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-6">
            <h2 className="font-quicksand font-bold text-lg text-slate-900 mb-6">
              Publishing Pipeline
            </h2>
            <div className="space-y-4">
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-emerald-50 to-green-50 rounded-lg border border-emerald-200/60">
                <div>
                  <div className="font-nunito font-semibold text-emerald-900">Published</div>
                  <div className="font-nunito text-sm text-emerald-700">Live in catalog</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-emerald-900">
                  {isLoading ? '...' : data?.publishedGames ?? 0}
                </div>
              </div>
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-amber-50 to-orange-50 rounded-lg border border-amber-200/60">
                <div>
                  <div className="font-nunito font-semibold text-amber-900">Pending Review</div>
                  <div className="font-nunito text-sm text-amber-700">Awaiting approval</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-amber-900">
                  {isLoading ? '...' : data?.pendingApprovals ?? 0}
                </div>
              </div>
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-slate-50 to-gray-50 rounded-lg border border-slate-200/60">
                <div>
                  <div className="font-nunito font-semibold text-slate-900">Pending Games</div>
                  <div className="font-nunito text-sm text-slate-600">Draft submissions</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-slate-900">
                  {isLoading ? '...' : data?.pendingGames ?? 0}
                </div>
              </div>
            </div>
          </div>

          {/* Community Overview */}
          <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-6">
            <h2 className="font-quicksand font-bold text-lg text-slate-900 mb-6">
              Community Overview
            </h2>
            <div className="space-y-4">
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-blue-50 to-cyan-50 rounded-lg border border-blue-200/60">
                <div>
                  <div className="font-nunito font-semibold text-blue-900">Total Users</div>
                  <div className="font-nunito text-sm text-blue-700">All registered accounts</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-blue-900">
                  {isLoading ? '...' : data?.totalUsers ?? 0}
                </div>
              </div>
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-purple-50 to-pink-50 rounded-lg border border-purple-200/60">
                <div>
                  <div className="font-nunito font-semibold text-purple-900">Active Users</div>
                  <div className="font-nunito text-sm text-purple-700">Active in last 30 days</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-purple-900">
                  {isLoading ? '...' : data?.activeUsers ?? 0}
                </div>
              </div>
              <div className="flex items-center justify-between p-4 bg-gradient-to-r from-rose-50 to-pink-50 rounded-lg border border-rose-200/60">
                <div>
                  <div className="font-nunito font-semibold text-rose-900">New Users</div>
                  <div className="font-nunito text-sm text-rose-700">Joined this month</div>
                </div>
                <div className="font-quicksand font-bold text-3xl text-rose-900">
                  {isLoading ? '...' : data?.newUsers ?? 0}
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
