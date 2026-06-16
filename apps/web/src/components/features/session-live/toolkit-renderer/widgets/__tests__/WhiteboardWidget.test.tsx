import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { WhiteboardWidget } from '../WhiteboardWidget';
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
  id: 'w6',
  type: 'Whiteboard' as const,
  isEnabled: true,
  displayOrder: 5,
  config: {
    strokes: [],
  },
};

describe('WhiteboardWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <WhiteboardWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-whiteboard"]')).toBeInTheDocument();
    expect(screen.getByText('Lavagna')).toBeInTheDocument();
  });

  it('renders tool palette buttons', () => {
    render(
      <WhiteboardWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByRole('button', { name: 'Penna' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Gomma' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cerchio' })).toBeInTheDocument();
  });

  it('renders placeholder canvas area', () => {
    render(
      <WhiteboardWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Canvas non disponibile in MVP')).toBeInTheDocument();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <WhiteboardWidget
        widget={baseWidget}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-whiteboard"]')).not.toBeInTheDocument();
  });
});
