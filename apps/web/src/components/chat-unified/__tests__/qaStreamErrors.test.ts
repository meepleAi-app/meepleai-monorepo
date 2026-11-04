/**
 * #4388 — il rifiuto 429 delle rotte di risposta.
 *
 * Il backend limita a due le risposte aperte per utente: la terza domanda riceve 429 prima che il
 * gateway sia chiamato. Le viste devono dirlo in italiano e non ritentare (un nuovo tentativo
 * arriverebbe mentre le altre due sono ancora in corso), non mostrare «HTTP 429» né il guasto
 * generico.
 */

import { describe, expect, it } from 'vitest';

import {
  QA_TOO_MANY_REQUESTS_MESSAGE,
  QaTooManyRequestsError,
  qaRequestFailureMessage,
  qaStreamErrorMessage,
  qaStreamKnownErrorMessage,
  throwIfQaTooManyRequests,
} from '../qaStreamErrors';

describe('QA_TOO_MANY_REQUESTS_MESSAGE', () => {
  it('says to wait for an answer in progress, in Italian', () => {
    expect(QA_TOO_MANY_REQUESTS_MESSAGE).toBe(
      'Hai già due risposte in corso. Aspetta che una finisca, poi riprova.'
    );
  });
});

describe('throwIfQaTooManyRequests', () => {
  it('throws the error that carries the message, for a 429', () => {
    expect(() => throwIfQaTooManyRequests({ status: 429 })).toThrow(QaTooManyRequestsError);
    expect(() => throwIfQaTooManyRequests({ status: 429 })).toThrow(QA_TOO_MANY_REQUESTS_MESSAGE);
  });

  it.each([200, 401, 403, 404, 500, 503])(
    'lets a %i through to the caller own handling',
    status => {
      expect(() => throwIfQaTooManyRequests({ status })).not.toThrow();
    }
  );
});

describe('qaRequestFailureMessage', () => {
  it('gives the message of the 429 when that is what failed', () => {
    expect(qaRequestFailureMessage(new QaTooManyRequestsError(), 'Errore generico')).toBe(
      QA_TOO_MANY_REQUESTS_MESSAGE
    );
  });

  it.each([
    ['another error', new Error('QA stream failed: HTTP 500')],
    ['a network failure', new TypeError('Failed to fetch')],
    ['something that is not an error', 'boom'],
    ['nothing', undefined],
  ])('gives the fallback for %s', (_label, failure) => {
    expect(qaRequestFailureMessage(failure, 'Errore generico')).toBe('Errore generico');
  });
});

describe('qaStreamKnownErrorMessage (#4332)', () => {
  it('gives the Italian sentence of a code we know', () => {
    expect(qaStreamKnownErrorMessage('AI_PROVIDER_UNAVAILABLE')).toBe(
      'Il servizio AI è temporaneamente non disponibile. Riprova tra qualche minuto.'
    );
  });

  // Il catch-all degli endpoint di risposta manda `An error occurred: {ex.Message}` con questo codice:
  // il testo è il messaggio di un'eccezione interna, non una frase per l'utente.
  it.each(['INTERNAL_ERROR', 'SOMETHING_NEW', '', undefined])(
    'gives nothing for %s, instead of falling back on the backend text',
    code => {
      expect(qaStreamKnownErrorMessage(code)).toBeUndefined();
    }
  );

  it('does not mistake an inherited object key for a code', () => {
    expect(qaStreamKnownErrorMessage('constructor')).toBeUndefined();
    expect(qaStreamKnownErrorMessage('toString')).toBeUndefined();
  });

  it('leaves qaStreamErrorMessage as it was: backend text for an unknown code', () => {
    expect(qaStreamErrorMessage('INTERNAL_ERROR', 'An error occurred: boom')).toBe(
      'An error occurred: boom'
    );
    expect(qaStreamErrorMessage('INTERNAL_ERROR')).toBe('Errore nella risposta AI');
  });
});
