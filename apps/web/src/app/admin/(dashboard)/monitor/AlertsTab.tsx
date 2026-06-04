'use client';

import { useState, useEffect, useRef } from 'react';

import { AlertTriangleIcon, Plus, RefreshCwIcon } from 'lucide-react';
import { toast } from 'sonner';

import { AlertActivityFeed } from '@/components/admin/alert-rules/AlertActivityFeed';
import { AlertKpiStrip } from '@/components/admin/alert-rules/AlertKpiStrip';
import { AlertRuleList } from '@/components/admin/alert-rules/AlertRuleList';
import { AlertsBanner } from '@/components/admin/AlertsBanner';
import { Button } from '@/components/ui/primitives/button';
import type { DashboardMetrics } from '@/lib/api';
import { api } from '@/lib/api';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

import { CreateAlertRuleDialog } from './CreateAlertRuleDialog';

export function AlertsTab() {
  const [rules, setRules] = useState<AlertRule[]>([]);
  const [metrics, setMetrics] = useState<DashboardMetrics | null>(null);
  const [healthyServices, setHealthyServices] = useState(0);
  const [totalServices, setTotalServices] = useState(0);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(false);
  const [createDialogOpen, setCreateDialogOpen] = useState(false);

  const loadData = (silent = false) => {
    if (!silent) setLoading(true);
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
      .finally(() => {
        if (!silent) setLoading(false);
      });
  };

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    loadData();
    intervalRef.current = setInterval(() => loadData(true), 30_000);
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
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

  /**
   * #1840 SP5 F4-C7 — TestAlert handler (dryRun di default per safety).
   *
   * Conferma utente D3: il button azione invia sempre dryRun. La modalità
   * "live" (notifica reale ai canali) sarà accessibile via dropdown / confirm
   * dialog dedicato — fuori scope di questo wiring iniziale per evitare
   * invii accidentali a Slack/Email durante la familiarizzazione con la UI.
   *
   * L'evento AlertFiredEvent emesso dal backend viene poi propagato all'
   * AlertActivityFeed via SSE → l'admin vede il "TEST · DRY RUN" badge in
   * pochi secondi dopo il toast di conferma.
   */
  const handleTestAlert = (id: string) => {
    const rule = rules.find(r => r.id === id);
    const ruleName = rule?.name ?? id;
    alertRulesApi.testRule(id, 'dryRun').then(
      result =>
        toast.success(`Test "${ruleName}" eseguito (dry-run)`, {
          description: `Canali simulati: ${result.channels.join(' + ')} · check Activity feed.`,
        }),
      () =>
        toast.error(`Errore nel test "${ruleName}"`, {
          description: 'Verifica connessione backend o configurazione canali.',
        })
    );
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
          <Button variant="outline" size="sm" onClick={() => loadData()}>
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
      {/* #1840 SP5 F4-C7 — KPI strip sopra la tabella regole */}
      <AlertKpiStrip />
      <div className="flex items-start justify-between gap-4">
        <div>
          <h2 className="font-quicksand text-lg font-semibold tracking-tight text-foreground">
            Regole di Alert
          </h2>
          <p className="mt-1 text-sm text-muted-foreground">
            Configura soglie di alert e destinazioni delle notifiche.
          </p>
        </div>
        <Button size="sm" onClick={() => setCreateDialogOpen(true)} className="gap-1.5">
          <Plus className="h-4 w-4" />
          Nuova Regola
        </Button>
      </div>
      <AlertRuleList
        rules={rules}
        onDelete={handleDelete}
        onToggle={handleToggle}
        onTestAlert={handleTestAlert}
      />
      {/* #1840 SP5 F4-C7 — Live activity feed sotto la tabella */}
      <AlertActivityFeed />
      <CreateAlertRuleDialog
        open={createDialogOpen}
        onClose={() => setCreateDialogOpen(false)}
        onCreated={() => {
          setCreateDialogOpen(false);
          loadData();
        }}
      />
    </div>
  );
}
