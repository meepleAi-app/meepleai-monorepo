// #4482: il confronto fra la citazione verificata di una domanda e i chunk recuperati.
//
// La citazione (`verifica.citazione`) è stata estratta dal PDF con un estrattore diverso da quello
// del corpus: spazi, a capo, trattini e accenti possono differire. Per questo il confronto non cerca
// la stringa, ma quanta parte delle sequenze di parole della citazione sta nel chunk.
//
// Il metodo:
//   1. normalizza i due testi: parole spezzate da un trattino a fine riga ricomposte, accenti tolti,
//      minuscole, solo lettere e cifre, spazi singoli;
//   2. divide la citazione nei suoi frammenti, separati da «[…]» o «[...]»: il set li usa quando la
//      citazione salta un pezzo del regolamento, e i due pezzi possono stare in chunk diversi;
//   3. per ogni frammento conta quanti dei suoi trigrammi di parole (sequenze di 3 parole consecutive)
//      compaiono nel chunk: il contenimento. Il frammento è nel chunk se il contenimento arriva alla
//      soglia;
//   4. se nessun chunk da solo arriva alla soglia, prova le coppie di chunk consecutivi dello stesso
//      documento (indici che differiscono di 1): un passaggio spezzato a metà fra due chunk è ancora
//      recuperato, se arrivano entrambi. La coppia è segnalata. Due chunk qualsiasi no: la loro unione
//      somma frasi di forma parallela («The first player to have 3 Knight cards…» e «…5 continuous
//      roads…») e supera la soglia senza contenere il passaggio.
//
// La soglia predefinita, 0,7, e il perché stanno nel README accanto.

/** Contenimento minimo dei trigrammi di un frammento perché il frammento valga trovato. */
export const SOGLIA_PREDEFINITA = 0.7;

/** Lunghezza delle sequenze di parole confrontate. */
export const N_PREDEFINITO = 3;

/**
 * Un frammento più corto di così («Contadino», fra due «[…]») non identifica un passaggio: si ignora,
 * se la citazione ha anche frammenti più lunghi.
 */
export const PAROLE_MINIME_FRAMMENTO = 4;

const SEPARATORE_FRAMMENTI = /\[\s*(?:…|\.\.\.)\s*\]|…/u;

/**
 * Il testo ridotto a parole confrontabili.
 * @param {string} testo
 * @returns {string} parole minuscole, solo lettere e cifre, separate da uno spazio
 */
export function normalizza(testo) {
  return String(testo ?? '')
    // Il trattino morbido non si vede, e un estrattore lo tiene dove l'altro no.
    .replace(/­/gu, '')
    // «re-\nsource» → «resource»: la parola spezzata a fine riga.
    .replace(/(\p{L})[-‐‑]\s*[\r\n]+\s*(\p{L})/gu, '$1$2')
    // «re- source»: lo stesso a capo, che l'estrattore ha già reso con uno spazio. Solo davanti a una
    // minuscola: «Catan - The» o «pre- e post-» restano parole distinte.
    .replace(/(\p{L})[-‐‑][ \t]+(\p{Ll})/gu, '$1$2')
    // «ﬁ» → «fi», «più» → «piu».
    .normalize('NFKD')
    .replace(/\p{M}+/gu, '')
    .toLowerCase()
    .replace(/[^\p{L}\p{N}]+/gu, ' ')
    .trim();
}

/**
 * @param {string[]} parole
 * @param {number} n
 * @returns {Set<string>}
 */
export function ngrammi(parole, n) {
  const insieme = new Set();
  for (let i = 0; i + n <= parole.length; i++) {
    insieme.add(parole.slice(i, i + n).join(' '));
  }
  return insieme;
}

/**
 * I frammenti confrontabili di una citazione.
 * @param {string} citazione
 * @param {{ n?: number, paroleMinime?: number }} [opzioni]
 * @returns {{ testo: string, parole: string[], n: number, ngrammi: Set<string> }[]}
 */
export function frammenti(citazione, opzioni = {}) {
  const n = opzioni.n ?? N_PREDEFINITO;
  const paroleMinime = opzioni.paroleMinime ?? PAROLE_MINIME_FRAMMENTO;

  const pezzi = String(citazione ?? '')
    .split(SEPARATORE_FRAMMENTI)
    .map((testo) => ({ testo: testo.trim(), parole: parole(testo) }))
    .filter((pezzo) => pezzo.parole.length > 0);

  const lunghi = pezzi.filter((pezzo) => pezzo.parole.length >= paroleMinime);
  if (lunghi.length > 0) {
    return lunghi.map((pezzo) => ({ ...pezzo, n, ngrammi: ngrammi(pezzo.parole, n) }));
  }

  // Una citazione corta, tutta: le sequenze sono lunghe quanto lei, se ha meno di n parole. Così una
  // citazione di due parole vale trovata solo se le due parole stanno di seguito.
  const tutta = parole(pezzi.map((pezzo) => pezzo.testo).join(' '));
  if (tutta.length === 0) {
    return [];
  }
  const nCorto = Math.min(n, tutta.length);
  return [{ testo: String(citazione).trim(), parole: tutta, n: nCorto, ngrammi: ngrammi(tutta, nCorto) }];
}

/**
 * La frazione dei trigrammi del frammento presenti in uno o più chunk.
 * @param {Set<string>} delFrammento
 * @param {Set<string>[]} deiChunk
 */
