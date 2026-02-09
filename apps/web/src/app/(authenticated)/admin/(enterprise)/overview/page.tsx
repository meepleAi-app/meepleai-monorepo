/**
 * Enterprise Overview Section Page
 * Issue #3689 - Layout Base & Navigation System
 *
 * Section 1: Overview & Monitoring
 * Tabs: Dashboard | Alerts | Quick Actions
 */

'use client';

import React from 'react';

import { EnterpriseSectionPage } from '@/components/admin/enterprise/EnterpriseSectionPage';
import { ENTERPRISE_SECTIONS } from '@/config/enterprise-navigation';

// eslint-disable-next-line @typescript-eslint/no-non-null-assertion
const section = ENTERPRISE_SECTIONS.find((s) => s.id === 'overview')!;

function DashboardTabContent() {
  return (
    <div className="space-y-6">
      {/* 8 KPI Cards */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        <KpiCard label="Utenti Totali" value="2,847" trend="+156" color="amber" />
        <KpiCard label="Giochi Catalogo" value="1,234" trend="+42" color="emerald" />
        <KpiCard label="Proposte Pending" value="18" trend="3 urgenti" color="purple" />
        <KpiCard label="Chat AI Oggi" value="892" trend="+180" color="blue" />
        <KpiCard label="Token Balance" value="€450" trend="/ €1000" color="cyan" />
        <KpiCard label="Database" value="2.3GB" trend="/ 10GB" color="indigo" />
        <KpiCard label="Cache Hit Rate" value="94.2%" trend="+2.1%" color="rose" />
        <KpiCard label="Active Alerts" value="3" trend="0 critical" color="amber" />
      </div>
    </div>
  );
}

function AlertsTabContent() {
  return (
    <div className="space-y-4">
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6">
        <h3 className="font-quicksand font-bold text-lg mb-4">Active Alerts</h3>
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Alert rules configuration and active alerts will be displayed here.</p>
      </div>
    </div>
  );
}

function QuickActionsTabContent() {
  return (
    <div className="space-y-4">
      <div className="rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border border-zinc-200/50 dark:border-zinc-700/50 p-6">
        <h3 className="font-quicksand font-bold text-lg mb-4">Quick Actions</h3>
        <p className="text-sm text-zinc-500 dark:text-zinc-400">Action cards with pending badges and favorites will be displayed here.</p>
      </div>
    </div>
  );
}

export default function OverviewPage() {
  return (
    <EnterpriseSectionPage
      section={section}
      tabContent={{
        'dashboard': <DashboardTabContent />,
        'alerts': <AlertsTabContent />,
        'quick-actions': <QuickActionsTabContent />,
      }}
    />
  );
}

// Shared KPI Card Component
function KpiCard({
  label,
  value,
  trend,
  color,
}: {
  label: string;
  value: string;
  trend: string;
  color: string;
}) {
  const colorMap: Record<string, string> = {
    amber: 'border-amber-200/50 dark:border-amber-500/30',
    emerald: 'border-emerald-200/50 dark:border-emerald-500/30',
    purple: 'border-purple-200/50 dark:border-purple-500/30',
    blue: 'border-blue-200/50 dark:border-blue-500/30',
    cyan: 'border-cyan-200/50 dark:border-cyan-500/30',
    indigo: 'border-indigo-200/50 dark:border-indigo-500/30',
    rose: 'border-rose-200/50 dark:border-rose-500/30',
  };

  return (
    <div
      className={`p-5 rounded-2xl bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border ${colorMap[color] ?? colorMap.amber} hover:-translate-y-0.5 hover:shadow-lg transition-all`}
      data-testid={`kpi-card-${label.toLowerCase().replace(/\s+/g, '-')}`}
    >
      <p className="text-3xl font-quicksand font-bold text-zinc-900 dark:text-zinc-100">
        {value}
      </p>
      <p className="text-sm text-zinc-500 dark:text-zinc-400 mt-1">{label}</p>
      <p className="text-xs text-zinc-400 dark:text-zinc-500 mt-0.5">{trend}</p>
    </div>
  );
}
