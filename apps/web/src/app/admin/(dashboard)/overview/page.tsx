'use client';

import { useEffect, useState } from 'react';

import { BarChart3, Users, Gamepad2, BookOpen, BrainCircuit, ClipboardCheck } from 'lucide-react';

import { QuickActionsWidget } from '@/components/admin/overview/QuickActionsWidget';
import { SystemHealthCard } from '@/components/admin/overview/SystemHealthCard';
import { createApiClient } from '@/lib/api';
import type { AdminOverviewStats } from '@/lib/api/schemas/admin.schemas';

function StatCard({
  label,
  value,
  icon: Icon,
  description,
}: {
  label: string;
  value?: number | string;
  icon: React.ComponentType<{ className?: string }>;
  description: string;
}) {
  return (
    <div className="rounded-2xl border border-slate-200/60 dark:border-zinc-700/40 bg-white/70 dark:bg-zinc-800/50 backdrop-blur-sm p-5">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-amber-100/80 dark:bg-amber-900/30">
          <Icon className="h-5 w-5 text-amber-700 dark:text-amber-400" />
        </div>
        <div>
          <p className="font-quicksand text-sm font-semibold text-foreground">{label}</p>
          {value !== undefined ? (
            <p className="font-nunito text-lg font-bold text-foreground">
              {typeof value === 'number' ? value.toLocaleString() : value}
            </p>
          ) : (
            <p className="text-xs text-muted-foreground">{description}</p>
          )}
        </div>
      </div>
    </div>
  );
}

export default function OverviewPage() {
  const [stats, setStats] = useState<AdminOverviewStats | null>(null);

  useEffect(() => {
    const api = createApiClient();
    api.admin
      .getOverviewStats()
      .then(setStats)
      .catch(() => {});
  }, []);

  return (
    <div className="space-y-8">
      <div>
        <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
          Dashboard Overview
        </h1>
        <p className="text-sm text-muted-foreground mt-1">
          Platform stats, approvals, and user management at a glance
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-6">
        <StatCard
          icon={Users}
          label="Utenti Totali"
          value={stats?.totalUsers}
          description="Utenti registrati"
        />
        <StatCard
          icon={BarChart3}
          label="Utenti Attivi (30gg)"
          value={stats?.activeUsers}
          description="Sessioni attive"
        />
        <StatCard
          icon={BrainCircuit}
          label="Utenti AI Attivi (30gg)"
          value={stats?.activeAiUsers}
          description="Utenti con interazione AI"
        />
        <StatCard
          icon={Gamepad2}
          label="Giochi"
          value={stats ? `${stats.publishedGames} / ${stats.totalGames}` : undefined}
          description="Pubblicati / Totali"
        />
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        <StatCard
          icon={ClipboardCheck}
          label="Approvazioni in attesa"
          value={stats?.pendingApprovals}
          description="Da revisionare"
        />
        <StatCard
          icon={BookOpen}
          label="Invii recenti (7gg)"
          value={stats?.recentSubmissions}
          description="Nuove sottomissioni"
        />
      </div>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        <div className="lg:col-span-2">
          <QuickActionsWidget />
        </div>
        <SystemHealthCard />
      </div>
    </div>
  );
}
