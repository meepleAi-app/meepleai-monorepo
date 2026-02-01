/**
 * API Client for E2E Tests - Real Backend Integration
 * Issue #3082: Implement Missing E2E Test Flows
 *
 * Provides direct backend API calls for:
 * - Test data setup/teardown
 * - Real authentication flows
 * - Backend state verification
 *
 * Usage:
 *   const api = new ApiClient(page);
 *   await api.authenticate('admin@test.com', 'password');
 *   const user = await api.createUser({ email: 'test@test.com', ... });
 *   // ... run tests ...
 *   await api.deleteUser(user.id);
 */

import { Page, APIRequestContext } from '@playwright/test';

import type {
  User,
  UserRole,
  Game,
  ChatMessage,
  PromptTemplate,
  ConfigurationItem,
} from '../types/pom-interfaces';

// Environment-based API configuration
const API_BASE =
  process.env.PLAYWRIGHT_API_BASE || process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Test credentials from environment
const TEST_CREDENTIALS = {
  admin: {
    email: process.env.ADMIN_EMAIL || 'admin@meepleai.dev',
    password: process.env.ADMIN_PASSWORD || 'Demo123!',
  },
  editor: {
    email: process.env.EDITOR_EMAIL || 'editor@meepleai.dev',
    password: process.env.EDITOR_PASSWORD || 'Demo123!',
  },
  user: {
    email: process.env.USER_EMAIL || 'user@meepleai.dev',
    password: process.env.USER_PASSWORD || 'Demo123!',
  },
};

/**
 * API Response wrapper for consistent error handling
 */
export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: string;
  status: number;
}

/**
 * Authentication response from backend
 */
export interface AuthResponse {
  user: {
    id: string;
    email: string;
    displayName: string;
    role: UserRole;
  };
  expiresAt: string;
  accessToken?: string;
}

/**
 * Session information
 */
export interface SessionInfo {
  isAuthenticated: boolean;
  user?: AuthResponse['user'];
  expiresAt?: string;
}

/**
 * API Client for direct backend interaction in E2E tests
 *
 * Provides real backend integration without mocks for:
 * - Authentication and session management
 * - User CRUD operations
 * - Game management
 * - Admin operations
 * - Test data cleanup
 */
export class ApiClient {
  private request: APIRequestContext;
  private currentSession: SessionInfo = { isAuthenticated: false };

  constructor(private page: Page) {
    this.request = page.request;
  }

  // ============================================================================
  // Authentication
  // ============================================================================

