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
        { source: 'PDF:pdf-demo-chess', text: 'Sezione introduttiva', page: 3, line: null }
      ]
    });
    mockApi.post.mockResolvedValueOnce({ ok: true });

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Quanti giocatori possono partecipare?');

    const sendButton = screen.getByRole('button', { name: /Invia/i });
    await user.click(sendButton);

    await waitFor(() => expect(mockApi.post).toHaveBeenNthCalledWith(1, '/agents/qa', {
      gameId: 'demo-chess',
      query: 'Quanti giocatori possono partecipare?'
    }));

    await screen.findByText('Il gioco supporta fino a 4 giocatori.');
    expect(screen.getByText('PDF:pdf-demo-chess (Pagina 3)')).toBeInTheDocument();
    expect(screen.getByText('Sezione introduttiva')).toBeInTheDocument();
    expect(screen.queryByText(/Errore nella comunicazione/i)).not.toBeInTheDocument();

    const helpfulButton = await screen.findByRole('button', { name: '👍 Utile' });
    await user.click(helpfulButton);

    await waitFor(() =>
      expect(mockApi.post).toHaveBeenNthCalledWith(
        2,
        '/agents/feedback',
        expect.objectContaining({
          endpoint: 'qa',
          outcome: 'helpful',
          userId: 'user-1',
          gameId: 'demo-chess'
        })
      )
    );
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

  it('rolls back feedback selection when the API request fails', async () => {
    const consoleErrorSpy = jest.spyOn(console, 'error').mockImplementation(() => {});
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post
      .mockResolvedValueOnce({
        answer: 'Risposta di prova.',
        sources: []
      })
      .mockRejectedValueOnce(new Error('Feedback error'));

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Domanda di test');

    const sendButton = screen.getByRole('button', { name: /Invia/i });
    await user.click(sendButton);

    await screen.findByText('Risposta di prova.');

    const helpfulButton = await screen.findByRole('button', { name: '👍 Utile' });
    await user.click(helpfulButton);

    await waitFor(() =>
      expect(mockApi.post).toHaveBeenNthCalledWith(
        2,
        '/agents/feedback',
        expect.objectContaining({ outcome: 'helpful' })
      )
    );

    await waitFor(() => expect(helpfulButton).toHaveStyle('background: #f1f3f4'));

    consoleErrorSpy.mockRestore();
  });

  it('handles multiple messages in a conversation', async () => {
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post
      .mockResolvedValueOnce({
        answer: 'Prima risposta',
        snippets: []
      })
      .mockResolvedValueOnce({
        answer: 'Seconda risposta',
        snippets: []
      });

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);

    // First message
    await user.type(input, 'Prima domanda?');
    await user.click(screen.getByRole('button', { name: /Invia/i }));

    // Wait for first response
    const firstResponse = await screen.findByText('Prima risposta');
    expect(firstResponse).toBeInTheDocument();

    // Second message - input should have been cleared
    await user.type(input, 'Seconda domanda?');
    await user.click(screen.getByRole('button', { name: /Invia/i }));

    // Wait for second response
    const secondResponse = await screen.findByText('Seconda risposta');
    expect(secondResponse).toBeInTheDocument();

    // Both questions and responses should be visible
    expect(screen.getByText('Prima domanda?')).toBeInTheDocument();
    expect(screen.getByText('Seconda domanda?')).toBeInTheDocument();
  });

  it('allows changing the selected game', async () => {
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post.mockResolvedValueOnce({
      answer: 'Risposta per nuovo gioco',
      snippets: []
    });
    mockApi.post.mockResolvedValueOnce({ ok: true });

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const gameSelect = screen.getByRole('combobox', { name: /Gioco/i });

    // Change game
    await user.selectOptions(gameSelect, 'demo-chess');
    expect(gameSelect).toHaveValue('demo-chess');

    // Send message with new game
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Domanda per il gioco selezionato');
    await user.click(screen.getByRole('button', { name: /Invia/i }));

    await waitFor(() =>
      expect(mockApi.post).toHaveBeenCalledWith('/agents/qa', {
        gameId: 'demo-chess',
        query: 'Domanda per il gioco selezionato'
      })
    );
  });

  it('disables send button while loading', async () => {
    mockApi.get.mockResolvedValue(mockAuthResponse);
    mockApi.post.mockImplementation(() => new Promise(() => {})); // Never resolves

    render(<ChatPage />);

    await screen.findByRole('heading', { name: /MeepleAI Chat/i });

    const user = userEvent.setup();
    const input = screen.getByPlaceholderText(/Fai una domanda sul gioco/i);
    await user.type(input, 'Test message');

    const sendButton = screen.getByRole('button', { name: /Invia/i });
    await user.click(sendButton);

    // Button should be disabled while loading
    expect(sendButton).toBeDisabled();
  });
});
