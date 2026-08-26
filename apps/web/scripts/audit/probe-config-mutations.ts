/**
 * Ondata 1B — mutazioni di configurazione e operazioni.
 *
 * Ogni ciclo crea una risorsa propria, la modifica e la elimina. Non tocca
 * configurazioni, regole di allerta o feature flag esistenti: cambiarli
 * altererebbe il comportamento del sistema per chiunque altro lo stia usando.
 *
 * Restano deliberatamente fuori, e non sono da considerarsi verificate:
 *   - `POST /admin/infrastructure/services/{name}/restart`, `operations/restart-service`,
 *     `secrets/restart` — riavviano servizi reali
 *   - `POST /admin/storage/migrate` — migrazione dello storage
 *   - `POST /admin/providers/{name}/rotate-key` — romperebbe le integrazioni
 *   - `DELETE /admin/rag-backup/snapshots/{id}` — cancella backup
 *   - `DELETE /admin/sessions/{sessionId}` — potrebbe revocare la sessione dell'audit stesso
 *   - `POST /admin/alert-channels/{type}/test-connection` — invia messaggi verso l'esterno
 *   - `POST /admin/configurations/{import,bulk-update}` — agiscono in massa
 *
 * Uso: MSYS_NO_PATHCONV=1 pnpm exec tsx scripts/audit/probe-config-mutations.ts
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const RESULTS = path.resolve('audit-results');
const OUT = path.join(RESULTS, 'config-mutations.jsonl');
const API = process.env.AUDIT_API_BASE ?? 'http://localhost:8080';

const cookies: Record<string, string> = JSON.parse(
  readFileSync(path.join(RESULTS, 'cookies.json'), 'utf8')
);

let attesi = 0;
let difformi = 0;

async function call(
  metodo: string,
  tracker: string,
  url: string,
  body?: unknown,
  livello: 'L2' | 'L3' = 'L2'
): Promise<{ status: number; json: any }> {
  const res = await fetch(`${API}${url}`, {
    method: metodo,
    headers: {
      Cookie: cookies.admin ?? '',
      ...(body === undefined ? {} : { 'Content-Type': 'application/json' }),
    },
    body: body === undefined ? undefined : JSON.stringify(body),
  });

  const testo = await res.text();
  let json: any = null;
  try {
    json = JSON.parse(testo);
  } catch {
    /* risposta non JSON */
  }

  const ok = res.status >= 200 && res.status < 300;
  ok ? (attesi += 1) : (difformi += 1);

  appendFileSync(
    OUT,
    JSON.stringify({
      metodo,
      path: tracker,
      livello,
      esito: ok ? 'atteso' : 'difforme',
      evidenza: `HTTP ${res.status}${ok ? '' : ` — ${testo.slice(0, 90).replace(/\s+/g, ' ')}`}`,
    }) + '\n',
    'utf8'
  );

  console.log(
    `${ok ? 'OK  ' : 'DIFF'} ${metodo.padEnd(6)} ${tracker.padEnd(50)} HTTP ${res.status}${
      ok ? '' : ` — ${testo.slice(0, 70).replace(/\s+/g, ' ')}`
    }`
  );
  return { status: res.status, json };
}

const idDi = (j: any): string | undefined => j?.id ?? j?.data?.id ?? j?.key ?? j?.data?.key;

