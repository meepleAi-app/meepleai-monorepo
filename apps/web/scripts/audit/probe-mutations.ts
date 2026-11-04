/**
 * Sonda di raggiungibilità per le mutazioni.
 *
 * Non dimostra che una mutazione faccia la cosa giusta — quello richiede di
 * costruire lo stato, eseguirla e verificarne l'effetto, un endpoint alla volta.
 * Dimostra però che l'endpoint **esiste, autorizza e valida**, e soprattutto
 * scova i 500: un errore server su richiesta malformata o su risorsa assente è
 * un difetto indipendentemente dal payload.
 *
 * Le righe verificate qui restano quindi a livello **L1**, non L2.
 *
 * Due regole di sicurezza:
 *   1. PUT, PATCH e DELETE usano un id INESISTENTE. Ci si aspetta 404: così si
 *      prova il comportamento senza toccare dati reali. Un 500 al posto del 404
 *      è un difetto — e frequente, perché il caso "risorsa assente" è quello che
 *      si dimentica di gestire.
 *   2. Gli endpoint il cui path contiene parole di operazioni irreversibili
 *      vengono saltati e riportati come tali, mai eseguiti "per vedere".
 *
 * Uso: MSYS_NO_PATHCONV=1 pnpm exec tsx scripts/audit/probe-mutations.ts <contesto>
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

/** Id che non esiste: usato per non toccare risorse reali. */
export const ID_INESISTENTE = '00000000-0000-0000-0000-000000000009';

/** Operazioni che non si provano mai: irreversibili o con effetti fuori dal sistema. */
const PAROLE_VIETATE = [
  'restart',
  'purge',
  'cleanup',
  'migrate',
  'rotate',
  'bulk',
  'orchestrate',
  'backup',
  'restore',
  'truncate',
  'drop',
  'reindex',
  'rebuild',
  'test-connection',
  'send-email',
  'broadcast',
  'import',
  'seed',
  'shutdown',
  'kill',
];

/**
 * Percorsi che agiscono sul soggetto autenticato: la sonda userebbe la propria
 * sessione per operare su se stessa. `DELETE /users/me` è il caso limite —
 * cancellerebbe l'account con cui l'audit sta lavorando.
 *
 * Le parole chiave non bastano a coprirli: qui non compare nessun verbo
 * pericoloso, eppure l'effetto è il più distruttivo possibile.
 */
const PERCORSI_SU_SE_STESSI = [/\/users\/me(\/|$)/i, /\/me\/account/i, /\/auth\/account/i];

export function daSaltare(p: string, metodo = 'POST'): string | null {
  const parola = PAROLE_VIETATE.find(w => p.toLowerCase().includes(w));
  if (parola) return `operazione irreversibile o con effetti esterni: contiene "${parola}"`;

  if (metodo === 'DELETE' && PERCORSI_SU_SE_STESSI.some(re => re.test(p)))
    return 'agisce sul soggetto autenticato: cancellerebbe l account usato dall audit';

  return null;
}

/**
 * Giudizio su una mutazione sondata.
 *
 * Con un id inesistente ci si aspetta 404. Con un payload vuoto ci si aspetta
 * 400 o 422. Entrambi dicono che l'endpoint funziona: è il 500 a essere un
 * difetto, perché nessuna richiesta dovrebbe far esplodere il server.
 */
export function giudica(
  status: number,
  conIdFinto: boolean
): { esito: 'atteso' | 'difforme'; nota: string } {
  if (status === 0) return { esito: 'difforme', nota: 'nessuna risposta entro 15s' };
  if (status >= 500) return { esito: 'difforme', nota: `errore server: ${status}` };
  if (status === 404 && conIdFinto)
    return { esito: 'atteso', nota: '404 su risorsa inesistente: gestito' };
  if (status === 400 || status === 422)
    return { esito: 'atteso', nota: `${status}: validazione attiva, endpoint raggiungibile` };
  if (status === 401 || status === 403)
    return { esito: 'atteso', nota: `${status}: autorizzazione applicata` };
  if (status >= 200 && status < 300) return { esito: 'atteso', nota: `${status}` };
  return { esito: 'atteso', nota: `${status}` };
}

async function main(): Promise<void> {
  const contesto = process.argv[2];
  if (!contesto) throw new Error('serve il nome del contesto');

  const cookies: Record<string, string> = JSON.parse(
    readFileSync(path.join(RESULTS, 'cookies.json'), 'utf8')
  );

  const righe = readFileSync(CSV, 'utf8')
    .trim()
    .split('\n')
    .slice(1)
    .map(l => l.split(','))
    .filter(
      c =>
        c[1] === 'endpoint' && c[3] !== 'GET' && c[7].includes('non coperto') && c[4] === contesto
    );

  const out = path.join(RESULTS, `probe-mutations-${contesto.toLowerCase()}.jsonl`);
  writeFileSync(out, '', 'utf8');
  console.log(`righe selezionate: ${righe.length}`);

  let attesi = 0;
  let difformi = 0;
  let saltati = 0;
  let consecutivi401 = 0;

  for (const c of righe) {
    const [, , tracker, metodo] = c;

    const motivo = daSaltare(tracker, metodo);
    if (motivo) {
      saltati += 1;
      appendFileSync(
        out,
        JSON.stringify({
          metodo,
          path: tracker,
          livello: 'L1',
          esito: 'saltato',
          evidenza: motivo,
        }) + '\n',
        'utf8'
      );
      continue;
    }

    const conIdFinto = /\{/.test(tracker);
    const url = tracker.replace(/\{[^}]+\}/g, ID_INESISTENTE);

    let status = 0;
    try {
      const res = await fetch(`${API}${url}`, {
        method: metodo,
        headers: { Cookie: cookies.admin ?? '', 'Content-Type': 'application/json' },
        body: metodo === 'DELETE' ? undefined : '{}',
        signal: AbortSignal.timeout(15_000),
      });
      status = res.status;
    } catch {
      status = 0;
    }

    // Una sessione che scade a metà passata trasforma ogni chiamata successiva
    // in un 401, che il giudizio leggerebbe come "autorizzazione applicata":
    // centinaia di endpoint risulterebbero conformi senza essere mai stati
    // provati. Meglio fermarsi e dirlo.
    consecutivi401 = status === 401 ? consecutivi401 + 1 : 0;
    if (consecutivi401 >= 5) {
      console.log(
        `
INTERROTTO: cinque 401 consecutivi — la sessione non è più valida.
` + `I risultati raccolti finora restano validi; il resto va rieseguito con cookie freschi.`
      );
      break;
    }

    const { esito, nota } = giudica(status, conIdFinto);
    esito === 'atteso' ? (attesi += 1) : (difformi += 1);

    appendFileSync(
      out,
      JSON.stringify({ metodo, path: tracker, livello: 'L1', esito, evidenza: nota }) + '\n',
      'utf8'
    );
    if (esito === 'difforme') console.log(`DIFF ${metodo.padEnd(6)} ${tracker.padEnd(56)} ${nota}`);
  }

  console.log(`\nattesi: ${attesi} · difformi: ${difformi} · saltati per sicurezza: ${saltati}`);
  console.log(`esiti in ${path.relative(process.cwd(), out)}`);
}

if (require.main === module) {
  void main();
}
