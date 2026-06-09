/**
 * inject-annotations.test.ts — unit tests for inject-annotations.mjs (DS-17-1)
 *
 * Run: pnpm vitest run scripts/mockup-annotations/__tests__/inject-annotations.test.ts
 *
 * Refs:
 *   - Spec:     docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   - Issue:    #2069 (DS-17-1)
 *   - Umbrella: #2063
 *   - Plan:     docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md §4.3
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, readFileSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import {
  ANNOTATION_MARKER,
  parseMockupsIndex,
  routeToFilePath,
  buildAnnotationBlock,
  injectAnnotation,
  shouldSkipFile,
} from '../inject-annotations.mjs';

const TMP_DIR = resolve(tmpdir(), `inject-annotations-tests-${process.pid}`);

beforeAll(() => {
  if (!existsSync(TMP_DIR)) mkdirSync(TMP_DIR, { recursive: true });
});

afterAll(() => {
  if (existsSync(TMP_DIR)) rmSync(TMP_DIR, { recursive: true, force: true });
});

describe('parseMockupsIndex', () => {
  it('parses a single page-mock row with one mapped route', () => {
    const md = [
      '## Auth & onboarding',
      '',
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `auth-flow.html` | page-mock | `/login`, `/register` |',
      '',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries).toHaveLength(1);
    expect(entries[0]).toMatchObject({
      mockup: 'auth-flow.html',
      type: 'page-mock',
      routes: ['/login', '/register'],
    });
  });

  it('extracts multiple page-mocks across sections', () => {
    const md = [
      '## SP4',
      '',
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `sp4-dashboard.html` | page-mock | `/dashboard` |',
      '| `sp4-game-detail.html` | page-mock | `/games/[id]`, `/library/[gameId]` |',
      '',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries).toHaveLength(2);
    expect(entries[0].routes).toEqual(['/dashboard']);
    expect(entries[1].routes).toEqual(['/games/[id]', '/library/[gameId]']);
  });

  it('strips trailing annotations like (reuse), (partial), (variant) from routes', () => {
    const md = [
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `sp3-faq.html` | page-mock | `/faq`, `/games/[id]/faqs` (reuse) |',
      '| `sp4-upload.html` | page-mock | `/upload`, `/gamebook/upload` (partial) |',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries[0].routes).toEqual(['/faq', '/games/[id]/faqs']);
    expect(entries[1].routes).toEqual(['/upload', '/gamebook/upload']);
  });

  it('skips component-mock and dev-fixture rows', () => {
    const md = [
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `tokens.css` | dev-fixture | Source of truth for design tokens |',
      '| `sp4-citation-pdf-viewer.html` | component-mock | Citation overlay |',
      '| `sp4-dashboard.html` | page-mock | `/dashboard` |',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries).toHaveLength(1);
    expect(entries[0].mockup).toBe('sp4-dashboard.html');
  });

  it('drops rows with no extractable routes', () => {
    const md = [
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `weird.html` | page-mock | (no concrete routes) |',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries).toHaveLength(0);
  });
});

describe('routeToFilePath', () => {
  const routeFiles = [
    'src/app/(authenticated)/dashboard/page.tsx',
    'src/app/(authenticated)/games/[id]/page.tsx',
    'src/app/(authenticated)/library/[gameId]/page.tsx',
    'src/app/(auth)/login/page.tsx',
    'src/app/(public)/faq/page.tsx',
    'src/app/(authenticated)/library/page.tsx',
  ];

  it('matches a simple top-level route', () => {
    expect(routeToFilePath('/dashboard', routeFiles)).toBe(
      'src/app/(authenticated)/dashboard/page.tsx'
    );
  });

  it('matches a dynamic-segment route', () => {
    expect(routeToFilePath('/games/[id]', routeFiles)).toBe(
      'src/app/(authenticated)/games/[id]/page.tsx'
    );
  });

  it('matches a route under a different dynamic segment name (gameId vs id)', () => {
    expect(routeToFilePath('/library/[gameId]', routeFiles)).toBe(
      'src/app/(authenticated)/library/[gameId]/page.tsx'
    );
  });

  it('matches a route under (auth) group', () => {
    expect(routeToFilePath('/login', routeFiles)).toBe('src/app/(auth)/login/page.tsx');
  });

  it('returns null when the route has no corresponding file', () => {
    expect(routeToFilePath('/nonexistent', routeFiles)).toBeNull();
  });

  it('prefers a more specific match over a generic one', () => {
    expect(routeToFilePath('/library', routeFiles)).toBe(
      'src/app/(authenticated)/library/page.tsx'
    );
  });
});

describe('buildAnnotationBlock', () => {
  it('contains the marker for idempotent detection', () => {
    const block = buildAnnotationBlock('admin-mockups/design_files/sp4-dashboard.html');
    expect(block).toContain(ANNOTATION_MARKER);
  });

  it('references the mockup source path', () => {
    const block = buildAnnotationBlock('admin-mockups/design_files/sp4-dashboard.html');
    expect(block).toContain('@mockup admin-mockups/design_files/sp4-dashboard.html');
  });

  it('starts with /** and ends with */', () => {
    const block = buildAnnotationBlock('admin-mockups/design_files/foo.html');
    expect(block.trim().startsWith('/**')).toBe(true);
    expect(block.trim().endsWith('*/')).toBe(true);
  });
});

