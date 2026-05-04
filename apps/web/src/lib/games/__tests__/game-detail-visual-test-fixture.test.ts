/**
 * Tests for `/games/[id]` visual-test fixture (Wave C.1, Issue #581).
 *
 * Contract (Phase 0.5 visual fixture sentinel pattern):
 *   - IS_VISUAL_TEST_BUILD === true only when NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'
 *   - tryLoadVisualTestFixture('default') → valid LibraryGameDetail when IS_VISUAL_TEST_BUILD=true
 *   - tryLoadVisualTestFixture('not-found') → null ALWAYS (regardless of IS_VISUAL_TEST_BUILD)
 *   - both return null when IS_VISUAL_TEST_BUILD === false (default in dev/prod)
 *
 * Mirror of: apps/web/src/lib/games/__tests__/library-filters.test.ts (Wave B.3)
 */

import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';

describe('game-detail-visual-test-fixture', () => {
  // ============================================================
  // Test IS_VISUAL_TEST_BUILD === false (default, dev/prod)
  // ============================================================
  describe('when IS_VISUAL_TEST_BUILD is false (default env)', () => {
    beforeEach(() => {
      // env var not set → IS_VISUAL_TEST_BUILD = false
      vi.resetModules();
    });

    it("tryLoadVisualTestFixture('default') returns null", async () => {
      // Module uses process.env at import time — reset modules to clear cached value
      const mod = await import('../game-detail-visual-test-fixture');
      // In test env NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED is not '1'
      // so IS_VISUAL_TEST_BUILD is false and fixture returns null
      expect(mod.IS_VISUAL_TEST_BUILD).toBe(false);
      expect(mod.tryLoadVisualTestFixture('default')).toBeNull();
    });

    it("tryLoadVisualTestFixture('not-found') returns null", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(mod.tryLoadVisualTestFixture('not-found')).toBeNull();
    });
  });

  // ============================================================
  // Test IS_VISUAL_TEST_BUILD behavior and fixture shape
  // ============================================================
  describe('fixture module exports', () => {
    it('exports IS_VISUAL_TEST_BUILD as a boolean constant', async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(typeof mod.IS_VISUAL_TEST_BUILD).toBe('boolean');
    });

    it('exports VISUAL_TEST_FIXTURE_GAME_DETAIL_ID as a UUID-shaped string encoding issue #581', async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(mod.VISUAL_TEST_FIXTURE_GAME_DETAIL_ID).toBe('00000000-0000-4000-8000-000000000581');
    });

    it("exports tryLoadVisualTestFixture as a function accepting 'default' | 'not-found'", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(typeof mod.tryLoadVisualTestFixture).toBe('function');
    });
  });

  // ============================================================
  // Test fixture shape when IS_VISUAL_TEST_BUILD is simulated as true
  // We test by directly calling the underlying fixture logic
  // ============================================================
  describe('tryLoadVisualTestFixture return shape contract', () => {
    it("'not-found' always returns null, even when NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED would be set", async () => {
      // We can't easily set process.env at module import time in vitest without
      // special setup, so we test the contract via the module's documented behavior:
      // the function unconditionally returns null for 'not-found' state.
      const mod = await import('../game-detail-visual-test-fixture');
      // Even if the function were called in a visual test build context,
      // 'not-found' returns null per contract
      const result = mod.tryLoadVisualTestFixture('not-found');
      expect(result).toBeNull();
    });

    it("default parameter is 'default' state", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      // calling with no args should behave as 'default' state
      // (returns null in non-visual-test build, which is our current env)
      const withDefault = mod.tryLoadVisualTestFixture();
      const withExplicit = mod.tryLoadVisualTestFixture('default');
      expect(withDefault).toBe(withExplicit);
    });
  });

  // ============================================================
  // Test with env var set to simulate visual test build
  // Uses module re-import after env manipulation
  // ============================================================
  describe('when NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 (simulated)', () => {
    const originalEnv = process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED;

    beforeEach(() => {
      vi.resetModules();
      process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED = '1';
    });

    afterEach(() => {
      if (originalEnv === undefined) {
        delete process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED;
      } else {
        process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED = originalEnv;
      }
      vi.resetModules();
    });

    it("IS_VISUAL_TEST_BUILD is true when env var is '1'", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(mod.IS_VISUAL_TEST_BUILD).toBe(true);
    });

    it("tryLoadVisualTestFixture('default') returns a valid LibraryGameDetail", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      const result = mod.tryLoadVisualTestFixture('default');

      expect(result).not.toBeNull();
      // Verify the Wingspan-shaped LibraryGameDetail structure
      expect(result).toMatchObject({
        gameTitle: 'Wingspan',
        gamePublisher: 'Stonemaier Games',
        gameYearPublished: 2019,
        isFavorite: expect.any(Boolean),
        libraryEntryId: expect.any(String),
        userId: expect.any(String),
        gameId: expect.any(String),
        addedAt: expect.any(String),
        isAvailableForPlay: expect.any(Boolean),
        hasCustomPdf: expect.any(Boolean),
        hasRagAccess: expect.any(Boolean),
        timesPlayed: expect.any(Number),
        averageRating: expect.any(Number),
      });
    });

    it("tryLoadVisualTestFixture('default') returns an object with recentSessions array", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      const result = mod.tryLoadVisualTestFixture('default');
      expect(result).not.toBeNull();
      expect(Array.isArray(result!.recentSessions)).toBe(true);
      expect(result!.recentSessions!.length).toBeGreaterThan(0);
    });

    it("tryLoadVisualTestFixture('not-found') returns null even in visual test build", async () => {
      const mod = await import('../game-detail-visual-test-fixture');
      expect(mod.IS_VISUAL_TEST_BUILD).toBe(true);
      // 'not-found' always returns null — it's the fixture for a missing game state
      expect(mod.tryLoadVisualTestFixture('not-found')).toBeNull();
    });
  });
});
