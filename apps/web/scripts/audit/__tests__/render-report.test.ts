/**
 * Unit test per la classificazione degli esiti e l'aggiornamento del tracker.
 *
 * La classificazione decide cosa diventa finding: sbagliarla in senso permissivo
 * produce un audit verde che non ha guardato nulla, in senso restrittivo un
 * report pieno di rumore che nessuno leggerà.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { describe, expect, it } from 'vitest';

import {
  applyStatuses,
  classify,
  evidenceOf,
  renderMarkdown,
  type CrawlEntry,
} from '../render-report';

const entry = (over: Partial<CrawlEntry> = {}): CrawlEntry => ({
  id: 'abc12345',
  route: '/library',
  role: 'user',
  status: 200,
  consoleErrors: [],
  failedRequests: [],
  bodyMarkers: [],
  ...over,
});

describe('classify', () => {
  it('ok quando tutto è pulito', () => {
    expect(classify(entry())).toBe('ok');
  });

  it('rotto quando la navigazione non è andata a buon fine', () => {
    expect(classify(entry({ status: 500 }))).toBe('rotto');
  });

  it('rotto quando la navigazione non ha prodotto alcuna risposta', () => {
    expect(classify(entry({ status: 0 }))).toBe('rotto');
  });

  it('rotto quando il corpo mostra un marker di guasto, anche con HTTP 200', () => {
    // Un error boundary React risponde 200: senza questo controllo una pagina
    // completamente rotta risulterebbe sana.
    expect(classify(entry({ bodyMarkers: ['/404/'] }))).toBe('rotto');
  });

  it('sospetto quando ci sono errori di console ma la pagina risponde', () => {
    expect(classify(entry({ consoleErrors: ['TypeError'] }))).toBe('sospetto');
  });

  it('sospetto quando una richiesta secondaria fallisce', () => {
    expect(classify(entry({ failedRequests: ['500 /api/v1/games'] }))).toBe('sospetto');
  });
});

describe('applyStatuses', () => {
  const csv =
    'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note\n' +
    'abc12345,route,/library,GET,UserLibrary,user,L1,⬜ non coperto,,\n';

  it('promuove a verificato le righe ok', () => {
    expect(applyStatuses(csv, [entry()])).toContain('✅ verificato');
  });

  it('marca le righe rotte come finding da aprire', () => {
    expect(applyStatuses(csv, [entry({ status: 500 })])).toContain('⚠️ finding da aprire');
  });

  it('lascia intatte le righe senza riscontro nel crawl', () => {
    expect(applyStatuses(csv, [])).toContain('⬜ non coperto');
  });

  it('preserva il numero di colonne delle righe che aggiorna', () => {
    const updated = applyStatuses(csv, [entry()]).trim().split('\n')[1];
    expect(updated.split(',')).toHaveLength(10);
  });
});

describe('renderMarkdown', () => {
  it('raggruppa per esito e conta', () => {
    const md = renderMarkdown([entry(), entry({ id: 'z', status: 500 })]);
    expect(md).toContain('rotto: 1');
    expect(md).toContain('ok: 1');
  });

  it('elenca i problemi e non le pagine sane', () => {
    const md = renderMarkdown([entry({ route: '/sana' }), entry({ route: '/rotta', status: 503 })]);
    expect(md).toContain('/rotta');
    expect(md).not.toContain('/sana');
  });
});

describe('evidenceOf', () => {
  it('riassume il segnale che ha determinato il verdetto', () => {
    const e = entry({ status: 200, bodyMarkers: ['not-found'], failedRequests: ['404 /api/v1/x'] });
    const ev = evidenceOf(e);
    expect(ev).toContain('HTTP 200');
    expect(ev).toContain('not-found');
    expect(ev).toContain('404 /api/v1/x');
  });

  it('neutralizza le virgole per non sfasare le colonne del CSV', () => {
    expect(evidenceOf(entry({ consoleErrors: ['a, b, c'] }))).not.toContain(',');
  });

  it('scrive l evidenza nel tracker, non solo lo stato', () => {
    // Una riga marcata "da triagare" con la colonna del motivo vuota costringe
    // a rieseguire tutto per sapere cosa era stato osservato.
    const csv =
      'id,tipo,path,metodo,contesto,ruolo,livello,stato,evidenza,note\n' +
      'abc12345,route,/x,GET,X,user,L1,⬜ non coperto,,\n';
    const out = applyStatuses(csv, [entry({ consoleErrors: ['TypeError'] })]);
    expect(out.split('\n')[1].split(',')[8]).toContain('HTTP');
  });
});
