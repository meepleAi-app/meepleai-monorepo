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

import { validate, FidelitySchema } from '../validate-fidelity.mjs';

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
