#!/usr/bin/env node
// #4482: per ogni domanda di riferimento con una citazione verificata, il passaggio citato è fra i
// chunk che la chat recupera? Chiama la sonda di solo recupero dell'API
// (POST /api/v1/admin/kb/retrieval-probe), che non chiama nessun modello linguistico.
//
// Uso e decisioni: README.md accanto. Le parti che decidono senza la rete sono esportate e provate in
// recupero.test.mjs.
import { readFile, writeFile } from 'node:fs/promises';
import { pathToFileURL } from 'node:url';
import { parseArgs } from 'node:util';

import { cercaCitazione, linguaDelTesto, SOGLIA_PREDEFINITA } from './confronto.mjs';

const COOKIE_PREDEFINITO = 'meepleai_session';

const USO = `Uso:
  node tests/evaluation-datasets/tools/recupero.mjs --set <domande.json> [opzioni]

Opzioni:
  --set <file>             il set di domande (obbligatorio)
  --api <url>              l'URL base dell'API (predefinito: http://localhost:8080)
  --giochi <file>          mappa JSON titolo del gioco -> gameId; senza, i titoli si risolvono con
                           GET /api/v1/admin/kb/vector-stats (i giochi indicizzati)
  --gioco <titolo>         solo le domande di questo gioco (ripetibile)
  --id <id,id,...>         solo queste domande
  --accept-language <v>    l'Accept-Language della chat (predefinito: it)
  --soglia <0..1>          contenimento minimo dei trigrammi (predefinito: ${SOGLIA_PREDEFINITA})
  --cookie-nome <nome>     il nome del cookie di sessione (predefinito: ${COOKIE_PREDEFINITO})
  --completo               mette nell'uscita la risposta intera della sonda, chunk compresi
  --out <file>             scrive l'esito JSON su file invece che sullo standard output

La sessione admin si passa nella variabile d'ambiente MEEPLEAI_ADMIN_COOKIE: il valore del cookie, oppure
l'header intero se comincia con «<nome del cookie>=». Non viene mai stampata.`;

async function main() {
  const { values } = parseArgs({
    options: {
      set: { type: 'string' },
      api: { type: 'string', default: 'http://localhost:8080' },
      giochi: { type: 'string' },
      gioco: { type: 'string', multiple: true },
      id: { type: 'string' },
      'accept-language': { type: 'string', default: 'it' },
      soglia: { type: 'string' },
      'cookie-nome': { type: 'string', default: COOKIE_PREDEFINITO },
      completo: { type: 'boolean', default: false },
      out: { type: 'string' },
      help: { type: 'boolean', short: 'h', default: false },
    },
  });

  if (values.help) {
    console.log(USO);
    return 0;
  }
  if (!values.set) {
    console.error(`Manca --set.\n\n${USO}`);
    return 2;
  }

  const soglia = values.soglia === undefined ? SOGLIA_PREDEFINITA : Number(values.soglia);
  if (!(soglia > 0 && soglia <= 1)) {
    console.error(`--soglia deve stare fra 0 (escluso) e 1: «${values.soglia}».`);
    return 2;
  }

  const cookie = intestazioneCookie(process.env.MEEPLEAI_ADMIN_COOKIE, values['cookie-nome']);
  if (!cookie) {
    console.error('Manca la variabile d\'ambiente MEEPLEAI_ADMIN_COOKIE (la sessione admin).');
    return 2;
  }

  const api = values.api.replace(/\/+$/u, '');
  const chiama = (percorso, init = {}) =>
    fetch(`${api}${percorso}`, {
      ...init,
      headers: { cookie, accept: 'application/json', ...(init.headers ?? {}) },
    });

  const tutteLeDomande = JSON.parse(await readFile(values.set, 'utf8'));
  const domande = selezione(tutteLeDomande, values);
  const conCitazione = domande.filter((d) => typeof d?.verifica?.citazione === 'string' && d.verifica.citazione.trim());
  if (conCitazione.length === 0) {
    console.error('Nessuna domanda con verifica.citazione nella selezione.');
    return 2;
  }

  const giochi = values.giochi
    ? JSON.parse(await readFile(values.giochi, 'utf8'))
    : await giochiDallApi(chiama);
  const titoli = [...new Set(conCitazione.map((d) => d.game))];
  const idDi = risolviTitoli(titoli, giochi);
  const linguaDi = lingueDeiRegolamenti(tutteLeDomande);

  const esiti = [];
  for (const domanda of conCitazione) {
    const esito = await misura(domanda, idDi.get(domanda.game), {
      chiama,
      soglia,
      linguaRegolamento: linguaDi.get(domanda.game),
      acceptLanguage: values['accept-language'],
      completo: values.completo,
    });
    esiti.push(esito);
    console.error(riga(esito));
  }

  const uscita = {
    data: new Date().toISOString(),
    api,
    set: values.set,
    soglia,
    accept_language: values['accept-language'],
    giochi: Object.fromEntries(idDi),
    servizi: servizi(esiti),
    riepilogo: riepilogo(esiti),
    domande: esiti,
  };

  console.error(`\n${tabella(uscita.riepilogo)}`);

  const json = `${JSON.stringify(uscita, null, 2)}\n`;
  if (values.out) {
    await writeFile(values.out, json, 'utf8');
    console.error(`\nEsito scritto in ${values.out}`);
  } else {
    process.stdout.write(json);
  }
  return 0;
}

