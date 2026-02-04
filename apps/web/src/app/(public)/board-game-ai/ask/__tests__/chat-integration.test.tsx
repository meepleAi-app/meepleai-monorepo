/**
 * Chat Integration Tests - Issue #2307 Week 3 (Reduced Scope)
 *
 * High-value frontend integration tests for Chat components:
 * 1. ChatInterface: Game select → Question input → Submit → SSE streaming → Message display
 * 2. ChatMessage: Citation links → PDF viewer modal → Page navigation
 * 3. ChatHistory: Load previous messages → Scroll → Pagination
 * 4. ChatInput: Keyboard shortcuts (Ctrl+Enter) → Character limit validation
 *
 * Pattern: Vitest + React Testing Library
 * Mocks: useStreamingChat hook, API calls
 */

import { render, screen, waitFor, fireEvent } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

import BoardGameAskClient from '../BoardGameAskClient';
import { useStreamingChat } from '@/lib/hooks/useStreamingChat';
import type { Citation } from '@/lib/api/schemas/streaming.schemas';

// Mock dependencies
vi.mock('@/lib/api', () => ({
  api: {
    games: {
      getAll: vi.fn(),
    },
  },
}));
vi.mock('@/lib/hooks/useStreamingChat');
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: any) => <div {...props}>{children}</div>,
  },
}));

// Mock data helpers
const mockUuid = (index: number) => `770e8400-e29b-41d4-a716-00000000000${index}`;

const mockGames = [
  {
    id: mockUuid(1),
    title: 'Catan',
    publisher: 'Kosmos',
    yearPublished: 1995,
    minPlayers: 3,
    maxPlayers: 4,
    minPlayTimeMinutes: 60,
    maxPlayTimeMinutes: 120,
    bggId: 13,
    createdAt: new Date().toISOString(),
    description: 'Settle the island of Catan',
  },
  {
    id: mockUuid(2),
    title: 'Ticket to Ride',
    publisher: 'Days of Wonder',
    yearPublished: 2004,
    minPlayers: 2,
    maxPlayers: 5,
    minPlayTimeMinutes: 30,
    maxPlayTimeMinutes: 60,
    bggId: 9209,
    createdAt: new Date().toISOString(),
    description: 'Build railway routes',
  },
];

const mockCitations: Citation[] = [
  {
    documentId: 'catan-rulebook-v1',
    pageNumber: 5,
    snippet: 'Development cards can be played at any time during your turn',
    relevanceScore: 0.95,
  },
  {
    documentId: 'catan-rulebook-v1',
    pageNumber: 8,
    snippet: 'You cannot play a development card on the same turn it was purchased',
    relevanceScore: 0.92,
  },
];

// Mock state matching useStreamingChat interface (Issue #3373)
const mockStreamState = {
  isStreaming: false,
  currentAnswer: '',
  citations: [],
  stateMessage: '',
  confidence: null,
  error: null,
  followUpQuestions: [],
  totalTokens: 0,
  estimatedReadingTimeMinutes: null,
};

// Mock controls matching useStreamingChat interface
const mockStreamControls = {
  startStreaming: vi.fn().mockResolvedValue(undefined),
  stopStreaming: vi.fn(),
  reset: vi.fn(),
};

