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

  beforeEach(() => {
    mockedApi.get.mockReset();
    mockedApi.post.mockReset();
    mockedApi.put.mockReset();
    jest.clearAllMocks();
    global.fetch = jest
      .fn()
      .mockResolvedValue({ ok: true, status: 200, json: async () => ({}) }) as unknown as typeof fetch;
    window.alert = jest.fn() as unknown as typeof window.alert;
  });

  afterEach(() => {
    mockedApi.get.mockReset();
    mockedApi.post.mockReset();
    mockedApi.put.mockReset();
    jest.clearAllMocks();
    global.fetch = originalFetch;
    window.alert = originalAlert;
  });

  it('loads current user and blocks QA requests when unauthenticated', async () => {
    mockedApi.get.mockResolvedValueOnce(null);

    render(<Home />);

    await waitFor(() => {
      expect(mockedApi.get).toHaveBeenCalledWith('/auth/me');
    });

    const askButton = screen.getByRole('button', { name: 'Chiedi' });
    expect(askButton).toBeDisabled();
    expect(mockedApi.post).not.toHaveBeenCalled();
  });

  it('logs in successfully, resets form fields, and updates the status message', async () => {
    mockedApi.get.mockResolvedValueOnce(null);
    const user = userEvent.setup();

    render(<Home />);

    const loginHeading = screen.getByRole('heading', { name: 'Accesso' });
    const loginForm = loginHeading.closest('form');
    expect(loginForm).not.toBeNull();
    const loginScope = within(loginForm as HTMLFormElement);

    await user.type(loginScope.getByLabelText('Email'), 'user@example.com');
    await user.type(loginScope.getByLabelText('Password'), 'super-secret');

    mockedApi.post.mockResolvedValueOnce({
      user: {
        id: '123',
        email: 'user@example.com',
        role: 'User'
      },
      expiresAt: 'tomorrow'
    });

    await user.click(loginScope.getByRole('button', { name: 'Entra' }));

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/login', {
        email: 'user@example.com',
        password: 'super-secret'
      });
    });

    expect(loginScope.getByLabelText('Email')).toHaveValue('');
    expect(loginScope.getByLabelText('Password')).toHaveValue('');
    expect(screen.getByText('Accesso eseguito.')).toBeInTheDocument();
  });

  it('shows registration and login errors from the API', async () => {
    mockedApi.get.mockResolvedValueOnce(null);
    const user = userEvent.setup();

    render(<Home />);

    const registerHeading = screen.getByRole('heading', { name: 'Registrazione' });
    const registerForm = registerHeading.closest('form');
    expect(registerForm).not.toBeNull();
    const registerScope = within(registerForm as HTMLFormElement);

    await user.type(registerScope.getByLabelText('Email'), 'new@example.com');
    await user.type(registerScope.getByLabelText('Password (min 8 caratteri)'), 'password');
    await user.type(registerScope.getByLabelText('Nome visualizzato'), 'New User');

    mockedApi.post.mockRejectedValueOnce(new Error('Registrazione fallita')); 

    await user.click(registerScope.getByRole('button', { name: 'Crea account' }));

    await waitFor(() => {
      expect(screen.getByText('Registrazione fallita')).toBeInTheDocument();
    });

    const loginHeading = screen.getByRole('heading', { name: 'Accesso' });
    const loginForm = loginHeading.closest('form');
    expect(loginForm).not.toBeNull();
    const loginScope = within(loginForm as HTMLFormElement);

    await user.clear(loginScope.getByLabelText('Email'));
    await user.type(loginScope.getByLabelText('Email'), 'user@example.com');
    await user.clear(loginScope.getByLabelText('Password'));
    await user.type(loginScope.getByLabelText('Password'), 'badpass');

    mockedApi.post.mockRejectedValueOnce(new Error('Accesso non autorizzato'));

    await user.click(loginScope.getByRole('button', { name: 'Entra' }));

    await waitFor(() => {
      expect(screen.getByText('Accesso non autorizzato')).toBeInTheDocument();
    });
  });

  it('logs out the user and notifies the session end', async () => {
    mockedApi.get.mockResolvedValueOnce({
      user: {
        id: 'abc',
        email: 'logged@example.com',
        role: 'Admin'
      },
      expiresAt: 'later'
    });
    const user = userEvent.setup();

    mockedApi.post.mockResolvedValueOnce({});

    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('logged@example.com')).toBeInTheDocument();
    });

    await user.click(screen.getByRole('button', { name: 'Esci' }));

    await waitFor(() => {
      expect(mockedApi.post).toHaveBeenCalledWith('/auth/logout');
    });

    expect(screen.getByText('Sessione terminata.')).toBeInTheDocument();
    expect(screen.getByText('Nessun utente connesso.')).toBeInTheDocument();
  });

  it('requests QA successfully, showing the answer and handling loading state', async () => {
    mockedApi.get.mockResolvedValueOnce({
      user: {
        id: 'qa-user',
        email: 'qa@example.com',
        role: 'User'
      },
      expiresAt: 'later'
    });
    const user = userEvent.setup();

    let resolveQa: ((value: { answer: string }) => void) | undefined;

    mockedApi.post.mockImplementation(async (path: string) => {
      if (path === '/auth/logout') {
        return {} as any;
      }
      if (path === '/agents/qa') {
        return await new Promise<{ answer: string }>((resolve) => {
          resolveQa = resolve;
        });
      }
      return {
        user: {
          id: 'qa-user',
          email: 'qa@example.com',
          role: 'User'
        },
        expiresAt: 'later'
      } as any;
    });

    render(<Home />);

    await waitFor(() => {
      expect(screen.getByText('qa@example.com')).toBeInTheDocument();
    });

    const askButton = screen.getByRole('button', { name: 'Chiedi' });
    await user.click(askButton);

    expect(screen.getByRole('button', { name: 'Richiesta...' })).toBeDisabled();

    await act(async () => {
      resolveQa?.({ answer: 'Due giocatori.' });
    });

    await waitFor(() => {
      expect(screen.getByRole('button', { name: 'Chiedi' })).not.toBeDisabled();
    });

    expect(screen.getByText('Due giocatori.')).toBeInTheDocument();
    expect(screen.getByText('Risposta aggiornata.')).toBeInTheDocument();
  });
});
