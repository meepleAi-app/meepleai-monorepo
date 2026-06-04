import { describe, expect, it } from 'vitest';
import { chipPresentation, deriveChipState } from './status-mapper';

describe('deriveChipState', () => {
  it('returns "running" when status is running regardless of lastRun', () => {
    expect(deriveChipState('running', 'Success')).toBe('running');
    expect(deriveChipState('running', 'Failed')).toBe('running');
    expect(deriveChipState('running', null)).toBe('running');
  });

  it('returns "setup" when status is never_run', () => {
    expect(deriveChipState('never_run', null)).toBe('setup');
  });

  it('returns "healthy" when idle with Success last run', () => {
    expect(deriveChipState('idle', 'Success')).toBe('healthy');
  });

  it('returns "degraded" when idle with Failed last run', () => {
    expect(deriveChipState('idle', 'Failed')).toBe('degraded');
  });

  it('returns "degraded" when idle with TimedOut last run', () => {
    expect(deriveChipState('idle', 'TimedOut')).toBe('degraded');
  });

  it('returns "healthy" when idle with null last run (no history)', () => {
    expect(deriveChipState('idle', null)).toBe('healthy');
  });
});

describe('chipPresentation', () => {
  it('has all 4 ChipState entries with required keys', () => {
    const states = ['running', 'healthy', 'degraded', 'setup'] as const;
    for (const state of states) {
      expect(chipPresentation[state]).toMatchObject({
        label: expect.any(String),
        toneClass: expect.any(String),
      });
    }
  });

  it('uses semantic token classes (no hardcoded colors)', () => {
    expect(chipPresentation.healthy.toneClass).toContain('toolkit');
    expect(chipPresentation.degraded.toneClass).toContain('event');
    expect(chipPresentation.running.toneClass).toContain('amber');
    expect(chipPresentation.setup.toneClass).toContain('muted');
  });
});