/**
 * L'header Cookie dalla variabile d'ambiente. È già un header solo se comincia con «<nome>=»; altrimenti
 * è il valore del cookie, che può finire con «=» (base64) e non va letto come «nome=valore».
 * @param {string | undefined} valore
 * @param {string} nome il nome del cookie di sessione
 * @returns {string | null}
 */
export function intestazioneCookie(valore, nome = COOKIE_PREDEFINITO) {
  const grezzo = valore?.trim();
  if (!grezzo) {
    return null;
  }
  return grezzo.startsWith(`${nome}=`) ? grezzo : `${nome}=${grezzo}`;
}

/**
 * Un servizio finto (le implementazioni «mock-…» degli strumenti di sviluppo) dà un recupero che non ha
 * niente a che fare con quello vero: il banco si ferma invece di misurarlo.
 * @param {{ embeddingModel?: string | null, rerankerModel?: string | null }} sonda
 */
export function controllaServizi(sonda) {
  const finti = [
    ['embedding', sonda.embeddingModel],
    ['reranker', sonda.rerankerModel],
  ].filter(([, modello]) => typeof modello === 'string' && modello.trim().toLowerCase().startsWith('mock-'));
  if (finti.length > 0) {
    const err = new Error(
      `Servizi finti sull'API: ${finti.map(([servizio, modello]) => `${servizio} «${modello}»`).join(', ')}. `
      + 'La misura non direbbe nulla del recupero vero: si ferma qui.');
    err.codice = 1;
    throw err;
  }
}

/**
 * L'esito di una domanda dalla risposta della sonda.
 * @param {object} domanda la domanda del set
 * @param {object} sonda la risposta di POST /api/v1/admin/kb/retrieval-probe
 * @param {{ soglia: number, linguaRegolamento: string, completo?: boolean }} opzioni
 */
export function valuta(domanda, sonda, { soglia, linguaRegolamento, completo = false }) {
  const base = descrizione(domanda, linguaRegolamento);
  const contesto = {
    lingua_ricerca: sonda.language,
    fonte_lingua: sonda.languageSource,
    profilo: sonda.profileVersionId,
    reranking: sonda.reranking,
    braccio_vettoriale: sonda.vectorArm,
    candidati_fusione: sonda.fusedCount,
    ...(completo ? { sonda } : {}),
  };

  // La query pgvector è fallita e la ricerca ha proseguito solo lessicale: «non trovata» direbbe una
  // cosa falsa sul recupero vero, quindi è un errore.
  if (sonda.vectorArm === 'Failed') {
    return { ...base, ...contesto, errore: 'braccio vettoriale fallito: la ricerca è stata solo lessicale' };
  }

  const comeChunk = (c) => ({ testo: c.text, documento: c.pdfDocumentId, indice: c.chunkIndex });
  const nelPool = cercaCitazione(domanda.verifica.citazione, sonda.pool.map(comeChunk), { soglia });
  const neiFinali = cercaCitazione(domanda.verifica.citazione, sonda.final.map(comeChunk), { soglia });

  // Per un PDF non inglese i finali possono contenere la traduzione inglese del passaggio giusto, che la
  // citazione (nella lingua del regolamento) non riconosce. Lo stesso PDF è quello dove la citazione sta
  // nel pool, o dove ci arriva più vicino; se non si sa, qualunque PDF.
  const pdf = pdfDelPassaggio(nelPool, sonda.pool);
  const tradotti = neiFinali.trovata
    ? []
    : sonda.final.filter((c) => c.isTranslation === true && (pdf === null || c.pdfDocumentId === pdf));

  return {
    ...base,
    ...contesto,
    pool: { ...sintesi(nelPool, sonda.pool), dimensione: sonda.pool.length },
    finali: { ...sintesi(neiFinali, sonda.final), dimensione: sonda.final.length },
    traduzione_nei_finali: tradotti.length > 0,
    pagine_tradotte_nei_finali: [...new Set(tradotti.map((c) => c.pageNumber))],
  };
}

