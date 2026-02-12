/**
 * MeepleAI Permission API Client SDK
 * Epic #4068 - Issue #4177
 *
 * @packageDocumentation
 * @module @meepleai/sdk/permissions
 */

import type {
  UserPermissions,
  PermissionCheckRequest,
  PermissionCheckResponse,
  UserTier,
  UserRole
} from './types/permissions';

export interface PermissionClientOptions {
  /** Base API URL */
  baseUrl: string;
  /** Auth token getter (called before each request) */
  getAuthToken: () => Promise<string> | string;
  /** Request timeout in milliseconds (default: 10000) */
  timeout?: number;
  /** Retry failed requests (default: 3) */
  retries?: number;
  /** Cache TTL in milliseconds (default: 300000 = 5 min) */
  cacheTTL?: number;
}

/**
 * Permission API client for MeepleAI permission system
 *
 * @example
 * ```typescript
 * const client = new PermissionClient({
 *   baseUrl: 'https://api.meepleai.com',
 *   getAuthToken: () => localStorage.getItem('token')!
 * });
 *
 * // Get user permissions
 * const permissions = await client.getMyPermissions();
 * console.log('Tier:', permissions.tier);
 *
 * // Check feature access
 * const canBulkSelect = await client.canAccess('bulk-select');
 * if (!canBulkSelect) {
 *   console.log('Upgrade to Pro for bulk selection');
 * }
 * ```
 */
export class PermissionClient {
  private readonly baseUrl: string;
  private readonly getAuthToken: () => Promise<string> | string;
  private readonly timeout: number;
  private readonly retries: number;
  private readonly cacheTTL: number;

  // In-memory cache
  private cache: Map<string, { data: unknown; expiresAt: number }> = new Map();

