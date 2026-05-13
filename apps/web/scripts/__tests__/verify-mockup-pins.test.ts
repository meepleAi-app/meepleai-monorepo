/**
 * Tests for WS-C Phase 2 mockup pin verifier (refs #1069, AC-C.8).
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadPolicy, verifyMockup, runCli, type Violation } from '../verify-mockup-pins';

const POLICY = {
  version: 1 as const,
  scripts: [
    {
      name: 'react',
      url: 'https://unpkg.com/react@18.3.1/umd/react.development.js',
      integrity: 'sha384-AAA',
    },
    {
      name: 'react-dom',
      url: 'https://unpkg.com/react-dom@18.3.1/umd/react-dom.development.js',
      integrity: 'sha384-BBB',
    },
  ],
  preconnects: [
    { href: 'https://fonts.googleapis.com' },
    { href: 'https://fonts.gstatic.com', crossorigin: true },
  ],
};

function conformantHtml(): string {
  return `<!DOCTYPE html>
<html><head>
<link rel="preconnect" href="https://fonts.googleapis.com"/>
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>
<script src="https://unpkg.com/react@18.3.1/umd/react.development.js" integrity="sha384-AAA" crossorigin="anonymous"></script>
<script src="https://unpkg.com/react-dom@18.3.1/umd/react-dom.development.js" integrity="sha384-BBB" crossorigin="anonymous"></script>
</head><body></body></html>`;
}

describe('verifyMockup', () => {
  it('returns no violations for a conformant mockup', () => {
    const violations = verifyMockup('test.html', conformantHtml(), POLICY);
    expect(violations).toEqual([]);
  });

  it('skips silently when mockup does not load the script at all (pure-CSS mockup)', () => {
    const html = conformantHtml()
      .replace(/<script src="https:\/\/unpkg.com\/react@[^>]+><\/script>/, '')
      .replace(/<script src="https:\/\/unpkg.com\/react-dom@[^>]+><\/script>/, '');
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toEqual([]);
  });

  it('flags sibling-version drift (e.g. react@19 loaded instead of pinned 18.3.1)', () => {
    const html = conformantHtml().replace(
      /<script src="https:\/\/unpkg.com\/react@18.3.1\/[^>]+><\/script>/,
      '<script src="https://unpkg.com/react@19.0.0/umd/react.development.js"></script>'
    );
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toHaveLength(1);
    expect(violations[0].type).toBe('missing-script');
    expect(violations[0].detail).toContain('different version');
  });

  it('flags wrong integrity hash', () => {
    const html = conformantHtml().replace('sha384-AAA', 'sha384-XXX');
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toHaveLength(1);
    expect(violations[0].type).toBe('wrong-integrity');
    expect(violations[0].detail).toContain('sha384-XXX');
  });

  it('flags missing integrity attribute entirely', () => {
    const html = conformantHtml().replace(/ integrity="sha384-AAA"/, '');
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toHaveLength(1);
    expect(violations[0].type).toBe('wrong-integrity');
    expect(violations[0].detail).toContain('(missing)');
  });

  it('flags missing preconnect', () => {
    const html = conformantHtml().replace(
      /<link rel="preconnect" href="https:\/\/fonts.googleapis.com"\/>/,
      ''
    );
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toHaveLength(1);
    expect(violations[0].type).toBe('missing-preconnect');
  });

  it('flags missing crossorigin on preconnect that requires it', () => {
    const html = conformantHtml().replace(
      '<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin/>',
      '<link rel="preconnect" href="https://fonts.gstatic.com"/>'
    );
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toHaveLength(1);
    expect(violations[0].type).toBe('missing-crossorigin');
  });

  it('accepts preconnect link with attributes in reverse order', () => {
    const html = conformantHtml().replace(
      '<link rel="preconnect" href="https://fonts.googleapis.com"/>',
      '<link href="https://fonts.googleapis.com" rel="preconnect"/>'
    );
    const violations = verifyMockup('test.html', html, POLICY);
    expect(violations).toEqual([]);
  });

  it('reports multiple violations from one file (preconnect-only when no scripts loaded)', () => {
    // Empty mockup: no React loaded → not subject to pin (acceptable per conditional rule).
    // Only preconnects are unconditionally required.
    const broken = '<!DOCTYPE html><html><head></head><body></body></html>';
    const violations = verifyMockup('test.html', broken, POLICY);
    expect(violations).toHaveLength(2); // 2 preconnect violations
    expect(violations.every(v => v.type === 'missing-preconnect')).toBe(true);
  });
});

describe('loadPolicy', () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), 'pin-policy-'));
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it('rejects unsupported version', () => {
    const p = join(tmp, 'p.json');
    writeFileSync(p, JSON.stringify({ ...POLICY, version: 2 }));
    expect(() => loadPolicy(p)).toThrow(/version/);
  });

  it('rejects empty scripts', () => {
    const p = join(tmp, 'p.json');
    writeFileSync(p, JSON.stringify({ version: 1, scripts: [], preconnects: [] }));
    expect(() => loadPolicy(p)).toThrow(/scripts/);
  });

  it('rejects non-array preconnects', () => {
    const p = join(tmp, 'p.json');
    writeFileSync(p, JSON.stringify({ ...POLICY, preconnects: null }));
    expect(() => loadPolicy(p)).toThrow(/preconnects/);
  });
});

describe('runCli integration on real mockups', () => {
  it('passes against current mockups + policy', () => {
    const result = runCli();
    if (!result.ok) {
      // Fail with violation details so CI shows the actual drift.
      const formatted = result.violations
        .map((v: Violation) => `${v.file}: ${v.type} — ${v.detail ?? v.expected}`)
        .join('\n');
      throw new Error(`Pin policy drift detected:\n${formatted}`);
    }
    expect(result.scannedCount).toBeGreaterThan(0);
    expect(result.violations).toEqual([]);
  });
});
