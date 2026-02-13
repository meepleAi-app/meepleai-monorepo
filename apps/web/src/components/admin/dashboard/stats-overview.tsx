'use client';

import { useQuery } from '@tanstack/react-query';
import { Users, Package, TrendingUp, CheckCircle } from 'lucide-react';

import { adminClient } from '@/lib/api/admin-client';

interface StatCardProps {
  title: string;
  value: string | number;
  subtitle: string;
  icon: React.ReactNode;
  accentColor: string;
  delay: number;
}

function StatCard({ title, value, subtitle, icon, accentColor, delay }: StatCardProps) {
  return (
    <div
      className="group relative"
      style={{
        animation: `slideInUp 0.6s ease-out ${delay}ms both`,
      }}
    >
      {/* Game box spine effect */}
      <div
        className={`absolute -left-2 top-4 bottom-4 w-1.5 rounded-full ${accentColor} opacity-80 group-hover:opacity-100 transition-opacity`}
      />

      {/* Card background - glassmorphic */}
      <div className="relative bg-white/70 backdrop-blur-md rounded-2xl border border-amber-200/60 hover:border-amber-300/80 transition-all duration-300 overflow-hidden group-hover:shadow-xl group-hover:shadow-amber-500/10">
        {/* Decorative corner */}
        <div className="absolute top-0 right-0 w-24 h-24 bg-gradient-to-br from-amber-100/40 to-transparent rounded-bl-full" />

        <div className="relative p-6">
          {/* Icon badge */}
          <div className="mb-4 inline-flex items-center justify-center w-12 h-12 rounded-xl bg-gradient-to-br from-amber-100 to-amber-50 text-amber-900 border border-amber-200/60">
            {icon}
          </div>

          {/* Title */}
          <h3 className="font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-600 mb-2">
            {title}
          </h3>

          {/* Value */}
          <div className="font-quicksand font-bold text-4xl text-slate-900 mb-1">
            {value}
          </div>

          {/* Subtitle */}
          <p className="font-nunito text-sm text-slate-500">{subtitle}</p>
        </div>

        {/* Hover effect gradient */}
        <div className="absolute inset-0 bg-gradient-to-t from-amber-500/5 to-transparent opacity-0 group-hover:opacity-100 transition-opacity pointer-events-none" />
      </div>
    </div>
  );
}

export function StatsOverview() {
  const { data, isLoading } = useQuery({
    queryKey: ['admin-stats'],
    queryFn: () => adminClient.getStats(),
  });

  if (isLoading) {
    return (
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {[...Array(4)].map((_, i) => (
          <div
            key={i}
            className="h-[180px] bg-white/40 backdrop-blur-sm rounded-2xl border border-slate-200/60 animate-pulse"
          />
        ))}
      </div>
    );
  }

  const stats = [
    {
      title: 'Shared Games',
      value: data?.totalGames ?? 0,
      subtitle: `${data?.publishedGames ?? 0} published, ${data?.pendingGames ?? 0} pending`,
      icon: <Package className="w-6 h-6" />,
      accentColor: 'bg-amber-500',
      delay: 0,
    },
    {
      title: 'Community',
      value: data?.totalUsers ?? 0,
      subtitle: `${data?.activeUsers ?? 0} active this month`,
      icon: <Users className="w-6 h-6" />,
      accentColor: 'bg-orange-500',
      delay: 100,
    },
    {
      title: 'Approvals',
      value: `${data?.approvalRate ?? 0}%`,
      subtitle: `${data?.pendingApprovals ?? 0} awaiting review`,
      icon: <CheckCircle className="w-6 h-6" />,
      accentColor: 'bg-emerald-500',
      delay: 200,
    },
    {
      title: 'Activity',
      value: data?.recentSubmissions ?? 0,
      subtitle: 'Submissions last 7 days',
      icon: <TrendingUp className="w-6 h-6" />,
      accentColor: 'bg-blue-500',
      delay: 300,
    },
  ];

  return (
    <div>
      <div className="mb-4 flex items-center gap-2">
        <div className="h-1 w-12 bg-gradient-to-r from-amber-500 to-orange-500 rounded-full" />
        <h2 className="font-quicksand font-bold text-xl text-slate-700">
          Collection Overview
        </h2>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        {stats.map((stat, index) => (
          <StatCard key={index} {...stat} />
        ))}
      </div>

      <style jsx>{`
        @keyframes slideInUp {
          from {
            opacity: 0;
            transform: translateY(30px);
          }
          to {
            opacity: 1;
            transform: translateY(0);
          }
        }
      `}</style>
    </div>
  );
}
