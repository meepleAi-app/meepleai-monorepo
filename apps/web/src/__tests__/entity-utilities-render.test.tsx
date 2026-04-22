import { describe, it, expect } from 'vitest';
import { render } from '@testing-library/react';

describe('Tailwind 4 entity-* utilities render', () => {
  const ENTITIES = [
    'game',
    'player',
    'session',
    'agent',
    'kb',
    'chat',
    'event',
    'toolkit',
    'tool',
  ] as const;

  ENTITIES.forEach(e => {
    it(`applies bg-entity-${e} and text-entity-${e} classes to DOM`, () => {
      const { container } = render(
        <div
          data-testid={`probe-${e}`}
          className={`bg-entity-${e} text-entity-${e} border-entity-${e}`}
        />
      );
      const el = container.querySelector(`[data-testid="probe-${e}"]`);
      expect(el).toBeTruthy();
      expect(el?.className).toContain(`bg-entity-${e}`);
      expect(el?.className).toContain(`text-entity-${e}`);
      expect(el?.className).toContain(`border-entity-${e}`);
    });
  });
});
