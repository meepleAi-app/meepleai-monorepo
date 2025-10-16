// UI-04: TimelineEventItem component comprehensive unit tests
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { TimelineEventItem } from '../TimelineEventItem';
import { TimelineEvent } from '@/lib/timeline-types';

describe('TimelineEventItem Component', () => {
  const mockEvent: TimelineEvent = {
    id: 'event-1',
    type: 'message',
    timestamp: new Date('2025-01-15T10:00:00Z'),
    status: 'success',
    data: {
      message: 'Test message',
      role: 'user'
    }
  };

  const mockOnSelect = jest.fn();
  const mockOnToggleExpand = jest.fn();

  beforeEach(() => {
    jest.clearAllMocks();
  });

  describe('Event Header Rendering', () => {
    it('renders event type badge', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Messaggio')).toBeInTheDocument();
    });

    it('renders status icon', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('✅')).toBeInTheDocument();
    });

    it('renders event message', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Test message')).toBeInTheDocument();
    });

    it('renders timestamp', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      // Check for time format (will be locale-specific)
      const timePattern = /\d{1,2}:\d{2}:\d{2}/;
      expect(screen.getByText(timePattern)).toBeInTheDocument();
    });
  });

  describe('Event Status Indicators', () => {
    it('renders success status icon', () => {
      const successEvent = { ...mockEvent, status: 'success' as const };
      render(
        <TimelineEventItem
          event={successEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('✅')).toBeInTheDocument();
    });

    it('renders error status icon', () => {
      const errorEvent = { ...mockEvent, status: 'error' as const };
      render(
        <TimelineEventItem
          event={errorEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('❌')).toBeInTheDocument();
    });

    it('renders in_progress status icon', () => {
      const inProgressEvent = { ...mockEvent, status: 'in_progress' as const };
      render(
        <TimelineEventItem
          event={inProgressEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('🔄')).toBeInTheDocument();
    });

    it('renders pending status icon', () => {
      const pendingEvent = { ...mockEvent, status: 'pending' as const };
      render(
        <TimelineEventItem
          event={pendingEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('⏱️')).toBeInTheDocument();
    });
  });

  describe('Metrics Display', () => {
    it('renders metrics badge when metrics are present', () => {
      const eventWithMetrics = {
        ...mockEvent,
        data: {
          ...mockEvent.data,
          metrics: {
            latencyMs: 1500,
            totalTokens: 100
          }
        }
      };

      render(
        <TimelineEventItem
          event={eventWithMetrics}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText(/1.50s/i)).toBeInTheDocument();
      expect(screen.getByText(/100 tokens/i)).toBeInTheDocument();
    });

    it('does not render metrics badge when no metrics exist', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText(/tokens/i)).not.toBeInTheDocument();
    });
  });

  describe('Selection State', () => {
    it('applies selected styling when isSelected is true', () => {
      const { container } = render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={true}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventDiv = container.firstChild as HTMLElement;
      expect(eventDiv).toHaveStyle({ background: '#f8f9fa' });
    });

    it('applies default styling when isSelected is false', () => {
      const { container } = render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventDiv = container.firstChild as HTMLElement;
      expect(eventDiv).toHaveStyle({ background: 'white' });
    });

    it('calls onSelect with event id when clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const eventHeader = screen.getByText('Test message').parentElement;
      await user.click(eventHeader!);

      expect(mockOnSelect).toHaveBeenCalledWith('event-1');
    });
  });

  describe('Expand/Collapse', () => {
    it('renders expand button when collapsed', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByTitle('Espandi')).toBeInTheDocument();
      expect(screen.getByText('▼')).toBeInTheDocument();
    });

    it('renders collapse button when expanded', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByTitle('Comprimi')).toBeInTheDocument();
      expect(screen.getByText('▲')).toBeInTheDocument();
    });

    it('calls onToggleExpand when expand button is clicked', async () => {
      const user = userEvent.setup();
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const expandButton = screen.getByTitle('Espandi');
      await user.click(expandButton);

      expect(mockOnToggleExpand).toHaveBeenCalledWith('event-1');
      expect(mockOnSelect).not.toHaveBeenCalled(); // Should not trigger selection
    });

    it('shows expanded details when isExpanded is true', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Messaggio:')).toBeInTheDocument();
      expect(screen.getByText('Dettagli Tecnici')).toBeInTheDocument();
    });

    it('hides expanded details when isExpanded is false', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText('Dettagli Tecnici')).not.toBeInTheDocument();
    });
  });

  describe('Expanded View - Message Content', () => {
    it('displays message content for message type events', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Messaggio:')).toBeInTheDocument();
      // "Test message" appears multiple times (header and expanded view), use getAllByText
      expect(screen.getAllByText('Test message').length).toBeGreaterThan(0);
    });
  });

  describe('Expanded View - Citations', () => {
    it('displays citations when present', () => {
      const eventWithCitations = {
        ...mockEvent,
        data: {
          ...mockEvent.data,
          citations: [
            { text: 'Citation text 1', source: 'doc1.pdf', page: 5 },
            { text: 'Citation text 2', source: 'doc2.pdf', page: null }
          ]
        }
      };

      render(
        <TimelineEventItem
          event={eventWithCitations}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Citazioni (2):')).toBeInTheDocument();
      expect(screen.getByText('Citation text 1')).toBeInTheDocument();
      expect(screen.getByText('Citation text 2')).toBeInTheDocument();
      expect(screen.getByText('doc1.pdf (Pagina 5)')).toBeInTheDocument();
      expect(screen.getByText('doc2.pdf')).toBeInTheDocument();
    });

    it('does not display citations section when no citations exist', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText(/Citazioni/i)).not.toBeInTheDocument();
    });
  });

  describe('Expanded View - Metrics', () => {
    it('displays all metrics when present', () => {
      const eventWithMetrics = {
        ...mockEvent,
        data: {
          ...mockEvent.data,
          metrics: {
            latencyMs: 2500,
            promptTokens: 100,
            completionTokens: 150,
            totalTokens: 250,
            confidence: 0.87
          }
        }
      };

      render(
        <TimelineEventItem
          event={eventWithMetrics}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Metriche:')).toBeInTheDocument();
      expect(screen.getByText('Latenza')).toBeInTheDocument();
      expect(screen.getByText('2.50s')).toBeInTheDocument();
      expect(screen.getByText('Token Prompt')).toBeInTheDocument();
      expect(screen.getByText('100')).toBeInTheDocument();
      expect(screen.getByText('Token Completamento')).toBeInTheDocument();
      expect(screen.getByText('150')).toBeInTheDocument();
      expect(screen.getByText('Totale Token')).toBeInTheDocument();
      expect(screen.getByText('250')).toBeInTheDocument();
      expect(screen.getByText('Confidenza')).toBeInTheDocument();
      expect(screen.getByText('87.0%')).toBeInTheDocument();
    });

    it('does not display metrics section when no metrics exist', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText('Metriche:')).not.toBeInTheDocument();
    });
  });

  describe('Expanded View - Error', () => {
    it('displays error message when present', () => {
      const eventWithError = {
        ...mockEvent,
        status: 'error' as const,
        data: {
          ...mockEvent.data,
          error: 'Connection timeout error'
        }
      };

      render(
        <TimelineEventItem
          event={eventWithError}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Errore:')).toBeInTheDocument();
      expect(screen.getByText('Connection timeout error')).toBeInTheDocument();
    });

    it('does not display error section when no error exists', () => {
      render(
        <TimelineEventItem
          event={mockEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.queryByText('Errore:')).not.toBeInTheDocument();
    });
  });

  describe('Expanded View - Technical Details', () => {
    it('displays technical details in collapsible section', () => {
      const eventWithDetails = {
        ...mockEvent,
        relatedMessageId: 'msg-123',
        data: {
          ...mockEvent.data,
          endpoint: '/api/v1/agents/qa',
          gameId: 'game-456',
          chatId: 'chat-789'
        }
      };

      render(
        <TimelineEventItem
          event={eventWithDetails}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={true}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      const details = screen.getByText('Dettagli Tecnici');
      expect(details).toBeInTheDocument();

      // Check technical details are present (inside details element)
      expect(screen.getByText(/ID: event-1/i)).toBeInTheDocument();
      expect(screen.getByText(/Type: message/i)).toBeInTheDocument();
      expect(screen.getByText(/Status: success/i)).toBeInTheDocument();
      expect(screen.getByText(/Related Message: msg-123/i)).toBeInTheDocument();
      expect(screen.getByText(/Endpoint: \/api\/v1\/agents\/qa/i)).toBeInTheDocument();
      expect(screen.getByText(/Game ID: game-456/i)).toBeInTheDocument();
      expect(screen.getByText(/Chat ID: chat-789/i)).toBeInTheDocument();
    });
  });

  describe('Event Type Variations', () => {
    it('renders RAG search event correctly', () => {
      const ragSearchEvent: TimelineEvent = {
        id: 'event-rag',
        type: 'rag_search',
        timestamp: new Date(),
        status: 'in_progress',
        data: { message: 'Searching...' }
      };

      render(
        <TimelineEventItem
          event={ragSearchEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Ricerca RAG')).toBeInTheDocument();
    });

    it('renders RAG retrieval event correctly', () => {
      const ragRetrievalEvent: TimelineEvent = {
        id: 'event-retrieval',
        type: 'rag_retrieval',
        timestamp: new Date(),
        status: 'success',
        data: { message: 'Retrieved 3 citations' }
      };

      render(
        <TimelineEventItem
          event={ragRetrievalEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Recupero Citazioni')).toBeInTheDocument();
    });

    it('renders RAG generation event correctly', () => {
      const ragGenerationEvent: TimelineEvent = {
        id: 'event-generation',
        type: 'rag_generation',
        timestamp: new Date(),
        status: 'in_progress',
        data: { message: 'Generating...' }
      };

      render(
        <TimelineEventItem
          event={ragGenerationEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Generazione Risposta')).toBeInTheDocument();
    });

    it('renders RAG complete event correctly', () => {
      const ragCompleteEvent: TimelineEvent = {
        id: 'event-complete',
        type: 'rag_complete',
        timestamp: new Date(),
        status: 'success',
        data: { message: 'Complete' }
      };

      render(
        <TimelineEventItem
          event={ragCompleteEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Completato')).toBeInTheDocument();
    });

    it('renders error event correctly', () => {
      const errorEvent: TimelineEvent = {
        id: 'event-error',
        type: 'error',
        timestamp: new Date(),
        status: 'error',
        data: { message: 'Error occurred', error: 'Network error' }
      };

      render(
        <TimelineEventItem
          event={errorEvent}
          isSelected={false}
          onSelect={mockOnSelect}
          isExpanded={false}
          onToggleExpand={mockOnToggleExpand}
        />
      );

      expect(screen.getByText('Errore')).toBeInTheDocument();
    });
  });
});
