'use client';

import { useState, useEffect } from 'react';

import { toast } from 'sonner';

import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import { AlertsBanner } from '@/components/admin/AlertsBanner';
import type { DashboardMetrics } from '@/lib/api';
import { api } from '@/lib/api';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

export function AlertsTab() {
  const [rules, setRules] = useState<AlertRule[]>([]);
  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [healthyServices, setHealthyServices] = useState(0);
  const [totalServices, setTotalServices] = useState(0);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    Promise.all([
      alertRulesApi.getAll().catch(() => []),
      api.admin.getAnalytics().catch(() => null),
      api.admin.getInfrastructureDetails().catch(() => null),
    ])
      .then(([fetchedRules, dashboardStats, infraDetails]) => {
        setRules(fetchedRules as AlertRule[]);
        setMetrics(dashboardStats?.metrics ?? null);
        setHealthyServices(infraDetails?.overall?.healthyServices ?? 0);
        setTotalServices(infraDetails?.overall?.totalServices ?? 0);
      })
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const handleToggle = (id: string) => {
    const rule = rules.find(r => r.id === id);
    if (!rule) return;
    setRules(prev => prev.map(r => (r.id === id ? { ...r, isEnabled: !r.isEnabled } : r)));
    alertRulesApi.toggle(id).catch(() => {
      setRules(prev => prev.map(r => (r.id === id ? { ...r, isEnabled: !r.isEnabled } : r)));
      toast.error('Failed to toggle alert rule');
    });
  };

  if (loading) {
    return (
      <div className="space-y-6 animate-pulse">
        <div className="h-24 rounded-xl bg-muted/50" />
        <div className="h-64 rounded-lg bg-muted/50" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <AlertsBanner
        metrics={metrics}
        healthyServices={healthyServices}
        totalServices={totalServices}
      />
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Alert Rules
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Configure alert thresholds and notification targets.
        </p>
      </div>
      <AlertRuleList rules={rules} onEdit={() => {}} onDelete={() => {}} onToggle={handleToggle} />
    </div>
  );
}
