/**
 * Guard: every static href declared in `src/config/` must resolve to a real route (#3938).
 *
 * Why: `contextual-tabs.ts` listed `/settings?tab=profile`, `/settings?tab=preferences`
 * and `/settings?tab=account` — a route that did not exist, a query parameter the hub
 * does not read, and section ids that are not in `SETTINGS_SECTIONS`. Nothing caught it,
 * because a string in a config object is neither type-checked nor linted against the
 * route tree.
 *
 * Scope: `src/config/` only. Hrefs live in many other places; this covers the ones that
 * are declared as data, where a typo cannot be caught by anything else.
 *
 * Second guard, added after the same defect recurred (#3938/#3946/#3961): an href can
 * resolve against the route tree and still be unreachable **in the network**, because Next
 * resolves `next.config.js` redirects BEFORE filesystem routing. That is how the settings
 * hub shipped invisible — nine 308s to `/profile?tab=settings` outlived the routes they
 * were meant to replace, and the return forward in `ProfilePageContent` closed the loop.
 * The first occurrence was `library/wishlist` (#2818); this file exists so there is not a
 * third one.
 *
 * Deliberately evaluates `next.config.js` directly rather than `.next/routes-manifest.json`:
 * a guard that needs a build does not run in unit tests, and so guards nothing.
 */

import { readdirSync, readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { join, resolve } from 'node:path';

import { describe, it, expect } from 'vitest';

import { SETTINGS_SECTIONS } from '@/components/features/settings/settings-sections';

const WEB_ROOT = process.cwd();
const APP_DIR = resolve(WEB_ROOT, 'src/app');
const CONFIG_DIR = resolve(WEB_ROOT, 'src/config');

/** `href: '/path'` — internal, absolute, statically written. */
const STATIC_HREF = /href:\s*'(\/[^']*)'/g;

/**
 * Known debt, tracked and deliberately not fixed here.
 *
 * `admin-navigation.ts` declares **38** hrefs to routes that do not exist — the
 * admin console was reorganised and its navigation config was not. That is a real
 * defect, but the admin surface is out of scope for epic #3916, and fixing 38
 * destinations in this PR would bury a settings change under an unrelated
 * refactor.
 *
 * This is an exclusion, not a silence: the count is asserted below, so the debt
 * cannot grow unnoticed, and shrinking it forces an update here.
 */
const KNOWN_BROKEN = {
  file: 'admin-navigation.ts',
  count: 38,
} as const;

/**
 * Routes whose own redirect covers them entirely — the `/settings` defect, elsewhere.
 *
 * Each of these is a redirect whose `source` equals an existing route, so the route can
 * never be served. They are real defects of the same class, but fixing them here would
 * bury a settings change under an unrelated refactor, so they are declared instead of
 * silenced: the count is asserted, the debt cannot grow, and shrinking it forces an
 * update here.
 */
const KNOWN_SHADOWED = [
  '/profile/achievements',
  '/play-records/stats',
  '/admin/shared-games',
] as const;

interface Redirect {
  readonly source: string;
  readonly destination: string;
}

function loadRedirects(): Promise<Redirect[]> {
  const require = createRequire(import.meta.url);
  const config = require(resolve(WEB_ROOT, 'next.config.js')) as {
    redirects?: () => Promise<Redirect[]>;
  };
  return config.redirects?.() ?? Promise.resolve([]);
}

const segments = (path: string): string[] => path.split('/').filter(Boolean);
const isParam = (s: string): boolean => s.startsWith(':');
const isCatchAll = (s: string): boolean => isParam(s) && s.endsWith('*');
const isDynamic = (s: string): boolean => s.startsWith('[') && s.endsWith(']');

/**
 * True when every URL the route would serve is captured by the redirect — i.e. the route
 * is unreachable.
 *
 * Deliberately narrower than "the two patterns intersect": a literal source against a
 * dynamic route segment (`/sessions/history` vs `/sessions/[id]`) steals one value and
 * leaves the rest, which is a legitimate and widely used pattern here.
 */
function covers(source: string, route: string): boolean {
  const src = segments(source);
  const dest = segments(route);
  const catchAll = src.length > 0 && isCatchAll(src[src.length - 1]);

  if (catchAll) {
    if (dest.length < src.length - 1) return false;
  } else if (src.length !== dest.length) {
    return false;
  }

  const fixed = catchAll ? src.length - 1 : src.length;
  for (let i = 0; i < fixed; i++) {
    if (isParam(src[i])) continue;
    if (isDynamic(dest[i])) return false;
    if (src[i] !== dest[i]) return false;
  }
  return true;
}

interface FoundHref {
  readonly file: string;
  readonly href: string;
}

