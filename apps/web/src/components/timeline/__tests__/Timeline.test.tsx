// UI-04: Timeline component comprehensive unit tests
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Timeline } from '../Timeline';
import { TimelineEvent } from '@/lib/timeline-types';

describe('Timeline Component', () => {
  const mockEvents: TimelineEvent[] = [
    {
      id: 'event-1',
      type: 'message',
      timestamp: new Date('2025-01-15T10:00:00Z'),
      status: 'success',
      data: {
        message: 'User question',
        role: 'user'
      }
    },
    {
      id: 'event-2',
      type: 'rag_search',
      timestamp: new Date('2025-01-15T10:00:01Z'),
      status: 'success',
      data: {
        message: 'Ricerca completata'
      }
    },
    {
      id: 'event-3',
      type: 'rag_retrieval',
      timestamp: new Date('2025-01-15T10:00:02Z'),
      status: 'success',
      data: {
        message: 'Recuperate 3 citazioni',
        citations: [
          { text: 'Citation 1', source: 'doc.pdf', page: 5 },
          { text: 'Citation 2', source: 'doc.pdf', page: 6 },
          { text: 'Citation 3', source: 'doc.pdf', page: 7 }
        ]
      }
    },
    {
      id: 'event-4',
      type: 'rag_complete',
      timestamp: new Date('2025-01-15T10:00:05Z'),
      status: 'success',
      data: {
        message: 'Risposta generata con successo',
        metrics: {
          latencyMs: 3500,
          promptTokens: 100,
          completionTokens: 200,
          totalTokens: 300,
          confidence: 0.95
        }
      }
    },
    {
      id: 'event-5',
      type: 'error',
      timestamp: new Date('2025-01-15T10:00:06Z'),
      status: 'error',
      data: {
        message: 'Errore',
        error: 'Connection timeout'
      }
    }
  ];

  describe('Visibility Toggle', () => {
    it('renders floating button when timeline is hidden', () => {
      const onToggle = jest.fn();
      render(<Timeline events={[]} isVisible={false} onToggleVisibility={onToggle} />);

      const button = screen.getByTitle('Mostra Timeline RAG');
      expect(button).toBeInTheDocument();
      expect(button).toHaveTextContent('Timeline RAG');
    });

    it('shows event count badge on floating button when events exist', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={false} onToggleVisibility={onToggle} />);

      expect(screen.getByText('5')).toBeInTheDocument(); // Event count badge
    });

    it('calls onToggleVisibility when floating button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={[]} isVisible={false} onToggleVisibility={onToggle} />);

      const button = screen.getByTitle('Mostra Timeline RAG');
      await user.click(button);

      expect(onToggle).toHaveBeenCalledTimes(1);
    });

    it('renders full timeline interface when visible', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByRole('heading', { name: /Timeline RAG/i })).toBeInTheDocument();
      expect(screen.getByText(/Cronologia eventi e metriche conversazione/i)).toBeInTheDocument();
    });

    it('calls onToggleVisibility when close button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      const closeButton = screen.getByText('Chiudi Timeline');
      await user.click(closeButton);

      expect(onToggle).toHaveBeenCalledTimes(1);
    });
  });

  describe('Multi-Pane Layout', () => {
    it('renders filters sidebar', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByText('Filtri Timeline')).toBeInTheDocument();
    });

    it('renders event list in center pane', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByText(/5 eventi trovati/i)).toBeInTheDocument();
    });

    it('renders event details panel', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByText('Dettagli Evento')).toBeInTheDocument();
    });

    it('shows empty state message in details panel initially', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByText(/Seleziona un evento dalla timeline per vedere i dettagli/i)).toBeInTheDocument();
    });
  });

  describe('Stats Bar', () => {
    it('displays total event count', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // The text is split: <strong>5</strong> eventi totali
      expect(screen.getByText((content, element) => {
        return element?.textContent === '5 eventi totali';
      })).toBeInTheDocument();
    });

    it('displays completed events count', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      const completedCount = mockEvents.filter(e => e.status === 'success').length;
      // The text is split: <strong>4</strong> completati
      expect(screen.getByText((content, element) => {
        return element?.textContent === `${completedCount} completati`;
      })).toBeInTheDocument();
    });

    it('displays error events count', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      const errorCount = mockEvents.filter(e => e.status === 'error').length;
      const errorText = errorCount === 1 ? 'errore' : 'errori';
      // The text is split: <strong>1</strong> errore
      expect(screen.getByText((content, element) => {
        return element?.textContent === `${errorCount} ${errorText}`;
      })).toBeInTheDocument();
    });

    it('displays total tokens when metrics are present', () => {
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // The text is split: <strong>300</strong> token totali
      expect(screen.getByText((content, element) => {
        return element?.textContent === '300 token totali';
      })).toBeInTheDocument();
    });

    it('does not render stats bar when no events exist', () => {
      const onToggle = jest.fn();
      render(<Timeline events={[]} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.queryByText(/eventi totali/i)).not.toBeInTheDocument();
    });
  });

  describe('Event Selection', () => {
    it('selects event when clicked in event list', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // Click on the message text itself (which is in the clickable event item)
      const userQuestionText = screen.getAllByText('User question')[0]; // First occurrence in event list
      await user.click(userQuestionText);

      // Details panel should show the selected event (there will be multiple "User question" texts now)
      await waitFor(() => {
        const allUserQuestions = screen.getAllByText('User question');
        expect(allUserQuestions.length).toBeGreaterThan(1); // Should appear in both list and details
      });
    });

    it('updates details panel when different event is selected', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // Select first event by clicking its message text
      const ricercaCompletata = screen.getAllByText('Ricerca completata')[0];
      await user.click(ricercaCompletata);

      await waitFor(() => {
        const allRicercaCompletata = screen.getAllByText('Ricerca completata');
        expect(allRicercaCompletata.length).toBeGreaterThan(1); // Should appear in list and details
      });

      // Select second event by clicking its message text
      const recuperateCitazioni = screen.getAllByText('Recuperate 3 citazioni')[0];
      await user.click(recuperateCitazioni);

      await waitFor(() => {
        const allRecuperateCitazioni = screen.getAllByText('Recuperate 3 citazioni');
        expect(allRecuperateCitazioni.length).toBeGreaterThan(1); // Should appear in list and details
      });
    });
  });

  describe('Filter Collapse/Expand', () => {
    it('collapses filters sidebar when collapse button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      const filtersHeading = screen.getByText('Filtri Timeline');
      expect(filtersHeading).toBeInTheDocument();

      // Find and click the collapse button using its title attribute
      const collapseButton = screen.getByTitle('Nascondi filtri');
      await user.click(collapseButton);

      await waitFor(() => {
        expect(screen.queryByText('Filtri Timeline')).not.toBeInTheDocument();
      });
    });

    it('expands filters sidebar when expand button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      const { rerender } = render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // Collapse first
      const collapseButton = screen.getByTitle('Nascondi filtri');
      await user.click(collapseButton);

      await waitFor(() => {
        expect(screen.queryByText('Filtri Timeline')).not.toBeInTheDocument();
      });

      // Expand
      const expandButton = screen.getByTitle('Mostra filtri');
      await user.click(expandButton);

      await waitFor(() => {
        expect(screen.getByText('Filtri Timeline')).toBeInTheDocument();
      });
    });
  });

  describe('Details Collapse/Expand', () => {
    it('collapses details panel when collapse button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      const detailsHeading = screen.getByText('Dettagli Evento');
      expect(detailsHeading).toBeInTheDocument();

      // Find and click the collapse button using its title attribute
      const collapseButton = screen.getByTitle('Nascondi dettagli');
      await user.click(collapseButton);

      await waitFor(() => {
        expect(screen.queryByText('Dettagli Evento')).not.toBeInTheDocument();
      });
    });

    it('expands details panel when expand button is clicked', async () => {
      const user = userEvent.setup();
      const onToggle = jest.fn();
      render(<Timeline events={mockEvents} isVisible={true} onToggleVisibility={onToggle} />);

      // First, select an event (expand button is disabled when no event is selected)
      const userQuestionText = screen.getAllByText('User question')[0];
      await user.click(userQuestionText);

      // Wait for event to be selected
      await waitFor(() => {
        const allUserQuestions = screen.getAllByText('User question');
        expect(allUserQuestions.length).toBeGreaterThan(1);
      });

      // Now collapse details
      const collapseButton = screen.getByTitle('Nascondi dettagli');
      await user.click(collapseButton);

      await waitFor(() => {
        expect(screen.queryByText('Dettagli Evento')).not.toBeInTheDocument();
      });

      // Expand details again
      const expandButton = screen.getByTitle('Mostra dettagli');
      await user.click(expandButton);

      await waitFor(() => {
        expect(screen.getByText('Dettagli Evento')).toBeInTheDocument();
      });
    });
  });

  describe('Empty State', () => {
    it('renders empty state when no events are provided', () => {
      const onToggle = jest.fn();
      render(<Timeline events={[]} isVisible={true} onToggleVisibility={onToggle} />);

      expect(screen.getByText(/Nessun evento trovato/i)).toBeInTheDocument();
      expect(screen.getByText(/Prova a modificare i filtri/i)).toBeInTheDocument();
    });
  });

  describe('Accessibility', () => {
    it('floating button has descriptive title', () => {
      const onToggle = jest.fn();
      render(<Timeline events={[]} isVisible={false} onToggleVisibility={onToggle} />);

      expect(screen.getByTitle('Mostra Timeline RAG')).toBeInTheDocument();
    });
  });
});
