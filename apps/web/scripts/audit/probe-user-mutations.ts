/**
 * Ondata 1A — mutazioni del blocco utenti di Administration.
 *
 * Opera su un utente creato apposta e lo elimina alla fine, così l'ambiente
 * torna come l'ha trovato. Non tocca gli utenti esistenti: sospendere o
 * declassare un account reale "per vedere cosa succede" non si annulla, e su
 * un ambiente condiviso si trasformerebbe in un guasto per qualcun altro.
 *
 * Le operazioni di massa (`/admin/users/bulk/*`) restano fuori: per definizione
 * agiscono su insiemi che non controllo.
 *
 * Uso: MSYS_NO_PATHCONV=1 pnpm exec tsx scripts/audit/probe-user-mutations.ts
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const RESULTS = path.resolve('audit-results');
const OUT = path.join(RESULTS, 'user-mutations.jsonl');
const API = process.env.AUDIT_API_BASE ?? 'http://localhost:8080';

type Esito = {
  metodo: string;
  path: string;
  livello: 'L2' | 'L3';
  esito: 'atteso' | 'difforme';
  evidenza: string;
};

const cookies: Record<string, string> = JSON.parse(
  readFileSync(path.join(RESULTS, 'cookies.json'), 'utf8')
);

let attesi = 0;
let difformi = 0;

function record(e: Esito): void {
  appendFileSync(OUT, JSON.stringify(e) + '\n', 'utf8');
  e.esito === 'atteso' ? (attesi += 1) : (difformi += 1);
  const tag = e.esito === 'atteso' ? 'OK  ' : 'DIFF';
  console.log(`${tag} ${e.metodo.padEnd(6)} ${e.path.padEnd(52)} ${e.evidenza}`);
}

/** Esegue una chiamata e giudica: 2xx = atteso, tutto il resto va guardato. */
async function call(
  metodo: string,
  tracker: string,
  url: string,
  body?: unknown,
  opts: { livello?: 'L2' | 'L3'; accetta?: number[] } = {}
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
    /* risposta non JSON: resta null, il testo va comunque nell'evidenza */
  }

  const accettabili = opts.accetta ?? [];
  const ok = (res.status >= 200 && res.status < 300) || accettabili.includes(res.status);

  record({
    metodo,
    path: tracker,
    livello: opts.livello ?? 'L2',
    esito: ok ? 'atteso' : 'difforme',
    evidenza: `HTTP ${res.status}${ok ? '' : ` — ${testo.slice(0, 90).replace(/\s+/g, ' ')}`}`,
  });

  return { status: res.status, json };
}

async function main(): Promise<void> {
  writeFileSync(OUT, '', 'utf8');
  const marca = process.env.AUDIT_RUN_ID ?? 'x';
  const email = `audit-mutazioni-${marca}@meepleai.test`;

  // ---- creazione ----
  const creato = await call('POST', '/api/v1/admin/users', '/api/v1/admin/users', {
    email,
    password: 'AuditProva!2026',
    displayName: 'Utente Prova Audit',
    role: 'user',
  });

  const id: string | undefined = creato.json?.id ?? creato.json?.user?.id;
  if (!id) {
    console.log('\nutente di prova non creato: le mutazioni successive non sono eseguibili');
    console.log(`attesi: ${attesi} · difformi: ${difformi}`);
    return;
  }
  console.log(`\nutente di prova: ${id}\n`);

  // ---- modifiche sull'utente di prova ----
  await call('PUT', '/api/v1/admin/users/{id}', `/api/v1/admin/users/${id}`, {
    displayName: 'Utente Prova Audit (modificato)',
  });
  await call(
    'PUT',
    '/api/v1/admin/users/{userId:guid}/role',
    `/api/v1/admin/users/${id}/role`,
    {
      role: 'editor',
    },
    { livello: 'L3' }
  );
  await call('PUT', '/api/v1/admin/users/{id}/tier', `/api/v1/admin/users/${id}/tier`, {
    tier: 'premium',
  });
  await call(
    'PATCH',
    '/api/v1/admin/users/{userId:guid}/level',
    `/api/v1/admin/users/${id}/level`,
    {
      level: 2,
    }
  );

  // ---- sospensione e riattivazione ----
  await call(
    'POST',
    '/api/v1/admin/users/{id}/suspend',
    `/api/v1/admin/users/${id}/suspend`,
    {
      reason: 'verifica audit ondata 1A',
    },
    { livello: 'L3' }
  );
  await call(
    'POST',
    '/api/v1/admin/users/{id}/unsuspend',
    `/api/v1/admin/users/${id}/unsuspend`,
    {}
  );
  await call('POST', '/api/v1/admin/users/{id}/unlock', `/api/v1/admin/users/${id}/unlock`, {});

  // ---- limiti e sessioni ----
  await call(
    'POST',
    '/api/v1/admin/users/{id:guid}/rate-limit-override',
    `/api/v1/admin/users/${id}/rate-limit-override`,
    { requestsPerMinute: 100, reason: 'verifica audit' }
  );
  await call(
    'DELETE',
    '/api/v1/admin/users/{id:guid}/rate-limit-override',
    `/api/v1/admin/users/${id}/rate-limit-override`
  );
  await call(
    'DELETE',
    '/api/v1/admin/users/{userId:guid}/sessions',
    `/api/v1/admin/users/${id}/sessions`,
    undefined,
    { livello: 'L3' }
  );

  // ---- comunicazioni ----
  await call(
    'POST',
    '/api/v1/admin/users/{userId:guid}/reset-password',
    `/api/v1/admin/users/${id}/reset-password`,
    {}
  );
  await call(
    'POST',
    '/api/v1/admin/users/{userId:guid}/send-email',
    `/api/v1/admin/users/${id}/send-email`,
    { subject: 'Verifica audit', body: 'Messaggio di prova dell audit ondata 1A' }
  );

  // ---- impersonificazione: avvio e chiusura nella stessa passata ----
  await call(
    'POST',
    '/api/v1/admin/users/{userId:guid}/impersonate',
    `/api/v1/admin/users/${id}/impersonate`,
    { reason: 'verifica audit ondata 1A' },
    { livello: 'L3' }
  );
  await call(
    'POST',
    '/api/v1/admin/impersonation/end',
    '/api/v1/admin/impersonation/end',
    {},
    {
      livello: 'L3',
    }
  );

  // ---- inviti ----
  const invito = await call('POST', '/api/v1/admin/invitations', '/api/v1/admin/invitations', {
    email: `audit-invito-${marca}@meepleai.test`,
    role: 'user',
  });
  const invitoId: string | undefined = invito.json?.id ?? invito.json?.invitation?.id;
  if (invitoId) {
    await call(
      'POST',
      '/api/v1/admin/invitations/{id:guid}/resend',
      `/api/v1/admin/invitations/${invitoId}/resend`,
      {}
    );
    await call(
      'DELETE',
      '/api/v1/admin/invitations/{id:guid}',
      `/api/v1/admin/invitations/${invitoId}`
    );
  }

  // ---- eliminazione dell'utente di prova: chiude il ciclo e ripulisce ----
  await call('DELETE', '/api/v1/admin/users/{id}', `/api/v1/admin/users/${id}`, undefined, {
    livello: 'L3',
  });

  console.log(`\nattesi: ${attesi} · difformi: ${difformi}`);
}

if (require.main === module) {
  void main();
}
