/**
 * ChipStrip (play-records) — Task 0.5 (Issue #1488).
 *
 * Row of entity-tinted chips ("game · player · chat · event") used by
 * RecordCard footer and PlayRecordHeroPodium ConnectionBar.
 *
 * Distinct from `features/sessions/ConnectionChipStripFooter` — sessions
 * has a max-3 cap and only game/player/chat entities. Play-records adds
 * `event` (session date) and `session` (linked session ref) chips, with
 * support for empty / dashed border state per chip.
 *
 * Chip props:
 *   - entity        — one of MeepleEntityType (game, player, chat, event, session)
 *   - label         — display text (overrides count)
 *   - count         — numeric counter; renders as label when `label` is absent
 *   - empty         — explicit empty (dashed border, dim icon)
 *   - href          — optional anchor for nav (renders as <a>)
 */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ChipStrip, type ChipStripProps } from '../ChipStrip';

function strip(props: Partial<ChipStripProps>) {
  return render(<ChipStrip chips={[]} {...props} />);
}

describe('ChipStrip (play-records)', () => {
  it('renders empty container when chips is empty', () => {
    strip({ chips: [] });
    const container = document.querySelector('[data-slot="chip-strip"]');
    expect(container).not.toBeNull();
    expect(container!.querySelectorAll('[data-slot="chip"]').length).toBe(0);
  });

  it('renders one chip per entry', () => {
    strip({
      chips: [
        { entity: 'game', label: 'Wingspan' },
        { entity: 'player', count: 4 },
      ],
    });
    const chips = document.querySelectorAll('[data-slot="chip"]');
    expect(chips.length).toBe(2);
  });

  it('chip with label displays label text', () => {
    strip({ chips: [{ entity: 'game', label: 'Wingspan' }] });
    expect(screen.getByText('Wingspan')).toBeTruthy();
  });

  it('chip with count (no label) displays count', () => {
    strip({ chips: [{ entity: 'player', count: 5 }] });
    expect(screen.getByText('5')).toBeTruthy();
  });

  it('chip carries data-entity attribute matching entity prop', () => {
    strip({ chips: [{ entity: 'event', label: '5 mag' }] });
    const chip = document.querySelector('[data-slot="chip"]');
    expect(chip!.getAttribute('data-entity')).toBe('event');
  });

  it('empty chip carries data-empty="true" attribute', () => {
    strip({
      chips: [{ entity: 'chat', label: 'Nessuna chat', empty: true }],
    });
    const chip = document.querySelector('[data-slot="chip"]');
    expect(chip!.getAttribute('data-empty')).toBe('true');
  });

  it('chip with count=0 implicitly treated as empty', () => {
    strip({ chips: [{ entity: 'chat', count: 0 }] });
    const chip = document.querySelector('[data-slot="chip"]');
    expect(chip!.getAttribute('data-empty')).toBe('true');
  });

  it('non-empty chip carries data-empty="false"', () => {
    strip({ chips: [{ entity: 'game', label: 'Wingspan' }] });
    const chip = document.querySelector('[data-slot="chip"]');
    expect(chip!.getAttribute('data-empty')).toBe('false');
  });

  it('chip with href renders as anchor', () => {
    strip({
      chips: [{ entity: 'game', label: 'Wingspan', href: '/games/wingspan' }],
    });
    const chip = document.querySelector('[data-slot="chip"]') as HTMLElement;
    expect(chip.tagName).toBe('A');
    expect(chip.getAttribute('href')).toBe('/games/wingspan');
  });

  it('chip without href renders as span', () => {
    strip({ chips: [{ entity: 'game', label: 'Wingspan' }] });
    const chip = document.querySelector('[data-slot="chip"]') as HTMLElement;
    expect(chip.tagName).toBe('SPAN');
  });

  it('accepts custom className on container', () => {
    strip({ chips: [], className: 'test-strip' });
    const container = document.querySelector('[data-slot="chip-strip"]');
    expect(container!.classList.contains('test-strip')).toBe(true);
  });

  it('supports all 5 entity tints (game, player, chat, event, session)', () => {
    strip({
      chips: [
        { entity: 'game', label: 'G' },
        { entity: 'player', label: 'P' },
        { entity: 'chat', label: 'C' },
        { entity: 'event', label: 'E' },
        { entity: 'session', label: 'S' },
      ],
    });
    const chips = document.querySelectorAll('[data-slot="chip"]');
    expect(chips.length).toBe(5);
    const entities = Array.from(chips).map(c => c.getAttribute('data-entity'));
    expect(entities).toEqual(['game', 'player', 'chat', 'event', 'session']);
  });
});
