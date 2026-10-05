/**
 * getHubBase Tests — Issue #4059
 *
 * `getHubBase()` esiste solo per una ragione: `getApiBase()` **non e' usabile per gli hub**, e
 * la differenza non e' deducibile dai nomi. Il test che conta e' quindi quello sull'asimmetria,
 * non quello sul valore: se un refactoring «semplificasse» facendo delegare `getHubBase` a
 * `getApiBase`, nel browser l'URL dell'hub tornerebbe relativo e la sessione live perderebbe gli
 * eventi in silenzio — il difetto da cui questa funzione nasce.
 *
 * Il meccanismo, misurato prima di scriverla:
 *   POST :3000/hubs/gamestate/negotiate -> 404 (nessun proxy per /hubs sull'origine frontend)
 *   POST :8080/hubs/gamestate/negotiate -> 200 con connectionToken
 */

import { getApiBase, getHubBase } from '../core/httpClient';

describe('getHubBase (#4059)', () => {
  const originalEnv = process.env;
  const hadWindow = 'window' in globalThis;

  beforeEach(() => {
    process.env = { ...originalEnv };
  });

  afterEach(() => {
    process.env = originalEnv;
    if (!hadWindow) {
      // @ts-expect-error — ripulisce il window simulato dai test che lo creano
      delete globalThis.window;
    }
  });

  it("e' ASSOLUTO nel browser, dove getApiBase e' vuoto: e' il motivo per cui esiste", () => {
    global.window = {} as unknown as Window & typeof globalThis;
    process.env.NODE_ENV = 'development';
    process.env.NEXT_PUBLIC_API_BASE = 'http://localhost:8080';

    // L'asimmetria, in una riga: lo stesso ambiente, due risposte diverse, entrambe volute.
    expect(getApiBase()).toBe('');
    expect(getHubBase()).toBe('http://localhost:8080');
  });

  it('legge NEXT_PUBLIC_API_BASE, il solo nome definito nei build deployabili', () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.test';
    expect(getHubBase()).toBe('https://api.example.test');
  });

  it("non legge API_BASE_URL, che e' un hostname della rete Docker e non esiste per il browser", () => {
    delete process.env.NEXT_PUBLIC_API_BASE;
    process.env.API_BASE_URL = 'http://api:8080';
    // `getApiBase()` server-side preferisce API_BASE_URL; `getHubBase()` deve ignorarlo, perche'
    // il valore che serve e' quello raggiungibile dal browser.
    expect(getHubBase()).toBe('http://localhost:8080');
  });

  it("toglie gli slash finali, cosi' `${getHubBase()}/hubs/gamestate` non raddoppia lo slash", () => {
    process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.test///';
    expect(getHubBase()).toBe('https://api.example.test');
    expect(`${getHubBase()}/hubs/gamestate`).toBe('https://api.example.test/hubs/gamestate');
  });

  it('tratta le stringhe letterali "undefined" e "null" come assenza', () => {
    // Non e' un caso teorico: una `NEXT_PUBLIC_*` non passata come build arg viene inlineata
    // da `next build` esattamente come la stringa "undefined". E' il difetto che
    // `useGameStateSignalR.ts` aveva, con `undefined/hubs/gamestate`.
    for (const value of ['undefined', 'null', '   ']) {
      process.env.NEXT_PUBLIC_API_BASE = value;
      expect(getHubBase()).toBe('http://localhost:8080');
    }
  });

  it("non ritorna mai la stringa vuota: un URL relativo per un hub e' sempre sbagliato", () => {
    for (const value of [undefined, '', 'undefined']) {
      if (value === undefined) {
        delete process.env.NEXT_PUBLIC_API_BASE;
      } else {
        process.env.NEXT_PUBLIC_API_BASE = value;
      }
      expect(getHubBase()).not.toBe('');
      expect(getHubBase()).toMatch(/^https?:\/\//);
    }
  });
});
