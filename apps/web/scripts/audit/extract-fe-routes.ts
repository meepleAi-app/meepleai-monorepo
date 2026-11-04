/**
 * Estrae le rotte URL dall'app router di Next.js.
 *
 * I route group `(authenticated)`, `(public)`, ... esistono solo nel filesystem:
 * non compaiono nella URL, ma sono l'informazione che dice quale pubblico deve
 * poter raggiungere la pagina, quindi vengono conservati come etichetta.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { readdirSync, statSync } from 'node:fs';
import path from 'node:path';

import type { RouteEntry } from './types';

const ROUTE_GROUP = /^\(.+\)$/;

/** Converte un path relativo a src/app in una rotta URL. Ritorna null se non è una page. */
export function toRoute(relPath: string): RouteEntry | null {
  const normalized = relPath.split(path.sep).join('/');
  if (!normalized.endsWith('page.tsx')) return null;

  const segments = normalized.split('/').slice(0, -1); // via 'page.tsx'
  // Il primo segmento, sia esso un route group o una cartella reale: per le 94
  // pagine sotto admin/(dashboard)/… l'etichetta utile è 'admin'.
  const group = segments[0] ?? '(root)';
  const urlSegments = segments.filter(s => !ROUTE_GROUP.test(s));
  const dynamicSegments = urlSegments
    .filter(s => s.startsWith('[') && s.endsWith(']'))
    .map(s => s.slice(1, -1));

  return {
    route: urlSegments.length ? `/${urlSegments.join('/')}` : '/',
    group,
    dynamicSegments,
    file: normalized,
  };
}

/** Percorre src/app e raccoglie tutte le page.tsx, ordinate per rotta. */
export function extractFeRoutes(appDir: string): RouteEntry[] {
  const found: RouteEntry[] = [];

  const walk = (dir: string): void => {
    for (const name of readdirSync(dir).sort()) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) {
        walk(full);
        continue;
      }
      const entry = toRoute(path.relative(appDir, full));
      if (entry) found.push(entry);
    }
  };

  walk(appDir);
  return found.sort((a, b) => a.route.localeCompare(b.route));
}
