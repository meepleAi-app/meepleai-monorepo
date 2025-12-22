/**
 * AuthLayout Component Tests - Issue #2231
 *
 * Test coverage:
 * - Rendering base con Header + Content + Footer minimal
 * - Centered card container
 * - Optional title/subtitle
 * - Back link visibility
 * - Custom className
 * - Security notice
 * - Responsive behavior
 * - Accessibility
 */

import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuthLayout, type AuthLayoutProps } from '../AuthLayout';

// Mock Next.js Link
vi.mock('next/link', () => ({
  default: ({ children, href, ...props }: any) => (
    <a href={href} {...props}>
      {children}
    </a>
  ),
}));

// Mock MeepleLogo to avoid styled-jsx issues in tests
vi.mock('@/components/ui/meeple/meeple-logo', () => ({
  MeepleLogo: () => <div data-testid="meeple-logo">MeepleAI</div>,
}));

describe('AuthLayout', () => {
  const defaultProps: Partial<AuthLayoutProps> = {};

  describe('Base Rendering', () => {
    it('renders layout with children', () => {
      render(
        <AuthLayout>
          <div>Auth Form Content</div>
        </AuthLayout>
      );

      expect(screen.getByText('Auth Form Content')).toBeInTheDocument();
    });

    it('renders MeepleLogo in header', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const logo = screen.getByTestId('meeple-logo');
      expect(logo).toBeInTheDocument();
    });

    it('renders minimal footer with links', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const privacyLink = screen.getByRole('link', { name: /privacy policy/i });
      const termsLink = screen.getByRole('link', { name: /terms of service/i });

      expect(privacyLink).toBeInTheDocument();
      expect(termsLink).toBeInTheDocument();
      expect(privacyLink).toHaveAttribute('href', '/privacy');
      expect(termsLink).toHaveAttribute('href', '/terms');
    });

    it('renders copyright with current year', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const currentYear = new Date().getFullYear();
      const copyright = screen.getByText(new RegExp(`© ${currentYear} MeepleAI`, 'i'));
      expect(copyright).toBeInTheDocument();
    });

    it('renders security notice', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const securityNotice = screen.getByText(/secured with industry-standard encryption/i);
      expect(securityNotice).toBeInTheDocument();
    });
  });

  describe('Title and Subtitle', () => {
    it('renders title when provided', () => {
      render(
        <AuthLayout title="Welcome Back">
          <div>Content</div>
        </AuthLayout>
      );

      const title = screen.getByRole('heading', { name: 'Welcome Back', level: 1 });
      expect(title).toBeInTheDocument();
    });

    it('renders subtitle when provided', () => {
      render(
        <AuthLayout subtitle="Sign in to continue">
          <div>Content</div>
        </AuthLayout>
      );

      const subtitle = screen.getByText('Sign in to continue');
      expect(subtitle).toBeInTheDocument();
    });

    it('renders both title and subtitle', () => {
      render(
        <AuthLayout title="Welcome Back" subtitle="Sign in to continue">
          <div>Content</div>
        </AuthLayout>
      );

      expect(screen.getByRole('heading', { name: 'Welcome Back' })).toBeInTheDocument();
      expect(screen.getByText('Sign in to continue')).toBeInTheDocument();
    });

    it('does not render title section when neither title nor subtitle provided', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const titleSection = container.querySelector('h1');
      expect(titleSection).not.toBeInTheDocument();
    });
  });

  describe('Back Link', () => {
    it('shows back link by default', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const backLink = screen.getByRole('link', { name: /back to home/i });
      expect(backLink).toBeInTheDocument();
      expect(backLink).toHaveAttribute('href', '/');
    });

    it('hides back link when showBackLink is false', () => {
      render(
        <AuthLayout showBackLink={false}>
          <div>Content</div>
        </AuthLayout>
      );

      const backLink = screen.queryByRole('link', { name: /back to home/i });
      expect(backLink).not.toBeInTheDocument();
    });

    it('shows back link explicitly when showBackLink is true', () => {
      render(
        <AuthLayout showBackLink={true}>
          <div>Content</div>
        </AuthLayout>
      );

      const backLink = screen.getByRole('link', { name: /back to home/i });
      expect(backLink).toBeInTheDocument();
    });
  });

  describe('Layout Structure', () => {
    it('has min-h-screen for full viewport height', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const layoutWrapper = container.querySelector('div.min-h-screen');
      expect(layoutWrapper).toBeInTheDocument();
      expect(layoutWrapper).toHaveClass('flex');
      expect(layoutWrapper).toHaveClass('flex-col');
    });

    it('main content has flex-1 and centers content', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('flex-1');
      expect(main).toHaveClass('flex');
      expect(main).toHaveClass('items-center');
      expect(main).toHaveClass('justify-center');
    });

    it('content card has max-width constraint', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const cardWrapper = container.querySelector('main > div');
      expect(cardWrapper).toHaveClass('max-w-md');
    });

    it('content card has proper styling', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const card = container.querySelector('main > div > div:not(.text-center)');
      expect(card).toHaveClass('bg-white');
      expect(card).toHaveClass('dark:bg-slate-800');
      expect(card).toHaveClass('rounded-lg');
      expect(card).toHaveClass('shadow-lg');
    });

    it('applies proper gradient background', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const layoutWrapper = container.querySelector('div.min-h-screen');
      expect(layoutWrapper).toHaveClass('bg-gradient-to-br');
      expect(layoutWrapper).toHaveClass('from-slate-50');
      expect(layoutWrapper).toHaveClass('to-slate-100');
      expect(layoutWrapper).toHaveClass('dark:from-slate-950');
      expect(layoutWrapper).toHaveClass('dark:to-slate-900');
    });
  });

  describe('Custom Styling', () => {
    it('accepts custom className', () => {
      const { container } = render(
        <AuthLayout className="custom-class">
          <div>Content</div>
        </AuthLayout>
      );

      const cardWrapper = container.querySelector('main > div');
      expect(cardWrapper).toHaveClass('custom-class');
    });

    it('preserves base classes with custom className', () => {
      const { container } = render(
        <AuthLayout className="custom-class">
          <div>Content</div>
        </AuthLayout>
      );

      const cardWrapper = container.querySelector('main > div');
      expect(cardWrapper).toHaveClass('w-full');
      expect(cardWrapper).toHaveClass('max-w-md');
      expect(cardWrapper).toHaveClass('custom-class');
    });
  });

  describe('Responsive Behavior', () => {
    it('applies responsive padding to header', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const header = container.querySelector('header');
      expect(header).toHaveClass('px-4');
      expect(header).toHaveClass('sm:px-6');
      expect(header).toHaveClass('lg:px-8');
    });

    it('applies responsive padding to main', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const main = container.querySelector('main');
      expect(main).toHaveClass('px-4');
      expect(main).toHaveClass('sm:px-6');
      expect(main).toHaveClass('lg:px-8');
    });

    it('applies responsive padding to card', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const card = container.querySelector('main > div > div:not(.text-center)');
      expect(card).toHaveClass('p-6');
      expect(card).toHaveClass('sm:p-8');
    });
  });

  describe('Accessibility', () => {
    it('uses semantic HTML elements', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      expect(container.querySelector('header')).toBeInTheDocument();
      expect(container.querySelector('main')).toBeInTheDocument();
      expect(container.querySelector('footer')).toBeInTheDocument();
    });

    it('logo link has proper aria-label', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const logoLink = screen.getByLabelText('MeepleAI Home');
      expect(logoLink).toBeInTheDocument();
      expect(logoLink).toHaveAttribute('href', '/');
    });

    it('back link has proper aria-label', () => {
      render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const backLink = screen.getByLabelText('Back to home');
      expect(backLink).toBeInTheDocument();
    });

    it('footer navigation has proper aria-label', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const footerNav = container.querySelector('footer nav');
      expect(footerNav).toHaveAttribute('aria-label', 'Footer navigation');
    });

    it('has proper focus management for links', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const links = container.querySelectorAll('a');
      links.forEach(link => {
        expect(link).toHaveClass('focus:outline-none');
        expect(link).toHaveClass('focus:ring-2');
      });
    });
  });

  describe('Composition', () => {
    it('renders all layout parts in correct order', () => {
      const { container } = render(
        <AuthLayout>
          <div data-testid="content">Content</div>
        </AuthLayout>
      );

      const wrapper = container.querySelector('div.min-h-screen');
      const children = wrapper?.children;

      // Should have 3 children: header, main, footer
      expect(children).toHaveLength(3);
      expect(children?.[0]?.tagName).toBe('HEADER');
      expect(children?.[1]?.tagName).toBe('MAIN');
      expect(children?.[2]?.tagName).toBe('FOOTER');
    });

    it('renders content inside card inside main', () => {
      const { container } = render(
        <AuthLayout>
          <div data-testid="test-content">Test Content</div>
        </AuthLayout>
      );

      const main = container.querySelector('main');
      const content = screen.getByTestId('test-content');

      expect(main).toContainElement(content);
    });

    it('renders security notice after card', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const mainContainer = container.querySelector('main > div');
      const children = mainContainer?.children;

      // Last child should be security notice
      const lastChild = children?.[children.length - 1];
      expect(lastChild?.textContent).toMatch(/secured with industry-standard encryption/i);
    });
  });

  describe('Dark Mode Support', () => {
    it('applies dark mode classes to background', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const wrapper = container.querySelector('div.min-h-screen');
      expect(wrapper).toHaveClass('dark:from-slate-950');
      expect(wrapper).toHaveClass('dark:to-slate-900');
    });

    it('applies dark mode classes to card', () => {
      const { container } = render(
        <AuthLayout>
          <div>Content</div>
        </AuthLayout>
      );

      const card = container.querySelector('main > div > div:not(.text-center)');
      expect(card).toHaveClass('dark:bg-slate-800');
    });

    it('applies dark mode classes to text elements', () => {
      render(
        <AuthLayout title="Test Title" subtitle="Test Subtitle">
          <div>Content</div>
        </AuthLayout>
      );

      const title = screen.getByRole('heading', { name: 'Test Title' });
      expect(title).toHaveClass('dark:text-slate-50');

      const subtitle = screen.getByText('Test Subtitle');
      expect(subtitle).toHaveClass('dark:text-slate-400');
    });
  });
});
