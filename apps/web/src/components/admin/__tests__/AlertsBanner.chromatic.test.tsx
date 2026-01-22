import React from 'react';
import { describe, it } from 'vitest';
import { AlertsBanner } from '../AlertsBanner';
import { render } from '@testing-library/react';

/**
 * Chromatic Visual Regression Tests for AlertsBanner
 *
 * These tests capture visual snapshots for Chromatic to detect UI regressions.
 * Each test renders a specific state/variant of the component.
 */

describe('AlertsBanner - Chromatic Visual Tests', () => {
  describe('State Variants', () => {
    it('renders all healthy state (green gradient)', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders has issues state (amber gradient)', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders single critical alert', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={1} healthyServices={9} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders many critical alerts', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={25} healthyServices={5} totalServices={30} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders zero services configured', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={0} totalServices={0} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Dark Mode Variants', () => {
    it('renders all healthy state in dark mode', () => {
      const { container } = render(
        <div className="dark p-8 bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders has issues state in dark mode', () => {
      const { container } = render(
        <div className="dark p-8 bg-stone-950">
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders zero services in dark mode', () => {
      const { container } = render(
        <div className="dark p-8 bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={0} totalServices={0} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Service Count Variations', () => {
    it('renders with all services down', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={10} healthyServices={0} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders with half services operational', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={5} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders with large service count', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner
            criticalCount={15}
            healthyServices={85}
            totalServices={100}
          />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Edge Cases', () => {
    it('renders with critical alerts but all services healthy', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={3} healthyServices={10} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders with no critical alerts but degraded services', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={7} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders with negative values normalized', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner
            criticalCount={-5}
            healthyServices={-3}
            totalServices={-10}
          />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Layout Variations', () => {
    it('renders with custom className', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner
            criticalCount={5}
            healthyServices={8}
            totalServices={10}
            className="mx-auto max-w-4xl"
          />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders in narrow container', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <div className="max-w-md">
            <AlertsBanner
              criticalCount={5}
              healthyServices={8}
              totalServices={10}
            />
          </div>
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders in wide container', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <div className="max-w-7xl">
            <AlertsBanner
              criticalCount={5}
              healthyServices={8}
              totalServices={10}
            />
          </div>
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('State Transitions', () => {
    it('captures before and after state change', () => {
      const { container, rerender } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
        </div>
      );

      // Capture healthy state
      expect(container).toMatchSnapshot('healthy-state');

      // Change to issues state
      rerender(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );

      // Capture issues state
      expect(container).toMatchSnapshot('issues-state');
    });
  });

  describe('Accessibility States', () => {
    it('renders with aria-label for screen readers', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={3} healthyServices={7} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders decorative elements with aria-hidden', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Responsive Behavior', () => {
    it('renders at mobile viewport (375px)', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950" style={{ width: 375 }}>
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders at tablet viewport (768px)', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950" style={{ width: 768 }}>
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders at desktop viewport (1440px)', () => {
      const { container } = render(
        <div className="p-8 bg-stone-50 dark:bg-stone-950" style={{ width: 1440 }}>
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });

  describe('Multiple Instances', () => {
    it('renders multiple banners stacked', () => {
      const { container } = render(
        <div className="p-8 space-y-4 bg-stone-50 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
          <AlertsBanner criticalCount={0} healthyServices={0} totalServices={0} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });

    it('renders multiple banners in grid', () => {
      const { container } = render(
        <div className="grid grid-cols-1 gap-4 p-8 bg-stone-50 md:grid-cols-2 dark:bg-stone-950">
          <AlertsBanner criticalCount={0} healthyServices={10} totalServices={10} />
          <AlertsBanner criticalCount={5} healthyServices={8} totalServices={10} />
        </div>
      );
      expect(container).toMatchSnapshot();
    });
  });
});
