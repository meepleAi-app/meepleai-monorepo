/**
 * Tests for agent detail variant derivation (Wave C.2, Issue #581).
 *
 * Covers the Phase 0.5 contract sez. 3.3 variant matrix:
 *   - 'active'   → agent is active and has been invoked (isActive=true, invocationCount>0)
 *   - 'draft'    → agent is active but never invoked (isActive=true, invocationCount=0)
 *   - 'archived' → agent is inactive/archived (isActive=false OR archivedAt!=null)
 *
 * Backend schema note: AgentDto does not expose archivedAt or systemPrompt.
 * Variant derivation falls back to isActive + invocationCount (canonical fields).
 * The function accepts optional archivedAt/systemPrompt for forward-compatibility.
 */

import { describe, it, expect } from 'vitest';

import {
  type AgentVariant,
  type AgentVariantInput,
  deriveAgentVariant,
} from '../agent-detail-variant';

// Helper: minimal active agent
const activeAgent = (overrides: Partial<AgentVariantInput> = {}): AgentVariantInput => ({
  isActive: true,
  invocationCount: 10,
  ...overrides,
});

describe('deriveAgentVariant', () => {
  // ============================================================
  // 'active' variant
  // ============================================================
  describe("'active' variant", () => {
    it("returns 'active' for isActive=true and invocationCount>0", () => {
      expect(deriveAgentVariant(activeAgent())).toBe<AgentVariant>('active');
    });

    it("returns 'active' with high invocation count", () => {
      expect(deriveAgentVariant(activeAgent({ invocationCount: 999 }))).toBe<AgentVariant>(
        'active'
      );
    });

    it("returns 'active' when systemPrompt is non-empty (forward compat)", () => {
      expect(
        deriveAgentVariant(
          activeAgent({ systemPrompt: 'You are a board game expert.', invocationCount: 5 })
        )
      ).toBe<AgentVariant>('active');
    });
  });

  // ============================================================
  // 'draft' variant
  // ============================================================
  describe("'draft' variant", () => {
    it("returns 'draft' for isActive=true and invocationCount=0", () => {
      expect(deriveAgentVariant(activeAgent({ invocationCount: 0 }))).toBe<AgentVariant>('draft');
    });

    it("returns 'draft' when systemPrompt is null (forward compat — setup not complete)", () => {
      expect(
        deriveAgentVariant(activeAgent({ systemPrompt: null, invocationCount: 5 }))
      ).toBe<AgentVariant>('draft');
    });

    it("returns 'draft' when systemPrompt is empty string (forward compat)", () => {
      expect(
        deriveAgentVariant(activeAgent({ systemPrompt: '', invocationCount: 5 }))
      ).toBe<AgentVariant>('draft');
    });
  });

  // ============================================================
  // 'archived' variant
  // ============================================================
  describe("'archived' variant", () => {
    it("returns 'archived' for isActive=false (canonical field)", () => {
      expect(deriveAgentVariant({ isActive: false, invocationCount: 0 })).toBe<AgentVariant>(
        'archived'
      );
    });

    it("returns 'archived' for isActive=false even with high invocationCount", () => {
      expect(deriveAgentVariant({ isActive: false, invocationCount: 142 })).toBe<AgentVariant>(
        'archived'
      );
    });

    it("returns 'archived' when archivedAt is set (explicit archive timestamp)", () => {
      expect(
        deriveAgentVariant(
          activeAgent({ archivedAt: '2026-04-15T12:00:00.000Z', invocationCount: 10 })
        )
      ).toBe<AgentVariant>('archived');
    });

    it('archivedAt takes precedence over isActive=true', () => {
      // If archivedAt is set, always archived regardless of isActive
      expect(
        deriveAgentVariant({
          isActive: true, // theoretically active
          invocationCount: 5,
          archivedAt: '2026-03-01T00:00:00.000Z',
        })
      ).toBe<AgentVariant>('archived');
    });
  });

  // ============================================================
  // Edge cases
  // ============================================================
  describe('Edge cases', () => {
    it('treats archivedAt=null as not archived (explicit null)', () => {
      expect(
        deriveAgentVariant(activeAgent({ archivedAt: null, invocationCount: 5 }))
      ).toBe<AgentVariant>('active');
    });

    it('ignores archivedAt=undefined (field absent)', () => {
      // When archivedAt is not in the object at all, should fall through to invocationCount
      const agent: AgentVariantInput = { isActive: true, invocationCount: 3 };
      expect(deriveAgentVariant(agent)).toBe<AgentVariant>('active');
    });

    it('ignores systemPrompt=undefined (field absent) — falls back to invocationCount=0 → draft', () => {
      const agent: AgentVariantInput = { isActive: true, invocationCount: 0 };
      expect(deriveAgentVariant(agent)).toBe<AgentVariant>('draft');
    });
  });
});
