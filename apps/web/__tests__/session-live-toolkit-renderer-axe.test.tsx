/**
 * #2376 G5c — axe AA test for ToolkitRenderer covering all 6 widget types
 * + Unknown + empty state.
 *
 * Spec: docs/superpowers/specs/2026-06-16-issue-2376-g5c-toolkit-renderer-design.md §6
 */

import { describe, it, expect, vi, beforeAll, afterAll } from 'vitest';
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';

import { ToolkitRenderer } from '@/components/features/session-live';
import type { ToolkitRendererLabels } from '@/components/features/session-live';
import type { ParsedWidget } from '@/lib/session-live/widget-state';
import { defaultConfigFor } from '@/lib/session-live/widget-state';

// toHaveNoViolations is extended globally in vitest.setup.tsx

const LABELS: ToolkitRendererLabels = {
  title: 'Tools',
  emptyTitle: 'Empty',
  emptyBody: 'Enable widgets',
  unknownTitle: 'Unsupported',
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
    incrementAriaTemplate: 'Increment {name}',
    decrementAriaTemplate: 'Decrement {name}',
  },
  resourceManager: {
    heading: 'Resources',
    sharedHeading: 'Shared',
    incrementAriaTemplate: 'Increment {label}',
    decrementAriaTemplate: 'Decrement {label}',
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

const PLAYERS = [
  { id: 'p1', name: 'Marco' },
  { id: 'p2', name: 'Sara' },
];

function makeWidget<T extends ParsedWidget['type']>(type: T, id = 'w1'): ParsedWidget {
  return {
    id,
    type,
    isEnabled: true,
    displayOrder: 0,
    config: defaultConfigFor(type),
  } as ParsedWidget;
}

beforeAll(() => {
  vi.spyOn(console, 'warn').mockImplementation(() => {});
});

afterAll(() => {
  vi.restoreAllMocks();
});

function renderRenderer(widgets: ReadonlyArray<ParsedWidget>) {
  return render(
    <ToolkitRenderer
      widgets={widgets}
      openWidgetId="w1"
      onOpenWidgetChange={() => {}}
      onWidgetConfigChange={() => {}}
      players={PLAYERS}
      labels={LABELS}
    />
  );
}

const TYPES = [
  'RandomGenerator',
  'TurnManager',
  'ScoreTracker',
  'ResourceManager',
  'NoteManager',
  'Whiteboard',
] as const;

describe('#2376 G5c — ToolkitRenderer axe AA', () => {
  for (const type of TYPES) {
    it(`${type}: 0 axe AA violations (expanded)`, async () => {
      const { container } = renderRenderer([makeWidget(type)]);
      const results = await axe(container);
      expect(results).toHaveNoViolations();
    });
  }

  it('empty state: 0 axe AA violations', async () => {
    const { container } = renderRenderer([]);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });

  it('Unknown widget: 0 axe AA violations', async () => {
    const w = {
      id: 'w1',
      type: 'BogusType',
      isEnabled: true,
      displayOrder: 0,
      config: {},
    } as unknown as ParsedWidget;
    const { container } = renderRenderer([w]);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