  constructor(options: PermissionClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/$/, ''); // Remove trailing slash
    this.getAuthToken = options.getAuthToken;
    this.timeout = options.timeout ?? 10000;
    this.retries = options.retries ?? 3;
    this.cacheTTL = options.cacheTTL ?? 300000; // 5 minutes default
  }

  /**
   * Get current user's permissions and accessible features
   *
   * @returns User permissions object with tier, role, limits, and features
   * @throws {PermissionAPIError} If request fails or user not authenticated
   *
   * @example
   * ```typescript
   * const permissions = await client.getMyPermissions();
   * console.log('Tier:', permissions.tier);
   * console.log('Max games:', permissions.limits.maxGames);
   * console.log('Features:', permissions.accessibleFeatures);
   * ```
   */
  async getMyPermissions(): Promise<UserPermissions> {
    const cacheKey = 'permissions:me';

    // Check cache
    const cached = this.getFromCache<UserPermissions>(cacheKey);
    if (cached) return cached;

    // Fetch from API
    const response = await this.request<UserPermissions>('/api/v1/permissions/me');

    // Cache result
    this.setCache(cacheKey, response);

    return response;
  }

  /**
   * Check if user has access to specific feature
   *
   * @param feature - Feature name (e.g., "wishlist", "bulk-select")
   * @param state - Optional resource state for state-based checks
   * @returns Permission check response with access decision and reason
   *
   * @example
   * ```typescript
   * const result = await client.checkPermission('bulk-select');
   * if (result.hasAccess) {
   *   enableBulkSelection();
   * } else {
   *   showUpgradePrompt(result.details.required.tier);
   * }
   * ```
   */
  async checkPermission(feature: string, state?: string): Promise<PermissionCheckResponse> {
    const cacheKey = `permission:check:${feature}:${state || 'none'}`;

    // Check cache
    const cached = this.getFromCache<PermissionCheckResponse>(cacheKey);
    if (cached) return cached;

    // Build query params
    const params = new URLSearchParams({ feature });
    if (state) params.append('state', state);

    // Fetch from API
    const response = await this.request<PermissionCheckResponse>(
      `/api/v1/permissions/check?${params.toString()}`
    );

    // Cache result
    this.setCache(cacheKey, response);

    return response;
  }

  /**
   * Simple boolean check if user can access feature
   *
   * @param feature - Feature name
   * @param state - Optional resource state
   * @returns True if user has access, false otherwise
   *
   * @example
   * ```typescript
   * if (await client.canAccess('bulk-select')) {
   *   showBulkSelectButton();
   * }
   * ```
   */
  async canAccess(feature: string, state?: string): Promise<boolean> {
    try {
      const result = await this.checkPermission(feature, state);
      return result.hasAccess;
    } catch {
      return false; // Fail-safe: Deny on error
    }
  }

  /**
   * Check if user has minimum required tier
   *
   * @param requiredTier - Minimum tier required
   * @returns True if user tier >= requiredTier
   *
   * @example
   * ```typescript
   * if (await client.hasTier('pro')) {
   *   showProFeatures();
   * }
   * ```
   */
  async hasTier(requiredTier: UserTier): Promise<boolean> {
    const permissions = await this.getMyPermissions();
    return this.compareTiers(permissions.tier, requiredTier) >= 0;
  }

  /**
   * Check if user has minimum required role
   *
   * @param requiredRole - Minimum role required
   * @returns True if user role >= requiredRole
   */
  async hasRole(requiredRole: UserRole): Promise<boolean> {
    const permissions = await this.getMyPermissions();
    return this.compareRoles(permissions.role, requiredRole) >= 0;
  }

  /**
   * Check if user has admin privileges
   *
   * @returns True if user is admin or superadmin
   */
  async isAdmin(): Promise<boolean> {
    const permissions = await this.getMyPermissions();
    return permissions.role === 'admin' || permissions.role === 'superadmin';
  }

  /**
   * Batch check multiple permissions (efficient)
   *
   * @param features - Array of feature names to check
   * @returns Map of feature → hasAccess boolean
   *
   * @example
   * ```typescript
   * const results = await client.batchCheckPermissions(['wishlist', 'bulk-select', 'drag-drop']);
   * console.log('Wishlist:', results.wishlist);
   * console.log('Bulk Select:', results['bulk-select']);
   * ```
   */
  async batchCheckPermissions(features: string[]): Promise<Record<string, boolean>> {
    // Check cache first
    const results: Record<string, boolean> = {};
    const uncached: string[] = [];

    features.forEach(feature => {
      const cached = this.getFromCache<PermissionCheckResponse>(`permission:check:${feature}:none`);
      if (cached) {
        results[feature] = cached.hasAccess;
      } else {
        uncached.push(feature);
      }
    });

    // Fetch uncached in parallel
    if (uncached.length > 0) {
      const responses = await Promise.all(
        uncached.map(f => this.checkPermission(f))
      );

      uncached.forEach((feature, index) => {
        results[feature] = responses[index].hasAccess;
      });
    }

    return results;
  }

  /**
   * Invalidate permission cache (force refetch)
   *
   * @example
   * ```typescript
   * // After tier upgrade
   * await upgradeTier('pro');
   * client.invalidateCache();
   * const newPermissions = await client.getMyPermissions(); // Fresh from API
   * ```
   */
  invalidateCache(): void {
    this.cache.clear();
  }

  /**
   * Invalidate specific feature cache
   */
  invalidateFeature(feature: string): void {
    const keysToDelete: string[] = [];

    this.cache.forEach((_, key) => {
      if (key.startsWith(`permission:check:${feature}:`)) {
        keysToDelete.push(key);
      }
    });

    keysToDelete.forEach(key => this.cache.delete(key));
  }

  // ============================================================================
  // Private Methods
  // ============================================================================

  private async request<T>(path: string, options: RequestInit = {}): Promise<T> {
    const token = await this.getAuthToken();

    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), this.timeout);

    try {
      const response = await this.fetchWithRetry(
        `${this.baseUrl}${path}`,
        {
          ...options,
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json',
            ...options.headers
          },
          signal: controller.signal
        }
      );

      if (!response.ok) {
        throw new PermissionAPIError(
          `API request failed: ${response.status} ${response.statusText}`,
          response.status,
          await response.json().catch(() => null)
        );
      }

      return await response.json();
    } finally {
      clearTimeout(timeoutId);
    }
  }

  private async fetchWithRetry(url: string, options: RequestInit, attempt = 1): Promise<Response> {
    try {
      return await fetch(url, options);
    } catch (error) {
      if (attempt < this.retries) {
        // Exponential backoff: 1s, 2s, 4s
        const delay = 1000 * Math.pow(2, attempt - 1);
        await new Promise(resolve => setTimeout(resolve, delay));

        return this.fetchWithRetry(url, options, attempt + 1);
      }

      throw error;
    }
  }

  private getFromCache<T>(key: string): T | null {
    const cached = this.cache.get(key);

    if (!cached) return null;

    // Check expiration
    if (Date.now() > cached.expiresAt) {
      this.cache.delete(key);
      return null;
    }

    return cached.data as T;
  }

  private setCache(key: string, data: unknown): void {
    this.cache.set(key, {
      data,
      expiresAt: Date.now() + this.cacheTTL
    });
  }

  private compareTiers(userTier: UserTier, requiredTier: UserTier): number {
    const hierarchy: Record<UserTier, number> = {
      free: 0,
      normal: 1,
      premium: 2,
      pro: 2,
      enterprise: 3
    };

    return hierarchy[userTier] - hierarchy[requiredTier];
  }

  private compareRoles(userRole: UserRole, requiredRole: UserRole): number {
    const hierarchy: Record<UserRole, number> = {
      user: 0,
      editor: 1,
      creator: 2,
      admin: 3,
      superadmin: 4
    };

    return hierarchy[userRole] - hierarchy[requiredRole];
  }
}

