/**
 * Tests for `local/use-canonical-api-base` (issue #4059).
 *
 * Run with:
 *   node --test apps/web/eslint-rules/use-canonical-api-base.test.js
 *
 * I casi `invalid` sono le righe che esistevano davvero nel repo, non esempi inventati: e'
 * l'unico modo di sapere che la regola avrebbe colto il difetto invece di coglierne uno
 * adiacente.
 */

'use strict';

const { RuleTester } = require('eslint');
const test = require('node:test');
const rule = require('./use-canonical-api-base.js');

const ruleTester = new RuleTester({
  languageOptions: {
    ecmaVersion: 'latest',
    sourceType: 'module',
    parserOptions: {
      ecmaFeatures: { jsx: true },
    },
  },
});

test('use-canonical-api-base', () => {
  ruleTester.run('use-canonical-api-base', rule, {
    valid: [
      // Il nome canonico resta lecito: e' quello che le due funzioni leggono.
      { code: `const b = process.env.NEXT_PUBLIC_API_BASE;` },
      { code: `const b = process.env.API_BASE_URL;` },
      // Altre variabili d'ambiente non c'entrano.
      { code: `const x = process.env.NODE_ENV;` },
      { code: `const x = process.env.NEXT_PUBLIC_ENABLE_PROGRESS_UI;` },
      // `env` che non e' `process.env`.
      { code: `const x = config.env.NEXT_PUBLIC_API_URL;` },
      // Le due forme corrette dei call site reali, dopo la correzione.
      {
        code: `new HubConnectionBuilder().withUrl(\`\${getHubBase()}/hubs/gamestate\`, { withCredentials: true });`,
      },
      {
        code: `const hubUrl = \`\${getHubBase()}/hubs/gamestate\`;\nnew signalR.HubConnectionBuilder().withUrl(hubUrl, { withCredentials: true });`,
      },
      // `withUrl` su qualcosa che non e' un hub: non e' affare di questa regola.
      { code: `client.withUrl('/api/v1/games');` },
      { code: `client.withUrl(\`\${getApiBase()}/api/v1/games\`);` },
      // Concatenazione con l'helper.
      { code: `const u = getHubBase() + '/hubs/gamestate';` },
    ],
    invalid: [
      // --- I tre nomi morti, nelle forme in cui esistevano nel repo ---
      {
        // useGameStateSignalR.ts: nessun fallback, quindi la stringa letterale "undefined".
        //
        // DUE errori su una riga, e sono davvero due difetti distinti: la base non risolve
        // (`deadEnvVar`) **e** l'URL dell'hub non passa dall'helper (`rawHubUrl`). La prima
        // stesura di questo test ne attendeva uno solo; aveva torto il test, non la regola.
        // L'ordine e' quello delle colonne: il template literal apre a 16, `process.env` a 19.
        code: `const hubUrl = \`\${process.env.NEXT_PUBLIC_API_URL}/hubs/gamestate\`;`,
        errors: [
          { messageId: 'rawHubUrl' },
          { messageId: 'deadEnvVar', data: { name: 'NEXT_PUBLIC_API_URL' } },
        ],
      },
      {
        // AgentChatPanel.tsx: il fallback assoluto a localhost, rotto in staging.
        code: `const u = \`\${process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080'}/api/v1/agents/qa/stream\`;`,
        errors: [{ messageId: 'deadEnvVar' }],
      },
      {
        // guest-session-view.tsx / use-sse-queue.ts: il fallback a stringa vuota.
        code: `const baseUrl = process.env.NEXT_PUBLIC_API_URL || '';`,
        errors: [{ messageId: 'deadEnvVar' }],
      },
      {
        // setup-account/_content.tsx
        code: `const b = process.env.NEXT_PUBLIC_API_BASE_URL ?? '';`,
        errors: [{ messageId: 'deadEnvVar', data: { name: 'NEXT_PUBLIC_API_BASE_URL' } }],
      },
      {
        // bgg-attempt/route.ts: due nomi morti nella stessa espressione -> due errori.
        code: `const backend = process.env.BACKEND_URL ?? process.env.NEXT_PUBLIC_API_BASE_URL ?? 'http://localhost:8080';`,
        errors: [{ messageId: 'deadEnvVar' }, { messageId: 'deadEnvVar' }],
      },

      // --- L'URL dell'hub ---
      {
        // useSignalrSession.ts, la riga esatta che c'era: relativa e col path sbagliato.
        code: `new HubConnectionBuilder().withUrl('/hubs/game-state').build();`,
        errors: [{ messageId: 'rawHubUrl' }],
      },
      {
        // Anche il path GIUSTO, se relativo, resta sbagliato: non esiste proxy per /hubs.
        code: `new HubConnectionBuilder().withUrl('/hubs/gamestate').build();`,
        errors: [{ messageId: 'rawHubUrl' }],
      },
      {
        // Assoluto ma hardcoded: passa il browser di staging alla macchina dell'utente.
        code: `new HubConnectionBuilder().withUrl('http://localhost:8080/hubs/gamestate').build();`,
        errors: [{ messageId: 'rawHubUrl' }],
      },
      {
        // Il buco che la risoluzione nello scope chiude: un alias che NON e' getHubBase.
        // Senza quella risoluzione questo caso passerebbe, e la regola sarebbe piu' debole
        // del suo messaggio.
        code: `const base = 'http://localhost:8080';\nconst u = \`\${base}/hubs/gamestate\`;`,
        errors: [{ messageId: 'rawHubUrl' }],
      },
      {
        // Un alias di getApiBase() non basta: nel browser ritorna '' e l'URL torna relativo.
        code: `const u = \`\${getApiBase()}/hubs/gamestate\`;`,
        errors: [{ messageId: 'rawHubUrl' }],
      },
    ],
  });
});
