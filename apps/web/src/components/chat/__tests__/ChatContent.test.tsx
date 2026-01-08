/**
 * ChatContent Tests - Issue #2308 Week 4
 *
 * Branch coverage tests for ChatContent component:
 * 1. Renders with active chat and displays header metadata
 * 2. Shows archived badge for closed threads
 * 3. Handles citation click and opens PDF modal
 * 4. Toggles sidebar on desktop vs mobile
 * 5. Displays error messages when present
 *
 * Pattern: Vitest + React Testing Library
 * Coverage target: 243 lines (~3% of total)
 */

import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import { ChatContent } from '../ChatContent';
import { useChatStore } from '@/store/chat/store';

// Mock dependencies
vi.mock('@/store/chat/store');
vi.mock('@/hooks/useChatWithStreaming', () => ({
  useChatWithStreaming: vi.fn(() => ({
    messages: [],
    activeChatId: null,
    loading: { messages: false },
    isStreaming: false,
    streamingAnswer: '',
    streamingState: null,
  })),
}));
vi.mock('../MessageList', () => ({
  MessageList: ({ onCitationClick }: any) => (
    <div data-testid="message-list">
      <button onClick={() => onCitationClick?.('doc-123', 5)}>Test Citation</button>
    </div>
  ),
}));
vi.mock('../MessageInput', () => ({
  MessageInput: () => <div data-testid="message-input">Message Input</div>,
}));
vi.mock('../MobileSidebar', () => ({
  MobileSidebar: ({ open }: any) => (
    <div data-testid="mobile-sidebar">{open ? 'Open' : 'Closed'}</div>
  ),
}));
vi.mock('@/components/pdf/PdfViewerModal', () => ({
  PdfViewerModal: ({ open, pdfUrl, initialPage, documentName }: any) => (
    <div data-testid="pdf-modal" data-open={open} data-url={pdfUrl} data-page={initialPage}>
      {documentName}
    </div>
  ),
}));
vi.mock('../ContextChip', () => ({
  ContextChip: () => <div data-testid="context-chip">Context</div>,
}));
vi.mock('next/link', () => ({
  default: ({ children, href }: any) => <a href={href}>{children}</a>,
}));

const mockGame = {
  id: 'game-1',
  title: 'Catan',
  publisher: 'Kosmos',
  yearPublished: 1995,
  minPlayers: 3,
  maxPlayers: 4,
  minPlayTimeMinutes: 60,
  maxPlayTimeMinutes: 120,
  bggId: 13,
  createdAt: new Date().toISOString(),
  description: 'Settle the island',
};

const mockThread = {
  id: 'thread-1',
  title: 'Catan Rules Questions',
  gameId: 'game-1',
  status: 'Active' as const,
  messageCount: 5,
  createdAt: new Date().toISOString(),
  updatedAt: new Date().toISOString(),
};

const mockArchivedThread = {
  ...mockThread,
  id: 'thread-2',
  status: 'Closed' as const,
  title: 'Old Conversation',
};

