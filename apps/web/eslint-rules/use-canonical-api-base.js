/**
 * ESLint Custom Rule: use-canonical-api-base
 *
 * Issue #4059 — un valore, tre nomi di variabile, e due di quei nomi non esistono.
 *
 * **Il problema, misurato e non supposto:**
 * `NEXT_PUBLIC_API_BASE` e' il nome canonico ed e' definito in tutti e cinque i compose
 * (dev, test, integration, staging, prod) come build arg. Accanto a quello il codice usava:
 *
 *   - `NEXT_PUBLIC_API_URL` — definita SOLO in quattro workflow E2E di CI, mai in un compose
 *     ne' in un `.env`. Le `NEXT_PUBLIC_*` sono inlineate da `next build`, quindi dove manca
 *     non arriva `undefined` ma la stringa **letterale** `"undefined"`.
 *   - `NEXT_PUBLIC_API_BASE_URL` — definita in nessun file del repo.
 *   - `BACKEND_URL` — definita in nessun file del repo.
 *
 * I difetti che ne sono nati non si assomigliavano, ed e' il motivo per cui questa regola
 * guarda il NOME e non il sintomo:
 *
 *   - `useGameStateSignalR.ts` costruiva `undefined/hubs/gamestate`.
 *   - `AgentChatPanel.tsx` ricadeva su `http://localhost:8080`, cioe' in staging mandava il
 *     browser dell'utente alla sua propria macchina. In sviluppo funzionava per coincidenza.
 *   - `app/api/metrics/bgg-attempt/route.ts` ricadeva su `localhost:8080` **server-side**, dove
 *     dentro il container `web` non ascolta nessuno: il beacon del contatore
 *     `meepleai_bgg_url_attempted_render_total_attempts_total` (SLO=0, P1 di conformita' ToS
 *     #2123) non arrivava mai, e il `catch` della rotta e' muto per costruzione.
 *   - tre altri siti ricadevano su `''`, quindi funzionavano — ma per caso, non per scelta.
 *
 * **La soluzione:** un solo punto di risoluzione.
 *   - `getApiBase()`  per `/api/v1/*`. Nel browser ritorna `''` DI PROPOSITO (#2366): ogni
 *     chiamata passa dal proxy Next, niente CORS, cookie `SameSite=Lax` come same-origin.
 *     Server-side ritorna `API_BASE_URL` (`http://api:8080`).
 *   - `getHubBase()`  per `/hubs/*`. Sempre assoluto, perche' per `/hubs` NON esiste ne' proxy
 *     ne' rewrite: un URL relativo finisce sull'origine del frontend, che risponde 404.
 *
 * **Severita':** `error`. Non ci sono eccezioni legittime nel `src`, e il costo di una
 * quarta occorrenza e' stato misurato tre volte.
 *
 * **Riferimenti:**
 * - apps/web/src/lib/api/core/httpClient.ts (le due funzioni, con la ragione dell'asimmetria)
 * - infra/prometheus/alerts/bgg-tos-compliance.yml (l'alert che il beacon rotto rendeva cieco)
 */

'use strict';

/** Nomi che non risolvono a nulla in un build deployabile. */
const DEAD_ENV_NAMES = new Set(['NEXT_PUBLIC_API_URL', 'NEXT_PUBLIC_API_BASE_URL', 'BACKEND_URL']);

/**
 * Risale un'espressione cercando una chiamata a `getHubBase`.
 *
 * Gli identificatori vengono risolti attraverso lo scope, non assunti validi: senza questo,
 * `` withUrl(`${base}/hubs/x`) `` con un `base` qualunque passerebbe il controllo, e la regola
 * sarebbe piu' debole di quanto dice il suo messaggio. `depth` evita la ricorsione infinita su
 * un alias circolare.
 */
