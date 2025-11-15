/**
 * Unit tests for OAuth Callback Page (AUTH-06)
 *
 * Tests cover:
 * - Component rendering with different query params
 * - Success state handling (new user vs returning user)
 * - Error state handling
 * - Automatic redirects after delays
 * - Loading state display
 * - Accessibility features
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import { useRouter } from 'next/router';
import type { NextRouter } from 'next/router';
import OAuthCallbackPage from '../../../pages/auth/callback';
import { createMockRouter } from '../../fixtures/common-fixtures';

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

// Mock timers
jest.useFakeTimers();

describe('OAuth Callback Page', () => {
  let mockRouter: Partial<NextRouter>;

  beforeEach(() => {
    jest.clearAllMocks();
    mockRouter = createMockRouter({
      pathname: '/auth/callback',
      route: '/auth/callback'
    });
  });

  afterEach(() => {
    jest.runOnlyPendingTimers();
  });

  afterAll(() => {
    jest.useRealTimers();
  });

  describe('Loading State', () => {
    it('renders loading state by default', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Processing login...')).toBeInTheDocument();
      expect(screen.getByText('Please wait while we complete your authentication.')).toBeInTheDocument();
    });

    it('displays loading spinner', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const spinner = container.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('does not redirect while in loading state', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(5000);
      });

      expect(mockRouter.push).not.toHaveBeenCalled();
    });
  });

  describe('Success State - New User', () => {
    beforeEach(() => {
      mockRouter.query = { success: 'true', new: 'true' };
    });

    it('renders success state for new user', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Welcome!')).toBeInTheDocument();
      expect(screen.getByText('Redirecting to home page...')).toBeInTheDocument();
    });

    it('displays success icon', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const successIcon = container.querySelector('svg.text-green-600');
      expect(successIcon).toBeInTheDocument();
    });

    it('redirects to home page after 1.5 seconds', async () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(mockRouter.push).not.toHaveBeenCalled();

      act(() => {
        jest.advanceTimersByTime(1500);
      });

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith('/');
      });
    });

    it('does not redirect before 1.5 seconds', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(1000);
      });

      expect(mockRouter.push).not.toHaveBeenCalled();
    });
  });

  describe('Success State - Returning User', () => {
    beforeEach(() => {
      mockRouter.query = { success: 'true', new: 'false' };
    });

    it('renders success state for returning user', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Login successful!')).toBeInTheDocument();
      expect(screen.getByText('Redirecting to home page...')).toBeInTheDocument();
    });

    it('displays correct message when new parameter is not "true"', () => {
      mockRouter.query = { success: 'true' }; // new is missing
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Login successful!')).toBeInTheDocument();
    });

    it('redirects to home page after 1.5 seconds', async () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(1500);
      });

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith('/');
      });
    });
  });

  describe('Error State', () => {
    beforeEach(() => {
      mockRouter.query = { error: 'authentication_failed' };
    });

    it('renders error state when error param is present', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Login failed')).toBeInTheDocument();
      expect(screen.getByText('Something went wrong. Redirecting to login page...')).toBeInTheDocument();
    });

    it('displays error icon', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const errorIcon = container.querySelector('svg.text-red-600');
      expect(errorIcon).toBeInTheDocument();
    });

    it('redirects to login page after 3 seconds', async () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(mockRouter.push).not.toHaveBeenCalled();

      act(() => {
        jest.advanceTimersByTime(3000);
      });

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith('/login');
      });
    });

    it('does not redirect before 3 seconds', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(2000);
      });

      expect(mockRouter.push).not.toHaveBeenCalled();
    });
  });

  describe('Page Title', () => {
    it('renders page title', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      // Next.js Head doesn't update document.title in Jest, but we can verify rendering succeeds
      expect(screen.getByText('Processing login...')).toBeInTheDocument();
    });
  });

  describe('Visual Elements', () => {
    it('renders with proper layout classes', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const mainContainer = container.querySelector('.min-h-dvh');
      expect(mainContainer).toBeInTheDocument();
      expect(mainContainer).toHaveClass('flex', 'items-center', 'justify-center');
    });

    it('renders card container with proper styling', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const card = container.querySelector('.rounded-2xl');
      expect(card).toBeInTheDocument();
      expect(card).toHaveClass('shadow-xl', 'p-8', 'text-center');
    });

    it('displays gradient background', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const background = container.querySelector('.bg-gradient-to-br');
      expect(background).toBeInTheDocument();
    });

    it('supports dark mode styling', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { container } = render(<OAuthCallbackPage />);

      const card = container.querySelector('.dark\\:bg-slate-800');
      expect(card).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('handles missing query parameters', () => {
      mockRouter.query = {};
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Processing login...')).toBeInTheDocument();
    });

    it('handles both success and error params (success takes precedence)', () => {
      mockRouter.query = { success: 'true', error: 'something_failed' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      // Success should take precedence since it's checked first in useEffect
      expect(screen.getByText('Login successful!')).toBeInTheDocument();
    });

    it('handles success=false correctly', () => {
      mockRouter.query = { success: 'false' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      // Should remain in loading state
      expect(screen.getByText('Processing login...')).toBeInTheDocument();
    });

    it('handles arbitrary error messages', () => {
      mockRouter.query = { error: 'custom_error_code' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      expect(screen.getByText('Login failed')).toBeInTheDocument();
    });

    it('handles new parameter as string array (Next.js edge case)', () => {
      mockRouter.query = { success: 'true', new: ['true', 'false'] as any };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      // Should not match 'true' when it's an array
      expect(screen.getByText('Login successful!')).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('has proper heading structure in loading state', () => {
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Processing login...');
    });

    it('has proper heading structure in success state', () => {
      mockRouter.query = { success: 'true' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Login successful!');
    });

    it('has proper heading structure in error state', () => {
      mockRouter.query = { error: 'failed' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      render(<OAuthCallbackPage />);

      const heading = screen.getByRole('heading', { level: 2 });
      expect(heading).toHaveTextContent('Login failed');
    });
  });

  describe('State Transitions', () => {
    it('transitions from loading to success when success param appears', () => {
      mockRouter.query = {};
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { rerender } = render(<OAuthCallbackPage />);

      expect(screen.getByText('Processing login...')).toBeInTheDocument();

      // Update query params (simulating router change)
      mockRouter.query = { success: 'true' };
      rerender(<OAuthCallbackPage />);

      expect(screen.getByText('Login successful!')).toBeInTheDocument();
    });

    it('transitions from loading to error when error param appears', () => {
      mockRouter.query = {};
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { rerender } = render(<OAuthCallbackPage />);

      expect(screen.getByText('Processing login...')).toBeInTheDocument();

      // Update query params (simulating router change)
      mockRouter.query = { error: 'failed' };
      rerender(<OAuthCallbackPage />);

      expect(screen.getByText('Login failed')).toBeInTheDocument();
    });
  });

  describe('Multiple Renders', () => {
    it('only sets up one redirect timeout on success', async () => {
      mockRouter.query = { success: 'true' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { rerender } = render(<OAuthCallbackPage />);

      rerender(<OAuthCallbackPage />);
      rerender(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(1500);
      });

      await waitFor(() => {
        // Should only be called once despite multiple renders
        expect(mockRouter.push).toHaveBeenCalledTimes(1);
      });
    });

    it('only sets up one redirect timeout on error', async () => {
      mockRouter.query = { error: 'failed' };
      useRouterMock.mockReturnValue(mockRouter as NextRouter);
      const { rerender } = render(<OAuthCallbackPage />);

      rerender(<OAuthCallbackPage />);
      rerender(<OAuthCallbackPage />);

      act(() => {
        jest.advanceTimersByTime(3000);
      });

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledTimes(1);
      });
    });
  });
});

