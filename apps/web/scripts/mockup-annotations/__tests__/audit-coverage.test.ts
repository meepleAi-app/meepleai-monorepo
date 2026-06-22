/**
 * audit-coverage.test.ts — unit tests for audit-coverage.mjs (DS-17-1)
 *
 * Run: pnpm vitest run scripts/mockup-annotations/__tests__/audit-coverage.test.ts
 *
 * Refs:
 *   - Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   - Issue:    #2069 (DS-17-1)
 *   - Umbrella: #2063
 *   - Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.3
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import {
  ANNOTATION_MARKER,
  collectCoverage,
  formatCoverageMarkdown,
  evaluateThreshold,
  mappableRouteSet,
} from '../audit-coverage.mjs';

const TMP_DIR = resolve(tmpdir(), `audit-coverage-tests-${process.pid}`);

beforeAll(() => {
  if (!existsSync(TMP_DIR)) mkdirSync(TMP_DIR, { recursive: true });
});

afterAll(() => {
  if (existsSync(TMP_DIR)) rmSync(TMP_DIR, { recursive: true, force: true });
});

function writeRoute(relPath, content) {
  const full = resolve(TMP_DIR, relPath);
  mkdirSync(resolve(full, '..'), { recursive: true });
  writeFileSync(full, content, 'utf-8');
  return full;
}

const ANNOTATED = `/**\n * @mockup admin-mockups/design_files/foo.html\n *\n * ${ANNOTATION_MARKER} marker — do not edit.\n */\nexport default function Page() {}\n`;
const UNANNOTATED = 'export default function Page() {}\n';

describe('collectCoverage', () => {
  it('reports 0/0 coverage when no route files exist', () => {
    const subdir = resolve(TMP_DIR, 'empty-app');
    mkdirSync(subdir, { recursive: true });
    const stats = collectCoverage('empty-app/**/page.tsx', TMP_DIR);
    expect(stats.total).toBe(0);
    expect(stats.covered).toBe(0);
    expect(stats.uncovered).toEqual([]);
    expect(stats.coverage).toBe(0);
  });

  it('reports 100% when every route has the annotation marker', () => {
    writeRoute('full/app/(authenticated)/a/page.tsx', ANNOTATED);
    writeRoute('full/app/(authenticated)/b/page.tsx', ANNOTATED);
    const stats = collectCoverage('full/app/**/page.tsx', TMP_DIR);
    expect(stats.total).toBe(2);
    expect(stats.covered).toBe(2);
    expect(stats.coverage).toBe(100);
    expect(stats.uncovered).toEqual([]);
  });

  it('reports partial coverage with uncovered paths listed', () => {
    writeRoute('mixed/app/(authenticated)/x/page.tsx', ANNOTATED);
    writeRoute('mixed/app/(authenticated)/y/page.tsx', UNANNOTATED);
    writeRoute('mixed/app/(authenticated)/z/page.tsx', UNANNOTATED);
    const stats = collectCoverage('mixed/app/**/page.tsx', TMP_DIR);
    expect(stats.total).toBe(3);
    expect(stats.covered).toBe(1);
    expect(stats.coverage).toBeCloseTo(33.33, 1);
    expect(stats.uncovered).toHaveLength(2);
    expect(stats.uncovered.some(p => p.endsWith('y/page.tsx'))).toBe(true);
    expect(stats.uncovered.some(p => p.endsWith('z/page.tsx'))).toBe(true);
  });

  it('uncovered list is sorted for stable output', () => {
    writeRoute('sorted/app/(auth)/zebra/page.tsx', UNANNOTATED);
    writeRoute('sorted/app/(auth)/apple/page.tsx', UNANNOTATED);
    writeRoute('sorted/app/(auth)/mango/page.tsx', UNANNOTATED);
    const stats = collectCoverage('sorted/app/**/page.tsx', TMP_DIR);
    expect(stats.uncovered).toHaveLength(3);
    expect(stats.uncovered[0]).toContain('apple');
    expect(stats.uncovered[1]).toContain('mango');
    expect(stats.uncovered[2]).toContain('zebra');
  });
});

describe('formatCoverageMarkdown', () => {
  it('emits a header with the coverage number', () => {
    const stats = { total: 10, covered: 8, uncovered: ['a.tsx', 'b.tsx'], coverage: 80 };
    const md = formatCoverageMarkdown(stats, { threshold: 80 });
    expect(md).toContain('Mockup Annotation Coverage');
    expect(md).toContain('80');
    expect(md).toContain('8 / 10');
  });

  it('lists uncovered files in a table', () => {
    const stats = {
      total: 3,
      covered: 1,
      uncovered: ['src/app/(authenticated)/y/page.tsx', 'src/app/(authenticated)/z/page.tsx'],
      coverage: 33.33,
    };
    const md = formatCoverageMarkdown(stats, { threshold: 80 });
    expect(md).toContain('src/app/(authenticated)/y/page.tsx');
    expect(md).toContain('src/app/(authenticated)/z/page.tsx');
  });

  it('flags below-threshold runs in the header', () => {
    const stats = { total: 10, covered: 5, uncovered: [], coverage: 50 };
    const md = formatCoverageMarkdown(stats, { threshold: 80 });
    expect(md.toLowerCase()).toContain('below threshold');
  });

  it('flags above-threshold runs with a pass marker', () => {
    const stats = { total: 10, covered: 9, uncovered: [], coverage: 90 };
    const md = formatCoverageMarkdown(stats, { threshold: 80 });
    expect(md.toLowerCase()).toMatch(/threshold met|pass/i);
  });
});

describe('collectCoverage with denominator=mappable', () => {
  it('restricts the denominator to MOCKUPS_INDEX mappable routes', () => {
    const subdir = resolve(TMP_DIR, 'mappable');
    mkdirSync(subdir, { recursive: true });

    const indexMd = [
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `auth-flow.html` | page-mock | `/login` |',
      '| `sp4-dashboard.html` | page-mock | `/dashboard` |',
    ].join('\n');
    writeFileSync(resolve(subdir, 'INDEX.md'), indexMd, 'utf-8');

    writeRoute('mappable/app/(auth)/login/page.tsx', ANNOTATED);
    writeRoute('mappable/app/(authenticated)/dashboard/page.tsx', UNANNOTATED);
    writeRoute('mappable/app/(authenticated)/admin/page.tsx', UNANNOTATED);

    const routeFiles = [
      'mappable/app/(auth)/login/page.tsx',
      'mappable/app/(authenticated)/dashboard/page.tsx',
      'mappable/app/(authenticated)/admin/page.tsx',
    ];
    const mappable = mappableRouteSet(routeFiles, resolve(subdir, 'INDEX.md'));

    expect(mappable.size).toBe(2);
    expect(mappable.has('mappable/app/(auth)/login/page.tsx')).toBe(true);
    expect(mappable.has('mappable/app/(authenticated)/dashboard/page.tsx')).toBe(true);
    expect(mappable.has('mappable/app/(authenticated)/admin/page.tsx')).toBe(false);
  });

  it('returns empty set when MOCKUPS_INDEX is missing', () => {
    const noIndex = resolve(TMP_DIR, 'NONEXISTENT.md');
    expect(mappableRouteSet(['src/app/whatever/page.tsx'], noIndex).size).toBe(0);
  });
});

describe('evaluateThreshold', () => {
  it('passes when coverage meets threshold exactly', () => {
    const r = evaluateThreshold({ coverage: 80 }, 80);
    expect(r.pass).toBe(true);
  });

  it('passes when coverage exceeds threshold', () => {
    const r = evaluateThreshold({ coverage: 85 }, 80);
    expect(r.pass).toBe(true);
  });

  it('fails when coverage is below threshold', () => {
    const r = evaluateThreshold({ coverage: 79.99 }, 80);
    expect(r.pass).toBe(false);
    expect(r.diff).toBeCloseTo(0.01, 2);
  });

  it('passes when total is zero (nothing to cover)', () => {
    const r = evaluateThreshold({ coverage: 0, total: 0 }, 80);
    expect(r.pass).toBe(true);
  });
});
