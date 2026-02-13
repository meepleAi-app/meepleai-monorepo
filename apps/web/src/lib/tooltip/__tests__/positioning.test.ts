import { describe, it, expect } from 'vitest';
import { calculateOptimalPosition, debounce } from '../positioning';

describe('calculateOptimalPosition', () => {
  const viewport = { width: 1920, height: 1080 };

  describe('Vertical Placement', () => {
    it('places below when space available', () => {
      const trigger = new DOMRect(400, 100, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('bottom');
      expect(pos.top).toBe(148); // trigger.bottom (140) + GAP (8)
    });

    it('flips to top when no space below', () => {
      const trigger = new DOMRect(400, 950, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('top');
      expect(pos.bottom).toBeDefined();
    });
  });

  describe('Viewport Edge Cases', () => {
    it('top-left corner - places bottom-right', () => {
      const trigger = new DOMRect(10, 10, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('bottom');
      expect(pos.top).toBeGreaterThan(trigger.bottom);
      expect(pos.left).toBeGreaterThanOrEqual(8); // Clamped to GAP
    });

    it('bottom-right corner - places top-left', () => {
      const trigger = new DOMRect(1800, 1000, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('top');
      expect(pos.bottom).toBeDefined();
      expect(pos.left).toBeLessThanOrEqual(viewport.width - tooltip.width - 8);
    });

    it('bottom-left corner - places top', () => {
      const trigger = new DOMRect(10, 1000, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('top');
    });

    it('top-right corner - places bottom', () => {
      const trigger = new DOMRect(1800, 10, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('bottom');
    });
  });

  describe('Horizontal Placement', () => {
    it('places right when vertical space insufficient', () => {
      const trigger = new DOMRect(100, 520, 100, 40); // Center vertically
      const tooltip = { width: 150, height: 600 }; // Tall tooltip

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('right');
      expect(pos.left).toBe(208); // trigger.right (200) + GAP (8)
    });

    it('places left when right has no space', () => {
      const trigger = new DOMRect(1800, 520, 100, 40);
      const tooltip = { width: 150, height: 600 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('left');
      expect(pos.right).toBeDefined();
    });
  });

  describe('Viewport Clamping', () => {
    it('clamps left position to viewport', () => {
      const trigger = new DOMRect(10, 500, 100, 40);
      const tooltip = { width: 300, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      // Should clamp to at least GAP from left edge
      expect(pos.left).toBeGreaterThanOrEqual(8);
    });

    it('clamps right position to viewport', () => {
      const trigger = new DOMRect(1850, 500, 100, 40);
      const tooltip = { width: 300, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      // Should fit within viewport
      if (pos.left !== undefined) {
        expect(pos.left).toBeLessThanOrEqual(viewport.width - tooltip.width - 8);
      }
    });

    it('clamps top position to viewport', () => {
      const trigger = new DOMRect(500, 10, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      if (pos.top !== undefined) {
        expect(pos.top).toBeGreaterThanOrEqual(8);
      }
    });
  });

  describe('Performance', () => {
    it('calculates position in <16ms', () => {
      const trigger = new DOMRect(500, 500, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const start = performance.now();
      calculateOptimalPosition(trigger, tooltip, viewport);
      const elapsed = performance.now() - start;

      expect(elapsed).toBeLessThan(16);
    });

    it('handles large tooltips efficiently', () => {
      const trigger = new DOMRect(500, 500, 100, 40);
      const tooltip = { width: 800, height: 600 };

      const start = performance.now();
      calculateOptimalPosition(trigger, tooltip, viewport);
      const elapsed = performance.now() - start;

      expect(elapsed).toBeLessThan(16);
    });
  });
});

describe('debounce', () => {
  it('delays function execution', async () => {
    let called = false;
    const fn = debounce(() => { called = true; }, 50);

    fn();
    expect(called).toBe(false);

    await new Promise(resolve => setTimeout(resolve, 60));
    expect(called).toBe(true);
  });

  it('cancels previous call on rapid fire', async () => {
    let callCount = 0;
    const fn = debounce(() => { callCount++; }, 50);

    fn();
    fn();
    fn();

    await new Promise(resolve => setTimeout(resolve, 60));
    expect(callCount).toBe(1); // Only last call executed
  });
});
