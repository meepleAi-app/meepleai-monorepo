/**
 * Gate di architettura — #4059: in una `page.tsx` sotto un segmento dinamico, `params` deve
 * essere tipizzato `Promise<...>`.
 *
 * **Il difetto che questo gate rende impossibile.** La pagina
 * `admin/agents/definitions/[id]/edit/page.tsx` dichiarava `{ params: { id: string } }` e
 * leggeva `params.id` direttamente. In Next 16 `params` e' una Promise, quindi quella proprieta'
 * non esiste: il valore era `undefined` e finiva interpolato nel path. Misurato con un id reale
 * nella URL del browser: `404 /api/v1/admin/agent-definitions/undefined`, e a schermo
 * «Agent not found» — la diagnosi sbagliata, perche' quello e' il messaggio di un id inesistente.
 *
 * **Perche' un gate sul TIPO e non sull'uso.** TypeScript non poteva accorgersene: l'annotazione
 * *dichiarava* un oggetto sincrono, quindi `params.id` era legittimo per il compilatore. Con
 * `Promise<{ id: string }>` quell'accesso non compila, e ogni uso corretto passa per `use()` o
 * `await`. Il tipo sbagliato era la causa, non la conseguenza — ed e' l'unica cosa che un
 * controllo statico puo' vedere.
 *
 * **Costo del gate: zero.** Quando e' stato scritto, 36 pagine dinamiche su 37 rispettavano
 * gia' la convenzione. Non introduce una regola nuova: impedisce alla trentottesima di
 * divergere.
 *
 * Contratto dalla versione installata, non dalla memoria:
 * `node_modules/next/dist/docs/01-app/03-api-reference/03-file-conventions/dynamic-routes.md`
 *   `params: Promise<{ slug: string }>` · `const { slug } = use(params)` nel Client Component.
 */

import { readFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

// `glob` e non `fast-glob`: e' quello presente in node_modules, e lo usano gia' gli altri test
// che scandiscono l'albero (es. meeple-card/__tests__/no-inline-card-reimplementation.test.tsx).
import { sync as globSync } from 'glob';
import { describe, expect, it } from 'vitest';

const APP_DIR = join(process.cwd(), 'src', 'app');

/**
 * Toglie commenti di blocco e di riga. Condiviso dai due controlli qui sotto: entrambi cercano
 * pattern che compaiono legittimamente nella prosa che documenta il contratto.
 */
function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
}

/** Pagine sotto almeno un segmento dinamico `[x]`, che sono le sole a ricevere `params`. */
function dynamicPageFiles(): string[] {
  return globSync('**/page.tsx', { cwd: APP_DIR, absolute: true }).filter(file =>
    relative(APP_DIR, file)
      .split(sep)
      .some(seg => seg.startsWith('['))
  );
}

describe('gate: `params` tipizzato Promise nelle pagine dinamiche (#4059)', () => {
  const files = dynamicPageFiles();

  it('trova delle pagine dinamiche da controllare', () => {
    // Guardia di non-vacuita'. Senza questa, un cambio di struttura delle cartelle o una glob
    // sbagliata renderebbe il gate verde su un insieme vuoto — e un gate che non guarda niente
    // passa sempre.
    expect(files.length).toBeGreaterThan(20);
  });

  it('nessuna pagina dinamica dichiara `params` come oggetto sincrono', () => {
    const offenders: string[] = [];

    for (const file of files) {
      // 🔴 I commenti vanno via PRIMA di cercare: questo controllo, nella sua prima stesura,
      // leggeva il sorgente grezzo, e il doc comment della pagina corretta — che cita
      // `params: Promise<{ slug: string }>` come riferimento — bastava a far credere al test
      // che la dichiarazione fosse giusta. Verificato per perturbazione: con la firma riportata
      // alla forma difettosa, questo test restava VERDE mentre quello sull'uso falliva.
      // È la stessa trappola di un gate che conta occorrenze nei commenti.
      const source = stripComments(readFileSync(file, 'utf8'));
      // Interessa solo chi dichiara il prop: una pagina che non lo usa non ha nulla da sbagliare.
      if (!/\bparams\s*:/.test(source)) continue;

      // `params: {` senza `Promise<` subito dopo i due punti.
      const syncDeclaration = /\bparams\s*:\s*\{/.test(source);
      const promiseDeclaration = /\bparams\s*:\s*Promise\s*</.test(source);

      if (syncDeclaration && !promiseDeclaration) {
        offenders.push(relative(APP_DIR, file));
      }
    }

    expect(
      offenders,
      "in Next 16 `params` e' una Promise: va tipizzata `Promise<...>` e letta con `use(params)` nel client o `await params` nel server"
    ).toEqual([]);
  });

  it('nessuna pagina legge una proprieta` di `params` senza prima risolverlo', () => {
    // Il complemento del test sopra: il tipo giusto con un accesso diretto non compila, ma un
    // `as any` o una destrutturazione creativa lo aggirerebbero. Questo guarda l'uso.
    const offenders: string[] = [];

    for (const file of files) {
      const code = stripComments(readFileSync(file, 'utf8'));

      const readsPropertyDirectly = /\bparams\.[a-zA-Z_]/.test(code);
      // ⚠️ `useParams` accetta un parametro di tipo, quindi nel codice reale compare come
      // `useParams<{ id: string }>()`. Una prima stesura di questo controllo cercava
      // `useParams\(` e segnalava `knowledge-base/[id]/pdf/page.tsx` — che è **corretta**: usa
      // `useParams`, che restituisce un oggetto già risolto. Un gate può essere non vacuo e
      // sbagliato allo stesso tempo, e questo lo era: avrebbe bloccato una pagina giusta.
      const resolves =
        /\buse\(\s*params\s*\)|\bawait\s+params\b|\buseParams\s*(<[^>]*>)?\s*\(/.test(code);

      if (readsPropertyDirectly && !resolves) {
        offenders.push(relative(APP_DIR, file));
      }
    }

    expect(
      offenders,
      'leggere `params.<prop>` senza `use(params)` / `await params` da `undefined`, che finisce interpolato nelle URL'
    ).toEqual([]);
  });
});
