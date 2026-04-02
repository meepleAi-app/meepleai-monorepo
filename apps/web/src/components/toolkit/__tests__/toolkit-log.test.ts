import { describe, it, expect, beforeEach } from 'vitest';
import { appendToolLog, getToolLog, clearOldEntries } from '@/lib/utils/toolkit-log';
import type { ToolLogEntry } from '@/lib/types/standalone-toolkit';

const STORAGE_KEY = 'meepleai:toolkit:log';

beforeEach(() => {
  localStorage.clear();
});

describe('appendToolLog', () => {
  it('saves an entry to localStorage', () => {
    const entry: ToolLogEntry = {
      id: '1',
      timestamp: '2026-04-02T10:00:00Z',
      toolType: 'dice',
      action: 'roll',
      actorLabel: 'Marco',
      result: '2D6 → 4+3 = 7',
    };
    appendToolLog(entry);
    const stored = JSON.parse(localStorage.getItem(STORAGE_KEY)!);
    expect(stored).toHaveLength(1);
    expect(stored[0].result).toBe('2D6 → 4+3 = 7');
  });

  it('appends to existing entries', () => {
    appendToolLog({
      id: '1',
      timestamp: '2026-04-02T10:00:00Z',
      toolType: 'dice',
      action: 'roll',
      result: 'D6 → 3',
    });
    appendToolLog({
      id: '2',
      timestamp: '2026-04-02T10:01:00Z',
      toolType: 'timer',
      action: 'start',
      result: '60s',
    });
    expect(getToolLog()).toHaveLength(2);
  });

  it('drops oldest entry when limit of 100 is reached', () => {
    for (let i = 0; i < 100; i++) {
      appendToolLog({
        id: String(i),
        timestamp: '2026-04-02T10:00:00Z',
        toolType: 'dice',
        action: 'roll',
        result: `D6 → ${i}`,
      });
    }
    appendToolLog({
      id: '100',
      timestamp: '2026-04-02T10:00:00Z',
      toolType: 'dice',
      action: 'roll',
      result: 'D6 → new',
    });
    const log = getToolLog();
    expect(log).toHaveLength(100);
    expect(log[log.length - 1].result).toBe('D6 → new');
    expect(log[0].id).toBe('1'); // entry '0' dropped
  });
});

describe('clearOldEntries', () => {
  it('removes entries older than 7 days', () => {
    const old = new Date();
    old.setDate(old.getDate() - 8);
    const recent = new Date();
    localStorage.setItem(
      STORAGE_KEY,
      JSON.stringify([
        {
          id: '1',
          timestamp: old.toISOString(),
          toolType: 'dice',
          action: 'roll',
          result: 'D6 → 1',
        },
        {
          id: '2',
          timestamp: recent.toISOString(),
          toolType: 'dice',
          action: 'roll',
          result: 'D6 → 2',
        },
      ])
    );
    clearOldEntries();
    expect(getToolLog()).toHaveLength(1);
    expect(getToolLog()[0].id).toBe('2');
  });
});
