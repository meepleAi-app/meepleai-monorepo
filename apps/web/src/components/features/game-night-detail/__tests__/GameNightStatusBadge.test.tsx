/**
 * Tests for GameNightStatusBadge (Issue #951 commit 2).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameNightStatusBadge } from '../GameNightStatusBadge';

describe('GameNightStatusBadge', () => {
  it('renders the localized label', () => {
    render(<GameNightStatusBadge status="Published" label="Attiva" />);
    expect(screen.getByText('Attiva')).toBeInTheDocument();
  });

  it.each([
    ['Draft', 'bg-entity-event/14', true, null],
    ['Published', 'bg-entity-toolkit/14', true, null],
    ['Completed', 'bg-muted', false, '✓'],
    ['Cancelled', 'bg-destructive/10', false, '✕'],
  ] as const)(
    'status %s maps to %s, dot=%s, icon=%j',
    (status, expectedBgFragment, expectsDot, expectedIcon) => {
      render(<GameNightStatusBadge status={status} label="x" />);
      const badge = screen.getByTestId('game-night-status-badge');
      expect(badge.getAttribute('data-status')).toBe(status);
      expect(badge.className).toContain(expectedBgFragment);

      // Dot vs icon: the badge contains exactly one of:
      //   (a) a pulsing dot (aria-hidden span with `animate-pulse`)
      //   (b) a static icon character
      const pulsingDot = badge.querySelector('.animate-pulse');
      if (expectsDot) {
        expect(pulsingDot).not.toBeNull();
      } else {
        expect(pulsingDot).toBeNull();
        if (expectedIcon !== null) {
          expect(badge.textContent).toContain(expectedIcon);
        }
      }
    }
  );

  it('forwards className for layout overrides', () => {
    render(<GameNightStatusBadge status="Draft" label="Bozza" className="custom-extra" />);
    expect(screen.getByTestId('game-night-status-badge').className).toContain('custom-extra');
  });
});
