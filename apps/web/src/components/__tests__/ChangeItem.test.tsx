import { render, screen } from '@testing-library/react';
import { ChangeItem } from '../versioning/ChangeItem';

describe('ChangeItem', () => {
  describe('Regola Aggiunta', () => {
    it('visualizza correttamente una regola aggiunta', () => {
      const change = {
        type: 'Added' as const,
        newAtom: 'rule-1',
        newValue: {
          id: 'rule-1',
          text: 'Nuova regola di gioco',
          section: 'Setup',
          page: '5',
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/\+ Added: rule-1/i)).toBeInTheDocument();
      expect(screen.getByText('Nuova regola di gioco')).toBeInTheDocument();
      expect(screen.getByText(/Sezione: Setup/i)).toBeInTheDocument();
      expect(screen.getByText(/Pagina: 5/i)).toBeInTheDocument();
      expect(screen.getByTestId('change-item-added')).toBeInTheDocument();
      expect(screen.getByTestId('change-added-content')).toBeInTheDocument();
    });

    it('visualizza una regola aggiunta senza sezione e pagina', () => {
      const change = {
        type: 'Added' as const,
        newAtom: 'rule-2',
        newValue: {
          id: 'rule-2',
          text: 'Regola senza dettagli',
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('Regola senza dettagli')).toBeInTheDocument();
      expect(screen.queryByText(/Sezione:/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Pagina:/i)).not.toBeInTheDocument();
    });
  });

  describe('Regola Modificata', () => {
    it('visualizza correttamente una regola modificata con field changes', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'rule-1',
        newAtom: 'rule-1',
        fieldChanges: [
          {
            fieldName: 'text',
            oldValue: 'Vecchio testo',
            newValue: 'Nuovo testo',
          },
          {
            fieldName: 'section',
            oldValue: 'Setup',
            newValue: 'Gameplay',
          },
        ],
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/~ Modified: rule-1/i)).toBeInTheDocument();
      expect(screen.getByText('text:')).toBeInTheDocument();
      expect(screen.getByText('Vecchio testo')).toBeInTheDocument();
      expect(screen.getByText('Nuovo testo')).toBeInTheDocument();
      expect(screen.getByText('section:')).toBeInTheDocument();
      expect(screen.getByText('Setup')).toBeInTheDocument();
      expect(screen.getByText('Gameplay')).toBeInTheDocument();
      expect(screen.getByTestId('change-item-modified')).toBeInTheDocument();
      expect(screen.getByTestId('change-modified-content')).toBeInTheDocument();
    });

    it('gestisce valori nulli nei field changes', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'rule-2',
        fieldChanges: [
          {
            fieldName: 'section',
            oldValue: null,
            newValue: 'New Section',
          },
        ],
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('(vuoto)')).toBeInTheDocument();
      expect(screen.getByText('New Section')).toBeInTheDocument();
    });
  });

  describe('Regola Eliminata', () => {
    it('visualizza correttamente una regola eliminata', () => {
      const change = {
        type: 'Deleted' as const,
        oldAtom: 'rule-3',
        oldValue: {
          id: 'rule-3',
          text: 'Regola rimossa',
          section: 'Endgame',
          page: '10',
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/- Deleted: rule-3/i)).toBeInTheDocument();
      expect(screen.getByText('Regola rimossa')).toBeInTheDocument();
      expect(screen.getByText(/Sezione: Endgame/i)).toBeInTheDocument();
      expect(screen.getByText(/Pagina: 10/i)).toBeInTheDocument();
      expect(screen.getByTestId('change-item-deleted')).toBeInTheDocument();
      expect(screen.getByTestId('change-deleted-content')).toBeInTheDocument();
    });

    it('visualizza una regola eliminata senza sezione e pagina', () => {
      const change = {
        type: 'Deleted' as const,
        oldAtom: 'rule-4',
        oldValue: {
          id: 'rule-4',
          text: 'Solo testo',
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('Solo testo')).toBeInTheDocument();
      expect(screen.queryByText(/Sezione:/i)).not.toBeInTheDocument();
      expect(screen.queryByText(/Pagina:/i)).not.toBeInTheDocument();
    });
  });

  describe('Regola Non Modificata', () => {
    it('visualizza correttamente una regola non modificata', () => {
      const change = {
        type: 'Unchanged' as const,
        oldAtom: 'rule-5',
        oldValue: {
          id: 'rule-5',
          text: 'Regola invariata',
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/= Unchanged: rule-5/i)).toBeInTheDocument();
      expect(screen.getByText('Regola invariata')).toBeInTheDocument();
      expect(screen.getByTestId('change-item-unchanged')).toBeInTheDocument();
      expect(screen.getByTestId('change-unchanged-content')).toBeInTheDocument();
    });
  });

  describe('Stili e Icone', () => {
    it("applica l'icona corretta per ogni tipo di change", () => {
      const changes = [
        { type: 'Added' as const, newAtom: 'r1', newValue: { id: 'r1', text: 'Test' } },
        { type: 'Modified' as const, oldAtom: 'r2', fieldChanges: [] },
        { type: 'Deleted' as const, oldAtom: 'r3', oldValue: { id: 'r3', text: 'Test' } },
        { type: 'Unchanged' as const, oldAtom: 'r4', oldValue: { id: 'r4', text: 'Test' } },
      ];

      const { rerender } = render(<ChangeItem change={changes[0]} />);
      expect(screen.getByText(/\+ Added/i)).toBeInTheDocument();

      rerender(<ChangeItem change={changes[1]} />);
      expect(screen.getByText(/~ Modified/i)).toBeInTheDocument();

      rerender(<ChangeItem change={changes[2]} />);
      expect(screen.getByText(/- Deleted/i)).toBeInTheDocument();

      rerender(<ChangeItem change={changes[3]} />);
      expect(screen.getByText(/= Unchanged/i)).toBeInTheDocument();
    });
  });

  describe('Edge Cases', () => {
    it('gestisce change con valori undefined', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'rule-x',
        fieldChanges: [
          {
            fieldName: 'text',
            oldValue: undefined,
            newValue: undefined,
          },
        ],
      };

      render(<ChangeItem change={change} />);

      // Dovrebbero essere visualizzati come "(vuoto)"
      const vuotoElements = screen.getAllByText('(vuoto)');
      expect(vuotoElements.length).toBeGreaterThanOrEqual(2);
    });

    it('gestisce multiple field changes', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'rule-multi',
        fieldChanges: [
          { fieldName: 'text', oldValue: 'Old 1', newValue: 'New 1' },
          { fieldName: 'section', oldValue: 'Old 2', newValue: 'New 2' },
          { fieldName: 'page', oldValue: 'Old 3', newValue: 'New 3' },
        ],
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('text:')).toBeInTheDocument();
      expect(screen.getByText('section:')).toBeInTheDocument();
      expect(screen.getByText('page:')).toBeInTheDocument();
    });

    it('handles unknown change type with default styles', () => {
      // Force an unknown type through type casting to test default branches
      const change = {
        type: 'UnknownType' as 'Added', // Cast to satisfy TypeScript
        oldAtom: 'rule-unknown',
      };

      const { container } = render(<ChangeItem change={change} />);

      // Default background (#f9f9f9) and border (#ccc) should be applied
      const changeItem = container.firstChild as HTMLElement;
      expect(changeItem).toHaveStyle({ background: '#f9f9f9' });
      expect(changeItem).toHaveStyle({ borderColor: '#ccc' });
    });

    it('renders without content when Added type has no newValue', () => {
      const change = {
        type: 'Added' as const,
        newAtom: 'rule-empty',
        newValue: undefined,
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/\+ Added: rule-empty/i)).toBeInTheDocument();
      expect(screen.queryByTestId('change-added-content')).not.toBeInTheDocument();
    });

    it('renders without content when Deleted type has no oldValue', () => {
      const change = {
        type: 'Deleted' as const,
        oldAtom: 'rule-deleted-empty',
        oldValue: undefined,
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/- Deleted: rule-deleted-empty/i)).toBeInTheDocument();
      expect(screen.queryByTestId('change-deleted-content')).not.toBeInTheDocument();
    });

    it('renders without content when Modified type has no fieldChanges', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'rule-modified-empty',
        fieldChanges: undefined,
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/~ Modified: rule-modified-empty/i)).toBeInTheDocument();
      expect(screen.queryByTestId('change-modified-content')).not.toBeInTheDocument();
    });

    it('renders without content when Unchanged type has no oldValue', () => {
      const change = {
        type: 'Unchanged' as const,
        oldAtom: 'rule-unchanged-empty',
        oldValue: undefined,
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/= Unchanged: rule-unchanged-empty/i)).toBeInTheDocument();
      expect(screen.queryByTestId('change-unchanged-content')).not.toBeInTheDocument();
    });

    it('displays oldAtom when newAtom is not provided', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'old-rule-id',
        newAtom: undefined,
        fieldChanges: [],
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/~ Modified: old-rule-id/i)).toBeInTheDocument();
    });

    it('displays newAtom over oldAtom when both provided', () => {
      const change = {
        type: 'Modified' as const,
        oldAtom: 'old-atom',
        newAtom: 'new-atom',
        fieldChanges: [],
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText(/~ Modified: new-atom/i)).toBeInTheDocument();
    });

    it('handles null line property in Added change', () => {
      const change = {
        type: 'Added' as const,
        newAtom: 'rule-with-line',
        newValue: {
          id: 'rule-with-line',
          text: 'Test text',
          section: 'Test Section',
          page: null,
          line: null,
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('Test text')).toBeInTheDocument();
      expect(screen.getByText(/Sezione: Test Section/i)).toBeInTheDocument();
      expect(screen.queryByText(/Pagina:/i)).not.toBeInTheDocument();
    });

    it('handles null line property in Deleted change', () => {
      const change = {
        type: 'Deleted' as const,
        oldAtom: 'rule-deleted-line',
        oldValue: {
          id: 'rule-deleted-line',
          text: 'Deleted text',
          section: null,
          page: '15',
          line: null,
        },
      };

      render(<ChangeItem change={change} />);

      expect(screen.getByText('Deleted text')).toBeInTheDocument();
      expect(screen.queryByText(/Sezione:/i)).not.toBeInTheDocument();
      expect(screen.getByText(/Pagina: 15/i)).toBeInTheDocument();
    });
  });
});
