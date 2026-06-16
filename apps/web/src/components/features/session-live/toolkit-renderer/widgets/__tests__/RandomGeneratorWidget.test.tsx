import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { RandomGeneratorWidget } from '../RandomGeneratorWidget';
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
  id: 'w1',
  type: 'RandomGenerator' as const,
  isEnabled: true,
  displayOrder: 0,
  config: {
    name: 'Dado D6',
    faces: [1, 2, 3, 4, 5, 6],
    quantity: 1,
    last: null,
  },
};

describe('RandomGeneratorWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  beforeEach(() => {
    onConfigChange.mockClear();
    onHeaderClick.mockClear();
  });

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <RandomGeneratorWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-random-generator"]')).toBeInTheDocument();
    expect(screen.getByText(makeLabels().randomGenerator.lastLabel)).toBeInTheDocument();
  });

  it('renders roll button with rollLabel', () => {
    render(
      <RandomGeneratorWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByRole('button', { name: 'Genera' })).toBeInTheDocument();
  });

  it('clicking roll calls onConfigChange with a value from faces (mocked Math.random)', () => {
    const mathRandom = vi.spyOn(Math, 'random').mockReturnValue(0); // idx = 0 → face = 1
    render(
      <RandomGeneratorWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Genera' }));
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(expect.objectContaining({ last: 1 }));
    mathRandom.mockRestore();
  });

  it('does not call onConfigChange when faces is empty', () => {
    render(
      <RandomGeneratorWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, faces: [] } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Genera' }));
    expect(onConfigChange).not.toHaveBeenCalled();
  });

  it('shows last result when config.last is set', () => {
    render(
      <RandomGeneratorWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, last: 6 } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('6')).toBeInTheDocument();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <RandomGeneratorWidget
        widget={baseWidget}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(
      container.querySelector('[data-slot="widget-random-generator"]')
    ).not.toBeInTheDocument();
  });
});
