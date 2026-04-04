'use client';

import { useQuery } from '@tanstack/react-query';
import { BarChart3, Users, FileText, AlertTriangleIcon, RefreshCwIcon } from 'lucide-react';

import { ChartsSection } from '@/components/admin/charts/ChartsSection';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';

interface QuickStat {
  label: string;
  value: string | number;
  icon: React.ReactNode;
}

export function OverviewTab() {
  const {
    data: stats,
    isLoading,
    isError,
    refetch,
  } = useQuery({
    queryKey: ['admin', 'analytics', 'overview-stats'],
    queryFn: () => api.admin.getOverviewStats(),
    staleTime: 60_000,
  });

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
        <Button variant="outline" size="icon" onClick={() => refetch()}>
          <RefreshCwIcon className="h-4 w-4" />
        </Button>
      </div>

      {isError && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-200 dark:border-amber-800/40 bg-amber-50 dark:bg-amber-950/20 px-4 py-3">
          <AlertTriangleIcon className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-sm text-amber-800 dark:text-amber-200 flex-1">
            Errore nel caricamento delle statistiche.
          </p>
          <Button variant="outline" size="sm" onClick={() => refetch()}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Riprova
          </Button>
        </div>
      )}

      {/* Quick stats row */}
      {!isLoading && quickStats.length > 0 && (
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

      {isLoading && (
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
