/**
 * MessageInput Component Tests
 *
 * Comprehensive tests for MessageInput component with ChatProvider integration.
 * Tests cover rendering, input handling, form submission, loading states, search mode,
 * streaming state, and accessibility.
 *
 * Issue #1887 - Batch 13: Test rewrite to match Zustand migration (Issue #1083)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MessageInput } from '../MessageInput';
import { useChatContext } from '@/hooks/useChatContext';

// Mock useChatContext hook
vi.mock('@/hooks/useChatContext');

// Mock SearchModeToggle component
vi.mock('@/components', () => ({
  SearchModeToggle: ({ value, onChange, disabled }: any) => (
    <div data-testid="search-mode-toggle">
      <button onClick={() => onChange('vector')} disabled={disabled}>
        Vector
      </button>
      <button onClick={() => onChange('hybrid')} disabled={disabled}>
        Hybrid
      </button>
      <span>Mode: {value}</span>
    </div>
  ),
}));

describe('MessageInput', () => {
  const mockSendMessage = vi.fn();
  const mockSetInputValue = vi.fn();
  const mockSetSearchMode = vi.fn();
  const mockStopStreaming = vi.fn();

  const defaultMockContext = {
    // Input State
    inputValue: '',
    setInputValue: mockSetInputValue,

    // Send message
    sendMessage: mockSendMessage,

    // Game/Agent selection
    selectedGameId: 'game-123',
    selectedAgentId: 'agent-456',

    // Loading state
    loading: {
      games: false,
      agents: false,
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
      deleting: false,
    },

    // Search mode
    searchMode: 'vector',
    setSearchMode: mockSetSearchMode,

    // Streaming state (Issue #1007)
    isStreaming: false,
    streamingAnswer: '',
    streamingState: 'idle',
    streamingCitations: [],
    stopStreaming: mockStopStreaming,

    // Required context properties (not used by MessageInput but needed for type)
    authUser: null,
    games: [],
    agents: [],
    selectGame: vi.fn(),
    selectAgent: vi.fn(),
    chats: [],
    activeChatId: null,
    messages: [],
    createChat: vi.fn(),
    deleteChat: vi.fn(),
    selectChat: vi.fn(),
    setMessageFeedback: vi.fn(),
    editMessage: vi.fn(),
    deleteMessage: vi.fn(),
    errorMessage: '',
    sidebarCollapsed: false,
    toggleSidebar: vi.fn(),
    editingMessageId: null,
    editContent: '',
    setEditContent: vi.fn(),
    startEditMessage: vi.fn(),
    cancelEdit: vi.fn(),
    saveEdit: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (useChatContext as any).mockReturnValue(defaultMockContext);
  });

  /**
   * RENDERING TESTS
   */
  describe('Rendering', () => {
    it('renders input field', () => {
      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      expect(input).toBeInTheDocument();
    });

    it('renders send button', () => {
      render(<MessageInput />);

      expect(screen.getByRole('button', { name: /send message/i })).toBeInTheDocument();
    });

    it('renders search mode toggle', () => {
      render(<MessageInput />);

      expect(screen.getByTestId('search-mode-toggle')).toBeInTheDocument();
    });

    it('shows current search mode', () => {
      render(<MessageInput />);

      expect(screen.getByText(/mode: vector/i)).toBeInTheDocument();
    });
  });

  /**
   * INPUT HANDLING TESTS
   */
  describe('Input Handling', () => {
    it('calls setInputValue when typing', async () => {
      const user = userEvent.setup();
      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      await user.type(input, 'Test message');

      expect(mockSetInputValue).toHaveBeenCalled();
    });

    it('displays current input value', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Current message',
      });

      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i }) as HTMLInputElement;
      expect(input.value).toBe('Current message');
    });
  });

  /**
   * FORM SUBMISSION TESTS
   */
  describe('Form Submission', () => {
    it('calls sendMessage on form submit', async () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test question',
      });

      render(<MessageInput />);

      const form = screen.getByRole('button', { name: /send message/i }).closest('form');
      fireEvent.submit(form!);

      await waitFor(() => {
        expect(mockSendMessage).toHaveBeenCalledWith('Test question');
      });
    });

    it('calls sendMessage on send button click', async () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Button click test',
      });

      render(<MessageInput />);

      const sendButton = screen.getByRole('button', { name: /send message/i });
      fireEvent.click(sendButton);

      await waitFor(() => {
        expect(mockSendMessage).toHaveBeenCalledWith('Button click test');
      });
    });

    it('does not submit empty message', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: '   ', // Whitespace only
      });

      render(<MessageInput />);

      const form = screen.getByRole('button', { name: /send message/i }).closest('form');
      fireEvent.submit(form!);

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('does not submit without game selected', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test',
        selectedGameId: null,
      });

      render(<MessageInput />);

      const form = screen.getByRole('button', { name: /send message/i }).closest('form');
      fireEvent.submit(form!);

      expect(mockSendMessage).not.toHaveBeenCalled();
    });

    it('does not submit without agent selected', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test',
        selectedAgentId: null,
      });

      render(<MessageInput />);

      const form = screen.getByRole('button', { name: /send message/i }).closest('form');
      fireEvent.submit(form!);

      expect(mockSendMessage).not.toHaveBeenCalled();
    });
  });

  /**
   * LOADING STATE TESTS
   */
  describe('Loading States', () => {
    it('disables input when sending', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        loading: { ...defaultMockContext.loading, sending: true },
      });

      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      expect(input).toBeDisabled();
    });

    it('shows loading text on button when sending', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test',
        loading: { ...defaultMockContext.loading, sending: true },
      });

      render(<MessageInput />);

      // LoadingButton shows loadingText "Invio..." when isLoading=true
      const button = screen.getByRole('button', { name: /send message/i });
      expect(button).toHaveTextContent(/invio/i);
    });

    it('disables send button when loading', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test',
        loading: { ...defaultMockContext.loading, sending: true },
      });

      render(<MessageInput />);

      const sendButton = screen.getByRole('button', { name: /send message/i });
      expect(sendButton).toBeDisabled();
    });
  });

  /**
   * STREAMING STATE TESTS (Issue #1007)
   */
  describe('Streaming State', () => {
    it('shows stop button when streaming', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        isStreaming: true,
      });

      render(<MessageInput />);

      expect(screen.getByRole('button', { name: /stop/i })).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: /invia/i })).not.toBeInTheDocument();
    });

    it('calls stopStreaming when stop button clicked', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        isStreaming: true,
      });

      render(<MessageInput />);

      const stopButton = screen.getByRole('button', { name: /stop/i });
      fireEvent.click(stopButton);

      expect(mockStopStreaming).toHaveBeenCalled();
    });

    it('disables input when streaming', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        isStreaming: true,
      });

      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      expect(input).toBeDisabled();
    });
  });

  /**
   * SEARCH MODE TOGGLE TESTS (AI-14)
   */
  describe('Search Mode Toggle', () => {
    it('displays current search mode', () => {
      render(<MessageInput />);

      expect(screen.getByText(/mode: vector/i)).toBeInTheDocument();
    });

    it('calls setSearchMode when mode changes', () => {
      render(<MessageInput />);

      const hybridButton = screen.getByText('Hybrid');
      fireEvent.click(hybridButton);

      expect(mockSetSearchMode).toHaveBeenCalledWith('hybrid');
    });

    it('disables search mode toggle when sending', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        loading: { ...defaultMockContext.loading, sending: true },
      });

      render(<MessageInput />);

      // SearchModeToggle buttons should be disabled
      const vectorButton = screen.getByText('Vector');
      const hybridButton = screen.getByText('Hybrid');
      expect(vectorButton).toBeDisabled();
      expect(hybridButton).toBeDisabled();
    });

    it('disables search mode toggle when no game selected', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        selectedGameId: null,
      });

      render(<MessageInput />);

      // SearchModeToggle buttons should be disabled
      const vectorButton = screen.getByText('Vector');
      const hybridButton = screen.getByText('Hybrid');
      expect(vectorButton).toBeDisabled();
      expect(hybridButton).toBeDisabled();
    });
  });

  /**
   * ACCESSIBILITY TESTS
   */
  describe('Accessibility', () => {
    it('has accessible label for input', () => {
      render(<MessageInput />);

      expect(screen.getByLabelText(/message input/i)).toBeInTheDocument();
    });

    it('has screen reader only label', () => {
      render(<MessageInput />);

      const srLabel = screen.getByText(/ask a question about the game/i);
      expect(srLabel).toHaveClass('sr-only');
    });

    it('has accessible aria-label for send button', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Test',
      });

      render(<MessageInput />);

      const sendButton = screen.getByRole('button', { name: /send message/i });
      expect(sendButton).toBeInTheDocument();
    });

    it('has accessible aria-label for stop button when streaming', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        isStreaming: true,
      });

      render(<MessageInput />);

      const stopButton = screen.getByRole('button', { name: /stop streaming/i });
      expect(stopButton).toBeInTheDocument();
    });
  });

  /**
   * EDGE CASES
   */
  describe('Edge Cases', () => {
    it('handles special characters in input', async () => {
      const user = userEvent.setup();
      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      await user.type(input, 'Special: <>&"\'');

      expect(mockSetInputValue).toHaveBeenCalled();
    });

    it('handles emoji in input', async () => {
      const user = userEvent.setup();
      render(<MessageInput />);

      const input = screen.getByRole('textbox', { name: /message input/i });
      await user.type(input, '🎲🎯');

      expect(mockSetInputValue).toHaveBeenCalled();
    });

    it('disables send button for empty input', () => {
      render(<MessageInput />);

      const sendButton = screen.getByRole('button', { name: /send message/i });
      expect(sendButton).toBeDisabled();
    });

    it('enables send button with valid input', () => {
      (useChatContext as any).mockReturnValue({
        ...defaultMockContext,
        inputValue: 'Valid message',
      });

      render(<MessageInput />);

      const sendButton = screen.getByRole('button', { name: /send message/i });
      expect(sendButton).not.toBeDisabled();
    });
  });
});
