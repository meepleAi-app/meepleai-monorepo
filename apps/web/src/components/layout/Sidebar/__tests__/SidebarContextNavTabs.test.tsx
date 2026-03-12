/**
 * SidebarContextNav — miniNavTabs rendering tests
 *
 * Tests the Zone 3 rendering that replaced MiniNav:
 * - Tab rendering from NavigationContext
 * - Active state detection (pathname + query params)
 * - Collapsed mode behavior (icon-only, hide icon-less tabs)
 * - ARIA roles (tablist, tab, aria-selected)
 */

import { render, screen } from '@testing-library/react';
import { BookOpen, Settings, Link as LinkIcon } from 'lucide-react';
import { usePathname, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { SidebarContextNav } from '../SidebarContextNav';

// ─── Mocks ───────────────────────────────────────────────────────────────────

vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

const mockMiniNavTabs: Array<{
  id: string;
  label: string;
  href: string;
  icon?: React.ElementType;
}> = [];

vi.mock('@/context/NavigationContext', () => ({
  NavigationProvider: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  useNavigation: () => ({
    miniNavTabs: mockMiniNavTabs,
    activeZone: null,
    contextKey: null,
    actionBarActions: [],
    breadcrumbs: [],
  }),
}));

vi.mock('@/components/catalog/GamesFilterPanel', () => ({
  GamesFilterPanel: () => <div data-testid="games-filter-panel" />,
}));

vi.mock('framer-motion', () => ({
  AnimatePresence: ({ children }: { children: React.ReactNode }) => <>{children}</>,
  motion: {
    div: ({ children, ...props }: { children: React.ReactNode; [key: string]: unknown }) => (
      <div
        data-testid={props['data-testid'] as string}
        role={props.role as string}
        aria-label={props['aria-label'] as string}
      >
        {children}
      </div>
    ),
  },
}));

const mockUsePathname = usePathname as ReturnType<typeof vi.fn>;
const mockUseSearchParams = useSearchParams as ReturnType<typeof vi.fn>;

// ─── Helpers ─────────────────────────────────────────────────────────────────

function setTabs(
  tabs: Array<{
    id: string;
    label: string;
    href: string;
    icon?: React.ElementType;
  }>
) {
  mockMiniNavTabs.length = 0;
  mockMiniNavTabs.push(...tabs);
}

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('SidebarContextNav — miniNavTabs (Zone 3)', () => {
  beforeEach(() => {
    mockMiniNavTabs.length = 0;
    mockUsePathname.mockReturnValue('/library/games/123');
    mockUseSearchParams.mockReturnValue(new URLSearchParams());
  });

  describe('Tab rendering', () => {
    it('renders nothing when no miniNavTabs are set', () => {
      render(<SidebarContextNav isCollapsed={false} />);
      expect(screen.queryByRole('tablist')).not.toBeInTheDocument();
    });

    it('renders tabs from NavigationContext', () => {
      setTabs([
        { id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen },
        {
          id: 'settings',
          label: 'Settings',
          href: '/library/games/123?tab=settings',
          icon: Settings,
        },
      ]);

      render(<SidebarContextNav isCollapsed={false} />);

      expect(screen.getByText('Overview')).toBeInTheDocument();
      expect(screen.getByText('Settings')).toBeInTheDocument();
    });
  });

  describe('ARIA roles', () => {
    it('renders tablist role on the container', () => {
      setTabs([{ id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen }]);

      render(<SidebarContextNav isCollapsed={false} />);

      expect(screen.getByRole('tablist')).toBeInTheDocument();
    });

    it('renders tab role with aria-selected on each tab', () => {
      setTabs([
        { id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen },
        { id: 'links', label: 'Links', href: '/library/games/123?tab=links', icon: LinkIcon },
      ]);

      render(<SidebarContextNav isCollapsed={false} />);

      const tabs = screen.getAllByRole('tab');
      expect(tabs).toHaveLength(2);
      // Overview is the default tab (no ?tab param), so it's active
      expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
      expect(tabs[1]).toHaveAttribute('aria-selected', 'false');
    });
  });

  describe('Active state detection', () => {
    it('marks default tab active when no query param is set', () => {
      setTabs([
        { id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen },
        { id: 'links', label: 'Links', href: '/library/games/123?tab=links', icon: LinkIcon },
      ]);

      render(<SidebarContextNav isCollapsed={false} />);

      const tabs = screen.getAllByRole('tab');
      expect(tabs[0]).toHaveAttribute('aria-selected', 'true');
      expect(tabs[1]).toHaveAttribute('aria-selected', 'false');
    });

    it('marks query-param tab active when matching search param is set', () => {
      mockUseSearchParams.mockReturnValue(new URLSearchParams('tab=links'));

      setTabs([
        { id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen },
        { id: 'links', label: 'Links', href: '/library/games/123?tab=links', icon: LinkIcon },
      ]);

      render(<SidebarContextNav isCollapsed={false} />);

      const tabs = screen.getAllByRole('tab');
      expect(tabs[0]).toHaveAttribute('aria-selected', 'false');
      expect(tabs[1]).toHaveAttribute('aria-selected', 'true');
    });
  });

  describe('Collapsed mode', () => {
    it('hides tab labels in collapsed mode', () => {
      setTabs([{ id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen }]);

      render(<SidebarContextNav isCollapsed={true} />);

      expect(screen.queryByText('Overview')).not.toBeInTheDocument();
    });

    it('hides tabs without icons in collapsed mode', () => {
      setTabs([
        { id: 'overview', label: 'Overview', href: '/library/games/123', icon: BookOpen },
        { id: 'no-icon', label: 'No Icon Tab', href: '/library/games/123?tab=noicon' },
      ]);

      render(<SidebarContextNav isCollapsed={true} />);

      const tabs = screen.getAllByRole('tab');
      // Only the tab with an icon should render
      expect(tabs).toHaveLength(1);
    });
  });
});
