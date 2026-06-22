/**
 * Activity Client (Issue #1593 Phase 3b)
 *
 * Modular client for the cross-entity activity feed endpoint (BE-3 #1590).
 * Single method: `listActivity({limit?, since?})`.
 */

import { ActivityFeedResponseSchema, type ActivityFeedResponse } from '../schemas/activity.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateActivityClientParams {
  httpClient: HttpClient;
}

export interface ListActivityParams {
  /** 1-100 inclusive. Server default 20. */
  limit?: number;
  /** Optional ISO datetime upper-bound cursor: returns events where loggedAt < since. */
  since?: string;
}

export interface ActivityClient {
  listActivity(params?: ListActivityParams): Promise<ActivityFeedResponse>;
}

export function createActivityClient({ httpClient }: CreateActivityClientParams): ActivityClient {
  return {
    async listActivity(params: ListActivityParams = {}): Promise<ActivityFeedResponse> {
      const qs = new URLSearchParams();
      if (params.limit !== undefined) qs.append('limit', String(params.limit));
      if (params.since !== undefined) qs.append('since', params.since);

      const url = `/api/v1/activity${qs.toString() ? `?${qs.toString()}` : ''}`;
      const response = await httpClient.get<ActivityFeedResponse>(url, ActivityFeedResponseSchema);
      // Pattern from libraryClient/kbDocsClient: defensive fallback when response is null.
      return response ?? { success: true, items: [], count: 0 };
    },
  };
}
