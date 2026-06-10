/**
 * Tests for `safe-loader.ts` (issue #2123 custom Next.js Image loader).
 */
import { beforeEach, describe, expect, it, vi } from 'vitest';

import safeImageLoader from '../safe-loader';

describe('safeImageLoader (#2123)', () => {
  beforeEach(() => {
    // Most tests run in jsdom; explicitly stub `navigator.sendBeacon` so
    // assertions on the beacon path stay deterministic and don't depend on
    // browser polyfills.
    if (typeof navigator !== 'undefined') {
      // @ts-expect-error — overriding for the test
      navigator.sendBeacon = vi.fn(() => true);
    }
  });

  it('returns the placeholder path for a BGG host src', () => {
    const url = safeImageLoader({ src: 'https://cf.geekdo-images.com/foo.jpg', width: 600 });
    expect(url).toBe('/placeholder.svg');
  });

  it('returns the placeholder path for null/empty src', () => {
    // shouldUsePlaceholder() returns true for empty strings; the loader cannot
    // receive null/undefined per Next.js types but should still tolerate '' .
    const url = safeImageLoader({ src: '', width: 600 });
    expect(url).toBe('/placeholder.svg');
  });

  it('passes through R2-hosted src with width and quality query params', () => {
    const url = safeImageLoader({
      src: 'https://abc.r2.cloudflarestorage.com/covers/123/cover.webp',
      width: 600,
      quality: 80,
    });
    expect(url).toBe('https://abc.r2.cloudflarestorage.com/covers/123/cover.webp?w=600&q=80');
  });

  it('passes through Wikimedia src with default quality 75 when omitted', () => {
    const url = safeImageLoader({
      src: 'https://upload.wikimedia.org/wikipedia/commons/foo.jpg',
      width: 800,
    });
    expect(url).toBe('https://upload.wikimedia.org/wikipedia/commons/foo.jpg?w=800&q=75');
  });

  it('preserves existing query string with & separator', () => {
    const url = safeImageLoader({
      src: 'https://abc.r2.dev/img?v=1',
      width: 400,
      quality: 50,
    });
    expect(url).toBe('https://abc.r2.dev/img?v=1&w=400&q=50');
  });

  it('clamps quality to [1, 100]', () => {
    expect(safeImageLoader({ src: 'https://abc.r2.dev/x.jpg', width: 100, quality: -5 })).toContain(
      'q=1'
    );
    expect(
      safeImageLoader({ src: 'https://abc.r2.dev/x.jpg', width: 100, quality: 9999 })
    ).toContain('q=100');
  });

  it('fires the beacon when redirecting a BGG-host src to placeholder', () => {
    const beacon = vi.fn(() => true);
    // @ts-expect-error — overriding for the test
    navigator.sendBeacon = beacon;

    safeImageLoader({ src: 'https://cf.geekdo-images.com/y.jpg', width: 100 });

    expect(beacon).toHaveBeenCalledTimes(1);
    expect(beacon.mock.calls[0][0]).toBe('/api/metrics/bgg-attempt');
  });

  it('does NOT fire the beacon when redirecting empty src to placeholder', () => {
    // Empty/null src is a missing-data state, not a ToS issue; no beacon.
    const beacon = vi.fn(() => true);
    // @ts-expect-error — overriding for the test
    navigator.sendBeacon = beacon;

    safeImageLoader({ src: '', width: 100 });

    expect(beacon).not.toHaveBeenCalled();
  });
});
