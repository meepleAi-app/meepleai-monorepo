/**
 * FloatingActionBar Safe Area Tests
 * Mobile UX Epic — Issue 9
 *
 * Verifies that FloatingActionBar respects env(safe-area-inset-bottom)
 * for proper positioning on iOS devices with notch/home indicator.
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { Plus, Settings } from 'lucide-react';

// ─── Mocks ────────────────────────────────────────────────────────────────────

vi.mock('@/hooks/useResponsive', () => ({
  usePrefersReducedMotion: () => false,
  useIsMobile: () => false,
  useMediaQuery: () => false,
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

  it('renders with safe area bottom positioning', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    expect(bar.className).toContain('bottom-[calc(1.5rem+env(safe-area-inset-bottom))]');
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

  it('does not use static bottom-6 without safe area', () => {
    render(<FloatingActionBar />);
    const bar = screen.getByRole('toolbar');
    // Should NOT have the old static `bottom-6` class
    // Instead uses calc with safe-area-inset-bottom
    expect(bar.className).not.toMatch(/\bbottom-6\b/);
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
