import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { EntityBadge } from '../EntityBadge';
import { entityHslText, entityIcon, entityLabel } from '../../tokens';

describe('EntityBadge (glass restyle, #1856)', () => {
  it('renders the entity emoji prefix followed by the label', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    // Emoji is in a separate aria-hidden span, so check separately
    const emoji = el.querySelector('span[aria-hidden="true"]');
    expect(emoji?.textContent).toBe(entityIcon.game);
    expect(el.textContent).toContain(entityLabel.game);
  });

  it('uses the glass background (bg-white/85 + backdrop-blur-md)', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/bg-white\/85/);
    expect(el.className).toMatch(/backdrop-blur-md/);
  });

  it('uses the entity text color (not white text on solid bg)', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    // No solid entity bg via inline style.
    expect(el.style.background).toBe('');
    // Inline color uses entityHslText for AA-safe contrast on glass bg (browser converts to RGB).
    expect(el.style.color).toBeTruthy();
    expect(el.style.color).not.toBe('');
    // No text-white class (regression: glass style uses entity color text).
    expect(el.className).not.toMatch(/\btext-white\b/);
  });

  it('keeps absolute positioning by default', () => {
    const { container } = render(<EntityBadge entity="game" />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/\babsolute\b/);
    expect(el.className).toMatch(/\btop-2\b/);
    expect(el.className).toMatch(/\bleft-2\.5\b/);
  });

  it('switches to self-start (no absolute) when stacked=true', () => {
    const { container } = render(<EntityBadge entity="game" stacked />);
    const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
    expect(el.className).toMatch(/self-start/);
    expect(el.className).not.toMatch(/\babsolute\b/);
  });

  it('renders the same glass style for all 9 entity types', () => {
    const entities = [
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
    for (const e of entities) {
      const { container, unmount } = render(<EntityBadge entity={e} />);
      const el = container.querySelector('[data-slot="meeple-card-entity-badge"]') as HTMLElement;
      expect(el.className).toMatch(/bg-white\/85/);
      expect(el.style.color).toBeTruthy();
      expect(el.style.color).not.toBe('');
      unmount();
    }
  });
});
