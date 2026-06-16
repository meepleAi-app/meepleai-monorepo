import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ScoreTrackerWidget } from '../ScoreTrackerWidget';
import type { ToolkitRendererLabels } from '../../labels';

function makeLabels(): ToolkitRendererLabels {
  return {
    title: 'Strumenti',
    emptyTitle: 'Nessuno strumento',
    emptyBody: 'Aggiungi strumenti dal toolkit.',
    unknownTitle: 'Widget sconosciuto',
    unknownBody: 'Tipo non supportato.',
    expandAriaTemplate: 'Espandi widget {name}',
    collapseAriaTemplate: 'Comprimi widget {name}',
    randomGenerator: {
      heading: 'Generatore',
      rollLabel: 'Genera',
      lastLabel: 'Ultimo risultato',
    },
    turnManager: {
      heading: 'Turni',
      prevLabel: 'Prec',
      nextLabel: 'Succ',
      turnOfLabel: 'Turno di',
      phaseLabel: 'Fase',
    },
    scoreTracker: {
      heading: 'Punteggi',
      incrementAriaTemplate: 'Incrementa punteggio {name}',
      decrementAriaTemplate: 'Decrementa punteggio {name}',
    },
    resourceManager: {
      heading: 'Risorse',
      sharedHeading: 'Risorse condivise',
      incrementAriaTemplate: 'Incrementa {label}',
      decrementAriaTemplate: 'Decrementa {label}',
    },
    noteManager: {
      heading: 'Note',
      inputAriaLabel: 'Scrivi note',
      savingLabel: 'Salvataggio...',
      savedLabel: 'Salvato',
    },
    whiteboard: {
      heading: 'Lavagna',
      toolPenLabel: 'Penna',
      toolEraserLabel: 'Gomma',
      toolCircleLabel: 'Cerchio',
      placeholderLabel: 'Canvas non disponibile in MVP',
    },
  };
}

const baseWidget = {
  id: 'w3',
  type: 'ScoreTracker' as const,
  isEnabled: true,
  displayOrder: 2,
  config: {
    scores: { 'player-1': 10, 'player-2': 5 },
  },
};

const players = [
  { id: 'player-1', name: 'Alice' },
  { id: 'player-2', name: 'Bob' },
];

describe('ScoreTrackerWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  beforeEach(() => {
    onConfigChange.mockClear();
    onHeaderClick.mockClear();
  });

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <ScoreTrackerWidget
        widget={baseWidget}
        players={players}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-score-tracker"]')).toBeInTheDocument();
    expect(screen.getByText('Punteggi')).toBeInTheDocument();
  });

  it('renders player names', () => {
    render(
      <ScoreTrackerWidget
        widget={baseWidget}
        players={players}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Alice')).toBeInTheDocument();
    expect(screen.getByText('Bob')).toBeInTheDocument();
  });

  it('increment button for player calls onConfigChange with updated score', () => {
    render(
      <ScoreTrackerWidget
        widget={baseWidget}
        players={players}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const incrementBtn = screen.getByRole('button', {
      name: 'Incrementa punteggio Alice',
    });
    fireEvent.click(incrementBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(
      expect.objectContaining({
        scores: expect.objectContaining({ 'player-1': 11 }),
      })
    );
  });

  it('decrement button for player calls onConfigChange with updated score', () => {
    render(
      <ScoreTrackerWidget
        widget={baseWidget}
        players={players}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const decrementBtn = screen.getByRole('button', {
      name: 'Decrementa punteggio Bob',
    });
    fireEvent.click(decrementBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(
      expect.objectContaining({
        scores: expect.objectContaining({ 'player-2': 4 }),
      })
    );
  });

  it('renders empty state when no players provided', () => {
    render(
      <ScoreTrackerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Nessun giocatore')).toBeInTheDocument();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <ScoreTrackerWidget
        widget={baseWidget}
        players={players}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-score-tracker"]')).not.toBeInTheDocument();
  });
});
