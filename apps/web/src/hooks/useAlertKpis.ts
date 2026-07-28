/**
 * useAlertKpis — Issue #1840 SP5 F4-C7
 *
 * Aggregates the three KPI tiles shown above the AlertRuleList:
 *
 *   • Regole attive   — active count / total
 *   • Alert oggi      — fired today / resolved today
 *   • Canali config.  — channel count + per-type breakdown
 *
 * The hook fans out to alert-rules, alert-history and alert-channels in parallel
 * via React Query so the underlying data stays cached for the other components
 * (`AlertRuleList`, `AlertHistoryTab`, `CanaliDrawer`) without re-fetching.
 */

import { useMemo } from 'react';

import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';
import { alertChannelsApi } from '@/lib/api/alert-channels.api';
import { alertRulesApi } from '@/lib/api/alert-rules.api';
import type { AlertChannel, AlertChannelType } from '@/lib/api/schemas/alert-channels.schemas';
import type { AlertRule } from '@/lib/api/schemas/alert-rules.schemas';

export interface AlertKpis {
  /** Active vs total rules. `0/0` when the list is empty. */
  rulesActive: number;
  rulesTotal: number;
  rulesDisabled: number;
  rulesBySeverity: {
    info: number;
    warning: number;
    error: number;
    critical: number;
  };
  /** Alerts whose `triggeredAt` falls on the current calendar day (server timezone proxy: local). */
  alertsToday: number;
  alertsResolvedToday: number;
  alertsActive: number;
  /** Total channels + per-type breakdown for the trend label. */
  channelsTotal: number;
  channelsByType: Record<AlertChannelType, number>;
}

const ALERT_KPIS_RULES_QUERY_KEY = ['admin', 'alert-rules'] as const;
const ALERT_KPIS_HISTORY_QUERY_KEY = ['admin', 'alerts', 'history'] as const;
const ALERT_KPIS_CHANNELS_QUERY_KEY = ['admin', 'alert-channels'] as const;

function isToday(iso: string | null): boolean {
  if (!iso) return false;
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return false;
  const now = new Date();
  return (
    d.getFullYear() === now.getFullYear() &&
    d.getMonth() === now.getMonth() &&
    d.getDate() === now.getDate()
  );
}

export function useAlertKpis() {
  // Use shared query keys so AlertRuleList / AlertHistoryTab / CanaliDrawer
  // benefit from the same cache entry (avoid duplicate network calls).
  const rulesQuery = useQuery<AlertRule[]>({
    queryKey: ALERT_KPIS_RULES_QUERY_KEY,
    queryFn: () => alertRulesApi.getAll(),
    refetchInterval: 30_000,
    retry: 1,
  });

  const historyQuery = useQuery({
    queryKey: ALERT_KPIS_HISTORY_QUERY_KEY,
    queryFn: () => api.admin.getAlertHistory(),
    refetchInterval: 30_000,
    retry: 1,
  });

  const channelsQuery = useQuery<AlertChannel[]>({
    queryKey: ALERT_KPIS_CHANNELS_QUERY_KEY,
    queryFn: () => alertChannelsApi.getAll(),
    refetchInterval: 60_000,
    retry: 1,
  });

  const kpis: AlertKpis = useMemo(() => {
    const rules = rulesQuery.data ?? [];
    const history = historyQuery.data ?? [];
    const channels = channelsQuery.data ?? [];

    const rulesActive = rules.filter(r => r.isEnabled).length;
    const rulesDisabled = rules.length - rulesActive;
    const rulesBySeverity = {
      info: 0,
      warning: 0,
      error: 0,
      critical: 0,
    };
    rules.forEach(r => {
      // Severity strings are PascalCase on the wire ('Info' | 'Warning' | 'Error' | 'Critical').
      const key = r.severity.toLowerCase() as keyof typeof rulesBySeverity;
      if (key in rulesBySeverity) rulesBySeverity[key] += 1;
    });

    const todays = history.filter(a => isToday(a.triggeredAt));
    const alertsToday = todays.length;
    const alertsResolvedToday = todays.filter(a => !a.isActive).length;
    const alertsActive = history.filter(a => a.isActive).length;

    const channelsByType: Record<AlertChannelType, number> = {
      email: 0,
      slack: 0,
    };
    channels.forEach(c => {
      if (c.isEnabled) channelsByType[c.type] += 1;
    });
    const channelsTotal = channelsByType.email + channelsByType.slack;

    return {
      rulesActive,
      rulesTotal: rules.length,
      rulesDisabled,
      rulesBySeverity,
      alertsToday,
      alertsResolvedToday,
      alertsActive,
      channelsTotal,
      channelsByType,
    };
  }, [rulesQuery.data, historyQuery.data, channelsQuery.data]);

  return {
    kpis,
    isLoading: rulesQuery.isLoading || historyQuery.isLoading || channelsQuery.isLoading,
    isError: rulesQuery.isError || historyQuery.isError || channelsQuery.isError,
  };
}
