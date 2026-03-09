/**
 * Admin Dashboard API Client
 * Lightweight client for admin dashboard data fetching
 * Issue #4656: Frontend API Integration
 */

import { HttpClient } from '../core/httpClient';

const httpClient = new HttpClient({});

/* eslint-disable @typescript-eslint/no-explicit-any -- untyped admin endpoints, typing tracked in #4656 */
export const adminDashboardClient = {
  /**
   * Get user activity log (#4652)
   */
  getUserActivityLog: () => httpClient.get<any>('/api/v1/admin/users/activity-log'),

  /**
   * Get agent chat history (#4653, #4917)
   */
  getChatHistory: (params?: {
    page?: number;
    pageSize?: number;
    agentType?: string;
    dateFrom?: string;
    dateTo?: string;
  }) => {
    const qs = new URLSearchParams();
    if (params?.page) qs.set('page', String(params.page));
    if (params?.pageSize) qs.set('pageSize', String(params.pageSize));
    if (params?.agentType) qs.set('agentType', params.agentType);
    if (params?.dateFrom) qs.set('dateFrom', params.dateFrom);
    if (params?.dateTo) qs.set('dateTo', params.dateTo);
    const query = qs.toString();
    return httpClient.get<any>(`/api/v1/admin/agents/chat-history${query ? `?${query}` : ''}`);
  },

  /**
   * Get AI models (#4653)
   */
  getAIModels: () => httpClient.get<any>('/api/v1/admin/agents/models'),

  /**
   * Get vector collections (#4655)
   */
  getVectorCollections: () => httpClient.get<any>('/api/v1/admin/kb/vector-collections'),

  /**
   * Get processing queue (#4655)
   */
  getProcessingQueue: () => httpClient.get<any>('/api/v1/admin/kb/processing-queue'),

  /**
   * Get game categories (#4654)
   */
  getGameCategories: () => httpClient.get<any>('/api/v1/admin/shared-games/categories'),
};
/* eslint-enable @typescript-eslint/no-explicit-any */
