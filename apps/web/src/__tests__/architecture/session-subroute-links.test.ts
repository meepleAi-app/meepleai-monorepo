/**
 * Gate di architettura — #4156: un link a `/sessions/{id}/<segmento>` porta a una rotta che esiste.
 *
 * **Il difetto.** La mini-nav delle pagine di sessione offriva «Strumenti» e «Chat» verso
 * `/sessions/{id}/tools` e `/sessions/{id}/chat`, la navigazione per entità portava a
 * `/sessions/{id}/chats`, il cassetto della card a `/sessions/{id}/results`. Nessuna di queste
 * cartelle esiste sotto `app/(authenticated)/sessions/[id]/`: ogni clic era un 404.
 *
 * **Cosa controlla.** Ogni template `/sessions/${…}/<segmento>` scritto nel sorgente: il primo
 * segmento dopo l'id deve essere una sottocartella di `sessions/[id]`. È un controllo sul testo,
 * non sulla navigazione: un URL costruito in altro modo (concatenazione, segmento in variabile)
 * non si vede.
 */

import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative } from 'node:path';

import { sync as globSync } from 'glob';
import { describe, expect, it } from 'vitest';

const SRC = join(process.cwd(), 'src');
const SESSION_ROUTE_DIR = join(SRC, 'app', '(authenticated)', 'sessions', '[id]');

/** Le sottorotte di `/sessions/[id]`: le cartelle che non sono gruppi o cartelle private. */
function existingSubroutes(): Set<string> {
  return new Set(
    readdirSync(SESSION_ROUTE_DIR).filter(
      name =>
        statSync(join(SESSION_ROUTE_DIR, name)).isDirectory() &&
        !name.startsWith('_') &&
        !name.startsWith('(') &&
        name !== '__tests__'
    )
  );
}

interface LinkSite {
  file: string;
  segment: string;
}

function sessionSubrouteLinks(): LinkSite[] {
  // `lib/api` costruisce rotte API da una base (`${GAME_NIGHT_BASE}/sessions/${id}/…`), non link.
  const files = globSync('**/*.{ts,tsx}', { cwd: SRC, absolute: true }).filter(
    file =>
      !/[\\/]__tests__[\\/]|\.test\.|\.stories\./.test(file) &&
      !relative(SRC, file).replace(/\\/g, '/').startsWith('lib/api/')
  );
  const sites: LinkSite[] = [];
  for (const file of files) {
    const source = readFileSync(file, 'utf8');
    // `/sessions/${qualcosa}/segmento` — non preceduto da `-` (live-sessions, game-sessions…)
    // né da `/v1` (le rotte API).
    for (const m of source.matchAll(/(?<![-\w])\/sessions\/\$\{[^}]+\}\/([A-Za-z][\w-]*)/g)) {
      const before = source.slice(Math.max(0, m.index! - 8), m.index!);
      if (/\/v1$|api$/.test(before)) continue;
      sites.push({ file: relative(SRC, file).replace(/\\/g, '/'), segment: m[1] });
    }
  }
  return sites;
}

describe('gate: i link a /sessions/{id}/<segmento> portano a una rotta che esiste (#4156)', () => {
  const routes = existingSubroutes();
  const sites = sessionSubrouteLinks();

  it('conosce le sottorotte e trova dei link da controllare', () => {
    // Guardia di non vacuità.
    expect(routes).toContain('live');
    expect(routes).toContain('scoreboard');
    expect(sites.some(site => site.segment === 'live')).toBe(true);
  });

  it('non ha link verso sottorotte inesistenti', () => {
    const dead = sites
      .filter(site => !routes.has(site.segment))
      .map(site => `${site.file} → /sessions/{id}/${site.segment}`);
    expect(
      dead,
      `sottorotte esistenti sotto sessions/[id]: ${[...routes].sort().join(', ')}`
    ).toEqual([]);
  });
});
