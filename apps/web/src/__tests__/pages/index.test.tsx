import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/router';
import type { NextRouter } from 'next/router';
import Home from '../index';
import { api } from '../../lib/api';

jest.mock('next/router', () => ({
  useRouter: jest.fn()
}));

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn()
  }
}));

const mockedApi = api as jest.Mocked<typeof api>;
const useRouterMock = useRouter as jest.MockedFunction<typeof useRouter>;

const createMockRouter = (): jest.Mocked<NextRouter> => ({
  push: jest.fn(),
  replace: jest.fn(),
  reload: jest.fn(),
  back: jest.fn(),
  prefetch: jest.fn().mockResolvedValue(undefined),
  beforePopState: jest.fn(),
  events: {
    on: jest.fn(),
    off: jest.fn(),
    emit: jest.fn()
  },
  isFallback: false,
  isReady: true,
  isPreview: false,
  isLocaleDomain: false,
  basePath: '',
  pathname: '/',
  route: '/',
  query: {},
  asPath: '/'
} as unknown as jest.Mocked<NextRouter>);

let routerMock = createMockRouter();

describe('Home page (Landing Page)', () => {
  beforeEach(() => {
    routerMock = createMockRouter();
    useRouterMock.mockReturnValue(routerMock);

    mockedApi.get.mockReset();
    mockedApi.post.mockReset();
    mockedApi.put.mockReset();
    mockedApi.get.mockResolvedValue(null);
  });

  afterEach(() => {
    jest.clearAllMocks();
    useRouterMock.mockReset();
  });

  describe('Hero Section', () => {
    it('renders hero section with title and description', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Your AI-Powered', { exact: false })).toBeInTheDocument();
      });

      expect(screen.getByText('Board Game Rules Assistant', { exact: false })).toBeInTheDocument();
      expect(
        screen.getByText('Never argue about rules again.', { exact: false })
      ).toBeInTheDocument();
    });

    it('shows "Get Started Free" button when user is not authenticated', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalledWith('/api/v1/auth/me');
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      expect(getStartedButtons.length).toBeGreaterThan(0);
    });

    it('shows "Go to Chat" button when user is authenticated', async () => {
      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Go to Chat')).toBeInTheDocument();
      });
    });

    it('shows demo credentials hint when not authenticated', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText(/Try with demo account/i)).toBeInTheDocument();
      });

      // user@meepleai.dev appears multiple times (hero + footer)
      const emailElements = screen.getAllByText('user@meepleai.dev');
      expect(emailElements.length).toBeGreaterThan(0);
      expect(screen.getByText('Demo123!')).toBeInTheDocument();
    });
  });

  describe('Header Navigation', () => {
    it('shows MeepleAI logo and Get Started button when not authenticated', async () => {
      render(<Home />);

      await waitFor(() => {
        const logoElements = screen.getAllByText('MeepleAI');
        expect(logoElements.length).toBeGreaterThan(0);
      });

      const header = screen.getByRole('banner');
      const getStartedButton = within(header).getByText('Get Started');
      expect(getStartedButton).toBeInTheDocument();
    });

    it('shows navigation links when authenticated', async () => {
      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByRole('link', { name: 'Chat' })).toBeInTheDocument();
      });

      expect(screen.getByRole('link', { name: 'Chess' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Upload' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument();
    });

    it('shows Admin link for admin users', async () => {
      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'admin-1',
          email: 'admin@example.com',
          displayName: 'Admin User',
          role: 'Admin'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByRole('link', { name: 'Admin' })).toBeInTheDocument();
      });
    });

    it('does not show Admin link for non-admin users', async () => {
      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByRole('link', { name: 'Chat' })).toBeInTheDocument();
      });

      expect(screen.queryByRole('link', { name: 'Admin' })).not.toBeInTheDocument();
    });
  });

  describe('Auth Modal', () => {
    it('opens auth modal when clicking Get Started button', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          const loginButtons = screen.getAllByRole('button', { name: 'Login' });
          expect(loginButtons.length).toBeGreaterThan(0);
          const registerButtons = screen.getAllByRole('button', { name: 'Register' });
          expect(registerButtons.length).toBeGreaterThan(0);
        },
        { timeout: 3000 }
      );
    });

    it('closes auth modal when clicking X button', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          const loginButtons = screen.getAllByRole('button', { name: 'Login' });
          expect(loginButtons.length).toBeGreaterThan(0);
        },
        { timeout: 3000 }
      );

      const closeButton = screen.getByText('✕');
      await user.click(closeButton);

      await waitFor(
        () => {
          const loginButtons = screen.queryAllByRole('button', { name: 'Login' });
          expect(loginButtons.length).toBe(0);
        },
        { timeout: 3000 }
      );
    });

    it('switches between Login and Register tabs', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          const loginButtons = screen.getAllByRole('button', { name: 'Login' });
          expect(loginButtons.length).toBeGreaterThan(0);
        },
        { timeout: 3000 }
      );

      // Should show login form by default
      await waitFor(() => {
        expect(screen.getByLabelText('Email')).toBeInTheDocument();
        expect(screen.getByLabelText('Password')).toBeInTheDocument();
      });

      // Switch to Register tab (first one is the tab)
      const registerButtons = screen.getAllByRole('button', { name: 'Register' });
      await user.click(registerButtons[0]);

      // Wait for register form to appear (has unique fields)
      await waitFor(
        () => {
          expect(screen.getByLabelText('Password (min 8 characters)')).toBeInTheDocument();
          expect(screen.getByLabelText('Display Name (optional)')).toBeInTheDocument();
          expect(screen.getByLabelText('Role')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    });
  });

  describe('Authentication - Login', () => {
    it('logs in the user and redirects to /chat', async () => {
      const user = userEvent.setup();

      const loginResponse = {
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      };

      mockedApi.post.mockResolvedValueOnce(loginResponse);

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill login form
      await user.type(screen.getByLabelText('Email'), 'user@example.com');
      await user.type(screen.getByLabelText('Password'), 'super-secret');

      // Submit (second button is the submit button)
      const loginButtons = screen.getAllByRole('button', { name: 'Login' });
      await user.click(loginButtons[1]);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/login', {
          email: 'user@example.com',
          password: 'super-secret'
        });
      });

      await waitFor(() => {
        expect(routerMock.push).toHaveBeenCalledWith('/chat');
      });
    });

    it('displays error message when login fails', async () => {
      const user = userEvent.setup();

      mockedApi.post.mockRejectedValueOnce(new Error('Credenziali errate'));

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill login form
      await user.type(screen.getByLabelText('Email'), 'user@example.com');
      await user.type(screen.getByLabelText('Password'), 'wrong-password');

      // Submit (second button is the submit button)
      const loginButtons = screen.getAllByRole('button', { name: 'Login' });
      await user.click(loginButtons[1]);

      await waitFor(() => {
        expect(screen.getByText('Credenziali errate')).toBeInTheDocument();
      });
    });

    it('displays fallback error message when login fails without error message', async () => {
      const user = userEvent.setup();

      mockedApi.post.mockRejectedValueOnce({});

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill and submit
      await user.type(screen.getByLabelText('Email'), 'user@example.com');
      await user.type(screen.getByLabelText('Password'), 'wrong-password');
      // Get all Login buttons (tab + submit) and click the submit button (index 1)
      const loginButtons = screen.getAllByRole('button', { name: 'Login' });
      await user.click(loginButtons[1]);

      await waitFor(() => {
        expect(screen.getByText('Accesso non riuscito.')).toBeInTheDocument();
      });
    });
  });

  describe('Authentication - Register', () => {
    it('registers the user and redirects to /chat', async () => {
      const user = userEvent.setup();

      const authResponse = {
        user: {
          id: 'user-1',
          email: 'new@example.com',
          displayName: 'New User',
          role: 'Editor'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      };

      mockedApi.post.mockResolvedValueOnce(authResponse);

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Switch to Register tab (first button is the tab)
      const registerButtons = screen.getAllByRole('button', { name: 'Register' });
      await user.click(registerButtons[0]);

      await waitFor(() => {
        expect(screen.getByLabelText('Password (min 8 characters)')).toBeInTheDocument();
      });

      // Fill register form
      await user.type(screen.getByLabelText('Email'), 'new@example.com');
      await user.type(screen.getByLabelText('Password (min 8 characters)'), 'password123');
      await user.type(screen.getByLabelText('Display Name (optional)'), 'New User');
      await user.selectOptions(screen.getByLabelText('Role'), 'Editor');

      // Submit
      const createAccountButton = screen.getByRole('button', { name: 'Create Account' });
      await user.click(createAccountButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
          email: 'new@example.com',
          password: 'password123',
          displayName: 'New User',
          role: 'Editor'
        });
      });

      await waitFor(() => {
        expect(routerMock.push).toHaveBeenCalledWith('/chat');
      });
    });

    it('handles registration with empty displayName using undefined fallback', async () => {
      const user = userEvent.setup();

      const authResponse = {
        user: {
          id: 'user-1',
          email: 'new@example.com',
          displayName: null,
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      };

      mockedApi.post.mockResolvedValueOnce(authResponse);

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal and switch to Register
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const registerTab = screen.getByRole('button', { name: 'Register' });
      await user.click(registerTab);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill form without displayName
      await user.type(screen.getByLabelText('Email'), 'new@example.com');
      await user.type(screen.getByLabelText('Password (min 8 characters)'), 'password123');

      const createAccountButton = screen.getByRole('button', { name: 'Create Account' });
      await user.click(createAccountButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
          email: 'new@example.com',
          password: 'password123',
          displayName: undefined,
          role: 'User'
        });
      });
    });

    it('displays error message when registration fails', async () => {
      const user = userEvent.setup();

      mockedApi.post.mockRejectedValueOnce(new Error('Registrazione fallita'));

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal and switch to Register
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const registerTab = screen.getByRole('button', { name: 'Register' });
      await user.click(registerTab);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill and submit
      await user.type(screen.getByLabelText('Email'), 'new@example.com');
      await user.type(screen.getByLabelText('Password (min 8 characters)'), 'password123');

      const createAccountButton = screen.getByRole('button', { name: 'Create Account' });
      await user.click(createAccountButton);

      await waitFor(() => {
        expect(screen.getByText('Registrazione fallita')).toBeInTheDocument();
      });
    });

    it('displays fallback error message when registration fails without error message', async () => {
      const user = userEvent.setup();

      mockedApi.post.mockRejectedValueOnce({});

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Open modal and switch to Register
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(
        () => {
          expect(screen.getByRole('button', { name: 'Register' })).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      const registerTab = screen.getByRole('button', { name: 'Register' });
      await user.click(registerTab);

      await waitFor(
        () => {
          expect(screen.getByLabelText('Email')).toBeInTheDocument();
        },
        { timeout: 3000 }
      );

      // Fill and submit
      await user.type(screen.getByLabelText('Email'), 'new@example.com');
      await user.type(screen.getByLabelText('Password (min 8 characters)'), 'password123');

      const createAccountButton = screen.getByRole('button', { name: 'Create Account' });
      await user.click(createAccountButton);

      await waitFor(() => {
        expect(screen.getByText('Registrazione non riuscita.')).toBeInTheDocument();
      });
    });
  });

  describe('Logout', () => {
    it('logs out the user and clears the session state', async () => {
      const user = userEvent.setup();

      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'Admin'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      mockedApi.post.mockResolvedValueOnce({});

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument();
      });

      const logoutButton = screen.getByRole('button', { name: 'Logout' });
      await user.click(logoutButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/logout');
      });

      // After logout, should show Get Started button again
      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });
    });

    it('handles logout error gracefully', async () => {
      const user = userEvent.setup();
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});

      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'Admin'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      mockedApi.post.mockRejectedValueOnce(new Error('Logout failed'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByRole('button', { name: 'Logout' })).toBeInTheDocument();
      });

      const logoutButton = screen.getByRole('button', { name: 'Logout' });
      await user.click(logoutButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/logout');
      });

      // User should still be logged out even if API call fails
      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      consoleErrorSpy.mockRestore();
    });
  });

  describe('Features Section', () => {
    it('renders How It Works section with three steps', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('How It Works')).toBeInTheDocument();
      });

      expect(screen.getByText('1. Upload')).toBeInTheDocument();
      expect(screen.getByText('2. Ask')).toBeInTheDocument();
      expect(screen.getByText('3. Play')).toBeInTheDocument();
    });

    it('renders Key Features section', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Semantic Search')).toBeInTheDocument();
      });

      expect(screen.getByText('Multi-Game Support')).toBeInTheDocument();
      expect(screen.getByText('Source Citations')).toBeInTheDocument();
      // RuleSpec Editor appears in both Key Features and Footer
      const ruleSpecElements = screen.getAllByText('RuleSpec Editor');
      expect(ruleSpecElements.length).toBeGreaterThan(0);
    });
  });

  describe('CTA Section', () => {
    it('renders CTA section with call to action', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Ready to Stop Arguing About Rules?')).toBeInTheDocument();
      });

      expect(
        screen.getByText('Join board game enthusiasts using AI to understand rules better')
      ).toBeInTheDocument();
    });

    it('CTA button redirects to chat when authenticated', async () => {
      const user = userEvent.setup();

      mockedApi.get.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Start Chatting')).toBeInTheDocument();
      });

      const ctaButton = screen.getByText('Start Chatting');
      await user.click(ctaButton);

      expect(routerMock.push).toHaveBeenCalledWith('/chat');
    });
  });

  describe('Footer', () => {
    it('renders footer with links', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('© 2025 MeepleAI. Open source project.')).toBeInTheDocument();
      });

      // Check Product links
      expect(screen.getByRole('link', { name: 'Chat' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Upload PDF' })).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'RuleSpec Editor' })).toBeInTheDocument();
    });

    it('renders demo accounts in footer', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Demo Accounts')).toBeInTheDocument();
      });

      expect(screen.getByText('admin@meepleai.dev')).toBeInTheDocument();
      expect(screen.getByText('editor@meepleai.dev')).toBeInTheDocument();
      // user@meepleai.dev appears in hero section too
      const userEmails = screen.getAllByText('user@meepleai.dev');
      expect(userEmails.length).toBeGreaterThan(0);
    });
  });

  describe('Error Handling', () => {
    it('sets authUser to null when loadCurrentUser catches an error', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockedApi.get.mockRejectedValueOnce(new Error('Network failure'));

      render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalledWith('/api/v1/auth/me');
      });

      // Should show Get Started button (not authenticated)
      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      consoleErrorSpy.mockRestore();
    });
  });
});