async function main(): Promise<void> {
  writeFileSync(OUT, '', 'utf8');
  const marca = process.env.AUDIT_RUN_ID ?? 'x';

  // ---- regole di allerta ----
  const regola = await call('POST', '/api/v1/admin/alert-rules/', '/api/v1/admin/alert-rules/', {
    name: `audit-regola-${marca}`,
    description: 'Regola creata dall audit ondata 1B',
    metric: 'cpu_usage',
    threshold: 90,
    comparison: 'GreaterThan',
    severity: 'Warning',
    enabled: false,
  });
  const regolaId = idDi(regola.json);
  if (regolaId) {
    await call(
      'PUT',
      '/api/v1/admin/alert-rules/{id:guid}',
      `/api/v1/admin/alert-rules/${regolaId}`,
      { name: `audit-regola-${marca}-modificata`, threshold: 95 }
    );
    await call(
      'PATCH',
      '/api/v1/admin/alert-rules/{id:guid}/toggle',
      `/api/v1/admin/alert-rules/${regolaId}/toggle`,
      {}
    );
    await call(
      'DELETE',
      '/api/v1/admin/alert-rules/{id:guid}',
      `/api/v1/admin/alert-rules/${regolaId}`,
      undefined,
      'L3'
    );
  }

  // ---- configurazioni ----
  const config = await call(
    'POST',
    '/api/v1/admin/configurations',
    '/api/v1/admin/configurations',
    {
      key: `audit.prova.${marca}`,
      value: 'valore-audit',
      description: 'Configurazione creata dall audit ondata 1B',
      category: 'General',
      environment: 'All',
    }
  );
  const configId = idDi(config.json);

  await call(
    'POST',
    '/api/v1/admin/configurations/validate',
    '/api/v1/admin/configurations/validate',
    { key: `audit.prova.${marca}`, value: 'valore-audit' }
  );

  if (configId) {
    await call(
      'PUT',
      '/api/v1/admin/configurations/{id:guid}',
      `/api/v1/admin/configurations/${configId}`,
      { value: 'valore-audit-modificato' }
    );
    await call(
      'PATCH',
      '/api/v1/admin/configurations/{id:guid}/toggle',
      `/api/v1/admin/configurations/${configId}/toggle`,
      {}
    );
    await call(
      'DELETE',
      '/api/v1/admin/configurations/{id:guid}',
      `/api/v1/admin/configurations/${configId}`,
      undefined,
      'L3'
    );
  }

  // ---- feature flag: creato apposta, mai toggle su quelli esistenti ----
  const flagKey = `audit-flag-${marca}`;
  const flag = await call('POST', '/api/v1/admin/feature-flags', '/api/v1/admin/feature-flags', {
    key: flagKey,
    name: 'Flag audit 1B',
    description: 'Flag creato dall audit ondata 1B',
    enabled: false,
  });
  if (flag.status >= 200 && flag.status < 300) {
    await call(
      'PUT',
      '/api/v1/admin/feature-flags/{key}',
      `/api/v1/admin/feature-flags/${flagKey}`,
      {
        description: 'Flag audit 1B (modificato)',
      }
    );
    await call(
      'POST',
      '/api/v1/admin/feature-flags/{key}/toggle',
      `/api/v1/admin/feature-flags/${flagKey}/toggle`,
      {}
    );
    await call(
      'POST',
      '/api/v1/admin/feature-flags/{key}/tier/{tier}/enable',
      `/api/v1/admin/feature-flags/${flagKey}/tier/premium/enable`,
      {}
    );
    await call(
      'POST',
      '/api/v1/admin/feature-flags/{key}/tier/{tier}/disable',
      `/api/v1/admin/feature-flags/${flagKey}/tier/premium/disable`,
      {}
    );
  }

  // ---- scenari del playground ----
  const scenario = await call(
    'POST',
    '/api/v1/admin/playground/scenarios/',
    '/api/v1/admin/playground/scenarios/',
    { name: `audit-scenario-${marca}`, description: 'Scenario audit 1B', prompt: 'prova' }
  );
  const scenarioId = idDi(scenario.json);
  if (scenarioId) {
    await call(
      'PUT',
      '/api/v1/admin/playground/scenarios/{id:guid}',
      `/api/v1/admin/playground/scenarios/${scenarioId}`,
      { name: `audit-scenario-${marca}-mod` }
    );
    await call(
      'DELETE',
      '/api/v1/admin/playground/scenarios/{id:guid}',
      `/api/v1/admin/playground/scenarios/${scenarioId}`
    );
  }

  console.log(`\nattesi: ${attesi} · difformi: ${difformi}`);
}

if (require.main === module) {
  void main();
}
