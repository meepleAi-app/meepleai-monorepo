/**
 * ChatSidebar Tests - Issue #2308 Week 4
 *
 * Branch coverage tests for ChatSidebar component:
 * 1. Renders game selector, agent selector, thread button
 * 2. Disables create button when no game/agent selected
 * 3. Shows thread limit indicator when at max threads
 * 4. Handles create thread click
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 105 lines (~2% of total)
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { ChatSidebar } from '../ChatSidebar';
import { useChatStore } from '@/store/chat/store';

// Mock dependencies
vi.mock('@/store/chat/store');
vi.mock('../GameSelector', () => ({
  GameSelector: () => <div data-testid="game-selector">Game Selector</div>,
}));
vi.mock('../AgentSelector', () => ({
  AgentSelector: () => <div data-testid="agent-selector">Agent Selector</div>,
}));
vi.mock('../ChatHistory', () => ({
  ChatHistory: () => <div data-testid="chat-history">Chat History</div>,
}));
vi.mock('../loading/LoadingButton', () => ({
  LoadingButton: ({ children, onClick, disabled, isLoading, loadingText, ...props }: any) => (
    <button onClick={onClick} disabled={disabled} {...props}>
      {isLoading ? loadingText : children}
    </button>
  ),
}));

const mockChats = [
  { id: 'thread-1', status: 'Active', title: 'Thread 1' },
  { id: 'thread-2', status: 'Active', title: 'Thread 2' },
  { id: 'thread-3', status: 'Closed', title: 'Archived Thread' },
];

describe('ChatSidebar - Issue #2308', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // TEST 1: Basic rendering
  // ============================================================================
  it('should render all sidebar components', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: {},
      selectedGameId: null,
      selectedAgentId: null,
      loading: { creating: false },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert
    expect(screen.getByTestId('game-selector')).toBeInTheDocument();
    expect(screen.getByTestId('agent-selector')).toBeInTheDocument();
    expect(screen.getByTestId('chat-history')).toBeInTheDocument();
    expect(screen.getByLabelText('Create new thread')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Create button disabled when no game/agent selected
  // ============================================================================
  it('should disable create button when no game selected', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: {},
      selectedGameId: null,
      selectedAgentId: 'agent-1',
      loading: { creating: false },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert
    const createButton = screen.getByLabelText('Create new thread');
    expect(createButton).toBeDisabled();
  });

  it('should disable create button when no agent selected', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: {},
      selectedGameId: 'game-1',
      selectedAgentId: null,
      loading: { creating: false },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert
    const createButton = screen.getByLabelText('Create new thread');
    expect(createButton).toBeDisabled();
  });

  // ============================================================================
  // TEST 3: Thread limit indicator
  // ============================================================================
  it('should show thread limit warning when at max active threads', () => {
    // Arrange - 5 active threads (at limit)
    const fiveActiveThreads = Array.from({ length: 5 }, (_, i) => ({
      id: `thread-${i}`,
      status: 'Active' as const,
      title: `Thread ${i}`,
    }));

    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: { 'game-1': fiveActiveThreads },
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      loading: { creating: false },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert - Limit indicator visible
    expect(screen.getByText('5 / 5 thread attivi')).toBeInTheDocument();
    expect(screen.getByText('(thread più vecchio sarà archiviato)')).toBeInTheDocument();

    // Assert - Warning styling applied
    const limitText = screen.getByText('5 / 5 thread attivi');
    expect(limitText).toHaveClass('text-[#d93025]', 'font-semibold');
  });

  it('should show normal thread count when below limit', () => {
    // Arrange - 2 active threads (below limit)
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: { 'game-1': mockChats },
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      loading: { creating: false },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert - Normal count displayed (2 active out of 3 total)
    expect(screen.getByText('2 / 5 thread attivi')).toBeInTheDocument();

    // Assert - No warning message
    expect(screen.queryByText('(thread più vecchio sarà archiviato)')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Create thread interaction
  // ============================================================================
  it('should call createChat when create button clicked', async () => {
    const user = userEvent.setup();
    const mockCreateChat = vi.fn();

    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: { 'game-1': [] },
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      loading: { creating: false },
      createChat: mockCreateChat,
    } as any);

    // Act
    render(<ChatSidebar />);
    const createButton = screen.getByLabelText('Create new thread');
    await user.click(createButton);

    // Assert
    expect(mockCreateChat).toHaveBeenCalledTimes(1);
  });

  // ============================================================================
  // TEST 5: Loading state during thread creation
  // ============================================================================
  it('should show loading text and disable button during thread creation', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      chatsByGame: { 'game-1': [] },
      selectedGameId: 'game-1',
      selectedAgentId: 'agent-1',
      loading: { creating: true },
      createChat: vi.fn(),
    } as any);

    // Act
    render(<ChatSidebar />);

    // Assert - Loading text shown
    expect(screen.getByText('Creazione...')).toBeInTheDocument();

    // Assert - Button disabled during loading
    const createButton = screen.getByLabelText('Create new thread');
    expect(createButton).toBeDisabled();
  });
});
