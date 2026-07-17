import { describe, expect, it } from 'vitest';

// @ts-expect-error — .mjs senza tipi, import runtime
import { buildReportHtml } from '../build-report.mjs';

const baseEntry = {
  id: 'library-wishlist',
  label: 'Library · Wishlist',
  route: '/library/wishlist',
  viewport: { width: 1920, height: 1080 },
  mockupDataUri: 'data:image/png;base64,MOCKUP',
  liveDataUri: 'data:image/png;base64,LIVE',
  designIntent: 'current',
};

describe('buildReportHtml', () => {
  it('produces a full self-contained HTML document', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('<!doctype html>');
    expect(html).toContain('Library · Wishlist');
    expect(html).toContain('/library/wishlist');
  });

  it('embeds both mockup and live images as data URIs', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('data:image/png;base64,MOCKUP');
    expect(html).toContain('data:image/png;base64,LIVE');
  });

  it('renders a slider control per pair', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('type="range"');
    expect(html).toContain('data-pair-id="library-wishlist"');
  });

  it('shows an error placeholder when live capture failed', () => {
    const html = buildReportHtml([
      { ...baseEntry, liveDataUri: null, liveError: 'Timeout 30000ms' },
    ]);
    expect(html).toContain('live capture failed');
    expect(html).toContain('Timeout 30000ms');
    expect(html).not.toContain('data:image/png;base64,LIVE');
  });

  it('shows an error placeholder when mockup capture failed', () => {
    const html = buildReportHtml([
      { ...baseEntry, mockupDataUri: null, mockupError: 'unpkg unreachable' },
    ]);
    expect(html).toContain('mockup capture failed');
    expect(html).toContain('unpkg unreachable');
    expect(html).not.toContain('data:image/png;base64,MOCKUP');
  });

  it('escapes HTML in labels and errors', () => {
    const html = buildReportHtml([
      { ...baseEntry, label: '<script>x</script>', liveError: '<b>bad</b>', liveDataUri: null },
    ]);
    expect(html).not.toContain('<script>x</script>');
    expect(html).toContain('&lt;script&gt;');
  });

  it('renders a side-by-side (sbs) block so the toggle shows both images', () => {
    const html = buildReportHtml([baseEntry]);
    expect(html).toContain('class="sbs"');
    // Entrambe le immagini compaiono 2 volte: overlay (stage) + side-by-side (sbs).
    expect((html.match(/data:image\/png;base64,MOCKUP/g) ?? []).length).toBeGreaterThanOrEqual(2);
    expect((html.match(/data:image\/png;base64,LIVE/g) ?? []).length).toBeGreaterThanOrEqual(2);
  });
});
