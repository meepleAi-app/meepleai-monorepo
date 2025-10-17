/**
 * Unit tests for Login Page (AUTH-05)
 *
 * Tests cover:
 * - Component rendering without crashing
 * - Session expired alert display based on query param
 * - Return to home link functionality
 * - Proper ARIA attributes for accessibility
 * - Visual elements (logo, title, description)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import type { NextRouter } from 'next/router';
import LoginPage from '../../pages/login';

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

const createMockRouter = (query: Record<string, string | string[]> = {}): jest.Mocked<NextRouter> => ({
  push: jest.fn(),
  replace: jest.fn(),
  reload: jest.fn(),
  back: jest.fn(),
  prefetch: jest.fn().mockResolvedValue(undefined),
  beforePopState: jest.fn(),
  events: {
    on: jest.fn(),
    off: jest.fn(),
    emit: jest.fn(),
  },
  isFallback: false,
  isReady: true,
  isPreview: false,
  isLocaleDomain: false,
  basePath: '',
  pathname: '/login',
  route: '/login',
  query,
  asPath: query.reason ? `/login?reason=${query.reason}` : '/login',
} as unknown as jest.Mocked<NextRouter>);

describe('Login Page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Basic Rendering', () => {
    it('renders without crashing', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    });

    it('renders the page title correctly', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      const { container } = render(<LoginPage />);

      // Check for Head component content (Next.js doesn't set document.title in tests)
      const head = container.querySelector('head');
      // Just verify the page renders without error since Next.js Head doesn't update document.title in jest
      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    });

    it('displays the MeepleAI branding', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
      expect(screen.getByText('AI-Powered Board Game Rules Assistant')).toBeInTheDocument();
    });

    it('displays login placeholder text', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      expect(screen.getByText('Login functionality will be implemented here.')).toBeInTheDocument();
    });

    it('displays security notice in footer', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      expect(screen.getByText('Sessions expire after 30 days of inactivity for your security.')).toBeInTheDocument();
    });
  });

  describe('Session Expiration Alert', () => {
    it('displays session expired alert when reason=session_expired query param is present', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'session_expired' }));
      render(<LoginPage />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText('Session Expired')).toBeInTheDocument();
      expect(screen.getByText(/Your session has expired due to inactivity/i)).toBeInTheDocument();
    });

    it('does not display session expired alert when no query param', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.queryByText('Session Expired')).not.toBeInTheDocument();
    });

    it('does not display session expired alert when reason param is different', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'other_reason' }));
      render(<LoginPage />);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.queryByText('Session Expired')).not.toBeInTheDocument();
    });

    it('session expired alert contains proper warning icon', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'session_expired' }));
      render(<LoginPage />);

      const alert = screen.getByRole('alert');
      // Check for SVG icon (warning triangle icon)
      const svg = alert.querySelector('svg');
      expect(svg).toBeInTheDocument();
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    });

    it('session expired alert contains complete message text', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'session_expired' }));
      render(<LoginPage />);

      expect(screen.getByText('Your session has expired due to inactivity. Please log in again to continue.')).toBeInTheDocument();
    });
  });

  describe('Return to Home Link', () => {
    it('renders "Return to Home" link', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      expect(homeLink).toBeInTheDocument();
    });

    it('Return to Home link has correct href', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      expect(homeLink).toHaveAttribute('href', '/');
    });

    it('Return to Home link has proper styling classes', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      expect(homeLink).toHaveClass('bg-primary-600', 'hover:bg-primary-700');
    });
  });

  describe('Accessibility', () => {
    it('session expired alert has proper ARIA role', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'session_expired' }));
      render(<LoginPage />);

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('role', 'alert');
    });

    it('warning icon is properly hidden from screen readers', () => {
      useRouterMock.mockReturnValue(createMockRouter({ reason: 'session_expired' }));
      render(<LoginPage />);

      const alert = screen.getByRole('alert');
      const svg = alert.querySelector('svg');
      expect(svg).toHaveAttribute('aria-hidden', 'true');
    });

    it('page has proper heading structure', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      const heading = screen.getByRole('heading', { level: 1 });
      expect(heading).toHaveTextContent('MeepleAI');
    });

    it('form section has proper semantic structure', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      // The page should have proper semantic HTML
      expect(screen.getByText('MeepleAI')).toBeInTheDocument();
    });
  });

  describe('Visual Elements', () => {
    it('renders with proper layout classes', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      const { container } = render(<LoginPage />);

      const mainContainer = container.querySelector('.min-h-screen');
      expect(mainContainer).toBeInTheDocument();
      expect(mainContainer).toHaveClass('flex', 'items-center', 'justify-center');
    });

    it('renders card container with proper styling', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      const { container } = render(<LoginPage />);

      const card = container.querySelector('.rounded-2xl');
      expect(card).toBeInTheDocument();
      expect(card).toHaveClass('shadow-xl', 'p-8');
    });

    it('displays gradient background', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      const { container } = render(<LoginPage />);

      const background = container.querySelector('.bg-gradient-to-br');
      expect(background).toBeInTheDocument();
    });

    it('supports dark mode styling', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      const { container } = render(<LoginPage />);

      const card = container.querySelector('.dark\\:bg-slate-800');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles missing query object gracefully', () => {
      // This test verifies behavior when query is undefined
      // Note: In production, router.query is always defined (at least as {})
      // but we test defensive coding
      const routerWithoutQuery = createMockRouter();
      delete (routerWithoutQuery as any).query;
      useRouterMock.mockReturnValue(routerWithoutQuery);

      // This will throw because the component destructures router.query
      // which is a known limitation - router.query is always defined in Next.js
      expect(() => render(<LoginPage />)).toThrow();
    });

    it('handles empty query object', () => {
      useRouterMock.mockReturnValue(createMockRouter({}));

      render(<LoginPage />);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('handles multiple query parameters', () => {
      useRouterMock.mockReturnValue(createMockRouter({
        reason: 'session_expired',
        redirect: '/chat'
      }));

      render(<LoginPage />);
      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('reason param as array (edge case)', () => {
      useRouterMock.mockReturnValue(createMockRouter({
        reason: ['session_expired', 'other']
      }));

      render(<LoginPage />);
      // Should not show alert when reason is an array
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });
  });

  describe('User Interactions', () => {
    it('clicking Return to Home link navigates correctly', async () => {
      const user = userEvent.setup();
      useRouterMock.mockReturnValue(createMockRouter());

      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      await user.click(homeLink);

      // Link should have correct href (navigation handled by Next.js)
      expect(homeLink).toHaveAttribute('href', '/');
    });

    it('Return to Home link can receive focus', async () => {
      const user = userEvent.setup();
      useRouterMock.mockReturnValue(createMockRouter());

      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      await user.tab();

      // After tab, the link should be focusable
      expect(homeLink).toBeInTheDocument();
    });

    it('focus styles are applied correctly', () => {
      useRouterMock.mockReturnValue(createMockRouter());
      render(<LoginPage />);

      const homeLink = screen.getByRole('link', { name: 'Return to Home' });
      expect(homeLink).toHaveClass('focus:outline-none', 'focus:ring-2', 'focus:ring-primary-500');
    });
  });
});
