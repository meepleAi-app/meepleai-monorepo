import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ResourceManagerWidget } from '../ResourceManagerWidget';
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
  id: 'w4',
  type: 'ResourceManager' as const,
  isEnabled: true,
  displayOrder: 3,
  config: {
    shared: false,
    resources: [
      { label: 'Legno', value: 3, max: 10 },
      { label: 'Ferro', value: 0, max: 5, danger: true },
    ],
  },
};

describe('ResourceManagerWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  beforeEach(() => {
    onConfigChange.mockClear();
    onHeaderClick.mockClear();
  });

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <ResourceManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-resource-manager"]')).toBeInTheDocument();
    expect(screen.getByText('Risorse')).toBeInTheDocument();
  });

  it('renders resource labels and max values', () => {
    render(
      <ResourceManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Legno')).toBeInTheDocument();
    expect(screen.getByText('Ferro')).toBeInTheDocument();
    expect(screen.getByText('/10')).toBeInTheDocument();
    expect(screen.getByText('/5')).toBeInTheDocument();
  });

  it('increment button calls onConfigChange with updated resource value', () => {
    render(
      <ResourceManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const incrementBtn = screen.getByRole('button', { name: 'Incrementa Legno' });
    fireEvent.click(incrementBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    const called = onConfigChange.mock.calls[0][0] as {
      resources: Array<{ label: string; value: number }>;
    };
    const legno = called.resources.find(r => r.label === 'Legno');
    expect(legno?.value).toBe(4);
  });

  it('decrement button on Legno calls onConfigChange with updated value', () => {
    render(
      <ResourceManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const decrementBtn = screen.getByRole('button', { name: 'Decrementa Legno' });
    fireEvent.click(decrementBtn);
    expect(onConfigChange).toHaveBeenCalledOnce();
    const called = onConfigChange.mock.calls[0][0] as {
      resources: Array<{ label: string; value: number }>;
    };
    const legno = called.resources.find(r => r.label === 'Legno');
    expect(legno?.value).toBe(2);
  });

  it('renders sharedHeading when config.shared is true', () => {
    render(
      <ResourceManagerWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, shared: true } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Risorse condivise')).toBeInTheDocument();
  });

  it('renders empty state when no resources', () => {
    render(
      <ResourceManagerWidget
        widget={{ ...baseWidget, config: { ...baseWidget.config, resources: [] } }}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(screen.getByText('Nessuna risorsa')).toBeInTheDocument();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <ResourceManagerWidget
        widget={baseWidget}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(
      container.querySelector('[data-slot="widget-resource-manager"]')
    ).not.toBeInTheDocument();
  });
});
