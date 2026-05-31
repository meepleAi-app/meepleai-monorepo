/**
 * KB Docs Client (Issue #1592 Phase 2b)
 *
 * Modular client for the cross-game per-user KB documents listing endpoint
 * (BE-1 #1588). Currently exposes one method: `listUserKbDocs`. Future readers/
 * detail endpoints stay in `knowledgeBaseClient.ts` (separation of concerns —
 * this client is scoped to the new cross-game user listing endpoint).
 */

import { type PatchKbDocMetadataRequest } from '../schemas/kb-docs-patch.schemas';
import {
  KbDocsListResponseSchema,
  type KbDocsListResponse,
  UserKbDocDtoSchema,
  type UserKbDocDto,
} from '../schemas/kb-docs.schemas';
import {
  GlobalKbSearchResponseSchema,
  type GlobalKbSearchRequest,
  type GlobalKbSearchResponse,
} from '../schemas/kb-globale.schemas';

import type { HttpClient } from '../core/httpClient';

export interface CreateKbDocsClientParams {
  httpClient: HttpClient;
}

export interface ListUserKbDocsParams {
  page?: number;
  pageSize?: number;
  /** Server orders by `ProcessedAt ?? UploadedAt` DESC. Only `recent` is supported in v1. */
  sortBy?: 'recent';
  /** `ready` (default) filters to ProcessingState=Ready; `all` returns every state. */
  state?: 'ready' | 'all';
}

export interface KbDocsClient {
  listUserKbDocs(params?: ListUserKbDocsParams): Promise<KbDocsListResponse>;
  searchGlobal(req: GlobalKbSearchRequest): Promise<GlobalKbSearchResponse>;
  patchKbDocMetadata(id: string, body: PatchKbDocMetadataRequest): Promise<UserKbDocDto>;
}

export function createKbDocsClient({ httpClient }: CreateKbDocsClientParams): KbDocsClient {
  return {
    async listUserKbDocs(params: ListUserKbDocsParams = {}): Promise<KbDocsListResponse> {
      const qs = new URLSearchParams();
      if (params.page !== undefined) qs.append('page', String(params.page));
      if (params.pageSize !== undefined) qs.append('pageSize', String(params.pageSize));
      if (params.sortBy !== undefined) qs.append('sortBy', params.sortBy);
      if (params.state !== undefined) qs.append('state', params.state);

      const url = `/api/v1/kb-docs${qs.toString() ? `?${qs.toString()}` : ''}`;
      const response = await httpClient.get<KbDocsListResponse>(url, KbDocsListResponseSchema);
      return (
        response ?? { items: [], total: 0, page: params.page ?? 1, pageSize: params.pageSize ?? 20 }
      );
    },

    async searchGlobal(req: GlobalKbSearchRequest): Promise<GlobalKbSearchResponse> {
      const response = await httpClient.post<GlobalKbSearchResponse>(
        '/api/v1/knowledge-base/search/global',
        req,
        GlobalKbSearchResponseSchema
      );
      return response ?? { results: [], hasMore: false, nextCursor: null };
    },

    async patchKbDocMetadata(id: string, body: PatchKbDocMetadataRequest): Promise<UserKbDocDto> {
      const response = await httpClient.patch<UserKbDocDto>(
        `/api/v1/kb-docs/${id}`,
        body,
        UserKbDocDtoSchema
      );
      return response;
    },
  };
}
