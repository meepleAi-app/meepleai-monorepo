# Mockup↔Live Compare Tool (#2999)

Dev-tool locale per la review **manuale** di fedeltà mockup↔implementazione live.
Genera una gallery HTML statica che affianca lo screenshot del page-mock e quello
della route live reale, con slider drag-to-reveal.

**NON è un gate CI** (nessuna baseline, nessun pixel-diff → immune al problema
baseline win32↔linux che ha descopato la suite pixel Storybook, #2063).

## Uso

```bash
cd apps/web
pnpm compare:mockups        # capture + genera gallery.html (stampa il path)
pnpm compare:mockups:open   # idem + apre nel browser
```

Output (gitignored): `apps/web/mockup-compare-output/gallery.html` + PNG.

### 🔴 Prerequisito: server con auth-bypass

La route live si cattura solo se il server Next gira con
`PLAYWRIGHT_AUTH_BYPASS=true` (attivo solo in dev). Il config **avvia lui** un
dev server con il bypass **se la :3000 è libera**. Se la :3000 è già occupata da
un server SENZA bypass (tipico: lo stack Docker `make dev`), `reuseExistingServer`
lo riusa e il gate SSR di `proxy.ts` **reindirizza a `/login`** → il capture live
mostra la pagina di login invece della route.

In quel caso punta a un dev server con bypass su una porta libera:

```bash
# in un terminale: dev server con bypass su 3100
PLAYWRIGHT_AUTH_BYPASS=true pnpm exec next dev -p 3100
# in un altro: capture contro quella porta
COMPARE_APP_PORT=3100 pnpm compare:mockups
```

## Aggiungere una coppia

Aggiungi una riga a `e2e/mockup-compare/manifest.ts`:

- `mockupHtml`: nome file in `admin-mockups/design_files/`.
- `route`: path live.
- `auth`: `'user'` | `'admin'`.
- `mock` (opzionale): `page.route` per popolare i dati — **glob host-agnostici**
  `**/api/v1/...` (l'app usa URL relativi nel browser), MAI `localhost:8080` —
  **oppure** usa il seam built-in `?fixture=default` dove supportato (es.
  `useLibrary`).

## Come funziona

`manifest.ts` → `capture.spec.ts` (screenshot mockup via http-server :5175 +
route live via app :3000 auth-bypass + mock) → `captures.json` →
`scripts/mockup-compare/generate.mjs` → `gallery.html`.

Auth della route live: `seedMockRoleCookies` (gate SSR di `proxy.ts`) +
`mockAuthEndpoints` (client `/auth/me` + `/auth/session/status`), entrambi da
`e2e/_helpers/seedAuthSession`.

## Note / limiti

- **Il mockup capture richiede rete** a `unpkg.com`: molti page-mock caricano
  React/ReactDOM/Babel da CDN e transpilano JSX in-browser. La capture attende
  il mount reale (`waitForFunction`); se unpkg è irraggiungibile la gallery
  mostra un placeholder "mockup capture failed" invece di uno screenshot vuoto.
- Alcuni page-mock `.html` sono standalone e possono divergere dalla sorgente
  che l'implementazione ha effettivamente tracciato (es. `sp4-library-wishlist`
  cita un `-ui.jsx`); un po' di drift è atteso e va giudicato manualmente.
- **Non è un gate CI**: la cattura è locale, l'output gitignored, il giudizio
  di fedeltà è umano.
