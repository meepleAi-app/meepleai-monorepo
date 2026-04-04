import { describe, it, expect, beforeEach, vi } from 'vitest';

// Mock idb with an in-memory store
vi.mock('idb', () => {
  const store = new Map<string, unknown>();
  const indexes = new Map<string, unknown[]>();

  const db = {
    put: vi.fn(
      async (
        _storeName: string,
        record: {
          id: string;
          sessionId: string;
          synced: boolean;
          type: string;
          payload: unknown;
          timestamp: string;
        }
      ) => {
        store.set(record.id, record);
        const sessionKey = `session:${record.sessionId}`;
        const existing = (indexes.get(sessionKey) as (typeof record)[] | undefined) ?? [];
        const filtered = existing.filter(r => r.id !== record.id);
        indexes.set(sessionKey, [...filtered, record]);
      }
    ),
    getAllFromIndex: vi.fn(async (_storeName: string, indexName: string, key: string) => {
      if (indexName === 'by-session') {
        return (indexes.get(`session:${key}`) as unknown[]) ?? [];
      }
      return [];
    }),
    delete: vi.fn(async (_storeName: string, id: string) => {
      const record = store.get(id) as { sessionId: string } | undefined;
      if (record) {
        const sessionKey = `session:${record.sessionId}`;
        const existing = (indexes.get(sessionKey) as { id: string }[] | undefined) ?? [];
        indexes.set(
          sessionKey,
          existing.filter(r => r.id !== id)
        );
      }
      store.delete(id);
    }),
  };

  return {
    openDB: vi.fn().mockResolvedValue(db),
  };
});

import {
  queueOperation,
  getPendingOperations,
  clearOperation,
  resetDbInstance,
} from '../toolkitDb';

describe('toolkitDb', () => {
  beforeEach(() => {
    resetDbInstance();
  });

  it('mette in coda un lancio di dado e lo recupera', async () => {
    const op = {
      id: crypto.randomUUID(),
      sessionId: 'sess-1',
      type: 'dice_roll' as const,
      payload: { diceType: 'D6', count: 2 },
      timestamp: new Date().toISOString(),
      synced: false,
    };

    await queueOperation(op);
    const pending = await getPendingOperations('sess-1');

    expect(pending).toHaveLength(1);
    expect(pending[0].type).toBe('dice_roll');
  });

  it('clearOperation rimuove una operazione sincronizzata', async () => {
    const id = crypto.randomUUID();
    await queueOperation({
      id,
      sessionId: 'sess-1',
      type: 'score_update',
      payload: { playerId: 'p1', score: 10 },
      timestamp: new Date().toISOString(),
      synced: false,
    });

    await clearOperation(id);
    const pending = await getPendingOperations('sess-1');
    expect(pending.find(op => op.id === id)).toBeUndefined();
  });
});
