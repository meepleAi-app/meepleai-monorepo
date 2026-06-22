/**
 * Staging Allowlist API Client (#845 — DevOps Wave 1)
 *
 * Superadmin-only endpoints for managing the email allowlist that gates
 * access to the staging environment.
 */

import { type HttpClient } from '../core/httpClient';

export interface StagingAllowlistEntryDto {
  id: string;
  email: string;
  addedByUserId: string | null;
  addedAt: string;
  note: string | null;
}

export interface AddStagingAllowlistEntryRequest {
  email: string;
  note: string | null;
}

export interface CreateStagingAllowlistClientParams {
  httpClient: HttpClient;
}

export function createStagingAllowlistClient({ httpClient }: CreateStagingAllowlistClientParams) {
  return {
    /** GET /api/v1/admin/staging-allowlist — list active entries, newest first */
    async list(): Promise<StagingAllowlistEntryDto[]> {
      const response = await httpClient.get<StagingAllowlistEntryDto[]>(
        '/api/v1/admin/staging-allowlist'
      );
      return response ?? [];
    },

    /** POST /api/v1/admin/staging-allowlist — add email; 409 on duplicate */
    async add(request: AddStagingAllowlistEntryRequest): Promise<StagingAllowlistEntryDto> {
      const response = await httpClient.post<StagingAllowlistEntryDto>(
        '/api/v1/admin/staging-allowlist',
        request
      );
      if (!response) {
        throw new Error('Empty response from staging allowlist add endpoint');
      }
      return response;
    },

    /** DELETE /api/v1/admin/staging-allowlist/{id} — soft-delete; 404 if missing */
    async remove(id: string): Promise<void> {
      await httpClient.delete(`/api/v1/admin/staging-allowlist/${id}`);
    },
  };
}

export type StagingAllowlistClient = ReturnType<typeof createStagingAllowlistClient>;
