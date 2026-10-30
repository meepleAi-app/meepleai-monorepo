/**
 * #4754 — il fuso del browser che il wizard manda con la serata.
 *
 * Il server rifiuta con 422 un fuso dalla forma sbagliata: questo helper non deve mai essere il motivo per cui
 * una serata non si crea. Se il browser non sa dire un fuso, o ne dice uno che non sembra un id IANA, il
 * campo resta fuori.
 */

import { afterEach, describe, expect, it, vi } from 'vitest';

import { getBrowserTimeZone } from '../browserTimeZone';

function browserReports(timeZone: unknown): void {
  vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(
    () => ({ resolvedOptions: () => ({ timeZone }) }) as unknown as Intl.DateTimeFormat
  );
}

describe('getBrowserTimeZone (#4754)', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it.each([
    'Europe/Rome',
    'America/Argentina/Buenos_Aires',
    'America/Port-au-Prince',
    'Etc/GMT+5',
    'UTC',
  ])("restituisce l'id IANA %s così com'è", zone => {
    browserReports(zone);
    expect(getBrowserTimeZone()).toBe(zone);
  });

  it.each([
    ['vuoto', ''],
    ['un offset', '+01:00'],
    ['con uno spazio', 'Europe Rome'],
    ['con uno slash iniziale', '/Europe/Rome'],
    ['con più livelli di quanti ne abbia un id IANA', 'a/b/c/d/e'],
    ['più lungo della colonna', `Europe/${'a'.repeat(60)}`],
  ])('lascia fuori il campo se il browser dice un fuso %s', (_label, zone) => {
    browserReports(zone);
    expect(getBrowserTimeZone()).toBeUndefined();
  });

  it('lascia fuori il campo se il browser non dà una stringa', () => {
    browserReports(undefined);
    expect(getBrowserTimeZone()).toBeUndefined();
  });

  it('lascia fuori il campo, senza lanciare, se Intl non è disponibile', () => {
    vi.spyOn(Intl, 'DateTimeFormat').mockImplementation(() => {
      throw new RangeError('Intl non disponibile');
    });
    expect(() => getBrowserTimeZone()).not.toThrow();
    expect(getBrowserTimeZone()).toBeUndefined();
  });

  it('con Intl vero restituisce un id che il server accetterebbe, oppure nulla', () => {
    const zone = getBrowserTimeZone();
    if (zone !== undefined) {
      expect(zone).toMatch(/^[A-Za-z][A-Za-z0-9_+-]*(\/[A-Za-z0-9_+-]+){0,3}$/);
    }
  });
});
