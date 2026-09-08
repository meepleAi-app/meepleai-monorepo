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
 */

import { readdirSync, readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';

import { describe, it, expect } from 'vitest';

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