describe('shouldSkipFile (idempotency guard)', () => {
  it('returns true when the marker already exists', () => {
    const content = `/**\n * existing JSDoc\n *\n * ${ANNOTATION_MARKER}\n */\nexport default function Page() {}`;
    expect(shouldSkipFile(content)).toBe(true);
  });

  it('returns false on a fresh file', () => {
    const content = 'export default function Page() { return <div>hi</div>; }';
    expect(shouldSkipFile(content)).toBe(false);
  });

  it('returns false when an unrelated JSDoc is present', () => {
    const content = '/**\n * Dashboard page\n */\nexport default function Page() {}';
    expect(shouldSkipFile(content)).toBe(false);
  });
});

describe('injectAnnotation', () => {
  it('prepends the annotation block above existing content', () => {
    const original = "import { Foo } from '@/foo';\n\nexport default function Page() {}\n";
    const updated = injectAnnotation(original, 'admin-mockups/design_files/sp4-dashboard.html');
    expect(updated).toContain(ANNOTATION_MARKER);
    expect(updated.indexOf('@mockup')).toBeLessThan(updated.indexOf('import'));
    expect(updated).toContain("import { Foo } from '@/foo';");
  });

  it('is idempotent: running twice yields the same result', () => {
    const original = 'export default function Page() {}\n';
    const once = injectAnnotation(original, 'admin-mockups/design_files/foo.html');
    const twice = injectAnnotation(once, 'admin-mockups/design_files/foo.html');
    expect(twice).toBe(once);
  });

  it('preserves a file that already has a top-of-file JSDoc by placing annotation above it', () => {
    const original =
      "/**\n * Dashboard page\n */\nimport { Foo } from '@/foo';\n\nexport default function Page() {}\n";
    const updated = injectAnnotation(original, 'admin-mockups/design_files/sp4-dashboard.html');
    expect(updated.indexOf(ANNOTATION_MARKER)).toBeLessThan(updated.indexOf(' * Dashboard page'));
    expect(updated).toContain(' * Dashboard page');
  });
});

describe('integration: parseMockupsIndex + routeToFilePath + injectAnnotation', () => {
  it('end-to-end: parse a row, find file, inject annotation, verify idempotency', () => {
    const md = [
      '| File | Type | Mapped routes |',
      '|------|------|---------------|',
      '| `sp4-dashboard.html` | page-mock | `/dashboard` |',
    ].join('\n');
    const entries = parseMockupsIndex(md);
    expect(entries).toHaveLength(1);

    const filePath = resolve(TMP_DIR, 'page.tsx');
    writeFileSync(filePath, 'export default function Page() {}\n', 'utf-8');

    const content = readFileSync(filePath, 'utf-8');
    const updated = injectAnnotation(content, `admin-mockups/design_files/${entries[0].mockup}`);
    writeFileSync(filePath, updated, 'utf-8');

    const reread = readFileSync(filePath, 'utf-8');
    expect(reread).toContain(ANNOTATION_MARKER);
    expect(reread).toContain('@mockup admin-mockups/design_files/sp4-dashboard.html');

    expect(shouldSkipFile(reread)).toBe(true);
  });
});
