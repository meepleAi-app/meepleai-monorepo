# Piano di redesign Web UI/UX — Glassmorphism Soft UI

> **Stato**: planning **approvato ed emesso**. Decisioni risolte in §M. **Epic [#3916](https://github.com/meepleAi-app/meepleai-monorepo/issues/3916) + 26 child issue create** (mappa in §N). Nessun codice modificato.
> **Data**: 2026-09-07 · **Branch di discovery**: `main-dev` (`cac40c94b`)
> **Metodo**: Fase 0–3 del brief `/sc:spec-panel`. Sorgente di verità = **codice**
> (`CODE > TESTS > CONFIG > DOCS`). Ogni affermazione è verificata con il comando riportato accanto al dato.
> **Raccomandazione finale**: **GO WITH CHANGES** (§L).

---

## A. Repository findings

### A.1 Divergenze brief ↔ repository (bloccanti per il piano originale)

| Nel brief | Realtà verificata | Impatto |
|---|---|---|
| `docs/04-frontend/design-system.md`, `accessibility-standards.md`, `architecture.md` | **Non esistono.** Reali: `docs/for-developers/frontend/` (20 file), `docs/for-developers/specs/2026-05-11-design-system-deversioning.md`, `docs/for-developers/frontend/v2-a11y-token-audit.md` | Fase 0 non eseguibile alla lettera |
| `apps/web/src/components/pages/` | **Non esiste.** Le pagine vivono in `src/app/**` (App Router), i compositi in `src/components/features/**` | — |
| Layout target: **sidebar sinistra persistente su desktop** | **Vietato dal repo.** `DesktopShell.tsx:31-41` documenta la rimozione di `MainSidebar` (#1977, audit F18) e la cancellazione dei moduli (#2158). `CLAUDE.md`: «Do NOT re-introduce a persistent desktop sidebar» | **P0 — layout target da riscrivere** |
| «GameCard deve essere riutilizzabile» | `GameCard`/`PlayerCard` sono **deprecati**; il canonico è `MeepleCard` (10 entity types, 6 varianti) | Terminologia da correggere |
| App piccola (Home/Games/Ask AI/Upload/History/Settings) | **220 route** (`find src/app -name page.tsx \| wc -l`), ~90 authenticated + ~90 admin | Scala sottostimata di ~1 ordine di grandezza |
| «Introdurre semantic glass/theme design tokens» | I token **esistono già**: `design-tokens-canonical.css` (304 righe, entity color AA-calibrati), `design-tokens.css` (938 righe, 52 occorrenze `glass`), tema via `[data-theme]` | Issue da riformulare, non da creare |
| «Audit current frontend UI and route inventory» | **Già fatto**: `docs/for-developers/frontend/site-map.md` — 224 route, 668 edge, 891 componenti, generato via `/sc:spec-panel`. Più il tooling `pnpm audit:inventory` / `audit:crawl` / `audit:report` | **Issue da NON creare** |

### A.2 Stato del design system (maturo, non greenfield)

- **Token**: `--bg #f7f3ee` (cream), `--bg-card #ffffff`, `--text #2b1f12`, border warm-brown alpha. Dark theme completo. Spacing 4px, radius 7 step, motion 5 durate, z-index 8 livelli.
- **Entity color system**: 9 entità (`game`, `player`, `session`, `agent`, `kb`, `chat`, `event`, `toolkit`, `tool`) **più** 9 varianti `-text` scurite, con ratio WCAG calcolati e annotati inline nel file.
- **Gate anti-drift già blocking in CI**: `lint:tokens`, `lint:tokens:mockups`, `lint:fidelity`, `mockup-annotations:audit`, `lint:bgg`, `lint:storybook-states`, `lint:mockup-state-naming`.
- **ESLint locali** (`apps/web/eslint-rules/`, 9 regole): `no-hardcoded-color-utility` (**error**), `no-hardcoded-hex`, `no-inline-hsl-v2`, `no-standalone-card-renderer`, `no-store-scores-direct`, `no-game-detail-orphan-routes`.
- **Test**: 1.936 file di unit test · 395 spec Playwright · 217 storie Storybook · soglie coverage locali branches 85 / lines 80.

### A.3 Il vero debito glassmorphism (misurato)

Il glassmorphism **c'è già ovunque, ma non è tokenizzato**:

```
grep -rho "backdrop-blur[-a-z]*" src --include=*.tsx | sort | uniq -c
    278 backdrop-blur-md · 109 backdrop-blur-sm · 44 backdrop-blur-xl
     42 backdrop-blur-  · 35 backdrop-blur-none · 16 backdrop-blur · 2 backdrop-blur-lg
→ 526 occorrenze su 308 file
```

```
grep -rho "bg-card/[0-9]*" src --include=*.tsx | sort | uniq -c
  276 /70 · 74 /90 · 61 /50 · 58 /60 · 54 /80 · 43 /40 · 29 /20
   27 /10 · 19 /5 · 10 /95 · 7 /30 · 7 /15 · 6 /85
→ ~671 superfici translucide su 13 valori di alpha distinti
```

Contro **3 soli file** che usano le utility centralizzate `glass-card` / `glass-nav` / `glass-modal`.

**Conseguenza diretta sul brief**: la soglia richiesta («glass funzionale 0.65–0.85, decorativo 0.15–0.25») è **violata da ~105 usi** (`/5`, `/10`, `/15`, `/20`, `/30`, `/40` su superfici che portano testo). Non è un problema di token mancanti: è un problema di **526 decisioni ad-hoc senza vincolo**.

### A.4 Gate a11y: verde su una pagina che non esiste

`e2e/accessibility.spec.ts` è il gate a11y **blocking**. Le route che testa:

| Route testata | Esiste? |
|---|---|
| `/`, `/about`, `/faq`, `/login`, `/register`, `/dashboard`, `/library` | ✅ |
| `/board-game-ai/games` (4 test: light, dark, keyboard, zoom) | ❌ **non esiste** — `find src/app -path "*board-game-ai*"` → vuoto |
| `/settings` | ❌ non esiste **e** il test è `test.skip` (riga 416) |

Quattro test del gate a11y principale puntano a una route inesistente: axe passa su una 404. Nessun gate a11y copre **chat, upload, rules, game-detail** — cioè esattamente i tre journey per cui il brief chiede AAA.

> Precedente noto nel repo: cluster «gate CI che non esaminano niente» (#3622 / #3625 / #3629 / #3632 / #3659 / #3662).

### A.5 `/settings` — indirizzo disallineato (rettificato 2026-09-07)

> ⚠️ **Rettifica.** La prima stesura diceva «dead link in produzione, P0». Verificando il codice prima di implementare, la diagnosi si è rivelata sbagliata in due punti. Priorità corretta: **P1**.

`/settings` non esiste come route, ma **nessun link cliccabile ci punta**:

| Riferimento | Tipo | Raggiungibile? |
|---|---|---|
| `config/contextual-tabs.ts:33-36` · `config/navigation-emoji.ts:8` | config | ❌ inerte (nessuno ci naviga) |
| `lib/constants/notification-routes.ts:35,39` | costanti | ❌ **dead code**: `NotificationRoutes` è importato solo dal proprio test |
| `components/library/LibraryQuotaBadge.tsx:49` | `<Link href>` | ❌ **dead code**: componente non montato |
| `components/admin/__tests__/QuickActions.test.tsx:129` | fixture | ❌ — **il componente non contiene `/settings`**: la prima stesura lo attribuiva erroneamente al componente |
| `locales/it.json:1050` | **testo mostrato all'utente** | ⚠️ **sì** |

L'unico riferimento realmente esposto è una frase in una pagina di privacy/consenso: «**Gestione del consenso** disponibile in `/settings/ai-consent`». È dentro backtick (reso come codice inline, non come link), quindi non è cliccabile — ma è un'istruzione sbagliata su come esercitare un diritto.

**E l'hub Settings esiste già.** `components/features/settings/` contiene `settings-sections.ts` con **7 sezioni** (profile · security · ai-consent · notifications · preferences · api-keys · services), `SettingsTab.tsx` con il layout elenco+pannello, `sections/AiConsentSection.tsx`, stories e 3 file di test. È montato e funzionante a **`/profile?tab=settings&section=<id>`** (`ProfilePageContent.tsx:45,47,59,432`).

> L'audit `2026-07-16-design-coverage-audit.md` segnalava come #1608 P0 che il tab `settings` potesse non essere nel tipo `Tab`, rendendo il wizard 2FA irraggiungibile. **Quel difetto è risolto**: `VALID_TABS` include `'settings'`.

Il difetto reale è quindi un **disallineamento di indirizzo**, non un link rotto — e il lavoro di #3938 scende da «costruire l'hub» (2 g) a «esporlo su una route e riallineare i riferimenti» (~0,5–1 g).

### A.6 Issue aperte

**18 issue aperte in totale**, **nessuna** sul redesign UX. Rilevanti:

- **#3878** — «l'utente non vede mai il fallimento di un upload PDF — la UI di progresso esiste ed è spenta». Copre esattamente il requisito «recoverable error / retry action» del brief.
- **#3901** — la suite Playwright non raccoglie test (Test Count: 0) → **il gate E2E è cieco**.
- **#3853** — 9 pagine admin, schema Zod più stretto del contratto backend.
- **#3836** — 4 difetti minori dall'audit (404 client, pagina card, a11y onboarding).

---

## B. UX audit

Priorità: **P0** blocca il redesign · **P1** core UX · **P2** importante · **P3** polish.

### B.1 Landing / Home pubblica — `(public)/page.tsx`

| # | Rilievo | Pri |
|---|---|---|
| B1.1 | Superfici translucide ad-hoc (`bg-card/70` + `backdrop-blur-md`) senza scala condivisa | P2 |
| B1.2 | Nessun gate a11y sulla landing in dark mode con hero gradient | P2 |

### B.2 Auth — `(auth)/`, 10 route

| # | Rilievo | Pri |
|---|---|---|
| B2.1 | `AuthLayout` + `auth-card` + `oauth-buttons` + `strength-meter` già coerenti, con 11 stories | — (riuso) |
| B2.2 | 10 route auth per quella che il brief tratta come 1 area. Nessun difetto: **fuori scope** | P3 |

### B.3 Home autenticata — `/dashboard` (`DashboardClient.tsx`, 381 righe)

| # | Rilievo | Pri |
|---|---|---|
| B3.1 | Ordine attuale: `DashboardHero` → Prossimi → Recenti → Suggeriti → Friends. **Manca «Continue playing»** come primo blocco e **manca «Ask MeepleAI»** come CTA primaria: il brief li mette ai posti 1 e 2 | **P1** |
| B3.2 | `/dashboard` non è presentata come «Home»: `TOP_BAR_NAV_IDS = ['dashboard','library','hub','sessions','toolkit']` la espone come *Dashboard* | P2 |
| B3.3 | «Quick actions» (Upload PDF / Find Game / Ask question) del wireframe: **assente** come blocco unitario | P1 |
| B3.4 | Loading/error/empty già gestiti (`deriveState` → `loading｜error｜empty｜default`) | — (riuso) |

### B.4 Games catalog — `/games` (hub a 4 tab)

| # | Rilievo | Pri |
|---|---|---|
| B4.1 | **3 tab su 4 sono placeholder «Coming Soon»**: `catalogo`, `trending`, `community` (`page.tsx:12-14`). Il catalogo descritto dal brief (search, filtri, player count, durata, rulebook availability) **non esiste** | **P0** |
| B4.2 | Cinque superfici parallele listano giochi: `/games`, `/library`, `/discover` (compat), `/hub` (redirect), `/shared-games` (pubblica) | **P1** duplicazione |
| B4.3 | `MeepleCard` è il canonico ma convivono **3 famiglie**: `ui/data-display/meeple-card`, `ui/shared-games/meeple-card-game`, `ui/data-display/extra-meeple-card`. Debito già tracciato nell'audit MeepleCard/CSS drift del 2026-07-12 | P2 |

### B.5 Game detail — `/games/[id]` + `/library/[gameId]`

| # | Rilievo | Pri |
|---|---|---|
| B5.1 | **Due detail concorrenti** per lo stesso gioco: `/games/[id]` (+ `card`, `faqs`, `rules`, `sessions`) e `/library/[gameId]` (+ `agent`, `kb`, `play`, `toolbox`, `toolkit`). Le CTA saltano fra i due alberi | **P0** IA |
| B5.2 | La regola ESLint `no-game-detail-orphan-routes` esiste già → problema noto e presidiato, ma non risolto | P1 |
| B5.3 | Le tab target del brief (Overview/Setup/Rules/Documents/History) mappano su route sparse fra i due alberi | P1 |

### B.6 Rule viewer — `/games/[id]/rules` (153 righe) — **il gap più grande**

| # | Rilievo | Pri |
|---|---|---|
| B6.1 | **Non è un lettore di regolamento**: è una lista di *versioni di `RuleSpec`* con accordion di «atomi». È di fatto una vista diagnostica esposta all'utente | **P0** |
| B6.2 | Nessun TOC, nessuna ricerca nel testo, nessun pannello AI contestuale, nessuna citazione | **P0** |
| B6.3 | `border-l-[hsl(262,83%,58%)]` — colore hardcoded inline (riga 30); passa il lint solo perché è arbitrary-value | P2 |
| B6.4 | `useEffect` + `api.games.getRules` invece di React Query: niente cache, niente retry, niente `isFetching` | P2 |
| B6.5 | Back-link a `/library/[gameId]` da una pagina sotto `/games/[id]` → conferma B5.1 | P1 |
| B6.6 | **Lingua mista**: header IT («Regolamento», «Gioco»), corpo EN («Version», «No rules have been published…», «Failed to load rules») | P1 |
| B6.7 | **Sorgente dati long-form non identificata**: il testo integrale del regolamento non è servito da questa route. Candidati: `knowledge-base/[id]/pdf`, `library/[gameId]/kb`, `gamebook`. **Serve uno spike prima di poter progettare il viewer** | **P0 bloccante** |

### B.7 AI Chat — `(chat)/chat`

| # | Rilievo | Pri |
|---|---|---|
| B7.1 | Il layout target del brief (thread sidebar + message area + citation panel + composer sticky) è **già implementato**: `chat/layout.tsx` (`aside w-80` desktop) + 40 componenti in `chat-unified/` (`CitationSheet`, `ChatInfoPanel`, `RuleSourceCard`, `InlineCitationText`, `ResponseMetaBadge`, streaming, TTS, voice) | — (riuso) |
| B7.2 | Nessun gate a11y sulla chat, benché il brief la voglia AAA | **P1** |
| B7.3 | `bg-background/50` sull'aside: superficie translucida sotto testo, alpha nella fascia proibita | P2 |

### B.8 PDF Upload — `/upload` (608 righe) + `/gamebook/upload` + admin

| # | Rilievo | Pri |
|---|---|---|
| B8.1 | Wizard a step **già esistente** (`useWizard` reducer, `WizardSteps`, `WizardProgress`) + 13 stories PDF + `PdfStatusTimeline`, `PdfProcessingProgressBar`, `PdfStatusBadge`, `PdfErrorCard`, `progress-{badge,card,modal,toast}` | — (riuso) |
| B8.2 | **La UI di errore/progresso esiste ma è spenta** → issue **#3878 già aperta**. È il difetto reale, non il layout | **P1** (già tracciato) |
| B8.3 | Tre superfici di upload distinte (`/upload`, `/gamebook/upload`, `/admin/knowledge-base/upload`) senza componente condiviso | P2 |
| B8.4 | Nessun gate a11y su upload, benché il brief lo voglia AAA | P1 |

### B.9 Processing / status

| # | Rilievo | Pri |
|---|---|---|
| B9.1 | Pipeline visiva (Upload→Validation→Extraction→Indexing→Ready) coperta da `PdfStatusTimeline` + `RagReadyIndicator`; superfici admin dedicate (`/admin/knowledge-base/processing`, `/queue`, `/pipeline`) | — (riuso) |
| B9.2 | Lato utente il feedback dipende da #3878 | P1 |

### B.10 Settings

| # | Rilievo | Pri |
|---|---|---|
| B10.1 | **`/settings` non esiste**, ma nessun link cliccabile ci punta: i riferimenti sono config inerte e dead code (§A.5 rettificata). L'unico esposto all'utente è una frase in una pagina di consenso che indica un percorso inesistente | **P1** *(era P0)* |
| B10.2 | **L'hub esiste già**, con 7 sezioni, a `/profile?tab=settings&section=<id>`. Non va costruito: va esposto e riallineato | — (riuso) |
| B10.3 | `ui/settings-list` + `ui/settings-row` esistono con test, **e sono già usati** da `SettingsTab` | — (riuso) |

### B.11 Admin UI

| # | Rilievo | Pri |
|---|---|---|
| B11.1 | ~90 route sotto `AdminShell` (tema scuro, `AdminSidebar` legittima). **Fuori scope**: l'audit 2026-08-26 la copre e #3853 / #3836 la stanno già toccando | **out of scope** |

### B.12 Trasversali

| # | Rilievo | Pri |
|---|---|---|
| B12.1 | 526 `backdrop-blur` + ~671 `bg-card/α` su 13 alpha, senza scala (§A.3) | **P0** |
| B12.2 | Gate a11y che punta a route inesistente; 3 journey core scoperti (§A.4) | **P0** |
| B12.3 | Gate E2E che non raccoglie test (#3901) → qualunque «validation E2E» del redesign è **non verificabile finché #3901 è aperta** | **P0 bloccante** |
| B12.4 | `prefers-reduced-motion`: presente in 7 file su ~40 animazioni dichiarate in `globals.css` (`@theme`) | P1 |
| B12.5 | `lint --max-warnings=510` → 510 warning tollerati | P3 |

---

## C. Target Information Architecture

**Vincolo non negoziabile**: nessuna sidebar desktop persistente (#1977 / #2158). L'IA target si realizza **dentro `AppTopBar` (5 slot) + overflow «Altro» + `MiniNavSlot`**.

```
MeepleAI  ── AppTopBar (5 slot, SSOT) ──────────────────────────────
│
├── Home            → /dashboard          [slot 1, rinominare "Home"]
├── Library         → /library            [slot 2]  personale
├── Games           → /games              [slot 3]  catalogo/esplorazione
│     ├── Discover  → /games?tab=discover        (default, esiste)
│     └── Catalogo  → /games?tab=catalogo        (PLACEHOLDER — da costruire)
├── Sessions        → /sessions           [slot 4]  = "History"
├── Toolkit         → /toolkit            [slot 5]
└── Altro (overflow)
      ├── Ask AI    → /chat
      ├── Upload    → /upload
      └── Settings  → /settings           (DA CREARE o da redirigere)

Game (entità unica, un solo albero — decisione richiesta §J.1)
└── /library/[gameId]              ← candidato canonico (più sotto-route)
      ├── Overview                  (esiste)
      ├── Rules      → …/rules      ← DA COSTRUIRE (B6)
      ├── Documents  → …/kb         (esiste)
      ├── Play/Setup → …/play, …/toolkit (esiste)
      └── History    → …/sessions   (oggi sotto /games/[id])
```

**Riduzione della duplicazione decisa dall'IA**:

| Concetto | Vive in | Le altre superfici diventano |
|---|---|---|
| Catalogo / esplorazione | `/games` | `/discover` → redirect (già compat), `/hub` → redirect (già) |
| Collezione personale | `/library` | — |
| Dettaglio gioco | **un solo albero** (§J.1) | l'altro → redirect permanenti |
| Documenti / PDF di un gioco | `/library/[gameId]/kb` | `/knowledge-base/[id]` resta vista globale |
| Conversazione AI | `/chat` (globale) + pannello contestuale in Rules | nessuna terza chat |
| Impostazioni | `/settings` (hub) | `/profile` resta identità pubblica |

**Nessuna pagina nuova** oltre a `/settings` (già referenziata, mai creata) e `/games?tab=catalogo` (già dichiarata, mai implementata).

---

## D. Design-system gap analysis

| Area | Stato | Gap reale |
|---|---|---|
| Semantic token base (`--bg`, `--text`, `--border`, `--primary`…) | ✅ completo | nessuno |
| `--success` / `--warning` / `--error` / `--info` | ✅ + varianti `-ink` AA | nessuno |
| Dark / light | ✅ `[data-theme]` + `.dark` compat | nessuno |
| `--glass-blur-{sm,md,lg,xl}` | ✅ definiti | **non usati** (3 file su 308) |
| `--glass-bg-*`, `--glass-border-*`, `--glass-shadow-*` | ✅ definiti | idem |
| **`--glass-opacity` come scala vincolata** | ❌ **assente** | **il gap principale**: nessun token nomina l'alpha, quindi 13 alpha ad-hoc |
| **Distinzione glass decorativo / funzionale / long-form** | ❌ assente | nessuna regola, nessun lint |
| `--focus-ring` | ⚠️ da verificare puntualmente | — |
| Radius / spacing / motion / z-index | ✅ completi | nessuno |
| Icone Lucide | ✅ già in uso | nessuno |
| Entity color + varianti `-text` AA | ✅ 9 + 9 calibrate | nessuno |

**Verdetto**: il brief chiede di *introdurre* i token glass. Il lavoro reale è **vincolarli e farli adottare**: 1 token di scala + 1 regola ESLint + codemod su 308 file.

### D.1 Palette A / B / C — analisi

La palette attuale **è già Palette A al ~70%**:

| Palette A (brief) | Token attuale | Δ |
|---|---|---|
| Warm Surface `#F5EFE6` | `--bg-muted #efe6d9` | ~ equivalente |
| Background `#EEE8DF` | `--bg #f7f3ee` | attuale più chiaro |
| Meeple Brown `#70513B` | `--text-sec #5a4a38` | ~ equivalente |
| Warm Yellow `#F4C95D` | `--c-warning` (38 92% 50%) ≈ `#f1a417` | ~ equivalente |
| **Primary Blue `#2E6F95`** | `--brand: var(--c-game)` = `hsl(25 95% 38%)` **terracotta** | ❌ **divergenza sostanziale** |

**Raccomandazione: una sola palette, non tre.**

1. Le palette B e C richiedono di ricalibrare **tutte e 9 le entity color più le 9 varianti `-text`**, ognuna con ratio WCAG annotato a mano nel file canonico. Costo alto, beneficio solo estetico.
2. Cambiare il **primary da terracotta a blu** tocca `--brand`, tutte le CTA e le calibrazioni AA su cream. Il blu esiste già come `--c-chat` (220 80% 40%).
3. Proposta: **mantenere terracotta come primary**, adottare i restanti valori di Palette A dove divergono, portare il blu come `--accent`. Se il committente vuole comunque il primary blu, va fatto come issue autonoma con ri-audit completo del contrasto — non dentro il redesign.

---

## E. Epic proposta

**Titolo**: `[WEB][UX] Normalizzazione glass + Rule Viewer + IA gioco (redesign evolutivo)`

*(non «Glass Design System Redesign»: i token esistono, non vanno ri-creati)*

**1. Context** — `apps/web` ha 220 route, 891 componenti, un design system canonico con entity color AA-calibrati e 7 gate anti-drift blocking. Il glassmorphism è già presente ma non tokenizzato (526 `backdrop-blur`, ~671 superfici translucide su 13 alpha ad-hoc). Tre journey core (rules, chat, upload) non hanno gate a11y; il gate a11y principale punta a una route inesistente.

**2. Current problems** — §B, con priorità P0/P1.

**3. Target experience** — §C.

**4. Scope** — token glass vincolati e adottati; Rule Viewer; consolidamento IA gioco; `/settings`; catalogo `/games?tab=catalogo`; riparazione dei gate a11y; Home con «Continue playing» e quick actions.

**5. Out of scope** — admin UI (~90 route); cambio del primary color; backend API; nuove librerie UI; rewrite di chat e upload wizard (già conformi); consolidamento delle 3 famiglie MeepleCard (audit 2026-07-12, epica separata).

**6. Architecture constraints** — (a) `AppTopBar` è SSOT della nav desktop, **nessuna sidebar persistente** (#1977 / #2158); (b) Shadcn/Radix restano; (c) `local/no-hardcoded-color-utility` resta *error*; (d) `MeepleCard` è il canonico, `no-standalone-card-renderer` resta attivo; (e) niente `components/v2/**` né `ui/v2/**`.

**7. Design system strategy** — estendere `design-tokens-canonical.css` con una **scala glass a 3 livelli**, codemod dei 308 file, nuova regola ESLint `local/no-adhoc-glass-surface`.

**8. Routes affected** — `/dashboard`, `/games`, `/games?tab=catalogo`, `/library/[gameId]/**`, `/games/[id]/rules`, `/settings` (nuova), più il chrome globale (`DesktopShell`, `AppTopBar`, `MiniNavSlot`).

**9. Component strategy** — riuso obbligatorio: `MeepleCard`, `settings-list` / `settings-row`, `WizardSteps`, `PdfStatusTimeline`, `chat-unified/*`, `empty-state/`, `loading/`, `errors/`. Nuovi: `GlassSurface`, `RuleReaderShell`, `RuleTableOfContents`, `ContextualAskPanel`, `SettingsShell`, `CatalogFilters`.

**10. Accessibility requirements** — §K.

**11. Performance constraints** — nessun aumento del numero di layer con `backdrop-filter` composti per viewport (misura: conteggio dei nodi con `backdrop-filter` computato ≠ `none` per route core, prima/dopo); nessuna animazione continua di `filter`; budget bundle invariati (`pnpm bundle:check`).

**12. Testing strategy** — §K.

**13. Migration strategy** — codemod glass in 3 ondate per cluster di route, ciascuna con il proprio PR e il proprio confronto Storybook; nessuna ondata oltre 60 file.

**14. Rollout strategy** — nessun feature flag per il glass (è CSS token-level, reversibile con un revert). Feature flag **sì** per Rule Viewer e `/settings` (superfici nuove).

**15. Definition of Done** — §K più la checklist per-issue.

**16. Child issues checklist** — §F.

---

## F. Child issues

**Le 15 issue del brief sono state riviste**: 2 eliminate (lavoro già fatto), 6 riformulate, 4 aggiunte (gate rotti e blocchi non previsti), 3 ridotte a polish.

### F.0 — Prerequisiti (bloccano tutto il resto)

| ID | Titolo | Note | Stima |
|---|---|---|---|
| **UX-00a** | *(nessuna issue nuova)* — sbloccare **#3901** «Playwright non raccoglie test» | Senza questo **nessun criterio E2E del redesign è verificabile**. Issue già aperta | — |
| **UX-00b** | fix(e2e): il gate a11y testa `/board-game-ai/games`, route inesistente | 4 test verdi su una 404; `/settings` skipped. Ripuntare su route reali e aggiungere chat / upload / rules / game-detail | 1 g |
| **UX-00c** | fix(web): `/settings` è referenziata da 3 punti ma la route non esiste | Dead link in prod. Decidere: creare l'hub o redirigere a `/profile` | 0,5 g |

### F.1 — Foundation

| ID | Titolo | Sostituisce | Stima |
|---|---|---|---|
| **UX-01** | ~~Audit + route inventory~~ | **ELIMINATA** — `site-map.md` (224 route / 668 edge / 891 componenti) più questo documento | — |
| **UX-02a** | feat(tokens): scala glass a 3 livelli (`decorative` / `functional` / `content`) + primitiva `GlassSurface` | ex WEB-UX-02, riformulata | 1 g |
| **UX-02b** | feat(lint): regola `local/no-adhoc-glass-surface` (blocca `backdrop-blur-*` e `bg-*/α` fuori dalla scala) | nuova | 1 g |
| **UX-02c** | refactor(web): codemod glass — ondata 1, chrome globale (`DesktopShell`, `AppTopBar`, `SideDrawer`, dialoghi) | nuova, ≤60 file | 1,5 g |
| **UX-02d** | refactor(web): codemod glass — ondata 2, card ed entity surfaces | nuova, ≤60 file | 1,5 g |
| **UX-02e** | refactor(web): codemod glass — ondata 3, resto e rimozione degli alpha <0.5 sotto testo | nuova | 2 g |
| **UX-02f** | feat(tokens): `--accent` al blu Palette A + `--ring` allineato (sana l'incoerenza viola/ambra fra i temi) | nuova (§M.2) | 1 g |
| **UX-03** | ~~Build shared Web App Shell~~ → **refactor(web): tokenizzare il chrome esistente** | `DesktopShell` esiste; **vietato** aggiungere sidebar | assorbita in UX-02c |

### F.2 — Superfici

| ID | Titolo | Note | Stima |
|---|---|---|---|
| **UX-04a** | feat(web): blocco «Continue playing» in cima alla Home | B3.1 | 1,5 g |
| **UX-04b** | feat(web): blocco «Quick actions» (Upload / Find game / Ask AI) in Home | B3.3 | 1 g |
| **UX-05a** | **spike**: definire il contratto dati del catalogo (search, player count, durata, rulebook availability) | B4.1 — la tab è placeholder, il backend va verificato **prima** | 1 g |
| **UX-05b** | feat(web): `/games?tab=catalogo` — griglia/lista, search, filtri | dipende da UX-05a | 2 g |
| **UX-06a** | ~~decisione~~ → **ADR**: `/games/[id]` è l'albero canonico | **decisa** (§M.1) — resta da scrivere l'ADR | 0,5 g |
| **UX-06b** | refactor(web): migrare le 9 sotto-route da `/library/[gameId]` a `/games/[id]` | 10 route, dipende da UX-06a | 2 g |
| **UX-06c** | refactor(web): codemod dei 117 link entranti su 84 file verso l'albero canonico | dipende da UX-06b | 1,5 g |
| **UX-06d** | feat(web): redirect di cortesia `/library/[gameId]/**` → `/games/[id]/**` + test | app non distribuita → niente 301 SEO-safe | 1 g |
| **UX-07a** | **spike**: individuare la sorgente long-form del regolamento | **B6.7 — blocca UX-07b/c e UX-08** | 1 g |
| **UX-07b** | feat(web): `RuleReaderShell` — layout 2 colonne (TOC + contenuto), long-form quasi opaco | dipende da UX-07a | 2 g |
| **UX-07c** | feat(web): TOC generato, deep-link per sezione, ricerca nel testo | | 2 g |
| **UX-08** | feat(web): `ContextualAskPanel` collassabile nel Rule Viewer (riusa `chat-unified`) | | 2 g |
| **UX-09** | ~~Refactor Chat layout~~ → **polish**: rimuovere `bg-background/50` sotto testo, allineare alla scala glass | B7 — layout già conforme | assorbita in UX-02d |
| **UX-10** | ~~Refactor PDF Upload wizard~~ → **#3878 già aperta** | B8 — il difetto è la UI spenta, non il layout | — |
| **UX-11** | feat(web): hub `/settings` (Profile / Preferences / Privacy / Advanced) con `settings-list` e `settings-row` | dipende da UX-00c | 2 g |

### F.3 — Qualità

| ID | Titolo | Note | Stima |
|---|---|---|---|
| **UX-12** | ~~Create empty/loading/error states~~ → **audit mirato** delle sole superfici toccate | `empty-state/`, `loading/`, `errors/`, `state/` esistono già | 1 g |
| **UX-13a** | test(a11y): estendere il gate a chat, upload, rules, game-detail | dipende da UX-00b | 1,5 g |
| **UX-13b** | fix(a11y): `prefers-reduced-motion` per le ~40 animazioni di `globals.css` | B12.4 | 1,5 g |
| **UX-14** | fix(web): passata responsive sulle sole superfici nuove (Rule Viewer, Settings, Catalogo) | non un rework globale | 1,5 g |
| **UX-15** | test(visual): stories Storybook a 8 stati per `GlassSurface`, `RuleReaderShell`, `SettingsShell`, `CatalogFilters` | `playwright.storybook.config.ts` esiste | 1,5 g |

**Totale**: 27 issue (di cui 2 spike + 1 ADR), ~37 giorni-uomo. Nessuna issue supera i 2 giorni.

---

## G. Dependency graph

```
#3901 (E2E cieco) ──┐
UX-00b (gate a11y)  ├──> gate verificabili ──────────────┐
UX-00c (/settings)  ┘                                    │
                                                         │
UX-02a (scala glass) ──> UX-02b (lint) ──> UX-02c ──> UX-02d ──> UX-02e
                                             │                      │
                                             └── UX-09 (assorbita)  │
                                                                    │
UX-05a (spike catalogo) ──> UX-05b (catalogo)                       │
UX-06a (ADR albero)     ──> UX-06b (redirect + CTA)                 │
UX-07a (spike regole)   ──> UX-07b ──> UX-07c ──> UX-08             │
UX-00c                  ──> UX-11 (/settings)                       │
UX-04a, UX-04b (Home)   ── indipendenti ────────────────────────────┤
                                                                    │
                                        UX-12, UX-13a, UX-13b, UX-14, UX-15
```

---

## H. Ordine di implementazione

1. **Sbloccare i gate** — #3901, UX-00b, UX-00c. *Senza questo nessun criterio è verificabile.*
2. **Spike in parallelo** — UX-05a, UX-06a, UX-07a. *Tre incognite che, scoperte tardi, invalidano il piano.*
3. **Foundation glass** — UX-02a → UX-02b → UX-02c / d / e.
4. **Superfici nuove** — UX-07b/c → UX-08 (Rule Viewer, il gap più grande); UX-11; UX-05b.
5. **Consolidamento IA** — UX-06b.
6. **Home** — UX-04a, UX-04b.
7. **Qualità** — UX-13a/b, UX-12, UX-14, UX-15.

> Il brief chiedeva «tokens → primitives → shell → home → …». Corretto in due punti: (a) **i gate vengono prima dei token**, perché il repo ha un precedente documentato di gate verdi che non esaminano nulla; (b) **la Home scende di priorità**, perché è l'area meno rotta mentre il Rule Viewer è quella più rotta.

---

## I. Lavoro parallelizzabile

| Lotto | Issue | Vincolo |
|---|---|---|
| **P-1** (subito, 3 tracce) | UX-00b · UX-00c · UX-02a | nessuna dipendenza reciproca |
| **P-2** (3 spike) | UX-05a · UX-06a · UX-07a | eseguibili insieme, aree disgiunte |
| **P-3** | UX-04a · UX-04b | stessa pagina → **serializzare** o dividere per file |
| **P-4** | UX-07b/c/08 ‖ UX-11 ‖ UX-05b | alberi di route disgiunti |
| **Non parallelizzabile** | UX-02c → UX-02d → UX-02e | codemod sugli stessi file: conflitti garantiti |

---

## J. Rischi critici

| # | Rischio | Probabilità | Mitigazione |
|---|---|---|---|
| **J.1** | **La sorgente long-form delle regole non esiste** e il Rule Viewer del brief non è costruibile senza lavoro backend | **alta** | UX-07a è uno spike bloccante. Se confermato: il brief vieta «cambiare UX e backend nello stesso task» → l'Epic va ridotta e il Rule Viewer diventa un'epica separata |
| **J.2** | Il codemod glass su 308 file rompe il contrasto in punti non coperti da test | media | ondate ≤60 file, confronto Storybook a 8 stati, `lint:tokens` più gate a11y su ogni PR |
| **J.3** | **#3901 non si risolve** → nessuna validation E2E | media | senza E2E il DoD si appoggia a unit, Storybook snapshot e verifica manuale documentata; **va dichiarato esplicitamente**, non nascosto |
| **J.4** | La decisione sull'albero canonico del gioco (UX-06a) tocca link condivisi e SEO | media | redirect permanenti più inventario link da `site-map.md` (668 edge già mappati) |
| **J.5** | Cambio primary terracotta → blu richiesto in un secondo momento | media | tenerlo fuori scope e tokenizzato: un solo `--brand` da riassegnare |
| **J.6** | I 510 warning ESLint tollerati mascherano regressioni introdotte | bassa | non alzare la soglia; ogni PR deve lasciarla ≤ baseline |
| **J.7** | Le 3 famiglie MeepleCard divergono ancora durante il redesign | media | vietato creare una quarta famiglia; `no-standalone-card-renderer` resta *error* |

---

## K. Validation plan

**Per ogni issue UI, DoD minimo**:

```
pnpm typecheck && pnpm lint && pnpm test
pnpm lint:tokens && pnpm lint:tokens:mockups && pnpm lint:fidelity
pnpm test:a11y:e2e          # solo dopo UX-00b
pnpm test:storybook:snapshots
```

**Gate specifici del redesign** (nuovi, verificabili):

| Criterio | Misura | Soglia |
|---|---|---|
| Glass tokenizzato | `grep -rho "backdrop-blur[-a-z]*" src --include=*.tsx \| wc -l` | da **526** a **0** fuori da `GlassSurface` |
| Alpha ad-hoc | `grep -rho "bg-card/[0-9]*" src --include=*.tsx \| sort -u \| wc -l` | da **13 valori** a **3** (i 3 livelli di scala) |
| Testo su glass sotto soglia | usi di alpha < 0,5 su nodi con testo | **0** |
| Copertura del gate a11y | route reali testate da `e2e/accessibility.spec.ts` | ≥ **12**, di cui chat, upload e rules |
| Route fantasma nei test | route testate che non risolvono a un `page.tsx` | **0** |
| Reduced motion | animazioni in `globals.css` senza guardia | **0** |
| Contrasto | axe `color-contrast` su route core, light e dark | **0 violazioni** (gate già blocking) |
| Performance | nodi con `backdrop-filter` computato ≠ `none` per route core | **non superiore** al baseline pre-redesign |
| Bundle | `pnpm bundle:check` | budget invariati |

**Baseline da catturare prima di iniziare** (altrimenti «non peggiorare» non è misurabile): conteggio dei nodi `backdrop-filter` per route core, LCP e CLS su `/dashboard` e `/library`, output di `bundle:check`.

---

## L. Raccomandazione

### **GO WITH CHANGES**

Il redesign è sensato e c'è debito reale da aggredire. Ma **il piano del brief va corretto in 7 punti** prima di aprire l'Epic:

1. **Il layout target va riscritto.** La sidebar sinistra persistente su desktop è vietata dal repo (#1977, #2158, `CLAUDE.md`). L'IA target si realizza in `AppTopBar` + overflow + `MiniNavSlot`.
2. **WEB-UX-01 va eliminata.** L'audit e l'inventario route esistono (`site-map.md`: 224 route, 668 edge, 891 componenti) più il tooling `pnpm audit:*`.
3. **WEB-UX-02 va riformulata.** I token glass esistono; il lavoro è vincolarli (scala a 3 livelli più regola ESLint) e adottarli su 308 file. Il numero che giustifica l'Epic è **526 `backdrop-blur` ad-hoc su 13 alpha diversi**, non «mancano i token».
4. **Le 3 palette vanno ridotte a 1.** La palette attuale è già Palette A al ~70%; B e C richiedono di ricalibrare 18 entity color con ratio WCAG calcolati a mano. Il cambio primary terracotta → blu è un'epica a sé.
5. **Tre spike bloccanti vanno prima del codice**: sorgente long-form delle regole (senza, il Rule Viewer non è progettabile), contratto dati del catalogo, albero canonico del dettaglio gioco.
6. **I gate vengono prima dei token.** Il gate a11y ha 4 test verdi su una route inesistente e la suite E2E non raccoglie test (#3901): oggi il redesign **non sarebbe verificabile**.
7. **Chat e Upload escono dallo scope di rewrite.** Sono già conformi al target del brief; l'unico difetto reale è #3878, già aperta.

**Decisioni richieste prima di creare le issue** — risolte in §M.

---

## M. Decisioni risolte (2026-09-07)

### M.1 — Albero canonico del dettaglio gioco: **`/games/[id]`**

Decisione del committente. Conseguenze misurate:

| | |
|---|---|
| Route sotto `/library/[gameId]` da migrare | **10** (index, `agent`, `kb`, `play`, `play/[campaignId]`, `play/[campaignId]/encounter`, `play/[campaignId]/translate`, `toolbox`, `toolkit`, `toolkit/[sessionId]`) |
| Route già sotto `/games/[id]` | 5 (index, `card`, `faqs`, `rules`, `sessions`) |
| Link entranti verso `/library/<id>` | **117 occorrenze su 84 file** |

`/library` (indice della collezione personale) **resta**: si sposta solo il *dettaglio*. `/library/[gameId]` diventa redirect.

**Effetto sul piano**: UX-06b era stimata 2 g. È irrealistica per 10 route + 117 link → **split in UX-06b / UX-06c / UX-06d** (§F.2 aggiornata). Poiché **l'app non è distribuita** (M.3), non servono 301 permanenti SEO-safe: bastano redirect di cortesia, e il rischio J.4 scende da *media* a *bassa*.

### M.2 — Palette: primary invariato, **accent al blu Palette A**

Delega del committente («scegli i colori migliori»). Scelta e giustificazione:

| Token | Oggi | Nuovo | Usi impattati |
|---|---|---|---|
| `--primary` | `25 95% 32%` terracotta | **invariato** | 923 (`bg-primary` 400 · `text-primary` 270 · `border-primary` 183 · `ring-primary` 70) → **0 toccati** |
| `--accent` light | `271 91% 55%` **viola** | `202 53% 38%` (`#2E6F95`, Palette A Primary Blue) | 60 (`bg-accent`) |
| `--accent` dark | `45 93% 56%` **ambra** | `202 53% 62%` (`#6FA8CB`) | idem |
| `--ring` light / dark | `25 95% 53%` / `45 93% 56%` | allineato all'accent blu | — |
| 9 entity color + 9 varianti `-text` | — | **invariate** | 0 |

**Perché non spostare il primary al blu**, malgrado Palette A lo indichi:
1. 923 usi cambierebbero colore in un colpo, senza controllo semantico per-contesto; una parte rappresenta l'entità *game*, che resterebbe terracotta → incoerenza.
2. `#2E6F95` (202 53% 38%) è adiacente a `--c-chat` (220 80% 40%): il primary diventerebbe confondibile con un entity color.
3. Cream + terracotta + brown *è già* il «legno / tavolo / affidabilità» che Palette A dichiara come obiettivo. Il blu come primary è il default di ogni SaaS: toglie distintività invece di aggiungerla.

**Perché spostare l'accent**, e perché è la mossa migliore:
1. `--accent` è oggi **incoerente fra i temi**: viola in light, ambra in dark — non cambia solo luminosità, cambia *tinta*. È un difetto non catalogato prima d'ora, e questa modifica lo sana.
2. Solo **60 usi** → rischio contenuto e reversibile.
3. Il glassmorphism su fondo cream caldo tende a «sporcare»: un accento **freddo** àncora le superfici frosted e dà il cambiamento visivo percepibile che il redesign richiede, senza toccare l'identità.
4. Il focus ring diventa per la prima volta *distinguibile* dagli stati hover terracotta (oggi ring arancione su hover arancione).

**Contrasti verificati** (sRGB linearizzato + luminanza relativa WCAG):

| Coppia | Ratio | Esito |
|---|---|---|
| `#2E6F95` su cream `#f7f3ee` | **4,98:1** | ✅ AA testo normale |
| bianco su `#2E6F95` (`--accent-foreground`) | **5,50:1** | ✅ AA |
| `#6FA8CB` su dark `#14100a` | **7,55:1** | ✅ AAA |
| `#1a1a1a` su `#6FA8CB` (dark fg) | **7,30:1** | ✅ AAA |

Giallo caldo Palette A `#F4C95D`: già coperto da `--c-warning` ≈ `#f1a417`. Non si tocca.

**Effetto sul piano**: nuova issue **UX-02f** (palette accent + ring). Il rischio J.5 (cambio primary) **decade**.

### M.3 — Rule Viewer: **procedere, backend ammesso**

Decisione del committente: «l'app non è distribuita». Conseguenze:

1. Se UX-07a rivela che la sorgente long-form non esiste, il lavoro backend **è autorizzato** — ma resta in **issue separate** dalla UX (vincolo del brief: «non cambiare UX e backend nello stesso task se separabili»).
2. Il rischio **J.1 scende da bloccante a gestito**: lo spike non decide più *se* fare il Rule Viewer, ma *quanto* backend serve.
3. Il rischio **J.4** (link condivisi / SEO sui redirect) scende a bassa: nessun utente reale, nessun link esterno da preservare.
4. Rimane invariato **J.3**: senza #3901 la validation E2E non è disponibile. Questo non dipende dalla distribuzione.


---

## N. Mappa ID del piano → issue GitHub

Epic: **#3916**

| ID nel piano | Issue | Titolo |
|---|---|---|
| UX-00a | **#3901** *(preesistente)* | la suite Playwright non raccoglie test |
| UX-00b | **#3917** | gate a11y su `/board-game-ai/games`, route inesistente |
| UX-00c | ~~#3918~~ | **chiusa come duplicato** (spec review 2026-09-07), fusa in #3938 |
| UX-01 | — | **eliminata**: `site-map.md` + `pnpm audit:*` |
| UX-02a | **#3919** | scala glass a 3 livelli + `GlassSurface` |
| UX-02b | **#3920** | regola `local/no-adhoc-glass-surface` |
| UX-02c | **#3921** | codemod glass — ondata 1, chrome globale |
| UX-02d | **#3922** | codemod glass — ondata 2, card ed entity surfaces |
| UX-02e | **#3923** | codemod glass — ondata 3 + regola a `error` |
| UX-02f | **#3924** | `--accent` al blu Palette A, `--ring` allineato |
| UX-03 | — | **assorbita** in #3921 (nessuna sidebar) |
| UX-04a | **#3930** | blocco «Continue playing» in Home |
| UX-04b | **#3931** | blocco «Quick actions» in Home |
| UX-05a | **#3932** | spike: contratto dati del catalogo |
| UX-05b | **#3933** | `/games?tab=catalogo` |
| UX-06a | **#3925** | ADR: `/games/[id]` canonico |
| UX-06b | **#3927** | migrare le 9 sotto-route |
| UX-06c | **#3928** | codemod dei 117 link |
| UX-06d | **#3929** | redirect di cortesia |
| UX-07a | **#3934** | spike: sorgente long-form del regolamento |
| UX-07b | **#3935** | `RuleReaderShell` |
| UX-07c | **#3936** | indice, deep-link, ricerca |
| UX-08 | **#3937** | `ContextualAskPanel` |
| UX-09 | — | **assorbita** in #3922 (chat già conforme) |
| UX-10 | **#3878** *(preesistente)* | UI di fallimento upload spenta |
| UX-11 | **#3938** | *(riscritta)* montare l'hub **esistente** su `/settings` + riallineare i riferimenti |
| — | **#3946** | *(nuova)* i testi legali indicavano `/settings/ai-consent`, inesistente — PR #3949 |
| — | **#3948** | *(nuova)* privacy policy IT ed EN divergono sulla base giuridica AI |
| UX-12 | **#3939** | audit empty / loading / error |
| UX-13a | **#3940** | gate a11y su chat, upload, rules, game-detail |
| UX-13b | **#3941** | `prefers-reduced-motion` |
| UX-14 | **#3942** | passata responsive |
| UX-15 | **#3943** | storie a 8 stati |

**Conteggio verificato**: 18 issue aperte prima · +1 Epic · +26 child = **45 aperte** (`gh issue list --state open | jq length`). Il numero #3926 appartiene a un altro processo, non a questo piano.

---

## O. Rettifiche emerse in implementazione

Registro di ciò che il codice ha smentito, man mano che le issue vengono lavorate. Serve a evitare che il piano invecchi in silenzio.

| Data | Voce del piano | Rettifica | Effetto |
|---|---|---|---|
| 2026-09-07 | §A.5 «`/settings` dead link P0» | Nessun link cliccabile: config inerte + dead code. L'unico riferimento esposto è testo in una pagina di consenso | #3918 da P0 a **P1** |
| 2026-09-07 | §F «UX-11: costruire l'hub Settings, 2 g» | **L'hub esiste già** (7 sezioni, `SettingsTab`, montato a `/profile?tab=settings`) | #3938 da 2 g a **~0,5–1 g** |
| 2026-09-07 | §F «UX-02a: creare `GlassSurface`» | **`ui/surfaces/GlassCard.tsx` esiste** con 4 consumatori — manca solo il concetto di livello | #3919 diventa «estendere», non «creare» |
| 2026-09-07 | §A.4 «il gate a11y scansiona 1 route inesistente» | Erano **due** route sbagliate: `/board-game-ai/games` (404, 5 esecuzioni) **e** `/library`, che senza sessione reindirizza a `/login` (3 esecuzioni). Più un terzo test con `if (length > 0)` senza `else` | #3917 più ampia del previsto — corretta |
| 2026-09-07 | §F «UX-00c e UX-11 sono due issue» | **Decidevano lo stesso indirizzo.** L'opzione raccomandata da #3918 (redirect `/settings` → `/profile`) confliggeva inoltre con `gotoChecked` di #3917, che vieta i redirect: #3940 avrebbe fallito | #3918 **chiusa**, fusa in #3938 |
| 2026-09-07 | §A.5 «l'unico riferimento esposto è testo, non cliccabile» | Vero, ma **sottovalutato**: sono 5 occorrenze su 3 chiavi in Termini e Privacy, e indicano come esercitare un diritto. Estratte in #3946, corrette | PR #3949 |
| 2026-09-07 | — *(non previsto dal piano)* | Le versioni **IT ed EN della privacy policy divergono** su tre affermazioni sostanziali (infrastruttura UE, revoca del consenso, DPA/audit rights). Non colmata: richiede verifica di fatti non desumibili dal codice | #3948 aperta |
| 2026-09-07 | §B «contrasto: nessun difetto noto sulle entity color» | Riparato il gate, sono emerse violazioni reali: `getEntityToken()` restituiva il colore **base** come colore del testo (toolkit 4,29:1 · agent 4,26:1 contro 4,5). Corretto alla radice; **130 occorrenze** dello stesso pattern scritte a mano restano fuori dalla primitiva | dato passato a #3922 |

### Nota di metodo

Quattro rettifiche su cinque vanno nella stessa direzione: **il repository conteneva già più di quanto il piano assumesse**. È coerente con §A.2 (design system maturo) ed è un argomento a favore dello STEP B del loop — ispezionare il codice prima di implementare — piuttosto che fidarsi della stima scritta in fase di planning.

La quinta va nella direzione opposta e vale come monito: dove il piano dava un difetto per circoscritto (una route sbagliata), ce n'erano tre della stessa famiglia. Un gate cieco tende ad esserlo in più modi contemporaneamente, perché nessuno dei modi produce un segnale.
