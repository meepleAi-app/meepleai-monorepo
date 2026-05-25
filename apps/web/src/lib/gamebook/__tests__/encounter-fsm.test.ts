import { describe, expect, it } from 'vitest';

import { EncounterParseError } from '@/lib/api/gamebook-encounter';

import { deriveEncounterStatus, mapEncounterError } from '../encounter-fsm';

describe('deriveEncounterStatus', () => {
  it('maps an untouched mutation to the entry state', () => {
    expect(deriveEncounterStatus('idle')).toBe('idle');
  });

  it('maps a pending mutation to the parsing state', () => {
    expect(deriveEncounterStatus('pending')).toBe('parsing');
  });

  it('maps a successful mutation to the rendered state', () => {
    expect(deriveEncounterStatus('success')).toBe('rendered');
  });

  it('maps a failed mutation to the error state', () => {
    expect(deriveEncounterStatus('error')).toBe('error');
  });
});

describe('mapEncounterError', () => {
  it('maps a 409 EncounterParseError to parse-failed', () => {
    expect(mapEncounterError(new EncounterParseError(409, 'nope'))).toBe('parse-failed');
  });

  it('maps a 404 EncounterParseError to not-found', () => {
    expect(mapEncounterError(new EncounterParseError(404, 'gone'))).toBe('not-found');
  });

  it('maps other EncounterParseError statuses to generic', () => {
    expect(mapEncounterError(new EncounterParseError(500, 'boom'))).toBe('generic');
  });

  it('maps a non-EncounterParseError to generic', () => {
    expect(mapEncounterError(new Error('network'))).toBe('generic');
    expect(mapEncounterError(null)).toBe('generic');
  });
});
