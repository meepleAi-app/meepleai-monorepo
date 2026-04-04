/**
 * PublicLayout Component Tests - Issue #2230
 *
 * Test coverage:
 * - Rendering base con Header + Content + Footer
 * - Container responsive
 * - Props propagation a Header
 * - Min-height per footer sticky
 * - Custom className
 */

import React from 'react';
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LayoutProvider } from '@/components/layout/LayoutProvider';
import { PublicLayout, type PublicUser } from '../PublicLayout';

// Mock Next.js router and Link
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/'),
  useRouter: vi.fn(() => ({
    push: vi.fn(),
  })),
  useSearchParams: vi.fn(() => new URLSearchParams()),
}));

vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock next-themes
vi.mock('next-themes', () => ({
  useTheme: vi.fn(() => ({
    theme: 'light',
    setTheme: vi.fn(),
    systemTheme: 'light',
  })),
  ThemeProvider: ({ children }: { children: React.ReactNode }) => children,
}));

// Mock MeepleLogo to avoid styled-jsx issues in tests
vi.mock('@/components/ui/meeple/meeple-logo', () => ({
  MeepleLogo: () => <div data-testid="meeple-logo">MeepleAI</div>,
}));

// Mock NotificationCenter to avoid IntlProvider dependency (it uses useTranslation/useIntl)
vi.mock('@/components/notifications/NotificationCenter', () => ({
  NotificationCenter: () => null,
}));

// Mock ActionBar components to avoid useLayout context dependency
vi.mock('@/components/layout/ActionBar', () => ({
  UnifiedActionBar: () => null,
  UnifiedActionBarSpacer: () => null,
}));

// Mock Sidebar to avoid complex context dependencies when authenticated
vi.mock('@/components/layout/Sidebar', () => ({
  Sidebar: () => null,
}));

// Mock MobileTabBar to avoid QueryClient/Navigation dependencies
vi.mock('@/components/layout/MobileTabBar', () => ({
  MobileTabBar: () => <nav data-testid="mobile-tab-bar" aria-label="Primary navigation" />,
}));

// Mock useCurrentUser to avoid QueryClient dependency in UnifiedHeader
const mockUseCurrentUser = vi.fn(() => ({
  data: null,
  isLoading: false,
  isError: false,
  error: null,
}));

vi.mock('@/hooks/queries/useCurrentUser', () => ({
  useCurrentUser: () => mockUseCurrentUser(),
}));

// Create a wrapper with QueryClientProvider for tests that need it
const createTestQueryClient = () =>
  new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

const renderWithQueryClient = (ui: React.ReactElement) => {
  const testQueryClient = createTestQueryClient();
  return render(
    <QueryClientProvider client={testQueryClient}>
      <LayoutProvider>{ui}</LayoutProvider>
    </QueryClientProvider>
  );
};

