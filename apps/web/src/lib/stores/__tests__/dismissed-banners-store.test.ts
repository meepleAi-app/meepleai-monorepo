/**
 * @vitest-environment jsdom
 *
 * dismissed-banners-store unit tests — Issue #1089.
 */

import { describe, it, expect, beforeEach } from 'vitest';

import {
  DISMISSED_BANNERS_CAP,
  DISMISSED_BANNERS_STORAGE_KEY,
  useDismissedBannersStore,
} from '../dismissed-banners-store';

describe('useDismissedBannersStore', () => {
  beforeEach(() => {
    window.localStorage.clear();
    useDismissedBannersStore.getState().reset();
  });

  it('starts with an empty list', () => {
    expect(useDismissedBannersStore.getState().dismissedIds).toEqual([]);
  });

  it('dismiss() adds a messageId', () => {
    useDismissedBannersStore.getState().dismiss('m1');
    expect(useDismissedBannersStore.getState().dismissedIds).toEqual(['m1']);
  });

  it('isDismissed() returns true for dismissed ids, false otherwise', () => {
    useDismissedBannersStore.getState().dismiss('m1');
    expect(useDismissedBannersStore.getState().isDismissed('m1')).toBe(true);
    expect(useDismissedBannersStore.getState().isDismissed('m2')).toBe(false);
  });

  it('dismiss() is idempotent on the same messageId', () => {
    useDismissedBannersStore.getState().dismiss('m1');
    useDismissedBannersStore.getState().dismiss('m1');
    expect(useDismissedBannersStore.getState().dismissedIds).toEqual(['m1']);
  });

  it('enforces FIFO cap dropping the oldest entries', () => {
    for (let i = 0; i < DISMISSED_BANNERS_CAP + 5; i++) {
      useDismissedBannersStore.getState().dismiss(`m${i}`);
    }
    const ids = useDismissedBannersStore.getState().dismissedIds;
    expect(ids).toHaveLength(DISMISSED_BANNERS_CAP);
    // First 5 oldest dropped; newest still present.
    expect(ids[0]).toBe('m5');
    expect(ids[ids.length - 1]).toBe(`m${DISMISSED_BANNERS_CAP + 4}`);
  });

  it('persists to LocalStorage under the canonical key', () => {
    useDismissedBannersStore.getState().dismiss('persist-1');
    const raw = window.localStorage.getItem(DISMISSED_BANNERS_STORAGE_KEY);
    expect(raw).toBeTruthy();
    expect(raw).toContain('persist-1');
  });
});
