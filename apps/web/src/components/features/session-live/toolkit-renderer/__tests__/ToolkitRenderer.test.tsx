import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { render } from '@testing-library/react';

import { ToolkitRenderer } from '../ToolkitRenderer';
import type { ToolkitRendererLabels } from '../labels';
import type { ParsedWidget } from '@/lib/session-live/widget-state';
import { defaultConfigFor } from '@/lib/session-live/widget-state';

const LABELS: ToolkitRendererLabels = {
  title: 'Toolkit',
  emptyTitle: 'Empty',
  emptyBody: 'Body',
  unknownTitle: 'Unknown',
  unknownBody: 'Update',
  expandAriaTemplate: 'Expand {name}',
  collapseAriaTemplate: 'Collapse {name}',
  randomGenerator: { heading: 'Random', rollLabel: 'Roll', lastLabel: 'Last' },
  turnManager: {
    heading: 'Turn',
    prevLabel: 'Prev',
    nextLabel: 'Next',
    turnOfLabel: 'Turn of',
    phaseLabel: 'Phase',
  },
  scoreTracker: {
    heading: 'Score',
    incrementAriaTemplate: '+{name}',
    decrementAriaTemplate: '-{name}',
  },
  resourceManager: {
    heading: 'Res',
    sharedHeading: 'Shared Res',
    incrementAriaTemplate: '+{label}',
    decrementAriaTemplate: '-{label}',
  },
  noteManager: {
    heading: 'Notes',
    inputAriaLabel: 'Write',
    savingLabel: 'Saving',
    savedLabel: 'Saved',
  },
  whiteboard: {
    heading: 'Board',
    toolPenLabel: 'Pen',
    toolEraserLabel: 'Eraser',
    toolCircleLabel: 'Circle',
    placeholderLabel: 'Draw',
  },
};

function makeWidget<T extends ParsedWidget['type']>(
  type: T,
  id: string,
  isEnabled = true,
  displayOrder = 0
): ParsedWidget {
  return {
    id,
    type,
    isEnabled,
    displayOrder,
    config: defaultConfigFor(type),
  } as ParsedWidget;
}

describe('ToolkitRenderer dispatch', () => {
  let warnSpy: ReturnType<typeof vi.spyOn>;

  beforeEach(() => {
    warnSpy = vi.spyOn(console, 'warn').mockImplementation(() => {});
  });

  afterEach(() => {
    warnSpy.mockRestore();
  });

  it.each([
    ['RandomGenerator', 'widget-random-generator'],
    ['TurnManager', 'widget-turn-manager'],
    ['ScoreTracker', 'widget-score-tracker'],
    ['ResourceManager', 'widget-resource-manager'],
    ['NoteManager', 'widget-note-manager'],
    ['Whiteboard', 'widget-whiteboard'],
  ] as const)('dispatches %s widget', (type, slot) => {
    const { container } = render(
      <ToolkitRenderer
        widgets={[makeWidget(type, 'w1')]}
        openWidgetId="w1"
        onOpenWidgetChange={() => {}}
        onWidgetConfigChange={() => {}}
        labels={LABELS}
      />
    );
    expect(container.querySelector(`[data-slot="${slot}"]`)).not.toBeNull();
  });

  it('renders Unknown widget for unregistered type + warns', () => {
    const w = {
      id: 'w1',
      type: 'BogusType',
      isEnabled: true,
      displayOrder: 0,
      config: {},
    } as unknown as ParsedWidget;
    const { container } = render(
      <ToolkitRenderer
        widgets={[w]}
        openWidgetId="w1"
        onOpenWidgetChange={() => {}}
        onWidgetConfigChange={() => {}}
        labels={LABELS}
      />
    );
    expect(container.querySelector('[data-slot="widget-unknown"]')).not.toBeNull();
    expect(warnSpy).toHaveBeenCalledWith('[ToolkitRenderer] Unknown widget type:', 'BogusType');
  });

  it('renders empty state when no widgets enabled', () => {
    const { container } = render(
      <ToolkitRenderer
        widgets={[makeWidget('RandomGenerator', 'w1', false)]}
        openWidgetId={null}
        onOpenWidgetChange={() => {}}
        onWidgetConfigChange={() => {}}
        labels={LABELS}
      />
    );
    expect(container.querySelector('[data-empty="true"]')).not.toBeNull();
  });

  it('sorts enabled widgets by displayOrder', () => {
    const { container } = render(
      <ToolkitRenderer
        widgets={[
          makeWidget('RandomGenerator', 'w1', true, 2),
          makeWidget('TurnManager', 'w2', true, 0),
          makeWidget('ScoreTracker', 'w3', true, 1),
        ]}
        openWidgetId={null}
        onOpenWidgetChange={() => {}}
        onWidgetConfigChange={() => {}}
        labels={LABELS}
      />
    );
    const types = Array.from(container.querySelectorAll('[data-widget-type]'));
    expect(types[0]?.getAttribute('data-widget-type')).toBe('TurnManager');
    expect(types[1]?.getAttribute('data-widget-type')).toBe('ScoreTracker');
    expect(types[2]?.getAttribute('data-widget-type')).toBe('RandomGenerator');
  });
});
