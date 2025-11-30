/**
 * Unit tests for UIProvider
 * Comprehensive coverage of UI state management
 */

import { renderHook, act } from '@testing-library/react';
import { UIProvider, useUI } from '@/components/ui/UIProvider';
import { ChatProvider, useChat } from '@/components/chat/ChatProvider';
import { GameProvider } from '@/components/game/GameProvider';
import { api } from '@/lib/api';
import React, { PropsWithChildren } from 'react';

// Mock api module
vi.mock('@/lib/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
    delete: vi.fn(),
    chat: {
      updateMessage: vi.fn(),
      deleteMessage: vi.fn(),
    },
  },
}));

const mockApi = api as Mocked<typeof api>;

describe('UIProvider', () => {
  // UIProvider needs to be nested under GameProvider and ChatProvider
  const wrapper = ({ children }: PropsWithChildren) => (
    <GameProvider>
      <ChatProvider>
        <UIProvider>{children}</UIProvider>
      </ChatProvider>
    </GameProvider>
  );

  beforeEach(() => {
    vi.clearAllMocks();
    // Mock empty responses for GameProvider and ChatProvider
    mockApi.get.mockResolvedValue([]);
  });

  describe('Sidebar State', () => {
    it('initializes with sidebar expanded', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      expect(result.current.sidebarCollapsed).toBe(false);
    });

    it('toggles sidebar state', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarCollapsed).toBe(true);

      act(() => {
        result.current.toggleSidebar();
      });

      expect(result.current.sidebarCollapsed).toBe(false);
    });
  });

  describe('Message Editing State', () => {
    it('initializes with no message being edited', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('starts editing message', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.startEdit('msg-1', 'Original content');
      });

      expect(result.current.editingMessageId).toBe('msg-1');
      expect(result.current.editContent).toBe('Original content');
    });

    it('cancels editing', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.startEdit('msg-1', 'Original content');
      });

      act(() => {
        result.current.cancelEdit();
      });

      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('updates edit content', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.startEdit('msg-1', 'Original');
      });

      act(() => {
        result.current.setEditContent('Modified content');
      });

      expect(result.current.editContent).toBe('Modified content');
      expect(result.current.editingMessageId).toBe('msg-1');
    });

    it('saves edit and clears state', async () => {
      const mockEditMessage = vi.fn().mockResolvedValue(undefined);

      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.startEdit('msg-1', 'Original');
        result.current.setEditContent('Updated');
      });

      await act(async () => {
        await result.current.saveEdit(mockEditMessage);
      });

      expect(mockEditMessage).toHaveBeenCalledWith('msg-1', 'Updated');
      expect(result.current.editingMessageId).toBeNull();
      expect(result.current.editContent).toBe('');
    });

    it('does not save if no message is being edited', async () => {
      const mockEditMessage = vi.fn();

      const { result } = renderHook(() => useUI(), { wrapper });

      await act(async () => {
        await result.current.saveEdit(mockEditMessage);
      });

      expect(mockEditMessage).not.toHaveBeenCalled();
    });

    it('handles save edit failure', async () => {
      const mockEditMessage = vi.fn().mockRejectedValue(new Error('Save failed'));

      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.startEdit('msg-1', 'Original');
      });

      await act(async () => {
        try {
          await result.current.saveEdit(mockEditMessage);
        } catch (err) {
          // Expected to throw
        }
      });

      // Edit state should remain since save failed
      expect(result.current.editingMessageId).toBe('msg-1');
    });
  });

  describe('Input Value State', () => {
    it('initializes with empty input', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      expect(result.current.inputValue).toBe('');
    });

    it('updates input value', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.setInputValue('User message');
      });

      expect(result.current.inputValue).toBe('User message');
    });

    it('clears input value', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.setInputValue('Some text');
        result.current.setInputValue('');
      });

      expect(result.current.inputValue).toBe('');
    });
  });

  describe('Search Mode State', () => {
    it('initializes with Hybrid search mode', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      expect(result.current.searchMode).toBe('Hybrid');
    });

    it('changes search mode', () => {
      const { result } = renderHook(() => useUI(), { wrapper });

      act(() => {
        result.current.setSearchMode('Vector');
      });

      expect(result.current.searchMode).toBe('Vector');

      act(() => {
        result.current.setSearchMode('Keyword');
      });

      expect(result.current.searchMode).toBe('Keyword');
    });
  });

  describe('error handling', () => {
    it('throws error if useUI used outside UIProvider', () => {
      const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

      expect(() => {
        renderHook(() => useUI());
      }).toThrow('useUI must be used within UIProvider');

      consoleSpy.mockRestore();
    });
  });
});
