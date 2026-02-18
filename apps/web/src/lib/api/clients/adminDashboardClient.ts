/**
 * Admin Dashboard API Client
 * Lightweight client for admin dashboard data fetching
 * Issue #4656: Frontend API Integration
 */

import { HttpClient } from '../core/httpClient';

const httpClient = new HttpClient({});

export const adminDashboardClient = {
  /**
   * Get user activity log (#4652)
   */
  getUserActivityLog: () => httpClient.get<any>('/api/v1/admin/users/activity-log'),

  /**
   * Get agent chat history (#4653)
   */
  getChatHistory: () => httpClient.get<any>('/api/v1/admin/agents/chat-history'),

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