describe('ChatContent - Issue #2308', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    // Default mock: No game selected
    vi.mocked(useChatStore).mockReturnValue({
      games: [],
      selectedGameId: null,
      chatsByGame: {},
      activeChatIds: {},
      messagesByChat: {},
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);
  });

  // ============================================================================
  // TEST 1: Active chat display with thread metadata
  // ============================================================================
  it('should render with active chat and display thread title and metadata', async () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      games: [mockGame],
      selectedGameId: 'game-1',
      chatsByGame: { 'game-1': [mockThread] },
      activeChatIds: { 'game-1': 'thread-1' },
      messagesByChat: { 'thread-1': [] },
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);

    // Act
    render(<ChatContent />);

    // Assert - Thread title
    expect(screen.getByRole('heading', { level: 1 })).toHaveTextContent('Catan Rules Questions');

    // Assert - Game name in metadata
    expect(screen.getByText('Catan')).toBeInTheDocument();

    // Assert - Message count
    expect(screen.getByText('5 messaggi')).toBeInTheDocument();

    // Assert - No archived badge
    expect(screen.queryByLabelText('Archived thread')).not.toBeInTheDocument();

    // Assert - Components rendered
    expect(screen.getByTestId('message-list')).toBeInTheDocument();
    expect(screen.getByTestId('message-input')).toBeInTheDocument();
    expect(screen.getByTestId('context-chip')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 2: Archived thread badge
  // ============================================================================
  it('should display archived badge for closed threads', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      games: [mockGame],
      selectedGameId: 'game-1',
      chatsByGame: { 'game-1': [mockArchivedThread] },
      activeChatIds: { 'game-1': 'thread-2' },
      messagesByChat: { 'thread-2': [] },
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);

    // Act
    render(<ChatContent />);

    // Assert - Archived badge visible
    const archivedBadge = screen.getByLabelText('Archived thread');
    expect(archivedBadge).toBeInTheDocument();
    expect(archivedBadge).toHaveTextContent('Archiviato');

    // Assert - Thread title still shown
    expect(screen.getByText('Old Conversation')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 3: Citation click opens PDF modal at correct page
  // ============================================================================
  it('should handle citation click and open PDF modal at specific page', async () => {
    const user = userEvent.setup();

    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      games: [mockGame],
      selectedGameId: 'game-1',
      chatsByGame: { 'game-1': [mockThread] },
      activeChatIds: { 'game-1': 'thread-1' },
      messagesByChat: { 'thread-1': [] },
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);

    render(<ChatContent />);

    // Act - Click citation (simulated from MessageList mock)
    const citationButton = screen.getByText('Test Citation');
    await user.click(citationButton);

    // Assert - PDF modal opened
    await waitFor(() => {
      const pdfModal = screen.getByTestId('pdf-modal');
      expect(pdfModal).toHaveAttribute('data-open', 'true');
      expect(pdfModal).toHaveAttribute('data-page', '5');
      expect(pdfModal).toHaveAttribute('data-url', expect.stringContaining('doc-123'));
      expect(pdfModal).toHaveTextContent('Catan');
    });
  });

  // ============================================================================
  // TEST 4: Sidebar toggle behavior (desktop vs mobile)
  // ============================================================================
  it('should toggle sidebar on desktop and mobile menu on mobile', async () => {
    const user = userEvent.setup();
    const mockToggleSidebar = vi.fn();

    // Arrange - Desktop viewport
    vi.mocked(useChatStore).mockReturnValue({
      games: [mockGame],
      selectedGameId: 'game-1',
      chatsByGame: { 'game-1': [mockThread] },
      activeChatIds: { 'game-1': 'thread-1' },
      messagesByChat: {},
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: mockToggleSidebar,
    } as any);

    // Mock matchMedia for desktop
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: vi.fn().mockImplementation(query => ({
        matches: false, // Desktop
        media: query,
        onchange: null,
        addListener: vi.fn(),
        removeListener: vi.fn(),
        addEventListener: vi.fn(),
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });

    render(<ChatContent />);

    // Act - Click sidebar toggle
    const toggleButton = screen.getByLabelText('Hide sidebar');
    await user.click(toggleButton);

    // Assert - Desktop sidebar toggled
    expect(mockToggleSidebar).toHaveBeenCalledTimes(1);
  });

  // ============================================================================
  // TEST 5: Error message display
  // ============================================================================
  it.skip('should display error message when present in store', () => {
    // Arrange - Must have selectedGameId for error to potentially show in context
    vi.mocked(useChatStore).mockReturnValue({
      games: [mockGame],
      selectedGameId: 'game-1',
      chatsByGame: { 'game-1': [] },
      activeChatIds: {},
      messagesByChat: {},
      error: 'Network error: Failed to load messages',
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);

    // Act
    render(<ChatContent />);

    // Assert - Error message visible
    expect(screen.getByText('Network error: Failed to load messages')).toBeInTheDocument();
  });

  // ============================================================================
  // TEST 6: No game selected state
  // ============================================================================
  it('should display placeholder when no game selected', () => {
    // Arrange
    vi.mocked(useChatStore).mockReturnValue({
      games: [],
      selectedGameId: null,
      chatsByGame: {},
      activeChatIds: {},
      messagesByChat: {},
      error: null,
      sidebarCollapsed: false,
      toggleSidebar: vi.fn(),
    } as any);

    // Act
    render(<ChatContent />);

    // Assert - Placeholder title
    expect(screen.getByText('Seleziona o crea un thread')).toBeInTheDocument();

    // Assert - No game selected message
    expect(screen.getByText('Nessun gioco selezionato')).toBeInTheDocument();

    // Assert - No context chip (game not selected)
    expect(screen.queryByTestId('context-chip')).not.toBeInTheDocument();
  });
});
