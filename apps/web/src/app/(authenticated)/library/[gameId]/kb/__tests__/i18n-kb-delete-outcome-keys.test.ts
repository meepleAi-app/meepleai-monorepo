/**
 * #4683 — i messaggi dell'esito di una cancellazione che non cancella.
 *
 * `KbHubContent.test.tsx` risolve `t()` con un dizionario finto, quindi non puo' accorgersi di una
 * chiave sbagliata nei cataloghi reali: la pagina mostrerebbe la chiave grezza. Qui si legge il
 * contenuto vero di `it.json` e `en.json` con lo stesso percorso che costruisce `_content.tsx`
 * (`pages.library.gameDetail.kb.deleteOutcome.<outcome>`).
 */

import { flattenMessages } from '@/locales';
import enMessages from '@/locales/en.json';
import itMessages from '@/locales/it.json';
import type { DeletePdfResult } from '@/lib/api/clients/pdfClient';

const PREFIX = 'pages.library.gameDetail.kb.deleteOutcome.';

// Esaustiva sul tipo: un nuovo esito del backend senza messaggio non compila finche' non si aggiunge qui
// (e quindi nei cataloghi).
const OUTCOMES_WITH_A_MESSAGE: Record<Exclude<DeletePdfResult['outcome'], 'deleted'>, true> = {
  unlinked: true,
  kept: true,
};

describe.each([
  ['it', itMessages],
  ['en', enMessages],
])("messaggi dell'esito della cancellazione (%s)", (_locale, catalog) => {
  const flat = flattenMessages(catalog as Record<string, unknown>);

  it.each(Object.keys(OUTCOMES_WITH_A_MESSAGE))('ha un messaggio per %s', outcome => {
    const message = flat[`${PREFIX}${outcome}`];

    expect(typeof message).toBe('string');
    expect((message as string).trim().length).toBeGreaterThan(0);
  });

  it("distingue i due esiti: uno ha scollegato, l'altro non ha cambiato nulla", () => {
    expect(flat[`${PREFIX}unlinked`]).not.toBe(flat[`${PREFIX}kept`]);
  });

  it("non ha un messaggio per deleted: una cancellazione riuscita non dice niente in piu'", () => {
    expect(flat[`${PREFIX}deleted`]).toBeUndefined();
  });
});
