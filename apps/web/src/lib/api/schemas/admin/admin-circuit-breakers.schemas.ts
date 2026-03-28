import { z } from 'zod';

export const CircuitBreakerStateSchema = z.object({
  serviceName: z.string(),
  state: z.string(),
  tripCount: z.number(),
  lastTrippedAt: z.string().nullable(),
  lastResetAt: z.string().nullable(),
  lastError: z.string().nullable(),
});

export type CircuitBreakerState = z.infer<typeof CircuitBreakerStateSchema>;
