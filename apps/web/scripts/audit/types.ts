/**
 * Tipi condivisi dell'harness di audit (ondata 0).
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

/** Una pagina dell'app router, con la URL che espone. */
export type RouteEntry = {
  route: string;
  group: string;
  dynamicSegments: string[];
  file: string;
};

/** Un endpoint registrato nel backend, con il suo stato di autorizzazione. */
export type EndpointEntry = {
  method: string;
  path: string;
  auth: 'anonymous' | 'authenticated' | 'admin' | 'unknown';
  tags: string[];
  file: string;
  line: number;
};

/** Una riga del tracker: l'unità di copertura dell'audit. */
export type InventoryRow = {
  id: string;
  tipo: 'route' | 'endpoint';
  path: string;
  metodo: string;
  contesto: string;
  ruolo: 'user' | 'admin';
  livello: 'L1' | 'L2' | 'L3';
  stato: string;
  evidenza: string;
  note: string;
};
