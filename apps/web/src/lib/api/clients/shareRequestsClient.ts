import {
  CreateShareRequestCommand,
  CreateShareRequestResponse,
  CreateShareRequestResponseSchema,
  PaginatedShareRequestsResponse,
  PaginatedShareRequestsResponseSchema,
  RateLimitStatusDto,
  RateLimitStatusDtoSchema,
  UserShareRequestDto,
  UserShareRequestDtoSchema,
} from '../schemas/share-requests.schemas';

import type { HttpClient } from '../core/httpClient';

/**
 * Share Requests API Client
 * Issue #2743: Frontend - UI Condivisione da Libreria
 */

export interface GetUserShareRequestsParams {
  page?: number;
  pageSize?: number;
  status?: string;
}

export interface ShareRequestsClient {
  /**
   * Get paginated list of user's share requests
   */
  getUserShareRequests(params?: GetUserShareRequestsParams): Promise<PaginatedShareRequestsResponse>;

  /**
   * Get a specific share request by ID
   */
  getShareRequestById(id: string): Promise<UserShareRequestDto>;

  /**
   * Create a new share request
   */
  createShareRequest(command: CreateShareRequestCommand): Promise<CreateShareRequestResponse>;

  /**
   * Get current rate limit status for the user
   */
  getRateLimitStatus(): Promise<RateLimitStatusDto>;
}

export interface CreateShareRequestsClientParams {
  httpClient: HttpClient;
}

export function createShareRequestsClient({
  httpClient,
}: CreateShareRequestsClientParams): ShareRequestsClient {
  return {
    async getUserShareRequests(
      params?: GetUserShareRequestsParams
    ): Promise<PaginatedShareRequestsResponse> {
      const queryParams = new URLSearchParams();

      if (params?.page !== undefined) {
        queryParams.append('page', String(params.page));
      }

      if (params?.pageSize !== undefined) {
        queryParams.append('pageSize', String(params.pageSize));
      }

      if (params?.status) {
        queryParams.append('status', params.status);
      }

      const url = queryParams.toString()
        ? `/api/v1/share-requests?${queryParams}`
        : '/api/v1/share-requests';

      const data = await httpClient.get<PaginatedShareRequestsResponse>(
        url,
        PaginatedShareRequestsResponseSchema
      );

      return (
        data ?? {
          items: [],
          page: 1,
          pageSize: 10,
          totalCount: 0,
          totalPages: 0,
          hasNextPage: false,
          hasPreviousPage: false,
        }
      );
    },

    async getShareRequestById(id: string): Promise<UserShareRequestDto> {
      const data = await httpClient.get<UserShareRequestDto>(
        `/api/v1/share-requests/${id}`,
        UserShareRequestDtoSchema
      );

      if (!data) {
        throw new Error(`Share request not found: ${id}`);
      }

      return data;
    },

    async createShareRequest(
      command: CreateShareRequestCommand
    ): Promise<CreateShareRequestResponse> {
      const data = await httpClient.post<CreateShareRequestResponse>(
        '/api/v1/share-requests',
        command,
        CreateShareRequestResponseSchema
      );

      if (!data) {
        throw new Error('Failed to create share request');
      }

      return data;
    },

    async getRateLimitStatus(): Promise<RateLimitStatusDto> {
      const data = await httpClient.get<RateLimitStatusDto>(
        '/api/v1/share-requests/rate-limit',
        RateLimitStatusDtoSchema
      );

      if (!data) {
        throw new Error('Failed to get rate limit status');
      }

      return data;
    },
  };
}
