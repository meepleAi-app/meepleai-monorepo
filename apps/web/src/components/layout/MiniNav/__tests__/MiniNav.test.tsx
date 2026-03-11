/**
 * MiniNav Tests
 * Issue #5037 — MiniNav Component
 *
 * Tests: MiniNav (context-driven tablist), MiniNavTab (individual tab)
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import {
  Home,
  Settings,
  Users,
  BookOpen,
  MessageSquare,
  Gamepad2,
  BarChart3,
  Lightbulb,
  Globe,
} from 'lucide-react';

import { MiniNav } from '../MiniNav';
import { MiniNavTab } from '../MiniNavTab';
import type { NavTab } from '@/types/navigation';
import { NAV_TEST_IDS } from '@/lib/test-ids';

// ─── Mocks ────────────────────────────────────────────────────────────────────

const mockPathname = vi.fn(() => '/library');

vi.mock('next/navigation', () => ({
  usePathname: () => mockPathname(),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

const mockMiniNavTabs = vi.fn<[], NavTab[]>(() => []);
const mockScrollDirection = vi.fn<[], 'up' | 'down' | null>(() => null);

vi.mock('@/hooks/useScrollState', () => ({
  useScrollState: () => ({
    scrollY: 0,
    direction: mockScrollDirection(),
    isScrolled: false,
    isScrolling: false,
  }),
}));

vi.mock('@/context/NavigationContext', () => ({
  useNavigation: () => ({
    miniNavTabs: mockMiniNavTabs(),
    actionBarActions: [],
    activeZone: null,
    setNavConfig: vi.fn(),
    clearNavConfig: vi.fn(),
  }),
}));

// ─── Fixtures ─────────────────────────────────────────────────────────────────

const libraryTabs: NavTab[] = [
  { id: 'games', label: 'Games', href: '/library', icon: BookOpen },
  { id: 'wishlist', label: 'Wishlist', href: '/library/wishlist', icon: Lightbulb },
  { id: 'private', label: 'Private', href: '/library/private', icon: Settings },
  { id: 'history', label: 'History', href: '/library/history', icon: BarChart3 },
];

const manyTabs: NavTab[] = [
  { id: 't1', label: 'Tab1', href: '/a' },
  { id: 't2', label: 'Tab2', href: '/b' },
  { id: 't3', label: 'Tab3', href: '/c' },
  { id: 't4', label: 'Tab4', href: '/d' },
  { id: 't5', label: 'Tab5', href: '/e' },
  { id: 't6', label: 'Tab6', href: '/f' },
  { id: 't7', label: 'Tab7', href: '/g' },
  { id: 't8', label: 'Tab8', href: '/h' },
  { id: 't9', label: 'Tab9', href: '/i' }, // > 8 → arrows
];

// ─── MiniNav Tests ────────────────────────────────────────────────────────────

describe('MiniNav', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockPathname.mockReturnValue('/library');
    mockMiniNavTabs.mockReturnValue([]);
    mockScrollDirection.mockReturnValue(null);
  });

  // ── Visibility ──────────────────────────────────────────────────────────────

  it('renders nothing when there are no tabs', () => {
    mockMiniNavTabs.mockReturnValue([]);
    const { container } = render(<MiniNav />);
    expect(container.firstChild).toBeNull();
  });

  it('renders when tabs are provided', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    expect(screen.getByTestId(NAV_TEST_IDS.miniNav)).toBeInTheDocument();
  });

  // ── ARIA ────────────────────────────────────────────────────────────────────

  it('renders tablist with aria-label', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const tablist = screen.getByRole('tablist');
    expect(tablist).toBeInTheDocument();
    expect(tablist).toHaveAttribute('aria-label', 'Section navigation');
  });

  it('renders each tab with role="tab"', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const tabs = screen.getAllByRole('tab');
    expect(tabs).toHaveLength(libraryTabs.length);
  });

  // ── Active state ────────────────────────────────────────────────────────────

  it('marks the tab matching current pathname as aria-selected=true', () => {
    mockPathname.mockReturnValue('/library');
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const gamesTab = screen.getByRole('tab', { name: /games/i });
    expect(gamesTab).toHaveAttribute('aria-selected', 'true');
  });

  it('marks non-active tabs as aria-selected=false', () => {
    mockPathname.mockReturnValue('/library');
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const wishlistTab = screen.getByRole('tab', { name: /wishlist/i });
    expect(wishlistTab).toHaveAttribute('aria-selected', 'false');
  });

  it('activates tab by prefix match for nested routes', () => {
    mockPathname.mockReturnValue('/library/wishlist/some-game');
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const wishlistTab = screen.getByRole('tab', { name: /wishlist/i });
    expect(wishlistTab).toHaveAttribute('aria-selected', 'true');
  });

  // ── Tab content ─────────────────────────────────────────────────────────────

  it('renders tab labels', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    expect(screen.getByText('Games')).toBeInTheDocument();
    expect(screen.getByText('Wishlist')).toBeInTheDocument();
    expect(screen.getByText('Private')).toBeInTheDocument();
    expect(screen.getByText('History')).toBeInTheDocument();
  });

  it('renders correct href for each tab', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const gamesTab = screen.getByRole('tab', { name: /games/i });
    expect(gamesTab).toHaveAttribute('href', '/library');
  });

  // ── Badge ───────────────────────────────────────────────────────────────────

  it('renders badge when tab has a badge value', () => {
    const tabWithBadge: NavTab[] = [
      { id: 'pending', label: 'Pending', href: '/admin/content?tab=approval', badge: 3 },
    ];
    mockMiniNavTabs.mockReturnValue(tabWithBadge);
    render(<MiniNav />);
    expect(screen.getByText('3')).toBeInTheDocument();
  });

  it('renders string badge', () => {
    const tabWithBadge: NavTab[] = [{ id: 'new', label: 'New', href: '/new', badge: 'NEW' }];
    mockMiniNavTabs.mockReturnValue(tabWithBadge);
    render(<MiniNav />);
    expect(screen.getByText('NEW')).toBeInTheDocument();
  });

  it('does not render badge element when badge is undefined', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    // No badge spans — libraryTabs have no badge
    expect(screen.queryByLabelText(/items/)).not.toBeInTheDocument();
  });

  // ── Scroll arrows (tabs > 8) ────────────────────────────────────────────────

  it('does NOT render scroll arrows when tabs ≤ 8', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs); // 4 tabs
    render(<MiniNav />);
    expect(screen.queryByTestId(NAV_TEST_IDS.miniNavScrollLeft)).not.toBeInTheDocument();
    expect(screen.queryByTestId(NAV_TEST_IDS.miniNavScrollRight)).not.toBeInTheDocument();
  });

  it('renders scroll arrows when tabs > 8', () => {
    mockMiniNavTabs.mockReturnValue(manyTabs); // 9 tabs
    render(<MiniNav />);
    expect(screen.getByTestId(NAV_TEST_IDS.miniNavScrollLeft)).toBeInTheDocument();
    expect(screen.getByTestId(NAV_TEST_IDS.miniNavScrollRight)).toBeInTheDocument();
  });

  // ── Custom className ────────────────────────────────────────────────────────

  it('applies custom className to wrapper', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav className="test-custom" />);
    expect(screen.getByTestId('mini-nav')).toHaveClass('test-custom');
  });

  // ── Scroll-hide behavior ──────────────────────────────────────────────────

  it('hides with opacity-0 and pointer-events-none when scrollDirection is down', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    mockScrollDirection.mockReturnValue('down');
    render(<MiniNav />);
    const nav = screen.getByTestId(NAV_TEST_IDS.miniNav);
    expect(nav).toHaveClass('opacity-0');
    expect(nav).toHaveClass('pointer-events-none');
    expect(nav).toHaveClass('-translate-y-full');
  });

  it('does not hide when scrollDirection is up', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    mockScrollDirection.mockReturnValue('up');
    render(<MiniNav />);
    const nav = screen.getByTestId(NAV_TEST_IDS.miniNav);
    expect(nav).not.toHaveClass('opacity-0');
    expect(nav).not.toHaveClass('pointer-events-none');
  });

  it('does not hide when scrollDirection is null (initial state)', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    mockScrollDirection.mockReturnValue(null);
    render(<MiniNav />);
    const nav = screen.getByTestId(NAV_TEST_IDS.miniNav);
    expect(nav).not.toHaveClass('opacity-0');
    expect(nav).not.toHaveClass('pointer-events-none');
  });

  it('has transition classes for smooth animation', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const nav = screen.getByTestId(NAV_TEST_IDS.miniNav);
    expect(nav).toHaveClass('transition-all');
    expect(nav).toHaveClass('duration-200');
  });

  it('respects prefers-reduced-motion with motion-reduce class', () => {
    mockMiniNavTabs.mockReturnValue(libraryTabs);
    render(<MiniNav />);
    const nav = screen.getByTestId(NAV_TEST_IDS.miniNav);
    expect(nav.className).toContain('motion-reduce:transition-none');
  });
});

// ─── MiniNavTab Tests ─────────────────────────────────────────────────────────

describe('MiniNavTab', () => {
  const tab: NavTab = { id: 'games', label: 'Games', href: '/library', icon: Home };

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders as a link with role="tab"', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    const el = screen.getByRole('tab', { name: /games/i });
    expect(el.tagName).toBe('A');
  });

  it('has correct href', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    expect(screen.getByRole('tab')).toHaveAttribute('href', '/library');
  });

  it('has aria-selected=true when active', () => {
    render(<MiniNavTab tab={tab} isActive={true} />);
    expect(screen.getByRole('tab')).toHaveAttribute('aria-selected', 'true');
  });

  it('has aria-selected=false when not active', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    expect(screen.getByRole('tab')).toHaveAttribute('aria-selected', 'false');
  });

  it('renders tab label', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    expect(screen.getByText('Games')).toBeInTheDocument();
  });

  it('renders icon when provided', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    // Icon renders as svg inside the link
    const svg = screen.getByRole('tab').querySelector('svg');
    expect(svg).not.toBeNull();
  });

  it('does not crash when no icon', () => {
    const noIconTab: NavTab = { id: 'x', label: 'X', href: '/x' };
    render(<MiniNavTab tab={noIconTab} isActive={false} />);
    expect(screen.getByRole('tab')).toBeInTheDocument();
  });

  it('renders numeric badge', () => {
    const badgeTab: NavTab = { id: 'b', label: 'Beta', href: '/b', badge: 5 };
    render(<MiniNavTab tab={badgeTab} isActive={false} />);
    expect(screen.getByText('5')).toBeInTheDocument();
  });

  it('renders string badge', () => {
    const badgeTab: NavTab = { id: 'n', label: 'New', href: '/n', badge: 'HOT' };
    render(<MiniNavTab tab={badgeTab} isActive={false} />);
    expect(screen.getByText('HOT')).toBeInTheDocument();
  });

  it('calls onClick when clicked', () => {
    const onClick = vi.fn();
    render(<MiniNavTab tab={tab} isActive={false} onClick={onClick} />);
    fireEvent.click(screen.getByRole('tab'));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('has data-testid with tab id', () => {
    render(<MiniNavTab tab={tab} isActive={false} />);
    expect(screen.getByTestId(NAV_TEST_IDS.miniNavTab('games'))).toBeInTheDocument();
  });
});
