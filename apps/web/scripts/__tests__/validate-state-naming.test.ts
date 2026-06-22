/**
 * validate-state-naming.test.ts — unit tests for validate-state-naming.mjs (DS-17 Phase 1 §2071)
 *
 * Run: pnpm vitest run scripts/__tests__/validate-state-naming.test.ts
 *
 * Refs:
 *   - Issue: #2071 (DS-17 Phase 1)
 *   - Pattern: scripts/__tests__/lint-tokens-mockups.test.ts (DS-17-2 #2070)
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import {
  STATE_CATALOG,
  CANONICAL_STATE_REGEX,
  STATE_VARIANT_DETECTOR,
  classifyStateFilename,
  lintMockupStateFiles,
} from '../validate-state-naming.mjs';

const TMP_DIR = resolve(tmpdir(), `validate-state-naming-tests-${process.pid}`);

beforeAll(() => {
  if (!existsSync(TMP_DIR)) mkdirSync(TMP_DIR, { recursive: true });
});

afterAll(() => {
  if (existsSync(TMP_DIR)) rmSync(TMP_DIR, { recursive: true, force: true });
});

describe('STATE_CATALOG', () => {
  it('contains the 6 canonical slots in order', () => {
    expect(Object.keys(STATE_CATALOG)).toEqual(['01', '02', '03', '04', '05', '06']);
  });

  it('maps each NN to the documented label', () => {
    expect(STATE_CATALOG['01']).toBe('default');
    expect(STATE_CATALOG['02']).toBe('empty');
    expect(STATE_CATALOG['03']).toBe('loading');
    expect(STATE_CATALOG['04']).toBe('error');
    expect(STATE_CATALOG['05']).toBe('sse');
    expect(STATE_CATALOG['06']).toBe('offline');
  });

  it('is immutable (frozen)', () => {
    expect(Object.isFrozen(STATE_CATALOG)).toBe(true);
  });
});

describe('STATE_VARIANT_DETECTOR', () => {
  it('matches stems containing `-state-`', () => {
    expect(STATE_VARIANT_DETECTOR.test('sp4-library-desktop-state-02-empty')).toBe(true);
  });

  it('does NOT match bare default stems', () => {
    expect(STATE_VARIANT_DETECTOR.test('sp4-library-desktop')).toBe(false);
    expect(STATE_VARIANT_DETECTOR.test('state-matrix')).toBe(false); // no `-state-` prefix-tail
  });
});

describe('CANONICAL_STATE_REGEX', () => {
  it('captures base, NN, label from a conforming stem', () => {
    const m = 'sp4-library-desktop-state-02-empty'.match(CANONICAL_STATE_REGEX);
    expect(m).not.toBeNull();
    expect(m![1]).toBe('sp4-library-desktop');
    expect(m![2]).toBe('02');
    expect(m![3]).toBe('empty');
  });

  it('rejects single-digit NN', () => {
    expect('sp4-library-desktop-state-2-empty'.match(CANONICAL_STATE_REGEX)).toBeNull();
  });

  it('rejects missing label', () => {
    expect('sp4-library-desktop-state-02'.match(CANONICAL_STATE_REGEX)).toBeNull();
  });

  it('rejects UpperCase label', () => {
    expect('sp4-library-desktop-state-02-Empty'.match(CANONICAL_STATE_REGEX)).toBeNull();
  });
});

describe('classifyStateFilename', () => {
  it('returns null for non-state filenames', () => {
    expect(classifyStateFilename('sp4-library-desktop')).toBeNull();
    expect(classifyStateFilename('settings')).toBeNull();
  });

  it('returns ok for canonical conforming names', () => {
    const r = classifyStateFilename('sp4-library-desktop-state-02-empty');
    expect(r).toEqual({ ok: true, base: 'sp4-library-desktop', nn: '02', label: 'empty' });
  });

  it('flags filename matching `-state-` but missing NN-label', () => {
    const r = classifyStateFilename('sp4-library-state-empty');
    expect(r?.ok).toBe(false);
    expect(r?.reason).toContain('NN prefix');
  });

  it('flags NN outside the catalog', () => {
    const r = classifyStateFilename('sp4-library-state-99-custom');
    expect(r?.ok).toBe(false);
    expect(r?.reason).toContain('outside the canonical catalog');
  });

  it('flags label mismatched against canonical', () => {
    const r = classifyStateFilename('sp4-library-state-02-loading');
    expect(r?.ok).toBe(false);
    expect(r?.reason).toContain('does not match the canonical "empty"');
    expect(r?.suggestion).toContain('sp4-library-state-02-empty');
  });

  it('flags 01-default (forbidden — bare `<base>` IS default)', () => {
    const r = classifyStateFilename('sp4-library-state-01-default');
    expect(r?.ok).toBe(false);
    expect(r?.reason).toContain('01-default is forbidden');
  });

  it('accepts all 5 canonical opt-in/required states', () => {
    for (const [nn, label] of Object.entries(STATE_CATALOG)) {
      if (nn === '01') continue; // 01-default is forbidden as a filename
      const r = classifyStateFilename(`base-state-${nn}-${label}`);
      expect(r?.ok, `state ${nn}-${label} should pass`).toBe(true);
    }
  });
});

describe('lintMockupStateFiles (glob integration)', () => {
  it('returns 0 violations on a clean tree with no state variants', () => {
    const dir = resolve(TMP_DIR, 'admin-mockups/design_files');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'sp4-library-desktop.html'), '<div/>', 'utf-8');

    const result = lintMockupStateFiles('admin-mockups/design_files/**/*.{html,jsx}', TMP_DIR);
    expect(result.violations).toEqual([]);
    expect(result.conforming).toEqual([]);
    expect(result.scanned).toBe(1);
  });

  it('counts conforming state files', () => {
    const dir = resolve(TMP_DIR, 'tree-ok/admin-mockups/design_files');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'sp4-library.html'), '<div/>', 'utf-8');
    writeFileSync(resolve(dir, 'sp4-library-state-02-empty.html'), '<div/>', 'utf-8');
    writeFileSync(resolve(dir, 'sp4-library-state-04-error.html'), '<div/>', 'utf-8');

    const result = lintMockupStateFiles(
      'tree-ok/admin-mockups/design_files/**/*.{html,jsx}',
      TMP_DIR
    );
    expect(result.violations).toEqual([]);
    expect(result.conforming).toHaveLength(2);
    expect(result.scanned).toBe(3);
  });

  it('reports violations', () => {
    const dir = resolve(TMP_DIR, 'tree-bad/admin-mockups/design_files');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'sp4-library-state-empty.html'), '<div/>', 'utf-8');
    writeFileSync(resolve(dir, 'sp4-other-state-99-typo.jsx'), '<div/>', 'utf-8');

    const result = lintMockupStateFiles(
      'tree-bad/admin-mockups/design_files/**/*.{html,jsx}',
      TMP_DIR
    );
    expect(result.violations).toHaveLength(2);
    expect(result.violations[0].file).toContain('sp4-library-state-empty.html');
    expect(result.violations[1].file).toContain('sp4-other-state-99-typo.jsx');
  });
});
