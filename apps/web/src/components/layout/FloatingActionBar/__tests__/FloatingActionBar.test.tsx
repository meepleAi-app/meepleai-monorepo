/**
 * FloatingActionBar Tests — Auto-Hide on Scroll
 * Issue #4 from mobile-first-ux-epic.md
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect, beforeEach } from 'vitest';

import { FloatingActionBar } from '../FloatingActionBar';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockUseNavigation = vi.fn();
vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => mockUseNavigation(),
}));

const mockUseResponsive = vi.fn();
const mockUsePrefersReducedMotion = vi.fn(() => false);
vi.mock('@/hooks/useResponsive', () => ({
  useResponsive: () => mockUseResponsive(),
  usePrefersReducedMotion: () => mockUsePrefersReducedMotion(),
}));

const mockUseScrollDirection = vi.fn();
vi.mock('@/hooks/useScrollDirection', () => ({
  useScrollDirection: (opts?: unknown) => mockUseScrollDirection(opts),
}));

// ─── Helpers ─────────────────────────────────────────────────────────────────

const DummyIcon = () => <span data-testid="icon">icon</span>;

function withActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [
      { id: 'save', label: 'Save', icon: DummyIcon, variant: 'primary', onClick: vi.fn() },
    ],
    miniNavTabs: [],
  });
}

function withNoActions() {
  mockUseNavigation.mockReturnValue({
    actionBarActions: [],
    miniNavTabs: [],
  });
}

function setMobile() {
  mockUseResponsive.mockReturnValue({
    isMobile: true,
    isTablet: false,
    isDesktop: false,
    deviceType: 'mobile',
  });
}

function setDesktop() {
  mockUseResponsive.mockReturnValue({
    isMobile: false,
    isTablet: false,
    isDesktop: true,
    deviceType: 'desktop',
  });
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('FloatingActionBar', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    setDesktop();
    mockUseScrollDirection.mockReturnValue(null);
    mockUsePrefersReducedMotion.mockReturnValue(false);
  });

  it('renders when actions are present', () => {
    withActions();
    render(<FloatingActionBar />);
    expect(screen.getByRole('toolbar')).toBeInTheDocument();
  });

  it('does not render when no actions', () => {
    withNoActions();
    const { container } = render(<FloatingActionBar />);
    expect(container.innerHTML).toBe('');
  });

  describe('Auto-hide on scroll', () => {
    it('hides on mobile when scrolling down', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue('down');
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar', { hidden: true });
      expect(toolbar).toHaveAttribute('aria-hidden', 'true');
      expect(toolbar.className).toContain('opacity-0');
    });

    it('shows on mobile when scrolling up', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue('up');
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar).toHaveAttribute('aria-hidden', 'false');
      expect(toolbar.className).not.toContain('opacity-0');
    });

    it('never hides on desktop regardless of scroll', () => {
      setDesktop();
      mockUseScrollDirection.mockReturnValue('down');
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar).toHaveAttribute('aria-hidden', 'false');
      expect(toolbar.className).not.toContain('opacity-0');
    });

    it('applies smooth transition when motion allowed', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue(null);
      mockUsePrefersReducedMotion.mockReturnValue(false);
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar.className).toContain('transition-');
    });

    it('uses invisible instead of animation when prefers-reduced-motion', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue('down');
      mockUsePrefersReducedMotion.mockReturnValue(true);
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar', { hidden: true });
      expect(toolbar.className).toContain('invisible');
      expect(toolbar.className).not.toContain('opacity-0');
    });

    it('shows when scrollDirection is null (initial state)', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue(null);
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar).toHaveAttribute('aria-hidden', 'false');
    });
  });

  describe('Mobile positioning', () => {
    it('positions above MobileTabBar on mobile', () => {
      setMobile();
      mockUseScrollDirection.mockReturnValue(null);
      withActions();

      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar.className).toContain('bottom-[calc(72px+1.5rem)]');
    });

    it('positions at bottom-6 on desktop', () => {
      withActions();
      render(<FloatingActionBar />);
      const toolbar = screen.getByRole('toolbar');
      expect(toolbar.className).toContain('md:bottom-6');
    });
  });

  it('passes threshold of 50 to useScrollDirection', () => {
    withActions();
    render(<FloatingActionBar />);
    expect(mockUseScrollDirection).toHaveBeenCalledWith({ threshold: 50 });
  });
});
