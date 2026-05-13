/**
 * Tests for WS-F.1 mockup ownership check (refs #1072).
 */
import { describe, it, expect } from 'vitest';
import { checkMockup, runCli, ALLOWLIST } from '../mockup-ownership-check';

const validHtml = `<!doctype html><html><head>
<title>x</title>
<!--
  @route /library
  @last-verified 2026-05-13
  @verified-by maintainer
  @status canonical
-->
</head></html>`;

const noHeaderHtml = `<!doctype html><html><body>nothing</body></html>`;

describe('checkMockup', () => {
  it('reports ok for valid header in allowlisted file', () => {
    const r = checkMockup('/x/sp4-library-desktop.html', validHtml, ALLOWLIST);
    expect(r.level).toBe('ok');
  });

  it('reports error for missing header in allowlisted file', () => {
    const r = checkMockup('/x/sp4-library-desktop.html', noHeaderHtml, ALLOWLIST);
    expect(r.level).toBe('error');
    expect(r.message).toMatch(/allowlist/i);
  });

  it('reports error for malformed header in allowlisted file', () => {
    const bad = validHtml.replace('canonical', 'mystery');
    const r = checkMockup('/x/sp4-library-desktop.html', bad, ALLOWLIST);
    expect(r.level).toBe('error');
    expect(r.message).toMatch(/status/i);
  });

  it('silently passes non-allowlisted file with no header', () => {
    const r = checkMockup('/x/some-other.html', noHeaderHtml, ALLOWLIST);
    expect(r.level).toBe('ok');
    expect(r.message).toMatch(/non-allowlisted/i);
  });

  it('warns on non-allowlisted file with malformed header', () => {
    const bad = validHtml.replace('canonical', 'mystery');
    const r = checkMockup('/x/some-other.html', bad, ALLOWLIST);
    expect(r.level).toBe('warning');
  });
});

describe('runCli integration on real mockups', () => {
  it('returns ok=true with current backfilled 5 allowlist mockups', () => {
    const { ok, reports } = runCli();
    const errors = reports.filter(r => r.level === 'error');
    if (errors.length > 0) {
      throw new Error(
        `Backfill regression detected:\n` + errors.map(r => `  ${r.file}: ${r.message}`).join('\n')
      );
    }
    expect(ok).toBe(true);
    // Sanity: at least the 5 allowlisted should report ok.
    const okFiles = reports.filter(r => r.level === 'ok' && !r.message.includes('non-allowlisted'));
    expect(okFiles.length).toBeGreaterThanOrEqual(ALLOWLIST.length);
  });
});
