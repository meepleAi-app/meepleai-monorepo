import { describe, it, expect } from 'vitest';
import { calculateOptimalPosition } from '../positioning';

describe('calculateOptimalPosition', () => {
  it('places below when space available', () => {
    const trigger = new DOMRect(400, 100, 100, 40);
    const tooltip = { width: 200, height: 100 };

    const pos = calculateOptimalPosition(trigger, tooltip);

    expect(pos.placement).toBe('bottom');
    expect(pos.top).toBe(148);
  });

  it('flips to top when no space below', () => {
    const trigger = new DOMRect(400, 700, 100, 40);
    const tooltip = { width: 200, height: 100 };

    const pos = calculateOptimalPosition(trigger, tooltip);

    expect(pos.placement).toBe('top');
  });
});
