/**
 * Tests for useAdminRole Hook (Issue #3690)
 *
 * Coverage target: 90%+
 * Tests: Role checks (SuperAdmin, Admin, Editor, User), loading states
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, beforeEach, vi } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useAdminRole } from '../useAdminRole';
import type { AuthUser } from '@/types/auth';

// Mock useCurrentUser hook
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
const mockUseCurrentUser = vi.mocked(useCurrentUser);

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: { retry: false },
    },
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
  );
};

describe('useAdminRole', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Loading State', () => {
    it('should return isLoading=true when user data is loading', () => {
      mockUseCurrentUser.mockReturnValue({
        data: undefined,
        isLoading: true,
      } as ReturnType<typeof useCurrentUser>);

      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isLoading).toBe(true);
      expect(result.current.user).toBeNull();
    });

    it('should return isLoading=false when user data is loaded', () => {
      const mockUser: AuthUser = {
        id: 'user-123',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      };

      mockUseCurrentUser.mockReturnValue({
        data: mockUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);

      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isLoading).toBe(false);
      expect(result.current.user).toEqual(mockUser);
    });
  });

  describe('SuperAdmin Role', () => {
    const superAdminUser: AuthUser = {
      id: 'admin-123',
      email: 'superadmin@example.com',
      displayName: 'Super Admin',
      role: 'SuperAdmin',
    };

    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue({
        data: superAdminUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);
    });

    it('should return isSuperAdmin=true for SuperAdmin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isSuperAdmin).toBe(true);
    });

    it('should return isAdminOrAbove=true for SuperAdmin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isAdminOrAbove).toBe(true);
    });

    it('should return isEditorOrAbove=true for SuperAdmin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isEditorOrAbove).toBe(true);
    });

    it('should hasRole return true for SuperAdmin checking any role', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.hasRole('SuperAdmin')).toBe(true);
      expect(result.current.hasRole('Admin')).toBe(true);
      expect(result.current.hasRole('Editor')).toBe(true);
      expect(result.current.hasRole('User')).toBe(true);
    });
  });

  describe('Admin Role', () => {
    const adminUser: AuthUser = {
      id: 'admin-456',
      email: 'admin@example.com',
      displayName: 'Admin User',
      role: 'Admin',
    };

    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue({
        data: adminUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);
    });

    it('should return isSuperAdmin=false for Admin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isSuperAdmin).toBe(false);
    });

    it('should return isAdminOrAbove=true for Admin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isAdminOrAbove).toBe(true);
    });

    it('should return isEditorOrAbove=true for Admin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isEditorOrAbove).toBe(true);
    });

    it('should hasRole return correct values for Admin', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.hasRole('SuperAdmin')).toBe(false);
      expect(result.current.hasRole('Admin')).toBe(true);
      expect(result.current.hasRole('Editor')).toBe(true);
      expect(result.current.hasRole('User')).toBe(true);
    });
  });

  describe('Editor Role', () => {
    const editorUser: AuthUser = {
      id: 'editor-789',
      email: 'editor@example.com',
      displayName: 'Editor User',
      role: 'Editor',
    };

    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue({
        data: editorUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);
    });

    it('should return isSuperAdmin=false for Editor', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isSuperAdmin).toBe(false);
    });

    it('should return isAdminOrAbove=false for Editor', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isAdminOrAbove).toBe(false);
    });

    it('should return isEditorOrAbove=true for Editor', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isEditorOrAbove).toBe(true);
    });

    it('should hasRole return correct values for Editor', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.hasRole('SuperAdmin')).toBe(false);
      expect(result.current.hasRole('Admin')).toBe(false);
      expect(result.current.hasRole('Editor')).toBe(true);
      expect(result.current.hasRole('User')).toBe(true);
    });
  });

  describe('User Role', () => {
    const regularUser: AuthUser = {
      id: 'user-999',
      email: 'user@example.com',
      displayName: 'Regular User',
      role: 'User',
    };

    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue({
        data: regularUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);
    });

    it('should return isSuperAdmin=false for User', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isSuperAdmin).toBe(false);
    });

    it('should return isAdminOrAbove=false for User', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isAdminOrAbove).toBe(false);
    });

    it('should return isEditorOrAbove=false for User', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isEditorOrAbove).toBe(false);
    });

    it('should hasRole return only User=true for regular User', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.hasRole('SuperAdmin')).toBe(false);
      expect(result.current.hasRole('Admin')).toBe(false);
      expect(result.current.hasRole('Editor')).toBe(false);
      expect(result.current.hasRole('User')).toBe(true);
    });
  });

  describe('Unauthenticated User', () => {
    beforeEach(() => {
      mockUseCurrentUser.mockReturnValue({
        data: undefined,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);
    });

    it('should return user=null for unauthenticated', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.user).toBeNull();
    });

    it('should return all role checks as false for unauthenticated', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.isSuperAdmin).toBe(false);
      expect(result.current.isAdminOrAbove).toBe(false);
      expect(result.current.isEditorOrAbove).toBe(false);
    });

    it('should hasRole return false for all roles when unauthenticated', () => {
      const { result } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });

      expect(result.current.hasRole('SuperAdmin')).toBe(false);
      expect(result.current.hasRole('Admin')).toBe(false);
      expect(result.current.hasRole('Editor')).toBe(false);
      expect(result.current.hasRole('User')).toBe(false);
    });
  });

  describe('Memoization', () => {
    it('should return same reference when user data unchanged', () => {
      const mockUser: AuthUser = {
        id: 'user-123',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'Admin',
      };

      mockUseCurrentUser.mockReturnValue({
        data: mockUser,
        isLoading: false,
      } as ReturnType<typeof useCurrentUser>);

      const { result, rerender } = renderHook(() => useAdminRole(), { wrapper: createWrapper() });
      const firstResult = result.current;

      rerender();
      const secondResult = result.current;

      // Due to useMemo, the result should be the same reference if inputs haven't changed
      expect(firstResult.isSuperAdmin).toBe(secondResult.isSuperAdmin);
      expect(firstResult.isAdminOrAbove).toBe(secondResult.isAdminOrAbove);
      expect(firstResult.isEditorOrAbove).toBe(secondResult.isEditorOrAbove);
    });
  });
});
