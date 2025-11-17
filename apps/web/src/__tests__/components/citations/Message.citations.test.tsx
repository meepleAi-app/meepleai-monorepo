/**
 * Message Component Tests - Citations Feature (Issue #859)
 *
 * Tests for citation display in Message component
 */

import React from 'react';
import { render, screen } from '@testing-library/react';
import { Message } from '@/components/chat/Message';
import { Message as MessageType, Citation } from '@/types';

// Mock Zustand store
const mockChatStore = {
  editingMessageId: null,
  startEdit: jest.fn(),
  deleteMessage: jest.fn(),
  setMessageFeedback: jest.fn(),
  loading: { updating: false, deleting: false, sending: false, messages: false, creating: false, chats: false, games: false, agents: false },
  setInputValue: jest.fn(),
};

jest.mock('@/store/chat/store', () => ({
  useChatStore: () => mockChatStore,
}));

describe('Message - Citations Feature (#859)', () => {
  const mockCitations: Citation[] = [
    {
      documentId: 'doc-1',
      pageNumber: 10,
      snippet: 'Citation from page 10',
      relevanceScore: 0.95,
    },
    {
      documentId: 'doc-2',
      pageNumber: 25,
      snippet: 'Citation from page 25',
      relevanceScore: 0.80,
    },
  ];

  const baseMessage: MessageType = {
    id: 'msg-1',
    role: 'assistant',
    content: 'This is an AI response with citations',
    timestamp: new Date('2024-01-15T10:00:00Z'),
  };

  it('renders citations for assistant messages', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
    };

    render(<Message message={message} isUser={false} />);

    expect(screen.getByTestId('message-citations')).toBeInTheDocument();
    expect(screen.getByTestId('citation-list')).toBeInTheDocument();
  });

  it('does not render citations for user messages', () => {
    const message: MessageType = {
      ...baseMessage,
      role: 'user',
      citations: mockCitations,
    };

    render(<Message message={message} isUser={true} />);

    expect(screen.queryByTestId('message-citations')).not.toBeInTheDocument();
  });

  it('does not render citations when array is empty', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: [],
    };

    render(<Message message={message} isUser={false} />);

    expect(screen.queryByTestId('message-citations')).not.toBeInTheDocument();
  });

  it('does not render citations when citations is undefined', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: undefined,
    };

    render(<Message message={message} isUser={false} />);

    expect(screen.queryByTestId('message-citations')).not.toBeInTheDocument();
  });

  it('does not render citations for deleted messages', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
      isDeleted: true,
    };

    render(<Message message={message} isUser={false} />);

    expect(screen.queryByTestId('message-citations')).not.toBeInTheDocument();
  });

  it('renders citations as collapsible by default', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
    };

    render(<Message message={message} isUser={false} />);

    const header = screen.getByTestId('citations-header');
    expect(header).toHaveAttribute('aria-expanded', 'true');
  });

  it('renders all citations from message', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
    };

    render(<Message message={message} isUser={false} />);

    const cards = screen.getAllByTestId('citation-card');
    expect(cards).toHaveLength(2);
  });

  it('does not show relevance scores by default', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
    };

    render(<Message message={message} isUser={false} />);

    expect(screen.queryByTestId('citation-score')).not.toBeInTheDocument();
  });

  it('renders citations with correct max-width constraint', () => {
    const message: MessageType = {
      ...baseMessage,
      citations: mockCitations,
    };

    render(<Message message={message} isUser={false} />);

    const citationsContainer = screen.getByTestId('message-citations');
    expect(citationsContainer).toHaveClass('max-w-[75%]');
  });

  it('renders citations after follow-up questions', () => {
    const message: MessageType = {
      ...baseMessage,
      followUpQuestions: ['Question 1', 'Question 2'],
      citations: mockCitations,
    };

    const { container } = render(<Message message={message} isUser={false} />);

    // Both follow-up and citations should be present
    expect(screen.getByTestId('follow-up-questions')).toBeInTheDocument();
    expect(screen.getByTestId('message-citations')).toBeInTheDocument();

    // Citations should come after follow-up questions in DOM
    const followUp = screen.getByTestId('follow-up-questions');
    const citations = screen.getByTestId('message-citations');
    const position = followUp.compareDocumentPosition(citations);
    expect(position & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });
});
