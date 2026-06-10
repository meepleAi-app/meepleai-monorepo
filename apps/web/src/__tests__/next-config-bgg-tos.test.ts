/**
 * Issue #2123 — BGG ToS compliance contract test on next.config.js.
 *
 * Asserts the Next.js `images.remotePatterns` allowlist:
 *   1. Contains NONE of the BGG hostnames (cf.geekdo-images.com,
 *      *.boardgamegeek.com, images.geekdo.com, geekdo-images.com).
 *   2. Does NOT contain the catch-all `**` wildcard — that nullifies the
 *      hostname-specific guards and was the root defect this issue fixes.
 *   3. Contains the canonical replacements (R2 + Wikimedia + placeholder).
 *
 * Failing this test means a regression has weakened the network-plane
 * fail-closed posture. See:
 *   docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md
 *   docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
 */
import { describe, expect, it } from 'vitest';
import nextConfig from '../../next.config.js';

interface RemotePattern {
  protocol?: string;
  hostname?: string;
  pathname?: string;
}

const patterns: RemotePattern[] =
  (nextConfig as { images?: { remotePatterns?: RemotePattern[] } })?.images?.remotePatterns ?? [];

const BGG_HOSTS = [
  'cf.geekdo-images.com',
  'geekdo-images.com',
  'images.geekdo.com',
  '**.boardgamegeek.com',
  'boardgamegeek.com',
];

describe('next.config.js BGG ToS compliance (#2123)', () => {
  it('does not whitelist any BGG host', () => {
    for (const host of BGG_HOSTS) {
      expect(patterns).not.toContainEqual(expect.objectContaining({ hostname: host }));
    }
    // Belt-and-braces: also detect a partial match in case someone adds a
    // creative subdomain pattern (e.g. `bgg.geekdo-something.net`).
    for (const p of patterns) {
      if (typeof p.hostname === 'string') {
        expect(p.hostname.toLowerCase()).not.toMatch(/geekdo|boardgamegeek/);
      }
    }
  });

  it('does not contain the catch-all `**` wildcard', () => {
    expect(patterns).not.toContainEqual(expect.objectContaining({ hostname: '**' }));
  });

  it('whitelists the R2 cover storage hosts', () => {
    const hostnames = patterns.map(p => p.hostname);
    expect(hostnames).toContain('**.r2.cloudflarestorage.com');
    expect(hostnames).toContain('*.r2.dev');
  });

  it('whitelists the Wikimedia Commons asset hosts', () => {
    const hostnames = patterns.map(p => p.hostname);
    expect(hostnames).toContain('commons.wikimedia.org');
    expect(hostnames).toContain('upload.wikimedia.org');
  });

  it('whitelists the placeholder + storybook hosts', () => {
    const hostnames = patterns.map(p => p.hostname);
    expect(hostnames).toContain('picsum.photos');
    expect(hostnames).toContain('placehold.co');
  });
});
