/**
 * MobileNavDrawer Component Tests (Issue #4064)
 *
 * Tests for mobile navigation drawer with hamburger menu.
 * Updated for unified navigation system (Issue #4369):
 * - MobileNavDrawer now uses useNavigationItems hook internally
 * - No more props-based nav items; mock useCurrentUser for auth state
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { usePathname, useSearchParams } from 'next/navigation';
import { describe, it, expect, vi, beforeEach, Mock } from 'vitest';
import { axe } from 'jest-axe';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

import { MobileNavDrawer } from '../MobileNavDrawer';
import { NAV_TEST_IDS } from '@/lib/test-ids';

// Mock Next.js navigation
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

// Mock useCurrentUser hook
vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: vi.fn(),
}));

const mockUsePathname = usePathname as Mock;
const mockUseSearchParams = useSearchParams as Mock;
const mockUseCurrentUser = useCurrentUser as Mock;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>
        {children}
      </QueryClientProvider>
    );
  };
}

function renderDrawer() {
  return render(<MobileNavDrawer />, { wrapper: createWrapper() });
}

describe('MobileNavDrawer', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUsePathname.mockReturnValue('/dashboard');
    mockUseSearchParams.mockReturnValue(new URLSearchParams());
    // Default to authenticated user
    mockUseCurrentUser.mockReturnValue({
      data: { id: '1', email: 'test@test.com', role: 'User', displayName: 'Test' },
      isLoading: false,
    });
  });

  describe('Rendering', () => {
    it('renders hamburger button', () => {
      renderDrawer();
      expect(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger)).toBeInTheDocument();
    });

    it('hamburger button has Menu icon', () => {
      renderDrawer();
      const button = screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger);
      const svg = button.querySelector('svg');
      expect(svg).toBeInTheDocument();
    });

    it('drawer is closed initially', () => {
      renderDrawer();
      expect(screen.queryByTestId(NAV_TEST_IDS.mobileNavDrawer)).not.toBeInTheDocument();
    });
  });

  describe('Drawer Interaction', () => {
    it('opens drawer on hamburger click', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      expect(screen.getByTestId(NAV_TEST_IDS.mobileNavDrawer)).toBeInTheDocument();
      expect(screen.getByText('Navigazione')).toBeInTheDocument();
    });

    it('shows navigation items when open', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      // Should show at least Dashboard, Scopri, Chat for authenticated user
      expect(screen.getByText('Dashboard')).toBeInTheDocument();
      expect(screen.getByText('Scopri')).toBeInTheDocument();
      expect(screen.getByText('Chat')).toBeInTheDocument();
    });

    it('closes drawer on Escape key', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      expect(screen.getByTestId(NAV_TEST_IDS.mobileNavDrawer)).toBeInTheDocument();

      await user.keyboard('{Escape}');

      expect(screen.queryByTestId(NAV_TEST_IDS.mobileNavDrawer)).not.toBeInTheDocument();
    });

    it('closes drawer on Chiudi button click', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavClose));

      expect(screen.queryByTestId(NAV_TEST_IDS.mobileNavDrawer)).not.toBeInTheDocument();
    });
  });

  describe('Library Expandable Section', () => {
    it('shows Library toggle button', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      expect(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle)).toBeInTheDocument();
    });

    it('expands library section on toggle click', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle));

      expect(screen.getByTestId(NAV_TEST_IDS.mobileLibraryItem('collection'))).toBeInTheDocument();
    });

    it('collapses library section on second toggle click', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle));
      expect(screen.getByTestId(NAV_TEST_IDS.mobileLibraryItem('collection'))).toBeInTheDocument();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle));
      expect(screen.queryByTestId(NAV_TEST_IDS.mobileLibraryItem('collection'))).not.toBeInTheDocument();
    });

    it('library section expanded by default when library route active', async () => {
      mockUsePathname.mockReturnValue('/library');
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      // Should be expanded without clicking toggle
      expect(screen.getByTestId(NAV_TEST_IDS.mobileLibraryItem('collection'))).toBeInTheDocument();
    });

    it('rotates chevron icon when expanded', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle));

      const toggle = screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle);
      const chevronContainer = toggle.querySelector('.rotate-180');
      expect(chevronContainer).toBeInTheDocument();
    });
  });

  describe('Active State Highlighting', () => {
    it('highlights active navigation item', async () => {
      mockUsePathname.mockReturnValue('/agents');
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      const agentsLink = screen.getByTestId(NAV_TEST_IDS.mobileNavItem('agents'));
      expect(agentsLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(agentsLink).toHaveAttribute('aria-current', 'page');
    });

    it('highlights Library toggle when library route active', async () => {
      mockUsePathname.mockReturnValue('/library');
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      const libraryToggle = screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle);
      expect(libraryToggle).toHaveClass('text-[hsl(262_83%_62%)]');
    });

    it('highlights active library sub-item', async () => {
      mockUsePathname.mockReturnValue('/library');
      mockUseSearchParams.mockReturnValue(new URLSearchParams('tab=collection'));
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      // Library should auto-expand since /library is active

      const collectionLink = screen.getByTestId(NAV_TEST_IDS.mobileLibraryItem('collection'));
      expect(collectionLink).toHaveClass('text-[hsl(262_83%_62%)]');
      expect(collectionLink).toHaveAttribute('aria-current', 'page');
    });
  });

  describe('Accessibility', () => {
    it('has no axe violations when drawer open', async () => {
      const user = userEvent.setup();
      const { container } = render(<MobileNavDrawer />, { wrapper: createWrapper() });

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });

    it('has navigation landmark', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      expect(screen.getByRole('navigation', { name: /mobile navigation/i })).toBeInTheDocument();
    });

    it('library submenu has group role', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));
      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle));

      expect(screen.getByRole('group', { name: /library submenu/i })).toBeInTheDocument();
    });

    it('library toggle has aria-expanded', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      const toggle = screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle);
      expect(toggle).toHaveAttribute('aria-expanded', 'false');

      await user.click(toggle);
      expect(toggle).toHaveAttribute('aria-expanded', 'true');
    });

    it('library toggle has aria-controls', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      expect(screen.getByTestId(NAV_TEST_IDS.mobileLibraryToggle)).toHaveAttribute(
        'aria-controls',
        'library-submenu'
      );
    });
  });

  describe('Strumenti Group', () => {
    it('renders Strumenti group label and items', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      expect(screen.getByText('Strumenti')).toBeInTheDocument();
      expect(screen.getByText('Agenti')).toBeInTheDocument();
      expect(screen.getByText('Sessioni')).toBeInTheDocument();
    });

    it('renders a separator before Strumenti group', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      // The separator is inside the Sheet portal — use screen query
      const drawer = screen.getByTestId(NAV_TEST_IDS.mobileNavDrawer);
      const hr = drawer.querySelector('hr');
      expect(hr).toBeInTheDocument();
    });

    it('does not render Profile in main nav (hidden by hideFromMainNav)', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      // Profile should NOT appear in the navigation
      expect(screen.queryByText('Profilo')).not.toBeInTheDocument();
    });
  });

  describe('Responsive Behavior', () => {
    it('has md:hidden class on trigger', () => {
      renderDrawer();
      const trigger = screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger);
      expect(trigger).toHaveClass('md:hidden');
    });

    it('drawer has correct width classes', async () => {
      const user = userEvent.setup();
      renderDrawer();

      await user.click(screen.getByTestId(NAV_TEST_IDS.mobileNavTrigger));

      const drawer = screen.getByTestId(NAV_TEST_IDS.mobileNavDrawer);
      expect(drawer).toHaveClass('w-[280px]', 'sm:w-[320px]');
    });
  });
});
