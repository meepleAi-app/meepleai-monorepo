import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { BggIdValidationBadge, parseBggIds } from './BggIdValidationBadge';

describe('BggIdValidationBadge', () => {
  it('renders the valid state with green tone', () => {
    render(<BggIdValidationBadge bggId={13} valid />);
    const badge = screen.getByTestId('bgg-id-badge-13');
    expect(badge.getAttribute('data-state')).toBe('valid');
    expect(badge.textContent).toContain('13');
  });

  it('renders the invalid state with reason', () => {
    render(<BggIdValidationBadge bggId="abc" valid={false} reason="not a number" />);
    const badge = screen.getByTestId('bgg-id-badge-abc');
    expect(badge.getAttribute('data-state')).toBe('invalid');
    expect(badge.textContent).toContain('not a number');
  });
});

describe('parseBggIds', () => {
  it('accepts comma-separated, line-separated, and space-separated lists', () => {
    const r = parseBggIds('13, 30549\n167791\n9209');
    expect(r.valid).toEqual([13, 30549, 167791, 9209]);
    expect(r.invalid).toEqual([]);
    expect(r.duplicates).toEqual([]);
  });

  it('marks non-numeric tokens as invalid', () => {
    const r = parseBggIds('13\nfoo\n42');
    expect(r.valid).toEqual([13, 42]);
    expect(r.invalid).toEqual([{ raw: 'foo', reason: 'not a positive integer' }]);
  });

  it('marks zero and negative numbers as invalid', () => {
    const r = parseBggIds('0\n-5\n12');
    expect(r.valid).toEqual([12]);
    expect(r.invalid).toHaveLength(2);
  });

  it('reports duplicates from the same batch', () => {
    const r = parseBggIds('13\n42\n13');
    expect(r.valid).toEqual([13, 42]);
    expect(r.duplicates).toEqual([13]);
  });

  it('returns empty buckets on empty input', () => {
    const r = parseBggIds('   \n\n');
    expect(r.valid).toEqual([]);
    expect(r.invalid).toEqual([]);
    expect(r.duplicates).toEqual([]);
  });
});