/**
 * Custom error class for Permission API errors
 */
export class PermissionAPIError extends Error {
  constructor(
    message: string,
    public readonly status: number,
    public readonly body?: unknown
  ) {
    super(message);
    this.name = 'PermissionAPIError';
  }

  /**
   * Check if error is authentication failure
   */
  isAuthError(): boolean {
    return this.status === 401;
  }

  /**
   * Check if error is permission denied
   */
  isPermissionDenied(): boolean {
    return this.status === 403;
  }

  /**
   * Check if error is rate limited
   */
  isRateLimited(): boolean {
    return this.status === 429;
  }
}

// ============================================================================
// React Hooks Wrapper (Optional)
// ============================================================================

/**
 * React hook wrapper for PermissionClient (requires React Query)
 *
 * @example
 * ```tsx
 * const client = usePermissionClient();
 * const { data: permissions } = useQuery({
 *   queryKey: ['permissions', 'me'],
 *   queryFn: () => client.getMyPermissions()
 * });
 * ```
 */
export function createPermissionClientHooks(client: PermissionClient) {
  return {
    /**
     * Hook: Get user permissions
     */
    useMyPermissions: () => {
      const { useQuery } = require('@tanstack/react-query');

      return useQuery({
        queryKey: ['permissions', 'me'],
        queryFn: () => client.getMyPermissions(),
        staleTime: client['cacheTTL'], // Use client's cache TTL
        retry: (failureCount, error: PermissionAPIError) => {
          if (error.isAuthError()) return false; // Don't retry auth errors
          return failureCount < 3;
        }
      });
    },

    /**
     * Hook: Check feature permission
     */
    useFeaturePermission: (feature: string, state?: string) => {
      const { useQuery } = require('@tanstack/react-query');

      return useQuery({
        queryKey: ['permission', 'check', feature, state],
        queryFn: () => client.checkPermission(feature, state),
        staleTime: client['cacheTTL']
      });
    },

    /**
     * Hook: Boolean check if user can access feature
     */
    useCanAccess: (feature: string, state?: string) => {
      const { useQuery } = require('@tanstack/react-query');

      return useQuery({
        queryKey: ['permission', 'can-access', feature, state],
        queryFn: () => client.canAccess(feature, state),
        staleTime: client['cacheTTL']
      });
    }
  };
}

// ============================================================================
// Factory Functions
// ============================================================================

/**
 * Create permission client for browser usage
 */