function collectConfigHrefs(): FoundHref[] {
  const found: FoundHref[] = [];
  for (const entry of readdirSync(CONFIG_DIR, { withFileTypes: true })) {
    if (!entry.isFile() || !entry.name.endsWith('.ts')) continue;
    if (entry.name.endsWith('.test.ts')) continue;
    const raw = readFileSync(join(CONFIG_DIR, entry.name), 'utf8');
    for (const match of raw.matchAll(STATIC_HREF)) {
      found.push({ file: entry.name, href: match[1] });
    }
  }
  return found;
}

/** Routable paths, with `(group)` segments dropped and `[dynamic]` kept as wildcards. */
function collectRoutes(): string[] {
  const routes: string[] = [];

  const walk = (dir: string, urlParts: string[]): void => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        if (entry.name.startsWith('_') || entry.name === '__tests__') continue;
        const isGroup = entry.name.startsWith('(') && entry.name.endsWith(')');
        walk(join(dir, entry.name), isGroup ? urlParts : [...urlParts, entry.name]);
      } else if (entry.name === 'page.tsx') {
        routes.push('/' + urlParts.join('/'));
      }
    }
  };

  walk(APP_DIR, []);
  return routes;
}

function resolves(pathname: string, routes: readonly string[]): boolean {
  const wanted = pathname.split('/').filter(Boolean);
  return routes.some(route => {
    const parts = route.split('/').filter(Boolean);
    if (parts.length !== wanted.length) return false;
    return parts.every((p, i) => (p.startsWith('[') && p.endsWith(']')) || p === wanted[i]);
  });
}

describe('config — static hrefs', () => {
  const hrefs = collectConfigHrefs();
  const routes = collectRoutes();

  // Both guards exist because a check that inspects nothing is indistinguishable
  // from a check that passes — see #3917 and the #3622/#3625/#3629/#3632/#3659/#3662
  // cluster.
  it('finds hrefs to check', () => {
    expect(hrefs.length).toBeGreaterThan(0);
  });

  it('finds routes to check them against', () => {
    expect(routes.length).toBeGreaterThan(100);
  });

  const broken = hrefs
    .map(({ file, href }) => ({ file, href, pathname: href.split('?')[0].split('#')[0] }))
    .filter(({ pathname }) => !resolves(pathname, routes));

  it('every static href in src/config resolves to a real route', () => {
    const offenders = broken
      .filter(({ file }) => file !== KNOWN_BROKEN.file)
      .map(({ file, href }) => `${file}: ${href}`);

    expect(
      offenders,
      `hrefs pointing at routes that do not exist:\n${offenders.join('\n')}`
    ).toEqual([]);
  });

  it('the known admin-navigation debt does not grow', () => {
    const actual = broken.filter(({ file }) => file === KNOWN_BROKEN.file).length;

    // Fixing some is welcome — but then lower KNOWN_BROKEN.count in the same PR,
    // so the number keeps meaning something.
    expect(
      actual,
      `${KNOWN_BROKEN.file} has ${actual} broken hrefs, expected ${KNOWN_BROKEN.count}`
    ).toBe(KNOWN_BROKEN.count);
  });
});

describe('next.config redirects — do not shadow real routes', () => {
  const routes = collectRoutes();

  it('finds redirects to check', async () => {
    expect((await loadRedirects()).length).toBeGreaterThan(50);
  });

  it('no redirect makes an existing route unreachable', async () => {
    const redirects = await loadRedirects();

    const shadowed = redirects
      .flatMap(({ source }) =>
        routes.filter(route => covers(source, route)).map(route => ({ source, route }))
      )
      .filter(({ route }) => !KNOWN_SHADOWED.includes(route as (typeof KNOWN_SHADOWED)[number]));

    expect(
      shadowed.map(({ source, route }) => `${source} shadows ${route}`),
      'redirects in next.config.js that make a route unreachable — Next resolves them before filesystem routing'
    ).toEqual([]);
  });

  it('the known shadowing debt does not grow', async () => {
    const redirects = await loadRedirects();

    const actual = routes.filter(
      route =>
        KNOWN_SHADOWED.includes(route as (typeof KNOWN_SHADOWED)[number]) &&
        redirects.some(({ source }) => covers(source, route))
    );

    // Fixing some is welcome — then shorten KNOWN_SHADOWED in the same PR.
    expect(actual.sort()).toEqual([...KNOWN_SHADOWED].sort());
  });

  it('every settings section is reachable at its own address', async () => {
    const redirects = await loadRedirects();

    // The hub defect in concrete form: a redirect whose source is one of the addresses
    // the hub actually serves. Formally each only steals one value from `[section]`, so
    // `covers` cannot see it — but that one value IS a real section.
    const intercepted = SETTINGS_SECTIONS.map(({ id }) => `/settings/${id}`)
      .concat('/settings')
      .filter(address =>
        redirects.some(({ source }) => source === address || covers(source, address))
      );

    expect(
      intercepted,
      'settings addresses intercepted by a redirect — the hub would be unreachable'
    ).toEqual([]);
  });
});
