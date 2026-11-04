/**
 * Il confine SSR di react-pdf non è osservato da nessun altro test (#4058).
 *
 * Una review adversarial del fix lo ha dimostrato per perturbazione: cancellando `ssr: false` da
 * `PdfInlineViewer` e da `ComponentDetail` — cioè **l'intero payload di #4058** — la suite resta
 * verde. Il difetto tornerebbe solo nel container, su Node 20, al primo render server di una delle
 * rotte interessate: un 500 che nessun gate di questa repo vede, perché `test-e2e.yml` non gira
 * sulle PR verso `main-dev` (#4032).
 *
 * Questo test asserisce sulla **sorgente**, non sul comportamento, e non è una scelta pigra: ciò
 * che era sbagliato è una stringa in un oggetto di opzioni, e nessun controllo di tipo la protegge
 * — `dynamic(fn, {})` e `dynamic(fn, { ssr: false })` hanno lo stesso tipo. Renderizzare il
 * componente non aiuta: in jsdom `typeof window !== 'undefined'`, quindi il ramo server non viene
 * mai percorso e un test di rendering passa con o senza il flag.
 *
 * ⚠️ Copre i due punti noti, NON la classe. Un terzo chiamante che introduca un `dynamic()` senza
 * il flag verso un modulo che raggiunge react-pdf passerebbe inosservato — ed è già successo una
 * volta, con `ComponentDetail`. Il gate sul grafo degli import è registrato come follow-up in
 * #4058; va scritto distinguendo `dynamic()` con e senza `ssr: false`, perché un probe che li
 * confonde produce lo zero che conferma il fix (errore occorso durante questo stesso lavoro).
 */

import { readFileSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

const SRC = path.resolve(__dirname, '../../..');

/**
 * I siti dove `ssr: false` è load-bearing, con la ragione per cui ciascuno lo è.
 *
 * `motivo` non è decorazione: compare nel messaggio di errore, così chi vede il test rosso sa
 * perché quella riga esisteva senza dover risalire alla issue.
 */
const CONFINI = [
  {
    file: 'components/pdf/PdfInlineViewer.tsx',
    motivo:
      'carica react-pdf, che tira pdfjs-dist: a module scope esegue un polyfill di ' +
      'Iterator.prototype.join senza guardia su `typeof Iterator`, e su Node 20 quel global non ' +
      'esiste → ReferenceError e 500 in SSR',
    attesi: 2, // <Document> e <Page>
  },
  {
    file: 'components/admin/ui-library/ComponentDetail.tsx',
    motivo:
      'il suo dynamic() raggiunge component-map → meeple-info-card → PdfViewerModal → react-pdf. ' +
      'Era privo del flag, e per questo /admin/ui-library/[id] sopravviveva alla prima stesura del fix',
    attesi: 1,
  },
];

describe('#4058 — confine SSR verso react-pdf', () => {
  it.each(CONFINI)('$file dichiara ssr: false in ogni dynamic()', ({ file, motivo, attesi }) => {
    const sorgente = readFileSync(path.join(SRC, file), 'utf8');
    // Via i commenti prima di contare: questi file **parlano** di `dynamic()` nei commenti che
    // spiegano #4058, e contare le menzioni dava 5 chiamate dove ce ne sono 2. La prima stesura
    // di questo test falliva così — su sé stessa, non sul codice.
    const codice = sorgente.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');

    // Solo le invocazioni assegnate (`const X = dynamic(`), che sono le dichiarazioni di
    // componente: è lì che il flag va messo.
    const dynamicCount = (codice.match(/=\s*dynamic\s*\(/g) ?? []).length;
    // Spaziatura libera: prettier potrebbe riformattare l'oggetto di opzioni.
    const ssrFalseCount = (codice.match(/\bssr\s*:\s*false\b/g) ?? []).length;

    expect(
      dynamicCount,
      `${file}: nessun dynamic() trovato. Se il file è stato ristrutturato, questo test va ` +
        `riscritto, non cancellato — ${motivo}`
    ).toBeGreaterThanOrEqual(attesi);

    expect(
      ssrFalseCount,
      `${file}: ${dynamicCount} dynamic() ma solo ${ssrFalseCount} con \`ssr: false\`.\n` +
        `Perché serve: ${motivo}.\n` +
        `⚠️ \`dynamic()\` da solo NON è un confine SSR: in ` +
        `next/dist/shared/lib/lazy-dynamic/loadable.js il default è \`ssr: true\`, e solo il ramo ` +
        `\`ssr: false\` avvolge il componente in BailoutToCSR tenendolo fuori dal render server.`
    ).toBeGreaterThanOrEqual(dynamicCount);
  });

  it('nessun import statico di react-pdf fuori dai moduli protetti da un barrel', () => {
    // I file che importano react-pdf staticamente sono raggiunti solo attraverso un barrel che
    // applica `dynamic(..., { ssr: false })` — `components/pdf/index.ts` e
    // `components/library/index.ts`. Il test fissa quella lista: un file nuovo che importa
    // react-pdf staticamente e non è in questo elenco è un percorso SSR non protetto.
    const protetti = [
      'components/pdf/PdfPreview.tsx',
      'components/pdf/PdfViewerModal.tsx',
      'components/features/kb-globale/KbDocViewerDesktop.tsx',
      'components/features/kb-globale/KbDocViewerMobile.tsx',
    ];

    for (const file of protetti) {
      const sorgente = readFileSync(path.join(SRC, file), 'utf8');
      expect(
        /from ['"]react-pdf['"]/.test(sorgente),
        `${file} non importa più react-pdf staticamente: aggiorna questo elenco, che serve a ` +
          `distinguere i percorsi protetti da un barrel da quelli nuovi e non protetti`
      ).toBe(true);
    }

    // `PdfInlineViewer` NON deve comparire fra questi: è il file che il fix ha convertito, e un
    // import statico lì sarebbe la regressione esatta di #4058.
    const viewer = readFileSync(path.join(SRC, 'components/pdf/PdfInlineViewer.tsx'), 'utf8');
    expect(
      /import\s+\{[^}]*\}\s+from ['"]react-pdf['"]/.test(viewer),
      'PdfInlineViewer è tornato a importare react-pdf staticamente: è la regressione di #4058, ' +
        'e si manifesta come 500 su /games/[id]/card solo nel container (Node 20), non in sviluppo'
    ).toBe(false);
  });
});
