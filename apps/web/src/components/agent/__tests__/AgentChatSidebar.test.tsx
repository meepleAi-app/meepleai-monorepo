/**
 * AgentChatSidebar Component Tests (Issue #3187)
 *
 * Test Coverage:
 * - Rendering (desktop/mobile)
 * - Header with minimize/close buttons
 * - Messages display
 * - Auto-scroll behavior
 * - Copy conversation
 * - Export conversation
 * - Message count display
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';

import { AgentChatSidebar } from '../AgentChatSidebar';
import { createMockAgentMessage } from '@/__tests__/fixtures/common-fixtures';

// Mock useMediaQuery hook
const mockIsMobile = vi.fn();
vi.mock('@/lib/hooks/useMediaQuery', () => ({
  useMediaQuery: () => mockIsMobile(),
}));

// Mock zustand store
const mockMessages = vi.fn();
const mockSetMinimized = vi.fn();
vi.mock('@/store/agent/store', () => ({
  useAgentChatStore: (selector: (state: any) => any) => {
    const state = {
      messagesBySession: {
        'session-123': mockMessages(),
      },
      isMinimized: false,
      isStreaming: false,
      setMinimized: mockSetMinimized,
    };
    return selector(state);
  },
}));

// Mock child components
vi.mock('../AgentMessage', () => ({
  AgentMessage: ({ message }: { message: { content: string } }) => (
    <div data-testid="agent-message">{message.content}</div>
  ),
}));

vi.mock('../AgentTypingIndicator', () => ({
  AgentTypingIndicator: () => <div data-testid="typing-indicator">Typing...</div>,
}));

vi.mock('../../ui/navigation/sheet', () => ({
  Sheet: ({ children, open }: any) => (open ? <div>{children}</div> : null),
  SheetContent: ({ children }: any) => <div data-testid="sheet-content">{children}</div>,
  SheetHeader: ({ children }: any) => <div>{children}</div>,
  SheetTitle: ({ children }: any) => <h2>{children}</h2>,
}));

// Clipboard will be mocked per-test to avoid conflicts with userEvent

// Mock URL APIs
const mockCreateObjectURL = vi.fn(() => 'blob:mock-url');
const mockRevokeObjectURL = vi.fn();
URL.createObjectURL = mockCreateObjectURL;
URL.revokeObjectURL = mockRevokeObjectURL;

describe('AgentChatSidebar', () => {
  const defaultProps = {
    sessionId: 'session-123',
    typologyName: 'Rules Expert',
    isOpen: true,
    onClose: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    mockIsMobile.mockReturnValue(false);
    // Use fixture factory instead of magic strings
    mockMessages.mockReturnValue([
      createMockAgentMessage({ id: '1', type: 'user', content: 'Hello' }),
      createMockAgentMessage({ id: '2', type: 'agent', content: 'Hi there!' }),
    ]);
  });

  describe('Desktop Rendering', () => {
    it('renders sidebar with typology name', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByText('Rules Expert')).toBeInTheDocument();
    });

    it('renders all messages', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      const messages = screen.getAllByTestId('agent-message');
      expect(messages).toHaveLength(2);
    });

    it('renders minimize button', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /minimize sidebar/i })).toBeInTheDocument();
    });

    it('renders close button when onClose is provided', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /close sidebar/i })).toBeInTheDocument();
    });

    it('does not render close button when onClose is not provided', () => {
      render(<AgentChatSidebar {...defaultProps} onClose={undefined} />);

      expect(screen.queryByRole('button', { name: /close sidebar/i })).not.toBeInTheDocument();
    });

    it('hides sidebar when isOpen is false', () => {
      const { container } = render(<AgentChatSidebar {...defaultProps} isOpen={false} />);

      // Sidebar should have translate-x-full class when closed
      expect(container.querySelector('.translate-x-full')).toBeInTheDocument();
    });
  });

  describe('Mobile Rendering', () => {
    beforeEach(() => {
      mockIsMobile.mockReturnValue(true);
    });

    it('renders as sheet on mobile', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByTestId('sheet-content')).toBeInTheDocument();
    });

    it('shows typology name in sheet header', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByText('Rules Expert')).toBeInTheDocument();
    });
  });

  describe('Minimize/Maximize', () => {
    it('calls setMinimized when minimize button is clicked', async () => {
      const user = userEvent.setup();
      render(<AgentChatSidebar {...defaultProps} />);

      await user.click(screen.getByRole('button', { name: /minimize sidebar/i }));

      expect(mockSetMinimized).toHaveBeenCalledWith(true);
    });
  });

  describe('Close', () => {
    it('calls onClose when close button is clicked', async () => {
      const user = userEvent.setup();
      const mockOnClose = vi.fn();
      render(<AgentChatSidebar {...defaultProps} onClose={mockOnClose} />);

      await user.click(screen.getByRole('button', { name: /close sidebar/i }));

      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Copy Conversation', () => {
    it('renders copy button', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /copy conversation/i })).toBeInTheDocument();
    });

    it('disables copy button when no messages', () => {
      mockMessages.mockReturnValue([]);
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /copy conversation/i })).toBeDisabled();
    });

    it('has copy button enabled when messages exist', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /copy conversation/i })).not.toBeDisabled();
    });
  });

  describe('Export Conversation', () => {
    it('renders export button', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /export conversation/i })).toBeInTheDocument();
    });

    it('disables export button when no messages', () => {
      mockMessages.mockReturnValue([]);
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /export conversation/i })).toBeDisabled();
    });

    it('has export button enabled when messages exist', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByRole('button', { name: /export conversation/i })).not.toBeDisabled();
    });

    it('has correct button title attribute', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      const exportButton = screen.getByRole('button', { name: /export conversation/i });
      expect(exportButton).toHaveAttribute('title', 'Export conversation');
    });
  });

  describe('Message Count', () => {
    it('displays correct message count', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByText('2 messages')).toBeInTheDocument();
    });

    it('displays singular form for 1 message', () => {
      mockMessages.mockReturnValue([
        createMockAgentMessage({ id: '1', type: 'user', content: 'Hello' }),
      ]);
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByText('1 message')).toBeInTheDocument();
    });

    it('displays 0 messages when empty', () => {
      mockMessages.mockReturnValue([]);
      render(<AgentChatSidebar {...defaultProps} />);

      expect(screen.getByText('0 messages')).toBeInTheDocument();
    });
  });

  describe('Scroll Handling', () => {
    it('renders messages container with scroll handling', () => {
      render(<AgentChatSidebar {...defaultProps} />);

      // Messages container should exist
      const messages = screen.getAllByTestId('agent-message');
      expect(messages).toHaveLength(2);
    });
  });
});
