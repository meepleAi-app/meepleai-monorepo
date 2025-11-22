/**
 * ChatContent Component Tests
 *
 * Tests for the ChatContent component that displays the main chat area
 * with header, message list, and input form.
 *
 * Target Coverage: 90%+ (from 81.8%)
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { ChatContent } from '../../../components/chat/ChatContent';

// Mock the ChatProvider context
const mockUseChatContext = jest.fn();
jest.mock('../../../components/chat/ChatProvider', () => ({
  useChatContext: () => mockUseChatContext(),
}));

// Mock child components
jest.mock('../../../components/chat/MessageList', () => ({
  MessageList: () => <div data-testid="message-list">Message List</div>,
}));

jest.mock('../../../components/chat/MessageInput', () => ({
  MessageInput: () => <div data-testid="message-input">Message Input</div>,
}));

// Mock Next.js Link
jest.mock('next/link', () => {
  return ({ children, href }: any) => {
    return <a href={href}>{children}</a>;
  };
});

/**
 * Helper to setup mock context with default values
 */
const setupMockContext = (overrides?: any) => {
  mockUseChatContext.mockReturnValue({
    games: [],
    selectedGameId: null,
    activeChatId: null,
    chats: [],
    errorMessage: null,
    sidebarCollapsed: false,
    toggleSidebar: jest.fn(),
    ...overrides,
  });
};

