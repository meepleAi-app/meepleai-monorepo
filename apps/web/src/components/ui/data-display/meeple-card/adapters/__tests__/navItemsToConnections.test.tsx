import { describe, it, expect, beforeEach, vi } from 'vitest';
import { navItemsToConnections } from '../navItemsToConnections';
import { __resetDevWarnDedup } from '../../hooks/devWarn';

describe('navItemsToConnections — happy path', () => {
  beforeEach(() => __resetDevWarnDedup());

  it('maps count=0 + showPlus + onPlusClick → onCreate', () => {
    const fn = vi.fn();
    const out = navItemsToConnections([
      { label: 'L', entity: 'player', count: 0, showPlus: true, onPlusClick: fn, icon: null },
    ]);
    expect(out[0].onCreate).toBe(fn);
  });

  it('maps label/entity/count/href/disabled 1:1 and forwards icon to iconOverride', () => {
    const iconNode = <i data-testid="legacy-icon" />;
    const out = navItemsToConnections([
      { label: 'L', entity: 'session', count: 3, href: '/s', disabled: true, icon: iconNode },
    ]);
    expect(out).toHaveLength(1);
    expect(out[0]).toMatchObject({
      entityType: 'session',
      label: 'L',
      count: 3,
      href: '/s',
      disabled: true,
    });
    expect(out[0].iconOverride).toBe(iconNode);
  });

  it('defaults count to 0 when omitted', () => {
    const out = navItemsToConnections([{ label: 'L', entity: 'kb', icon: null }]);
    expect(out[0].count).toBe(0);
  });

  it('preserves iconOverride as null when icon is explicitly null', () => {
    const out = navItemsToConnections([{ label: 'L', entity: 'kb', icon: null }]);
    expect(out[0].iconOverride).toBeNull();
  });

  it('returns empty array for empty input', () => {
    expect(navItemsToConnections([])).toEqual([]);
  });

  it('W3: count>0 + onPlusClick → onCreate dropped, emits indexed warning', () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {});
    const fn = vi.fn();
    const out = navItemsToConnections([
      { label: 'A', entity: 'player', count: 0, icon: null },
      { label: 'B', entity: 'player', count: 2, showPlus: true, onPlusClick: fn, icon: null },
    ]);
    expect(out[1].onCreate).toBeUndefined();
    expect(warn).toHaveBeenCalledWith(
      expect.stringMatching(/navItems\[1\].*onPlusClick.*dropped.*count>0/)
    );
  });
});
