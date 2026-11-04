/**
 * Unit test per la sonda delle mutazioni.
 *
 * Due proprietà da difendere: che gli endpoint irreversibili vengano saltati
 * SEMPRE, e che un 500 non venga mai scambiato per un esito accettabile.
 */

import { describe, expect, it } from 'vitest';

import { daSaltare, giudica } from '../probe-mutations';

describe('daSaltare', () => {
  it.each([
    '/api/v1/admin/infrastructure/services/api/restart',
    '/api/v1/admin/pdfs/maintenance/purge-stale',
    '/api/v1/admin/storage/migrate',
    '/api/v1/admin/providers/openai/rotate-key',
    '/api/v1/admin/users/bulk/role-change',
    '/api/v1/admin/rag-backup/snapshots',
    '/api/v1/admin/alert-channels/slack/test-connection',
  ])('salta %s', p => {
    expect(daSaltare(p)).not.toBeNull();
  });

  it('non salta una mutazione ordinaria', () => {
    expect(daSaltare('/api/v1/admin/configurations')).toBeNull();
  });

  it('spiega quale parola ha fatto scattare il rifiuto', () => {
    expect(daSaltare('/api/v1/admin/storage/migrate')).toContain('migrate');
  });
});

describe('giudica', () => {
  it('considera difetto qualunque errore server', () => {
    expect(giudica(500, true).esito).toBe('difforme');
    expect(giudica(503, false).esito).toBe('difforme');
    // È il punto della sonda: nessuna richiesta, per quanto malformata,
    // dovrebbe far esplodere il server.
    expect(giudica(500, true).nota).toContain('errore server');
  });

  it('accetta il 404 su risorsa inesistente come comportamento corretto', () => {
    const v = giudica(404, true);
    expect(v.esito).toBe('atteso');
    expect(v.nota).toContain('gestito');
  });

  it('accetta 400 e 422 come prova che la validazione è attiva', () => {
    expect(giudica(400, false).esito).toBe('atteso');
    expect(giudica(422, false).nota).toContain('validazione');
  });

  it('accetta 401 e 403 come prova che l autorizzazione è applicata', () => {
    expect(giudica(403, false).nota).toContain('autorizzazione');
  });

  it('segnala il timeout invece di ignorarlo', () => {
    expect(giudica(0, true)).toEqual({ esito: 'difforme', nota: 'nessuna risposta entro 15s' });
  });
});

describe('daSaltare — percorsi sul soggetto autenticato', () => {
  it('salta DELETE /users/me, che cancellerebbe l account dell audit', () => {
    // Nessuna parola pericolosa nel path, effetto massimamente distruttivo:
    // è il caso che le sole parole chiave non intercettano.
    const motivo = daSaltare('/api/v1/users/me', 'DELETE');
    expect(motivo).toContain('soggetto autenticato');
  });

  it('non blocca le letture o gli aggiornamenti sullo stesso percorso', () => {
    expect(daSaltare('/api/v1/users/me', 'PUT')).toBeNull();
    expect(daSaltare('/api/v1/users/me/preferences', 'POST')).toBeNull();
  });

  it('salta anche le varianti del proprio account', () => {
    expect(daSaltare('/api/v1/users/me/account', 'DELETE')).not.toBeNull();
  });
});
