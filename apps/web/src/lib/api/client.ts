/**
 * Shared API Client (Epic #4068)
 *
 * Centralized HTTP client for permission and admin endpoints.
 * Uses HttpClient from core/httpClient for consistency.
 */

import { HttpClient } from './core/httpClient';

/**
 * Shared API client instance
 * Used by: permissions.ts, admin-client.ts
 */
export const apiClient = new HttpClient();
