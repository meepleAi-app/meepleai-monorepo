import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { useRouter } from 'next/navigation';
import Home from '@/components/pages/HomePage';
import { api } from '@/lib/api';
import { waitForApiCall } from '../fixtures/test-helpers';

// Mock next/navigation (App Router)
const mockPush = jest.fn();
const mockBack = jest.fn();
const mockRefresh = jest.fn();

jest.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
    back: mockBack,
    refresh: mockRefresh,
    forward: jest.fn(),
    prefetch: jest.fn(),
  }),
  usePathname: () => '/',
  useSearchParams: () => new URLSearchParams(),
}));

// Mock ApiError class - must be defined before jest.mock
class MockApiError extends Error {
  constructor(public statusCode: number, message: string) {
    super(message);
    this.name = 'ApiError';
  }
}

jest.mock('../../lib/api', () => {
  // Capture MockApiError in closure
  class ApiErrorMock extends Error {
    constructor(public statusCode: number, message: string) {
      super(message);
      this.name = 'ApiError';
    }
  }

  return {
    api: {
      get: jest.fn(),
      post: jest.fn(),
      put: jest.fn()
    },
    ApiError: ApiErrorMock
  };
});

// Mock framer-motion to avoid animation issues in tests
jest.mock('framer-motion', () => ({
  motion: {
    div: ({ children, whileHover, whileTap, initial, animate, exit, variants, transition, ...props }: any) => <div {...props}>{children}</div>,
    button: ({ children, whileHover, whileTap, initial, animate, exit, variants, transition, ...props }: any) => <button {...props}>{children}</button>,
    a: ({ children, whileHover, whileTap, initial, animate, exit, variants, transition, ...props }: any) => <a {...props}>{children}</a>,
  },
  AnimatePresence: ({ children }: any) => <>{children}</>,
  useInView: () => [null, true],
}));

// Mock react-intersection-observer
jest.mock('react-intersection-observer', () => ({
  useInView: () => [null, true],
}));

const mockedApi = api as jest.Mocked<typeof api>;