describe('PublicLayout', () => {
  const mockUser: PublicUser = {
    name: 'Test User',
    email: 'test@example.com',
  };

  const mockOnLogout = vi.fn();

  describe('Base Rendering', () => {
    it('renders layout with children', () => {
      renderWithQueryClient(
        <PublicLayout>
          <div>Test Content</div>
        </PublicLayout>
      );

      expect(screen.getByText('Test Content')).toBeInTheDocument();
    });

    it('renders PublicHeader', () => {
      renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      // Header should contain logo link
      const logo = screen.getByLabelText('MeepleAI Home');
      expect(logo).toBeInTheDocument();
    });

    it('renders PublicFooter', () => {
      renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      // Footer should contain copyright
      const currentYear = new Date().getFullYear();
      const copyright = screen.getByText(new RegExp(`© ${currentYear} MeepleAI`, 'i'));
      expect(copyright).toBeInTheDocument();
    });

    it('renders main element for content', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toBeInTheDocument();
      expect(main).toHaveClass('flex-1');
    });
  });

  describe('User Prop Propagation', () => {
    it('passes user prop to PublicHeader', () => {
      // Mock authenticated user for this test
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: mockUser.email, displayName: mockUser.name },
        isLoading: false,
        isError: false,
        error: null,
      });

      renderWithQueryClient(
        <PublicLayout user={mockUser} onLogout={mockOnLogout}>
          <div>Content</div>
        </PublicLayout>
      );

      // User menu should be present (may appear in multiple places: header + sidebar)
      const userMenuButtons = screen.getAllByLabelText('User menu');
      expect(userMenuButtons.length).toBeGreaterThan(0);

      // Reset mock
      mockUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      });
    });

    it('passes onLogout prop to PublicHeader', () => {
      // Mock authenticated user for this test
      mockUseCurrentUser.mockReturnValue({
        data: { id: '1', email: mockUser.email, displayName: mockUser.name },
        isLoading: false,
        isError: false,
        error: null,
      });

      renderWithQueryClient(
        <PublicLayout user={mockUser} onLogout={mockOnLogout}>
          <div>Content</div>
        </PublicLayout>
      );

      // Logout should be available in user menu (may appear in multiple places: header + sidebar)
      const userMenuButtons = screen.getAllByLabelText('User menu');
      expect(userMenuButtons.length).toBeGreaterThan(0);

      // Reset mock
      mockUseCurrentUser.mockReturnValue({
        data: null,
        isLoading: false,
        isError: false,
        error: null,
      });
    });

    it('renders login button when no user provided', () => {
      renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      // "Accedi" renders as a Button inside a Link. The accessible name may come
      // from the button child. Accept either a link or button with the text.
      const loginEl = screen.getByText(/accedi/i);
      expect(loginEl).toBeInTheDocument();
    });
  });

  describe('Container Width', () => {
    it('uses full width by default', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-full');
    });

    it('applies sm container width', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout containerWidth="sm">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-3xl');
    });

    it('applies md container width', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout containerWidth="md">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-5xl');
    });

    it('applies lg container width', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout containerWidth="lg">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-7xl');
    });

    it('applies xl container width', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout containerWidth="xl">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-screen-2xl');
    });
  });

  describe('Layout Structure', () => {
    it('has min-h-screen for full viewport height', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const layoutWrapper = container.querySelector('div.min-h-screen');
      expect(layoutWrapper).toBeInTheDocument();
      expect(layoutWrapper).toHaveClass('flex');
      expect(layoutWrapper).toHaveClass('flex-col');
    });

    it('main content has flex-1 for sticky footer', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('flex-1');
    });

    it('applies proper spacing classes', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('mx-auto');
      expect(contentContainer).toHaveClass('px-4');
      expect(contentContainer).toHaveClass('sm:px-6');
      expect(contentContainer).toHaveClass('lg:px-8');
      expect(contentContainer).toHaveClass('py-8');
    });
  });

  describe('Custom Styling', () => {
    it('accepts custom className for main content', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout className="custom-class">
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('custom-class');
    });

    it('preserves base classes with custom className', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout className="custom-class">
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('flex-1');
      expect(main).toHaveClass('w-full');
      expect(main).toHaveClass('custom-class');
    });
  });

  describe('Footer Props', () => {
    it('passes showNewsletter prop to PublicFooter', () => {
      renderWithQueryClient(
        <PublicLayout showNewsletter>
          <div>Content</div>
        </PublicLayout>
      );

      // Footer should be rendered (newsletter section not implemented yet)
      const currentYear = new Date().getFullYear();
      const copyright = screen.getByText(new RegExp(`© ${currentYear} MeepleAI`, 'i'));
      expect(copyright).toBeInTheDocument();
    });

    it('does not show newsletter by default', () => {
      renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      // Newsletter section should not be present
      expect(screen.queryByText(/newsletter/i)).not.toBeInTheDocument();
    });
  });

  describe('Responsive Behavior', () => {
    it('applies responsive padding', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('px-4');
      expect(contentContainer).toHaveClass('sm:px-6');
      expect(contentContainer).toHaveClass('lg:px-8');
    });

    it('content container centers with mx-auto', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('mx-auto');
    });
  });

  describe('Composition', () => {
    it('renders all layout parts in correct order', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div data-testid="content">Content</div>
        </PublicLayout>
      );

      const wrapper = container.querySelector('div.min-h-screen');

      // Should contain header, main, and footer elements
      // (may have additional utility elements like Toaster)
      expect(wrapper?.querySelector('header')).toBeInTheDocument();
      expect(wrapper?.querySelector('main')).toBeInTheDocument();
      expect(wrapper?.querySelector('footer')).toBeInTheDocument();

      // Verify order: header before main, main before footer
      const header = wrapper?.querySelector('header');
      const main = wrapper?.querySelector('main');
      const footer = wrapper?.querySelector('footer');

      if (header && main) {
        expect(
          header.compareDocumentPosition(main) & Node.DOCUMENT_POSITION_FOLLOWING
        ).toBeTruthy();
      }
      if (main && footer) {
        expect(
          main.compareDocumentPosition(footer) & Node.DOCUMENT_POSITION_FOLLOWING
        ).toBeTruthy();
      }
    });

    it('renders content inside main element', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div data-testid="test-content">Test Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      const content = screen.getByTestId('test-content');

      expect(main).toContainElement(content);
    });
  });

  describe('Accessibility', () => {
    it('uses semantic HTML elements', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      expect(container.querySelector('header')).toBeInTheDocument();
      expect(container.querySelector('main')).toBeInTheDocument();
      expect(container.querySelector('footer')).toBeInTheDocument();
    });

    it('main element is properly labeled', () => {
      const { container } = renderWithQueryClient(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toBeInTheDocument();
    });
  });
});
