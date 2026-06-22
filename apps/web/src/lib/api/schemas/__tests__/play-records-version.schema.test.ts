import { describe, it, expect } from 'vitest';

import { PlayRecordVersionSchema } from '../play-records.schemas';

const validVersion = {
  versionNumber: 3,
  sessionDate: '2026-06-20',
  notes: 'Great game',
  location: 'Home',
  createdAt: '2026-06-20T18:00:00Z',
  createdByUserId: '550e8400-e29b-41d4-a716-446655440001',
};

describe('PlayRecordVersionSchema (#2437-3)', () => {
  it('parses a fully-populated version', () => {
    const result = PlayRecordVersionSchema.parse(validVersion);
    expect(result.versionNumber).toBe(3);
    expect(result.sessionDate).toBe('2026-06-20');
    expect(result.notes).toBe('Great game');
    expect(result.location).toBe('Home');
    expect(result.createdAt).toBe('2026-06-20T18:00:00Z');
    expect(result.createdByUserId).toBe('550e8400-e29b-41d4-a716-446655440001');
  });

  it('accepts null notes and location', () => {
    const result = PlayRecordVersionSchema.parse({ ...validVersion, notes: null, location: null });
    expect(result.notes).toBeNull();
    expect(result.location).toBeNull();
  });

  it('rejects a non-integer versionNumber', () => {
    expect(() => PlayRecordVersionSchema.parse({ ...validVersion, versionNumber: 1.5 })).toThrow();
  });

  it('rejects a missing createdByUserId', () => {
    const { createdByUserId: _, ...rest } = validVersion;
    expect(() => PlayRecordVersionSchema.parse(rest)).toThrow();
  });

  it('rejects a non-UUID createdByUserId', () => {
    expect(() =>
      PlayRecordVersionSchema.parse({ ...validVersion, createdByUserId: 'not-a-uuid' })
    ).toThrow();
  });
});
