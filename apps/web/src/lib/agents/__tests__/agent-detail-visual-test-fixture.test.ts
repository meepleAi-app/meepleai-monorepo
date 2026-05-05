/**
 * Tests for `/agents/[id]` visual-test fixture (Wave C.2, Issue #581).
 *
 * Covers:
 *   - IS_VISUAL_TEST_BUILD constant (process.env gating)
 *   - tryLoadVisualTestFixture in non-visual-test builds (always returns null)
 *   - tryLoadVisualTestFixture with IS_VISUAL_TEST_BUILD=true (via vi.resetModules + re-import)
 *   - 'default' state: returns active agent WITH gameId set
 *   - 'standalone' state: returns active agent WITH gameId === null (Cell 10)
 *   - 'not-found' state: returns null always (even in visual test builds)
 *
 * Testing strategy for IS_VISUAL_TEST_BUILD:
 *   We test the fixture in the non-test build environment directly (IS_VISUAL_TEST_BUILD=false).
 *   For the true branch, we re-import after overriding process.env via vi.resetModules().
 *   This mirrors Wave B.2/B.3 fixture test pattern.
 */

import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest';

import {
  IS_VISUAL_TEST_BUILD,
  VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID,
  tryLoadVisualTestFixture,
} from '../agent-detail-visual-test-fixture';

describe('agent-detail-visual-test-fixture', () => {
  // ============================================================
  // IS_VISUAL_TEST_BUILD — default (non-visual-test) build
  // ============================================================
  describe('IS_VISUAL_TEST_BUILD constant', () => {
    it('is false in normal test environment (env var not set to "1")', () => {
      // In normal vitest runs, NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED is not '1'
      expect(IS_VISUAL_TEST_BUILD).toBe(false);
    });

    it('VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID is a deterministic UUID sentinel', () => {
      expect(VISUAL_TEST_FIXTURE_AGENT_DETAIL_ID).toBe('00000000-0000-4000-8000-000000000581');
    });
  });

  // ============================================================
  // tryLoadVisualTestFixture — non-visual-test build (IS_VISUAL_TEST_BUILD=false)
  // ============================================================
  describe('tryLoadVisualTestFixture — non-visual-test build', () => {
    it("returns null for 'default' state when not a visual-test build", () => {
      expect(tryLoadVisualTestFixture('default')).toBeNull();
    });

    it("returns null for 'standalone' state when not a visual-test build", () => {
      expect(tryLoadVisualTestFixture('standalone')).toBeNull();
    });

    it("returns null for 'not-found' state when not a visual-test build", () => {
      expect(tryLoadVisualTestFixture('not-found')).toBeNull();
    });

    it("returns null with no argument (default='default') when not a visual-test build", () => {
      expect(tryLoadVisualTestFixture()).toBeNull();
    });
  });

  // ============================================================
  // tryLoadVisualTestFixture — simulated visual-test build
  // We override the env var and re-import the module to test the IS_VISUAL_TEST_BUILD=true branch
  // ============================================================
  describe('tryLoadVisualTestFixture — simulated visual-test build', () => {
    let fixtureModule: typeof import('../agent-detail-visual-test-fixture');

    beforeEach(async () => {
      // Set env var to simulate visual-test build
      process.env['NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED'] = '1';
      vi.resetModules();
      // Re-import after env override and module reset
      fixtureModule = await import('../agent-detail-visual-test-fixture');
    });

    afterEach(() => {
      delete process.env['NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED'];
      vi.resetModules();
    });

    it("IS_VISUAL_TEST_BUILD is true when env var is '1'", () => {
      expect(fixtureModule.IS_VISUAL_TEST_BUILD).toBe(true);
    });

    it("returns AgentDto for 'default' state in visual-test build", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('default');
      expect(result).not.toBeNull();
      expect(result).toMatchObject({
        id: '00000000-0000-4000-8000-000000000581',
        isActive: true,
        invocationCount: 142,
      });
    });

    it("'default' fixture has gameId set (not null) for standard visual baseline", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('default');
      expect(result).not.toBeNull();
      expect(result!.gameId).not.toBeNull();
      expect(result!.gameId).toMatch(/^[0-9a-f-]{36}$/);
    });

    it("returns AgentDto for 'standalone' state — Cell 10 fixture (gameId === null)", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('standalone');
      expect(result).not.toBeNull();
      expect(result).toMatchObject({
        isActive: true,
        gameId: null,
      });
    });

    it("'standalone' fixture has gameId === null (Cell 10 contract)", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('standalone');
      expect(result!.gameId).toBeNull();
    });

    it("'standalone' fixture gameName is null when gameId is null", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('standalone');
      expect(result!.gameName).toBeNull();
    });

    it("returns null for 'not-found' state even in visual-test build", () => {
      const result = fixtureModule.tryLoadVisualTestFixture('not-found');
      expect(result).toBeNull();
    });

    it("'default' and 'standalone' fixtures are distinct agents (different ids)", () => {
      const defaultResult = fixtureModule.tryLoadVisualTestFixture('default');
      const standaloneResult = fixtureModule.tryLoadVisualTestFixture('standalone');
      expect(defaultResult!.id).not.toBe(standaloneResult!.id);
    });

    it('fixture data conforms to AgentDto shape (has required fields)', () => {
      const result = fixtureModule.tryLoadVisualTestFixture('default');
      expect(result).toMatchObject({
        id: expect.any(String),
        name: expect.any(String),
        type: expect.any(String),
        strategyName: expect.any(String),
        strategyParameters: expect.any(Object),
        isActive: expect.any(Boolean),
        createdAt: expect.any(String),
        invocationCount: expect.any(Number),
        isRecentlyUsed: expect.any(Boolean),
        isIdle: expect.any(Boolean),
      });
    });
  });
});
