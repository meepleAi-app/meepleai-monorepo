/**
 * Tests for WS-C Phase 1 ownership loader (refs #1069, umbrella #1066).
 * Spec: docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md §3 WS-C
 */
import { describe, it, expect, beforeEach, afterEach } from 'vitest';
import { mkdtempSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { loadOwnership, resolveLivePath, type OwnershipFile } from '../conformity-ownership';

const validMinimal: OwnershipFile = {
  version: 1,
  routes: [
    {
      id: 'library',
      livePath: '/library',
      mockup: 'sp4-library-desktop.html',
      triggerPaths: ['apps/web/src/app/(authenticated)/library/**'],
    },
  ],
};

const validWithFixture: OwnershipFile = {
  version: 1,
  defaults: {
    threshold: 0.2,
    conformityRatio: 0.1,
    viewports: [{ name: 'desktop', width: 1920, height: 1080 }],
  },
  routes: [
    {
      id: 'detail',
      livePath: '/library/{gameId}',
      liveFixture: { gameId: 'nanolith-runthrough' },
      mockup: 'nanolith.html',
      triggerPaths: ['apps/web/src/app/(authenticated)/library/[gameId]/**'],
      threshold: 0.05,
    },
  ],
};

describe('loadOwnership', () => {
  let tmpDir: string;

  beforeEach(() => {
    tmpDir = mkdtempSync(join(tmpdir(), 'conformity-ownership-'));
  });

  afterEach(() => {
    rmSync(tmpDir, { recursive: true, force: true });
  });

  function writeJson(name: string, data: unknown): string {
    const p = join(tmpDir, name);
    writeFileSync(p, JSON.stringify(data));
    return p;
  }

  it('loads valid minimal config and applies framework defaults', () => {
    const path = writeJson('minimal.json', validMinimal);
    const config = loadOwnership(path);

    expect(config.version).toBe(1);
    expect(config.routes).toHaveLength(1);
    const route = config.routes[0];
    expect(route.id).toBe('library');
    expect(route.threshold).toBe(0.1); // framework default
    expect(route.conformityRatio).toBe(0.05); // framework default
    expect(route.viewports).toEqual([
      { name: 'desktop', width: 1280, height: 720 },
      { name: 'mobile', width: 375, height: 740 },
    ]);
  });

  it('user defaults override framework defaults', () => {
    const path = writeJson('overrides.json', validWithFixture);
    const config = loadOwnership(path);
    const route = config.routes[0];

    expect(route.threshold).toBe(0.05); // route override wins
    expect(route.conformityRatio).toBe(0.1); // defaults.conformityRatio
    expect(route.viewports).toEqual([{ name: 'desktop', width: 1920, height: 1080 }]);
  });

  it('rejects unknown version', () => {
    const path = writeJson('bad-version.json', { ...validMinimal, version: 99 });
    expect(() => loadOwnership(path)).toThrow(/version/i);
  });

  it('rejects missing routes', () => {
    const path = writeJson('no-routes.json', { version: 1, routes: [] });
    expect(() => loadOwnership(path)).toThrow(/route/i);
  });

  it('rejects duplicate route ids', () => {
    const path = writeJson('dup.json', {
      version: 1,
      routes: [validMinimal.routes[0], { ...validMinimal.routes[0] }],
    });
    expect(() => loadOwnership(path)).toThrow(/duplicate.*library/i);
  });

  it('rejects threshold out of range', () => {
    const path = writeJson('bad-threshold.json', {
      version: 1,
      routes: [{ ...validMinimal.routes[0], threshold: 1.5 }],
    });
    expect(() => loadOwnership(path)).toThrow(/threshold/i);
  });

  it('rejects conformityRatio out of range', () => {
    const path = writeJson('bad-ratio.json', {
      version: 1,
      routes: [{ ...validMinimal.routes[0], conformityRatio: -0.1 }],
    });
    expect(() => loadOwnership(path)).toThrow(/conformityRatio/i);
  });

  it('rejects livePath without leading slash', () => {
    const path = writeJson('bad-path.json', {
      version: 1,
      routes: [{ ...validMinimal.routes[0], livePath: 'library' }],
    });
    expect(() => loadOwnership(path)).toThrow(/livePath/i);
  });

  it('rejects livePath with {param} but no liveFixture', () => {
    const path = writeJson('missing-fixture.json', {
      version: 1,
      routes: [
        {
          id: 'detail',
          livePath: '/library/{gameId}',
          mockup: 'x.html',
          triggerPaths: ['apps/web/**'],
        },
      ],
    });
    expect(() => loadOwnership(path)).toThrow(/liveFixture.*gameId/i);
  });

  it('rejects liveFixture missing a {param} placeholder', () => {
    const path = writeJson('partial-fixture.json', {
      version: 1,
      routes: [
        {
          id: 'detail',
          livePath: '/library/{gameId}/{tab}',
          liveFixture: { gameId: 'x' },
          mockup: 'x.html',
          triggerPaths: ['apps/web/**'],
        },
      ],
    });
    expect(() => loadOwnership(path)).toThrow(/liveFixture.*tab/i);
  });

  it('rejects empty triggerPaths', () => {
    const path = writeJson('no-triggers.json', {
      version: 1,
      routes: [{ ...validMinimal.routes[0], triggerPaths: [] }],
    });
    expect(() => loadOwnership(path)).toThrow(/triggerPaths/i);
  });

  it('rejects malformed JSON', () => {
    const p = join(tmpDir, 'malformed.json');
    writeFileSync(p, '{ not json');
    expect(() => loadOwnership(p)).toThrow();
  });

  it('rejects unknown top-level keys (strict mode)', () => {
    const path = writeJson('extra.json', { ...validMinimal, unknownField: 'oops' });
    expect(() => loadOwnership(path)).toThrow(/unknownField|unknown/i);
  });
});

describe('resolveLivePath', () => {
  it('returns livePath unchanged when no placeholders', () => {
    expect(resolveLivePath('/library', {})).toBe('/library');
  });

  it('substitutes single placeholder', () => {
    expect(resolveLivePath('/library/{gameId}', { gameId: 'nano' })).toBe('/library/nano');
  });

  it('substitutes multiple placeholders', () => {
    expect(
      resolveLivePath('/games/{gameId}/sessions/{sessionId}', {
        gameId: 'g1',
        sessionId: 's2',
      })
    ).toBe('/games/g1/sessions/s2');
  });

  it('throws on unresolved placeholder', () => {
    expect(() => resolveLivePath('/library/{gameId}', {})).toThrow(/gameId/);
  });

  it('URL-encodes fixture values', () => {
    expect(resolveLivePath('/q/{term}', { term: 'a b/c' })).toBe('/q/a%20b%2Fc');
  });
});
