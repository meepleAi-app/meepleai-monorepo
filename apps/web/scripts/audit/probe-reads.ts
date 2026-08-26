/**
 * Prova sistematicamente gli endpoint di sola lettura di un blocco del tracker,
 * con due ruoli, e ne registra l'esito.
 *
 * Serve per i contesti dove le letture sono decine: farle a mano una per una
 * costa ore e l'attenzione cala proprio dove serve. Le mutazioni restano fuori
 * di proposito — un DELETE provato "per vedere" su dati reali non si annulla.
 *
 * Uso:
 *   pnpm exec tsx scripts/audit/probe-reads.ts "<regex sui path>" [contesto]
 *
 * Su Git Bash (Windows) anteporre MSYS_NO_PATHCONV=1: un argomento che inizia
 * con `/` viene altrimenti convertito in path Windows — `/users|impersonation`
 * diventa `C:/Program Files/Git/users|impersonation`, e la regex risultante
 * matcha solo il secondo ramo dell'alternanza. Lo script prova otto endpoint
 * invece di cinquantasette e non se ne lamenta: per questo stampa quante righe
 * ha selezionato dal tracker, che è il numero da controllare.
 *
 * Legge i cookie di sessione da audit-results/cookies.json:
 *   {"admin":"meepleai_session=…","user":"meepleai_session=…"}
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, readFileSync, writeFileSync } from 'node:fs';
import path from 'node:path';

const CSV = path.resolve(
  '../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv'
);
const RESULTS = path.resolve('audit-results');
const API = process.env.AUDIT_API_BASE ?? 'http://localhost:8080';

/**
 * Sostituisce {nome:tipo} e {nome} con un valore reale; null se manca.
 * I valori arrivano come parametro: leggerli a livello di modulo renderebbe
 * questo file non importabile senza i file su disco, test compresi.
 */
export function fillPath(template: string, values: Record<string, string>): string | null {
  let missing = false;
  const filled = template.replace(/\{(\w+)(?::[^}]+)?\}/g, (_, name: string) => {
    const value = values[name] ?? values[name.replace(/Id$/, '')];
    if (!value) missing = true;
    return value ?? '';
  });
  return missing ? null : filled;
}

/**
 * Giudizio di conformità di un endpoint di lettura.
 *
 * Il criterio dipende da CHI dovrebbe poterlo leggere, informazione che il
 * tracker già porta nella colonna `ruolo`:
 *   - `admin`  → l'utente semplice deve essere respinto (401/403)
 *   - `user`   → l'utente semplice deve poter accedere: `/users/me/quota` letto
 *                dal proprio titolare è il funzionamento previsto, non una falla
 *
 * Senza questa distinzione ogni endpoint self-service risulterebbe difforme, e
 * trentatré falsi positivi renderebbero inutile l'intero elenco.
 *
 * Un 404 lato admin non è di per sé un difetto — la risorsa può non esistere —
 * ma resta annotato.
 */
export function judge(
  adminStatus: number,
  userStatus: number,
  ruoloAtteso: string
): { esito: 'atteso' | 'difforme'; nota: string } {
  const ok = (s: number): boolean => s >= 200 && s < 300;
  const respinto = userStatus === 401 || userStatus === 403;

  if (adminStatus === 0 || userStatus === 0)
    return { esito: 'difforme', nota: 'nessuna risposta entro 15s' };

  if (ruoloAtteso === 'user') {
    if (ok(userStatus)) return { esito: 'atteso', nota: 'endpoint self-service' };
    if (userStatus === 404) return { esito: 'atteso', nota: 'self-service, risorsa assente' };
    return {
      esito: 'difforme',
      nota: `endpoint destinato all'utente ma risponde ${userStatus}`,
    };
  }

  if (ok(adminStatus) && respinto) return { esito: 'atteso', nota: '' };
  if (adminStatus === 404 && respinto)
    return { esito: 'atteso', nota: 'admin 404: risorsa assente, non un difetto di per sé' };
  if (!respinto)
    return { esito: 'difforme', nota: `endpoint admin non respinge l'utente: ${userStatus}` };
  return { esito: 'difforme', nota: `admin riceve ${adminStatus}` };
}

async function main(): Promise<void> {
  const pattern = new RegExp(process.argv[2] ?? '.', 'i');
  const contesto = process.argv[3];

  const paramValues: Record<string, string> = JSON.parse(
    readFileSync(path.join(RESULTS, 'probe-params.json'), 'utf8')
  );
  const cookies: Record<string, string> = JSON.parse(
    readFileSync(path.join(RESULTS, 'cookies.json'), 'utf8')
  );

  const rows = readFileSync(CSV, 'utf8')
    .trim()
    .split('\n')
    .slice(1)
    .map(l => l.split(','))
    .filter(
      c =>
        c[1] === 'endpoint' &&
        c[3] === 'GET' &&
        c[7].includes('non coperto') &&
        pattern.test(c[2]) &&
        (!contesto || c[4] === contesto)
    );

  console.log(`righe selezionate dal tracker: ${rows.length}`);

  // Il nome dell'output porta il contesto: un file unico veniva sovrascritto
  // dalla passata successiva, e i risultati della precedente sparivano prima di
  // essere riportati nel tracker.
  const out = path.join(RESULTS, `probe-reads-${(contesto ?? 'tutti').toLowerCase()}.jsonl`);
  writeFileSync(out, '', 'utf8');
  let attesi = 0;
  let difformi = 0;
  let saltati = 0;

  for (const c of rows) {
    const url = fillPath(c[2], paramValues);
    if (!url) {
      saltati += 1;
      continue;
    }

    // Timeout esplicito: senza, una richiesta che non risponde blocca l'intera
    // passata a tempo indeterminato — è accaduto su /admin/status/*, e la sonda
    // si è fermata a due terzi senza segnalare nulla. Meglio registrare 0 e
    // proseguire: uno zero nel report è un dato, un blocco no.
    const call = async (role: string): Promise<number> => {
      try {
        const res = await fetch(`${API}${url}`, {
          headers: { Cookie: cookies[role] ?? '' },
          signal: AbortSignal.timeout(15_000),
        });
        return res.status;
      } catch {
        return 0;
      }
    };

    const adminStatus = await call('admin');
    const userStatus = await call('user');
    const { esito, nota } = judge(adminStatus, userStatus, c[5]);
    esito === 'atteso' ? (attesi += 1) : (difformi += 1);

    appendFileSync(
      out,
      JSON.stringify({
        metodo: 'GET',
        path: c[2],
        livello: 'L2',
        esito,
        evidenza: `admin ${adminStatus} · utente ${userStatus}${nota ? ` — ${nota}` : ''}`,
      }) + '\n',
      'utf8'
    );

    if (esito === 'difforme')
      console.log(`DIFF  GET ${c[2]} — admin ${adminStatus} · utente ${userStatus} ${nota}`);
  }

  console.log(
    `\nprovati: ${attesi + difformi} · attesi: ${attesi} · difformi: ${difformi} · saltati per parametro: ${saltati}`
  );
}

if (require.main === module) {
  void main();
}
