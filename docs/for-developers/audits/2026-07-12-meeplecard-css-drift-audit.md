# Audit: MeepleCard family — barriere CSS/funzioni al porting mockup→app

**Data:** 2026-07-12
**Trigger (domanda utente):** timore che la MeepleCard family (creata come entità UI, con layout aggiunti e tolti) contenga classi CSS o funzioni che rendano difficile la trasformazione `admin-mockups/` → pagine app.
**Metodo:** panel spec-panel — discovery a 5 dimensioni (family-inventory, parallel-families, css-layers, friction-functions, mockup-drift) + critique di 4 esperti (Fowler, Nygard, Wiegers, Adzic). Evidence chiave ri-verificate sul repository (branch `feature/issue-2421-g5a-wire-up-v3`) prima della sintesi. **4 claim cardine ri-verificate indipendentemente in post-sintesi** (valori `--c-game`, import-order in `layout.tsx`, 26 ref stale in `component-registry.ts`, assenza di `token-bridge.css`) — tutte confermate.
**Relazione con DS-17 (#2063):** questo audit è un sotto-caso applicato della drift mockup→app. I finding mappano direttamente su **DS-17 CRIT-9 (Nygard, token bridge)** e **DS-17 MAJOR-4 (Fowler, componenti reimplementati)** — con la differenza che qui il debito vive DENTRO la component-family, non solo nel bridge CSS.

---

## 1. Executive summary + verdict score

Sì, il timore è fondato e le barriere sono concrete e verificate sul codice, non cosmetiche. Le tre più gravi:

1. **Nessun componente card canonico.** Coesistono 3 famiglie card parallele non convergenti e ≥5 renderer per la sola entità "game". `MeepleCardGame` reimplementa a mano cover/stelle/badge con docstring esplicito `Mirrors mockup sp3-shared-games.jsx lines 208-274` invece di comporre `MeepleCard`. Quando un mockup mostra "una card", il default per lo sviluppatore diventa copiare l'HTML.
2. **Colore-entità senza single source of truth.** `--c-game` ha 4 valori di lightness (45%/38%/39%/32%) e il valore vincente è deciso dall'ordine di import CSS, non da una decisione: il valore mockup-faithful (L=45%) è sovrascritto a runtime da L=38%.
3. **~1.020 LOC di feature morte** (Flip/Drag/Carousel/Hover/Swipe/EntityTable) esportate dal barrel ma cablate a ZERO in produzione, con 11/36 props che le alimentano scartando input silenziosamente. Sono esattamente i "layout aggiunti e tolti" temuti.

Trasversalmente, il porting oggi è un **guasto silenzioso per costruzione**: compila verde e renderizza sbagliato, perché nessun esempio eseguibile lega il valore del mockup al valore reso. Le mappe di orientamento (`component-registry.ts`, `CLAUDE.md`) mentono attivamente sul filesystem.

**Verdict score: 3/10.** Le fondamenta esistono (dispatcher, parts atomici, un gate AST autentico), ma mancano i tre prerequisiti di un porting affidabile: token source-of-truth unica, componente card canonico, esempi eseguibili mockup↔render. Non è 1-2 perché il debito è delimitato, ~40% safe-delete, rimediabile senza riscrittura.

---

## 2. I tre problemi strutturali

### 2.1 Famiglie parallele — nessun componente canonico

Tre famiglie "card" strutturalmente parallele che NON convergono su un unico primitivo:

| Famiglia | File chiave | Natura | Token usati |
|---|---|---|---|
| `meeple-card/` | `MeepleCard.tsx` dispatcher 6-variant | dispatcher polimorfico + ~20 parts | `var(--mc-*)` + `entityHsl()` JS-inline |
| `shared-games/meeple-card-game.tsx` | `MeepleCardGame` (std-alone) | reimplementa cover/stelle/badge/chip a mano | `hsl(var(--c-*))` legacy |
| `extra-meeple-card/` | `ExtraMeepleCard.tsx` + ~55 file | detail-card 600×900 con drawer/tab | Tailwind palette hardcoded (16 file con `eslint-disable`) |

Evidence verificata:
- `find src/components -name 'Meeple*Card.tsx'` → **21 adapter**.
- `meeple-card-game.tsx:4` docstring `Mirrors mockup sp3-shared-games.jsx lines 208-274`; righe `:105/:106/:113` usano `hsl(var(--c-game)/…)` inline — reimplementazione manuale confermata.
- `≥5 renderer per 'game'`: `MeepleGameCard.tsx`, `MeepleGameCatalogCard.tsx`, `MeepleLibraryGameCard.tsx`, `MeepleCardGame` (std-alone), `GameExtraMeepleCard.tsx` (expanded).
- 3 tabelle colori entity con lightness divergente: `meeple-card/tokens.ts:25` game L=39%, `extra-meeple-card/shared.tsx:22` game L=45% — verificato.

**Impatto porting:** un mockup di "card gioco" può corrispondere a qualsiasi delle 3 famiglie; la scelta sbagliata o un sesto nuovo componente è l'esito di default. È la MAJOR-4 di DS-17 (componenti reimplementati) e la causa-radice dell'attrito.

### 2.2 Feature morte — i "layout aggiunti e tolti"

`features/index.ts` esporta 6 compositori (`FlipCard/HoverPreview/Carousel3D/DragHandle/SwipeGesture/EntityTable` + `FlipBack`); **grep nelle varianti = 0 match** (verificato). Wiring produzione = 0.

| Elemento morto | Evidence | LOC ~ |
|---|---|---|
| 7 features non cablate | `features/index.ts` esporta tutto; 0 import in `variants/` | ~1.020 (EntityTable 442 + FlipBack 274 + …) |
| 11/36 props orfane | `types.ts:85-161` — flippable/flipBackContent/flipTrigger/draggable/onDragStart/onDragEnd/ownership/lifecycle/customColor/coverLabels/onWishlistToggle | — |
| Variante `focus` orfana | `MeepleCard.tsx:20` (verificato nel variantMap), 0 call-site prod (verificato) | 73 (FocusCard) |
| Compound API `MeepleCards.*` | `compound.tsx`, 0 uso prod | — |
| Modello ownership/lifecycle | `status-adapter.ts` esportato, 0 consumatori; `LifecycleStateBadge`/`OwnershipBadge` cablati in 0 varianti (verificato: 0 letture) | ~230 |

**Impatto porting:** TypeScript accetta `flippable`/`draggable`, a runtime non succede nulla → bug silenzioso difficile da diagnosticare. Il barrel/autocomplete fuorviano attivamente.

### 2.3 Layer CSS + funzioni di trasformazione

**CSS:** `token-bridge.css` NON esiste (verificato: glob → not found) ma `CLAUDE.md` + il commento in `design-tokens-canonical.css:10` lo citano come attivo. NB: `layout.tsx` (righe 22-28) NON lo importa — importa `design-tokens-canonical.css` poi `globals.css`. `--c-game` definito 3 volte con L divergente (verificato):
- `design-tokens-canonical.css:22` → `25 95% 45%`
- `globals.css:562` → `25 95% 38%` (commento: `4.82:1 ✅ (#587, gate #601, then #807)` — fix di contrasto AA)
- `design-tokens.css:364` (v1) → 45%, `:383` (v2) → 38%
- + `tokens.ts:25` JS → L=39%, `:81` text → L=32%
- + dark variants (`:220`/`:721`/`:577`) → L=58%

`layout.tsx:22-23` importa canonical PRIMA di globals → **last-wins, L=38% sovrascrive L=45% mockup-faithful** (verificato l'ordine di import). Il 38% è nato come fix AA (#587), quindi la card è "corretta" per contrasto ma "infedele" al mockup, e nessuno dei due valori è dichiarato come canonico.

**Funzioni:** la family concentra ~40 funzioni di trasformazione dati assenti nei mockup statici (chip hardcoded `>8< >4< KB|Agent`): 10 detail-hook con fetch accoppiato, 12 `mapTo*`, 2 famiglie PARALLELE di build*Connections, 3 status-mapping, ~10 funzioni token che RI-DERIVANO colori in JS invece di leggere le CSS-var.

**Impatto porting:** portare un mockup non è copia-classi. Richiede tradurre a mano ogni `.e-tint` mockup in stringhe arbitrarie `bg-[hsl(var(--c-game)/0.12)]` (alpha 0.12 vs 0.10 → drift), reinventare tutta la logica di mapping, e scegliere tra namespace token divergenti.

---

## 3. Barriere prioritizzate

| Severity | Barriera | Evidence (file:line) | Impatto porting | Azione |
|---|---|---|---|---|
| CRITICAL | Nessun componente card canonico | `meeple-card-game.tsx:4`; `find Meeple*Card.tsx`=21 | default = copiare HTML → divergenza per pagina | Decision-table DTO×contesto→componente; MeepleCardGame → adapter sottile |
| CRITICAL | Colore-entità 4 valori L, vincente per import-order | `canonical:22`=45%, `globals:562`=38%, `tokens.ts:25`=39% | render non prevedibile dal mockup; card diverge in dark | 1 sola source-of-truth; decidere 45% vs 38% esplicitamente |
| CRITICAL | ~1.020 LOC feature morte esportate | `features/index.ts` (0 wiring in variants); `types.ts:85-161` | props accettate, scartate a runtime = bug silenzioso | Safe-delete via Serena (barrel+test seguono) |
| CRITICAL | Nessun esempio mockup↔render (guasto silenzioso) | 0 `.fidelity.json` per 34 mockup; contract test = `expectTypeOf` | porting non verificabile visualmente | Golden token test + acceptance test comportamentali |
| MAJOR | Registry punta a cartella inesistente | `component-registry.ts` 26 ref `meeple-card-features/` (verificato) | seguire il registry = import irrisolti | Ripuntare/rimuovere + test che valida ogni importPath |
| MAJOR | Doppio modello di stato, uno morto | `status-adapter.ts` 0 consumatori; ownership/lifecycle 0 letture (verificato) | si sceglie il modello pulito e non rende nulla | Eleggere CardStatus O ownership/lifecycle, uccidere l'altro |
| MAJOR | 2 build*Connections ordine invertito | `nav-items/buildGameConnections` KB\|Agent vs `connection-bar` Agent\|KB | UI incoerente tra porting | Consolidare in 1 famiglia + snapshot ordine-slot |
| MAJOR | 3 status-mapping, enum incompleto | `use-kb-detail.ts:56` omette chunking/embedding/uploading | stesso PDF, stati diversi per pagina (correttezza) | Modulo condiviso + test copertura enum |
| MAJOR | Variante `focus` orfana | `MeepleCard.tsx:20` (verificato); 0 call-site prod | comportamento su dati reali mai validato | Promuovere+documentare o rimuovere |
| MAJOR | agent-*.css globale scope-/agents | `layout.tsx:25-27`; ~0 consumatori token | @media scavalca theme mockup-faithful ovunque | Scopare a segmento `/agents` |
| MINOR | Drift documentale CLAUDE.md | `CLAUDE.md § Card Components`; `MeepleCard.tsx:20` | porter si fida di doc stale → reimplementa | Aggiornare a 6 varianti; rimuovere token-bridge.css |
| MINOR | Mockup con host BGG banditi | `real-app--game-catalog-card.html:307/339` | porting fedele viola freeze #2123 | Rigenerare con placeholder deterministico |

---

## 4. Findings dettagliati per dimensione

### 4.1 family-inventory

La cartella `meeple-card/` (89 file, ~6.576 LOC) è un dispatcher polimorfico a 6 varianti con un sottobosco di codice morto stratificato. Parti realmente vive: `grid` (28 call-site), `compact` (25), `list` (18); marginali: `featured` (2-3), `hero` (1); **orfana: `focus` (0 call-site prod)**.

- **CRITICAL** — 7 features (~1.020 LOC) mai cablate: `features/index.ts` esporta tutto, 0 import in `variants/` (verificato). Git churn conferma: commit `9cc282fa5` FlipBack "+ 8 entity flip backs" poi mai integrate.
- **CRITICAL** — Styling su 3 sistemi paralleli: `var(--mc-*)` (`GridCard.tsx:50`, file legacy `design-tokens.css:520-529`), `entityHsl()` hsl JS-inline (`GridCard.tsx:46-51`, `HeroCard.tsx:56/73`), `statusColors` hex grezzi (`tokens.ts:169-180`). Nessuno usa i token semantici canonici.
- **MAJOR** — Modello badge a due-assi costruito ma cablato in 0 varianti: `status-adapter.ts:17-60` esportato ma 0 consumatori; `LifecycleStateBadge.tsx`/`OwnershipBadge.tsx` importati da nessuna variante prod.
- **MAJOR** — Variante `focus` orfana (`types.ts:19`, `MeepleCard.tsx:20`); solo `heading-level.smoke.test.tsx:31` la parametrizza.
- **MAJOR** — 11/36 props non pilotano rendering (`types.ts:85-161`).
- **MAJOR** — `component-registry.ts:623-1023` referenzia 27+ importPath verso `meeple-card-features/` inesistente.
- **MINOR** — Status frammentato: `StatusBadge` (FeaturedCard) vs `CardFooter` (GridCard, commento `#1856 DEC-5`).
- **MINOR** — Compound API `MeepleCards.*` con 0 uso prod.

### 4.2 parallel-families

3 famiglie card non convergenti; ≥5 renderer per "game"; 3 tabelle colori entity duplicate con lightness divergente.

- **CRITICAL** — `MeepleCardGame` reimplementa cover/stelle/badge/chip (`meeple-card-game.tsx:55-76` Stars locale duplica `parts/Rating.tsx:6-30`; `:111-138` cover duplica `parts/Cover.tsx:39-95`).
- **CRITICAL** — 3 sorgenti divergenti per palette entity: `tokens.ts:24-36` game L=39%, `entity-tokens.ts:73-83` classi Tailwind, `extra-meeple-card/shared.tsx:21-100` game L=45% (commento "from MeepleCard v2 tokens" ma i numeri NON coincidono).
- **MAJOR** — `extra-meeple-card/` è terza implementazione indipendente (non importa nulla da `../meeple-card/`); 16 file con `eslint-disable local/no-hardcoded-color-utility`; naming crea falsa parentela.
- **MAJOR** — Nessuna scelta canonica: ≥5 renderer per "game"; docstring rivelano storia stratificata (`#4041`, `#3334`, `#596`).
- **MAJOR** — CLAUDE.md drift (5 varianti vs 6, path/props errati).
- **MINOR** — `meeple-card-game` usa `hsl(var(--c-*))` mentre `meeple-card` usa `--mc-*`: token mixing tra famiglie sorelle.

### 4.3 css-layers

Il layer CSS è la barriera principale. Tre famiglie di token `--c-*` concorrenti caricate insieme; l'ultima vince.

- **CRITICAL** — `--c-game` L=45%/38%/38% su `canonical:22` / `globals:562` / `design-tokens.css:383`; import order in `layout.tsx:22-23` → 45% overridden da 38% (verificato).
- **CRITICAL** — `token-bridge.css` NON esiste (verificato: glob → not found) ma citato in `CLAUDE.md` + commento `design-tokens-canonical.css:10`. 34 occorrenze legacy `var(--bg-base|--gaming-|--nh-|--e-)` residue.
- **MAJOR** — 411 usi inline `hsl(var(--c-*))` / 123 file; 579 classi arbitrarie `*-[hsl(...)]` / 190 file bypassano le utility semantiche. Il mockup usa `.e-tint`/`.e-bg` (`canonical:268-271`), la app le re-implementa inline con alpha diverso.
- **MAJOR** — `tokens.ts` quarto layer CSS-in-JS a runtime, light-theme-only (`:50`), diverge dai CSS.
- **MAJOR** — `agent-*.css` (~260 LOC) globale con ~0 consumatori; `agent-theme.css:56-60` forza dark via `@media prefers-color-scheme`.
- **MINOR** — `--c-game-text` duplicato-triplicato mantenuto a mano.
- **MINOR** — `design-tokens.css` definisce sia `--e-*` (Tailwind feed) sia `--c-*` v1/v2/dark: doppio sistema entity nello stesso file.

### 4.4 friction-functions

~40 funzioni di trasformazione assenti nei mockup statici → reinventate ad ogni porting. Il pericolo non è la singola funzione ma i DOPPIONI DIVERGENTI.

- **CRITICAL** — 2 famiglie build*Connections con ordine-slot invertito (KB\|Agent vs Agent\|KB) e output-type incompatibile (`ConnectionChipProps` vs `ConnectionPip`), entrambe live (17 vs 4 consumatori).
- **CRITICAL** — Stesso lifecycle-state con colori diversi: `tokens.ts:168-181` statusColors completed viola `#f3e8ff` vs `LifecycleStateBadge.tsx:34-58` completed verde `hsl(152 76% 40%)`.
- **MAJOR** — 3 catene status-mapping non riconciliate; `use-kb-detail.ts:56` omette chunking/embedding/uploading presenti in `drawer-helpers.ts`.
- **MAJOR** — 10 detail-hook accoppiano fetch+mapping alla card (`use-game-detail.ts:34` Promise.all su 3 endpoint); `use-entity-detail.ts:27` è stub no-op che ritorna sempre `{data:null}`.
- **MAJOR** — La card RI-DERIVA i colori in JS (`entityHsl` da entityColors L=39%; `entityHslText` L=32% light-only) invece di leggere le CSS-var → diverge in dark.
- **MINOR** — 2 mappe icona per entity (emoji `tokens.ts:113-124` vs lucide `entity-icons.tsx:23-25`) — trappola di autocomplete `entityIcon` vs `entityIcons`.
- **MINOR** — `useConnectionSource.ts:22-24` ramo `manaPips` legacy ritorna `items:[]` — scarta silenziosamente.

### 4.5 mockup-drift

Componente sovra-specificato: i mockup descrivono 32 feature-file + drawer 6-tab, il codice reale ha 6 varianti e la maggior parte delle feature avanzate non è wired in prod.

- **CRITICAL** — 4 vocabolari token-entità paralleli con lightness divergente (`--e-*` split-channel L=45%, `--c-*` triplet, `--mc-*` RGBA privati, JS entityColors L=39%).
- **CRITICAL** — I mockup auto-documentano un componente ~2× più grande del reale: `summary--feature-matrix.html:249-266` dichiara 32 file + props (`showTagStrip`, `sessionScoreTable`…) che `types.ts` NON contiene; `real-app--unused-features.html:323` conclude "~40% delle feature non usate da nessun adapter".
- **MAJOR** — FlipCard/Carousel3D/EntityTable consumate SOLO da `app/(public)/dev/meeple-card/page.tsx`.
- **MAJOR** — Variante `focus` senza alcun mockup di riferimento (drift bidirezionale).
- **MAJOR** — 3 famiglie card parallele.
- **MAJOR** — Classi mockup non combaciano con utility codice: `.mc/.mc-list/.entity-badge` vs `border-[var(--mc-border)]` — 3 grammatiche stilistiche diverse.
- **MINOR** — Mockup puntano a host BGG hard-banned (`real-app--game-catalog-card.html:307/339`).
- **MINOR** — Drift CLAUDE.md (path import, 5 vs 6 varianti, token-bridge.css).

---

## 5. Panel critique per esperto

### 5.1 Martin Fowler — Architecture & Component Design

**Verdict:** il debito non è "una card scritta male", è una FAMIGLIA senza componente canonico. La causa-radice non è sciatteria ma evolutionary design mai potato: ogni feature (#287/#596/#3334/#4041) e ogni fix AA (#587→#601→#807→#1094) ha aggiunto un layer senza rimuovere il precedente. ~40% del rumore è safe-delete a rischio ~0.

**Top concerns:** nessun componente canonico (5 renderer per game) · colore-entità senza SSOT (45/38/39/32%) · prop-surface e barrel mentono (11/36 props morte, 7 feature 0-wired) · registry punta a cartella cancellata (26 voci) · 2 build*Connections invertite · doppio modello di stato con nuovo cablato a 0.

**Priority actions:** FASE 0 safe-delete rumore verificato → FASE 1 bonifica registry + test importPath → FASE 2 componente canonico + decision-table → FASE 3 collasso token colore → FASE 4 consolidamento connections/status → FASE 5 allineamento doc + rinomina extra-meeple-card.

### 5.2 Michael Nygard — Production Reliability & ACL

**Verdict:** il debito non è "troppo codice", è ASSENZA DI CONFINI GOVERNATI. Il bridge token nato temporaneo (CRIT-9 DS-17) non è stato rimosso: è stato triplicato e lasciato senza custode. Oggi il valore-token vincente è deciso dall'ordine di import, non da una decisione. Il porting non fallisce con errore: compila verde e renderizza sbagliato — la categoria di guasto più costosa.

**Top concerns:** valore-token vincente per import-order (guasto silenzioso) · mappe che mentono sul filesystem senza gate · 7-8 feature esportate cablate a 0 (bomba di diagnostica) · 3 famiglie + 5 renderer senza confine · 3 status-mapping con enum incompleto (incoerenza osservabile) · agent-*.css scope promosso a globale · nessun test cross-file sui token duplicati.

**Priority actions:** STABILIRE IL GATE PRIMA DI CONSOLIDARE (test cross-file + lint anti-ridefinizione) → eleggere 1 SSOT token con decisione esplicita 45-vs-38 → riallineare le mappe e difenderle → bonificare morto come bundle atomico DOPO il registry → unificare status-mapping (correttezza) → decision-table card + lint anti-nuovo-renderer → scopare agent-*.css → migrare varianti ai token canonici SOLO dopo i gate.

### 5.3 Karl Wiegers — Requirements Quality

**Verdict:** il debito è primariamente un DIFETTO DI REQUISITI. Non esiste un requisito verificabile "data variante V e prop P, la card DEVE rendere A". I contract test usano `expectTypeOf` (esistenza del tipo), MAI asserzioni di rendering. Debito ALTO ma RIMEDIABILE a basso costo: la cura è scrivere i contratti mancanti, non riscrivere.

**Top concerns:** nessun contratto prop→rendering verificabile · nessuna definizione misurabile di "feature/variante/prop USATA" · doppio modello di stato senza oracolo · prop-surface priva di precondizioni prop×variante · 2 connection-builder senza contratto d'ordine · drift CLAUDE.md come pseudo-spec.

**Priority actions:** scrivere la MATRICE DI ACCETTAZIONE prop×variante versionata → convertire contract test da `expectTypeOf` a acceptance comportamentali → definire criterio operativo di "feature ATTIVA" (≥1 call-site prod AND ≥1 acceptance test) → eleggere 1 modello di stato → fissare ordine-slot come snapshot test → ancorare CLAUDE.md alla matrice con test di tracciabilità.

### 5.4 Gojko Adzic — Specification by Example & Living Documentation

**Verdict:** fallimento di specifica. Non esiste un solo esempio eseguibile che affermi "questo mockup deve rendere così". I 34 mockup meeple-card NON hanno companion `.fidelity.json` (mentre il repo ne ha 147 altrove). La disciplina giusta esiste già su UN asse: `call-site-coverage.test.tsx` è living documentation autentica (AST-gate) — va estesa all'asse token/mockup.

**Top concerns:** zero esempi che leghino valore mockup↔render · funzioni-adapter invisibili nel mockup reinventate ad ogni porting · feature morte = specifica-fantasma · CLAUDE.md fuori sync · tokens.ts quarto layer light-only de-sincronizzato senza test.

**Priority actions:** creare un GOLDEN TOKEN TEST (legge `admin-mockups/design_files/tokens.css`, asserisce coincidenza con canonical+globals+entityColors — deve fallire OGGI sul drift) → estendere il pattern call-site-coverage a un gate anti-reimplementazione (AST) → decision-table eseguibile come table-test → safe-delete specifica-fantasma + test importPath registry → living doc derivata (test varianti==variantMap; `.fidelity.json` per i 34 mockup) → consolidare 3 status-mapping con test copertura enum.

---

## 6. Roadmap raccomandata

Punto di convergenza dei 4 esperti: **stabilire i gate PRIMA di consolidare** (Nygard/Adzic) — altrimenti si ripaga il debito e si riaccende. Fowler aggiunge: **safe-delete del rumore prima** per liberare chiarezza. Sequenza combinata:

### Quick wins (rischio ~0, sblocco immediato)

| # | Azione | Effort | Collega a |
|---|---|---|---|
| QW1 | Bonificare `component-registry.ts`: ripuntare/rimuovere 26 voci `meeple-card-features/` + test che valida ogni importPath | S | DS-17 (mappa porting) |
| QW2 | Rimuovere da `CLAUDE.md` + commenti CSS ogni riferimento a `token-bridge.css`; correggere 5→6 varianti | S | Sez. 7 |
| QW3 | Safe-delete via Serena: 7 features morte (~1.020 LOC) + 11 props orfane + variante `focus` + `compound.tsx` + strato ownership/lifecycle (se CardStatus resta canonico) — bundle atomico DOPO QW1 | M | Fowler FASE 0 |
| QW4 | Scopare `agent-*.css` dal root layout al segmento `/agents` (+`/editor`) | S | Nygard |

### Interventi strutturali

| # | Azione | Effort | Collega a |
|---|---|---|---|
| ST1 | GATE token: test cross-file che asserisce uguaglianza `--c-{entity}` tra sorgenti superstiti + lint anti-ridefinizione `--c-*` fuori dal file canonico | M | DS-17 CRIT-9 |
| ST2 | Golden token test (legge `admin-mockups/design_files/tokens.css`); risolvere 45%-vs-38% con decisione esplicita (se serve AA, applicarlo NEL mockup source) | M | DS-17 CRIT-9, DS-16 |
| ST3 | Collassare token in 1 SSOT: rimuovere `:root --c-*` da `globals.css:559-573` e v1/v2 da `design-tokens.css`; far leggere `entityHsl` dalle CSS-var o usare `bg-entity-*` theme-aware | L | DS-16 (rimozione bridge) |
| ST4 | Eleggere componente canonico + decision-table DTO×contesto→componente accanto a `v2-migration-matrix.md`; MeepleCardGame → adapter sottile; lint anti-nuovo-renderer | L | DS-17 MAJOR-4, DEC Storybook |
| ST5 | Matrice acceptance prop×variante + convertire contract test a acceptance comportamentali (render+assert DOM) | M | Wiegers |
| ST6 | Consolidare 2 build*Connections in 1 famiglia + snapshot ordine-slot; unificare 3 status-mapping in modulo condiviso con test copertura enum (chunking mancante → rosso) | M | correttezza |
| ST7 | Gate AST anti-reimplementazione (fallisce se nuovo `*Card` rende cover/stelle/badge inline invece di comporre MeepleCard) | M | Adzic |
| ST8 | Migrare le 6 varianti da `--mc-*`/`entityHsl` inline ai token semantici canonici — ULTIMO passo, contro un bersaglio ormai stabile | L | DS-16 |

**Nota di sequenza (incertezza):** l'effort è indicativo (S/M/L, non stimato in giorni). ST3 e ST8 dipendono dal grado di accoppiamento CSS non ancora misurato esaustivamente (411 usi inline + 579 classi arbitrarie); un audit di migrazione dedicato è consigliato prima di committarsi a una stima.

---

## 7. Drift documentale CLAUDE.md da correggere

| Punto | Stato attuale (doc) | Realtà codice (verificata) | Correzione |
|---|---|---|---|
| Varianti | "Variants: grid \| list \| compact \| featured \| hero" (5) | `MeepleCard.tsx:20` variantMap include `focus` (6) | Aggiornare a 6, o rimuovere `focus` in QW3 e restare a 5 — coerentemente |
| Esempio props | `<MeepleCard entity variant title imageUrl rating ratingMax>` (6 campi) | `types.ts:85-161` ha 36 campi (connections, status, attribution, headingLevel…) | Esempio realistico o nota "props API completa in types.ts" |
| token-bridge.css | Citato come tech-debt attivo con ~120 consumatori | File NON esiste (glob → not found); aliasing in globals.css/design-tokens.css | Rimuovere riferimento o correggere in "aliasing in globals.css" |
| extra-meeple-card | Non menzionato accanto a MeepleCard | Terza famiglia (~55 file) con naming che suggerisce falsa parentela | Aggiungere puntatore + tabella "quale card per quale mockup" |
| 4 vocabolari token | Non avvertito | `--e-*` / `--c-*` / `--mc-*` / JS entityColors | Nota finché DS-16 non li consolida |

**Raccomandazione (Adzic/Wiegers):** non "aggiornare a mano" (invecchierà di nuovo) ma **derivare la doc**: un test che asserisce `elenco-varianti-in-CLAUDE.md == keys(variantMap)` trasforma la doc in living documentation che non può driftare senza rompere il build.

---

*Generated with Claude Code — spec-panel critique (Fowler · Nygard · Wiegers · Adzic), 2026-07-12. Evidence chiave ri-verificate indipendentemente sul branch `feature/issue-2421-g5a-wire-up-v3`.*
