import { describe, it, expect } from 'vitest';

import { pdfStatusKeys, getAdaptiveInterval } from '@/hooks/queries/usePdfProcessingStatus';

describe('usePdfProcessingStatus', () => {
  describe('query key structure', () => {
    it('generates correct key for a game', () => {
      expect(pdfStatusKeys.byGame('game-123')).toEqual(['pdf-status', 'game-123']);
    });

    it('generates consistent keys for same gameId (deduplication)', () => {
      const key1 = pdfStatusKeys.byGame('game-abc');
      const key2 = pdfStatusKeys.byGame('game-abc');
      expect(key1).toEqual(key2);
    });

    it('generates different keys for different gameIds', () => {
      const key1 = pdfStatusKeys.byGame('game-1');
      const key2 = pdfStatusKeys.byGame('game-2');
      expect(key1).not.toEqual(key2);
    });
  });

  describe('adaptive interval logic', () => {
    it('returns 3s for low cycle counts', () => {
      expect(getAdaptiveInterval(0)).toBe(3_000);
      expect(getAdaptiveInterval(9)).toBe(3_000);
    });

    it('returns 5s after 10 unchanged cycles', () => {
      expect(getAdaptiveInterval(10)).toBe(5_000);
      expect(getAdaptiveInterval(19)).toBe(5_000);
    });

    it('returns 10s after 20 unchanged cycles', () => {
      expect(getAdaptiveInterval(20)).toBe(10_000);
      expect(getAdaptiveInterval(100)).toBe(10_000);
    });
  });
});
