/**
 * useNavigationItems Hook Tests
 * Tests navigation item filtering based on auth state and role.
 */

import { renderHook } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { useNavigationItems } from '../useNavigationItems';

// Mock useCurrentUser
const mockUseCurrentUser = vi.fn();
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

// Mock next/navigation
vi.mock('next/navigation', () => ({
  usePathname: () => '/dashboard',
  useRouter: () => ({ push: vi.fn() }),
}));

describe('useNavigationItems', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns only public items when not authenticated', () => {
    mockUseCurrentUser.mockReturnValue({
      data: null,
      isLoading: false,
    });

    const { result } = renderHook(() => useNavigationItems());

    const ids = result.current.items.map(item => item.id);
    expect(ids).toContain('welcome');
    expect(ids).toContain('catalog');
    expect(ids).not.toContain('dashboard');
    expect(ids).not.toContain('library');
    expect(result.current.isAuthenticated).toBe(false);
  });

  it('returns auth items and hides welcome when authenticated as User', () => {
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User' },
      isLoading: false,
    });

    const { result } = renderHook(() => useNavigationItems());

    const ids = result.current.items.map(item => item.id);
    expect(ids).not.toContain('welcome');
    expect(ids).toContain('dashboard');
    expect(ids).toContain('library');
    expect(ids).toContain('catalog');
    expect(ids).toContain('chat');
    expect(ids).toContain('profile');
    expect(ids).toContain('agents');
    expect(ids).toContain('sessions');
    expect(result.current.isAuthenticated).toBe(true);
  });

  it('returns minimal items while auth is loading', () => {
    mockUseCurrentUser.mockReturnValue({
      data: null,
      isLoading: true,
    });

    const { result } = renderHook(() => useNavigationItems());

    const ids = result.current.items.map(item => item.id);
    // Only non-auth-restricted items (catalog)
    expect(ids).toContain('catalog');
    expect(ids).not.toContain('welcome');
    expect(ids).not.toContain('dashboard');
    expect(result.current.isAuthLoading).toBe(true);
  });

  it('exposes isItemActive function', () => {
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User' },
      isLoading: false,
    });

    const { result } = renderHook(() => useNavigationItems());

    expect(typeof result.current.isItemActive).toBe('function');

    const dashboard = result.current.items.find(item => item.id === 'dashboard');
    if (dashboard) {
      expect(result.current.isItemActive(dashboard, '/dashboard')).toBe(true);
      expect(result.current.isItemActive(dashboard, '/games')).toBe(false);
    }
  });

  it('returns items with LucideIcon components', () => {
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User' },
      isLoading: false,
    });

    const { result } = renderHook(() => useNavigationItems());

    result.current.items.forEach(item => {
      // LucideIcon can be function or ForwardRef object depending on environment
      expect(['function', 'object']).toContain(typeof item.icon);
      expect(item.icon).toBeTruthy();
      expect(typeof item.iconName).toBe('string');
    });
  });

  it('library item has children from library-navigation', () => {
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User' },
      isLoading: false,
    });

    const { result } = renderHook(() => useNavigationItems());

    const library = result.current.items.find(item => item.id === 'library');
    expect(library).toBeDefined();
    expect(library!.children).toBeDefined();
    expect(library!.children!.length).toBeGreaterThan(0);

    // Check children have expected structure
    library!.children!.forEach(child => {
      expect(child.id).toBeDefined();
      expect(child.href).toBeDefined();
      expect(child.label).toBeDefined();
      expect(child.ariaLabel).toBeDefined();
    });
  });
});