function descrizione(domanda, linguaRegolamento) {
  return {
    id: domanda.id,
    gioco: domanda.game,
    domanda: domanda.question,
    categoria: categoria(domanda),
    lingua_regolamento: linguaRegolamento,
    citazione: domanda.verifica.citazione,
    pagina_attesa: domanda.verifica.pagina ?? null,
  };
}

/** Il PDF dove la citazione sta nel pool, o dove ci arriva più vicino; null se non ci arriva per niente. */
function pdfDelPassaggio(esito, pool) {
  const posizioni = esito.trovata
    ? esito.frammenti.map((f) => f.posizione)
    : [...esito.frammenti].sort((a, b) => b.migliore.punteggio - a.migliore.punteggio).map((f) => f.migliore.posizione);
  const posizione = posizioni.find((p) => p != null);
  return posizione ? (pool[posizione - 1]?.pdfDocumentId ?? null) : null;
}

function selezione(set, values) {
  if (!Array.isArray(set)) {
    throw new Error('Il set deve essere un array di domande.');
  }
  const giochi = values.gioco?.length ? new Set(values.gioco) : null;
  const id = values.id ? new Set(values.id.split(',').map((s) => s.trim()).filter(Boolean)) : null;
  return set.filter((d) => (!giochi || giochi.has(d.game)) && (!id || id.has(d.id)));
}

async function giochiDallApi(chiama) {
  const risposta = await chiama('/api/v1/admin/kb/vector-stats');
  fermatiSeNonAutorizzato(risposta);
  if (!risposta.ok) {
    throw new Error(`GET /api/v1/admin/kb/vector-stats: HTTP ${risposta.status}. Passa i giochi con --giochi.`);
  }
  const corpo = await risposta.json();
  return (corpo.gameBreakdown ?? []).map((g) => ({ titolo: g.gameName, id: g.gameId }));
}

/**
 * @param {string[]} titoli
 * @param {Record<string, string> | { titolo: string, id: string }[]} giochi
 * @returns {Map<string, string>}
 */
function risolviTitoli(titoli, giochi) {
  if (!Array.isArray(giochi)) {
    const mancanti = titoli.filter((t) => !giochi[t]);
    if (mancanti.length > 0) {
      throw new Error(`La mappa dei giochi non ha: ${mancanti.join(', ')}.`);
    }
    return new Map(titoli.map((t) => [t, giochi[t]]));
  }

  const chiave = (s) => String(s).trim().toLocaleLowerCase('it');
  const mappa = new Map();
  for (const titolo of titoli) {
    const candidati = giochi.filter((g) => chiave(g.titolo) === chiave(titolo));
    if (candidati.length !== 1) {
      const disponibili = giochi.map((g) => g.titolo).sort().join(' · ');
      throw new Error(
        `«${titolo}»: ${candidati.length === 0 ? 'nessun gioco indicizzato con questo titolo' : 'più giochi con questo titolo'}. `
        + `Passa una mappa con --giochi. Giochi indicizzati: ${disponibili}`);
    }
    mappa.set(titolo, candidati[0].id);
  }
  return mappa;
}

/**
 * La lingua del regolamento di ogni gioco: quella della maggioranza delle sue citazioni, su tutto il set.
 * Una citazione sola può non dirlo («fino a due oggetti oppure un oggetto»), il regolamento sì.
 * @returns {Map<string, string>}
 */
