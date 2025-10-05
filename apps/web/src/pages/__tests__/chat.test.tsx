import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../chat';
import { api } from '../../lib/api';

jest.mock('../../lib/api', () => ({
  api: {
    get: jest.fn(),
    post: jest.fn(),
    put: jest.fn()
  }
}));

const mockApi = api as jest.Mocked<typeof api>;

const mockAuthResponse = {
  user: {
    id: 'user-1',
    email: 'user@example.com',
    displayName: 'User One',
    role: 'Admin'
  },
  expiresAt: new Date().toISOString()
};

describe('ChatPage', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('renders login prompt when user is not authenticated', async () => {
    mockApi.get.mockResolvedValueOnce(null);

    render(<ChatPage />);

    await waitFor(() => expect(mockApi.get).toHaveBeenCalledWith('/auth/me'));
    expect(screen.getByRole('heading', { name: /Accesso richiesto/i })).toBeInTheDocument();
    expect(screen.getByText(/Devi effettuare l'accesso per utilizzare la chat/i)).toBeInTheDocument();
  });

  it('sends a message and renders agent response for authenticated users', async () => {
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post.mockResolvedValueOnce({
      answer: 'Il gioco supporta fino a 4 giocatori.',
      snippets: [
        { text: 'Sezione introduttiva', source: 'Manuale Ufficiale', page: 3 }
      ]
    });

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Quanti giocatori possono partecipare?');

    const sendButton = screen.getByRole('button', { name: /Invia/i });
    await user.click(sendButton);

    await waitFor(() => expect(mockApi.post).toHaveBeenCalledWith('/agents/qa', {
      gameId: 'demo-chess',
      query: 'Quanti giocatori possono partecipare?'
    }));

    await screen.findByText('Il gioco supporta fino a 4 giocatori.');
    expect(screen.getByText('Sezione introduttiva')).toBeInTheDocument();
    expect(screen.getByText('Manuale Ufficiale')).toBeInTheDocument();
    expect(screen.getByText('Pagina 3')).toBeInTheDocument();
    expect(screen.queryByText(/Errore nella comunicazione/i)).not.toBeInTheDocument();
  });

  it('shows an error and restores state when the agent request fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post.mockRejectedValueOnce(new Error('Network error'));

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Ciao agente?');

    const sendButton = screen.getByRole('button', { name: /Invia/i });
    await user.click(sendButton);

    await waitFor(() => expect(mockApi.post).toHaveBeenCalled());

    await screen.findByText(/Errore nella comunicazione con l'agente/i);
    await screen.findByText(/Nessun messaggio ancora/i);

    consoleErrorSpy.mockRestore();
  });
});
