/**
 * StatsZone Component
 *
 * Compact horizontal KPI bar showing 4 stats:
 * - Games in library
 * - Games played last 30 days
 * - Active chats
 * - Current streak (days)
 *
 * Shares the same React Query key ['dashboard'] as HeroZone
 * via useDashboardData() so data is fetched only once.
 */

'use client';

import { Flame, Gamepad2, Library, MessageCircle } from 'lucide-react';

import { useDashboardData } from '@/hooks/useDashboardData';
import { cn } from '@/lib/utils';

interface StatItemProps {
  icon: React.ReactNode;
  value: number;
  label: string;
}

function StatItem({ icon, value, label }: StatItemProps) {
  return (
    <div
      className={cn(
        'relative rounded-xl px-4 py-3',
        'bg-[rgba(255,255,255,0.75)] dark:bg-[rgba(30,27,24,0.75)]',
        'backdrop-blur-md',
        'border border-[rgba(200,180,160,0.20)] dark:border-[rgba(100,90,75,0.25)]',
        'shadow-[0_2px_12px_rgba(180,120,60,0.06)] dark:shadow-[0_2px_12px_rgba(0,0,0,0.15)]',
        'hover:shadow-[0_4px_16px_rgba(180,120,60,0.10)] transition-shadow duration-200'
      )}
    >
      <div className="flex items-center gap-2.5 mb-1">
        <span className="text-muted-foreground">{icon}</span>
        <span className="text-2xl font-bold font-quicksand text-foreground leading-none">
          {value}
        </span>
      </div>
      <div className="text-xs font-medium font-nunito text-muted-foreground">{label}</div>
    </div>
  );
}

function StatSkeleton() {
  return (
    <div
      className={cn(
        'h-20 rounded-xl animate-pulse',
        'bg-[rgba(200,180,160,0.20)] dark:bg-[rgba(40,36,32,0.40)]'
      )}
      aria-label="Loading stats"
    />
  );
}

export function StatsZone() {
  const { data, isLoading } = useDashboardData();

  if (isLoading) {
    return (
      <div data-testid="stats-zone" className="grid grid-cols-2 lg:grid-cols-4 gap-3">
        {[0, 1, 2, 3].map(i => (
          <StatSkeleton key={i} />
        ))}
      </div>
    );
  }

  if (!data) return null;

  const { stats } = data;

  return (
    <div data-testid="stats-zone" className="grid grid-cols-2 lg:grid-cols-4 gap-3">
      <StatItem
        icon={<Library className="h-5 w-5" />}
        value={stats.libraryCount}
        label="Games in library"
      />
      <StatItem
        icon={<Gamepad2 className="h-5 w-5" />}
        value={stats.playedLast30Days}
        label="Played last 30 days"
      />
      <StatItem
        icon={<MessageCircle className="h-5 w-5" />}
        value={stats.chatCount}
        label="Active chats"
      />
      <StatItem
        icon={<Flame className="h-5 w-5" />}
        value={stats.currentStreak}
        label="Current streak"
      />
    </div>
  );
}
