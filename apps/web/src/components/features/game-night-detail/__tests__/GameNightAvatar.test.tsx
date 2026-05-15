/**
 * Tests for GameNightAvatar primitive + helpers (Issue #951 commit 2).
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { GameNightAvatar, computeInitials, deriveHueFromId } from '../GameNightAvatar';

describe('GameNightAvatar', () => {
  it('renders initials with accessible label', () => {
    render(<GameNightAvatar initials="MR" label="Marco R." hue={210} />);
    const el = screen.getByRole('img', { name: 'Marco R.' });
    expect(el.textContent).toBe('MR');
  });

  it('applies HSL background derived from hue prop', () => {
    render(<GameNightAvatar initials="DC" label="Davide C." hue={120} />);
    const el = screen.getByRole('img');
    expect(el).toHaveStyle({ backgroundColor: 'hsl(120, 60%, 55%)' });
  });

  it('switches border to entity-player ring when highlightSelf=true', () => {
    const { rerender } = render(
      <GameNightAvatar initials="GM" label="Giulia M." hue={10} highlightSelf={false} />
    );
    const initial = screen.getByRole('img');
    expect(initial.className).toContain('border-card');
    expect(initial.className).not.toContain('ring-');

    rerender(<GameNightAvatar initials="GM" label="Giulia M." hue={10} highlightSelf />);
    const updated = screen.getByRole('img');
    expect(updated.className).toContain('border-entity-player');
    expect(updated.className).toContain('ring-entity-player');
  });

  it.each([
    [22, 'h-[22px] w-[22px]'],
    [28, 'h-7 w-7'],
    [32, 'h-8 w-8'],
    [40, 'h-10 w-10'],
    [48, 'h-12 w-12'],
  ] as const)('size %s renders %s', (size, expectedClass) => {
    render(<GameNightAvatar initials="AK" label="Aaron K." hue={140} size={size} />);
    const el = screen.getByRole('img');
    expect(el.className).toContain(expectedClass);
  });
});

describe('computeInitials', () => {
  it.each([
    ['Marco R.', 'MR'],
    ['Davide Carbone', 'DC'],
    ['Giulia Maria Bianchi', 'GB'],
    ['solo', 'SO'],
    ['', '??'],
    ['   spaced   name   ', 'SN'],
    ['A', 'A'],
  ])('computeInitials(%j) → %j', (input, expected) => {
    expect(computeInitials(input)).toBe(expected);
  });
});

describe('deriveHueFromId', () => {
  it('returns a stable value for the same input', () => {
    const id = '11111111-1111-1111-1111-111111111111';
    expect(deriveHueFromId(id)).toBe(deriveHueFromId(id));
  });

  it('returns a value in [0, 360)', () => {
    for (const id of ['a', 'foo', '11111111-1111-1111-1111-111111111111', 'unicode-úñ-ø']) {
      const hue = deriveHueFromId(id);
      expect(hue).toBeGreaterThanOrEqual(0);
      expect(hue).toBeLessThan(360);
    }
  });

  it('handles empty string gracefully (returns 0)', () => {
    expect(deriveHueFromId('')).toBe(0);
  });
});
