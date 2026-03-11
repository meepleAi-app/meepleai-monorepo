/**
 * FloatingActionBar Safe Area Tests
 * Mobile UX Epic — Issue 9
 *
 * Verifies that FloatingActionBar is properly positioned
 * for devices with various safe areas and viewport configurations.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Plus, Settings } from 'lucide-react';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useResponsive', () => ({
  usePrefersReducedMotion: () => false,
  useResponsive: () => ({ isMobile: false, isTablet: false, isDesktop: true, deviceType: 'desktop' }),
  useIsMobile: () => false,
  useMediaQuery: () => false,
}));

vi.mock('@/hooks/useScrollDirection', () => ({
  useScrollDirection: () => 'up',
}));

vi.mock('@/hooks/useVirtualKeyboard', () => ({
  useVirtualKeyboard: () => ({ isKeyboardOpen: false }),
}));

const mockActions = vi.fn(() => [
  {
    id: 'save',
    label: 'Salva',
    icon: Plus,
    variant: 'primary' as const,
    onClick: vi.fn(),
  },
  {
    id: 'settings',
    label: 'Impostazioni',
    icon: Settings,
    variant: 'ghost' as const,
    onClick: vi.fn(),
  },
]);

vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => ({
    miniNavTabs: [],
    actionBarActions: mockActions(),
    activeZone: null,
    setNavConfig: vi.fn(),
    clearNavConfig: vi.fn(),
  }),
}));

import { FloatingActionBar } from '../FloatingActionBar';

// ─── Tests ────────────────────────────────────────────────────────────────────

describe('FloatingActionBar safe area', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders with bottom positioning that accounts for mobile tab bar', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    // Mobile: bottom-[calc(72px+1.5rem)] — accounts for MobileTabBar height
    // Desktop: md:bottom-6
    expect(bar.className).toContain('bottom-[calc(72px+1.5rem)]');
  });

  it('is fixed positioned and centered horizontally', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    expect(bar.className).toContain('fixed');
    expect(bar.className).toContain('left-1/2');
    expect(bar.className).toContain('-translate-x-1/2');
  });

  it('has z-50 for proper stacking above content', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    expect(bar.className).toContain('z-50');
  });

  it('uses desktop bottom-6 via md: breakpoint', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    expect(bar.className).toContain('md:bottom-6');
  });

  it('renders all actions regardless of safe area', () => {
    render(<FloatingActionBar />);
    expect(screen.getByLabelText('Salva')).toBeInTheDocument();
    expect(screen.getByLabelText('Impostazioni')).toBeInTheDocument();
  });

  it('does not render when no actions', () => {
    mockActions.mockReturnValue([]);
    render(<FloatingActionBar />);
    expect(screen.queryByRole('toolbar')).not.toBeInTheDocument();
  });
});
