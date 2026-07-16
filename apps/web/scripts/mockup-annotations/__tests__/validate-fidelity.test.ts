/**
 * validate-fidelity.test.mjs — unit tests for validate-fidelity.mjs (DS-17-4)
 *
 * Run: pnpm vitest run scripts/mockup-annotations/__tests__/validate-fidelity.test.mjs
 *      (vitest picks .mjs via vitest.config.ts pattern; if not, add to include glob)
 *
 * Refs: #2072 / #2063
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, unlinkSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import {
  validate,
  FidelitySchema,
  checkApprovalStatus,
  computeGateVerdict,
  P250_WAIVER_TOKEN,
} from '../validate-fidelity.mjs';

const TMP_DIR = resolve(tmpdir(), `mockup-fidelity-tests-${process.pid}`);

// Mockup source file we reference in fidelity fixtures must actually exist.
// Use a real file from admin-mockups/design_files/ for cross-reference success.
const REAL_MOCKUP = 'admin-mockups/design_files/sp4-dashboard.html';

beforeAll(() => {
  if (!existsSync(TMP_DIR)) {
    mkdirSync(TMP_DIR, { recursive: true });
  }
});

afterAll(() => {
  if (existsSync(TMP_DIR)) {
    rmSync(TMP_DIR, { recursive: true, force: true });
  }
});

function writeFixture(name, content) {
  const filePath = resolve(TMP_DIR, name);
  writeFixtureBody(filePath, content);
  return filePath;
}

function writeFixtureBody(filePath, content) {
  const body = typeof content === 'string' ? content : JSON.stringify(content, null, 2);
  writeFileSync(filePath, body, 'utf-8');
}

describe('FidelitySchema (zod)', () => {
  it('accepts minimal valid object with defaults', () => {
    const result = FidelitySchema.safeParse({
      mockup: {
        source: REAL_MOCKUP,
        states: ['default'],
      },
      acceptance: {
        states_covered: ['default'],
      },
    });
    expect(result.success).toBe(true);
    expect(result.data?.acceptance.visual_diff_max_px).toBe(5);
    expect(result.data?.acceptance.a11y_axe).toBe('AA');
  });

  it('rejects unknown state name', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['typo-state'] },
      acceptance: { states_covered: ['default'] },
    });
    expect(result.success).toBe(false);
  });

  it('rejects negative visual_diff_max_px', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: { states_covered: ['default'], visual_diff_max_px: -1 },
    });
    expect(result.success).toBe(false);
  });

  it('rejects invalid date format', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: { states_covered: ['default'], designer_approved_on: '2026/06/09' },
    });
    expect(result.success).toBe(false);
  });
});

describe('validate() cross-reference checks', () => {
  it('PASS — valid fixture referencing existing mockup', async () => {
    const file = writeFixture('valid.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default', 'loading'] },
      acceptance: { states_covered: ['default', 'loading'] },
    });
    const result = await validate(file);
    expect(result.ok).toBe(true);
  });

  it('FAIL — mockup.source does not exist', async () => {
    const file = writeFixture('bad-source.fidelity.json', {
      mockup: { source: 'admin-mockups/design_files/nonexistent.html', states: ['default'] },
      acceptance: { states_covered: ['default'] },
    });
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('mockup.source'))).toBe(true);
  });

  it('FAIL — states and states_covered mismatch (set inequality)', async () => {
    const file = writeFixture('state-mismatch.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default', 'loading'] },
      acceptance: { states_covered: ['default'] }, // missing 'loading'
    });
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('states_covered'))).toBe(true);
  });

  it('FAIL — story_path referenced but missing (Phase 2+ check)', async () => {
    const file = writeFixture('missing-story.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        story_path: 'apps/web/.storybook/stories/nonexistent.stories.tsx',
      },
    });
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('story_path'))).toBe(true);
  });

  it('FAIL — file not found', async () => {
    const result = await validate(resolve(TMP_DIR, 'does-not-exist.fidelity.json'));
    expect(result.ok).toBe(false);
    expect(result.errors[0]).toMatch(/File not found/i);
  });

  it('FAIL — unsupported file extension', async () => {
    const file = writeFixture('weird.fidelity.txt', 'not yaml not json');
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('Unsupported file extension'))).toBe(true);
  });
});

describe('FidelitySchema design_intent + viewports (DEC-P3-1+2+4)', () => {
  it('rejects unknown design_intent enum value', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        design_intent: 'unknown-intent',
      },
    });
    expect(result.success).toBe(false);
  });

  it('defaults design_intent to "current" when omitted', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: { states_covered: ['default'] },
    });
    expect(result.success).toBe(true);
    expect(result.data?.acceptance.design_intent).toBe('current');
    expect(result.data?.acceptance.viewports).toEqual(['desktop']);
  });

  it('FAIL — design_intent="forward-refactor-obsolete" requires obsolete_tracking_issue', async () => {
    const file = writeFixture('obsolete-no-tracking.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        design_intent: 'forward-refactor-obsolete',
        // obsolete_tracking_issue intentionally missing
      },
    });
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('obsolete_tracking_issue'))).toBe(true);
  });

  it('accepts design_intent="deferred" (#2063 ratchet enum extension)', () => {
    const result = FidelitySchema.safeParse({
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        design_intent: 'deferred',
        obsolete_tracking_issue: '#2234',
      },
    });
    expect(result.success).toBe(true);
  });

  it('FAIL — design_intent="deferred" requires obsolete_tracking_issue', async () => {
    const file = writeFixture('deferred-no-tracking.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        design_intent: 'deferred',
        // obsolete_tracking_issue intentionally missing
      },
    });
    const result = await validate(file);
    expect(result.ok).toBe(false);
    expect(result.errors.some(e => e.includes('obsolete_tracking_issue'))).toBe(true);
  });
});

// ---------------------------------------------------------------------------
// ADR-077 — Designer signoff attestation (Option D)
// ---------------------------------------------------------------------------

describe('checkApprovalStatus (ADR-077 signoff)', () => {
  const withApprover = (design_intent: string, designer_approved_by: string) => ({
    acceptance: { design_intent, designer_approved_by },
  });

  it('FAIL — design_intent="current" with empty designer_approved_by', () => {
    const errors = checkApprovalStatus(withApprover('current', ''));
    expect(errors.length).toBe(1);
    expect(errors[0]).toContain('designer_approved_by');
  });

  it('FAIL — design_intent="current" with whitespace-only approver (trim)', () => {
    const errors = checkApprovalStatus(withApprover('current', '   '));
    expect(errors.length).toBe(1);
  });

  it('PASS — design_intent="current" with P250 self-waiver token', () => {
    const errors = checkApprovalStatus(
      withApprover('current', `user@meepleAi (${P250_WAIVER_TOKEN}, single-person team)`)
    );
    expect(errors).toEqual([]);
  });

  it('PASS — design_intent="current" with a real approver name', () => {
    const errors = checkApprovalStatus(withApprover('current', 'Jane Designer'));
    expect(errors).toEqual([]);
  });

  it('PASS (advisory) — non-current intents are not signoff-gated even when empty', () => {
    expect(checkApprovalStatus(withApprover('forward-refactor', ''))).toEqual([]);
    expect(checkApprovalStatus(withApprover('forward-refactor-obsolete', ''))).toEqual([]);
    expect(checkApprovalStatus(withApprover('deferred', ''))).toEqual([]);
  });

  it('treats an absent design_intent as "current" (schema default parity)', () => {
    const errors = checkApprovalStatus({ acceptance: { designer_approved_by: '' } });
    expect(errors.length).toBe(1);
  });
});

describe('validate() surfaces approvalErrors separately from structural ok', () => {
  it('current + empty approver → structurally ok:true but approvalErrors populated', async () => {
    const file = writeFixture('signoff-missing.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: { states_covered: ['default'] }, // design_intent defaults current, approver empty
    });
    const result = await validate(file);
    expect(result.ok).toBe(true); // structural validity unchanged (non-breaking)
    expect(result.approvalErrors.length).toBe(1);
  });

  it('current + P250 waiver → ok:true, no approvalErrors', async () => {
    const file = writeFixture('signoff-waiver.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        designer_approved_by: `dev@meepleAi (${P250_WAIVER_TOKEN}, single-person team)`,
        designer_approved_on: '2026-06-15',
      },
    });
    const result = await validate(file);
    expect(result.ok).toBe(true);
    expect(result.approvalErrors).toEqual([]);
  });

  it('forward-refactor + empty approver → ok:true, no approvalErrors (advisory)', async () => {
    const file = writeFixture('fr-no-signoff.fidelity.json', {
      mockup: { source: REAL_MOCKUP, states: ['default'] },
      acceptance: {
        states_covered: ['default'],
        design_intent: 'forward-refactor',
      },
    });
    const result = await validate(file);
    expect(result.ok).toBe(true);
    expect(result.approvalErrors).toEqual([]);
  });
});

describe('computeGateVerdict (strict signoff + baselined structural)', () => {
  const ok = () => ({ ok: true, approvalErrors: [] });
  const structuralFail = () => ({ ok: false, errors: ['bad source'], approvalErrors: [] });
  const signoffFail = () => ({ ok: true, approvalErrors: ['missing signoff'] });

  it('all pass → pass:true', () => {
    const v = computeGateVerdict([ok(), ok()], 0);
    expect(v.pass).toBe(true);
    expect(v.structuralFailCount).toBe(0);
    expect(v.approvalFailCount).toBe(0);
  });

  it('structural failures within baseline, no signoff → pass:true', () => {
    const v = computeGateVerdict([ok(), structuralFail(), structuralFail(), structuralFail()], 3);
    expect(v.pass).toBe(true);
    expect(v.structuralFailCount).toBe(3);
    expect(v.overBaseline).toBe(false);
  });

  it('structural failures exceeding baseline → pass:false', () => {
    const v = computeGateVerdict(
      [structuralFail(), structuralFail(), structuralFail(), structuralFail()],
      3
    );
    expect(v.pass).toBe(false);
    expect(v.overBaseline).toBe(true);
  });

  it('signoff failure → pass:false regardless of baseline', () => {
    const v = computeGateVerdict([ok(), signoffFail()], 100);
    expect(v.pass).toBe(false);
    expect(v.approvalFailCount).toBe(1);
  });

  it('signoff failure + structural within baseline → still pass:false', () => {
    const v = computeGateVerdict([signoffFail(), structuralFail()], 3);
    expect(v.pass).toBe(false);
  });
});