  /**
   * Authenticate with real backend
   */
  async authenticate(email: string, password: string): Promise<ApiResponse<AuthResponse>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/auth/login`, {
        data: { email, password },
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const data = await response.json();
        this.currentSession = {
          isAuthenticated: true,
          user: data.user,
          expiresAt: data.expiresAt,
        };
        return { success: true, data, status: response.status() };
      }

      const errorText = await response.text();
      return {
        success: false,
        error: errorText || `Authentication failed: ${response.status()}`,
        status: response.status(),
      };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Authentication error',
        status: 0,
      };
    }
  }

  /**
   * Authenticate as admin user
   */
  async authenticateAsAdmin(): Promise<ApiResponse<AuthResponse>> {
    return this.authenticate(TEST_CREDENTIALS.admin.email, TEST_CREDENTIALS.admin.password);
  }

  /**
   * Authenticate as editor user
   */
  async authenticateAsEditor(): Promise<ApiResponse<AuthResponse>> {
    return this.authenticate(TEST_CREDENTIALS.editor.email, TEST_CREDENTIALS.editor.password);
  }

  /**
   * Authenticate as regular user
   */
  async authenticateAsUser(): Promise<ApiResponse<AuthResponse>> {
    return this.authenticate(TEST_CREDENTIALS.user.email, TEST_CREDENTIALS.user.password);
  }

  /**
   * Get current session info
   */
  async getCurrentSession(): Promise<ApiResponse<AuthResponse>> {
    try {
      const response = await this.request.get(`${API_BASE}/api/v1/auth/me`);

      if (response.ok()) {
        const data = await response.json();
        this.currentSession = {
          isAuthenticated: true,
          user: data.user,
          expiresAt: data.expiresAt,
        };
        return { success: true, data, status: response.status() };
      }

      this.currentSession = { isAuthenticated: false };
      return { success: false, error: 'Not authenticated', status: response.status() };
    } catch (error) {
      this.currentSession = { isAuthenticated: false };
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Session check error',
        status: 0,
      };
    }
  }

  /**
   * Logout current session
   */
  async logout(): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/auth/logout`);
      this.currentSession = { isAuthenticated: false };
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      this.currentSession = { isAuthenticated: false };
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Logout error',
        status: 0,
      };
    }
  }

  /**
   * Check if currently authenticated
   */
  isAuthenticated(): boolean {
    return this.currentSession.isAuthenticated;
  }

  // ============================================================================
  // User Management (Admin)
  // ============================================================================

  /**
   * Create a new user (requires admin)
   */
  async createUser(userData: Omit<User, 'id' | 'createdAt'>): Promise<ApiResponse<User>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/admin/users`, {
        data: userData,
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      const errorText = await response.text();
      return { success: false, error: errorText, status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Create user error',
        status: 0,
      };
    }
  }

  /**
   * Get user by ID
   */
  async getUser(userId: string): Promise<ApiResponse<User>> {
    try {
      const response = await this.request.get(`${API_BASE}/api/v1/admin/users/${userId}`);

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      return { success: false, error: 'User not found', status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Get user error',
        status: 0,
      };
    }
  }

  /**
   * Delete user by ID (requires admin)
   */
  async deleteUser(userId: string): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.delete(`${API_BASE}/api/v1/admin/users/${userId}`);
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Delete user error',
        status: 0,
      };
    }
  }

  /**
   * Unlock user account (requires admin)
   */
  async unlockUser(userId: string): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/admin/users/${userId}/unlock`);
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Unlock user error',
        status: 0,
      };
    }
  }

  // ============================================================================
  // Game Management
  // ============================================================================

  /**
   * Create a new game
   */
  async createGame(gameData: Omit<Game, 'id' | 'createdAt' | 'updatedAt'>): Promise<ApiResponse<Game>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/games`, {
        data: gameData,
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      const errorText = await response.text();
      return { success: false, error: errorText, status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Create game error',
        status: 0,
      };
    }
  }

  /**
   * Get all games
   */
  async getGames(): Promise<ApiResponse<Game[]>> {
    try {
      const response = await this.request.get(`${API_BASE}/api/v1/games`);

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      return { success: false, error: 'Failed to get games', status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Get games error',
        status: 0,
      };
    }
  }

  /**
   * Delete game by ID
   */
  async deleteGame(gameId: string): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.delete(`${API_BASE}/api/v1/games/${gameId}`);
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Delete game error',
        status: 0,
      };
    }
  }

  // ============================================================================
  // Chat/Threads
  // ============================================================================

  /**
   * Create a chat thread
   */
  async createChatThread(gameId: string): Promise<ApiResponse<{ id: string }>> {
    try {
      const response = await this.request.post(`${API_BASE}/api/v1/chat/threads`, {
        data: { gameId },
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      return { success: false, error: 'Failed to create thread', status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Create thread error',
        status: 0,
      };
    }
  }

  /**
   * Delete chat thread
   */
  async deleteChatThread(threadId: string): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.delete(`${API_BASE}/api/v1/chat/threads/${threadId}`);
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Delete thread error',
        status: 0,
      };
    }
  }

  // ============================================================================
  // Admin Configuration
  // ============================================================================

  /**
   * Get system configuration
   */
  async getConfiguration(category?: string): Promise<ApiResponse<ConfigurationItem[]>> {
    try {
      const url = category
        ? `${API_BASE}/api/v1/configuration?category=${category}`
        : `${API_BASE}/api/v1/configuration`;
      const response = await this.request.get(url);

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      return { success: false, error: 'Failed to get configuration', status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Get configuration error',
        status: 0,
      };
    }
  }

  /**
   * Update configuration item
   */
  async updateConfiguration(key: string, value: string | number | boolean): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.put(`${API_BASE}/api/v1/configuration/${key}`, {
        data: { value },
        headers: { 'Content-Type': 'application/json' },
      });
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Update configuration error',
        status: 0,
      };
    }
  }

  // ============================================================================
  // Health & Status
  // ============================================================================

  /**
   * Check backend health
   */
  async checkHealth(): Promise<ApiResponse<{ status: string }>> {
    try {
      const response = await this.request.get(`${API_BASE}/health`);

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      return { success: false, error: 'Backend unhealthy', status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'Health check error',
        status: 0,
      };
    }
  }

  /**
   * Wait for backend to be ready
   */
  async waitForBackend(timeoutMs: number = 30000): Promise<boolean> {
    const startTime = Date.now();
    while (Date.now() - startTime < timeoutMs) {
      const health = await this.checkHealth();
      if (health.success) {
        return true;
      }
      await new Promise((resolve) => setTimeout(resolve, 1000));
    }
    return false;
  }

  // ============================================================================
  // Generic Request Methods
  // ============================================================================

  /**
   * Make a GET request
   */
  async get<T>(endpoint: string): Promise<ApiResponse<T>> {
    try {
      const response = await this.request.get(`${API_BASE}${endpoint}`);

      if (response.ok()) {
        const data = await response.json();
        return { success: true, data, status: response.status() };
      }

      const errorText = await response.text();
      return { success: false, error: errorText, status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'GET request error',
        status: 0,
      };
    }
  }

  /**
   * Make a POST request
   */
  async post<T>(endpoint: string, data?: unknown): Promise<ApiResponse<T>> {
    try {
      const response = await this.request.post(`${API_BASE}${endpoint}`, {
        data,
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const responseData = await response.json().catch(() => ({}));
        return { success: true, data: responseData, status: response.status() };
      }

      const errorText = await response.text();
      return { success: false, error: errorText, status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'POST request error',
        status: 0,
      };
    }
  }

  /**
   * Make a PUT request
   */
  async put<T>(endpoint: string, data?: unknown): Promise<ApiResponse<T>> {
    try {
      const response = await this.request.put(`${API_BASE}${endpoint}`, {
        data,
        headers: { 'Content-Type': 'application/json' },
      });

      if (response.ok()) {
        const responseData = await response.json().catch(() => ({}));
        return { success: true, data: responseData, status: response.status() };
      }

      const errorText = await response.text();
      return { success: false, error: errorText, status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'PUT request error',
        status: 0,
      };
    }
  }

  /**
   * Make a DELETE request
   */
  async delete(endpoint: string): Promise<ApiResponse<void>> {
    try {
      const response = await this.request.delete(`${API_BASE}${endpoint}`);
      return { success: response.ok(), status: response.status() };
    } catch (error) {
      return {
        success: false,
        error: error instanceof Error ? error.message : 'DELETE request error',
        status: 0,
      };
    }
  }
}

// Export singleton factory for easy usage
export function createApiClient(page: Page): ApiClient {
  return new ApiClient(page);
}

// Export API_BASE for tests that need direct access
export { API_BASE, TEST_CREDENTIALS };
