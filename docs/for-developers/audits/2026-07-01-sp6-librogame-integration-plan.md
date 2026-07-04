# SP6 Libro-Game — FE/BE integration plan (2026-07-01)

Deriva dalla demo Claude Design SP6 ([#1888](https://github.com/meepleAi-app/meepleai-monorepo/issues/1888), 49 gap · gap report `2026-06-30-claude-design-gap-report-sp6.md`) + un workflow di ricognizione FE/BE/issue eseguito il 2026-07-01.

## Finding chiave

Il libro-game **non è greenfield**: il BE è ricco e in gran parte shipped, il FE ha route reali per molte pagine, e **le issue implementabili sono quasi tutte CHIUSE** (lavoro già fatto). L'**unica issue OPEN** che copre lavoro residuo è l'umbrella **[#2619](https://github.com/meepleAi-app/meepleai-monorepo/issues/2619)** (6 HIGH gap UI↔dominio). Quindi "usare quanto prodotto per integrare FE/BE" = **wiring dei pezzi mancanti + adozione FE del modello di dominio**, non costruzione da zero.

## Matrice di integrazione (15 pagine)

Legenda: FE/BE = `have` / `partial` / `missing` · P0/P1/P2 = priorità.

| # | Pagina | FE | BE | Issue riusabili (stato) | Azione | Pri |
|---|--------|----|----|------------------------|--------|-----|
| 1 | library-search | partial (embedded in `/gamebook/upload`) | have (`GET /games?search`) | #1047 (closed) | facet "libro" + eventuale route dedicata | P2 |
| 2 | game-detail (libro) | **have** (`LibroGameDetailView`) | partial (join games+books) | #1486/#1552/#1288 (closed) | flag `isGamebook` BE + tab house-rules | P2 |
| 3 | onboarding (book upload) | missing (mock only) | **missing endpoint** | #786/#869 (closed) | ✅ **fatto BE (PR #2624)**; FE prereq-stepper + book-manager 1..N | **P0** |
| 4 | setup-wizard | have (`CampaignSetupDrawer`) | have (`POST /campaigns`) | #1486 (closed) | persistere roster/#15 promotion | P1 |
| 5 | setup-chat | missing (mock only) | partial (game-scoped chat) | — (net-new) | chat campaign-scoped + FE surface | P2 |
| 6 | play-session | have (`GamebookPlayShell`) | have (progress/history) | #1387/#747 (closed) | #10 LIVE badge + #14 Ora-inizio (→ #2619) | P1 |
| 7 | translate-viewer | **have** | **have** (SSE) | #1559/#1560/#836 (closed) | glossary-pill trigger (vedi §quick-win) | P1 |
| 8 | encounter-cheatsheet | **have** | **have** (#1520) | #1484/#1520 (closed) | — (completa) | — |
| 9 | session-end (3-way) | missing (mock only) | **missing** (no close SM) | — (net-new, HIGH #8) | `Complete/Abandon` campaign commands + FE modal | P1 |
| 10 | resume-picker | have (`GamebookResumeShell`) | partial (no LastPlayedAt/Status) | #835/#954/#1388 (closed) | `LastPlayedAt`+`Status` su aggregate (#11/#14 → #2619) | P1 |
| 11 | glossary-editor | partial (**modal completo, route-orphaned**) | partial (no DELETE, no contexts[]) | #952 (closed) | wire pill→modal + DELETE endpoint + contexts[] migration | P1 |
| 12 | quota-credits | have (inline `/gamebook`) | partial (generic usage) | #953/#869 (closed) | meter gamebook-credit dedicato | P2 |
| 13 | error-states | missing (mock only) | have (contratto errori) | #833/#834 (closed) | primitiva error-banner riusabile | P2 |
| 14 | game-night-storyboard | missing | partial (diary flat) | **#2619 (OPEN)** | render aggregato GameNight + projection storyboard | **P0** |
| 15 | house-rule | partial (surface generico) | **have** | #2492 (closed) | tab house-rules nel libro detail | P2 |

## Quick-win (basso rischio, alto valore)

1. ✅ **[FATTO] BE — GameBook write endpoints** (PR **#2624**, aperta): `POST/PUT/DELETE /gamebook/books` wired ai comandi orfani già testati. Sblocca la creazione di GameBook (page 3). Personal-book scope. **Da mergiare dopo CI verde + review.**
2. **BE — `DELETE /gamebook/campaigns/{id}/glossary/{entryId}`** (page 11): comando+handler+endpoint nuovi ma piccoli (mirror del PUT esistente). Sblocca la rimozione termini nel glossary editor.
3. **FE — `LastPlayedAt` + `Status` su `GamebookCampaignSession`** (page 10): campo aggregate + migration; abilita ordering recency + filtro completati nel resume-picker (oggi `UpdatedAt` è sporcato da rename/glossary).

## NON quick-win (richiedono più lavoro / decisioni)

- **glossary-pill trigger** (page 7/11): il `GlossaryEditorModal` è **completo** ma route-orphaned; però `TranslationPane` **non renderizza le glossary pill tappabili** (0 handler) → serve implementare highlight+click del termine nel paragrafo tradotto PRIMA di poter montare il modal. Non è un wiring banale.
- **session-end 3-way** (page 9, HIGH #8): `GamebookCampaignSession` non ha `Status`/`StartedAt`/`CompletedAt` né state machine di chiusura (solo SoftDelete). Serve modellare `Complete/Abandon/Save` + FE modal. Architetturale.
- **I 6 HIGH gap (#2619)**: rendering aggregato GameNight (spine #1/#15/#8/#10), identity lock, resume semantics (#11/#14, **decisione product**), GameBook 1..N FE. Vanno spezzati in sub-issue.

## Issue: riuso vs net-new

- **NON rifilare** (già CLOSED-shipped): #1559/#1560 (translate), #1484/#1520 (encounter), #2492 (house-rule), #835/#954 (resume UI), #869 (gamebooks/quota BE), #1388 (progress BE), #834/#833 (EXIF/resilience), #747 (paragraph lookup), #1392 (GameRef), #836 (legacy cleanup), #952/#953 (glossary/quota FE).
- **Net-new da filare sotto #2619**: (a) session-end 3-way close (BE close-SM + FE), (b) glossary contexts[] BE migration + DELETE, (c) `LastPlayedAt`/`Status` su campaign, (d) i 6 HIGH come sub-issue impl (GameNight aggregate render = spine).

## Prossimo passo consigliato

Mergiare **#2624** (post-CI), poi quick-win #2 (glossary DELETE) e #3 (`LastPlayedAt`/`Status`) — entrambi BE contenuti e sicuri. Le pagine 9/11/14 e i 6 HIGH sono i pezzi grossi da spezzare in sub-issue di #2619 con decisione product sulle resume-semantics (#11/#14).
