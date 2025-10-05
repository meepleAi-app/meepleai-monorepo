import { act, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import Home from '../index';
import { api } from '../../lib/api';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn()
  }
}));

const mockedApi = api as jest.Mocked<typeof api>;

describe('Home page', () => {
  const originalFetch = global.fetch;
  const originalAlert = window.alert;
  let fetchMock: jest.Mock;
  let alertMock: jest.Mock;

  beforeEach(() => {
    fetchMock = jest.fn().mockImplementation(async (input: RequestInfo | URL) => {
      if (typeof input === 'string' && input.includes('/api/health')) {
        return {
          ok: true,
          status: 200,
          json: async () => ({ ok: true })
        } as unknown as Response;
      }

      return {
        ok: true,
        status: 200,
        json: async () => ({})
      } as unknown as Response;
    });
    alertMock = jest.fn();

    global.fetch = fetchMock as unknown as typeof fetch;
    window.alert = alertMock as typeof window.alert;

    mockedApi.get.mockReset();
    mockedApi.post.mockReset();
    mockedApi.put.mockReset();
    mockedApi.get.mockResolvedValue(null);
  });

  afterEach(() => {
    global.fetch = originalFetch;
    window.alert = originalAlert;
    jest.clearAllMocks();
  });

  it("shows an error message when an unauthenticated user tries to ask a question", async () => {
    render(<Home />);

    await waitFor(() => {
      expect(mockedApi.get).toHaveBeenCalledWith('/auth/me');
    });

    const askButton = await screen.findByRole('button', { name: 'Chiedi' });
    expect(askButton).toBeDisabled();

    const reactPropsKey = Object.keys(askButton).find((key) => key.startsWith('__reactProps$'));
    expect(reactPropsKey).toBeDefined();

    act(() => {
      const props = (askButton as unknown as Record<string, { onClick?: (event: unknown) => void }>)[
        reactPropsKey as string
      ];
      props.onClick?.({ preventDefault: () => undefined });
    });

    await waitFor(() => {
      expect(
        screen.getByText("Devi effettuare l'accesso per porre domande.")
      ).toBeInTheDocument();
    });

    expect(mockedApi.post).not.toHaveBeenCalledWith('/agents/qa', expect.anything());
  });

  it('resets the registration form and shows a positive status after successful registration', async () => {
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

    const registerFormHeading = await screen.findByRole('heading', { name: 'Registrazione' });
    const registerForm = registerFormHeading.closest('form');
    expect(registerForm).not.toBeNull();
    const registerScope = within(registerForm as HTMLFormElement);

    await user.type(registerScope.getByLabelText('Email'), 'new@example.com');
    await user.type(registerScope.getByLabelText('Password (min 8 caratteri)'), 'password123');
    await user.type(registerScope.getByLabelText('Nome visualizzato'), 'New User');
    await user.selectOptions(registerScope.getByLabelText('Ruolo'), 'Editor');

    await user.click(registerScope.getByRole('button', { name: 'Crea account' }));

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/register', {
        email: 'new@example.com',
        password: 'password123',
        displayName: 'New User',
        role: 'Editor'
      });
    });

    expect(registerScope.getByLabelText('Email')).toHaveValue('');
    expect(registerScope.getByLabelText('Password (min 8 caratteri)')).toHaveValue('');
    expect(registerScope.getByLabelText('Nome visualizzato')).toHaveValue('');
    expect(registerScope.getByLabelText('Ruolo')).toHaveValue('User');

    await waitFor(() => {
      expect(screen.getByText('Registrazione completata. Sei connesso!')).toBeInTheDocument();
    });
    expect(screen.getByText('new@example.com')).toBeInTheDocument();
  });

  it('logs in the user, updates the session state, and enables the ask button', async () => {
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

    const loginFormHeading = await screen.findByRole('heading', { name: 'Accesso' });
    const loginForm = loginFormHeading.closest('form');
    expect(loginForm).not.toBeNull();
    const loginScope = within(loginForm as HTMLFormElement);

    await user.type(loginScope.getByLabelText('Email'), 'user@example.com');
    await user.type(loginScope.getByLabelText('Password'), 'super-secret');

    await user.click(loginScope.getByRole('button', { name: 'Entra' }));

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/login', {
        email: 'user@example.com',
        password: 'super-secret'
      });
    });

    await waitFor(() => {
      expect(screen.getByText('Accesso eseguito.')).toBeInTheDocument();
    });

    const askButton = await screen.findByRole('button', { name: 'Chiedi' });
    await waitFor(() => {
      expect(askButton).toBeEnabled();
    });
    expect(screen.getByText('user@example.com')).toBeInTheDocument();
  });

  it('displays error messages when register, login, or ask operations fail', async () => {
    const user = userEvent.setup();

    render(<Home />);

    const registerFormHeading = await screen.findByRole('heading', { name: 'Registrazione' });
    const registerForm = registerFormHeading.closest('form');
    expect(registerForm).not.toBeNull();
    const registerScope = within(registerForm as HTMLFormElement);

    await user.type(registerScope.getByLabelText('Email'), 'new@example.com');
    await user.type(registerScope.getByLabelText('Password (min 8 caratteri)'), 'password123');
    await user.type(registerScope.getByLabelText('Nome visualizzato'), 'New User');

    mockedApi.post.mockRejectedValueOnce(new Error('Registrazione fallita'));

    await user.click(registerScope.getByRole('button', { name: 'Crea account' }));

    await waitFor(() => {
      expect(screen.getByText('Registrazione fallita')).toBeInTheDocument();
    });

    const loginFormHeading = await screen.findByRole('heading', { name: 'Accesso' });
    const loginForm = loginFormHeading.closest('form');
    expect(loginForm).not.toBeNull();
    const loginScope = within(loginForm as HTMLFormElement);

    await user.type(loginScope.getByLabelText('Email'), 'user@example.com');
    await user.type(loginScope.getByLabelText('Password'), 'wrong-password');

    mockedApi.post.mockRejectedValueOnce(new Error('Credenziali errate'));

    await user.click(loginScope.getByRole('button', { name: 'Entra' }));

    await waitFor(() => {
      expect(screen.getByText('Credenziali errate')).toBeInTheDocument();
    });

    mockedApi.post.mockResolvedValueOnce({
      user: {
        id: 'user-1',
        email: 'user@example.com',
        role: 'User'
      },
      expiresAt: '2024-12-31T00:00:00.000Z'
    });

    await user.clear(loginScope.getByLabelText('Password'));
    await user.type(loginScope.getByLabelText('Password'), 'super-secret');

    await user.click(loginScope.getByRole('button', { name: 'Entra' }));

    await waitFor(() => {
      expect(screen.getByText('Accesso eseguito.')).toBeInTheDocument();
    });

    mockedApi.post.mockRejectedValueOnce(new Error('Agente non raggiungibile'));

    const askButton = await screen.findByRole('button', { name: 'Chiedi' });
    await waitFor(() => {
      expect(askButton).toBeEnabled();
    });

    await user.click(askButton);

    await waitFor(() => {
      expect(
        screen.getByText("Impossibile interrogare l'agente. Controlla le credenziali.")
      ).toBeInTheDocument();
    });
  });

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
      expect(screen.getByText('user@example.com')).toBeInTheDocument();
    });

    const logoutButton = screen.getByRole('button', { name: 'Esci' });
    await user.click(logoutButton);

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/logout');
    });

    await waitFor(() => {
      expect(screen.getByText('Sessione terminata.')).toBeInTheDocument();
      expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
    });

    const askButton = await screen.findByRole('button', { name: 'Chiedi' });
    expect(askButton).toBeDisabled();
  });
});