export function contenimento(delFrammento, ...deiChunk) {
  if (delFrammento.size === 0) {
    return 0;
  }
  let presenti = 0;
  for (const g of delFrammento) {
    if (deiChunk.some((insieme) => insieme.has(g))) {
      presenti++;
    }
  }
  return presenti / delFrammento.size;
}

/**
 * Dove sta la citazione in una lista ordinata di chunk.
 *
 * @param {string} citazione
 * @param {(string | { testo: string, documento?: string | null, indice?: number | null })[]} chunk
 *   i chunk nell'ordine della lista (posizione 1 = il primo): il testo, e se si conoscono il documento
 *   e l'indice del chunk nel documento, che servono a riconoscere due chunk consecutivi
 * @param {{ soglia?: number, n?: number, paroleMinime?: number }} [opzioni]
 * @returns {{
 *   trovata: boolean,
 *   parziale: boolean,
 *   posizione: number | null,
 *   frammenti: {
 *     testo: string,
 *     posizione: number | null,
 *     coppia: [number, number] | null,
 *     punteggio: number,
 *     migliore: { posizione: number | null, punteggio: number },
 *   }[],
 * }}
 *   `posizione` è la più piccola K per cui tutti i frammenti stanno nei primi K chunk; `coppia` dice
 *   che il frammento sta solo nell'unione di due chunk; `migliore` è il chunk più vicino anche sotto
 *   la soglia, per leggere i casi mancati.
 */
export function cercaCitazione(citazione, chunk, opzioni = {}) {
  const soglia = opzioni.soglia ?? SOGLIA_PREDEFINITA;
  const daCercare = frammenti(citazione, opzioni);
  const elementi = chunk.map((c) => (typeof c === 'string' ? { testo: c } : c));
  const cache = new Map();
  const insiemi = (n) => {
    if (!cache.has(n)) {
      cache.set(n, elementi.map((c) => ngrammi(parole(c.testo), n)));
    }
    return cache.get(n);
  };
  const consecutivi = (a, b) =>
    a.documento != null &&
    a.documento === b.documento &&
    Number.isInteger(a.indice) &&
    Number.isInteger(b.indice) &&
    Math.abs(a.indice - b.indice) === 1;

  const esiti = daCercare.map((frammento) => {
    const deiChunk = insiemi(frammento.n);
    let migliore = { posizione: null, punteggio: 0 };
    let posizione = null;
    let punteggio = 0;

    deiChunk.forEach((insieme, i) => {
      const c = contenimento(frammento.ngrammi, insieme);
      if (c > migliore.punteggio) {
        migliore = { posizione: i + 1, punteggio: c };
      }
      if (posizione === null && c >= soglia) {
        posizione = i + 1;
        punteggio = c;
      }
    });

    let coppia = null;
    if (posizione === null) {
      // La coppia che entra per prima nella lista: quella con il secondo chunk più in alto.
      for (let j = 1; j < deiChunk.length && coppia === null; j++) {
        for (let i = 0; i < j; i++) {
          if (!consecutivi(elementi[i], elementi[j])) {
            continue;
          }
          const c = contenimento(frammento.ngrammi, deiChunk[i], deiChunk[j]);
          if (c >= soglia) {
            coppia = [i + 1, j + 1];
            posizione = j + 1;
            punteggio = c;
            break;
          }
        }
      }
    }

    return {
      testo: frammento.testo,
      posizione,
      coppia,
      punteggio: arrotonda(punteggio),
      migliore: { posizione: migliore.posizione, punteggio: arrotonda(migliore.punteggio) },
    };
  });

  const trovati = esiti.filter((esito) => esito.posizione !== null);
  const trovata = esiti.length > 0 && trovati.length === esiti.length;
  return {
    trovata,
    parziale: !trovata && trovati.length > 0,
    posizione: trovata ? Math.max(...esiti.map((esito) => esito.posizione)) : null,
    frammenti: esiti,
  };
}

/**
 * La lingua di una citazione, dalle parole più frequenti: «en» o «it». Serve a separare nel riepilogo
 * le domande su un regolamento inglese (la domanda è in italiano) da quelle su un regolamento italiano.
 * @param {string} testo
 * @returns {'en' | 'it' | 'sconosciuta'}
 */
export function linguaDelTesto(testo) {
  const conta = { en: 0, it: 0 };
  for (const parola of parole(testo)) {
    if (PAROLE_EN.has(parola)) conta.en++;
    if (PAROLE_IT.has(parola)) conta.it++;
  }
  if (conta.en === conta.it) {
    return 'sconosciuta';
  }
  return conta.en > conta.it ? 'en' : 'it';
}

const PAROLE_EN = new Set(
  'the of and to a in is you your are may must can not with for on or each player players their they this that be it any one an at from by if when'.split(' '),
);
const PAROLE_IT = new Set(
  'il lo la i gli le di del della dei delle e un una per con non che si al alla sono puo deve ogni giocatore giocatori suo sua nel nella da se quando ad'.split(' '),
);

function parole(testo) {
  const normalizzato = normalizza(testo);
  return normalizzato.length === 0 ? [] : normalizzato.split(' ');
}

function arrotonda(x) {
  return Math.round(x * 1000) / 1000;
}
