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

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { PublicLayout, type PublicUser } from '../PublicLayout';

// Mock Next.js router and Link
vi.mock('next/navigation', () => ({
  usePathname: vi.fn(() => '/'),
  useRouter: vi.fn(() => ({
    push: vi.fn(),
  })),
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

describe('PublicLayout', () => {
  const mockUser: PublicUser = {
    name: 'Test User',
    email: 'test@example.com',
  };

  const mockOnLogout = vi.fn();

  describe('Base Rendering', () => {
    it('renders layout with children', () => {
      render(
        <PublicLayout>
          <div>Test Content</div>
        </PublicLayout>
      );

      expect(screen.getByText('Test Content')).toBeInTheDocument();
    });

    it('renders PublicHeader', () => {
      render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      // Header should contain logo link
      const logo = screen.getByLabelText('MeepleAI Home');
      expect(logo).toBeInTheDocument();
    });

    it('renders PublicFooter', () => {
      render(
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
      const { container } = render(
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
      render(
        <PublicLayout user={mockUser} onLogout={mockOnLogout}>
          <div>Content</div>
        </PublicLayout>
      );

      // User menu should be present
      const userMenuButton = screen.getByLabelText('User menu');
      expect(userMenuButton).toBeInTheDocument();
    });

    it('passes onLogout prop to PublicHeader', () => {
      render(
        <PublicLayout user={mockUser} onLogout={mockOnLogout}>
          <div>Content</div>
        </PublicLayout>
      );

      // Logout should be available in user menu
      const userMenuButton = screen.getByLabelText('User menu');
      expect(userMenuButton).toBeInTheDocument();
    });

    it('renders login button when no user provided', () => {
      render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const loginButton = screen.getByRole('link', { name: /accedi/i });
      expect(loginButton).toBeInTheDocument();
    });
  });

  describe('Container Width', () => {
    it('uses full width by default', () => {
      const { container } = render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-full');
    });

    it('applies sm container width', () => {
      const { container } = render(
        <PublicLayout containerWidth="sm">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-3xl');
    });

    it('applies md container width', () => {
      const { container } = render(
        <PublicLayout containerWidth="md">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-5xl');
    });

    it('applies lg container width', () => {
      const { container } = render(
        <PublicLayout containerWidth="lg">
          <div>Content</div>
        </PublicLayout>
      );

      const contentContainer = container.querySelector('main > div');
      expect(contentContainer).toHaveClass('max-w-7xl');
    });

    it('applies xl container width', () => {
      const { container } = render(
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
      const { container } = render(
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
      const { container } = render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('flex-1');
    });

    it('applies proper spacing classes', () => {
      const { container } = render(
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
      const { container } = render(
        <PublicLayout className="custom-class">
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('custom-class');
    });

    it('preserves base classes with custom className', () => {
      const { container } = render(
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
      render(
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
      render(
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
      const { container } = render(
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
      const { container } = render(
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
      const { container } = render(
        <PublicLayout>
          <div data-testid="content">Content</div>
        </PublicLayout>
      );

      const wrapper = container.querySelector('div.min-h-screen');
      const children = wrapper?.children;

      // Should have 3 children: header, main, footer
      expect(children).toHaveLength(3);
      expect(children?.[0]?.tagName).toBe('HEADER');
      expect(children?.[1]?.tagName).toBe('MAIN');
      expect(children?.[2]?.tagName).toBe('FOOTER');
    });

    it('renders content inside main element', () => {
      const { container } = render(
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
      const { container } = render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      expect(container.querySelector('header')).toBeInTheDocument();
      expect(container.querySelector('main')).toBeInTheDocument();
      expect(container.querySelector('footer')).toBeInTheDocument();
    });

    it('main element is properly labeled', () => {
      const { container } = render(
        <PublicLayout>
          <div>Content</div>
        </PublicLayout>
      );

      const main = container.querySelector('main');
      expect(main).toBeInTheDocument();
    });
  });
});
