/**
 * Admin Infrastructure Monitoring Schemas
 *
 * Service health, Prometheus metrics, metrics time series,
 * database/cache/vector store metrics, and enhanced service dashboard.
 */

import { z } from 'zod';

// ========== Infrastructure Monitoring (Issue #896) ==========

export const HealthStateSchema = z.enum(['Healthy', 'Degraded', 'Unhealthy']);
export type HealthState = z.infer<typeof HealthStateSchema>;

export const ServiceHealthStatusSchema = z.object({
  serviceName: z.string(),
  state: HealthStateSchema,
  errorMessage: z.string().nullable().optional(),
  checkedAt: z.string().datetime(),
  responseTimeMs: z.number().nonnegative(), // Response time in milliseconds
});

export type ServiceHealthStatus = z.infer<typeof ServiceHealthStatusSchema>;

export const OverallHealthStatusSchema = z.object({
  state: HealthStateSchema,
  totalServices: z.number().int().nonnegative(),
  healthyServices: z.number().int().nonnegative(),
  degradedServices: z.number().int().nonnegative(),
  unhealthyServices: z.number().int().nonnegative(),
  checkedAt: z.string().datetime(),
});

export type OverallHealthStatus = z.infer<typeof OverallHealthStatusSchema>;

export const PrometheusMetricsSummarySchema = z.object({
  apiRequestsLast24h: z.number().int().nonnegative(),
  avgLatencyMs: z.number().nonnegative(),
  errorRate: z.number().min(0).max(1),
  llmCostLast24h: z.number().nonnegative(),
});

export type PrometheusMetricsSummary = z.infer<typeof PrometheusMetricsSummarySchema>;

export const InfrastructureDetailsSchema = z.object({
  overall: OverallHealthStatusSchema,
  services: z.array(ServiceHealthStatusSchema),
  prometheusMetrics: PrometheusMetricsSummarySchema,
});

export type InfrastructureDetails = z.infer<typeof InfrastructureDetailsSchema>;

// ========== Enhanced Service Health (Issue #132) ==========

export const ServiceCategorySchema = z.enum([
  'Core Infrastructure',
  'AI Services',
  'External APIs',
  'Monitoring',
]);
export type ServiceCategory = z.infer<typeof ServiceCategorySchema>;

export const ResponseTimeTrendSchema = z.enum(['up', 'down', 'stable']);
export type ResponseTimeTrend = z.infer<typeof ResponseTimeTrendSchema>;

export const EnhancedServiceHealthSchema = z.object({
  serviceName: z.string(),
  state: HealthStateSchema,
  errorMessage: z.string().nullable().optional(),
  checkedAt: z.string().datetime(),
  responseTimeMs: z.number().nonnegative(),
  category: ServiceCategorySchema,
  uptimePercent24h: z.number().min(0).max(100),
  responseTimeTrend: ResponseTimeTrendSchema,
  previousResponseTimeMs: z.number().nonnegative().optional(),
  lastIncidentAt: z.string().datetime().nullable().optional(),
});
export type EnhancedServiceHealth = z.infer<typeof EnhancedServiceHealthSchema>;

export const EnhancedServiceDashboardSchema = z.object({
  overall: OverallHealthStatusSchema,
  services: z.array(EnhancedServiceHealthSchema),
  prometheusMetrics: PrometheusMetricsSummarySchema,
});
export type EnhancedServiceDashboard = z.infer<typeof EnhancedServiceDashboardSchema>;

// ========== Metrics Time Series (Issue #901) ==========

/**
 * Time-series data point from Prometheus range query.
 * Issue #901: Replaces mock chart data with real metrics.
 */
export const MetricsTimeSeriesDataPointSchema = z.object({
  timestamp: z.string().datetime(),
  value: z.number(),
});

export type MetricsTimeSeriesDataPoint = z.infer<typeof MetricsTimeSeriesDataPointSchema>;

/**
 * Response from /api/v1/admin/infrastructure/metrics/timeseries
 * Contains CPU, memory, and request rate time-series data.
 */
export const MetricsTimeSeriesResponseSchema = z.object({
  cpu: z.array(MetricsTimeSeriesDataPointSchema),
  memory: z.array(MetricsTimeSeriesDataPointSchema),
  requests: z.array(MetricsTimeSeriesDataPointSchema),
});

export type MetricsTimeSeriesResponse = z.infer<typeof MetricsTimeSeriesResponseSchema>;

// ========== Infrastructure Resources (Issue #125) ==========

export const DatabaseMetricsSchema = z.object({
  sizeBytes: z.number(),
  sizeFormatted: z.string(),
  growthLast7Days: z.number(),
  growthLast30Days: z.number(),
  growthLast90Days: z.number(),
  activeConnections: z.number(),
  maxConnections: z.number(),
  transactionsCommitted: z.number(),
  transactionsRolledBack: z.number(),
  measuredAt: z.string().datetime(),
});
export type DatabaseMetrics = z.infer<typeof DatabaseMetricsSchema>;

export const CacheMetricsSchema = z.object({
  usedMemoryBytes: z.number(),
  usedMemoryFormatted: z.string(),
  maxMemoryBytes: z.number(),
  maxMemoryFormatted: z.string(),
  memoryUsagePercent: z.number(),
  totalKeys: z.number(),
  keyspaceHits: z.number(),
  keyspaceMisses: z.number(),
  hitRate: z.number(),
  evictedKeys: z.number(),
  expiredKeys: z.number(),
  measuredAt: z.string().datetime(),
});
export type CacheMetrics = z.infer<typeof CacheMetricsSchema>;

export const TableSizeSchema = z.object({
  tableName: z.string(),
  sizeBytes: z.number(),
  sizeFormatted: z.string(),
  rowCount: z.number(),
  indexSizeBytes: z.number(),
  indexSizeFormatted: z.string(),
  totalSizeBytes: z.number(),
  totalSizeFormatted: z.string(),
});
export type TableSize = z.infer<typeof TableSizeSchema>;

export const VectorCollectionMetricsSchema = z.object({
  collectionName: z.string(),
  vectorCount: z.number(),
  indexedCount: z.number(),
  vectorDimensions: z.number(),
  distanceMetric: z.string(),
  memoryBytes: z.number(),
  memoryFormatted: z.string(),
});

export type VectorCollectionMetrics = z.infer<typeof VectorCollectionMetricsSchema>;

export const VectorStoreMetricsSchema = z.object({
  totalCollections: z.number(),
  totalVectors: z.number(),
  indexedVectors: z.number(),
  memoryBytes: z.number(),
  memoryFormatted: z.string(),
  collections: z.array(VectorCollectionMetricsSchema),
  measuredAt: z.string().datetime(),
});
export type VectorStoreMetrics = z.infer<typeof VectorStoreMetricsSchema>;
