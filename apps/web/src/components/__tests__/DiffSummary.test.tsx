import { render, screen } from '@testing-library/react';
import { DiffSummary } from '../DiffSummary';

describe('DiffSummary', () => {
  it('visualizza correttamente le statistiche delle modifiche', () => {
    const summary = {
      added: 5,
      modified: 3,
      deleted: 2,
      unchanged: 10
    };

    render(<DiffSummary summary={summary} />);

    // Verifica che il titolo sia presente
    expect(screen.getByText('Riepilogo Modifiche')).toBeInTheDocument();

    // Verifica che tutte le statistiche siano visualizzate correttamente
    expect(screen.getByText('+5')).toBeInTheDocument();
    expect(screen.getByText('Aggiunte')).toBeInTheDocument();

    expect(screen.getByText('~3')).toBeInTheDocument();
    expect(screen.getByText('Modificate')).toBeInTheDocument();

    expect(screen.getByText('-2')).toBeInTheDocument();
    expect(screen.getByText('Eliminate')).toBeInTheDocument();

    expect(screen.getByText('10')).toBeInTheDocument();
    expect(screen.getByText('Non modificate')).toBeInTheDocument();
  });

  it('gestisce correttamente i valori zero', () => {
    const summary = {
      added: 0,
      modified: 0,
      deleted: 0,
      unchanged: 0
    };

    render(<DiffSummary summary={summary} />);

    expect(screen.getByText('+0')).toBeInTheDocument();
    expect(screen.getByText('~0')).toBeInTheDocument();
    expect(screen.getByText('-0')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('visualizza correttamente grandi numeri', () => {
    const summary = {
      added: 999,
      modified: 123,
      deleted: 456,
      unchanged: 789
    };

    render(<DiffSummary summary={summary} />);

    expect(screen.getByText('+999')).toBeInTheDocument();
    expect(screen.getByText('~123')).toBeInTheDocument();
    expect(screen.getByText('-456')).toBeInTheDocument();
    expect(screen.getByText('789')).toBeInTheDocument();
  });

  it('applica gli stili corretti agli elementi', () => {
    const summary = {
      added: 1,
      modified: 1,
      deleted: 1,
      unchanged: 1
    };

    render(<DiffSummary summary={summary} />);

    // Verifica che i data-testid siano presenti per facilitare gli E2E test
    expect(screen.getByTestId('diff-summary-added')).toBeInTheDocument();
    expect(screen.getByTestId('diff-summary-modified')).toBeInTheDocument();
    expect(screen.getByTestId('diff-summary-deleted')).toBeInTheDocument();
    expect(screen.getByTestId('diff-summary-unchanged')).toBeInTheDocument();
  });

  it('visualizza solo le modifiche rilevanti (aggiunte, modificate, eliminate)', () => {
    const summary = {
      added: 5,
      modified: 0,
      deleted: 3,
      unchanged: 100
    };

    render(<DiffSummary summary={summary} />);

    // Anche se ci sono molte unchanged, devono essere visualizzate
    expect(screen.getByText('+5')).toBeInTheDocument();
    expect(screen.getByText('~0')).toBeInTheDocument();
    expect(screen.getByText('-3')).toBeInTheDocument();
    expect(screen.getByText('100')).toBeInTheDocument();
  });
});
