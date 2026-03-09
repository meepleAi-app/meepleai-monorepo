/**
 * Tests for useBottomPadding hook
 * Issue #2 from mobile-first-ux-epic.md
 *
 * Verifies dynamic padding calculation based on:
 * - Mobile/desktop viewport
 * - FloatingActionBar visibility (actionBarActions)
 */

import { renderHook } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { useBottomPadding } from '../useBottomPadding';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockUseResponsive = vi.fn();
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => mockUseResponsive(),
}));

const mockUseNavigation = vi.fn();
vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => mockUseNavigation(),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

function mockMobile() {
  mockUseResponsive.mockReturnValue({
    isMobile: true,
    isTablet: false,
    isDesktop: false,
    deviceType: 'mobile',
  });
}

function mockDesktop() {
  mockUseResponsive.mockReturnValue({
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    deviceType: 'desktop',
  });
}

function mockWithActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [{ id: 'save', label: 'Save', icon: () => null, onClick: vi.fn() }],
    miniNavTabs: [],
  });
}

function mockWithHiddenActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [
      { id: 'save', label: 'Save', icon: () => null, onClick: vi.fn(), hidden: true },
    ],
    miniNavTabs: [],
  });
}

function mockNoActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [],
    miniNavTabs: [],
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('useBottomPadding', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Mobile viewport', () => {
    beforeEach(() => {
      mockMobile();
    });

    it('returns pb-36 when MobileTabBar + FloatingActionBar both visible', () => {
      mockWithActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-36');
    });

    it('returns pb-24 when MobileTabBar only (no actions)', () => {
      mockNoActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-24');
    });

    it('returns pb-24 when all actions are hidden', () => {
      mockWithHiddenActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-24');
    });
  });

  describe('Desktop viewport', () => {
    beforeEach(() => {
      mockDesktop();
    });

    it('returns pb-24 when FloatingActionBar has actions', () => {
      mockWithActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-24');
    });

    it('returns pb-6 when no actions (minimal padding)', () => {
      mockNoActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-6');
    });

    it('returns pb-6 when all actions are hidden', () => {
      mockWithHiddenActions();
      const { result } = renderHook(() => useBottomPadding());
      expect(result.current).toBe('pb-6');
    });
  });
});