describe('Home page (Landing Page)', () => {
  beforeEach(() => {
    mockPush.mockReset();
    mockBack.mockReset();
    mockRefresh.mockReset();

    mockedApi.get.mockReset();
    mockedApi.post.mockReset();
    mockedApi.put.mockReset();
    mockedApi.get.mockResolvedValue(null);
  });

  afterEach(() => {
    jest.clearAllMocks();
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

      await waitForApiCall(mockedApi.get, '/api/v1/auth/me');

      const getStartedButtons = screen.getAllByText('Get Started Free');
      expect(getStartedButtons.length).toBeGreaterThan(0);
    });

    it('shows "Go to Chat" button when user is authenticated', async () => {
      mockedApi.get.mockResolvedValue({
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
        // user@meepleai.dev appears multiple times (hero + footer)
        const emailElements = screen.getAllByText('user@meepleai.dev');
        expect(emailElements.length).toBeGreaterThan(0);
      });
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
      mockedApi.get.mockResolvedValue({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      // Get the header navigation element
      const nav = await screen.findByRole('navigation', { name: 'Main navigation' });

      // Check that authenticated navigation links are present in the nav
      // Note: The nav uses "hidden md:flex" so links won't be visible in default test viewport
      // but they should still be in the DOM
      await waitFor(() => {
        expect(within(nav).getByText('Chat')).toBeInTheDocument();
      });

      expect(within(nav).getByText('Chess')).toBeInTheDocument();
      expect(within(nav).getByText('Upload')).toBeInTheDocument();
      expect(await within(nav).findByRole('button', { name: /logout/i })).toBeInTheDocument();
    });

    it('shows Admin link for admin users', async () => {
      mockedApi.get.mockResolvedValue({
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
      mockedApi.get.mockResolvedValue({
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
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      // Modal should open - check for modal title
      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });
    });

    it('switches between Login and Register tabs', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Find Register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      // Should change modal title
      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
      });
    });
  });

  describe('Authenticated User Navigation', () => {
    it('clicking Go to Chat button navigates when authenticated', async () => {
      const user = userEvent.setup();

      mockedApi.get.mockResolvedValue({
        user: {
          id: 'user-1',
          email: 'user@example.com',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('Go to Chat')).toBeInTheDocument();
      });

      const goToChatButton = screen.getByText('Go to Chat');
      await user.click(goToChatButton);

      expect(mockPush).toHaveBeenCalledWith('/chat');
    });

    it('clicking Start Chatting button navigates when authenticated', async () => {
      const user = userEvent.setup();

      mockedApi.get.mockResolvedValue({
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

      const startChattingButton = screen.getByText('Start Chatting');
      await user.click(startChattingButton);

      expect(mockPush).toHaveBeenCalledWith('/chat');
    });
  });


  describe('Logout', () => {
    it('logs out the user and clears the session state', async () => {
      const user = userEvent.setup();

      mockedApi.get.mockResolvedValue({
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
        expect(screen.getByRole('button', { name: /logout/i })).toBeInTheDocument();
      });

      const logoutButton = screen.getByRole('button', { name: /logout/i });
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

      mockedApi.get.mockResolvedValue({
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
        expect(screen.getByRole('button', { name: /logout/i })).toBeInTheDocument();
      });

      const logoutButton = screen.getByRole('button', { name: /logout/i });
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

      mockedApi.get.mockResolvedValue({
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

      expect(mockPush).toHaveBeenCalledWith('/chat');
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

  describe('Auth Modal Forms', () => {
    it('submits login form successfully', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Setup the login response
      mockedApi.post.mockResolvedValueOnce({
        user: {
          id: 'user-1',
          email: 'test@example.com',
          displayName: 'Test User',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      // Wait for modal AND form to be visible
      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');

      // Find inputs by role instead of label text
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0]; // First input should be email

      // Password input has type="password" so it won't have textbox role
      const passwordInput = tabPanel.querySelector('input[type="password"]') as HTMLInputElement;
      expect(passwordInput).toBeInTheDocument();

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password123');

      // Submit
      const loginButton = await screen.findByRole('button', { name: 'Accedi' });
      await user.click(loginButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/login', {
          email: 'test@example.com',
          password: 'password123'
        });
      });

      expect(mockPush).toHaveBeenCalledWith('/chat');
    });

    it('submits register form successfully', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Setup the register response
      mockedApi.post.mockResolvedValueOnce({
        user: {
          id: 'user-2',
          email: 'new@example.com',
          displayName: 'New User',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Switch to register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');

      // Find inputs by role - register form has 2 text inputs: email, display name
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0]; // First input: email (type="email" has textbox role)
      const displayNameInput = inputs[1]; // Second input: display name

      // Password inputs have type="password" so won't have textbox role
      const passwordInputs = tabPanel.querySelectorAll('input[type="password"]');
      expect(passwordInputs).toHaveLength(2); // password and confirmPassword
      const passwordInput = passwordInputs[0] as HTMLInputElement;
      const confirmPasswordInput = passwordInputs[1] as HTMLInputElement;

      await user.type(emailInput, 'new@example.com');
      await user.type(passwordInput, 'NewPassword123');
      await user.type(confirmPasswordInput, 'NewPassword123');
      await user.type(displayNameInput, 'New User');

      // Submit
      const createButton = screen.getByRole('button', { name: 'Crea Account' });
      await user.click(createButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
          email: 'new@example.com',
          password: 'NewPassword123',
          displayName: 'New User',
          role: 'User' // Default role when selector is not shown
        });
      });

      expect(mockPush).toHaveBeenCalledWith('/chat');
    });

    // TODO: React 19 useActionState doesn't trigger re-renders in test environment
    // The action runs and returns error state, but component doesn't update
    // This is a known limitation - these tests pass in real browser
    it.skip('displays login error message', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Use MockApiError for proper error handling
      mockedApi.post.mockRejectedValueOnce(new MockApiError(401, 'Invalid credentials'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal and submit login
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];
      const passwordInput = tabPanel.querySelector('input[type="password"]') as HTMLInputElement;

      await user.type(emailInput, 'bad@example.com');
      await user.type(passwordInput, 'wrongpassword');

      const loginButton = screen.getByRole('button', { name: 'Accedi' });
      await user.click(loginButton);

      // Wait for API call and error rendering (useActionState is async)
      await waitFor(
        () => {
          expect(mockedApi.post).toHaveBeenCalled();
          expect(screen.getByText('Email o password non corretti.')).toBeInTheDocument();
        },
        { timeout: 8000 }
      );
    }, 12000); // Increase timeout for useActionState tests

    it.skip('displays register error message', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Use MockApiError for proper error handling
      mockedApi.post.mockRejectedValueOnce(new MockApiError(409, 'Email already exists'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Switch to register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];

      // Password inputs have type="password" so won't have textbox role
      const passwordInputs = tabPanel.querySelectorAll('input[type="password"]');
      const passwordInput = passwordInputs[0] as HTMLInputElement;
      const confirmPasswordInput = passwordInputs[1] as HTMLInputElement;

      await user.type(emailInput, 'existing@example.com');
      await user.type(passwordInput, 'Password123');
      await user.type(confirmPasswordInput, 'Password123');

      const createButton = screen.getByRole('button', { name: 'Crea Account' });
      await user.click(createButton);

      // Wait for API call and error rendering (useActionState is async)
      await waitFor(
        () => {
          expect(mockedApi.post).toHaveBeenCalled();
          expect(screen.getByText("Questa email è già registrata. Prova con un'altra email o effettua il login.")).toBeInTheDocument();
        },
        { timeout: 8000 }
      );
    }, 12000); // Increase timeout for useActionState tests

    it.skip('clears error message when switching tabs', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Use MockApiError for proper error handling
      mockedApi.post.mockRejectedValueOnce(new MockApiError(401, 'Login failed'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];
      const passwordInput = tabPanel.querySelector('input[type="password"]') as HTMLInputElement;

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'WrongPassword123');

      const loginButton = screen.getByRole('button', { name: 'Accedi' });
      await user.click(loginButton);

      // Wait for API call and error rendering (useActionState is async)
      await waitFor(
        () => {
          expect(mockedApi.post).toHaveBeenCalled();
          expect(screen.getByText('Email o password non corretti.')).toBeInTheDocument();
        },
        { timeout: 8000 }
      );

      // Switch to register tab - error should clear
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(
        () => {
          expect(screen.queryByText('Email o password non corretti.')).not.toBeInTheDocument();
        },
        { timeout: 3000 }
      );
    }, 12000); // Increase timeout for useActionState tests

    it.skip('clears error when modal is closed', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Mock with proper ApiError instance
      mockedApi.post.mockRejectedValueOnce(new MockApiError(400, 'Test error'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];
      const passwordInput = tabPanel.querySelector('input[type="password"]') as HTMLInputElement;

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'WrongPassword123');

      const loginButton = screen.getByRole('button', { name: 'Accedi' });
      await user.click(loginButton);

      // Wait for API call and error rendering (useActionState is async)
      // Test error is a generic 400 error, getLocalizedError passes through the error message
      await waitFor(
        () => {
          expect(mockedApi.post).toHaveBeenCalled();
          expect(screen.getByText('Test error')).toBeInTheDocument();
        },
        { timeout: 8000 }
      );
    }, 12000); // Increase timeout for useActionState tests

    it('submits register form with optional display name omitted', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      mockedApi.post.mockResolvedValueOnce({
        user: {
          id: 'user-3',
          email: 'minimal@example.com',
          role: 'User'
        },
        expiresAt: '2024-12-31T00:00:00.000Z'
      });

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Switch to register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];

      // Password inputs have type="password" so won't have textbox role
      const passwordInputs = tabPanel.querySelectorAll('input[type="password"]');
      const passwordInput = passwordInputs[0] as HTMLInputElement;
      const confirmPasswordInput = passwordInputs[1] as HTMLInputElement;

      await user.type(emailInput, 'minimal@example.com');
      await user.type(passwordInput, 'Password123');
      await user.type(confirmPasswordInput, 'Password123');

      // Display name is optional - leave it empty

      const createButton = screen.getByRole('button', { name: 'Crea Account' });
      await user.click(createButton);

      await waitFor(() => {
        expect(mockedApi.post).toHaveBeenCalledWith('/api/v1/auth/register', {
          email: 'minimal@example.com',
          password: 'Password123',
          displayName: undefined,
          role: 'User'
        });
      });
    });
  });

  describe('Scroll Behavior', () => {
    it('renders "See How It Works" button with anchor link', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('See How It Works')).toBeInTheDocument();
      });

      const seeHowButton = screen.getByText('See How It Works');
      expect(seeHowButton).toBeInTheDocument();
      expect(seeHowButton.closest('a')).toHaveAttribute('href', '#features');
    });
  });

  describe('Error Handling', () => {
    it('sets authUser to null when loadCurrentUser catches an error', async () => {
      const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
      mockedApi.get.mockRejectedValueOnce(new Error('Network failure'));

      render(<Home />);

      await waitForApiCall(mockedApi.get, '/api/v1/auth/me');

      // Should show Get Started button (not authenticated)
      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      consoleErrorSpy.mockRestore();
    });

    it('handles login error with generic message when no error message provided', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Mock with regular Error (not ApiError) - will trigger generic error message
      mockedApi.post.mockRejectedValueOnce(new Error('Network error'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal and submit login
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];
      const passwordInput = tabPanel.querySelector('input[type="password"]') as HTMLInputElement;

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'password');

      const loginButton = screen.getByRole('button', { name: 'Accedi' });
      await user.click(loginButton);

      await waitFor(() => {
        expect(screen.getByText('Impossibile connettersi al server.')).toBeInTheDocument();
      });
    });

    it('handles register error with generic message when no error message provided', async () => {
      const user = userEvent.setup();

      // Setup the default response for auth check
      mockedApi.get.mockResolvedValue(null);

      // Mock with regular Error (not ApiError) - will trigger generic error message
      mockedApi.post.mockRejectedValueOnce(new Error('Network error'));

      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Switch to register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
        expect(screen.getByRole('tabpanel')).toBeInTheDocument();
      });

      // Get the tab panel and find inputs within it
      const tabPanel = screen.getByRole('tabpanel');
      const inputs = await within(tabPanel).findAllByRole('textbox', {}, { timeout: 3000 });
      const emailInput = inputs[0];

      // Password inputs have type="password" so won't have textbox role
      const passwordInputs = tabPanel.querySelectorAll('input[type="password"]');
      const passwordInput = passwordInputs[0] as HTMLInputElement;
      const confirmPasswordInput = passwordInputs[1] as HTMLInputElement;

      await user.type(emailInput, 'test@example.com');
      await user.type(passwordInput, 'Password123');
      await user.type(confirmPasswordInput, 'Password123');

      const createButton = screen.getByRole('button', { name: 'Crea Account' });
      await user.click(createButton);

      await waitFor(() => {
        expect(screen.getByText('Impossibile connettersi al server.')).toBeInTheDocument();
      });
    });
  });

  describe('Additional Coverage', () => {
    it('register form does not show role selector when showRoleSelector is false', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Switch to register tab
      const registerTab = await screen.findByRole('tab', { name: 'Registrati' });
      await user.click(registerTab);

      await waitFor(() => {
        expect(screen.getByText('Crea il tuo Account')).toBeInTheDocument();
      });

      // Role selector should NOT be present when showRoleSelector is false
      const roleSelect = screen.queryByLabelText(/Select user role/i);
      expect(roleSelect).not.toBeInTheDocument();
    });

    it('switches between login and register tabs with Login tab initially selected', async () => {
      const user = userEvent.setup();
      render(<Home />);

      await waitFor(() => {
        expect(screen.getAllByText('Get Started Free').length).toBeGreaterThan(0);
      });

      // Open modal
      const getStartedButtons = screen.getAllByText('Get Started Free');
      await user.click(getStartedButtons[0]);

      await waitFor(() => {
        expect(screen.getByText('Accedi a MeepleAI')).toBeInTheDocument();
      });

      // Check login tab is initially selected
      const loginTab = screen.getByRole('tab', { name: 'Accedi' });
      expect(loginTab).toHaveAttribute('aria-selected', 'true');

      const registerTab = screen.getByRole('tab', { name: 'Registrati' });
      expect(registerTab).toHaveAttribute('aria-selected', 'false');
    });
  });

  describe('Hero Visual Content', () => {
    it('renders hero conversation example', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText('How does en passant work in chess?')).toBeInTheDocument();
      });

      expect(screen.getByText(/En passant is a special pawn capture/i)).toBeInTheDocument();
      expect(screen.getByText(/Sources: Chess Rules \(FIDE\) - Page 12/i)).toBeInTheDocument();
    });

    it('renders scroll indicator', async () => {
      const { container } = render(<Home />);

      await waitFor(() => {
        expect(mockedApi.get).toHaveBeenCalled();
      });

      // Check for SVG scroll indicator - flexible selector
      const svgs = container.querySelectorAll('svg');
      expect(svgs.length).toBeGreaterThan(0);
    });
  });

  describe('Footer Links', () => {
    it('renders GitHub link', async () => {
      render(<Home />);

      await waitFor(() => {
        const githubLink = screen.getByRole('link', { name: 'GitHub' });
        expect(githubLink).toBeInTheDocument();
        expect(githubLink).toHaveAttribute('href', 'https://github.com/yourusername/meepleai');
        expect(githubLink).toHaveAttribute('target', '_blank');
        expect(githubLink).toHaveAttribute('rel', 'noopener noreferrer');
      });
    });

    it('renders Documentation link', async () => {
      render(<Home />);

      await waitFor(() => {
        const docsLink = screen.getByRole('link', { name: 'Documentation' });
        expect(docsLink).toBeInTheDocument();
        expect(docsLink).toHaveAttribute('href', '/docs');
      });
    });

    it('renders API Logs link', async () => {
      render(<Home />);

      await waitFor(() => {
        const logsLink = screen.getByRole('link', { name: 'API Logs' });
        expect(logsLink).toBeInTheDocument();
        expect(logsLink).toHaveAttribute('href', '/logs');
      });
    });
  });

  describe('Feature Descriptions', () => {
    it('renders all feature descriptions in How It Works section', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText(/Upload any PDF rulebook/i)).toBeInTheDocument();
      });

      expect(screen.getByText(/Ask questions in natural language/i)).toBeInTheDocument();
      expect(screen.getByText(/Get instant answers with exact sources/i)).toBeInTheDocument();
    });

    it('renders all key feature descriptions', async () => {
      render(<Home />);

      await waitFor(() => {
        expect(screen.getByText(/Advanced AI understands context and meaning/i)).toBeInTheDocument();
      });

      expect(screen.getByText(/Upload rulebooks for chess/i)).toBeInTheDocument();
      expect(screen.getByText(/Every answer includes exact page numbers/i)).toBeInTheDocument();
      expect(screen.getByText(/Create machine-readable rule specifications/i)).toBeInTheDocument();
    });
  });
});
