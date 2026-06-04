import { HttpClient } from './core/httpClient';

import type { PrometheusMetricLabelsResponse } from './schemas/prometheus-metrics.schemas';

const api = new HttpClient();

/**
 * Issue #1840 SP5 F4-C7 — Prometheus metric label catalogue.
 *
 * The backend caches labels for 60s and falls back to a hardcoded shortlist
 * when Prometheus is unreachable. Callers should surface `isFallback=true` to
 * the admin (e.g. inline warning on the MetricSelector dropdown).
 */
export const prometheusMetricsApi = {
  getLabels: async (): Promise<PrometheusMetricLabelsResponse> => {
    const result = await api.get<PrometheusMetricLabelsResponse>('/api/v1/admin/metrics/labels');
    return result ?? { labels: [], isFallback: false };
  },
};
