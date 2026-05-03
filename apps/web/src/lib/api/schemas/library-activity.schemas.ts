/**
 * Library activity feed schemas (Issue #642 — Wave B.3 followup).
 *
 * Backend endpoint: `GET /api/v1/library/activity`. The backend MVP only emits
 * `added` and `state-changed` event types (DomainEventLog infrastructure for
 * `removed` and `session-recorded` is tracked in a separate followup issue),
 * but the schema accepts all four kinds so we don't break the contract once
 * the additional types ship.
 */

import { z } from 'zod';

export const LibraryActivityItemSchema = z.object({
  id: z.string().uuid(),
  type: z.enum(['added', 'state-changed', 'removed', 'session-recorded']),
  timestamp: z.string().datetime({ offset: true }),
  gameId: z.string().uuid(),
  gameTitle: z.string(),
  message: z.string(),
});

export type LibraryActivityItem = z.infer<typeof LibraryActivityItemSchema>;

export const LibraryActivityResponseSchema = z.array(LibraryActivityItemSchema);
