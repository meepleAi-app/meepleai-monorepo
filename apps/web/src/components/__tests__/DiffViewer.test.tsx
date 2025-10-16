import { render, screen, within } from '@testing-library/react';
import { DiffViewer } from '../DiffViewer';

describe('DiffViewer', () => {
  const baseDiff = {
    gameId: 'test-game',
    fromVersion: 'v1.0',
    toVersion: 'v2.0',
    fromCreatedAt: '2025-01-01T00:00:00Z',
    toCreatedAt: '2025-01-02T00:00:00Z'
  };

  describe('Visualizzazione Riepilogo', () => {
    it('visualizza il riepilogo delle modifiche', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 10,
          added: 3,
          modified: 4,
          deleted: 2,
          unchanged: 5
        },
        changes: []
      };

      render(<DiffViewer diff={diff} showOnlyChanges={false} />);

      expect(screen.getByText('Riepilogo Modifiche')).toBeInTheDocument();
      expect(screen.getByText('+3')).toBeInTheDocument();
      expect(screen.getByText('~4')).toBeInTheDocument();
      expect(screen.getByText('-2')).toBeInTheDocument();
      expect(screen.getByText('5')).toBeInTheDocument();
    });
  });

  describe('Filtro "Mostra solo modifiche"', () => {
    it('mostra tutte le modifiche quando showOnlyChanges è false', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 3,
          added: 1,
          modified: 1,
          deleted: 1,
          unchanged: 2
        },
        changes: [
          {
            type: 'Added' as const,
            newAtom: 'rule-1',
            newValue: { id: 'rule-1', text: 'Regola aggiunta' }
          },
          {
            type: 'Modified' as const,
            oldAtom: 'rule-2',
            fieldChanges: [{ fieldName: 'text', oldValue: 'Old', newValue: 'New' }]
          },
          {
            type: 'Deleted' as const,
            oldAtom: 'rule-3',
            oldValue: { id: 'rule-3', text: 'Regola eliminata' }
          },
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-4',
            oldValue: { id: 'rule-4', text: 'Regola non modificata 1' }
          },
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-5',
            oldValue: { id: 'rule-5', text: 'Regola non modificata 2' }
          }
        ]
      };

      render(<DiffViewer diff={diff} showOnlyChanges={false} />);

      // Dovrebbero essere visualizzate tutte e 5 le modifiche
      expect(screen.getByText('Modifiche (5)')).toBeInTheDocument();
      expect(screen.getByText('Regola aggiunta')).toBeInTheDocument();
      expect(screen.getByText('Regola eliminata')).toBeInTheDocument();
      expect(screen.getByText('Regola non modificata 1')).toBeInTheDocument();
      expect(screen.getByText('Regola non modificata 2')).toBeInTheDocument();
    });

    it('mostra solo le modifiche rilevanti quando showOnlyChanges è true', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 3,
          added: 1,
          modified: 1,
          deleted: 1,
          unchanged: 2
        },
        changes: [
          {
            type: 'Added' as const,
            newAtom: 'rule-1',
            newValue: { id: 'rule-1', text: 'Regola aggiunta' }
          },
          {
            type: 'Modified' as const,
            oldAtom: 'rule-2',
            fieldChanges: [{ fieldName: 'text', oldValue: 'Old', newValue: 'New' }]
          },
          {
            type: 'Deleted' as const,
            oldAtom: 'rule-3',
            oldValue: { id: 'rule-3', text: 'Regola eliminata' }
          },
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-4',
            oldValue: { id: 'rule-4', text: 'Regola non modificata 1' }
          },
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-5',
            oldValue: { id: 'rule-5', text: 'Regola non modificata 2' }
          }
        ]
      };

      render(<DiffViewer diff={diff} showOnlyChanges={true} />);

      // Dovrebbero essere visualizzate solo 3 modifiche (escluse le Unchanged)
      expect(screen.getByText('Modifiche (3)')).toBeInTheDocument();
      expect(screen.getByText('Regola aggiunta')).toBeInTheDocument();
      expect(screen.getByText('Regola eliminata')).toBeInTheDocument();
      expect(screen.queryByText('Regola non modificata 1')).not.toBeInTheDocument();
      expect(screen.queryByText('Regola non modificata 2')).not.toBeInTheDocument();
    });
  });

  describe('Stato Vuoto', () => {
    it('visualizza un messaggio quando non ci sono modifiche da mostrare', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 0,
          added: 0,
          modified: 0,
          deleted: 0,
          unchanged: 5
        },
        changes: [
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-1',
            oldValue: { id: 'rule-1', text: 'Regola 1' }
          },
          {
            type: 'Unchanged' as const,
            oldAtom: 'rule-2',
            oldValue: { id: 'rule-2', text: 'Regola 2' }
          }
        ]
      };

      render(<DiffViewer diff={diff} showOnlyChanges={true} />);

      expect(screen.getByText('Modifiche (0)')).toBeInTheDocument();
      expect(screen.getByText('Nessuna modifica da visualizzare')).toBeInTheDocument();
    });

    it('visualizza un messaggio quando non ci sono changes affatto', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 0,
          added: 0,
          modified: 0,
          deleted: 0,
          unchanged: 0
        },
        changes: []
      };

      render(<DiffViewer diff={diff} showOnlyChanges={false} />);

      expect(screen.getByText('Modifiche (0)')).toBeInTheDocument();
      expect(screen.getByText('Nessuna modifica da visualizzare')).toBeInTheDocument();
    });
  });

  describe('Rendering Multipli Change Items', () => {
    it('visualizza correttamente molteplici change items di tipi diversi', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 4,
          added: 2,
          modified: 1,
          deleted: 1,
          unchanged: 0
        },
        changes: [
          {
            type: 'Added' as const,
            newAtom: 'rule-new-1',
            newValue: { id: 'rule-new-1', text: 'Prima regola aggiunta' }
          },
          {
            type: 'Added' as const,
            newAtom: 'rule-new-2',
            newValue: { id: 'rule-new-2', text: 'Seconda regola aggiunta' }
          },
          {
            type: 'Modified' as const,
            oldAtom: 'rule-mod',
            fieldChanges: [{ fieldName: 'text', oldValue: 'Vecchio', newValue: 'Nuovo' }]
          },
          {
            type: 'Deleted' as const,
            oldAtom: 'rule-del',
            oldValue: { id: 'rule-del', text: 'Regola cancellata' }
          }
        ]
      };

      render(<DiffViewer diff={diff} showOnlyChanges={true} />);

      expect(screen.getByText('Modifiche (4)')).toBeInTheDocument();
      expect(screen.getByText('Prima regola aggiunta')).toBeInTheDocument();
      expect(screen.getByText('Seconda regola aggiunta')).toBeInTheDocument();
      expect(screen.getByText('Vecchio')).toBeInTheDocument();
      expect(screen.getByText('Nuovo')).toBeInTheDocument();
      expect(screen.getByText('Regola cancellata')).toBeInTheDocument();
    });
  });

  describe('Integrazione con Componenti Figli', () => {
    it('integra correttamente DiffSummary e ChangeItem', () => {
      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 1,
          added: 1,
          modified: 0,
          deleted: 0,
          unchanged: 0
        },
        changes: [
          {
            type: 'Added' as const,
            newAtom: 'rule-integration',
            newValue: { id: 'rule-integration', text: 'Test integrazione', section: 'Setup', page: '3' }
          }
        ]
      };

      render(<DiffViewer diff={diff} showOnlyChanges={false} />);

      // Verifica la presenza del DiffSummary
      expect(screen.getByTestId('diff-summary-added')).toBeInTheDocument();

      // Verifica la presenza del ChangeItem
      expect(screen.getByTestId('change-item-added')).toBeInTheDocument();
      expect(screen.getByText('Test integrazione')).toBeInTheDocument();
      expect(screen.getByText(/Sezione: Setup/i)).toBeInTheDocument();
    });
  });

  describe('Performance con Grandi Dataset', () => {
    it('gestisce correttamente un grande numero di changes', () => {
      const manyChanges = Array.from({ length: 100 }, (_, i) => {
        const changeType = i % 2 === 0 ? 'Added' as const : 'Deleted' as const;
        return {
          type: changeType,
          ...(i % 2 === 0
            ? {
                newAtom: `rule-${i}`,
                newValue: { id: `rule-${i}`, text: `Regola ${i}` }
              }
            : {
                oldAtom: `rule-${i}`,
                oldValue: { id: `rule-${i}`, text: `Regola ${i}` }
              })
        };
      });

      const diff = {
        ...baseDiff,
        summary: {
          totalChanges: 100,
          added: 50,
          modified: 0,
          deleted: 50,
          unchanged: 0
        },
        changes: manyChanges
      };

      render(<DiffViewer diff={diff} showOnlyChanges={true} />);

      expect(screen.getByText('Modifiche (100)')).toBeInTheDocument();
      expect(screen.getByText('+50')).toBeInTheDocument();
      expect(screen.getByText('-50')).toBeInTheDocument();
    });
  });
});
