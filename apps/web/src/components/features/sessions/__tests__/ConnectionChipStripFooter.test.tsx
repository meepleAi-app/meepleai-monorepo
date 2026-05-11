/**
 * ConnectionChipStripFooter unit tests — Wave D.1 (Issue #735).
 *
 * 7 tests:
 * 1. Renders data-slot="connection-chip-strip-footer"
 * 2. Renders up to 3 chips
 * 3. Silently drops chips beyond 3
 * 4. Empty chip (count=0) has opacity style applied (data-entity present)
 * 5. Empty chip (empty=true) is rendered
 * 6. Non-empty chip renders label text
 * 7. Non-empty chip with count renders count text
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ConnectionChipStripFooter } from '../ConnectionChipStripFooter';
import type { ConnectionChipStripFooterProps } from '../ConnectionChipStripFooter';

describe('ConnectionChipStripFooter', () => {
  it('renders data-slot="connection-chip-strip-footer"', () => {
    render(<ConnectionChipStripFooter chips={[]} />);
    expect(document.querySelector('[data-slot="connection-chip-strip-footer"]')).not.toBeNull();
  });

  it('renders up to 3 chips', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [
      { entity: 'game', label: 'Wingspan' },
      { entity: 'player', count: 4 },
      { entity: 'chat', count: 3 },
    ];
    render(<ConnectionChipStripFooter chips={chips} />);
    const chipEls = document.querySelectorAll('[data-slot="connection-chip"]');
    expect(chipEls).toHaveLength(3);
  });

  it('silently drops chips beyond 3', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [
      { entity: 'game', label: 'A' },
      { entity: 'player', count: 2 },
      { entity: 'chat', count: 1 },
      { entity: 'game', label: 'Extra' },
    ];
    render(<ConnectionChipStripFooter chips={chips} />);
    const chipEls = document.querySelectorAll('[data-slot="connection-chip"]');
    expect(chipEls).toHaveLength(3);
    expect(screen.queryByText('Extra')).toBeNull();
  });

  it('empty chip (count=0) is rendered with data-entity attribute', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [{ entity: 'chat', count: 0 }];
    render(<ConnectionChipStripFooter chips={chips} />);
    const chip = document.querySelector('[data-entity="chat"]');
    expect(chip).not.toBeNull();
  });

  it('empty chip (empty=true) is rendered', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [{ entity: 'chat', empty: true }];
    render(<ConnectionChipStripFooter chips={chips} />);
    const chip = document.querySelector('[data-entity="chat"]');
    expect(chip).not.toBeNull();
  });

  it('non-empty chip renders label text', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [{ entity: 'game', label: 'Wingspan' }];
    render(<ConnectionChipStripFooter chips={chips} />);
    expect(screen.getByText('Wingspan')).toBeTruthy();
  });

  it('non-empty chip with count renders count text', () => {
    const chips: ConnectionChipStripFooterProps['chips'] = [{ entity: 'player', count: 4 }];
    render(<ConnectionChipStripFooter chips={chips} />);
    expect(screen.getByText('4')).toBeTruthy();
  });
});
