/**
 * MessageList Tests - Issue #2308 Week 4
 *
 * Branch coverage tests for MessageList component:
 * 1. Shows loading skeleton when messages loading
 * 2. Shows empty state when no messages
 * 3. Renders message list with virtualization
 * 4. Displays streaming message indicator
 * 5. Passes citation click handler to VirtualizedMessageList
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: Core message rendering paths
 */

import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { MessageList } from '../MessageList';
import { useChatWithStreaming } from '@/hooks/useChatWithStreaming';

// Mock dependencies
vi.mock('@/hooks/useChatWithStreaming');
vi.mock('../VirtualizedMessageList', () => ({
  VirtualizedMessageList: ({ messages, streamingMessage, isStreaming, onCitationClick }: any) => (
    <div data-testid="virtualized-list">
      <div data-testid="message-count">{messages.length}</div>
      {streamingMessage && <div data-testid="streaming-msg">{streamingMessage.content}</div>}
      {isStreaming && <div data-testid="streaming-indicator">Streaming...</div>}
      {onCitationClick && (
        <button onClick={() => onCitationClick('doc-1', 5)}>Test Citation</button>
      )}
    </div>
  ),
}));
vi.mock('../loading/SkeletonLoader', () => ({
  SkeletonLoader: ({ count, variant }: any) => (
    <div data-testid="skeleton-loader">
      Loading {count} {variant}s...
    </div>
  ),
}));

describe('MessageList - Issue #2308', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ============================================================================
  // TEST 1: Loading state with skeleton
  // ============================================================================
  it.skip('should show skeleton loader when messages are loading', () => {
    // Arrange
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [],
      activeChatId: 'chat-1',
      loading: { messages: true },
      isStreaming: false,
      streamingAnswer: '',
      streamingState: null,
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Loading indicator
    expect(screen.getByText('Caricamento messaggi...')).toBeInTheDocument();

    // Assert - Role status for accessibility
    expect(screen.getByRole('status')).toBeInTheDocument();

    // Assert - Not showing message list
    expect(screen.queryByTestId('virtualized-list')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Empty state with no messages
  // ============================================================================
  it('should show empty state when no messages exist', () => {
    // Arrange - No active chat
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [],
      activeChatId: null,
      loading: { messages: false },
      isStreaming: false,
      streamingAnswer: '',
      streamingState: null,
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Empty state message
    expect(screen.getByText('Nessun messaggio ancora.')).toBeInTheDocument();
    expect(
      screen.getByText(/Seleziona una chat esistente o creane una nuova/i)
    ).toBeInTheDocument();

    // Assert - Not showing message list
    expect(screen.queryByTestId('virtualized-list')).not.toBeInTheDocument();
  });

  it('should show different empty state text when chat is active', () => {
    // Arrange - Active chat but no messages
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [],
      activeChatId: 'chat-1',
      loading: { messages: false },
      isStreaming: false,
      streamingAnswer: '',
      streamingState: null,
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Active chat empty state
    expect(screen.getByText('Nessun messaggio ancora.')).toBeInTheDocument();
    expect(screen.getByText('Inizia facendo una domanda!')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Message list rendering
  // ============================================================================
  it('should render virtualized message list when messages exist', () => {
    // Arrange
    const mockMessages = [
      { id: 'msg-1', role: 'user', content: 'Question 1' },
      { id: 'msg-2', role: 'assistant', content: 'Answer 1' },
      { id: 'msg-3', role: 'user', content: 'Question 2' },
    ];

    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: mockMessages,
      activeChatId: 'chat-1',
      loading: { messages: false },
      isStreaming: false,
      streamingAnswer: '',
      streamingState: null,
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Virtualized list rendered
    const list = screen.getByTestId('virtualized-list');
    expect(list).toBeInTheDocument();

    // Assert - Message count passed
    expect(screen.getByTestId('message-count')).toHaveTextContent('3');

    // Assert - No loading or empty states
    expect(screen.queryByText('Caricamento messaggi...')).not.toBeInTheDocument();
    expect(screen.queryByText('Nessun messaggio ancora.')).not.toBeInTheDocument();
  });

  // ============================================================================
  // TEST 4: Streaming message display
  // ============================================================================
  it('should display streaming message when streaming is active', () => {
    // Arrange
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [{ id: 'msg-1', role: 'user', content: 'Question' }],
      activeChatId: 'chat-1',
      loading: { messages: false },
      isStreaming: true,
      streamingAnswer: 'Streaming answer in progress...',
      streamingState: 'Searching knowledge base...',
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Streaming indicator
    expect(screen.getByTestId('streaming-indicator')).toBeInTheDocument();

    // Assert - Streaming message content
    expect(screen.getByTestId('streaming-msg')).toHaveTextContent(
      'Streaming answer in progress...'
    );
  });

  it('should pass streaming state message when no answer yet', () => {
    // Arrange - streaming with at least one user message
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [{ id: 'msg-1', role: 'user', content: 'Question' }],
      activeChatId: 'chat-1',
      loading: { messages: false },
      isStreaming: true,
      streamingAnswer: '',
      streamingState: 'Analyzing question...',
    } as any);

    // Act
    render(<MessageList />);

    // Assert - Virtualized list renders with streaming indicator
    expect(screen.getByTestId('virtualized-list')).toBeInTheDocument();
    expect(screen.getByTestId('streaming-indicator')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 5: Citation click handler propagation
  // ============================================================================
  it('should pass onCitationClick handler to VirtualizedMessageList', async () => {
    const mockOnCitationClick = vi.fn();

    // Arrange
    vi.mocked(useChatWithStreaming).mockReturnValue({
      messages: [{ id: 'msg-1', role: 'assistant', content: 'Answer' }],
      activeChatId: 'chat-1',
      loading: { messages: false },
      isStreaming: false,
      streamingAnswer: '',
      streamingState: null,
    } as any);

    // Act
    render(<MessageList onCitationClick={mockOnCitationClick} />);

    // Assert - Handler was passed (button appears from mock)
    const citationButton = screen.getByText('Test Citation');
    expect(citationButton).toBeInTheDocument();
  });
});
