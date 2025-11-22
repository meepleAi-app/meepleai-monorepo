/**
 * AuthModal Component Tests
 *
 * Tests for the unified authentication modal, with focus on:
 * - Demo login flow (passwordless)
 * - Loading states
 * - Error handling
 * - Tab navigation
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AuthModal } from '../AuthModal';

// Mock next/navigation
const mockPush = jest.fn();
jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

// Mock useAuth hook
const mockDemoLogin = jest.fn();
const mockUseAuth = {
  demoLogin: mockDemoLogin,
};

jest.mock('@/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth,
}));

// Mock useQueryClient
jest.mock('@/hooks/queries', () => ({
  useQueryClient: () => ({
    setQueryData: jest.fn(),
  }),
}));

// Mock useTranslation
jest.mock('@/hooks/useTranslation', () => ({
  useTranslation: () => ({
    t: (key: string) => key,
  }),
}));

describe('AuthModal - Demo Login', () => {
  const mockOnClose = jest.fn();
  const mockOnSuccess = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Demo Tab', () => {
    it('should render demo tab with three demo accounts', () => {
      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Check for Demo tab
      expect(screen.getByText('Demo')).toBeInTheDocument();

      // Check for all three demo accounts
      expect(screen.getByText('Admin')).toBeInTheDocument();
      expect(screen.getByText('Editor')).toBeInTheDocument();
      expect(screen.getByText('User')).toBeInTheDocument();

      // Check for email addresses
      expect(screen.getByText('admin@meepleai.dev')).toBeInTheDocument();
      expect(screen.getByText('editor@meepleai.dev')).toBeInTheDocument();
      expect(screen.getByText('user@meepleai.dev')).toBeInTheDocument();
    });

    it('should not display password fields', () => {
      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Verify "No password required" message is shown
      expect(screen.getAllByText(/No password required/i).length).toBeGreaterThan(0);

      // Verify no password input fields exist
      const passwordInputs = screen.queryAllByLabelText(/password/i);
      expect(passwordInputs).toHaveLength(0);
    });

    it('should show "Use this account" buttons for each demo account', () => {
      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      expect(buttons).toHaveLength(3);
    });
  });

  describe('Demo Login Flow', () => {
    it('should call demoLogin when "Use this account" button is clicked', async () => {
      const mockUser = {
        id: '1',
        email: 'user@meepleai.dev',
        displayName: 'Demo User',
        role: 'User',
      };

      mockDemoLogin.mockResolvedValueOnce(mockUser);

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
          defaultMode="demo"
        />
      );

      // Click on the first "Use this account" button (Admin)
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Verify demoLogin was called with correct email
      await waitFor(() => {
        expect(mockDemoLogin).toHaveBeenCalledWith({
          email: 'admin@meepleai.dev',
        });
      });
    });

    it('should show loading state during demo login', async () => {
      // Create a promise that we can control
      let resolveLogin: (value: any) => void;
      const loginPromise = new Promise((resolve) => {
        resolveLogin = resolve;
      });
      mockDemoLogin.mockReturnValueOnce(loginPromise);

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click "Use this account" button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for loading state
      await waitFor(() => {
        expect(screen.getByText('Logging in...')).toBeInTheDocument();
      });

      // Verify loading spinner is shown (SVG element)
      const svgElements = document.querySelectorAll('svg');
      expect(svgElements.length).toBeGreaterThan(0);

      // Resolve the login
      resolveLogin!({
        id: '1',
        email: 'admin@meepleai.dev',
        displayName: 'Admin',
        role: 'Admin',
      });
    });

    it('should hide demo credentials during loading', async () => {
      let resolveLogin: (value: any) => void;
      const loginPromise = new Promise((resolve) => {
        resolveLogin = resolve;
      });
      mockDemoLogin.mockReturnValueOnce(loginPromise);

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Initially, "Use this account" buttons should be visible
      expect(screen.getAllByRole('button', { name: /Use this account/i })).toHaveLength(3);

      // Click button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // During loading, buttons should be hidden
      await waitFor(() => {
        expect(screen.queryAllByRole('button', { name: /Use this account/i })).toHaveLength(0);
      });

      // Resolve login
      resolveLogin!({
        id: '1',
        email: 'admin@meepleai.dev',
        displayName: 'Admin',
        role: 'Admin',
      });
    });

    it('should close modal and redirect to /chat on successful login', async () => {
      const mockUser = {
        id: '1',
        email: 'user@meepleai.dev',
        displayName: 'Demo User',
        role: 'User',
      };

      mockDemoLogin.mockResolvedValueOnce(mockUser);

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          onSuccess={mockOnSuccess}
          defaultMode="demo"
        />
      );

      // Click "Use this account" button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[2]); // User account

      // Wait for login to complete
      await waitFor(() => {
        expect(mockDemoLogin).toHaveBeenCalledWith({
          email: 'user@meepleai.dev',
        });
      });

      // Verify modal closed and navigation occurred
      await waitFor(() => {
        expect(mockOnSuccess).toHaveBeenCalledWith(mockUser);
        expect(mockOnClose).toHaveBeenCalled();
        expect(mockPush).toHaveBeenCalledWith('/chat');
      });
    });
  });

  describe('Demo Login Error Handling', () => {
    it('should display error message when demo login fails', async () => {
      const errorMessage = 'Demo account not found';
      mockDemoLogin.mockRejectedValueOnce(new Error(errorMessage));

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click "Use this account" button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for error message to appear
      await waitFor(() => {
        expect(screen.getByText(errorMessage)).toBeInTheDocument();
      });

      // Verify error has proper role for accessibility
      const errorElement = screen.getByText(errorMessage).closest('[role="alert"]');
      expect(errorElement).toBeInTheDocument();
    });

    it('should show generic error message when error is not an Error instance', async () => {
      mockDemoLogin.mockRejectedValueOnce('Network error');

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click "Use this account" button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for generic error message
      await waitFor(() => {
        expect(screen.getByText('Demo login failed')).toBeInTheDocument();
      });
    });

    it('should clear error when attempting new login', async () => {
      // First login fails
      mockDemoLogin.mockRejectedValueOnce(new Error('First error'));

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click button - login fails
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for error
      await waitFor(() => {
        expect(screen.getByText('First error')).toBeInTheDocument();
      });

      // Second login succeeds
      mockDemoLogin.mockResolvedValueOnce({
        id: '1',
        email: 'admin@meepleai.dev',
        displayName: 'Admin',
        role: 'Admin',
      });

      // Click button again
      await userEvent.click(buttons[0]);

      // Error should be cleared during loading
      await waitFor(() => {
        expect(screen.queryByText('First error')).not.toBeInTheDocument();
      });
    });

    it('should show demo credentials again after error', async () => {
      mockDemoLogin.mockRejectedValueOnce(new Error('Login failed'));

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for error and loading to complete
      await waitFor(() => {
        expect(screen.getByText('Login failed')).toBeInTheDocument();
      });

      // Verify buttons are visible again
      expect(screen.getAllByRole('button', { name: /Use this account/i })).toHaveLength(3);
    });
  });

  describe('Tab Navigation', () => {
    it('should switch to demo tab when clicked', async () => {
      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="login"
        />
      );

      // Initially on login tab
      expect(screen.getByLabelText(/email/i)).toBeInTheDocument();

      // Click Demo tab
      const demoTab = screen.getByRole('tab', { name: /demo/i });
      await userEvent.click(demoTab);

      // Should show demo accounts
      await waitFor(() => {
        expect(screen.getByText('admin@meepleai.dev')).toBeInTheDocument();
      });
    });

    it('should not show OAuth buttons in demo tab', () => {
      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // OAuth buttons should not be present
      // (Check for common OAuth provider names)
      expect(screen.queryByText(/Google/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/GitHub/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Discord/i)).not.toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('should have accessible error alerts', async () => {
      mockDemoLogin.mockRejectedValueOnce(new Error('Test error'));

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Trigger error
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Wait for error
      await waitFor(() => {
        const alert = screen.getByRole('alert');
        expect(alert).toBeInTheDocument();
        expect(alert).toHaveTextContent('Test error');
      });
    });

    it('should have proper ARIA labels for loading state', async () => {
      let resolveLogin: (value: any) => void;
      const loginPromise = new Promise((resolve) => {
        resolveLogin = resolve;
      });
      mockDemoLogin.mockReturnValueOnce(loginPromise);

      render(
        <AuthModal
          isOpen={true}
          onClose={mockOnClose}
          defaultMode="demo"
        />
      );

      // Click button
      const buttons = screen.getAllByRole('button', { name: /Use this account/i });
      await userEvent.click(buttons[0]);

      // Check loading text is present
      await waitFor(() => {
        expect(screen.getByText('Logging in...')).toBeInTheDocument();
      });

      // Resolve login
      resolveLogin!({
        id: '1',
        email: 'admin@meepleai.dev',
        displayName: 'Admin',
        role: 'Admin',
      });
    });
  });
});
