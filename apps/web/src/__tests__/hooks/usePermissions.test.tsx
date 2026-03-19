/**
 * Tests for usePermissions hook
 * Epic #4068 - Issue #4178
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { PermissionProvider, usePermissions } from '@/contexts/PermissionContext';
import * as permissionsApi from '@/lib/api/permissions';
import type { UserPermissions } from '@/types/permissions';

// Mock logger — source uses logger.error, not console.error directly
const mockLoggerError = vi.fn();
vi.mock('@/lib/logger', () => ({
  logger: {
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  },
  getLogger: () => ({
    debug: vi.fn(),
    info: vi.fn(),
    warn: vi.fn(),
    error: (...args: unknown[]) => mockLoggerError(...args),
  }),
  resetLogger: vi.fn(),
  LogLevel: { DEBUG: 'debug', INFO: 'info', WARN: 'warn', ERROR: 'error' },
}));

// Mock the API
vi.mock('@/lib/api/permissions');

describe('usePermissions', () => {
  let queryClient: QueryClient;

  beforeEach(() => {
    queryClient = new QueryClient({
      defaultOptions: {
        queries: {
          retry: false,
          gcTime: 0,
          retryDelay: 0, // PermissionProvider uses retry: 2, so make retries instant
        },
      },
    });
    vi.clearAllMocks();
  });

  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      <PermissionProvider>{children}</PermissionProvider>
    </QueryClientProvider>
  );

  describe('Hook Initialization', () => {
    it('throws error when used outside PermissionProvider', () => {
      // Suppress console.error for this test
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => usePermissions());
      }).toThrow('usePermissions must be within PermissionProvider');

      consoleSpy.mockRestore();
    });

    it('provides default values while loading', () => {
      vi.mocked(permissionsApi.getUserPermissions).mockImplementation(
        () => new Promise(() => {}) // Never resolves (loading state)
      );

      const { result } = renderHook(() => usePermissions(), { wrapper });

      expect(result.current.tier).toBe('free');
      expect(result.current.role).toBe('user');
      expect(result.current.loading).toBe(true);
    });
  });

  describe('Permission Data Loading', () => {
    it('loads and provides user permissions', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'pro',
        role: 'creator',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop', 'agent.create'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.tier).toBe('pro');
      expect(result.current.role).toBe('creator');
      expect(result.current.canAccess('wishlist')).toBe(true);
      expect(result.current.canAccess('bulk-select')).toBe(true);
      expect(result.current.isAdmin()).toBe(false);
    });

    it('handles Free tier user correctly', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'free',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: ['wishlist'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.tier).toBe('free');
      expect(result.current.canAccess('wishlist')).toBe(true);
      expect(result.current.canAccess('bulk-select')).toBe(false);
      expect(result.current.canAccess('drag-drop')).toBe(false);
    });

    it('handles Enterprise tier user correctly', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'enterprise',
        role: 'admin',
        status: 'Active',
        limits: { maxGames: 2147483647, storageQuotaMB: 2147483647 },
        accessibleFeatures: [
          'wishlist',
          'bulk-select',
          'drag-drop',
          'agent.create',
          'analytics.view',
        ],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.tier).toBe('enterprise');
      expect(result.current.role).toBe('admin');
      expect(result.current.isAdmin()).toBe(true);
      expect(result.current.canAccess('analytics.view')).toBe(true);
    });
  });

  describe('Error Handling', () => {
    it('falls back to safe defaults on API error', async () => {
      mockLoggerError.mockClear();

      vi.mocked(permissionsApi.getUserPermissions).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Safe defaults applied
      expect(result.current.tier).toBe('free');
      expect(result.current.role).toBe('user');
      expect(result.current.canAccess('any-feature')).toBe(false); // Deny all on error
      expect(result.current.isAdmin()).toBe(false);
      expect(result.current.hasTier('pro')).toBe(false);

      // Source uses logger.error, not console.error directly
      expect(mockLoggerError).toHaveBeenCalledWith(
        'Failed to load user permissions:',
        expect.any(Error)
      );
    });

    it('retries failed requests up to 2 times', async () => {
      let callCount = 0;
      vi.mocked(permissionsApi.getUserPermissions).mockImplementation(() => {
        callCount++;
        if (callCount < 3) {
          return Promise.reject(new Error('Temporary failure'));
        }
        return Promise.resolve({
          tier: 'normal',
          role: 'user',
          status: 'Active',
          limits: { maxGames: 100, storageQuotaMB: 500 },
          accessibleFeatures: ['wishlist', 'drag-drop'],
        });
      });

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Should succeed after retries
      expect(callCount).toBe(3); // Initial + 2 retries
      expect(result.current.tier).toBe('normal');
    });
  });

  describe('Permission Check Methods', () => {
    const mockPermissions: UserPermissions = {
      tier: 'pro',
      role: 'creator',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: ['wishlist', 'bulk-select', 'drag-drop', 'agent.create'],
    };

    beforeEach(() => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);
    });

    it('canAccess() checks feature in accessibleFeatures list', async () => {
      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.canAccess('wishlist')).toBe(true);
      expect(result.current.canAccess('bulk-select')).toBe(true);
      expect(result.current.canAccess('non-existent-feature')).toBe(false);
    });

    it('hasTier() checks minimum tier level', async () => {
      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // User is Pro (level 2)
      expect(result.current.hasTier('free')).toBe(true); // free < pro
      expect(result.current.hasTier('normal')).toBe(true); // normal < pro
      expect(result.current.hasTier('pro')).toBe(true); // pro == pro
      expect(result.current.hasTier('enterprise')).toBe(false); // enterprise > pro
    });

    it('isAdmin() returns true for admin and superadmin roles', async () => {
      const adminPermissions: UserPermissions = {
        ...mockPermissions,
        role: 'admin',
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.isAdmin()).toBe(true);
    });

    it('isAdmin() returns false for non-admin roles', async () => {
      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Creator role (not admin)
      expect(result.current.isAdmin()).toBe(false);
    });
  });

  describe('Cache Behavior', () => {
    it('caches permissions for 5 minutes (staleTime)', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result, rerender } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(permissionsApi.getUserPermissions).toHaveBeenCalledTimes(1);

      // Rerender (simulates re-mount)
      rerender();

      // Should NOT refetch (cache hit within 5min)
      expect(permissionsApi.getUserPermissions).toHaveBeenCalledTimes(1);
    });

    it('does not make redundant API calls on multiple hook usages', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist', 'bulk-select'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      // Render hook twice (simulates multiple components using it)
      const { result: result1 } = renderHook(() => usePermissions(), { wrapper });
      const { result: result2 } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result1.current.loading).toBe(false);
        expect(result2.current.loading).toBe(false);
      });

      // API called only ONCE (React Query deduplication)
      expect(permissionsApi.getUserPermissions).toHaveBeenCalledTimes(1);

      // Both hooks return same data
      expect(result1.current.tier).toBe('pro');
      expect(result2.current.tier).toBe('pro');
    });
  });

  describe('Edge Cases', () => {
    it('handles missing accessibleFeatures gracefully', async () => {
      const incompletePermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: undefined as unknown as string[],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(
        incompletePermissions as UserPermissions
      );

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Should not crash, returns false for all features
      expect(result.current.canAccess('wishlist')).toBe(false);
    });

    it('handles null tier/role gracefully', async () => {
      const nullPermissions = {
        tier: null,
        role: null,
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: [],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(
        nullPermissions as unknown as UserPermissions
      );

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Falls back to 'free' and 'user'
      expect(result.current.tier).toBe('free');
      expect(result.current.role).toBe('user');
    });
  });

  describe('Loading States', () => {
    it('sets loading=true during initial fetch', () => {
      vi.mocked(permissionsApi.getUserPermissions).mockImplementation(
        () => new Promise(() => {}) // Never resolves
      );

      const { result } = renderHook(() => usePermissions(), { wrapper });

      expect(result.current.loading).toBe(true);
      expect(result.current.tier).toBe('free'); // Default while loading
    });

    it('sets loading=false after successful fetch', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.tier).toBe('normal');
    });

    it('sets loading=false after error', async () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      vi.mocked(permissionsApi.getUserPermissions).mockRejectedValue(new Error('API error'));

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Falls back to safe defaults
      expect(result.current.tier).toBe('free');

      consoleSpy.mockRestore();
    });
  });

  describe('Tier Hierarchy Checks', () => {
    const proUser: UserPermissions = {
      tier: 'pro',
      role: 'user',
      status: 'Active',
      limits: { maxGames: 500, storageQuotaMB: 5000 },
      accessibleFeatures: [],
    };

    beforeEach(() => {
      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(proUser);
    });

    it('hasTier() correctly compares tier levels', async () => {
      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      // Pro user (level 2)
      expect(result.current.hasTier('free')).toBe(true); // 0 < 2 ✓
      expect(result.current.hasTier('normal')).toBe(true); // 1 < 2 ✓
      expect(result.current.hasTier('premium')).toBe(true); // 2 == 2 ✓
      expect(result.current.hasTier('pro')).toBe(true); // 2 == 2 ✓
      expect(result.current.hasTier('enterprise')).toBe(false); // 3 > 2 ✗
    });
  });

  describe('Role Checks', () => {
    it('isAdmin() returns true for admin role', async () => {
      const adminUser: UserPermissions = {
        tier: 'normal',
        role: 'admin',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: [],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(adminUser);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.isAdmin()).toBe(true);
    });

    it('isAdmin() returns true for superadmin role', async () => {
      const superAdminUser: UserPermissions = {
        tier: 'free',
        role: 'superadmin',
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: [],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(superAdminUser);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.isAdmin()).toBe(true);
    });

    it('isAdmin() returns false for user/editor/creator roles', async () => {
      const roles: Array<'user' | 'editor' | 'creator'> = ['user', 'editor', 'creator'];

      for (const role of roles) {
        const mockPermissions: UserPermissions = {
          tier: 'pro',
          role,
          status: 'Active',
          limits: { maxGames: 500, storageQuotaMB: 5000 },
          accessibleFeatures: [],
        };

        vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

        const { result } = renderHook(() => usePermissions(), { wrapper });

        await waitFor(() => {
          expect(result.current.loading).toBe(false);
        });

        expect(result.current.isAdmin()).toBe(false);
      }
    });
  });

  describe('Feature Access Logic', () => {
    it('canAccess() case-sensitive feature name matching', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'pro',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 500, storageQuotaMB: 5000 },
        accessibleFeatures: ['wishlist', 'bulk-select'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.canAccess('wishlist')).toBe(true);
      expect(result.current.canAccess('Wishlist')).toBe(false); // Case-sensitive
      expect(result.current.canAccess('WISHLIST')).toBe(false);
    });

    it('canAccess() returns false for empty accessibleFeatures', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'free',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 50, storageQuotaMB: 100 },
        accessibleFeatures: [],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.canAccess('wishlist')).toBe(false);
      expect(result.current.canAccess('any-feature')).toBe(false);
    });
  });

  describe('React Query Integration', () => {
    it('uses correct query key', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(permissionsApi.getUserPermissions).toHaveBeenCalled();
      });

      // Verify query is cached with correct key
      const cache = queryClient.getQueryCache();
      const queries = cache.getAll();

      expect(
        queries.some(q => JSON.stringify(q.queryKey) === JSON.stringify(['permissions', 'me']))
      ).toBe(true);
    });

    it('invalidates cache when query client is invalidated', async () => {
      const mockPermissions: UserPermissions = {
        tier: 'normal',
        role: 'user',
        status: 'Active',
        limits: { maxGames: 100, storageQuotaMB: 500 },
        accessibleFeatures: ['wishlist'],
      };

      vi.mocked(permissionsApi.getUserPermissions).mockResolvedValue(mockPermissions);

      const { result } = renderHook(() => usePermissions(), { wrapper });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(permissionsApi.getUserPermissions).toHaveBeenCalledTimes(1);

      // Invalidate permissions query
      queryClient.invalidateQueries({ queryKey: ['permissions', 'me'] });

      await waitFor(() => {
        expect(permissionsApi.getUserPermissions).toHaveBeenCalledTimes(2);
      });
    });
  });
});
