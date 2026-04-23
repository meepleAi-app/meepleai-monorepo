import { describe, expect, it } from 'vitest';

import { mapLegacyStatus, resolveStatus } from '../status-adapter';

describe('mapLegacyStatus()', () => {
  it('maps ownership values', () => {
    expect(mapLegacyStatus('owned')).toEqual({ ownership: 'owned' });
    expect(mapLegacyStatus('wishlist')).toEqual({ ownership: 'wishlist' });
    expect(mapLegacyStatus('archived')).toEqual({ ownership: 'archived' });
  });

  it('maps direct lifecycle values', () => {
    expect(mapLegacyStatus('active')).toEqual({ lifecycle: 'active' });
    expect(mapLegacyStatus('idle')).toEqual({ lifecycle: 'idle' });
    expect(mapLegacyStatus('completed')).toEqual({ lifecycle: 'completed' });
    expect(mapLegacyStatus('setup')).toEqual({ lifecycle: 'setup' });
    expect(mapLegacyStatus('processing')).toEqual({ lifecycle: 'processing' });
    expect(mapLegacyStatus('failed')).toEqual({ lifecycle: 'failed' });
  });

  it('merges inprogress → active', () => {
    expect(mapLegacyStatus('inprogress')).toEqual({ lifecycle: 'active' });
  });

  it('merges paused → idle', () => {
    expect(mapLegacyStatus('paused')).toEqual({ lifecycle: 'idle' });
  });

  it('drops internal indexed state from UI', () => {
    expect(mapLegacyStatus('indexed')).toEqual({});
  });

  it('returns empty for undefined input', () => {
    expect(mapLegacyStatus(undefined)).toEqual({});
  });
});

describe('resolveStatus()', () => {
  it('prefers explicit ownership over legacy status', () => {
    expect(resolveStatus({ ownership: 'wishlist', legacyStatus: 'owned' })).toEqual({
      ownership: 'wishlist',
      lifecycle: undefined,
    });
  });

  it('prefers explicit lifecycle over legacy status', () => {
    expect(resolveStatus({ lifecycle: 'failed', legacyStatus: 'active' })).toEqual({
      ownership: undefined,
      lifecycle: 'failed',
    });
  });

  it('falls back to legacy when explicit props are absent', () => {
    expect(resolveStatus({ legacyStatus: 'owned' })).toEqual({
      ownership: 'owned',
      lifecycle: undefined,
    });
  });

  it('returns empty dimensions when nothing is provided', () => {
    expect(resolveStatus({})).toEqual({
      ownership: undefined,
      lifecycle: undefined,
    });
  });
});
