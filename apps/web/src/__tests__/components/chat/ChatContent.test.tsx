/**
 * ChatContent Component Tests (Migrated to Zustand)
 *
 * Tests for the ChatContent component that displays the main chat area
 * with header, message list, and input form.
 *
 * Migration Notes (Issue #1083):
 * - Replaced ChatProvider context with Zustand store
 * - Updated to match new store structure (threads, activeChatIds, messagesByChat)
 * - Updated text strings to match implementation (thread vs chat)
 *
 * Target Coverage: 90%+
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { ChatContent } from '../../../components/chat/ChatContent';
import { ChatThread, Game } from '../../../types';
import { renderWithChatStore, resetChatStore } from '@/__tests__/utils/zustand-test-utils';
import { useChatStore } from '@/store/chat/store';

// Mock child components
jest.mock('../../../components/chat/MessageList', () => ({
  MessageList: () => <div data-testid="message-list">Message List</div>,
}));

jest.mock('../../../components/chat/MessageInput', () => ({
  MessageInput: () => <div data-testid="message-input">Message Input</div>,
}));

jest.mock('../../../components/chat/MobileSidebar', () => ({
  MobileSidebar: ({ open }: { open: boolean }) =>
    open ? <div data-testid="mobile-sidebar">Mobile Sidebar</div> : null,
}));

// Mock Next.js Link
jest.mock('next/link', () => {
  return ({ children, href }: any) => {
    return <a href={href}>{children}</a>;
  };
});

/**
 * Helper to create mock chat thread
 */
const createMockChatThread = (overrides?: Partial<ChatThread>): ChatThread => ({
  id: 'chat-1',
  userId: 'user-1',
  gameId: 'game-1',
  title: 'Chess Expert',
  status: 'Active',
  createdAt: '2025-01-10T10:00:00Z',
  lastMessageAt: '2025-01-10T10:05:00Z',
  messageCount: 5,
  messages: [],
  ...overrides,
});

/**
 * Helper to setup chat store with default values
 */
const setupChatStore = (overrides?: any) => {
  const gameId = overrides?.selectedGameId || 'game-1';
  const activeChatId = overrides?.activeChatId || null;
  const threads = overrides?.threads || [];
  const games = overrides?.games || [];

  const state: any = {
    selectedGameId: gameId,
    games,
    chatsByGame: {
      [gameId]: threads,
    },
    activeChatIds: {
      [gameId]: activeChatId,
    },
    error: overrides?.error || null,
    sidebarCollapsed: overrides?.sidebarCollapsed ?? false,
    toggleSidebar: jest.fn(),
    loading: {
      chats: false,
      messages: false,
      sending: false,
      creating: false,
      updating: false,
      deleting: false,
      games: false,
      agents: false,
      ...overrides?.loading,
    },
  };

  return state;
};

