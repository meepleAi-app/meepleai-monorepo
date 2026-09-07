/**
 * Guard: every internal link written in a user-facing string must resolve to a
 * real route (#3946).
 *
 * Why this exists: the privacy policy and the terms of service told users to
 * visit `/settings/ai-consent` to manage their AI consent. That path never
 * existed — the panel lives under `/profile?tab=settings&section=ai-consent`.
 * Nothing caught it, because a string in a locale file is not type-checked and
 * not linted: it is just text until a user follows it.
 *
 * Scope note: this checks *markdown links* (`[label](/path)`), not every slash
 * in every string. Prose mentions paths for many reasons; a link is a promise
 * that something is there.
 */

import { readFileSync, readdirSync } from 'node:fs';
import { join, resolve } from 'node:path';

import { describe, it, expect } from 'vitest';

const WEB_ROOT = process.cwd();
const APP_DIR = resolve(WEB_ROOT, 'src/app');
const LOCALES = ['it.json', 'en.json'] as const;

/** `[label](/path)` — internal links only; http(s) and mailto are out of scope. */
const INTERNAL_LINK = /\[[^\]]+\]\((\/[^)]*)\)/g;

interface FoundLink {
  readonly locale: string;
  readonly href: string;
}

function collectLinks(): FoundLink[] {
  const found: FoundLink[] = [];
  for (const locale of LOCALES) {
    const raw = readFileSync(resolve(WEB_ROOT, 'src/locales', locale), 'utf8');
    for (const match of raw.matchAll(INTERNAL_LINK)) {
      found.push({ locale, href: match[1] });
    }
  }
  return found;
}

/**
 * Every routable path, with App Router group segments `(foo)` removed and
 * dynamic segments `[id]` normalised to a `*` placeholder.
 */
function collectRoutes(): string[] {
  const routes: string[] = [];

  const walk = (dir: string, urlParts: string[]): void => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      if (entry.isDirectory()) {
        // Route groups `(auth)` and private folders `_components` are not URL segments.
        const isGroup = entry.name.startsWith('(') && entry.name.endsWith(')');
        const isPrivate = entry.name.startsWith('_') || entry.name === '__tests__';
        if (isPrivate) continue;
        walk(join(dir, entry.name), isGroup ? urlParts : [...urlParts, entry.name]);
      } else if (entry.name === 'page.tsx') {
        routes.push('/' + urlParts.join('/'));
      }
    }
  };

  walk(APP_DIR, []);
  return routes;
}

/** A route matches if every segment is equal, or the route segment is dynamic. */
function resolves(pathname: string, routes: readonly string[]): boolean {
  const wanted = pathname.split('/').filter(Boolean);
  return routes.some(route => {
    const parts = route.split('/').filter(Boolean);
    if (parts.length !== wanted.length) return false;
    return parts.every((p, i) => (p.startsWith('[') && p.endsWith(']')) || p === wanted[i]);
  });
}

describe('locales — internal links', () => {
  const links = collectLinks();
  const routes = collectRoutes();

  // Both guards below exist because a check that inspects nothing is
  // indistinguishable from a check that passes — see #3917 and the
  // #3622/#3625/#3629/#3632/#3659/#3662 cluster.
  it('finds internal links to check', () => {
    expect(links.length).toBeGreaterThan(0);
  });

  it('finds routes to check them against', () => {
    expect(routes.length).toBeGreaterThan(100);
  });

  it('every internal link in a locale string resolves to a real route', () => {
    const broken = links
      .map(({ locale, href }) => ({ locale, href, pathname: href.split('?')[0].split('#')[0] }))
      .filter(({ pathname }) => !resolves(pathname, routes))
      .map(({ locale, href }) => `${locale}: ${href}`);

    expect(broken, `broken internal links in locale strings:\n${broken.join('\n')}`).toEqual([]);
  });
});
