import { describe, it, expect } from 'vitest';
import {
  formatPlayTime,
  mechToIcon,
  rankToIcon,
  buildLinkedEntities,
  sessionStatusToLabel,
  processingStateToLabel,
} from '../shared-utils';

describe('formatPlayTime', () => {
  it('formats < 60 minutes', () => {
    expect(formatPlayTime(45)).toBe('45min');
    expect(formatPlayTime(30)).toBe('30min');
  });
  it('formats exact hours', () => {
    expect(formatPlayTime(60)).toBe('1h');
    expect(formatPlayTime(120)).toBe('2h');
  });
  it('formats hours and minutes', () => {
    expect(formatPlayTime(90)).toBe('1h30min');
    expect(formatPlayTime(150)).toBe('2h30min');
  });
  it('handles 0', () => {
    expect(formatPlayTime(0)).toBe('0min');
  });
});

describe('mechToIcon', () => {
  it('returns emoji for known mechanisms', () => {
    expect(mechToIcon('Worker Placement')).toBe('\u2699\uFE0F');
    expect(mechToIcon('Deck Building')).toBe('\uD83C\uDCCF');
    expect(mechToIcon('Cooperative')).toBe('\uD83E\uDD1D');
    expect(mechToIcon('Area Control')).toBe('\uD83D\uDDFA\uFE0F');
    expect(mechToIcon('Auction')).toBe('\uD83D\uDCB0');
    expect(mechToIcon('Dice')).toBe('\uD83C\uDFB2');
  });
  it('returns undefined for unknown mechanisms', () => {
    expect(mechToIcon('Unknown Mechanic')).toBeUndefined();
    expect(mechToIcon(undefined)).toBeUndefined();
  });
  it('is case-insensitive', () => {
    expect(mechToIcon('worker placement')).toBe('\u2699\uFE0F');
  });
});

describe('rankToIcon', () => {
  it('returns emoji for known ranks', () => {
    expect(rankToIcon('Master')).toBe('\uD83D\uDC51');
    expect(rankToIcon('Expert')).toBe('\u2B50');
    expect(rankToIcon('Veteran')).toBe('\uD83C\uDF96\uFE0F');
  });
  it('returns undefined for unknown or missing rank', () => {
    expect(rankToIcon(undefined)).toBeUndefined();
    expect(rankToIcon('Novice')).toBeUndefined();
  });
});

describe('buildLinkedEntities', () => {
  it('includes entity types with count > 0', () => {
    const result = buildLinkedEntities({ agentCount: 2, kbCount: 5 });
    expect(result).toHaveLength(2);
    expect(result).toContainEqual({ entityType: 'agent', count: 2 });
    expect(result).toContainEqual({ entityType: 'kb', count: 5 });
  });
  it('filters counts at zero or undefined', () => {
    const result = buildLinkedEntities({ agentCount: 0, kbCount: 3, sessionCount: undefined });
    expect(result).toHaveLength(1);
    expect(result[0]).toEqual({ entityType: 'kb', count: 3 });
  });
  it('returns empty array when all counts are zero', () => {
    expect(buildLinkedEntities({ agentCount: 0 })).toHaveLength(0);
    expect(buildLinkedEntities({})).toHaveLength(0);
  });
});

describe('sessionStatusToLabel', () => {
  it('maps InProgress to Live success', () => {
    expect(sessionStatusToLabel('InProgress')).toEqual({ text: 'Live', variant: 'success' });
  });
  it('maps Paused to Pausa warning', () => {
    expect(sessionStatusToLabel('Paused')).toEqual({ text: 'Pausa', variant: 'warning' });
  });
  it('maps Completed to Completata info', () => {
    expect(sessionStatusToLabel('Completed')).toEqual({ text: 'Completata', variant: 'info' });
  });
  it('maps Setup to Impostazione info', () => {
    expect(sessionStatusToLabel('Setup')).toEqual({ text: 'Impostazione', variant: 'info' });
  });
  it('returns info for unknown status', () => {
    const result = sessionStatusToLabel('Unknown');
    expect(result.variant).toBe('info');
  });
});

describe('processingStateToLabel', () => {
  it('maps Completed to Indicizzato success', () => {
    expect(processingStateToLabel('Completed')).toEqual({
      text: 'Indicizzato',
      variant: 'success',
    });
  });
  it('maps Failed to Errore error', () => {
    expect(processingStateToLabel('Failed')).toEqual({ text: 'Errore', variant: 'error' });
  });
  it('maps processing states to In Elaborazione warning', () => {
    for (const s of ['Uploading', 'Extracting', 'Chunking', 'Embedding', 'Indexing']) {
      expect(processingStateToLabel(s)).toEqual({ text: 'In Elaborazione', variant: 'warning' });
    }
  });
  it('maps Pending to In Attesa info', () => {
    expect(processingStateToLabel('Pending')).toEqual({ text: 'In Attesa', variant: 'info' });
  });
});
