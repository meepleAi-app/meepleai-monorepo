/**
 * Genera il tracker dell'audit da eseguire con `pnpm audit:inventory`.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { buildInventory, toCsv } from './build-inventory';
import { extractApiEndpoints } from './extract-api-endpoints';
import { extractFeRoutes } from './extract-fe-routes';

const OUT_DIR = path.resolve('../../docs/for-developers/audits/2026-08-26-full-feature-audit');

const routes = extractFeRoutes('src/app');
const endpoints = extractApiEndpoints('../api/src/Api');
const rows = buildInventory(routes, endpoints);

mkdirSync(OUT_DIR, { recursive: true });
writeFileSync(path.join(OUT_DIR, 'inventory.csv'), toCsv(rows), 'utf8');

const unmapped = rows.filter(r => r.contesto === 'Unmapped');
console.log(`rotte: ${routes.length} · endpoint: ${endpoints.length} · righe: ${rows.length}`);
console.log(
  `Unmapped: ${unmapped.length} (${((unmapped.length / rows.length) * 100).toFixed(1)}%)` +
    (unmapped.length ? ` → ${[...new Set(unmapped.map(r => r.path))].slice(0, 10).join(' ')}` : '')
);
