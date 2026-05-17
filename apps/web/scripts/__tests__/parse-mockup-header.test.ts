/**
 * Tests for WS-F.1 mockup header parser (refs #1072, umbrella #1066).
 * Spec: docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md §3 WS-F
 */
import { describe, it, expect } from 'vitest';
import {
  parseMockupHeader,
  type MockupHeader,
  VERIFIED_BY_VALUES,
  STATUS_VALUES,
} from '../parse-mockup-header';

function header(extra = ''): string {
  return `<!doctype html>
<html>
<head>
<title>x</title>
<!--
  @route /library/{gameId}
  @last-verified 2026-05-12
  @verified-by maintainer
  @status canonical
  ${extra}
-->
</head>
</html>`;
}

describe('parseMockupHeader', () => {
  it('parses a complete canonical header', () => {
    const result = parseMockupHeader(header());
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.header).toEqual<MockupHeader>({
      route: '/library/{gameId}',
      routes: ['/library/{gameId}'],
      lastVerified: '2026-05-12',
      verifiedBy: 'maintainer',
      status: 'canonical',
    });
  });

  it('treats space-separated @route as array', () => {
    const html = header().replace(
      '@route /library/{gameId}',
      '@route /library/{gameId} /library/alt-id'
    );
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.header.routes).toEqual(['/library/{gameId}', '/library/alt-id']);
    expect(result.header.route).toBe('/library/{gameId}');
  });

  it('accepts header among multiple comments (picks the one with @route)', () => {
    const html = `<!-- random commentary -->
<!doctype html>
<!-- @route /x @last-verified 2026-01-01 @verified-by designer @status pending-implementation -->`;
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(true);
    if (!result.ok) return;
    expect(result.header.status).toBe('pending-implementation');
  });

  it('rejects missing @route', () => {
    const html = header().replace(/@route[^\n]+/, '');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/route/i);
  });

  it('rejects missing @last-verified', () => {
    const html = header().replace(/@last-verified[^\n]+/, '');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/last-verified/i);
  });

  it('rejects malformed @last-verified date', () => {
    const html = header().replace('2026-05-12', '12/05/2026');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/last-verified/i);
  });

  it('rejects unknown @verified-by enum value', () => {
    const html = header().replace('maintainer', 'random-user');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/verified-by/i);
  });

  it('rejects unknown @status enum value', () => {
    const html = header().replace('canonical', 'mystery');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/status/i);
  });

  it('rejects @route without leading slash', () => {
    const html = header().replace('/library/{gameId}', 'library/{gameId}');
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/route/i);
  });

  it('rejects HTML with no comment containing @route', () => {
    const html = '<html><body>nothing here</body></html>';
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.error).toMatch(/route|header/i);
  });

  it('parses each enum value', () => {
    for (const v of VERIFIED_BY_VALUES) {
      const html = header().replace('maintainer', v);
      const result = parseMockupHeader(html);
      expect(result.ok).toBe(true);
    }
    for (const s of STATUS_VALUES) {
      const html = header().replace('canonical', s);
      const result = parseMockupHeader(html);
      expect(result.ok).toBe(true);
    }
  });

  it('tolerates pre-existing freeform comments (Nanolith-style)', () => {
    const html = `<!doctype html>
<!--
  Mockup: nanolith-runthrough-game-detail
  Route:  /library/{gameId}  (gameId = nanolith)
  Scope:  Phase A runthrough
-->
<!--
  @route /library/{gameId}
  @last-verified 2026-05-12
  @verified-by maintainer
  @status canonical
-->`;
    const result = parseMockupHeader(html);
    expect(result.ok).toBe(true);
  });
});
