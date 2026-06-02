# Mobile Golden Path Audit — 2026-06-02

> **Scope**: smoke test mobile (390×844) sulle 3 user stories più comuni:
> 1. Gestione libreria (`/library`)
> 2. Upload PDF (`/library/[gameId]/kb`)
> 3. Chat con agente post-upload (`/chat/[threadId]` con `?gameId=…`)
>
> **Environment**: staging `https://meepleai.app` (post-deploy `8b05b39` + hotfix `7c239973b`).
> **Auth**: `admin@meepleai.app` (added to staging allowlist for the session).
> **Tool**: Playwright MCP browser + a11y snapshot + screenshot @ 390×844 (Pixel 5 emulation).
> **Game**: Catan (catalog id `cc1678e8-f460-4b53-81f6-6d6539f82b65`).

## Top-level outcome

- **2 P0 bug fixed in-session** (#1811 2FA login schema mismatch — shipped PRs #1812 / #1813).
- **1 P0 bug discovered**: chat SSE response timeout on mobile after send (no answer, no error banner).
- **6 P2/P3 UX bugs documented** below (i18n mix, double headings, testid coverage, semantic titles).
- **Mobile layout structurally OK** for all 3 US: no horizontal page overflow, hamburger + bottom-bar pattern works, MeepleCard responsive.

## Findings — sorted by severity

### 🚨 P0 — 2FA login schema mismatch (RESOLVED in-session)

**Status**: ✅ shipped to staging in deploy `7c239973b` (2026-06-02 15:19 UTC). Verified live on `/login` 390×844: pre-fix → "Accesso fallito. Riprova."; post-fix → 2FA prompt appears as expected.

- **Issue**: [#1811](https://github.com/meepleAi-app/meepleai-monorepo/issues/1811)
- **Root cause**: BE serialized `sessionToken`, FE Zod schema accepted only `tempSessionToken`.
- **Fix PR (main-dev)**: [#1812](https://github.com/meepleAi-app/meepleai-monorepo/pull/1812)
- **Release PR (main-staging)**: [#1813](https://github.com/meepleAi-app/meepleai-monorepo/pull/1813)
- **Regression test**: `apps/web/src/lib/api/schemas/__tests__/auth.schemas.test.ts` (6 tests).

### 🚨 P0 — Chat SSE risposta agente non arriva (`/chat/[threadId]?gameId=…`)

**Status**: 🔴 open — to be opened as follow-up issue.

- **Repro**: as `admin@meepleai.app` su `/library/<catanId>` → "Chat con Agente" → select "Auto" → "Inizia Chat" → invio "Qual è il numero minimo di giocatori per Catan?" → attendere 27s.
- **Behavior**: messaggio utente appare nel chat surface (msgCount=2), MA risposta agente **mai arriva**, NESSUN spinner/thinking indicator, NESSUN error alert, console pulita (solo CSP manifest noise).
- **Possible causes**:
  1. Agent "Auto" potrebbe non avere un'implementazione concreta — solo placeholder UI.
  2. Game Catan non ha agent configurato → backend silenziosamente non genera risposta.
  3. SSE stream non viene aperto da mobile (no error feedback).
  4. KB Coverage Stats "0 Documenti" anche se `rulebook-catan` 244 KB è listato — possibile inconsistenza index → niente RAG context → no response.
- **Impact**: bloccante per la US "chat con agente post-PDF". Senza feedback (loader/error) l'utente non capisce che è bloccato.
- **Action**: aprire issue P1 con repro + log network.

### 🚨 P0 — STAGING_ACCESS_DENIED silent in FE (no error shown)

**Status**: 🔴 open — to be opened as follow-up issue.

- **Repro**: login con utente non in `staging_allowlist`.
- **BE response** (`POST /api/v1/auth/login`): 403 con body `{ code: "STAGING_ACCESS_DENIED", message: "Staging access by invite only — contact …", contactEmail: "…" }`.
- **FE behavior**: form login resta nello stato submitting, **nessun alert** mostrato all'utente, **nessun feedback visivo** (form retorna semplicemente al normale state). Utente bloccato senza capire perché.
- **Root cause likely**: `apps/web/src/lib/api/clients/httpClient` cattura la 403 ma o non passa il body parseato a `setError(err.message)` (`_content.tsx:93-97`) oppure il branch sul Zod parse fail silently drop the message field.
- **Reference**: `apps/api/src/Api/BoundedContexts/Authentication/Infrastructure/Middleware/StagingAccessMiddleware.cs:74-89` (comment `// Embedded in user-facing message so existing frontend error handler (login _content.tsx:93 setError(err.message)) displays it without code-specific branching` is wrong — the FE does NOT show it).
- **Severity**: P0 staging-only UX block; might also affect prod if equivalent middleware is enabled there.

### ⚠️ P2 — CSP blocks PWA manifest from Cloudflare Access

**Status**: 🟡 open.

- **Symptom**: 2 console error su login + ogni page authenticated:
  ```
  Loading a manifest from 'https://meepleai-staging.cloudflareaccess.com/cdn-cgi/access/login/meepleai.app?...&redirect_url=%2Fmanifest.json'
  violates the following Content Security Policy directive: "default-src 'self'".
  Note that 'manifest-src' was not explicitly set, so 'default-src' is used as a fallback.
  The action has been blocked.
  ```
- **Impact**: PWA manifest non caricato → "Add to Home Screen" iOS/Android rotto, app icon e theme color non applicati. Funzionalità app intatta.
- **Root cause likely**: il browser ha una sessione CF Access scaduta e prova a caricare `/manifest.json` via Cloudflare Access proxy, che restituisce un redirect a `cloudflareaccess.com`. CSP `default-src 'self'` blocca il sub-domain CF.
- **Fix proposal**: aggiungere `manifest-src 'self' https://*.cloudflareaccess.com` al CSP header oppure deduplicare la richiesta manifest quando la session è scaduta.
- **Action**: issue separata P2.

### ⚠️ P2 — `data-testid="message-input"` solo su desktop textarea (E2E mobile broken)

**Status**: 🔴 open.

- **Repro**: ispeziona DOM su `/chat/[threadId]` a 390×844 → presenti **2 textarea**:
  - `[0]` visible (mobile, 303×42 a y=726), **no `data-testid`**
  - `[1]` `data-testid="message-input"` ma `width=0 height=0` (display nascosto a mobile)
- **Impact**: E2E tests con `getByTestId('message-input')` falliscono su `mobile-chrome` project (Playwright). Lo `alpha-happy-path.spec.ts` probabilmente non testa chat-send su mobile per questo (verificare).
- **Fix**: aggiungere `data-testid="message-input-mobile"` (o stessa testid duplicata sul mobile textarea — Playwright può usare `.locator('[data-testid=message-input]:visible')`).
- **Action**: issue separata P2 + lo si fix nello stesso PR del E2E spec B1.

### 🌐 P3 — 2FA prompt i18n/a11y mix (post-#1811 fix)

**Status**: 🟡 open.

- **Symptom** on `/login` 2FA step (post-credentials submit):
  - h1: "Autenticazione a due fattori" (Italian)
  - h2: "Two-Factor Authentication" (English)
  - Paragraph 1 italiano, paragraph 2 english
  - Button label: **"Verify"** (English — should be "Verifica")
  - Button "Annulla" correctly localized
- **Impact**: UX bilingue inconsistente; screen reader legge 2 heading duplicati.
- **Root cause likely**: `TwoFactorVerification` component hardcoda h2 + paragraph + button labels in inglese, mentre l'AuthCard wrapper fornisce h1 + subtitle localizzati.
- **Action**: issue P3.

### 🌐 P3 — AddGameDrawer interamente in inglese

**Status**: 🟡 open.

- **Repro**: `/library?action=add` apre dialog "Add a game" (h2 EN) con:
  - Title "Add a game" (EN)
  - Paragraph "How do you want to add your game?" (EN)
  - Button 1 "Add manually" + description (EN)
  - Button 2 "From shared catalog" + description (EN)
- **Action**: issue P3 — `AddGameDrawer` component needs i18n keys.

### 🌐 P3 — Catalog drawer pagination i18n mix

**Status**: 🟡 open.

- **Repro**: in `Add from catalog` drawer mobile, footer pagination:
  - Italian: "Pagina 1 di 18 • 158 risultati"
  - English: aria-label "First page" / "Previous page" / "Next page" / "Last page" / "Page 1" / "Page 2" …
- **Impact**: screen reader Italian announce "Page 1, button" instead of "Pagina 1, pulsante".
- **Action**: issue P3.

### 🏷️ P2 — Semantic title `/library/[gameId]` h1 dice "Gioco" generico

**Status**: 🟡 open.

- **Repro**: navigate to `/library/<gameId>` o `/library/<gameId>/kb` → h1 sempre "Gioco" letterale invece del game name (es. "Catan").
- **Page title** invece: "MeepleAI - AI-Powered Board Game Rules Assistant" (sempre lo stesso, dovrebbe includere game name).
- **Impact**: a11y (screen reader user non capisce su quale gioco è); SEO (page title generico non differenzia pages); UX (mobile breadcrumb mostra solo "Libreria" → "Gioco" senza differenziare).
- **Action**: issue P2 — `LibraryGameView` h1 dovrebbe leggere il game name dal context.

### 📊 P3 — KB Coverage Stats inconsistenza count

**Status**: 🟡 open.

- **Repro**: `/library/<gameId>/kb` mostra contemporaneamente:
  - Documento `rulebook-catan` 244.1 KB · 28/05/2026 (presente in lista)
  - "📊 KB Coverage Stats — 📄 0 Documenti · Copertura: Nessuna · 0%"
- **Impact**: confonde l'utente; suggerisce che il PDF non è indicizzato (potrebbe essere vero — indexing pending — ma il messaging non lo chiarisce). Forse correlato al P0 chat-SSE-timeout (KB vuoto → no RAG context → no response).
- **Action**: issue P3 — KB Coverage Stats deve riflettere lo stato indexing reale; aggiungere stato "Indicizzazione in corso" se in progress.

### 🌐 P3 — Breadcrumb mobile "Chat" / "Game" non localizzato

**Status**: 🟡 open.

- **Repro**: su `/chat/[threadId]?gameId=…` mobile breadcrumb dice "Chat / Game" — "Game" in inglese.
- **Action**: issue P3.

### ⌨️ P3 — Enter su chat textarea mobile non submit

**Status**: 🟡 open — verificare se intentional.

- **Repro**: textarea chat (`/chat/[threadId]`), Enter va a newline, send richiede tap su button "Invia messaggio".
- **Impact**: pattern legittimo (multiline expected), ma su desktop di norma Enter=submit + Shift+Enter=newline. Mobile può variare; documentare scelta UX.
- **Action**: issue P3 di chiarimento UX o adesione pattern.

### 📱 P3 — Library 6-tab row a 390px

**Status**: 🟡 open.

- **Repro**: `/library` mostra tabs `Tutti / Giochi / Agenti / KB / Sessioni / Chat` (6 in row). A 390px probabile horizontal scroll interno o crowding.
- **Mockup brief SP8** richiede invece 3 tab (Games/Sessions/Chat) + overflow "Più" (Agents/KB) — vedi `admin-mockups/design_files/sp4-library-mobile.html`.
- **Implementation gap**: il mockup SP8 mobile NON è stato implementato (`apps/web/src/components/features/library/LibraryTabs.tsx` ha le 6 tab desktop).
- **Action**: track sotto issue SP8 library-mobile implementation (già menzionata in `v2-migration-matrix.md` come "SP8 brief 2026-05-30").

## Pending verifiche (skipped per time budget)

- [ ] Upload PDF flow mobile (`/library/[gameId]/kb` → "Carica PDF") — non testato in questa sessione perché PDF già presente.
- [ ] Aaron CORE refinement `/library/[gameId]/play/[campaignId]/translate` (reader-mode, manual-mode, multi-lang)
- [ ] Performance @ 390 (LCP/TBT su slow-3G emulato)
- [ ] iOS Safari (vs Pixel 5 Chrome) parity

## E2E spec status (B1)

- [ ] `apps/web/e2e/mobile-golden-path.spec.ts` — TODO post-issue.

## Cleanup post-sessione

- [ ] Rimuovere `admin@meepleai.app` dalla `staging_allowlist` (solo se vogliamo restringere allowlist; rimanere whitelisted non causa rischi).
- [ ] Eliminare il gioco Catan temp aggiunto a `admin@meepleai.app` library — gameId `cc1678e8-f460-4b53-81f6-6d6539f82b65` (decision: forse lasciare per future smoke).

## Related issues

- ✅ #1811 — 2FA schema mismatch (resolved in-session)
- TBD — Chat SSE response timeout su `/chat/[threadId]?gameId=` (P0)
- TBD — `STAGING_ACCESS_DENIED` silent in FE (P0/P1)
- TBD — CSP `manifest-src` for Cloudflare Access proxy (P2)
- TBD — `data-testid="message-input"` mobile coverage (P2)
- TBD — Library `/library/[gameId]` h1 semantic (P2)
- TBD — `TwoFactorVerification` i18n/double heading (P3)
- TBD — `AddGameDrawer` full i18n (P3)
- TBD — Catalog drawer pagination i18n mix (P3)
- TBD — KB Coverage Stats count consistency (P3)
- TBD — Breadcrumb mobile "Chat / Game" i18n (P3)
- TBD — Chat Enter-submit pattern documentation (P3)
- TBD — SP8 library-mobile variant implementation (tracked separately in v2-migration-matrix.md)

## Screenshots

- `audit-01-login-mobile-390.png` (pre-hotfix login error)
- `audit-02-library-mobile-390.png` (library hero + empty state)
- `audit-03-library-add-drawer-390.png` (add game drawer EN)
- `audit-04-library-kb-390.png` (KB hub con rulebook-catan listato)

Locazione: workspace root (audit-*.png) — da spostare in `docs/for-developers/audits/2026-06-02-mobile-screenshots/` come follow-up.
