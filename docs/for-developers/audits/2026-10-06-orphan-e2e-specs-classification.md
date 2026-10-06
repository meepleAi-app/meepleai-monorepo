# Spec Playwright orfane: inventario e classificazione

**Data**: 2026-10-06 · **Issue**: [#4092](https://github.com/meepleAi-app/meepleai-monorepo/issues/4092)

Ventuno spec Playwright vivevano fuori da ogni radice raccolta da una config: non erano skippate né
rosse, erano **assenti**. Questo documento registra cosa sono, perché nessuno se n'era accorto, e in
che stato si trovano ora che vengono eseguite.

## Perché nessuno le vedeva

Le config dichiarano radici tutte sotto `./e2e`; i file stavano sotto `./tests/e2e`,
`./__tests__/e2e` e `./src/__tests__/e2e`. Nessun `testDir` puntava là, e **nessun conteggio lo
rivelava**: «392 file raccolti» non dice quanti file esistono.

```bash
# il comando che lo mostra
node scripts/lint-orphan-specs.mjs
```

Le dieci sotto `./tests/e2e` sono state trovate a mano. **Le altre undici le ha trovate il gate**,
la prima volta che l'ho eseguito — in una seconda radice che l'indagine manuale non aveva guardato,
perché aveva cercato solo dove stava il primo lotto. È la ragione per cui questa issue consegna un
gate e non un elenco: l'elenco era incompleto mentre lo si consegnava come completo.

## Il caso che spiega il danno meglio di qualunque argomento

`rag-001-validation.spec.ts` esisteva in **entrambe** le radici. Entrambe le copie nascono dallo
stesso commit (`bcc9ac348`, 2026-01-30) e **entrambe** sono state modificate il 2026-10-05 da #4059
(`d0824577a`): 3 righe nella copia orfana, 16 in quella raccolta. Erano divergenti con correzioni
complementari, e nel verso peggiore:

| | copia raccolta (eseguita) | copia orfana (mai eseguita) |
|---|---|---|
| sorgente dei conteggi vettoriali | `GET localhost:6333/collections/...` — **Qdrant**, dismesso | `GET /api/v1/admin/kb/vector-stats` — l'API attuale |
| commento #4059 su `NEXT_PUBLIC_API_URL` | versione lunga | versione in una riga |

La copia che girava puntava a un servizio che non esiste più; quella corretta non girava. Adottata
l'orfana, eliminata l'altra.

## Classificazione, misurata

Eseguite contro lo stack locale (`baseURL http://localhost:3000`, `PLAYWRIGHT_SKIP_WEB_SERVER=1`).
Le dieci del primo lotto, **119 test**: `27 passati · 92 falliti`.

| file | passati | falliti |
|---|---|---|
| `chat-page.spec.ts` | 15 | 10 |
| `game-catalog.spec.ts` | 4 | 14 |
| `dashboard.spec.ts` | 4 | 7 |
| `admin/dashboard.spec.ts` | 2 | 14 |
| `admin-reports-email.spec.ts` | 2 | 6 |
| `admin-reports.spec.ts` | 0 | 26 |
| `admin/shared-games/manual-creation.spec.ts` | 0 | 5 |
| `admin/shared-games/agent-workflow.spec.ts` | 0 | 4 |
| `admin/ai-lab/agent-workflow.spec.ts` | 0 | 3 |
| `admin/ai-lab/analytics-workflow.spec.ts` | 0 | 3 |

**Causa dei 92 fallimenti**, raggruppata dai messaggi d'errore:

| n | causa | esempio |
|---|---|---|
| 89 | selettore o locator non trovato | `waiting for locator('text=Create Agent')` |
| 2 | codice della spec rotto | `Cannot read properties of undefined (reading 'click')` |
| 1 | asserzione su contenuto di un'altra pagina | attende `"Show password"` in una lista di giochi |

Nessun fallimento per ambiente, autenticazione o servizio assente. **È deriva di selettori contro
una UI che è cambiata**: le spec sono state scritte fra il 2025-11-30 e il 2026-05-12 e da allora
nessuno le ha eseguite. L'ultimo commit che le ha toccate si chiama «restyle Gaming Hub to
token-first mock fidelity».

## Perché il typecheck non c'entra

Il DoD iniziale della issue chiedeva di far vedere le spec al typechecker «così la prossima deriva
si vede alla build». **Misurato: la premessa è falsa per questi file.** Le ventuno adottate hanno
**zero** errori di tipo e non importano nulla dall'applicazione — solo `@playwright/test` e
`@axe-core/playwright`. La loro deriva vive in selettori e rotte, che sono stringhe: nessun
typechecker la vedrà mai. L'unica rete per quella classe di deriva è **eseguirle**.

Esiste però un debito di tipi reale e separato: **181 errori** nelle spec e2e preesistenti
(`tsc --noEmit` su un tsconfig che includa `e2e/**/*.ts`). Non è questa issue.

## Stato dei gate: adottarle non cambia il colore di nulla

Va detto per non lasciare credere a una copertura che non c'è:

- `test-e2e.yml` è l'unico workflow che esegue la suite Playwright completa. Parte solo su PR verso
  `main`, non è required, e **in 202 run non è mai stato verde** (`gh api .../test-e2e.yml/runs?status=success` → `total_count: 0`).
- `dev-fast.yml` e `dev-async.yml` — i gate dei branch di sviluppo — non eseguono alcun test browser.
- `ci.yml` esegue in browser solo `e2e/smoke.spec.ts` (home, login, redirect) e il gate a11y, che non
  visita nessuna rotta `/admin`.

Quindi i 92 fallimenti non accendono nessun gate oggi. Il valore di questa issue è che da invisibili
sono diventati **visibili e contati**, e che il gate impedisce che il caso si ripeta.

## Cosa resta

1. Riscrivere i selettori dei 92, file per file, partendo dai quattro con più valore per riga:
   `admin-reports.spec.ts` (26), `admin/dashboard.spec.ts` (14), `game-catalog.spec.ts` (14),
   `chat-page.spec.ts` (10).
2. Classificare le **undici** spec trovate dal gate nella seconda radice: adottate ma non ancora
   eseguite, quindi il loro stato è ignoto.
3. I 181 errori di tipo nelle spec e2e preesistenti.
4. Il motivo per cui `test-e2e.yml` non è mai verde: finché resta tale, qualunque investimento in
   E2E è invisibile.
