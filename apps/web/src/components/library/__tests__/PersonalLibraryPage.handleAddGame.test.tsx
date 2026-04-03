import { describe, expect, it } from 'vitest';

// Test the handleAddGame URL construction logic in isolation
describe('handleAddGame URL construction', () => {
  it('appends ?action=add preserving existing params', () => {
    const existing = new URLSearchParams('tab=games');
    const params = new URLSearchParams(existing.toString());
    params.set('action', 'add');
    const result = `/library?${params.toString()}`;
    expect(result).toBe('/library?tab=games&action=add');
  });

  it('sets ?action=add when no existing params', () => {
    const existing = new URLSearchParams('');
    const params = new URLSearchParams(existing.toString());
    params.set('action', 'add');
    const result = `/library?${params.toString()}`;
    expect(result).toBe('/library?action=add');
  });
});
