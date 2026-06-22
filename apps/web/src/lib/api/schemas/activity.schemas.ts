/**
 * Activity Feed Schemas (Issue #1593 Phase 3b)
 *
 * Zod schemas for the cross-entity activity feed endpoint (BE-3 #1590):
 * GET /api/v1/activity?limit=&since=.
 *
 * Matches `ActivityItemDto` from
 * apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ActivityFeed/ActivityItemDto.cs.
 *
 * Important contract notes (verified 2026-05-28 against PR #1641):
 * - NO `payload` field — the BE intentionally hides the raw event payload.
 * - NO `message` field — the FE derives display text from `eventType` + `title?`.
 * - Envelope is `{success, items, count}` where `count` is the page size (NOT a global total).
 */

import { z } from 'zod';

/**
 * ActivityItemDto — 10 fields, all camelCase, only `title` is nullable.
 */
export const ActivityItemDtoSchema = z
  .object({
    id: z.string().uuid(),
    eventId: z.string().uuid(),
    eventType: z.string().min(1),
    userId: z.string().uuid(),
    entityType: z.string().min(1),
    entityId: z.string().uuid(),
    title: z.string().nullable(),
    timestamp: z.string().datetime({ offset: true }),
    loggedAt: z.string().datetime({ offset: true }),
    payloadVersion: z.number().int().positive(),
  })
  .strict();

export type ActivityItemDto = z.infer<typeof ActivityItemDtoSchema>;

/**
 * Envelope. `count` is the page size returned, NOT a global total.
 */
export const ActivityFeedResponseSchema = z
  .object({
    success: z.boolean(),
    items: z.array(ActivityItemDtoSchema),
    count: z.number().int().nonnegative(),
  })
  .strict();

export type ActivityFeedResponse = z.infer<typeof ActivityFeedResponseSchema>;