export function createBrowserPermissionClient(
  baseUrl: string,
  tokenKey: string = 'meepleai_token'
): PermissionClient {
  return new PermissionClient({
    baseUrl,
    getAuthToken: () => {
      const token = localStorage.getItem(tokenKey);
      if (!token) throw new Error('Not authenticated');
      return token;
    },
    timeout: 10000,
    retries: 3,
    cacheTTL: 300000
  });
}

/**
 * Create permission client for Node.js usage (server-side)
 */
export function createServerPermissionClient(
  baseUrl: string,
  apiKey: string
): PermissionClient {
  return new PermissionClient({
    baseUrl,
    getAuthToken: () => apiKey,
    timeout: 5000,
    retries: 2,
    cacheTTL: 600000 // 10 minutes (server-side can cache longer)
  });
}

// ============================================================================
// Usage Examples
// ============================================================================

/**
 * Example 1: Browser usage
 *
 * ```typescript
 * import { createBrowserPermissionClient } from '@meepleai/sdk/permissions';
 *
 * const permissionClient = createBrowserPermissionClient('https://api.meepleai.com');
 *
 * // Get permissions
 * const permissions = await permissionClient.getMyPermissions();
 * console.log('Tier:', permissions.tier);
 *
 * // Check feature
 * if (await permissionClient.canAccess('bulk-select')) {
 *   enableBulkSelection();
 * }
 * ```
 */

/**
 * Example 2: Server-side usage (Next.js API route)
 *
 * ```typescript
 * import { createServerPermissionClient } from '@meepleai/sdk/permissions';
 *
 * const permissionClient = createServerPermissionClient(
 *   process.env.API_URL!,
 *   process.env.API_KEY!
 * );
 *
 * export async function GET(request: Request) {
 *   const userId = request.headers.get('X-User-ID');
 *
 *   const permissions = await permissionClient.getMyPermissions();
 *
 *   return Response.json(permissions);
 * }
 * ```
 */

/**
 * Example 3: React integration
 *
 * ```tsx
 * import { createBrowserPermissionClient, createPermissionClientHooks } from '@meepleai/sdk/permissions';
 *
 * const client = createBrowserPermissionClient('https://api.meepleai.com');
 * const hooks = createPermissionClientHooks(client);
 *
 * function MyComponent() {
 *   const { data: permissions, isLoading } = hooks.useMyPermissions();
 *   const { data: canBulkSelect } = hooks.useCanAccess('bulk-select');
 *
 *   if (isLoading) return <Spinner />;
 *
 *   return (
 *     <div>
 *       <p>Tier: {permissions.tier}</p>
 *       {canBulkSelect && <BulkSelectButton />}
 *     </div>
 *   );
 * }
 * ```
 */

/**
 * Example 4: Batch permission checks
 *
 * ```typescript
 * const results = await permissionClient.batchCheckPermissions([
 *   'wishlist',
 *   'bulk-select',
 *   'drag-drop',
 *   'agent.create'
 * ]);
 *
 * console.log('Wishlist:', results.wishlist); // true/false
 * console.log('Bulk Select:', results['bulk-select']);
 * console.log('Drag & Drop:', results['drag-drop']);
 * console.log('Agent Create:', results['agent.create']);
 * ```
 */

/**
 * Example 5: Cache invalidation
 *
 * ```typescript
 * // After user upgrades tier
 * await upgradeTier('pro');
 *
 * // Clear cache to fetch fresh permissions
 * permissionClient.invalidateCache();
 *
 * // Next call fetches from API (not cache)
 * const newPermissions = await permissionClient.getMyPermissions();
 * console.log('New tier:', newPermissions.tier); // "pro"
 * ```
 */

/**
 * Example 6: Error handling
 *
 * ```typescript
 * try {
 *   const permissions = await permissionClient.getMyPermissions();
 * } catch (error) {
 *   if (error instanceof PermissionAPIError) {
 *     if (error.isAuthError()) {
 *       // Redirect to login
 *       window.location.href = '/login';
 *     } else if (error.isRateLimited()) {
 *       // Wait and retry
 *       await new Promise(r => setTimeout(r, 10000));
 *       return permissionClient.getMyPermissions();
 *     } else {
 *       console.error('Permission API error:', error.message);
 *     }
 *   }
 * }
 * ```
 */
