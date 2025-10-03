import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from './chat';
import { api } from '../lib/api';

jest.mock('../lib/api');
const mockedApi = api as jest.Mocked<typeof api>;

describe('Chat page', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  it('shows login required message when not authenticated', async () => {
    mockedApi.get.mockResolvedValue(null);
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText('Accesso richiesto')).toBeInTheDocument();
      expect(screen.getByText(/devi effettuare l'accesso per utilizzare la chat/i)).toBeInTheDocument();
    });
  });

  it('shows link to home when not authenticated', async () => {
    mockedApi.get.mockResolvedValue(null);
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByRole('link', { name: /vai al login/i })).toBeInTheDocument();
    });
  });

  it('renders chat interface when authenticated', async () => {
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
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText('MeepleAI Chat')).toBeInTheDocument();
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });
  });

  it('shows game selector with options', async () => {
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
    render(<ChatPage />);

    await waitFor(() => {
      const select = screen.getByLabelText(/seleziona il gioco/i);
      expect(select).toBeInTheDocument();
      expect(screen.getByRole('option', { name: /demo - chess/i })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: /demo - catan/i })).toBeInTheDocument();
      expect(screen.getByRole('option', { name: /demo - risk/i })).toBeInTheDocument();
    });
  });

  it('shows empty state when no messages', async () => {
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
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByText(/nessun messaggio ancora/i)).toBeInTheDocument();
    });
  });

  it('sends message and displays response', async () => {
    const mockUser = {
      user: {
        id: '1',
        tenantId: 'test-tenant',
        email: 'test@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    const mockResponse = {
      answer: 'Chess is played by 2 players.',
      sources: [
        {
          title: 'Chess Rules',
          snippet: 'Chess is a two-player strategy board game.',
          page: 1,
        },
      ],
    };

    mockedApi.get.mockResolvedValue(mockUser);
    mockedApi.post.mockResolvedValue(mockResponse);

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    const sendButton = screen.getByRole('button', { name: /invia/i });

    await user.type(input, 'How many players?');
    await user.click(sendButton);

    await waitFor(() => {
      expect(screen.getByText('How many players?')).toBeInTheDocument();
      expect(screen.getByText('Chess is played by 2 players.')).toBeInTheDocument();
    });
  });

  it('shows sources in assistant messages', async () => {
    const mockUser = {
      user: {
        id: '1',
        tenantId: 'test-tenant',
        email: 'test@example.com',
        role: 'User',
      },
      expiresAt: '2025-01-01T00:00:00Z',
    };

    const mockResponse = {
      answer: 'The game starts with setup.',
      sources: [
        {
          title: 'Game Manual',
          snippet: 'Place the board and pieces.',
          page: 3,
        },
      ],
    };

    mockedApi.get.mockResolvedValue(mockUser);
    mockedApi.post.mockResolvedValue(mockResponse);

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, 'How to start?');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => {
      expect(screen.getByText(/fonti:/i)).toBeInTheDocument();
      expect(screen.getByText(/game manual/i)).toBeInTheDocument();
      expect(screen.getByText(/pagina 3/i)).toBeInTheDocument();
    });
  });

  it('shows loading state while sending message', async () => {
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
    mockedApi.post.mockImplementation(() => new Promise((resolve) => setTimeout(() => resolve({ answer: 'Response' }), 100)));

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, 'Test question');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    expect(screen.getByText(/sto pensando/i)).toBeInTheDocument();

    await waitFor(() => {
      expect(screen.queryByText(/sto pensando/i)).not.toBeInTheDocument();
    });
  });

  it('handles send error and removes user message', async () => {
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
    mockedApi.post.mockRejectedValue(new Error('API Error'));

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, 'Test question');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => {
      expect(screen.getByText(/errore nella comunicazione/i)).toBeInTheDocument();
      expect(screen.queryByText('Test question')).not.toBeInTheDocument();
    });
  });

  it('can change selected game', async () => {
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

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByLabelText(/seleziona il gioco/i)).toBeInTheDocument();
    });

    const select = screen.getByLabelText(/seleziona il gioco/i) as HTMLSelectElement;
    expect(select.value).toBe('demo-chess');

    await user.selectOptions(select, 'demo-catan');

    expect(select.value).toBe('demo-catan');
  });

  it('clears chat history when clear button clicked', async () => {
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
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    // Send a message
    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, 'Test question');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => {
      expect(screen.getByText('Test answer')).toBeInTheDocument();
    });

    // Clear history
    const clearButton = screen.getByRole('button', { name: /cancella storia/i });
    await user.click(clearButton);

    await waitFor(() => {
      expect(screen.queryByText('Test answer')).not.toBeInTheDocument();
      expect(screen.getByText(/nessun messaggio ancora/i)).toBeInTheDocument();
    });
  });

  it('can toggle feedback on assistant messages', async () => {
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
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, 'Question');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    await waitFor(() => {
      expect(screen.getByText('Test answer')).toBeInTheDocument();
    });

    const helpfulButtons = screen.getAllByRole('button', { name: /utile/i });
    const helpfulButton = helpfulButtons[0];
    await user.click(helpfulButton);

    expect(helpfulButton).toHaveStyle({ background: '#34a853' });

    // Click again to toggle off
    await user.click(helpfulButton);

    expect(helpfulButton).toHaveStyle({ background: '#f1f3f4' });
  });

  it('disables input when loading', async () => {
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
    mockedApi.post.mockImplementation(() => new Promise((resolve) => setTimeout(() => resolve({ answer: 'Response' }), 200)));

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i) as HTMLInputElement;
    await user.type(input, 'Test');
    await user.click(screen.getByRole('button', { name: /invia/i }));

    expect(input).toBeDisabled();

    await waitFor(() => {
      expect(input).not.toBeDisabled();
    });
  });

  it('does not send empty messages', async () => {
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

    const user = userEvent.setup();
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByPlaceholderText(/fai una domanda sul gioco/i)).toBeInTheDocument();
    });

    const sendButton = screen.getByRole('button', { name: /invia/i });

    // Button should be disabled when input is empty
    expect(sendButton).toBeDisabled();

    // Type only spaces
    const input = screen.getByPlaceholderText(/fai una domanda sul gioco/i);
    await user.type(input, '   ');

    // Button should still be disabled
    expect(sendButton).toBeDisabled();
  });
});
