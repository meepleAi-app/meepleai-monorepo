/**
 * Unit tests for Profile Page (AUTH-06-P3)
 *
 * Tests cover:
 * - Profile display (user info, avatar, roles)
 * - OAuth accounts management (list, link, unlink)
 * - Loading states and error handling
 * - Success/error alerts
 * - User interactions and confirmations
 * - Accessibility
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import ProfilePage from '../../pages/profile';
import { createMockRouter } from '../fixtures/common-fixtures';

// Mock Next.js router
jest.mock('next/router', () => ({
  useRouter: jest.fn(),
}));

const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

// Mock fetch globally
const mockFetch = jest.fn();
global.fetch = mockFetch;

// Mock window.confirm
const mockConfirm = jest.fn();
global.confirm = mockConfirm;

// Mock window.alert
const mockAlert = jest.fn();
global.alert = mockAlert;

// Test data
const mockUser = {
  id: 'user-123',
  email: 'test@meepleai.dev',
  displayName: 'Test User',
  role: 'User',
};

const mockUserNoDisplayName = {
  id: 'user-456',
  email: 'no-display@meepleai.dev',
  displayName: null,
  role: 'Editor',
};

const mockOAuthAccounts = [
  { provider: 'google', createdAt: '2024-01-01T00:00:00Z' },
  { provider: 'discord', createdAt: '2024-01-02T00:00:00Z' },
];

describe('ProfilePage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
    mockFetch.mockClear();
    mockConfirm.mockClear();
    mockAlert.mockClear();
    useRouterMock.mockReturnValue(createMockRouter({ pathname: '/profile' }));
    process.env.NEXT_PUBLIC_API_BASE = 'http://localhost:8080';
  });

  describe('Loading States', () => {
    it('displays loading spinner initially', () => {
      mockFetch.mockImplementation(() => new Promise(() => {})); // Never resolves
      render(<ProfilePage />);

      expect(screen.getByRole('status', { hidden: true })).toBeInTheDocument();
      const spinner = document.querySelector('.animate-spin');
      expect(spinner).toBeInTheDocument();
    });

    it('hides loading spinner after data loads', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.queryByRole('status', { hidden: true })).not.toBeInTheDocument();
      });
    });
  });

  describe('Authentication Flow', () => {
    it('redirects to login when auth fails', async () => {
      const mockRouter = createMockRouter({ pathname: '/profile' });
      useRouterMock.mockReturnValue(mockRouter);

      mockFetch.mockResolvedValueOnce({
        ok: false,
        status: 401,
      });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(mockRouter.push).toHaveBeenCalledWith('/login');
      });
    });

    it('fetches user info with credentials', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/auth/me',
          { credentials: 'include' }
        );
      });
    });

    it('fetches OAuth accounts after user info loads', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/users/me/oauth-accounts',
          { credentials: 'include' }
        );
      });
    });

    it('handles OAuth account fetch failure gracefully', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();

      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: false,
          status: 500,
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });

      // Should still render profile without OAuth accounts
      expect(screen.getByText(mockUser.email)).toBeInTheDocument();

      consoleError.mockRestore();
    });
  });

  describe('User Information Display', () => {
    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });
    });

    it('renders page title', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });
    });

    it('displays user email', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText(mockUser.email)).toBeInTheDocument();
      });
    });

    it('displays user display name', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText(mockUser.displayName)).toBeInTheDocument();
      });
    });

    it('displays user role in lowercase', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('user')).toBeInTheDocument();
      });
    });

    it('handles missing display name gracefully', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUserNoDisplayName }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText(mockUserNoDisplayName.email)).toBeInTheDocument();
      });

      // Display Name section should not be rendered
      expect(screen.queryByText('Display Name')).not.toBeInTheDocument();
    });

    it('renders "Back to Home" link', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const homeLink = screen.getByRole('link', { name: 'Back to Home' });
        expect(homeLink).toHaveAttribute('href', '/');
      });
    });
  });

  describe('OAuth Accounts Display', () => {
    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });
    });

    it('displays "Linked Accounts" heading', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Linked Accounts')).toBeInTheDocument();
      });
    });

    it('displays all OAuth providers', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
        expect(screen.getByText('Discord')).toBeInTheDocument();
        expect(screen.getByText('GitHub')).toBeInTheDocument();
      });
    });

    it('shows "Connected" status for linked accounts', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const connectedElements = screen.getAllByText('Connected');
        expect(connectedElements).toHaveLength(2); // Google and Discord
      });
    });

    it('shows "Not connected" status for unlinked accounts', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const notConnectedElements = screen.getAllByText('Not connected');
        expect(notConnectedElements).toHaveLength(1); // GitHub only
      });
    });

    it('displays provider avatar placeholders', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('G')).toBeInTheDocument(); // Google
        expect(screen.getByText('D')).toBeInTheDocument(); // Discord
        expect(screen.getByText('G')).toBeInTheDocument(); // GitHub (also G)
      });
    });

    it('displays helper text about linking accounts', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        expect(
          screen.getByText('Connect your accounts to login with social providers.')
        ).toBeInTheDocument();
        expect(
          screen.getByText('Linking an account allows you to login without a password.')
        ).toBeInTheDocument();
      });
    });
  });

  describe('OAuth Linking', () => {
    let originalLocation: Location;

    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      // Save original location and mock it
      originalLocation = window.location;
      delete (window as any).location;
      window.location = { href: '' } as any;
    });

    afterEach(() => {
      // Restore original location
      window.location = originalLocation;
    });

    it('renders "Link" button for unlinked accounts', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const linkButtons = screen.getAllByRole('button', { name: 'Link' });
        expect(linkButtons).toHaveLength(3); // All providers unlinked
      });
    });

    it('redirects to OAuth flow when clicking Link button', async () => {
      const user = userEvent.setup();
      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleLinkButton = screen.getAllByRole('button', { name: 'Link' })[0];
      await user.click(googleLinkButton);

      expect(window.location.href).toBe(
        'http://localhost:8080/api/v1/auth/oauth/google/login'
      );
    });

    it('applies correct styling to Link buttons', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const linkButtons = screen.getAllByRole('button', { name: 'Link' });
        expect(linkButtons[0]).toHaveClass('bg-blue-600', 'hover:bg-blue-700'); // Google
        expect(linkButtons[1]).toHaveClass('bg-indigo-600', 'hover:bg-indigo-700'); // Discord
        expect(linkButtons[2]).toHaveClass('bg-slate-800', 'hover:bg-slate-900'); // GitHub
      });
    });
  });

  describe('OAuth Unlinking', () => {
    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });
    });

    it('renders "Unlink" button for linked accounts', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const unlinkButtons = screen.getAllByRole('button', { name: 'Unlink' });
        expect(unlinkButtons).toHaveLength(2); // Google and Discord
      });
    });

    it('shows confirmation dialog when clicking Unlink', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(false); // User cancels

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      expect(mockConfirm).toHaveBeenCalledWith('Unlink your google account?');
    });

    it('does not unlink if user cancels confirmation', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(false);

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      expect(mockFetch).toHaveBeenCalledTimes(2); // Only initial fetches
    });

    it('sends DELETE request when unlink confirmed', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockFetch.mockResolvedValueOnce({ ok: true });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/auth/oauth/google/unlink',
          {
            method: 'DELETE',
            credentials: 'include',
          }
        );
      });
    });

    it('updates UI after successful unlink', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockFetch.mockResolvedValueOnce({ ok: true });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getAllByText('Connected')).toHaveLength(2);
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      await waitFor(() => {
        expect(screen.getAllByText('Connected')).toHaveLength(1); // Only Discord now
        expect(screen.getAllByText('Not connected')).toHaveLength(2); // Google and GitHub
      });
    });

    it('shows loading state during unlink', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockFetch.mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve({ ok: true }), 100))
      );

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      // Should show "Unlinking..." text
      await waitFor(() => {
        expect(screen.getByText('Unlinking...')).toBeInTheDocument();
      });

      // Button should be disabled
      const unlinkingButton = screen.getByRole('button', { name: 'Unlinking...' });
      expect(unlinkingButton).toBeDisabled();
    });

    it('shows alert on unlink failure', async () => {
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockFetch.mockResolvedValueOnce({ ok: false, status: 500 });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith('Failed to unlink account. Please try again.');
      });
    });

    it('handles unlink network error', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();
      const user = userEvent.setup();
      mockConfirm.mockReturnValue(true);
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Google')).toBeInTheDocument();
      });

      const googleUnlinkButton = screen.getAllByRole('button', { name: 'Unlink' })[0];
      await user.click(googleUnlinkButton);

      await waitFor(() => {
        expect(mockAlert).toHaveBeenCalledWith('Failed to unlink account.');
      });

      consoleError.mockRestore();
    });
  });

  describe('Error Handling', () => {
    it('logs error when profile load fails', async () => {
      const consoleError = jest.spyOn(console, 'error').mockImplementation();

      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      render(<ProfilePage />);

      await waitFor(() => {
        expect(consoleError).toHaveBeenCalledWith(
          'Failed to load profile:',
          expect.any(Error)
        );
      });

      consoleError.mockRestore();
    });

    it('returns null if user data fails to load', async () => {
      mockFetch.mockRejectedValueOnce(new Error('Network error'));

      const { container } = render(<ProfilePage />);

      await waitFor(() => {
        expect(container.firstChild).toBeNull();
      });
    });
  });

  describe('Accessibility', () => {
    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });
    });

    it('has proper heading hierarchy', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const h1 = screen.getByRole('heading', { level: 1, name: 'Profile' });
        expect(h1).toBeInTheDocument();

        const h2s = screen.getAllByRole('heading', { level: 2 });
        expect(h2s).toHaveLength(2); // Account Information, Linked Accounts
      });
    });

    it('uses semantic HTML for user info', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const dlElements = document.querySelectorAll('dl');
        expect(dlElements).toHaveLength(1); // Description list for user info

        const dtElements = document.querySelectorAll('dt');
        expect(dtElements.length).toBeGreaterThan(0);

        const ddElements = document.querySelectorAll('dd');
        expect(ddElements.length).toBeGreaterThan(0);
      });
    });

    it('buttons have proper focus styling', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const linkButton = screen.getAllByRole('button', { name: 'Link' })[0];
        expect(linkButton).toHaveClass('focus:outline-none', 'focus:ring-2');
      });
    });

    it('loading spinner has accessible role', () => {
      mockFetch.mockImplementation(() => new Promise(() => {}));
      render(<ProfilePage />);

      const spinner = document.querySelector('[role="status"]');
      expect(spinner).toBeInTheDocument();
    });
  });

  describe('Visual Elements', () => {
    beforeEach(async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => mockOAuthAccounts,
        });
    });

    it('renders with gradient background', async () => {
      const { container } = render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });

      const background = container.querySelector('.bg-gradient-to-br');
      expect(background).toBeInTheDocument();
    });

    it('renders cards with proper styling', async () => {
      const { container } = render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });

      const cards = container.querySelectorAll('.rounded-xl.shadow-lg');
      expect(cards.length).toBeGreaterThan(0);
    });

    it('supports dark mode', async () => {
      const { container } = render(<ProfilePage />);

      await waitFor(() => {
        expect(screen.getByText('Profile')).toBeInTheDocument();
      });

      const darkElements = container.querySelectorAll('[class*="dark:"]');
      expect(darkElements.length).toBeGreaterThan(0);
    });

    it('uses provider-specific button colors', async () => {
      render(<ProfilePage />);

      await waitFor(() => {
        const linkButtons = screen.getAllByRole('button', { name: 'Link' });
        expect(linkButtons[0]).toHaveClass('bg-blue-600'); // Google
        expect(linkButtons[1]).toHaveClass('bg-indigo-600'); // Discord
        expect(linkButtons[2]).toHaveClass('bg-slate-800'); // GitHub
      });
    });
  });

  describe('Edge Cases', () => {
    it('handles empty OAuth accounts array', async () => {
      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      render(<ProfilePage />);

      await waitFor(() => {
        const notConnectedElements = screen.getAllByText('Not connected');
        expect(notConnectedElements).toHaveLength(3); // All providers unlinked
      });
    });

    it('handles API_BASE from environment variable', async () => {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';

      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          'https://api.example.com/api/v1/auth/me',
          { credentials: 'include' }
        );
      });
    });

    it('falls back to localhost when API_BASE not set', async () => {
      delete process.env.NEXT_PUBLIC_API_BASE;

      mockFetch
        .mockResolvedValueOnce({
          ok: true,
          json: async () => ({ user: mockUser }),
        })
        .mockResolvedValueOnce({
          ok: true,
          json: async () => [],
        });

      render(<ProfilePage />);

      await waitFor(() => {
        expect(mockFetch).toHaveBeenCalledWith(
          'http://localhost:8080/api/v1/auth/me',
          { credentials: 'include' }
        );
      });
    });
  });
});
