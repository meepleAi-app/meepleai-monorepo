/**
 * Unit test per il giudizio sugli endpoint di sola lettura.
 *
 * Il criterio decide cosa diventa finding: troppo permissivo e un endpoint
 * aperto a chiunque passa inosservato; troppo severo e il report si riempie di
 * 404 legittimi.
 */

import { describe, expect, it } from 'vitest';

import { fillPath, judge } from '../probe-reads';

describe('judge — endpoint riservati agli amministratori', () => {
  it('conforme quando admin legge e utente semplice è respinto', () => {
    expect(judge(200, 403, 'admin').esito).toBe('atteso');
    expect(judge(204, 401, 'admin').esito).toBe('atteso');
  });

  it('difforme quando un endpoint admin NON respinge l utente', () => {
    // È il caso che conta davvero: un endpoint riservato leggibile da chiunque.
    const v = judge(200, 200, 'admin');
    expect(v.esito).toBe('difforme');
    expect(v.nota).toContain('non respinge');
  });

  it('tollera il 404 lato admin ma lo annota', () => {
    const v = judge(404, 403, 'admin');
    expect(v.esito).toBe('atteso');
    expect(v.nota).toContain('risorsa assente');
  });

  it('difforme quando admin riceve un errore server', () => {
    expect(judge(500, 403, 'admin').esito).toBe('difforme');
  });
});

describe('judge — endpoint self-service', () => {
  it('conforme quando l utente accede al proprio dato', () => {
    // /users/me/quota letto dal titolare è il funzionamento previsto: giudicarlo
    // con il criterio degli endpoint admin produrrebbe decine di falsi positivi.
    const v = judge(200, 200, 'user');
    expect(v.esito).toBe('atteso');
    expect(v.nota).toContain('self-service');
  });

  it('difforme quando l utente viene respinto dal proprio dato', () => {
    const v = judge(200, 403, 'user');
    expect(v.esito).toBe('difforme');
    expect(v.nota).toContain("destinato all'utente");
  });

  it('tollera il 404 su risorsa self-service assente', () => {
    expect(judge(404, 404, 'user').esito).toBe('atteso');
  });
});

describe('fillPath', () => {
  const values = { userId: 'U1', id: 'X9' };

  it('sostituisce i segmenti tipizzati', () => {
    expect(fillPath('/api/v1/admin/users/{userId:guid}', values)).toBe('/api/v1/admin/users/U1');
  });

  it('sostituisce anche i segmenti senza tipo', () => {
    expect(fillPath('/api/v1/admin/users/{id}/roles', values)).toBe('/api/v1/admin/users/X9/roles');
  });

  it('ritorna null quando un parametro non è noto, invece di inventarlo', () => {
    expect(fillPath('/api/v1/admin/{sconosciuto:guid}', values)).toBeNull();
  });

  it('lascia intatti i path senza parametri', () => {
    expect(fillPath('/api/v1/admin/users', values)).toBe('/api/v1/admin/users');
  });
});
