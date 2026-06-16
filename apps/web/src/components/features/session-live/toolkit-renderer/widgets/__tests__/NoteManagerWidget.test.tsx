import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { NoteManagerWidget } from '../NoteManagerWidget';
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
  id: 'w5',
  type: 'NoteManager' as const,
  isEnabled: true,
  displayOrder: 4,
  config: {
    text: 'Regola casa: no alleanze.',
  },
};

describe('NoteManagerWidget', () => {
  const onConfigChange = vi.fn();
  const onHeaderClick = vi.fn();

  beforeEach(() => {
    onConfigChange.mockClear();
    onHeaderClick.mockClear();
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('renders data-slot and heading from labels', () => {
    const { container } = render(
      <NoteManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-note-manager"]')).toBeInTheDocument();
    expect(screen.getByText('Note')).toBeInTheDocument();
  });

  it('renders textarea with existing text', () => {
    render(
      <NoteManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const textarea = screen.getByRole('textbox', { name: 'Scrivi note' });
    expect(textarea).toHaveValue('Regola casa: no alleanze.');
  });

  it('typing + advancing fake timers triggers onConfigChange after debounce', async () => {
    render(
      <NoteManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const textarea = screen.getByRole('textbox', { name: 'Scrivi note' });
    fireEvent.change(textarea, { target: { value: 'Nuova nota!' } });

    // Before debounce fires: not called yet
    expect(onConfigChange).not.toHaveBeenCalled();

    // Advance past the 800ms debounce
    await vi.advanceTimersByTimeAsync(900);
    expect(onConfigChange).toHaveBeenCalledOnce();
    expect(onConfigChange).toHaveBeenCalledWith(expect.objectContaining({ text: 'Nuova nota!' }));
  });

  it('shows savingLabel while text is dirty (before debounce)', () => {
    render(
      <NoteManagerWidget
        widget={baseWidget}
        collapsed={false}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    const textarea = screen.getByRole('textbox', { name: 'Scrivi note' });
    fireEvent.change(textarea, { target: { value: 'Edit in progress' } });
    expect(screen.getByText('Salvataggio...')).toBeInTheDocument();
  });

  it('does not render body when collapsed', () => {
    const { container } = render(
      <NoteManagerWidget
        widget={baseWidget}
        collapsed={true}
        onHeaderClick={onHeaderClick}
        onConfigChange={onConfigChange}
        labels={makeLabels()}
      />
    );
    expect(container.querySelector('[data-slot="widget-note-manager"]')).not.toBeInTheDocument();
  });
});