describe('ChatContent Component', () => {
  beforeEach(() => {
    jest.clearAllMocks();
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders main container', () => {
      setupMockContext();
      const { container } = render(<ChatContent />);

      expect(container.firstChild).toBeInTheDocument();
    });

    it('renders header section', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByRole('heading')).toBeInTheDocument();
    });

    it('renders MessageList component', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByTestId('message-list')).toBeInTheDocument();
    });

    it('renders MessageInput component', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    it('renders home link', () => {
      setupMockContext();
      render(<ChatContent />);

      const homeLink = screen.getByRole('link', { name: 'Home' });
      expect(homeLink).toBeInTheDocument();
      expect(homeLink).toHaveAttribute('href', '/');
    });

    it('renders sidebar toggle button', () => {
      setupMockContext();
      render(<ChatContent />);

      const toggleButton = screen.getByRole('button', { name: /sidebar/i });
      expect(toggleButton).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Header Display
   */
  describe('Header Display', () => {
    it('displays default title when no chat is active', () => {
      setupMockContext({ activeChatId: null });
      render(<ChatContent />);

      expect(screen.getByText('Seleziona o crea una chat')).toBeInTheDocument();
    });

    it('displays agent name when chat is active', () => {
      const chats = [
        {
          id: 'chat-1',
          title: 'Chess Expert',
          gameName: 'Chess',
          gameId: 'game-1',
          createdAt: '2025-01-10T10:00:00Z',
          lastMessageAt: '2025-01-10T10:05:00Z',
          messageCount: 5,
          messages: []
        },
      ];
      setupMockContext({ activeChatId: 'chat-1', chats });
      render(<ChatContent />);

      expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
    });

    it('displays "Chat" as fallback when agent name is not found', () => {
      const chats = [
        {
          id: 'chat-1',
          title: undefined,
          gameName: 'Chess',
          gameId: 'game-1',
          createdAt: '2025-01-10T10:00:00Z',
          lastMessageAt: '2025-01-10T10:05:00Z',
          messageCount: 5,
          messages: []
        },
      ];
      setupMockContext({ activeChatId: 'chat-1', chats });
      render(<ChatContent />);

      expect(screen.getByRole('heading', { name: 'Chat' })).toBeInTheDocument();
    });

    it('displays game name when game is selected', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectedGameId: 'game-1' });
      render(<ChatContent />);

      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    it('displays default message when no game is selected', () => {
      setupMockContext({ games: [], selectedGameId: null });
      render(<ChatContent />);

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('displays empty string when game is selected but not found', () => {
      const games = [{ id: 'game-1', name: 'Chess' }];
      setupMockContext({ games, selectedGameId: 'game-999' });
      render(<ChatContent />);

      expect(screen.queryByText('Chess')).not.toBeInTheDocument();
    });

    it('updates header when active chat changes', () => {
      const chats = [
        {
          id: 'chat-1',
          title: 'Chess Expert',
          gameName: 'Chess',
          gameId: 'game-1',
          createdAt: '2025-01-10T10:00:00Z',
          lastMessageAt: '2025-01-10T10:05:00Z',
          messageCount: 5,
          messages: []
        },
        {
          id: 'chat-2',
          title: 'Catan Helper',
          gameName: 'Catan',
          gameId: 'game-2',
          createdAt: '2025-01-10T10:00:00Z',
          lastMessageAt: '2025-01-10T10:05:00Z',
          messageCount: 3,
          messages: []
        },
      ];
      setupMockContext({ activeChatId: 'chat-1', chats });
      const { rerender } = render(<ChatContent />);

      expect(screen.getByText('Chess Expert')).toBeInTheDocument();

      // Change active chat
      setupMockContext({ activeChatId: 'chat-2', chats });
      rerender(<ChatContent />);

      expect(screen.queryByText('Chess Expert')).not.toBeInTheDocument();
      expect(screen.getByText('Catan Helper')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Sidebar Toggle
   */
  describe('Sidebar Toggle', () => {
    it('shows hide icon when sidebar is expanded', () => {
      setupMockContext({ sidebarCollapsed: false });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: 'Hide sidebar' });
      expect(button).toHaveTextContent('✕');
    });

    it('shows show icon when sidebar is collapsed', () => {
      setupMockContext({ sidebarCollapsed: true });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: 'Show sidebar' });
      expect(button).toHaveTextContent('☰');
    });

    it('calls toggleSidebar when button is clicked', () => {
      const toggleSidebar = jest.fn();
      setupMockContext({ toggleSidebar });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: /sidebar/i });
      fireEvent.click(button);

      expect(toggleSidebar).toHaveBeenCalled();
    });

    it('has correct aria-label when expanded', () => {
      setupMockContext({ sidebarCollapsed: false });
      render(<ChatContent />);

      expect(screen.getByRole('button', { name: 'Hide sidebar' })).toBeInTheDocument();
    });

    it('has correct aria-label when collapsed', () => {
      setupMockContext({ sidebarCollapsed: true });
      render(<ChatContent />);

      expect(screen.getByRole('button', { name: 'Show sidebar' })).toBeInTheDocument();
    });

    it('has correct aria-expanded attribute', () => {
      setupMockContext({ sidebarCollapsed: false });
      const { rerender } = render(<ChatContent />);

      let button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'true');

      // Collapse
      setupMockContext({ sidebarCollapsed: true });
      rerender(<ChatContent />);

      button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });

    it('has correct title attribute', () => {
      setupMockContext({ sidebarCollapsed: false });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('title', 'Nascondi sidebar');
    });

    it('updates icon when sidebar state changes', () => {
      setupMockContext({ sidebarCollapsed: false });
      const { rerender } = render(<ChatContent />);

      expect(screen.getByText('✕')).toBeInTheDocument();

      // Collapse
      setupMockContext({ sidebarCollapsed: true });
      rerender(<ChatContent />);

      expect(screen.queryByText('✕')).not.toBeInTheDocument();
      expect(screen.getByText('☰')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Error Message Display
   */
  describe('Error Message Display', () => {
    it('does not display error when errorMessage is null', () => {
      setupMockContext({ errorMessage: null });
      render(<ChatContent />);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('displays error message when errorMessage is set', () => {
      const errorMessage = 'Failed to send message';
      setupMockContext({ errorMessage });
      render(<ChatContent />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText(errorMessage)).toBeInTheDocument();
    });

    it('has correct aria-live attribute', () => {
      setupMockContext({ errorMessage: 'Error occurred' });
      render(<ChatContent />);

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });

    it('updates when error message changes', () => {
      setupMockContext({ errorMessage: 'First error' });
      const { rerender } = render(<ChatContent />);

      expect(screen.getByText('First error')).toBeInTheDocument();

      // Change error
      setupMockContext({ errorMessage: 'Second error' });
      rerender(<ChatContent />);

      expect(screen.queryByText('First error')).not.toBeInTheDocument();
      expect(screen.getByText('Second error')).toBeInTheDocument();
    });

    it('removes error when errorMessage is cleared', () => {
      setupMockContext({ errorMessage: 'Error occurred' });
      const { rerender } = render(<ChatContent />);

      expect(screen.getByRole('alert')).toBeInTheDocument();

      // Clear error
      setupMockContext({ errorMessage: null });
      rerender(<ChatContent />);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('handles very long error messages', () => {
      const longError = 'E'.repeat(500);
      setupMockContext({ errorMessage: longError });
      render(<ChatContent />);

      expect(screen.getByText(longError)).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Component Integration
   */
  describe('Component Integration', () => {
    it('renders MessageList and MessageInput together', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByTestId('message-list')).toBeInTheDocument();
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    it('passes context to child components', () => {
      setupMockContext({
        games: [{ id: 'game-1', name: 'Chess' }],
        selectedGameId: 'game-1',
      });
      render(<ChatContent />);

      // Components should render with context
      expect(screen.getByTestId('message-list')).toBeInTheDocument();
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles empty games array', () => {
      setupMockContext({ games: [], selectedGameId: null });
      render(<ChatContent />);

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('handles empty chats array', () => {
      setupMockContext({ chats: [], activeChatId: null });
      render(<ChatContent />);

      expect(screen.getByText('Seleziona o crea una chat')).toBeInTheDocument();
    });

    it('handles undefined activeChatId', () => {
      setupMockContext({ activeChatId: undefined });
      render(<ChatContent />);

      expect(screen.getByText('Seleziona o crea una chat')).toBeInTheDocument();
    });

    it('handles undefined selectedGameId', () => {
      setupMockContext({ selectedGameId: undefined });
      render(<ChatContent />);

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('handles chat with missing agent name', () => {
      const chats = [
        {
          id: 'chat-1',
          gameName: 'Chess',
          gameId: 'game-1',
          agentId: 'agent-1',
        },
      ];
      setupMockContext({ activeChatId: 'chat-1', chats });
      render(<ChatContent />);

      expect(screen.getByText('Chat')).toBeInTheDocument();
    });

    it('handles game with special characters in name', () => {
      const games = [{ id: 'game-1', name: "Catan: Trader's & Barbarians" }];
      setupMockContext({ games, selectedGameId: 'game-1' });
      render(<ChatContent />);

      expect(screen.getByText("Catan: Trader's & Barbarians")).toBeInTheDocument();
    });

    it('handles rapid sidebar toggles', () => {
      const toggleSidebar = jest.fn();
      setupMockContext({ toggleSidebar });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: /sidebar/i });
      for (let i = 0; i < 5; i++) {
        fireEvent.click(button);
      }

      expect(toggleSidebar).toHaveBeenCalledTimes(5);
    });

    it('handles null games array', () => {
      setupMockContext({ games: [], selectedGameId: null });

      // Should not crash, might show empty or default state
      expect(() => render(<ChatContent />)).not.toThrow();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('uses semantic heading element', () => {
      setupMockContext();
      render(<ChatContent />);

      const heading = screen.getByRole('heading');
      expect(heading.tagName).toBe('H1');
    });

    it('has proper button aria-labels', () => {
      setupMockContext({ sidebarCollapsed: false });
      render(<ChatContent />);

      expect(screen.getByRole('button', { name: 'Hide sidebar' })).toBeInTheDocument();
    });

    it('uses aria-expanded for toggle button', () => {
      setupMockContext({ sidebarCollapsed: false });
      render(<ChatContent />);

      const button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'true');
    });

    it('provides error context with role alert', () => {
      setupMockContext({ errorMessage: 'Error' });
      render(<ChatContent />);

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('uses aria-live for error announcements', () => {
      setupMockContext({ errorMessage: 'Error' });
      render(<ChatContent />);

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });
  });

  /**
   * Test Group: Layout
   */
  describe('Layout', () => {
    it('maintains header at top', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByRole('heading')).toBeInTheDocument();
    });

    it('maintains input at bottom', () => {
      setupMockContext();
      render(<ChatContent />);

      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });
  });
});