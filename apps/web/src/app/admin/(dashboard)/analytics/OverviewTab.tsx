'use client';

import { useState, useEffect } from 'react';

import { BarChart3, Users, FileText, RefreshCwIcon } from 'lucide-react';

import { ChartsSection } from '@/components/admin/charts/ChartsSection';
import { Card, CardContent } from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { AdminOverviewStats } from '@/lib/api/schemas/admin.schemas';

type DateRange = '7d' | '30d' | '90d';

interface QuickStat {
  label: string;
  value: string | number;
  icon: React.ReactNode;
}

export function OverviewTab() {
  const [dateRange, setDateRange] = useState<DateRange>('30d');
  const [stats, setStats] = useState<AdminOverviewStats | null>(null);
  const [loading, setLoading] = useState(true);

  const loadStats = () => {
    setLoading(true);
    api.admin
      .getOverviewStats()
      .then(data => setStats(data))
      .catch(() => {})
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadStats();
  }, []);

  const quickStats: QuickStat[] = stats
    ? [
        {
          label: 'Utenti totali',
          value: stats.totalUsers,
          icon: <Users className="h-4 w-4 text-blue-500" />,
        },
        {
          label: 'Utenti attivi',
          value: stats.activeUsers,
          icon: <Users className="h-4 w-4 text-green-500" />,
        },
        {
          label: 'Giochi pubblicati',
          value: stats.publishedGames,
          icon: <BarChart3 className="h-4 w-4 text-amber-500" />,
        },
        {
          label: 'Invii recenti',
          value: stats.recentSubmissions,
          icon: <FileText className="h-4 w-4 text-purple-500" />,
        },
      ]
    : [];

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Panoramica Analitiche
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Grafici di utilizzo e metriche della piattaforma.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Select value={dateRange} onValueChange={(v: string) => setDateRange(v as DateRange)}>
            <SelectTrigger className="w-36">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="7d">Ultimi 7 giorni</SelectItem>
              <SelectItem value="30d">Ultimi 30 giorni</SelectItem>
              <SelectItem value="90d">Ultimi 90 giorni</SelectItem>
            </SelectContent>
          </Select>
          <Button variant="outline" size="icon" onClick={loadStats}>
            <RefreshCwIcon className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Quick stats row */}
      {!loading && quickStats.length > 0 && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {quickStats.map(stat => (
            <Card
              key={stat.label}
              className="bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm border-slate-200/60 dark:border-zinc-700/40"
            >
              <CardContent className="flex items-center gap-3 py-3 px-4">
                {stat.icon}
                <div>
                  <p className="text-xs text-muted-foreground">{stat.label}</p>
                  <p className="font-quicksand text-lg font-bold">{stat.value}</p>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      )}

      {loading && (
        <div className="grid grid-cols-2 md:grid-cols-4 gap-3">
          {[1, 2, 3, 4].map(i => (
            <div key={i} className="h-16 rounded-xl bg-muted/50 animate-pulse" />
          ))}
        </div>
      )}

      <ChartsSection />
    </div>
  );
}
