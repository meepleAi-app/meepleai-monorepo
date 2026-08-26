/**
 * Costruisce il tracker dell'audit: una riga per ogni unità di copertura.
 *
 * Una rotta non-admin genera due righe (utente e admin percorrono la stessa
 * pagina con permessi diversi); una rotta /admin ne genera una sola.
 *
 * L'ordinamento è esplicito e totale: due esecuzioni sullo stesso codice devono
 * produrre un CSV byte-identico, altrimenti il diff del tracker fra un'ondata e
 * la successiva diventa illeggibile.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { createHash } from 'node:crypto';

import { contextForEndpoint, contextForRoute } from './context-map';
import type { EndpointEntry, InventoryRow, RouteEntry } from './types';

const MUTATIONS = new Set(['POST', 'PUT', 'PATCH', 'DELETE']);
const HEADER = 'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note';

const idFor = (parts: string[]): string =>
  createHash('sha1').update(parts.join('|')).digest('hex').slice(0, 8);

export function buildInventory(routes: RouteEntry[], endpoints: EndpointEntry[]): InventoryRow[] {
  const rows: InventoryRow[] = [];

  for (const r of routes) {
    const roles: Array<'user' | 'admin'> = r.route.startsWith('/admin')
      ? ['admin']
      : ['admin', 'user'];

    for (const ruolo of roles) {
      rows.push({
        id: idFor(['route', r.route, ruolo]),
        tipo: 'route',
        path: r.route,
        metodo: 'GET',
        contesto: contextForRoute(r.route),
        ruolo,
        livello: 'L1',
        stato: '⬜ non coperto',
        evidenza: '',
        note: r.dynamicSegments.length ? `param: ${r.dynamicSegments.join(' ')}` : '',
      });
    }
  }

  for (const e of endpoints) {
    rows.push({
      id: idFor(['endpoint', e.method, e.path]),
      tipo: 'endpoint',
      path: e.path,
      metodo: e.method,
      contesto: contextForEndpoint(e),
      ruolo: e.auth === 'admin' ? 'admin' : 'user',
      livello: MUTATIONS.has(e.method) ? 'L2' : 'L1',
      stato: '⬜ non coperto',
      evidenza: '',
      note: e.auth === 'unknown' ? 'auth non dedotta: leggere il codice' : '',
    });
  }

  return rows.sort(
    (a, b) =>
      a.contesto.localeCompare(b.contesto) ||
      a.tipo.localeCompare(b.tipo) ||
      a.path.localeCompare(b.path) ||
      a.metodo.localeCompare(b.metodo) ||
      a.ruolo.localeCompare(b.ruolo)
  );
}

const cell = (value: string): string =>
  /[",\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;

export function toCsv(rows: InventoryRow[]): string {
  const body = rows.map(r =>
    [r.id, r.tipo, r.path, r.metodo, r.contesto, r.ruolo, r.livello, r.stato, r.evidenza, r.note]
      .map(cell)
      .join(',')
  );
  return [HEADER, ...body].join('\n') + '\n';
}
