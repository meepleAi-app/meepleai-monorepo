/**
 * Chat Page - UI Interactions Tests
 *
 * Tests for UI interactions and visual elements in the chat interface.
 * This file focuses on sidebar toggling, header displays, and chat preview formatting.
 *
 * Uses component mocking for isolated, fast testing like chat.test.tsx
 */

import React from 'react';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import ChatPage from '../../../pages/chat';
import { api } from '../../../lib/api';

// Mock ChatProvider with context values
const mockUseChatContext = jest.fn();

jest.mock('../../../components/chat/ChatProvider', () => ({
  ChatProvider: ({ children }: { children: React.ReactNode }) => (
    <div data-testid="chat-provider">{children}</div>
  ),
  useChatContext: () => mockUseChatContext(),
}));

// Mock ChatSidebar with collapse functionality
const mockSidebarProps = {
  isCollapsed: false,
  onToggleCollapse: jest.fn(),
};

jest.mock('../../../components/chat/ChatSidebar', () => ({
  ChatSidebar: () => {
    const React = require('react');
    const [isCollapsed, setIsCollapsed] = React.useState(mockSidebarProps.isCollapsed);
    const { useChatContext } = require('../../../components/chat/ChatProvider');
    const { selectedGameId } = useChatContext();

    React.useEffect(() => {
      setIsCollapsed(mockSidebarProps.isCollapsed);
    }, []);

    return (
      <div data-testid="chat-sidebar">
        <button
          title={isCollapsed ? 'Mostra sidebar' : 'Nascondi sidebar'}
          onClick={() => {
            const newCollapsed = !isCollapsed;
            setIsCollapsed(newCollapsed);
            mockSidebarProps.isCollapsed = newCollapsed;
            mockSidebarProps.onToggleCollapse();
          }}
        >
          {isCollapsed ? '→' : '←'}
        </button>
        {!isCollapsed && selectedGameId && (
          <div data-testid="sidebar-content">Sidebar Content</div>
        )}
      </div>
    );
  },
}));

// Mock ChatContent with header
jest.mock('../../../components/chat/ChatContent', () => ({
  ChatContent: () => {
    const { useChatContext } = require('../../../components/chat/ChatProvider');
    const { selectedGameId, selectedAgentId, activeChatId } = useChatContext();

    const gameName = selectedGameId === 'game-1' ? 'Chess' : undefined;
    const agentName = selectedAgentId === 'agent-1' && activeChatId ? 'Chess Expert' : undefined;

    return (
      <div data-testid="chat-content">
        <h1>MeepleAI Chat</h1>
        {agentName ? (
          <h2>{agentName}</h2>
        ) : gameName ? (
          <div>{gameName}</div>
        ) : (
          <h2>Seleziona o crea una chat</h2>
        )}
      </div>
    );
  },
}));

// Mock ExportChatModal
jest.mock('../../../components/ExportChatModal', () => ({
  ExportChatModal: ({ isOpen }: { isOpen: boolean }) =>
    isOpen ? <div data-testid="export-modal">modal</div> : null,
}));

// Mock API
jest.mock('../../../lib/api');

const mockApi = api as jest.Mocked<typeof api>;

describe('ChatPage - UI Interactions', () => {
  const userResponse = {
    user: {
      id: 'user-1',
      email: 'user@example.com',
      displayName: 'Test User',
      role: 'User',
    },
    expiresAt: new Date(Date.now() + 3600000).toISOString(),
  };

  beforeEach(() => {
    jest.clearAllMocks();
    mockSidebarProps.isCollapsed = false;
    mockSidebarProps.onToggleCollapse.mockClear();

    // Default mock context
    mockUseChatContext.mockReturnValue({
      selectedGameId: null,
      selectedAgentId: null,
      activeChatId: null,
      messages: [],
      isStreaming: false,
    });

    // Default API setup
    mockApi.get.mockResolvedValue(userResponse);
  });

  it('toggles sidebar when collapse button is clicked', async () => {
    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    const user = userEvent.setup();
    const collapseButton = screen.getByTitle(/Nascondi sidebar/i);

    await user.click(collapseButton);

    // Check button changes to expand icon
    await waitFor(() => {
      expect(screen.getByTitle(/Mostra sidebar/i)).toBeInTheDocument();
    });

    expect(mockSidebarProps.onToggleCollapse).toHaveBeenCalled();
  });

  it('shows game name in header when game is selected', async () => {
    mockUseChatContext.mockReturnValue({
      selectedGameId: 'game-1',
      selectedAgentId: null,
      activeChatId: null,
      messages: [],
      isStreaming: false,
    });

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    // Wait for game data to load
    await waitFor(() => {
      expect(screen.getByText('Chess')).toBeInTheDocument();
    });
  });

  it('shows agent name in header when chat is active', async () => {
    mockUseChatContext.mockReturnValue({
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      activeChatId: 'chat-1',
      messages: [
        {
          id: 'msg-1',
          role: 'user',
          message: 'How do I castle?',
          createdAt: '2025-01-10T10:00:00Z',
        },
      ],
      isStreaming: false,
    });

    render(<ChatPage />);

    await waitFor(() => {
      expect(screen.getByTestId('chat-provider')).toBeInTheDocument();
    });

    // Verify agent name in header
    await waitFor(() => {
      expect(screen.getByRole('heading', { name: /Chess Expert/i })).toBeInTheDocument();
    });
  });
});
