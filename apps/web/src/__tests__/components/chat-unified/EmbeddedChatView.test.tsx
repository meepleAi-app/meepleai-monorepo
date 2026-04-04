import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { EmbeddedChatView } from '@/components/chat-unified/EmbeddedChatView';

const mockSendMessage = vi.fn();
const mockReset = vi.fn();
const mockStopStreaming = vi.fn();

let mockState = {
  statusMessage: null as string | null,
  currentAnswer: '',
  followUpQuestions: [] as string[],
  isStreaming: false,
  error: null as string | null,
  chatThreadId: 't1',
  totalTokens: 0,
  debugSteps: [],
  modelDowngrade: null,
  strategyTier: null,
  executionId: null,
};

vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: () => ({
    state: mockState,
    sendMessage: mockSendMessage,
    stopStreaming: mockStopStreaming,
    reset: mockReset,
  }),
}));

describe('EmbeddedChatView', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockState = {
      statusMessage: null,
      currentAnswer: '',
      followUpQuestions: [],
      isStreaming: false,
      error: null,
      chatThreadId: 't1',
      totalTokens: 0,
      debugSteps: [],
      modelDowngrade: null,
      strategyTier: null,
      executionId: null,
    };
  });

  it('renders the embedded chat view container', () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByTestId('embedded-chat-view')).toBeInTheDocument();
  });

  it('renders input area with placeholder', () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByPlaceholderText(/scrivi/i)).toBeInTheDocument();
  });

  it('renders send button', () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByRole('button', { name: /invia/i })).toBeInTheDocument();
  });

  it('renders welcome state when no messages', () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByText(/chiedi qualsiasi cosa/i)).toBeInTheDocument();
  });

  it('sends message via SSE on submit', async () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    const input = screen.getByPlaceholderText(/scrivi/i);
    fireEvent.change(input, { target: { value: 'Come si gioca?' } });
    fireEvent.submit(input.closest('form')!);
    await waitFor(() => {
      expect(mockSendMessage).toHaveBeenCalledWith(
        'a1',
        'Come si gioca?',
        't1',
        expect.anything(),
        undefined
      );
    });
  });

  it('clears input after sending', async () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    const input = screen.getByPlaceholderText(/scrivi/i) as HTMLInputElement;
    fireEvent.change(input, { target: { value: 'Test message' } });
    fireEvent.submit(input.closest('form')!);
    await waitFor(() => {
      expect(input.value).toBe('');
    });
  });

  it('adds user message to the list on send', async () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    const input = screen.getByPlaceholderText(/scrivi/i);
    fireEvent.change(input, { target: { value: 'Ciao!' } });
    fireEvent.submit(input.closest('form')!);
    await waitFor(() => {
      expect(screen.getByText('Ciao!')).toBeInTheDocument();
    });
    // Welcome message should be gone
    expect(screen.queryByText(/chiedi qualsiasi cosa/i)).not.toBeInTheDocument();
  });

  it('does not send empty messages', () => {
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    const input = screen.getByPlaceholderText(/scrivi/i);
    fireEvent.submit(input.closest('form')!);
    expect(mockSendMessage).not.toHaveBeenCalled();
  });

  it('shows streaming answer when isStreaming with currentAnswer', () => {
    mockState = {
      ...mockState,
      isStreaming: true,
      currentAnswer: 'Streaming response...',
    };
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByTestId('message-streaming')).toBeInTheDocument();
    expect(screen.getByText('Streaming response...')).toBeInTheDocument();
  });

  it('shows status message during streaming', () => {
    mockState = {
      ...mockState,
      isStreaming: true,
      statusMessage: 'Connecting...',
    };
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByTestId('stream-status')).toBeInTheDocument();
    expect(screen.getByText('Connecting...')).toBeInTheDocument();
  });

  it('shows error message when error state is set', () => {
    mockState = {
      ...mockState,
      error: 'Something went wrong',
    };
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByRole('alert')).toBeInTheDocument();
    expect(screen.getByText('Something went wrong')).toBeInTheDocument();
  });

  it('disables input while streaming', () => {
    mockState = {
      ...mockState,
      isStreaming: true,
    };
    render(<EmbeddedChatView threadId="t1" agentId="a1" gameId="g1" />);
    expect(screen.getByPlaceholderText(/scrivi/i)).toBeDisabled();
  });
});
