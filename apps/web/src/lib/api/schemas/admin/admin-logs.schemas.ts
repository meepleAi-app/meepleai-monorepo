import { z } from 'zod';

export const ApplicationLogSchema = z.object({
  id: z.string(),
  timestamp: z.string(),
  level: z.string(),
  message: z.string(),
  source: z.string().nullable(),
  correlationId: z.string().nullable(),
  exception: z.string().nullable(),
  properties: z.record(z.string(), z.string()).nullable(),
});

export const ApplicationLogsResponseSchema = z.object({
  items: z.array(ApplicationLogSchema),
  remainingCount: z.number().nullable(),
  lastId: z.string().nullable(),
});

export type ApplicationLog = z.infer<typeof ApplicationLogSchema>;
export type ApplicationLogsResponse = z.infer<typeof ApplicationLogsResponseSchema>;

export interface ApplicationLogsFilters {
  search?: string;
  level?: string;
  source?: string;
  correlationId?: string;
  from?: string;
  to?: string;
  count?: number;
  afterId?: string;
}
