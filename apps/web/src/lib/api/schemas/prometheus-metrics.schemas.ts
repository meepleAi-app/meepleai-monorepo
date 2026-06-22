import { z } from 'zod';

// Issue #1840 SP5 F4-C7 — Prometheus metric label catalogue for the
// MetricSelector dropdown in CreateAlertRuleDialog.
// Backend: AdminMetricsEndpoints `GET /api/v1/admin/metrics/labels` returning
// `{ labels: string[]; isFallback: boolean }`.

export const prometheusMetricLabelsResponseSchema = z.object({
  labels: z.array(z.string()),
  // True when the backend served the hardcoded fallback list because
  // Prometheus was unreachable / returned 5xx within the cache window.
  isFallback: z.boolean(),
});

export type PrometheusMetricLabelsResponse = z.infer<typeof prometheusMetricLabelsResponseSchema>;
