import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MobileSidebar } from '../MobileSidebar';

// Mock useChatStore hook
jest.mock('@/store/chat/store', () => ({
  useChatStore: jest.fn()
}));

import { useChatStore } from '@/store/chat/store';

// Mock child components
jest.mock('../GameSelector', () => ({
  GameSelector: () => <div data-testid="game-selector">Game Selector Mock</div>
}));

jest.mock('../AgentSelector', () => ({
  AgentSelector: () => <div data-testid="agent-selector">Agent Selector Mock</div>
}));

jest.mock('../ChatHistory', () => ({
  ChatHistory: () => <div data-testid="chat-history">Chat History Mock</div>
}));

jest.mock('@/components/loading/LoadingButton', () => ({
  LoadingButton: ({ children, isLoading, loadingText, onClick, disabled, ...props }: any) => (
    <button
      onClick={onClick}
      disabled={disabled}
      aria-busy={isLoading}
      {...props}
    >
      {isLoading ? loadingText : children}
    </button>
  )
}));

// Mock Sheet component
jest.mock('@/components/ui/sheet', () => ({
  Sheet: ({ children, open }: any) => open ? <div role="dialog">{children}</div> : null,
  SheetContent: ({ children, side, className, ...props }: any) => (
    <div className={className} data-side={side} {...props}>{children}</div>
  ),
  SheetHeader: ({ children, className }: any) => <div className={className}>{children}</div>,
  SheetTitle: ({ children }: any) => <h2>{children}</h2>
}));

describe('MobileSidebar', () => {
  const mockOnOpenChange = jest.fn();
  const mockCreateChat = jest.fn();
  const mockUseChatStore = useChatStore as jest.MockedFunction<typeof useChatStore>;

  const defaultContextValue = {
    games: [
      { id: '770e8400-e29b-41d4-a716-000000000001', name: 'Catan' },
      { id: '770e8400-e29b-41d4-a716-000000000002', name: 'Wingspan' }
    ],
    selectedGameId: '770e8400-e29b-41d4-a716-000000000001',
    selectedAgentId: 'agent-1',
    loading: { creating: false, messages: false, sending: false },
    createChat: mockCreateChat,
    agents: [],
    threads: [],
    activeThread: null,
    activeChatId: null,
    messages: [],
    setSelectedGameId: jest.fn(),
    setSelectedAgentId: jest.fn(),
    loadThreadMessages: jest.fn(),
    sendMessage: jest.fn(),
    deleteMessage: jest.fn(),
    editMessage: jest.fn(),
    regenerateMessage: jest.fn()
  } as any;

  beforeEach(() => {
    jest.clearAllMocks();
    mockCreateChat.mockResolvedValue(undefined);
    mockUseChatStore.mockReturnValue(defaultContextValue);
  });

  describe('Basic Rendering', () => {
    it('should render when open is true', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByRole('dialog')).toBeInTheDocument();
    });

    it('should not render when open is false', () => {
      render(<MobileSidebar open={false} onOpenChange={mockOnOpenChange} />);
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('should render MeepleAI Chat title', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByText('MeepleAI Chat')).toBeInTheDocument();
    });

    it('should render game selector', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByTestId('game-selector')).toBeInTheDocument();
    });

    it('should render agent selector', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByTestId('agent-selector')).toBeInTheDocument();
    });

    it('should render chat history', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByTestId('chat-history')).toBeInTheDocument();
    });

    it('should render new chat button', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByRole('button', { name: /create new chat/i })).toBeInTheDocument();
    });
  });

  describe('Game Context Badge', () => {
    it('should display selected game name in badge', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByText('Catan')).toBeInTheDocument();
    });

    it('should have aria-label with game name', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      const badge = screen.getByLabelText(/active game context: catan/i);
      expect(badge).toBeInTheDocument();
    });

    it('should have title attribute with full game name', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      const badge = screen.getByTitle(/currently chatting about: catan/i);
      expect(badge).toBeInTheDocument();
    });

    it('should not display badge when no game is selected', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: null
      });
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.queryByText('Catan')).not.toBeInTheDocument();
    });

    it('should display "..." when selected game is not found', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: 'non-existent-game'
      });
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);
      expect(screen.getByText('...')).toBeInTheDocument();
    });
  });

  describe('Create Chat Functionality', () => {
    it('should call createChat when button is clicked', async () => {
      const user = userEvent.setup();
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const button = screen.getByRole('button', { name: /create new chat/i });
      await user.click(button);

      expect(mockCreateChat).toHaveBeenCalledTimes(1);
    });

    it('should close sidebar after creating chat', async () => {
      const user = userEvent.setup();
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const button = screen.getByRole('button', { name: /create new chat/i });
      await user.click(button);

      await waitFor(() => {
        expect(mockOnOpenChange).toHaveBeenCalledWith(false);
      });
    });
  });

  describe('Button State Management', () => {
    it('should disable button when neither game nor agent selected', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: null,
        selectedAgentId: null
      });
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const button = screen.getByRole('button', { name: /create new chat/i });
      expect(button).toBeDisabled();
    });

    it('should enable button when both game and agent are selected', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const button = screen.getByRole('button', { name: /create new chat/i });
      expect(button).toBeEnabled();
    });

    it('should display loading text during chat creation', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        loading: { creating: true, messages: false, sending: false }
      });
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      expect(screen.getByText('Creazione...')).toBeInTheDocument();
      expect(screen.queryByText('+ Nuova Chat')).not.toBeInTheDocument();
    });
  });

  describe('Layout Structure', () => {
    it('should have proper width for mobile', () => {
      const { container } = render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const content = container.querySelector('[data-side="left"]');
      expect(content).toHaveClass('w-[320px]');
    });

    it('should slide in from left', () => {
      const { container } = render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const content = container.querySelector('[data-side="left"]');
      expect(content).toHaveAttribute('data-side', 'left');
    });

    it('should have full viewport height', () => {
      const { container } = render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const content = container.querySelector('[data-side="left"]');
      expect(content).toHaveClass('h-dvh');
    });

    it('should have scrollable chat history area', () => {
      const { container } = render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const historyContainer = screen.getByTestId('chat-history').parentElement;
      expect(historyContainer).toHaveClass('overflow-hidden');
    });
  });

  describe('Accessibility', () => {
    it('should have aria-label for mobile chat sidebar', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const sidebar = screen.getByLabelText(/mobile chat sidebar/i);
      expect(sidebar).toBeInTheDocument();
    });

    it('should have touch-target class for mobile touch interaction', () => {
      render(<MobileSidebar open={true} onOpenChange={mockOnOpenChange} />);

      const button = screen.getByRole('button', { name: /create new chat/i });
      expect(button.className).toContain('touch-target');
    });
  });
});