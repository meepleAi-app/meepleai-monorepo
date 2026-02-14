/**
 * MobileNavDrawer Component Tests (Issue #4064)
 *
 * Tests for mobile navigation drawer with hamburger menu.
 */

import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { axe } from 'jest-axe';
import { LayoutDashboard, Gamepad2, Users, History, Calendar } from 'lucide-react';

import { MobileNavDrawer } from '../MobileNavDrawer';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
}));

const mockUsePathname = usePathname as Mock;

const MOCK_NAV_ITEMS = [
  {
    href: '/dashboard',
    icon: LayoutDashboard,
    label: 'Dashboard',
    ariaLabel: 'Navigate to dashboard',
  },
  {
    href: '/games',
    icon: Gamepad2,
    label: 'Catalogo',
    ariaLabel: 'Navigate to games catalog',
  },
  {
    href: '/agents',
    icon: Users,
    label: 'Agenti',
    ariaLabel: 'Navigate to agents list',
  },
  {
    href: '/chat',
    icon: History,
    label: 'Chat History',
    ariaLabel: 'Navigate to chat history',
  },
  {
    href: '/sessions',
    icon: Calendar,
    label: 'Sessioni',
    ariaLabel: 'Navigate to play sessions',
  },
];

const MOCK_LIBRARY_ITEMS = [
  {
    href: '/library',
    label: 'Collezione',
    ariaLabel: 'Navigate to your game collection',
  },
  {
    href: '/library/private',
    label: 'Giochi Privati',
    ariaLabel: 'Navigate to your private games',
  },
];

describe('MobileNavDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePathname.mockReturnValue('/dashboard');
  });

  describe('Rendering', () => {
    it('renders hamburger button', () => {
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );
      expect(screen.getByTestId('mobile-nav-trigger')).toBeInTheDocument();
    });

    it('hamburger button has Menu icon', () => {
      const { container } = render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );
      const button = screen.getByTestId('mobile-nav-trigger');
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('drawer is closed initially', () => {
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );
      expect(screen.queryByTestId('mobile-nav-drawer')).not.toBeInTheDocument();
    });
  });

  describe('Drawer Interaction', () => {
    it('opens drawer on hamburger click', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      expect(screen.getByTestId('mobile-nav-drawer')).toBeInTheDocument();
      expect(screen.getByText('Navigazione')).toBeInTheDocument();
    });

    it('shows all navigation items when open', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      expect(screen.getByTestId('mobile-nav-item-dashboard')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-nav-item-games')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-nav-item-agents')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-nav-item-chat')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-nav-item-sessions')).toBeInTheDocument();
    });

    it('closes drawer on Escape key', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      expect(screen.getByTestId('mobile-nav-drawer')).toBeInTheDocument();

      await user.keyboard('{Escape}');

      // Drawer should close
      expect(screen.queryByTestId('mobile-nav-drawer')).not.toBeInTheDocument();
    });

    it('closes drawer on Chiudi button click', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      await user.click(screen.getByTestId('mobile-nav-close'));

      expect(screen.queryByTestId('mobile-nav-drawer')).not.toBeInTheDocument();
    });
  });

  describe('Library Expandable Section', () => {
    it('shows Library toggle button', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      expect(screen.getByTestId('mobile-library-toggle')).toBeInTheDocument();
    });

    it('expands library section on toggle click', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      await user.click(screen.getByTestId('mobile-library-toggle'));

      expect(screen.getByTestId('mobile-library-item-library')).toBeInTheDocument();
      expect(screen.getByTestId('mobile-library-item-private')).toBeInTheDocument();
    });

    it('collapses library section on second toggle click', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      await user.click(screen.getByTestId('mobile-library-toggle'));
      expect(screen.getByTestId('mobile-library-item-library')).toBeInTheDocument();

      await user.click(screen.getByTestId('mobile-library-toggle'));
      expect(screen.queryByTestId('mobile-library-item-library')).not.toBeInTheDocument();
    });

    it('library section expanded by default when library route active', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={true}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      // Should be expanded without clicking toggle
      expect(screen.getByTestId('mobile-library-item-library')).toBeInTheDocument();
    });

    it('rotates chevron icon when expanded', async () => {
      const user = userEvent.setup();
      const { container } = render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      await user.click(screen.getByTestId('mobile-library-toggle'));

      const toggle = screen.getByTestId('mobile-library-toggle');
      const chevronContainer = toggle.querySelector('.rotate-180');
      expect(chevronContainer).toBeInTheDocument();
    });
  });

  describe('Active State Highlighting', () => {
    it('highlights active navigation item', async () => {
      mockUsePathname.mockReturnValue('/agents');
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const agentsLink = screen.getByTestId('mobile-nav-item-agents');
      expect(agentsLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(agentsLink).toHaveAttribute('aria-current', 'page');
    });

    it('highlights Library toggle when library route active', async () => {
      mockUsePathname.mockReturnValue('/library');
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={true}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const libraryToggle = screen.getByTestId('mobile-library-toggle');
      expect(libraryToggle).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('highlights active library sub-item', async () => {
      mockUsePathname.mockReturnValue('/library/private');
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={true}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const privateLink = screen.getByTestId('mobile-library-item-private');
      expect(privateLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(privateLink).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('Accessibility', () => {
    it('has no axe violations when drawer open', async () => {
      const user = userEvent.setup();
      const { container } = render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has navigation landmark', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      expect(screen.getByRole('navigation', { name: /mobile navigation/i })).toBeInTheDocument();
    });

    it('library submenu has group role', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));
      await user.click(screen.getByTestId('mobile-library-toggle'));

      expect(screen.getByRole('group', { name: /library submenu/i })).toBeInTheDocument();
    });

    it('library toggle has aria-expanded', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const toggle = screen.getByTestId('mobile-library-toggle');
      expect(toggle).toHaveAttribute('aria-expanded', 'false');

      await user.click(toggle);
      expect(toggle).toHaveAttribute('aria-expanded', 'true');
    });

    it('library toggle has aria-controls', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      expect(screen.getByTestId('mobile-library-toggle')).toHaveAttribute(
        'aria-controls',
        'library-submenu'
      );
    });
  });

  describe('Responsive Behavior', () => {
    it('has md:hidden class on trigger', () => {
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );
      const trigger = screen.getByTestId('mobile-nav-trigger');
      expect(trigger).toHaveClass('md:hidden');
    });

    it('drawer has correct width classes', async () => {
      const user = userEvent.setup();
      render(
        <MobileNavDrawer
          navItems={MOCK_NAV_ITEMS}
          libraryItems={MOCK_LIBRARY_ITEMS}
          isLibraryActive={false}
        />
      );

      await user.click(screen.getByTestId('mobile-nav-trigger'));

      const drawer = screen.getByTestId('mobile-nav-drawer');
      expect(drawer).toHaveClass('w-[280px]', 'sm:w-[320px]');
    });
  });
});