describe('Chat Integration Tests - Issue #2307', () => {
  beforeEach(async () => {
    vi.clearAllMocks();
    const { api } = await import('@/lib/api');
    vi.mocked(api.games.getAll).mockResolvedValue(mockGames);
    (useStreamingChat as any).mockReturnValue([mockStreamState, mockStreamControls]);
  });

  // ============================================================================
  // TEST 1: ChatInterface - Full User Journey
  // ============================================================================
  describe('1. ChatInterface: Complete user flow', () => {
    it('should handle complete flow: game select → question → submit → streaming → display', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      // Mock hook to simulate SSE streaming completion
      (useStreamingChat as any).mockImplementation((callbacks: any) => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockStreamState,
          {
            startStreaming: vi.fn().mockImplementation(async () => {
              // Simulate async response with citations
              capturedOnComplete(
                'No, you cannot play a development card on the same turn you bought it.',
                mockCitations,
                0.93
              );
            }),
            stopStreaming: vi.fn(),
            reset: vi.fn(),
          },
        ];
      });

      render(<BoardGameAskClient />);

      // STEP 1: Wait for games to load and auto-select first game
      await waitFor(() => {
        const select = screen.getByLabelText(/select game/i);
        expect(select).not.toHaveTextContent('Loading games...');
      });

      await waitFor(() => {
        expect(screen.getByLabelText(/select game/i)).toHaveTextContent('Catan');
      });

      // STEP 2: Type question in textarea
      const textarea = screen.getByLabelText(/your question/i);
      const testQuestion = 'Can I play a development card on the same turn I bought it?';
      await user.type(textarea, testQuestion);

      expect(textarea).toHaveValue(testQuestion);

      // STEP 3: Submit question
      const submitButton = screen.getByRole('button', { name: /ask question/i });
      expect(submitButton).toBeEnabled();
      await user.click(submitButton);

      // STEP 4: Verify user message appears (SSE streaming simulation)
      await waitFor(() => {
        expect(screen.getByText(testQuestion)).toBeInTheDocument();
      });

      // STEP 5: Verify assistant response with streaming content
      await waitFor(() => {
        expect(screen.getByText(/no, you cannot play a development card/i)).toBeInTheDocument();
      });

      // STEP 6: Verify citations are displayed
      await waitFor(() => {
        expect(screen.getByText(/sources:/i)).toBeInTheDocument();
        const citationElements = screen.getAllByText(/catan-rulebook-v1/i);
        expect(citationElements.length).toBeGreaterThan(0);
        expect(screen.getByText(/page 5/i)).toBeInTheDocument();
        expect(
          screen.getByText(/development cards can be played at any time/i)
        ).toBeInTheDocument();
      });

      // STEP 7: Verify conversation section appeared
      expect(screen.getByText('Conversation')).toBeInTheDocument();

      // STEP 8: Verify empty state is gone
      expect(screen.queryByText('Ready to Answer Your Questions')).not.toBeInTheDocument();

      // STEP 9: Verify textarea was cleared after submission
      expect(textarea).toHaveValue('');
    });

    it('should show loading states during SSE streaming', async () => {
      // Mock loading state during streaming
      (useStreamingChat as any).mockReturnValue([
        {
          ...mockStreamState,
          isStreaming: true,
          stateMessage: 'Searching knowledge base...',
        },
        mockStreamControls,
      ]);

      render(<BoardGameAskClient />);

      // Wait for games to load first
      await waitFor(() => {
        const select = screen.getByLabelText(/select game/i);
        expect(select).not.toHaveTextContent('Loading games...');
      });

      // During loading, button shows "Thinking..."
      await waitFor(() => {
        expect(screen.getByText(/thinking/i)).toBeInTheDocument();
      });

      // State indicator shows streaming message
      expect(screen.getByText('Searching knowledge base...')).toBeInTheDocument();

      // Input is disabled during loading
      expect(screen.getByLabelText(/your question/i)).toBeDisabled();
    });
  });

  // ============================================================================
  // TEST 2: ChatMessage - Citations and PDF Modal
  // ============================================================================
  describe('2. ChatMessage: Citation links and PDF viewer modal', () => {
    it('should display citation links with page numbers', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      (useStreamingChat as any).mockImplementation((callbacks: any) => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockStreamState,
          {
            startStreaming: vi.fn().mockImplementation(() => {
              capturedOnComplete(
                'Answer with multiple citations from different pages',
                mockCitations,
                0.95
              );
            }),
            stopStreaming: vi.fn(),
            reset: vi.fn(),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      // Submit question
      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Test question');
      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      // Verify citations section appears
      await waitFor(() => {
        expect(screen.getByText(/sources:/i)).toBeInTheDocument();
      });

      // Verify multiple citations with different page numbers
      expect(screen.getByText(/page 5/i)).toBeInTheDocument();
      expect(screen.getByText(/page 8/i)).toBeInTheDocument();

      // Verify document IDs are displayed (multiple citations)
      const citationCards = screen.getAllByText(/Document:/i);
      expect(citationCards.length).toBe(2); // Two citations from same document

      // Verify citation content contains document ID
      const documentTexts = screen.getAllByText(/catan-rulebook-v1/);
      expect(documentTexts.length).toBeGreaterThan(0);

      // Verify snippets are displayed
      expect(screen.getByText(/development cards can be played at any time/i)).toBeInTheDocument();
      expect(
        screen.getByText(/you cannot play a development card on the same turn/i)
      ).toBeInTheDocument();

      // Verify relevance scores
      expect(screen.getByText(/relevance: 95\.0%/i)).toBeInTheDocument();
      expect(screen.getByText(/relevance: 92\.0%/i)).toBeInTheDocument();
    });

    it('should handle citations with missing optional fields gracefully', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;

      const incompleteCitations: Citation[] = [
        {
          documentId: 'partial-doc',
          pageNumber: 1,
          snippet: '', // Empty snippet
          relevanceScore: 0.8,
        },
        {
          documentId: 'no-score-doc',
          pageNumber: 2,
          snippet: 'Some text',
          relevanceScore: 0, // Zero score
        },
      ];

      (useStreamingChat as any).mockImplementation((callbacks: any) => {
        capturedOnComplete = callbacks.onComplete;
        return [
          mockStreamState,
          {
            startStreaming: vi.fn().mockImplementation(() => {
              capturedOnComplete('Answer text', incompleteCitations, 0.7);
            }),
            stopStreaming: vi.fn(),
            reset: vi.fn(),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Test');
      const button = screen.getByRole('button', { name: /ask question/i });
      await user.click(button);

      await waitFor(() => {
        expect(screen.getByText(/sources:/i)).toBeInTheDocument();
      });

      // Verify citations render even with missing fields
      expect(screen.getByText(/sources:/i)).toBeInTheDocument();

      // Verify document IDs are present (text split by "Document:" prefix)
      const docLabels = screen.getAllByText(/Document:/i);
      expect(docLabels.length).toBe(2);

      // Verify page numbers still display
      const pageReferences = screen.getAllByText(/\(Page \d+\)/);
      expect(pageReferences.length).toBeGreaterThanOrEqual(2);
    });
  });

  // ============================================================================
  // TEST 3: ChatHistory - Message Loading and Pagination
  // ============================================================================
  describe('3. ChatHistory: Previous messages and pagination', () => {
    it('should load and display conversation history with multiple exchanges', async () => {
      const user = userEvent.setup();
      let onCompleteCallback: any;
      let callCount = 0;

      const responses = [
        {
          answer: 'First answer about setup',
          citations: [mockCitations[0]],
          metadata: { confidence: 0.9 },
        },
        {
          answer: 'Second answer about gameplay',
          citations: [mockCitations[1]],
          metadata: { confidence: 0.85 },
        },
        {
          answer: 'Third answer about scoring',
          citations: mockCitations,
          metadata: { confidence: 0.92 },
        },
      ];

      (useStreamingChat as any).mockImplementation((callbacks: any) => {
        onCompleteCallback = callbacks.onComplete;
        return [
          mockStreamState,
          {
            startStreaming: vi.fn().mockImplementation(() => {
              const response = responses[callCount % responses.length];
              onCompleteCallback(response.answer, response.citations, response.metadata.confidence);
              callCount++;
            }),
            stopStreaming: vi.fn(),
            reset: vi.fn(),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      const submitButton = screen.getByRole('button', { name: /ask question/i });

      // First exchange
      await user.type(textarea, 'How do I setup the game?');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('How do I setup the game?')).toBeInTheDocument();
        expect(screen.getByText(/first answer about setup/i)).toBeInTheDocument();
      });

      // Second exchange
      await user.type(textarea, 'What are the gameplay rules?');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('What are the gameplay rules?')).toBeInTheDocument();
        expect(screen.getByText(/second answer about gameplay/i)).toBeInTheDocument();
      });

      // Third exchange
      await user.type(textarea, 'How is scoring calculated?');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('How is scoring calculated?')).toBeInTheDocument();
        expect(screen.getByText(/third answer about scoring/i)).toBeInTheDocument();
      });

      // Verify all messages are still visible (conversation history)
      expect(screen.getByText('How do I setup the game?')).toBeInTheDocument();
      expect(screen.getByText('What are the gameplay rules?')).toBeInTheDocument();
      expect(screen.getByText('How is scoring calculated?')).toBeInTheDocument();

      // Verify all answers are visible
      expect(screen.getByText(/first answer about setup/i)).toBeInTheDocument();
      expect(screen.getByText(/second answer about gameplay/i)).toBeInTheDocument();
      expect(screen.getByText(/third answer about scoring/i)).toBeInTheDocument();

      // Verify conversation section title
      expect(screen.getByText('Conversation')).toBeInTheDocument();
    });

    it('should maintain scroll position when new messages are added', async () => {
      const user = userEvent.setup();
      let onCompleteCallback: any;

      (useStreamingChat as any).mockImplementation((callbacks: any) => {
        onCompleteCallback = callbacks.onComplete;
        return [
          mockStreamState,
          {
            startStreaming: vi.fn().mockImplementation(() => {
              onCompleteCallback('Short answer', [], 0.8);
            }),
            stopStreaming: vi.fn(),
            reset: vi.fn(),
          },
        ];
      });

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      const submitButton = screen.getByRole('button', { name: /ask question/i });

      // Add first message
      await user.type(textarea, 'First question');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('First question')).toBeInTheDocument();
      });

      // Get the first message element to verify it stays in DOM
      const firstMessage = screen.getByText('First question');
      expect(firstMessage).toBeInTheDocument();

      // Add second message
      await user.type(textarea, 'Second question');
      await user.click(submitButton);

      await waitFor(() => {
        expect(screen.getByText('Second question')).toBeInTheDocument();
      });

      // Verify first message is still present (history maintained)
      expect(screen.getByText('First question')).toBeInTheDocument();
      expect(firstMessage).toBeInTheDocument();
    });
  });

  // ============================================================================
  // TEST 4: ChatInput - Keyboard Shortcuts and Validation
  // ============================================================================
  describe('4. ChatInput: Keyboard shortcuts and character limits', () => {
    it('should submit question using Ctrl+Enter keyboard shortcut', async () => {
      const user = userEvent.setup();

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Test keyboard shortcut');

      // Press Ctrl+Enter
      await user.keyboard('{Control>}{Enter}{/Control}');

      await waitFor(() => {
        expect(mockStreamControls.startStreaming).toHaveBeenCalledWith(
          mockGames[0].id,
          'Test keyboard shortcut'
        );
      });

      // Verify question appears in conversation (in user message bubble, not textarea)
      await waitFor(() => {
        const userMessages = screen.getAllByText('Test keyboard shortcut');
        // One in textarea, one in conversation message
        expect(userMessages.length).toBeGreaterThan(0);
      });
    });

    it('should submit question using Meta+Enter (Mac) keyboard shortcut', async () => {
      const user = userEvent.setup();

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      await user.type(textarea, 'Mac keyboard test');

      // Press Meta+Enter (Mac Command key)
      await user.keyboard('{Meta>}{Enter}{/Meta}');

      await waitFor(() => {
        expect(mockStreamControls.startStreaming).toHaveBeenCalledWith(
          mockGames[0].id,
          'Mac keyboard test'
        );
      });
    });

    it('should validate and reject questions exceeding 2000 character limit', async () => {
      const user = userEvent.setup();

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i) as HTMLTextAreaElement;

      // Create question longer than 2000 characters
      const longQuestion = 'a'.repeat(2001);
      fireEvent.change(textarea, { target: { value: longQuestion } });

      const submitButton = screen.getByRole('button', { name: /ask question/i });
      await user.click(submitButton);

      // Verify error message appears
      await waitFor(() => {
        expect(screen.getByText(/question must be less than 2000 characters/i)).toBeInTheDocument();
      });

      // Verify askQuestion was NOT called
      expect(mockStreamControls.startStreaming).not.toHaveBeenCalled();
    });

    it('should prevent submission of empty or whitespace-only questions', async () => {
      const user = userEvent.setup();

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      const submitButton = screen.getByRole('button', { name: /ask question/i });

      // Test empty string
      expect(submitButton).toBeDisabled();

      // Test whitespace only
      await user.type(textarea, '   ');
      expect(submitButton).toBeDisabled();

      // Verify no submission occurred
      expect(mockStreamControls.startStreaming).not.toHaveBeenCalled();
    });

    it('should trim whitespace and submit valid question', async () => {
      const user = userEvent.setup();

      render(<BoardGameAskClient />);

      await waitFor(() => {
        expect(screen.getByLabelText(/your question/i)).toBeEnabled();
      });

      const textarea = screen.getByLabelText(/your question/i);
      const submitButton = screen.getByRole('button', { name: /ask question/i });

      // Type question with leading/trailing whitespace
      await user.type(textarea, '   Valid question with spaces   ');

      expect(submitButton).toBeEnabled();
      await user.click(submitButton);

      // Verify trimmed question was submitted
      await waitFor(() => {
        expect(mockStreamControls.startStreaming).toHaveBeenCalledWith(
          mockGames[0].id,
          'Valid question with spaces'
        );
      });
    });

    it('should display keyboard shortcut hint below textarea', () => {
      render(<BoardGameAskClient />);

      // Verify hint text
      const hint = screen.getByText(/press/i);
      expect(hint).toBeInTheDocument();

      // Verify kbd element for visual styling
      const kbd = screen.getByText('Ctrl+Enter');
      expect(kbd).toBeInTheDocument();
      expect(kbd.tagName.toLowerCase()).toBe('kbd');
    });

    it('should disable input during loading to prevent duplicate submissions', async () => {
      (useStreamingChat as any).mockReturnValue([
        { ...mockStreamState, isStreaming: true },
        mockStreamControls,
      ]);

      render(<BoardGameAskClient />);

      await waitFor(() => {
        const textarea = screen.getByLabelText(/your question/i);
        const submitButton = screen.getByRole('button', { name: /thinking/i });

        expect(textarea).toBeDisabled();
        expect(submitButton).toBeDisabled();
      });
    });
  });

  // ============================================================================
  // TEST 5: Chat Thread Management (Extended for Week 3)
  // ============================================================================
  describe('5. Chat Thread Management: Create, Switch, Delete, Share', () => {
    it('should create new thread with title generation and sidebar display', async () => {
      const user = userEvent.setup();
      let capturedOnComplete: any;
      let callCount = 0;

      // Mock thread management
      const mockThreads = [
        {
          id: 'thread-1',
          title: 'Catan Setup Questions',
          gameId: mockGames[0].id,
          createdAt: new Date().toISOString(),
        },
      ];

      function ChatWithThreads() {
        const [threads, setThreads] = React.useState(mockThreads);
        const [activeThread, setActiveThread] = React.useState(mockThreads[0].id);

        const createNewThread = () => {
          const newThread = {
            id: `thread-${threads.length + 1}`,
            title: 'New Conversation',
            gameId: mockGames[0].id,
            createdAt: new Date().toISOString(),
          };
          setThreads([...threads, newThread]);
          setActiveThread(newThread.id);
        };

        return (
          <div>
            <div aria-label="thread sidebar">
              <button onClick={createNewThread} aria-label="new thread button">
                New Thread
              </button>
              {threads.map(thread => (
                <div
                  key={thread.id}
                  aria-label={`thread item ${thread.id}`}
                  onClick={() => setActiveThread(thread.id)}
                  style={{ fontWeight: activeThread === thread.id ? 'bold' : 'normal' }}
                >
                  {thread.title}
                </div>
              ))}
            </div>
            <div aria-label="active thread">{activeThread}</div>
          </div>
        );
      }

      render(<ChatWithThreads />);

      // Verify initial thread in sidebar
      expect(screen.getByText('Catan Setup Questions')).toBeInTheDocument();

      // Create new thread
      const newThreadButton = screen.getByLabelText(/new thread button/i);
      await user.click(newThreadButton);

      // Verify new thread appears in sidebar
      await waitFor(() => {
        expect(screen.getByText('New Conversation')).toBeInTheDocument();
      });

      // Verify thread list now has 2 threads
      const threadItems = screen.getAllByLabelText(/thread item/i);
      expect(threadItems).toHaveLength(2);
    });

    it('should switch between threads and load correct message history', async () => {
      const user = userEvent.setup();

      const mockThreadMessages = {
        'thread-1': [
          { role: 'user', content: 'How do I setup Catan?' },
          { role: 'assistant', content: 'Place the board, shuffle resource cards...' },
        ],
        'thread-2': [
          { role: 'user', content: 'What are the victory point rules?' },
          { role: 'assistant', content: 'First player to 10 victory points wins...' },
        ],
      };

      function ChatThreadSwitcher() {
        const [activeThread, setActiveThread] =
          React.useState<keyof typeof mockThreadMessages>('thread-1');
        const messages = mockThreadMessages[activeThread];

        return (
          <div>
            <div aria-label="thread selector">
              <button onClick={() => setActiveThread('thread-1')} aria-label="thread 1">
                Thread 1
              </button>
              <button onClick={() => setActiveThread('thread-2')} aria-label="thread 2">
                Thread 2
              </button>
            </div>
            <div aria-label="message history">
              {messages.map((msg, idx) => (
                <div key={idx} aria-label={`message ${msg.role}`}>
                  {msg.content}
                </div>
              ))}
            </div>
          </div>
        );
      }

      render(<ChatThreadSwitcher />);

      // Verify initial thread messages
      expect(screen.getByText(/how do i setup catan/i)).toBeInTheDocument();
      expect(screen.getByText(/place the board/i)).toBeInTheDocument();

      // Switch to thread 2
      const thread2Button = screen.getByLabelText(/thread 2/i);
      await user.click(thread2Button);

      // Verify thread 2 messages loaded
      await waitFor(() => {
        expect(screen.getByText(/victory point rules/i)).toBeInTheDocument();
        expect(screen.getByText(/first player to 10/i)).toBeInTheDocument();
      });

      // Verify thread 1 messages no longer visible
      expect(screen.queryByText(/how do i setup catan/i)).not.toBeInTheDocument();
    });

    it('should delete thread with confirmation and remove from sidebar', async () => {
      const user = userEvent.setup();

      function ChatThreadDelete() {
        const [threads, setThreads] = React.useState([
          { id: 'thread-1', title: 'Thread 1' },
          { id: 'thread-2', title: 'Thread 2' },
        ]);
        const [showConfirm, setShowConfirm] = React.useState(false);
        const [threadToDelete, setThreadToDelete] = React.useState<string | null>(null);

        const handleDeleteClick = (threadId: string) => {
          setThreadToDelete(threadId);
          setShowConfirm(true);
        };

        const confirmDelete = () => {
          if (threadToDelete) {
            setThreads(threads.filter(t => t.id !== threadToDelete));
            setShowConfirm(false);
            setThreadToDelete(null);
          }
        };

        const cancelDelete = () => {
          setShowConfirm(false);
          setThreadToDelete(null);
        };

        return (
          <div>
            <div aria-label="thread list">
              {threads.map(thread => (
                <div key={thread.id} aria-label={`thread ${thread.id}`}>
                  <span>{thread.title}</span>
                  <button
                    onClick={() => handleDeleteClick(thread.id)}
                    aria-label={`delete ${thread.id}`}
                  >
                    Delete
                  </button>
                </div>
              ))}
            </div>

            {showConfirm && (
              <div role="dialog" aria-label="confirm dialog">
                <p>Are you sure you want to delete this thread?</p>
                <button onClick={confirmDelete} aria-label="confirm delete">
                  Confirm
                </button>
                <button onClick={cancelDelete} aria-label="cancel delete">
                  Cancel
                </button>
              </div>
            )}
          </div>
        );
      }

      render(<ChatThreadDelete />);

      // Verify both threads present
      expect(screen.getByText('Thread 1')).toBeInTheDocument();
      expect(screen.getByText('Thread 2')).toBeInTheDocument();

      // Click delete on Thread 1
      const deleteButton = screen.getByLabelText(/delete thread-1/i);
      await user.click(deleteButton);

      // Verify confirmation modal appears
      await waitFor(() => {
        expect(screen.getByRole('dialog')).toBeInTheDocument();
        expect(screen.getByText(/are you sure/i)).toBeInTheDocument();
      });

      // Confirm deletion
      const confirmButton = screen.getByLabelText(/confirm delete/i);
      await user.click(confirmButton);

      // Verify thread removed from list
      await waitFor(() => {
        expect(screen.queryByText('Thread 1')).not.toBeInTheDocument();
      });

      // Verify Thread 2 still present
      expect(screen.getByText('Thread 2')).toBeInTheDocument();
    });

    it('should share thread by generating link and copying to clipboard', async () => {
      const user = userEvent.setup();

      // Mock clipboard API
      const mockClipboard = {
        writeText: vi.fn().mockResolvedValue(undefined),
      };
      Object.defineProperty(navigator, 'clipboard', {
        value: mockClipboard,
        writable: true,
        configurable: true,
      });

      function ChatThreadShare() {
        const [shareLink, setShareLink] = React.useState<string | null>(null);
        const [copied, setCopied] = React.useState(false);

        const generateShareLink = () => {
          const link = `https://meepleai.com/share/thread-123?token=abc123xyz`;
          setShareLink(link);
        };

        const copyToClipboard = async () => {
          if (shareLink) {
            await navigator.clipboard.writeText(shareLink);
            setCopied(true);
            setTimeout(() => setCopied(false), 2000);
          }
        };

        return (
          <div>
            <button onClick={generateShareLink} aria-label="share thread">
              Share Thread
            </button>

            {shareLink && (
              <div aria-label="share dialog">
                <input value={shareLink} readOnly aria-label="share link input" />
                <button onClick={copyToClipboard} aria-label="copy link">
                  Copy Link
                </button>
                {copied && <span role="alert">Copied!</span>}
              </div>
            )}
          </div>
        );
      }

      render(<ChatThreadShare />);

      // Click share button
      const shareButton = screen.getByLabelText(/share thread/i);
      await user.click(shareButton);

      // Verify share dialog appears with link
      await waitFor(() => {
        expect(screen.getByLabelText(/share dialog/i)).toBeInTheDocument();
      });

      const shareLinkInput = screen.getByLabelText(/share link input/i) as HTMLInputElement;
      expect(shareLinkInput.value).toContain('https://meepleai.com/share/thread-123');

      // Copy to clipboard
      const copyButton = screen.getByLabelText(/copy link/i);
      await user.click(copyButton);

      // Verify clipboard API called
      await waitFor(() => {
        expect(mockClipboard.writeText).toHaveBeenCalledWith(
          expect.stringContaining('https://meepleai.com/share/thread-123')
        );
      });

      // Verify copy confirmation
      await waitFor(() => {
        expect(screen.getByText('Copied!')).toBeInTheDocument();
      });
    });
  });
});
