import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { TurnManagerWidget } from '../TurnManagerWidget';
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
  id: 'w2',
  type: 'TurnManager' as const,
  isEnabled: true,
  displayOrder: 1,
  config: {
    phaseBased: true,
    phases: ['Setup', 'Gioco', 'Fine'],
    activeIndex: 0,
  },
};

describe('TurnManagerWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  beforeEach(() => {
    onConfigChange.mockClear();
    onHeaderClick.mockClear();
  });

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <TurnManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-turn-manager"]')).toBeInTheDocument();
    expect(screen.getByText('Turni')).toBeInTheDocument();
  });

  it('shows current phase label', () => {
    render(
      <TurnManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Setup')).toBeInTheDocument();
  });

  it('prev button is disabled at first phase', () => {
    render(
      <TurnManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const prevBtn = screen.getByRole('button', { name: /Prec/ });
    expect(prevBtn).toBeDisabled();
  });

  it('clicking next calls onConfigChange with incremented activeIndex', () => {
    render(
      <TurnManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const nextBtn = screen.getByRole('button', { name: /Succ/ });
    fireEvent.click(nextBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(expect.objectContaining({ activeIndex: 1 }));
  });

  it('clicking prev calls onConfigChange with decremented activeIndex', () => {
    render(
      <TurnManagerWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, activeIndex: 1 } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const prevBtn = screen.getByRole('button', { name: /Prec/ });
    fireEvent.click(prevBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(expect.objectContaining({ activeIndex: 0 }));
  });

  it('next button is disabled at last phase', () => {
    render(
      <TurnManagerWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, activeIndex: 2 } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const nextBtn = screen.getByRole('button', { name: /Succ/ });
    expect(nextBtn).toBeDisabled();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <TurnManagerWidget
        widget={baseWidget}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-turn-manager"]')).not.toBeInTheDocument();
  });
});