describe('ChatContent Component', () => {
  let mockToggleSidebar: jest.Mock;

  beforeEach(() => {
    mockToggleSidebar = jest.fn();
    jest.clearAllMocks();
    resetChatStore();

    // Mock window.matchMedia for mobile detection
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: jest.fn().mockImplementation(query => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: jest.fn(),
        removeListener: jest.fn(),
        addEventListener: jest.fn(),
        removeEventListener: jest.fn(),
        dispatchEvent: jest.fn(),
      })),
    });
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders main container', () => {
      const state = setupChatStore();
      const { container } = renderWithChatStore(<ChatContent />, { initialState: state });

      expect(container.firstChild).toBeInTheDocument();
    });

    it('renders header section', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('heading')).toBeInTheDocument();
    });

    it('renders MessageList component', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByTestId('message-list')).toBeInTheDocument();
    });

    it('renders MessageInput component', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    it('renders home link', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      const homeLink = screen.getByRole('link', { name: 'Home' });
      expect(homeLink).toBeInTheDocument();
      expect(homeLink).toHaveAttribute('href', '/');
    });

    it('renders sidebar toggle button', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      const toggleButton = screen.getByRole('button', { name: /sidebar/i });
      expect(toggleButton).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Header Display
   */
  describe('Header Display', () => {
    it('displays default title when no thread is active', () => {
      const state = setupChatStore({ activeChatId: null });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Seleziona o crea un thread')).toBeInTheDocument();
    });

    it('displays thread title when thread is active', () => {
      const thread = createMockChatThread({ id: 'chat-1', title: 'Chess Expert' });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('heading', { name: 'Chess Expert' })).toBeInTheDocument();
    });

    it('displays "Chat Thread" as fallback when thread title is not found', () => {
      const thread = createMockChatThread({ id: 'chat-1', title: undefined as any });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('heading', { name: 'Chat Thread' })).toBeInTheDocument();
    });

    it('displays game name when game is selected', () => {
      const games: Game[] = [{ id: 'game-1', name: 'Chess' }];
      const state = setupChatStore({ games, selectedGameId: 'game-1' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Chess')).toBeInTheDocument();
    });

    it('displays default message when no game is selected', () => {
      const state = setupChatStore({ games: [], selectedGameId: null });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('displays "Nessun gioco selezionato" when game is selected but not found', () => {
      const games: Game[] = [{ id: 'game-1', name: 'Chess' }];
      const state = setupChatStore({ games, selectedGameId: 'game-999' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('updates header when active thread changes', () => {
      const thread1 = createMockChatThread({ id: 'chat-1', title: 'Chess Expert' });
      const thread2 = createMockChatThread({ id: 'chat-2', title: 'Catan Helper' });

      const state1 = setupChatStore({ activeChatId: 'chat-1', threads: [thread1, thread2] });
      const { rerender } = renderWithChatStore(<ChatContent />, { initialState: state1 });

      expect(screen.getByText('Chess Expert')).toBeInTheDocument();

      // Change active thread - setState BEFORE rerender
      const state2 = setupChatStore({ activeChatId: 'chat-2', threads: [thread1, thread2] });
      useChatStore.setState(state2);
      rerender(<ChatContent />);

      expect(screen.queryByText('Chess Expert')).not.toBeInTheDocument();
      expect(screen.getByText('Catan Helper')).toBeInTheDocument();
    });

    it('displays message count for active thread', () => {
      const thread = createMockChatThread({ id: 'chat-1', messageCount: 10 });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('10 messaggi')).toBeInTheDocument();
    });

    it('displays archived badge for closed threads', () => {
      const thread = createMockChatThread({ id: 'chat-1', status: 'Closed' });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const badge = screen.getByText('Archiviato');
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveAttribute('title', 'This thread is archived');
    });
  });

  /**
   * Test Group: Sidebar Toggle
   */
  describe('Sidebar Toggle', () => {
    beforeEach(() => {
      mockToggleSidebar = jest.fn();
    });

    it('shows hide icon when sidebar is expanded', () => {
      const state = setupChatStore({ sidebarCollapsed: false });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const button = screen.getByRole('button', { name: 'Hide sidebar' });
      expect(button).toHaveTextContent('✕');
    });

    it('shows show icon when sidebar is collapsed', () => {
      const state = setupChatStore({ sidebarCollapsed: true });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const button = screen.getByRole('button', { name: 'Show sidebar' });
      expect(button).toHaveTextContent('☰');
    });

    it('calls toggleSidebar when button is clicked', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      // Spy on the store's toggleSidebar AFTER render
      const toggleSpy = jest.spyOn(useChatStore.getState(), 'toggleSidebar');

      const button = screen.getByRole('button', { name: /sidebar/i });
      fireEvent.click(button);

      expect(toggleSpy).toHaveBeenCalled();
    });

    it('has correct aria-label when expanded', () => {
      const state = setupChatStore({ sidebarCollapsed: false });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('button', { name: 'Hide sidebar' })).toBeInTheDocument();
    });

    it('has correct aria-label when collapsed', () => {
      const state = setupChatStore({ sidebarCollapsed: true });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('button', { name: 'Show sidebar' })).toBeInTheDocument();
    });

    it('has correct aria-expanded attribute', () => {
      const state1 = setupChatStore({ sidebarCollapsed: false });
      const { rerender } = renderWithChatStore(<ChatContent />, { initialState: state1 });

      let button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'true');

      // Collapse - setState BEFORE rerender
      useChatStore.setState({ sidebarCollapsed: true });
      rerender(<ChatContent />);

      button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'false');
    });

    it('has correct title attribute', () => {
      const state = setupChatStore({ sidebarCollapsed: false });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('title', 'Nascondi sidebar');
    });

    it('updates icon when sidebar state changes', () => {
      const state1 = setupChatStore({ sidebarCollapsed: false });
      const { rerender } = renderWithChatStore(<ChatContent />, { initialState: state1 });

      expect(screen.getByText('✕')).toBeInTheDocument();

      // Collapse - setState BEFORE rerender
      useChatStore.setState({ sidebarCollapsed: true });
      rerender(<ChatContent />);

      expect(screen.queryByText('✕')).not.toBeInTheDocument();
      expect(screen.getByText('☰')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Error Message Display
   */
  describe('Error Message Display', () => {
    it('does not display error when error is null', () => {
      const state = setupChatStore({ error: null });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('displays error message when error is set', () => {
      const errorMessage = 'Failed to send message';
      const state = setupChatStore({ error: errorMessage });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('alert')).toBeInTheDocument();
      expect(screen.getByText(errorMessage)).toBeInTheDocument();
    });

    it('has correct aria-live attribute', () => {
      const state = setupChatStore({ error: 'Error occurred' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });

    it('updates when error message changes', () => {
      const state1 = setupChatStore({ error: 'First error' });
      const { rerender } = renderWithChatStore(<ChatContent />, { initialState: state1 });

      expect(screen.getByText('First error')).toBeInTheDocument();

      // Change error - setState BEFORE rerender
      useChatStore.setState({ error: 'Second error' });
      rerender(<ChatContent />);

      expect(screen.queryByText('First error')).not.toBeInTheDocument();
      expect(screen.getByText('Second error')).toBeInTheDocument();
    });

    it('removes error when error is cleared', () => {
      const state1 = setupChatStore({ error: 'Error occurred' });
      const { rerender } = renderWithChatStore(<ChatContent />, { initialState: state1 });

      expect(screen.getByRole('alert')).toBeInTheDocument();

      // Clear error - setState BEFORE rerender
      useChatStore.setState({ error: null });
      rerender(<ChatContent />);

      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    });

    it('handles very long error messages', () => {
      const longError = 'E'.repeat(500);
      const state = setupChatStore({ error: longError });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText(longError)).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Component Integration
   */
  describe('Component Integration', () => {
    it('renders MessageList and MessageInput together', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByTestId('message-list')).toBeInTheDocument();
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });

    it('passes store to child components', () => {
      const games: Game[] = [{ id: 'game-1', name: 'Chess' }];
      const state = setupChatStore({ games, selectedGameId: 'game-1' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      // Components should render with store access
      expect(screen.getByTestId('message-list')).toBeInTheDocument();
      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles empty games array', () => {
      const state = setupChatStore({ games: [], selectedGameId: null });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('handles empty threads array', () => {
      const state = setupChatStore({ threads: [], activeChatId: null });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Seleziona o crea un thread')).toBeInTheDocument();
    });

    it('handles undefined activeChatId', () => {
      const state = setupChatStore({ activeChatId: undefined });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Seleziona o crea un thread')).toBeInTheDocument();
    });

    it('handles undefined selectedGameId', () => {
      const state = setupChatStore({ selectedGameId: undefined });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();
    });

    it('handles thread with missing title', () => {
      const thread = createMockChatThread({ id: 'chat-1', title: undefined as any });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText('Chat Thread')).toBeInTheDocument();
    });

    it('handles game with special characters in name', () => {
      const games: Game[] = [{ id: 'game-1', name: "Catan: Trader's & Barbarians" }];
      const state = setupChatStore({ games, selectedGameId: 'game-1' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByText("Catan: Trader's & Barbarians")).toBeInTheDocument();
    });

    it('handles rapid sidebar toggles', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      // Spy on the store's toggleSidebar AFTER render
      const toggleSpy = jest.spyOn(useChatStore.getState(), 'toggleSidebar');

      const button = screen.getByRole('button', { name: /sidebar/i });
      for (let i = 0; i < 5; i++) {
        fireEvent.click(button);
      }

      expect(toggleSpy).toHaveBeenCalledTimes(5);
    });

    it('handles null games array', () => {
      const state = setupChatStore({ games: [], selectedGameId: null });

      // Should not crash, shows default state
      expect(() => renderWithChatStore(<ChatContent />, { initialState: state })).not.toThrow();
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('uses semantic heading element', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      const heading = screen.getByRole('heading');
      expect(heading.tagName).toBe('H1');
    });

    it('has proper button aria-labels', () => {
      const state = setupChatStore({ sidebarCollapsed: false });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('button', { name: 'Hide sidebar' })).toBeInTheDocument();
    });

    it('uses aria-expanded for toggle button', () => {
      const state = setupChatStore({ sidebarCollapsed: false });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const button = screen.getByRole('button', { name: /sidebar/i });
      expect(button).toHaveAttribute('aria-expanded', 'true');
    });

    it('provides error context with role alert', () => {
      const state = setupChatStore({ error: 'Error' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('alert')).toBeInTheDocument();
    });

    it('uses aria-live for error announcements', () => {
      const state = setupChatStore({ error: 'Error' });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const alert = screen.getByRole('alert');
      expect(alert).toHaveAttribute('aria-live', 'polite');
    });

    it('has aria-label for archived badge', () => {
      const thread = createMockChatThread({ id: 'chat-1', status: 'Closed' });
      const state = setupChatStore({ activeChatId: 'chat-1', threads: [thread] });
      renderWithChatStore(<ChatContent />, { initialState: state });

      const badge = screen.getByText('Archiviato');
      expect(badge).toHaveAttribute('aria-label', 'Archived thread');
    });
  });

  /**
   * Test Group: Layout
   */
  describe('Layout', () => {
    it('maintains header at top', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByRole('heading')).toBeInTheDocument();
    });

    it('maintains input at bottom', () => {
      const state = setupChatStore();
      renderWithChatStore(<ChatContent />, { initialState: state });

      expect(screen.getByTestId('message-input')).toBeInTheDocument();
    });
  });
});