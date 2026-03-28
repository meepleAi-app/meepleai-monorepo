import { z } from 'zod';

export const ServiceCallSchema = z.object({
  id: z.string(),
  serviceName: z.string(),
  httpMethod: z.string(),
  requestUrl: z.string(),
  statusCode: z.number().nullable(),
  latencyMs: z.number(),
  isSuccess: z.boolean(),
  errorMessage: z.string().nullable(),
  correlationId: z.string().nullable(),
  timestampUtc: z.string(),
  requestSummary: z.string().nullable(),
  responseSummary: z.string().nullable(),
});

export const ServiceCallsResponseSchema = z.object({
  items: z.array(ServiceCallSchema),
  totalCount: z.number(),
  page: z.number(),
  pageSize: z.number(),
});

export const ServiceCallSummarySchema = z.object({
  serviceName: z.string(),
  totalCalls: z.number(),
  successCount: z.number(),
  errorCount: z.number(),
  errorRate: z.number(),
  avgLatencyMs: z.number(),
  p95LatencyMs: z.number(),
  maxLatencyMs: z.number(),
  lastCallAt: z.string().nullable(),
  lastErrorAt: z.string().nullable(),
});

export type ServiceCall = z.infer<typeof ServiceCallSchema>;
export type ServiceCallsResponse = z.infer<typeof ServiceCallsResponseSchema>;
export type ServiceCallSummary = z.infer<typeof ServiceCallSummarySchema>;

export interface ServiceCallFilters {
  service?: string;
  success?: boolean;
  correlationId?: string;
  from?: string;
  to?: string;
  minLatencyMs?: number;
  page?: number;
  pageSize?: number;
}