function lingueDeiRegolamenti(set) {
  const conte = new Map();
  for (const d of set) {
    const lingua = typeof d?.verifica?.citazione === 'string' ? linguaDelTesto(d.verifica.citazione) : 'sconosciuta';
    if (lingua === 'sconosciuta') continue;
    const delGioco = conte.get(d.game) ?? {};
    delGioco[lingua] = (delGioco[lingua] ?? 0) + 1;
    conte.set(d.game, delGioco);
  }
  const lingue = new Map();
  for (const d of set) {
    const delGioco = Object.entries(conte.get(d.game) ?? {}).sort((a, b) => b[1] - a[1]);
    lingue.set(d.game, delGioco[0]?.[0] ?? 'sconosciuta');
  }
  return lingue;
}

async function misura(domanda, gameId, { chiama, soglia, linguaRegolamento, acceptLanguage, completo }) {
  let risposta;
  try {
    risposta = await chiama('/api/v1/admin/kb/retrieval-probe', {
      method: 'POST',
      headers: { 'content-type': 'application/json' },
      body: JSON.stringify({ gameId, question: domanda.question, acceptLanguage }),
    });
  } catch (errore) {
    return { ...descrizione(domanda, linguaRegolamento), errore: `rete: ${errore.message}` };
  }
  fermatiSeNonAutorizzato(risposta);
  if (!risposta.ok) {
    return {
      ...descrizione(domanda, linguaRegolamento),
      errore: `HTTP ${risposta.status}: ${(await risposta.text()).slice(0, 300)}`,
    };
  }

  const sonda = await risposta.json();
  controllaServizi(sonda);
  return {
    ...valuta(domanda, sonda, { soglia, linguaRegolamento, completo }),
    modello_embedding: sonda.embeddingModel,
    modello_reranker: sonda.rerankerModel ?? null,
  };
}

function categoria(domanda) {
  if (domanda.fuori_dal_corpus === true) {
    return 'fuori_dal_corpus';
  }
  return /out of scope/iu.test(domanda.question_type ?? '') ? 'fuori_ambito' : 'in_ambito';
}

/** L'esito del confronto, con pagina e testo del chunk più vicino per ogni frammento, da rileggere a mano. */
function sintesi(esito, lista) {
  return {
    trovata: esito.trovata,
    parziale: esito.parziale,
    posizione: esito.posizione,
    frammenti: esito.frammenti.map((f) => {
      const vicino = f.migliore.posizione ? lista[f.migliore.posizione - 1] : null;
      return {
        testo: f.testo,
        posizione: f.posizione,
        coppia: f.coppia,
        punteggio: f.punteggio,
        migliore: {
          ...f.migliore,
          pagina: vicino?.pageNumber ?? null,
          traduzione: vicino?.isTranslation ?? null,
          testo: vicino?.text ?? null,
        },
      };
    }),
  };
}

/** I modelli che hanno risposto nella misura: uno solo per servizio, se il servizio non è cambiato a metà. */
function servizi(esiti) {
  const distinti = (campo) => [...new Set(esiti.map((e) => e[campo]).filter((m) => m != null))];
  return { embedding: distinti('modello_embedding'), reranker: distinti('modello_reranker') };
}

/**
 * Il riepilogo per gioco e per lingua del regolamento, con tre categorie di domande. Una domanda con un
 * errore (rete, HTTP, braccio vettoriale fallito) conta fra le domande ma non fra le misurate.
 */
