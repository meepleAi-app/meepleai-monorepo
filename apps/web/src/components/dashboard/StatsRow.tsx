'use client';

import { Clock, Gamepad2, Play, TrendingUp } from 'lucide-react';

import { Card } from '@/components/ui/card';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

function StatCard({
  icon,
  value,
  label,
  colorClass,
}: {
  icon: React.ReactNode;
  value: string;
  label: string;
  colorClass: string;
}) {
  return (
    <Card className="bg-card border-border/50 p-4 hover:bg-[hsl(var(--card-hover))] transition-colors">
      <div className="flex items-start gap-3">
        <div className={cn('p-2 rounded-lg bg-secondary/50 shrink-0', colorClass)}>{icon}</div>
        <div className="min-w-0 flex-1">
          <p className="font-bold text-lg text-foreground truncate">{value}</p>
          <p className="text-xs text-muted-foreground truncate">{label}</p>
        </div>
      </div>
    </Card>
  );
}

function parsePlayTime(ts: string | undefined): string {
  if (!ts) return '—';
  const parts = ts.split(':');
  const h = parseInt(parts[0] ?? '0', 10);
  const m = parseInt(parts[1] ?? '0', 10);
  return h > 0 ? `${h}h ${m}m` : `${m}m`;
}

export function StatsRow() {
  const stats = useDashboardStore(s => s.stats);

  const changeLabel =
    stats?.monthlyPlaysChange !== undefined
      ? `${stats.monthlyPlaysChange > 0 ? '+' : ''}${stats.monthlyPlaysChange}%`
      : '—';

  return (
    <section className="grid grid-cols-2 lg:grid-cols-4 gap-3">
      <StatCard
        icon={<Gamepad2 className="w-4 h-4" />}
        value={stats ? String(stats.totalGames) : '—'}
        label="giochi in libreria"
        colorClass="text-[hsl(var(--e-game))]"
      />
      <StatCard
        icon={<Play className="w-4 h-4" />}
        value={stats ? String(stats.monthlyPlays) : '—'}
        label="partite questo mese"
        colorClass="text-[hsl(var(--e-session))]"
      />
      <StatCard
        icon={<Clock className="w-4 h-4" />}
        value={parsePlayTime(stats?.weeklyPlayTime)}
        label="ore questa settimana"
        colorClass="text-[hsl(var(--e-agent))]"
      />
      <StatCard
        icon={<TrendingUp className="w-4 h-4" />}
        value={changeLabel}
        label="vs mese scorso"
        colorClass="text-[hsl(var(--e-chat))]"
      />
    </section>
  );
}
