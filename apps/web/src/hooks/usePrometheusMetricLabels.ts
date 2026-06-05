/**
 * usePrometheusMetricLabels — Issue #1840 SP5 F4-C7
 *
 * Fetches the Prometheus metric label catalogue for the MetricSelector
 * dropdown in CreateAlertRuleDialog. The backend caches labels for 60s and
 * falls back to a hardcoded shortlist when Prometheus is unreachable —
 * callers should surface `isFallback=true` to the admin with an inline warning.
 */

import { useQuery } from '@tanstack/react-query';

import { prometheusMetricsApi } from '@/lib/api/prometheus-metrics.api';
import type { PrometheusMetricLabelsResponse } from '@/lib/api/schemas/prometheus-metrics.schemas';

export const PROMETHEUS_LABELS_QUERY_KEY = ['admin', 'prometheus', 'metric-labels'] as const;

export function usePrometheusMetricLabels() {
  return useQuery<PrometheusMetricLabelsResponse>({
    queryKey: PROMETHEUS_LABELS_QUERY_KEY,
    queryFn: () => prometheusMetricsApi.getLabels(),
    // Catalogue churn is slow (new metric registrations are rare); 5min stale +
    // 30min gc keeps the dropdown snappy across tab switches without spamming the
    // BE cache layer.
    staleTime: 5 * 60_000,
    gcTime: 30 * 60_000,
    retry: 1,
  });
}