export function riepilogo(esiti) {
  const vuoto = () => ({
    domande: 0,
    nel_pool: 0,
    nei_finali: 0,
    parziali_nel_pool: 0,
    mancate_con_traduzione_nei_finali: 0,
    vettoriale_vuoto: 0,
    errori: 0,
  });
  const aggiungi = (conteggio, e) => {
    conteggio.domande++;
    if (e.errore) {
      conteggio.errori++;
      return;
    }
    if (e.pool.trovata) conteggio.nel_pool++;
    if (e.finali.trovata) conteggio.nei_finali++;
    if (e.pool.parziale) conteggio.parziali_nel_pool++;
    if (e.traduzione_nei_finali) conteggio.mancate_con_traduzione_nei_finali++;
    if (e.braccio_vettoriale === 'Empty') conteggio.vettoriale_vuoto++;
  };

  const perGioco = {};
  const perLingua = {};
  const reranking = {};
  for (const e of esiti) {
    perGioco[e.gioco] ??= {
      lingua_regolamento: e.lingua_regolamento, in_ambito: vuoto(), fuori_ambito: vuoto(), fuori_dal_corpus: vuoto(),
    };
    aggiungi(perGioco[e.gioco][e.categoria], e);

    perLingua[e.lingua_regolamento] ??= { in_ambito: vuoto(), fuori_ambito: vuoto(), fuori_dal_corpus: vuoto() };
    aggiungi(perLingua[e.lingua_regolamento][e.categoria], e);

    if (!e.errore) {
      reranking[e.reranking] = (reranking[e.reranking] ?? 0) + 1;
    }
  }

  return { per_gioco: perGioco, per_lingua_regolamento: perLingua, reranking };
}

function riga(e) {
  if (e.errore) {
    return `${e.id.padEnd(7)} ERRORE ${e.errore}`;
  }
  const dove = (x) => (x.trovata ? `#${x.posizione}` : x.parziale ? 'parziale' : '-');
  const note = [
    e.traduzione_nei_finali ? 'traduzione nei finali' : null,
    e.braccio_vettoriale === 'Empty' ? 'vettoriale vuoto' : null,
  ].filter(Boolean).join(', ');
  return `${e.id.padEnd(7)} pool ${dove(e.pool).padEnd(9)} finali ${dove(e.finali).padEnd(9)} `
    + `${e.reranking} ${e.lingua_ricerca}/${e.fonte_lingua}${note ? ` (${note})` : ''}`;
}

function tabella(r) {
  const righe = [
    'gioco (regolamento)                 categoria          nel pool   nei finali  trad.finali  vett.vuoto  errori',
  ];
  const cella = (c) => {
    const misurate = c.domande - c.errori;
    return `${`${c.nel_pool}/${misurate}`.padEnd(10)} ${`${c.nei_finali}/${misurate}`.padEnd(11)} `
      + `${String(c.mancate_con_traduzione_nei_finali).padEnd(12)} ${String(c.vettoriale_vuoto).padEnd(11)} ${c.errori}`;
  };
  for (const [nome, g] of Object.entries(r.per_gioco)) {
    for (const cat of ['in_ambito', 'fuori_ambito', 'fuori_dal_corpus']) {
      if (g[cat].domande > 0) {
        righe.push(`${`${nome} (${g.lingua_regolamento})`.padEnd(35)} ${cat.padEnd(18)} ${cella(g[cat])}`);
      }
    }
  }
  righe.push('');
  for (const [lingua, l] of Object.entries(r.per_lingua_regolamento)) {
    for (const cat of ['in_ambito', 'fuori_ambito', 'fuori_dal_corpus']) {
      if (l[cat].domande > 0) {
        righe.push(`${`regolamento ${lingua}`.padEnd(35)} ${cat.padEnd(18)} ${cella(l[cat])}`);
      }
    }
  }
  righe.push(
    '',
    'trad.finali: mancate nei finali, con la traduzione inglese di chunk dello stesso PDF nei finali.',
    'vett.vuoto: il braccio vettoriale non ha trovato nessun chunk sopra la soglia di coseno.',
    `reranking: ${Object.entries(r.reranking).map(([k, v]) => `${k} ${v}`).join(', ')}`);
  if (r.reranking.FellBack) {
    righe.push('ATTENZIONE: su alcune domande il reranker non ha lavorato (FellBack): i finali sono i primi della fusione.');
  }
  return righe.join('\n');
}

function fermatiSeNonAutorizzato(risposta) {
  if (risposta.status === 401 || risposta.status === 403) {
    const err = new Error(
      `HTTP ${risposta.status}: la sessione in MEEPLEAI_ADMIN_COOKIE non è valida o non è di un admin.`);
    err.codice = 1;
    throw err;
  }
}

// Gira solo quando è lanciato, non quando i test lo importano.
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().then(
    (codice) => { process.exitCode = codice; },
    (errore) => {
      console.error(errore.message);
      process.exitCode = errore.codice ?? 1;
    },
  );
}
