'use client';

import { useState, useEffect } from 'react';

import { AlertTriangleIcon, RefreshCwIcon } from 'lucide-react';
import { toast } from 'sonner';

import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import { AlertsBanner } from '@/components/admin/AlertsBanner';
import { Button } from '@/components/ui/primitives/button';
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
  const [loadError, setLoadError] = useState(false);

  const loadData = () => {
    setLoading(true);
    setLoadError(false);
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
      .catch(() => setLoadError(true))
      .finally(() => setLoading(false));
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleToggle = (id: string) => {
    const rule = rules.find(r => r.id === id);
    if (!rule) return;
    setRules(prev => prev.map(r => (r.id === id ? { ...r, isEnabled: !r.isEnabled } : r)));
    alertRulesApi.toggle(id).catch(() => {
      setRules(prev => prev.map(r => (r.id === id ? { ...r, isEnabled: !r.isEnabled } : r)));
      toast.error('Errore nel toggle della regola');
    });
  };

  const handleDelete = (id: string) => {
    const prev = [...rules];
    setRules(r => r.filter(item => item.id !== id));
    alertRulesApi.delete(id).then(
      () => toast.success('Regola eliminata'),
      () => {
        setRules(prev);
        toast.error("Errore nell'eliminazione della regola");
      }
    );
  };

  const handleEdit = (rule: AlertRule) => {
    // Toggle enabled state as a lightweight edit action for now.
    // Full editing UI can be added when alert rule form component is built.
    handleToggle(rule.id);
    toast.info('Modifica regola: toggle stato abilitazione');
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
      {loadError && (
        <div className="flex items-center gap-3 rounded-xl border border-amber-200 dark:border-amber-800/40 bg-amber-50 dark:bg-amber-950/20 px-4 py-3">
          <AlertTriangleIcon className="h-5 w-5 text-amber-600 dark:text-amber-400 shrink-0" />
          <p className="text-sm text-amber-800 dark:text-amber-200 flex-1">
            Errore nel caricamento degli alert. Alcuni dati potrebbero non essere disponibili.
          </p>
          <Button variant="outline" size="sm" onClick={loadData}>
            <RefreshCwIcon className="h-3.5 w-3.5 mr-1.5" />
            Riprova
          </Button>
        </div>
      )}
      <AlertsBanner
        metrics={metrics}
        healthyServices={healthyServices}
        totalServices={totalServices}
      />
      <div>
        <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
          Regole di Alert
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Configura soglie di alert e destinazioni delle notifiche.
        </p>
      </div>
      <AlertRuleList
        rules={rules}
        onEdit={handleEdit}
        onDelete={handleDelete}
        onToggle={handleToggle}
      />
    </div>
  );
}