function mentionsGetHubBase(node, scope, depth = 0) {
  if (!node || typeof node !== 'object' || depth > 6) return false;

  if (
    node.type === 'CallExpression' &&
    ((node.callee.type === 'Identifier' && node.callee.name === 'getHubBase') ||
      (node.callee.type === 'MemberExpression' &&
        node.callee.property.type === 'Identifier' &&
        node.callee.property.name === 'getHubBase'))
  ) {
    return true;
  }

  if (node.type === 'TemplateLiteral') {
    return node.expressions.some(e => mentionsGetHubBase(e, scope, depth + 1));
  }
  if (node.type === 'BinaryExpression') {
    return (
      mentionsGetHubBase(node.left, scope, depth + 1) ||
      mentionsGetHubBase(node.right, scope, depth + 1)
    );
  }
  if (node.type === 'Identifier' && scope) {
    // Risolvi l'alias: una sola scrittura, con inizializzatore ispezionabile.
    let current = scope;
    while (current) {
      const variable = current.variables.find(v => v.name === node.name);
      if (variable) {
        if (variable.defs.length !== 1) return false;
        const init = variable.defs[0].node && variable.defs[0].node.init;
        return mentionsGetHubBase(init, scope, depth + 1);
      }
      current = current.upper;
    }
    return false;
  }
  return false;
}

/** Un'espressione che costruisce un path `/hubs/...`? */
function buildsHubPath(node) {
  if (!node || typeof node !== 'object') return false;
  if (node.type === 'Literal') {
    return typeof node.value === 'string' && node.value.includes('/hubs/');
  }
  if (node.type === 'TemplateLiteral') {
    return node.quasis.some(q => q.value.cooked && q.value.cooked.includes('/hubs/'));
  }
  if (node.type === 'BinaryExpression' && node.operator === '+') {
    return buildsHubPath(node.left) || buildsHubPath(node.right);
  }
  return false;
}

module.exports = {
  meta: {
    type: 'problem',
    docs: {
      description:
        'Resolve the API/hub base through getApiBase()/getHubBase(), never through NEXT_PUBLIC_API_URL, NEXT_PUBLIC_API_BASE_URL or BACKEND_URL — names that are defined in no deployable build (#4059).',
      category: 'Possible Errors',
      recommended: true,
    },
    messages: {
      deadEnvVar:
        '`process.env.{{name}}` non e\' definita in nessun build deployabile (#4059): `next build` inlinea la stringa letterale "undefined", oppure si cade su un fallback sbagliato. Usa `getApiBase()` da `@/lib/api/core/httpClient` per `/api/v1/*`, o `getHubBase()` per `/hubs/*`.',
      rawHubUrl:
        "L'URL di un hub SignalR deve passare da `getHubBase()` (#4059). Un URL relativo risolve sull'origine del frontend, dove non esiste proxy per `/hubs` — misurato: 404. Il path corretto e' `/hubs/gamestate`, senza trattino.",
    },
    schema: [],
  },

  create(context) {
    // `context.sourceCode.getScope` e' la forma di ESLint 9; il fallback copre l'API vecchia.
    const getScope = node =>
      context.sourceCode && typeof context.sourceCode.getScope === 'function'
        ? context.sourceCode.getScope(node)
        : context.getScope();

    return {
      // `process.env.NEXT_PUBLIC_API_URL` e compagnia.
      MemberExpression(node) {
        if (
          node.object.type !== 'MemberExpression' ||
          node.object.object.type !== 'Identifier' ||
          node.object.object.name !== 'process' ||
          node.object.property.type !== 'Identifier' ||
          node.object.property.name !== 'env' ||
          node.property.type !== 'Identifier'
        ) {
          return;
        }
        if (DEAD_ENV_NAMES.has(node.property.name)) {
          context.report({
            node,
            messageId: 'deadEnvVar',
            data: { name: node.property.name },
          });
        }
      },

      // `.withUrl('/hubs/...')` o `.withUrl(\`${qualcosa}/hubs/...\`)`.
      CallExpression(node) {
        if (
          node.callee.type !== 'MemberExpression' ||
          node.callee.property.type !== 'Identifier' ||
          node.callee.property.name !== 'withUrl' ||
          node.arguments.length === 0
        ) {
          return;
        }
        const first = node.arguments[0];
        if (!buildsHubPath(first)) return;
        if (!mentionsGetHubBase(first, getScope(node))) {
          context.report({ node: first, messageId: 'rawHubUrl' });
        }
      },

      // Il caso indiretto: `const hubUrl = \`${X}/hubs/gamestate\`` passato poi a withUrl.
      VariableDeclarator(node) {
        if (!node.init || !buildsHubPath(node.init)) return;
        if (!mentionsGetHubBase(node.init, getScope(node))) {
          context.report({ node: node.init, messageId: 'rawHubUrl' });
        }
      },
    };
  },
};
