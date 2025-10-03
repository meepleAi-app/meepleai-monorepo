import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Home from './index';
import { api } from '../lib/api';

jest.mock('../lib/api');
const mockedApi = api as jest.Mocked<typeof api>;

describe('Home page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders the main heading', () => {
    mockedApi.get.mockResolvedValue(null);
    render(<Home />);

    expect(screen.getByText('MeepleAI')).toBeInTheDocument();
  });

  it('renders navigation links', () => {
    mockedApi.get.mockResolvedValue(null);
    render(<Home />);

    expect(screen.getByRole('link', { name: /chat/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /upload pdf/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /view logs/i })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /editor rulespec/i })).toBeInTheDocument();
  });

  it('renders registration and login forms', () => {
    mockedApi.get.mockResolvedValue(null);
    render(<Home />);

    expect(screen.getByText('Registrazione')).toBeInTheDocument();
    expect(screen.getByText('Accesso')).toBeInTheDocument();
  });

  it('loads current user on mount', async () => {
    const mockUser = {
      user: {
        id: '1',
        tenantId: 'test-tenant',
        email: 'test@example.com',
        displayName: 'Test User',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    mockedApi.get.mockResolvedValue(mockUser);
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('test@example.com')).toBeInTheDocument();
    });
  });

  it('shows "no user" message when not logged in', async () => {
    mockedApi.get.mockResolvedValue(null);
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });
  });

  it('disables ask button when not logged in', async () => {
    mockedApi.get.mockResolvedValue(null);
    render(<Home />);

    // Wait for component to fully load
    await waitFor(() => {
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });

    const askButton = screen.getByRole('button', { name: /chiedi/i });
    expect(askButton).toBeDisabled();
  });

  it('can ask question when logged in', async () => {
    const mockUser = {
      user: {
        id: '1',
        tenantId: 'test-tenant',
        email: 'test@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    mockedApi.get.mockResolvedValue(mockUser);
    mockedApi.post.mockResolvedValue({ answer: 'Test answer' });

    const user = userEvent.setup();
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('test@example.com')).toBeInTheDocument();
    });

    const askButton = screen.getByRole('button', { name: /chiedi/i });
    await user.click(askButton);

    await waitFor(() => {
      expect(screen.getByText('Test answer')).toBeInTheDocument();
    });
  });

  it('handles registration form submission', async () => {
    const mockResponse = {
      user: {
        id: '2',
        tenantId: 'new-tenant',
        email: 'new@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    mockedApi.get.mockResolvedValue(null);
    mockedApi.post.mockResolvedValue(mockResponse);

    const user = userEvent.setup();
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });

    const emailInput = screen.getAllByLabelText(/email/i)[0];
    const passwordInput = screen.getByLabelText(/password \(min 8 caratteri\)/i);
    const submitButton = screen.getByRole('button', { name: /crea account/i });

    await user.type(emailInput, 'new@example.com');
    await user.type(passwordInput, 'password123');
    await user.click(submitButton);

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/register', expect.objectContaining({
        email: 'new@example.com',
        password: 'password123',
      }));
    });
  });

  it('handles login form submission', async () => {
    const mockResponse = {
      user: {
        id: '3',
        tenantId: 'test-tenant',
        email: 'login@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    mockedApi.get.mockResolvedValue(null);
    mockedApi.post.mockResolvedValue(mockResponse);

    const user = userEvent.setup();
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });

    const emailInput = screen.getAllByLabelText(/email/i)[1];
    const passwordInputs = screen.getAllByLabelText(/password/i);
    const loginPasswordInput = passwordInputs[passwordInputs.length - 1];
    const loginButton = screen.getByRole('button', { name: /entra/i });

    await user.type(emailInput, 'login@example.com');
    await user.type(loginPasswordInput, 'password123');
    await user.click(loginButton);

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/login', expect.objectContaining({
        email: 'login@example.com',
        password: 'password123',
      }));
    });
  });

  it('handles logout', async () => {
    const mockUser = {
      user: {
        id: '4',
        tenantId: 'test-tenant',
        email: 'logout@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    mockedApi.get.mockResolvedValue(mockUser);
    mockedApi.post.mockResolvedValue({});

    const user = userEvent.setup();
    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('logout@example.com')).toBeInTheDocument();
    });

    const logoutButton = screen.getByRole('button', { name: /esci/i });
    await user.click(logoutButton);

    await waitFor(() => {
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });
  });
});
