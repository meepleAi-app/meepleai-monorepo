/**
 * Tests for WS-F.3 ownership status dashboard generator (refs #1072).
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import {
  scanMockups,
  scanOne,
  generateStatusMarkdown,
  countByStatus,
  type ScanEntry,
} from '../generate-ownership-status';

const TODAY = new Date('2026-05-13T00:00:00Z');

function withHeader({
  route = '/library',
  lastVerified = '2026-05-13',
  verifiedBy = 'maintainer',
  status = 'canonical',
} = {}): string {
  return `<!doctype html><html><head>
<!--
  @route ${route}
  @last-verified ${lastVerified}
  @verified-by ${verifiedBy}
  @status ${status}
-->
</head></html>`;
}

describe('scanOne', () => {
  it('parses valid header and computes 0-day age', () => {
    const entry = scanOne('/x/a.html', withHeader(), TODAY);
    expect(entry.parseError).toBeNull();
    expect(entry.header?.route).toBe('/library');
    expect(entry.ageDays).toBe(0);
    expect(entry.effectiveStatus).toBe('canonical');
    expect(entry.isStale).toBe(false);
  });

  it('flags canonical as stale when older than 30 days', () => {
    const entry = scanOne('/x/a.html', withHeader({ lastVerified: '2026-04-01' }), TODAY);
    expect(entry.ageDays).toBeGreaterThan(30);
    expect(entry.isStale).toBe(true);
    expect(entry.effectiveStatus).toBe('canonical');
  });

  it('does not flag verified status as stale (already-stale label)', () => {
    const entry = scanOne(
      '/x/a.html',
      withHeader({ status: 'verified', lastVerified: '2026-01-01' }),
      TODAY
    );
    expect(entry.isStale).toBe(false);
    expect(entry.effectiveStatus).toBe('verified');
  });

  it('reports unmapped for HTML with no @route', () => {
    const entry = scanOne('/x/a.html', '<html><body>nothing</body></html>', TODAY);
    expect(entry.effectiveStatus).toBe('unmapped');
    expect(entry.header).toBeNull();
    expect(entry.parseError).toMatch(/route|header/i);
  });

  it('reports parse error for malformed header', () => {
    const entry = scanOne('/x/a.html', withHeader({ status: 'invalid-status' }), TODAY);
    expect(entry.header).toBeNull();
    expect(entry.effectiveStatus).toBe('unmapped');
    expect(entry.parseError).toMatch(/status/i);
  });
});

describe('countByStatus', () => {
  it('counts each enum bucket including unmapped', () => {
    const scan: ScanEntry[] = [
      { effectiveStatus: 'canonical' } as ScanEntry,
      { effectiveStatus: 'canonical' } as ScanEntry,
      { effectiveStatus: 'verified' } as ScanEntry,
      { effectiveStatus: 'pending-implementation' } as ScanEntry,
      { effectiveStatus: 'unmapped' } as ScanEntry,
    ];
    const c = countByStatus(scan);
    expect(c).toEqual({
      canonical: 2,
      verified: 1,
      drifted: 0,
      'pending-implementation': 1,
      archived: 0,
      unmapped: 1,
    });
  });
});

describe('generateStatusMarkdown', () => {
  let tmp: string;

  beforeEach(() => {
    tmp = mkdtempSync(join(tmpdir(), 'ws-f3-'));
  });

  afterEach(() => {
    rmSync(tmp, { recursive: true, force: true });
  });

  it('renders a deterministic header, summary, and detail section', () => {
    writeFileSync(join(tmp, 'a.html'), withHeader({ route: '/library' }));
    writeFileSync(
      join(tmp, 'b.html'),
      withHeader({ route: '/agents', status: 'verified', lastVerified: '2026-01-01' })
    );
    writeFileSync(join(tmp, 'c.html'), '<html>no header</html>');

    const scan = scanMockups(tmp, TODAY);
    const md = generateStatusMarkdown(scan, TODAY);

    expect(md).toContain('# Mockup Ownership Status');
    expect(md).toContain('**Last generated**: 2026-05-13T00:00:00Z');
    expect(md).toContain('| 🟢 canonical | 1 |');
    expect(md).toContain('| 🟡 verified | 1 |');
    expect(md).toContain('| ❌ unmapped (no header) | 1 |');
    expect(md).toContain('| **Total** | **3** |');
    expect(md).toContain('| `a.html` | `/library` | 🟢 canonical |');
    expect(md).toContain('| `b.html` | `/agents` | 🟡 verified |');
    expect(md).toContain('| `c.html` | _(unmapped)_ | ❌ unmapped (no header) |');
  });

  it('appends "(stale)" to canonical entries older than 30d', () => {
    writeFileSync(join(tmp, 'old.html'), withHeader({ lastVerified: '2026-04-01' }));
    const md = generateStatusMarkdown(scanMockups(tmp, TODAY), TODAY);
    expect(md).toMatch(/🟢 canonical _\(stale\)_/);
  });

  it('orders detail rows by status priority then alphabetical', () => {
    writeFileSync(join(tmp, 'z.html'), withHeader({ route: '/z', status: 'archived' }));
    writeFileSync(join(tmp, 'a.html'), withHeader({ route: '/a', status: 'canonical' }));
    writeFileSync(
      join(tmp, 'm.html'),
      withHeader({ route: '/m', status: 'pending-implementation' })
    );
    const md = generateStatusMarkdown(scanMockups(tmp, TODAY), TODAY);

    const lines = md.split('\n');
    const aIdx = lines.findIndex(l => l.includes('`a.html`'));
    const mIdx = lines.findIndex(l => l.includes('`m.html`'));
    const zIdx = lines.findIndex(l => l.includes('`z.html`'));
    expect(aIdx).toBeLessThan(mIdx);
    expect(mIdx).toBeLessThan(zIdx);
  });
});
