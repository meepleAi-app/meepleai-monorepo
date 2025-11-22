import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { BottomNav } from '../BottomNav';

// Mock Zustand store
jest.mock('@/store/chat/store', () => ({
  useChatStore: jest.fn()
}));

import { useChatStore } from '@/store/chat/store';

// Mock LoadingButton
jest.mock('@/components/loading/LoadingButton', () => ({
  LoadingButton: ({ children, isLoading, loadingText, onClick, disabled, className, ...props }: any) => (
    <button
      onClick={onClick}
      disabled={disabled}
      className={className}
      aria-busy={isLoading}
      {...props}
    >
      {isLoading ? loadingText : children}
    </button>
  )
}));

describe('BottomNav', () => {
  const mockCreateChat = jest.fn();
  const mockUseChatStore = useChatStore as jest.MockedFunction<typeof useChatStore>;

  const defaultContextValue = {
    games: [
      { id: '770e8400-e29b-41d4-a716-000000000001', name: 'Test Game 1' },
      { id: '770e8400-e29b-41d4-a716-000000000002', name: 'Test Game 2' }
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
    mockUseChatStore.mockReturnValue(defaultContextValue);
  });

  describe('Basic Rendering', () => {
    it('should render mobile chat actions nav', () => {
      render(<BottomNav />);

      const nav = screen.getByRole('navigation', { name: /mobile chat actions/i });
      expect(nav).toBeInTheDocument();
    });

    it('should render new chat button', () => {
      render(<BottomNav />);

      expect(screen.getByRole('button', { name: /create new chat on mobile/i })).toBeInTheDocument();
    });

    it('should display Italian button text', () => {
      render(<BottomNav />);

      expect(screen.getByText('+ Nuova Chat')).toBeInTheDocument();
    });
  });

  describe('Button State', () => {
    it('should enable button when game and agent are selected', () => {
      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button).toBeEnabled();
    });

    it('should disable button when no game is selected', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: null
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button).toBeDisabled();
    });

    it('should disable button when no agent is selected', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedAgentId: null
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button).toBeDisabled();
    });

    it('should disable button when loading', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        loading: { creating: true, messages: false, sending: false }
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button).toBeDisabled();
    });
  });

  describe('Create Chat Action', () => {
    it('should call createChat when button is clicked', async () => {
      const user = userEvent.setup();

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      await user.click(button);

      expect(mockCreateChat).toHaveBeenCalledTimes(1);
    });

    it('should not call createChat when button is disabled', async () => {
      const user = userEvent.setup();
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: null
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      await user.click(button);

      expect(mockCreateChat).not.toHaveBeenCalled();
    });
  });

  describe('Loading State', () => {
    it('should display loading text when creating chat', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        loading: { creating: true, messages: false, sending: false }
      });

      render(<BottomNav />);

      expect(screen.getByText('Creazione...')).toBeInTheDocument();
    });

    it('should set aria-busy when loading', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        loading: { creating: true, messages: false, sending: false }
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button).toHaveAttribute('aria-busy', 'true');
    });
  });

  describe('Styling & Responsive', () => {
    it('should have fixed bottom positioning', () => {
      const { container } = render(<BottomNav />);

      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('fixed', 'bottom-0', 'left-0', 'right-0');
    });

    it('should be hidden on desktop (md:hidden)', () => {
      const { container } = render(<BottomNav />);

      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('md:hidden');
    });

    it('should have z-50 for proper layering', () => {
      const { container } = render(<BottomNav />);

      const nav = container.querySelector('nav');
      expect(nav).toHaveClass('z-50');
    });

    it('should apply disabled styling when button is disabled', () => {
      mockUseChatStore.mockReturnValue({
        ...defaultContextValue,
        selectedGameId: null
      });

      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button.className).toContain('bg-[#dadce0]');
      expect(button.className).toContain('cursor-not-allowed');
    });

    it('should apply active styling when button is enabled', () => {
      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button.className).toContain('bg-[#1a73e8]');
      expect(button.className).toContain('cursor-pointer');
    });
  });

  describe('Accessibility', () => {
    it('should have proper nav aria-label', () => {
      render(<BottomNav />);

      const nav = screen.getByRole('navigation', { name: /mobile chat actions/i });
      expect(nav).toHaveAttribute('aria-label', 'Mobile chat actions');
    });

    it('should have touch-target class for touch-friendly interaction', () => {
      render(<BottomNav />);

      const button = screen.getByRole('button', { name: /create new chat on mobile/i });
      expect(button.className).toContain('touch-target');
    });
  });
});
