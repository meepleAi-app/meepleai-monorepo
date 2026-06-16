import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { UnknownWidget } from '../UnknownWidget';
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

// Cast to ParsedWidget via 'unknown' to simulate an unrecognised future type
const unknownWidget = {
  id: 'w99',
  type: 'SuperNewWidget',
  isEnabled: true,
  displayOrder: 99,
  config: {},
} as unknown as Parameters<typeof UnknownWidget>[0]['widget'];

describe('UnknownWidget', () => {
  it('renders data-slot="widget-unknown"', () => {
    const { container } = render(<UnknownWidget widget={unknownWidget} labels={makeLabels()} />);
    expect(container.querySelector('[data-slot="widget-unknown"]')).toBeInTheDocument();
  });

  it('renders unknownTitle from labels', () => {
    render(<UnknownWidget widget={unknownWidget} labels={makeLabels()} />);
    expect(screen.getByText('Widget sconosciuto')).toBeInTheDocument();
  });

  it('renders unknownBody from labels', () => {
    render(<UnknownWidget widget={unknownWidget} labels={makeLabels()} />);
    expect(screen.getByText('Tipo non supportato.')).toBeInTheDocument();
  });

  it('displays the unknown widget type string', () => {
    render(<UnknownWidget widget={unknownWidget} labels={makeLabels()} />);
    expect(screen.getByText(/widget\.type: SuperNewWidget/)).toBeInTheDocument();
  });

  it('has role="status" and aria-live="polite"', () => {
    render(<UnknownWidget widget={unknownWidget} labels={makeLabels()} />);
    const section = screen.getByRole('status');
    expect(section).toBeInTheDocument();
    expect(section).toHaveAttribute('aria-live', 'polite');
  });
});
