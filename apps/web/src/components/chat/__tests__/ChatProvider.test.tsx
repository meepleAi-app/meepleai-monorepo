/**
 * Tests for ChatProvider
 * Comprehensive testing of chat state management
 */

import { render, screen, waitFor, act } from '@testing-library/react';
import { renderHook } from '@testing-library/react';
import { ChatProvider, useChatContext } from '../ChatProvider';

describe('ChatProvider', () => {
  describe('Context Initialization', () => {
    it('should throw error when useChatContext is used outside provider', () => {
      // Suppress console.error for this test
      const consoleError = jest.spyOn(console, 'error').mockImplementation();

      expect(() => {
        renderHook(() => useChatContext());
      }).toThrow('useChatContext must be used within ChatProvider');

      consoleError.mockRestore();
    });

    it('should provide context when used within provider', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current).toBeDefined();
      expect(result.current.authUser).toBeNull();
      expect(result.current.games).toEqual([]);
      expect(result.current.selectedGameId).toBeNull();
    });
  });

  describe('Initial State', () => {
    it('should have correct initial authentication state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.authUser).toBeNull();
    });

    it('should have correct initial game selection state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.games).toEqual([]);
      expect(result.current.selectedGameId).toBeNull();
      expect(result.current.agents).toEqual([]);
      expect(result.current.selectedAgentId).toBeNull();
    });

    it('should have correct initial chat state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.chats).toEqual([]);
      expect(result.current.activeChatId).toBeNull();
      expect(result.current.messages).toEqual([]);
    });

    it('should have correct initial UI state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.loading).toEqual({
        games: false,
        agents: false,
        chats: false,
        messages: false,
        sending: false,
        creating: false,
        updating: false,
        deleting: false,
      });
      expect(result.current.errorMessage).toBe('');
      expect(result.current.sidebarCollapsed).toBe(false);
      expect(result.current.inputValue).toBe('');
    });

    it('should have correct initial message edit state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });
  });

  describe('UI Actions', () => {
    it('should toggle sidebar collapsed state', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.sidebarCollapsed).toBe(false);

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarCollapsed).toBe(true);

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarCollapsed).toBe(false);
    });

    it('should update input value', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.inputValue).toBe('');

      act(() => {
        result.current.setInputValue('test message');
      });

      expect(result.current.inputValue).toBe('test message');
    });

    it('should start editing a message', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');

      act(() => {
        result.current.startEditMessage('msg-123', 'Original content');
      });

      expect(result.current.editingMessageId).toBe('msg-123');
      expect(result.current.editContent).toBe('Original content');
    });

    it('should cancel editing', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      act(() => {
        result.current.startEditMessage('msg-123', 'Original content');
      });

      expect(result.current.editingMessageId).toBe('msg-123');

      act(() => {
        result.current.cancelEdit();
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });
  });

  describe('Game Selection', () => {
    it('should update selectedGameId when selectGame is called', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.selectedGameId).toBeNull();

      act(() => {
        result.current.selectGame('game-123');
      });

      expect(result.current.selectedGameId).toBe('game-123');
    });

    it('should update selectedAgentId when selectAgent is called', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      expect(result.current.selectedAgentId).toBeNull();

      act(() => {
        result.current.selectAgent('agent-456');
      });

      expect(result.current.selectedAgentId).toBe('agent-456');
    });
  });

  describe('Type Safety', () => {
    it('should have proper TypeScript types', () => {
      const { result } = renderHook(() => useChatContext(), {
        wrapper: ChatProvider,
      });

      // These should compile without errors due to proper typing
      const authUser = result.current.authUser;
      const games = result.current.games;
      const loading = result.current.loading;
      const selectGame = result.current.selectGame;

      // Verify types are correct (runtime checks)
      expect(typeof selectGame).toBe('function');
      expect(Array.isArray(games)).toBe(true);
      expect(typeof loading).toBe('object');
    });
  });

  describe('Component Integration', () => {
    it('should render children components', () => {
      render(
        <ChatProvider>
          <div data-testid="test-child">Child Component</div>
        </ChatProvider>
      );

      expect(screen.getByTestId('test-child')).toBeInTheDocument();
      expect(screen.getByText('Child Component')).toBeInTheDocument();
    });

    it('should allow multiple children to access context', () => {
      function TestChild1() {
        const { sidebarCollapsed } = useChatContext();
        return <div data-testid="child1">{sidebarCollapsed ? 'collapsed' : 'expanded'}</div>;
      }

      function TestChild2() {
        const { toggleSidebar } = useChatContext();
        return <button data-testid="toggle-btn" onClick={toggleSidebar}>Toggle</button>;
      }

      render(
        <ChatProvider>
          <TestChild1 />
          <TestChild2 />
        </ChatProvider>
      );

      expect(screen.getByTestId('child1')).toHaveTextContent('expanded');

      act(() => {
        screen.getByTestId('toggle-btn').click();
      });

      expect(screen.getByTestId('child1')).toHaveTextContent('collapsed');
    });
  });
});

describe('ChatProvider API Stubs', () => {
  // These tests verify that the stub methods are callable
  // Implementation tests will be added when methods are fully implemented

  it('should have createChat stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
    });

    await act(async () => {
      await result.current.createChat();
    });

    expect(consoleLog).toHaveBeenCalledWith('createChat - to be implemented');
    consoleLog.mockRestore();
  });

  it('should have deleteChat stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
      });

    await act(async () => {
      await result.current.deleteChat('chat-123');
    });

    expect(consoleLog).toHaveBeenCalledWith('deleteChat - to be implemented', 'chat-123');
    consoleLog.mockRestore();
  });

  it('should have sendMessage stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
    });

    await act(async () => {
      await result.current.sendMessage('test message');
    });

    expect(consoleLog).toHaveBeenCalledWith('sendMessage - to be implemented', 'test message');
    consoleLog.mockRestore();
  });

  it('should have setMessageFeedback stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
    });

    await act(async () => {
      await result.current.setMessageFeedback('msg-123', 'helpful');
    });

    expect(consoleLog).toHaveBeenCalledWith('setMessageFeedback - to be implemented', 'msg-123', 'helpful');
    consoleLog.mockRestore();
  });

  it('should have editMessage stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
    });

    await act(async () => {
      await result.current.editMessage('msg-123', 'updated content');
    });

    expect(consoleLog).toHaveBeenCalledWith('editMessage - to be implemented', 'msg-123', 'updated content');
    consoleLog.mockRestore();
  });

  it('should have deleteMessage stub', async () => {
    const consoleLog = jest.spyOn(console, 'log').mockImplementation();

    const { result } = renderHook(() => useChatContext(), {
      wrapper: ChatProvider,
    });

    await act(async () => {
      await result.current.deleteMessage('msg-123');
    });

    expect(consoleLog).toHaveBeenCalledWith('deleteMessage - to be implemented', 'msg-123');
    consoleLog.mockRestore();
  });
});
