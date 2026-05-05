import { describe, expect, it } from 'vitest';
import { render } from '@testing-library/react';

import { entityIcons, ENTITY_ICON_STROKE, ENTITY_ICON_SIZE } from '../entity-icons';

import type { MeepleEntityType } from '../../types';

const allEntities: MeepleEntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
];

describe('entityIcons registry', () => {
  it('has a Lucide component for every MeepleEntityType', () => {
    for (const entity of allEntities) {
      expect(entityIcons[entity]).toBeDefined();
      expect(typeof entityIcons[entity]).toBe('object');
    }
  });

  it('exports canonical stroke and size constants', () => {
    expect(ENTITY_ICON_STROKE).toBe(1.75);
    expect(ENTITY_ICON_SIZE.sm).toBe(14);
    expect(ENTITY_ICON_SIZE.md).toBe(16);
  });

  it('each icon renders as an SVG element', () => {
    for (const entity of allEntities) {
      const Icon = entityIcons[entity];
      const { container } = render(<Icon size={14} strokeWidth={1.75} />);
      const svg = container.querySelector('svg');
      expect(svg).toBeTruthy();
      expect(svg?.getAttribute('stroke-width')).toBe('1.75');
    }
  });
});
