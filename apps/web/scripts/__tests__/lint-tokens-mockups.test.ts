/**
 * lint-tokens-mockups.test.ts — unit tests for lint-tokens-mockups.mjs (DS-17-2)
 *
 * Run: pnpm vitest run scripts/__tests__/lint-tokens-mockups.test.ts
 *
 * Refs:
 *   - Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   - Issue:    #2070 (DS-17-2)
 *   - Umbrella: #2063 (DS-17 Mockup-to-App Fidelity)
 *   - Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.1
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import { LEGACY_TOKEN_REGEX, findViolationsInText, lintFiles } from '../lint-tokens-mockups.mjs';

const TMP_DIR = resolve(tmpdir(), `lint-tokens-mockups-tests-${process.pid}`);

beforeAll(() => {
  if (!existsSync(TMP_DIR)) mkdirSync(TMP_DIR, { recursive: true });
});

afterAll(() => {
  if (existsSync(TMP_DIR)) rmSync(TMP_DIR, { recursive: true, force: true });
});

describe('LEGACY_TOKEN_REGEX', () => {
  it('matches all 4 forbidden token families', () => {
    const samples = [
      'color: var(--bg-base);',
      'background: var(--gaming-blue);',
      'border-color: var(--nh-bg-2);',
      'fill: var(--e-1);',
    ];
    for (const s of samples) {
      const re = new RegExp(LEGACY_TOKEN_REGEX.source, LEGACY_TOKEN_REGEX.flags);
      expect(re.test(s), `should match: ${s}`).toBe(true);
    }
  });

  it('does NOT match canonical semantic tokens', () => {
    const samples = [
      'color: var(--background);',
      'background: var(--foreground);',
      'border-color: var(--muted-foreground);',
      'color: var(--card);',
      'background: var(--border-strong);',
      'color: var(--primary);',
    ];
    for (const s of samples) {
      const re = new RegExp(LEGACY_TOKEN_REGEX.source, LEGACY_TOKEN_REGEX.flags);
      expect(re.test(s), `should NOT match: ${s}`).toBe(false);
    }
  });

  it('does NOT match unrelated var() patterns', () => {
    const samples = [
      'transition: var(--transition);',
      'box-shadow: var(--shadow-md);',
      'color: var(--accent);',
    ];
    for (const s of samples) {
      const re = new RegExp(LEGACY_TOKEN_REGEX.source, LEGACY_TOKEN_REGEX.flags);
      expect(re.test(s), `should NOT match: ${s}`).toBe(false);
    }
  });
});

describe('findViolationsInText', () => {
  it('returns empty array for clean text', () => {
    const result = findViolationsInText('body { color: var(--foreground); }', 'clean.css');
    expect(result).toEqual([]);
  });

  it('detects single violation with line and column', () => {
    const text = 'body {\n  color: var(--bg-base);\n}';
    const result = findViolationsInText(text, 'sample.css');
    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({
      file: 'sample.css',
      line: 2,
      token: 'bg-base',
    });
    expect(result[0].column).toBeGreaterThan(0);
  });

  it('detects multiple violations across all 4 families on multi-line input', () => {
    const text = [
      'body {',
      '  color: var(--bg-base);',
      '  background: var(--gaming-blue);',
      '  border: 1px solid var(--nh-bg-2);',
      '  fill: var(--e-1);',
      '}',
    ].join('\n');
    const result = findViolationsInText(text, 'multi.css');
    expect(result).toHaveLength(4);
    const tokens = result.map(v => v.token).sort();
    expect(tokens).toEqual(['bg-base', 'e-1', 'gaming-blue', 'nh-bg-2']);
  });

  it('detects multiple violations on the same line', () => {
    const text = 'background: linear-gradient(var(--bg-base), var(--gaming-blue));';
    const result = findViolationsInText(text, 'same-line.css');
    expect(result).toHaveLength(2);
    expect(result[0].line).toBe(1);
    expect(result[1].line).toBe(1);
  });

  it('handles empty input', () => {
    expect(findViolationsInText('', 'empty.css')).toEqual([]);
  });
});

describe('lintFiles (glob integration)', () => {
  it('finds violations across HTML, CSS, JSX files in a directory', () => {
    const subdir = resolve(TMP_DIR, 'lint-mixed');
    mkdirSync(subdir, { recursive: true });

    writeFileSync(
      resolve(subdir, 'page.html'),
      '<style>body { background: var(--bg-base); }</style>',
      'utf-8'
    );
    writeFileSync(resolve(subdir, 'theme.css'), 'a { color: var(--gaming-blue); }', 'utf-8');
    writeFileSync(
      resolve(subdir, 'comp.jsx'),
      'const styles = { color: "var(--nh-bg-2)" };',
      'utf-8'
    );
    writeFileSync(resolve(subdir, 'clean.css'), 'body { color: var(--foreground); }', 'utf-8');

    const result = lintFiles('lint-mixed/**/*.{html,jsx,css}', TMP_DIR);
    expect(result.fileCount).toBe(4);
    expect(result.violations).toHaveLength(3);

    const filesWithViolations = new Set(result.violations.map(v => v.file));
    expect(filesWithViolations.size).toBe(3);
  });

  it('returns zero violations when no legacy tokens are present', () => {
    const subdir = resolve(TMP_DIR, 'lint-clean');
    mkdirSync(subdir, { recursive: true });
    writeFileSync(
      resolve(subdir, 'page.html'),
      '<style>body { color: var(--foreground); }</style>',
      'utf-8'
    );

    const result = lintFiles('lint-clean/**/*.{html,jsx,css}', TMP_DIR);
    expect(result.fileCount).toBe(1);
    expect(result.violations).toEqual([]);
  });

  it('respects ignore patterns', () => {
    const subdir = resolve(TMP_DIR, 'lint-ignore');
    mkdirSync(resolve(subdir, 'node_modules'), { recursive: true });
    writeFileSync(
      resolve(subdir, 'node_modules', 'noise.css'),
      'body { color: var(--bg-base); }',
      'utf-8'
    );
    writeFileSync(resolve(subdir, 'real.css'), 'body { color: var(--bg-base); }', 'utf-8');

    const result = lintFiles('lint-ignore/**/*.{html,jsx,css}', TMP_DIR);
    expect(result.violations).toHaveLength(1);
    expect(result.violations[0].file).toContain('real.css');
  });

  it('returns sorted, stable output (file then line then column)', () => {
    const subdir = resolve(TMP_DIR, 'lint-stable');
    mkdirSync(subdir, { recursive: true });
    writeFileSync(resolve(subdir, 'b-second.css'), 'a { color: var(--gaming-blue); }', 'utf-8');
    writeFileSync(
      resolve(subdir, 'a-first.css'),
      'a { color: var(--bg-base); }\nb { color: var(--gaming-blue); }',
      'utf-8'
    );

    const result = lintFiles('lint-stable/**/*.{html,jsx,css}', TMP_DIR);
    expect(result.violations).toHaveLength(3);
    expect(result.violations[0].file).toContain('a-first.css');
    expect(result.violations[2].file).toContain('b-second.css');
  });
});
