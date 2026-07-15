# Claude Design — Gap Report Mobile (SP8 parity + libro-game companion)

**Data**: 2026-07-15
**Issue**: [#1890](https://github.com/meepleAi-app/meepleai-monorepo/issues/1890) — Rerun Claude Design demo post-SP8
**Tool**: claude.ai/design (sessione runtime, operatore-guidata via runbook a 5 turni)
**Trigger**: ✅ soddisfatto — SP8 merged via PR [#1689](https://github.com/meepleAi-app/meepleai-monorepo/pull/1689) (2026-05-29)
**Baseline desktop**: [`2026-06-04-claude-design-gap-report.md`](./2026-06-04-claude-design-gap-report.md) (38 gap)
**Bundle**: `claude-design-bundle/sp8-mobile/` (rebuild via `scripts/build-claude-design-bundle.sh sp8`)
**System prompt + runbook**: [`claude-design-demo-prompts.md`](../workflows/claude-design-demo-prompts.md) § SP8
**Domain model**: [`2026-06-04-gamenight-session-domain-model.md`](../specs/2026-06-04-gamenight-session-domain-model.md) (20 invarianti)

> **Stato**: sessione runtime **ESEGUITA** a viewport 375px (Turn 1 library-mobile → Turn 2 companion → Turn 3 diff/scope → Turn 4 socratic → Turn 5 report). Prototipo React del bundle replayato (non rigenerato). Ogni gap è verificato a runtime; gli ID `A-0x`/`B-0x` sono la numerazione finale, riconciliati con la pre-analisi statica (18 gap) nella § 6. La bozza pre-analisi è stata **superata** da questo esito.

---

## § 0 — Riproducibilità

- **Bundle**: `scripts/build-claude-design-bundle.sh sp8` → `claude-design-bundle/sp8-mobile/`.
- **Sessione**: caricare i file del bundle su claude.ai/design, canvas **375px**, incollare il system-prompt SP8 (primo messaggio), poi i 5 prompt-turno del runbook.
- **Handoff / prova visiva**: [`claude-design-handoff/2026-07-15-sp8-mobile/`](../../../claude-design-handoff/2026-07-15-sp8-mobile/) — 13 screenshot dei turni library-mobile (default → overflow → filtri → bulk → tablet) + README con mapping screenshot→gap.
- **Finding infra (fixato in questa PR)**: i mockup referenziano gli asset come sibling (`href="tokens.css"`) ma lo script li poneva un livello sopra (`mockups/` vs root). Lo script ora **mirrora** `tokens/components/data` dentro `mockups/` così i path relativi risolvono senza intervento manuale.
- **Peso bundle ~720KB** (companion `.html` 146KB): ~3× il target ~250KB. Se satura il context → split in 2 sessioni (A library + B companion).

---

## § 1 — Tabella gap completa (25 gap · runtime)

Categorie: **ROUTE / STATE / CTA / ENTITY / TOKEN** (le "gesture" sono mappate su STATE/CTA). Severity: **CRITICO / IMPORTANTE / MINORE**. Tag: `[runtime-confirmed]` = era nella pre-analisi (A1–A13/B1–B5); `[new]` = emerso solo a runtime.

### 1A — Library mobile (`sp4-library-mobile` · /library)

| # | cat | schermo | descrizione | tag | sev | proposta fix (Turn 4 dove applicabile) |
|---|-----|---------|-------------|-----|-----|----------------------------------------|
| A-01 | ENTITY | tab bar | Tab in-page "Games" collide con voce sidebar "Games" (catalogo, inv #20); amplificato a 375px dove la sidebar collassa | `runtime-confirmed` | CRITICO | Rinomina il **tab** in "I miei giochi" / entity-icon game (`PRIMARY_TABS` label "Games", `jsx:28`); voce sidebar "Games"=catalogo intatta (T4 #1) |
| A-02 | CTA | bulk sheet | Archivia/Tag/Esporta/Rimuovi tutte toast; "Rimuovi" (danger) **senza dialog di conferma** | `runtime-confirmed` | CRITICO | Wire azioni reali + confirm sul danger prima del toast (`onAction` wiring `jsx:648`; danger def `jsx:320,332`) |
| A-03 | STATE | sheet ×3 | Nessun ESC/Back → dismiss; il Back Android uscirebbe dalla route | `runtime-confirmed` | CRITICO | Back-guard **1-livello** (history guard): ESC/Back/gesture chiudono 1 solo sheet (T4 #3+4) |
| A-04 | CTA | detail sheet | "Apri scheda" → `onClose`, non naviga a `/games/[id]` | `runtime-confirmed` | IMPORTANTE | Wire a `/games/[id]` (`jsx:~275`) |
| A-05 | CTA | empty | "+ Aggiungi gioco"→`/library/add` e "Scopri shared"→`/shared-games` = toast | `runtime-confirmed` | IMPORTANTE | Wire alle route reali (`jsx:~535`) |
| A-06 | CTA | error | "Riprova"/"Mostra cache" = toast, nessun retry/cache | `runtime-confirmed` | IMPORTANTE | Wire refetch + lettura cache (`jsx:~515`) |
| A-07 | CTA | permission | "Chiedi invito"/"Torna" = toast | `runtime-confirmed` | IMPORTANTE | Wire invito + nav libreria propria |
| A-08 | STATE | sheet + card | No swipe-to-dismiss (grab decorativo) + conflitto long-press 500ms↔tap su device lenti | `runtime-confirmed` | IMPORTANTE | Swipe-down reale; **cancel-on-move ~10px** + cue di soglia in `useLongPress` (T4 #2) |
| A-09 | TOKEN | shell | `#fff` on-color + `rgba()` overlay/ombre fuori da tokens.css, nonostante il commento "nessun hex hardcoded" (`html:29`) | `runtime-confirmed` | IMPORTANTE | Tokenizza on-color + overlay (eredita #37/#38); correggi il commento |
| A-10 | CTA | header | Hamburger ☰ senza handler = **unico** accesso alla sidebar su mobile (dove vive "Games" catalogo) | `new` | IMPORTANTE | Wire ad apertura drawer nav globale |
| A-11 | STATE | search row | Touch-target <44px: input ricerca e "Filtri" a `height:40px` | `runtime-confirmed` | MINORE | Porta a ≥44px |
| A-12 | STATE | overflow+filtri | Il popover overflow non si chiude all'apertura dei filtri (2 layer sovrapposti) | `new` | MINORE | Disclosure non-stackabili si auto-chiudono all'apertura di un'altra (T4 #3+4) |
| A-13 | STATE | offline | Bottone Filtri inerte SENZA feedback (`!readOnly && …`), mentre `enterBulk` un toast ce l'ha | `new` | MINORE | Toast "non disponibile offline" (`jsx:~486`) |
| A-14 | STATE | bulk sheet | "1 selezionati" (plurale non gestito) | `new` | MINORE | Pluralizzazione condizionale |
| A-15 | STATE | bulk sheet | Label e sub-label sovrapposti (`.bsub` senza `display:block`) | `new` | MINORE | `display:block` sul sub-label (`html:~465`) |
| A-16 | STATE | filtered-empty | "Resetta filtri" muta lo state ma la vista resta (agganciata a `condition`, non ad `activeFilters`) | `new` | MINORE | Deriva la vuotezza da `activeFilters` (`jsx:~460`) |

### 1B — Libro-game companion (`librogame-runthrough-play-session` · s05/06/07)

| # | cat | schermo | descrizione | tag | sev | proposta fix (Turn 4 dove applicabile) |
|---|-----|---------|-------------|-----|-----|----------------------------------------|
| B-01 | CTA | s07 summary | "Scarica PDF", "Riapri campagna", "Torna alla libreria", "Archivia" = placeholder (no handler / solo `reset`) | `runtime-confirmed` | CRITICO | Wire PDF (job server), reopen (lock read-only), nav libreria |
| B-02 | CTA | s06 drawer | `.hy-goto` "→ vai qui" = **76×24px** (misurato), altezza 24<44 su CTA primaria del drawer | `runtime-confirmed` | CRITICO | Porta la riga-CTA a ≥44px (`html:1062`) |
| B-03 | STATE | s05/06/07 dialog | Focus-trap assente in **tutti** i dialog/sheet; ESC solo in 2/4 (jump-confirm, close-dialog); history sheet senza ESC | `runtime-confirmed` | IMPORTANTE | Focus-trap + ESC ovunque + back-guard 1-livello (T4 #3+4) |
| B-04 | TOKEN | s06/07 scrim | 4 scrim `rgba(0,0,0,α)` hardcoded (`.4`/`.5`/`.35`/`.5`), non adattano al dark | `runtime-confirmed` | IMPORTANTE | Tokenizza overlay (`html:956,1010,1025,1295`) — eredita #38 |
| B-05 | STATE | s05 editor | Contatore caratteri assente (solo "Bozza salvata") — viola DoD "~150 char + contatore" | `new` | IMPORTANTE | Counter live accanto a "Bozza salvata" |
| B-06 | STATE | s07 loading | Loading end-campaign = **spinner** (`.ec-spinner`) invece di skeleton (viola brief §2) | `new` | MINORE | Sostituisci con skeleton |
| B-07 | CTA | s07 kebab | "Esporta dati" e "Preferenze" senza handler | `new` | MINORE | Wire o rimuovi |
| B-08 | STATE | s05 offline | Banner "…online●1 in coda" — dot/testo attaccati (manca gap) | `new` | MINORE | Spazio/gap nel markup del banner |
| B-09 | CTA | s07 reopen | "Abbandona" post-close non ha reopen distinto (`outcome==='abandon'` cade in `!isDone`→Riapri) | `runtime-confirmed` | MINORE | **Single-confirm** sul reopen — lock read-only, nessun dato perso (T4 #5) |

**Totale § 1: 25 gap (16 A + 9 B).**

---

## § 2 — Top priorità

**★ GAP HEADLINE** (superficie mancante, **non** fidelity) · ROUTE · shell mobile · effort **L**
Il **flow GameNight/Session non è portato in mobile**: 19/20 invarianti (tutte tranne #20 — incl. drawer-stack, "+ Nuova session", live-immersive, max-1-live, RSVP 5-fasi, tagging) vivono in un flow che SP8 non porta a 375px. Va registrato come **superficie non portata** (prossima wave mobile), non allucinato nei 2 schermi presenti. Dep: definizione `MobileBottomBar` (la 5-tab bar è shell globale; i tab in-page /library sono subordinati — T4 bonus).

1. **Rename tab "Games"** · ENTITY · /library — risolve collisione inv #20, amplificata su mobile · **XS** · deps: nessuna → A-01
2. **Bulk danger senza conferma + azioni toast** · CTA · bulk — perdita dato irreversibile senza guardrail · **M** · deps: backend → A-02
3. **Back-guard 1-livello sui sheet** · STATE · library+companion — su mobile il Back esce dalla route · **M** · deps: history/cascade store → A-03/B-03
4. **`.hy-goto` 24px + touch-target <44** · CTA · s06+/library — CTA primarie non tappabili in modo affidabile · **S** → B-02/A-11
5. **PDF / Riapri / Archivia placeholder** · CTA · s07 — chiusura campagna è dead-end · **M** · deps: PDF job + lock → B-01
6. **cancel-on-move long-press** · STATE · /library — falsi positivi su scroll/device lenti · **S** → A-08
7. **Overlay/scrim + on-color non-token** · TOKEN · A+B — rompono il dark, eredita #37/#38 · **S** → A-09/B-04
8. **Contatore caratteri diary** · STATE · s05 — viola DoD · **XS** → B-05

---

## § 3 — Mobile-specific vs shared (diff vs baseline 38 gap)

**Mobile-specific (7)** — esistono solo a 375px:
A-10 (hamburger dead-end) · A-12 (overflow+filtri stacking) · A-13 (offline Filtri no-feedback) · A-08 (swipe/long-press) · A-03 (no back/ESC) · B-02 (`.hy-goto` 24px) · **+ gap headline** (bottom-nav mobile vs tab in-page, concettuale/superficie).

**Shared (19)** — ereditati dal baseline, viewport-agnostici: tutto il resto della § 1.

**Diff col baseline desktop:**
- **Eredita #37/#38** (overlay/on-color non-token): A-09 (`#fff`+`rgba` shell) e B-04 (4 scrim) → mobile **re-introduce**, non risolve il debito.
- **Ereditati come CTA→stub di dominio**: A-04, A-05, A-06, A-07, B-01 (già stub sul desktop).
- **Nuovo-mobile**: i 7 mobile-specific sopra — non presenti nei 38 gap desktop perché nascono da gesture, disclosure a sheet, breakpoint reflow e touch-target.
- **Nessuna nuova categoria TOKEN**: le violazioni sono 2 (A-09, B-04), entrambe riconducibili al debt #38 già noto — chiudere #38 le copre.

---

## § 4 — Tensioni risolte (socratic Turn 4)

| # | Ambiguità | Decisione canonica (domain model) | Gap chiusi |
|---|-----------|-----------------------------------|-----------|
| 1 | Tab "Games" vs sidebar "Games" | Rinomina il **tab** ("I miei giochi"); la voce sidebar "Games"=catalogo (inv #20) resta canonica | A-01 |
| 2 | Long-press 500ms vs tap (device lenti) | **cancel-on-move ~10px** + feedback di soglia; tap protetto da `consume()` | A-08 |
| 3+4 | Stacking sheet-su-drawer + Back/ESC | **Backtrack di 1 livello** (history guard, no close-all, no uscita route) per detail/filtri/bulk + drawer s06 + jump-confirm; disclosure non-stackabili si auto-chiudono | A-03, A-12, B-03 |
| 5 | Reopen "Abbandona" single vs double | **Single confirm** (ripristina solo lock read-only, rischio nullo); chiusura "Abbandona" resta confirm soft | B-09 |
| bonus | Bottom-nav globale vs tab in-page | `MobileBottomBar` 5-tab = shell globale persistente; tab in-page subordinati | gap headline |

---

## § 5 — Statistiche

- **Per categoria** (25): CTA 10 · STATE 12 · ENTITY 1 · TOKEN 2 · ROUTE 0 *(il gap headline è ROUTE, fuori-tabella)*
- **Per severity** (25): CRITICO 5 · IMPORTANTE 10 · MINORE 10
- **Per surface**: A 16 · B 9
- **Mobile-specific vs shared**: mobile-specific 6 in-tabella (+headline = 7) · shared 19
- **TOKEN non in tokens.css**: 2 (A-09, B-04) — entrambi eredita #38
- **Quadratura**: 10+12+1+2 = 25 ✓ · 5+10+10 = 25 ✓ · 16+9 = 25 ✓ · 6+19 = 25 ✓

---

## § 6 — Riconciliazione con la pre-analisi statica (18 → 25)

Pre-analisi = **18 ID** (A1–A13 = 13 · B1–B5 = 5). Di questi, **17 testati** a runtime — A4 (DEMO-NAV) non esercitato (vedi sotto).

- **16 `[runtime-confirmed]`** — 15 numerati in § 1 + **pre-A12** (bottom-nav): confermato ma concettuale → instradato al gap headline. ⚠️ **Da non confondere con la riga runtime A-12** (overflow+filtri, `[new]`): sono due finding diversi che condividono il numero-base.
- **1 superato / non più valido** — **A11 pre-analisi** (lp-arming reduced-motion): `@media (prefers-reduced-motion: reduce)` spegne la `transition`, quindi lo scale non anima → **non** è violazione a11y. Declassato a non-gap.
- **1 non testato** — **A4 pre-analisi** (DEMO-NAV `window.location`): la card-nav specifica non è stata esercitata a runtime; resta finding statico low.
- **10 `[new]`** — A-10, A-12, A-13, A-14, A-15, A-16 (library) + B-05, B-06, B-07, B-08 (companion).

**Totale numerato § 1 = 15 confirmed + 10 new = 25 ✓** (pre-A12 bottom-nav e gap headline restano fuori-tabella come superficie mancante).

> Mappatura ID pre-analisi → runtime: A1→A-01, A2→A-05, A3→A-04, A5→A-07, A6→A-06, A7→A-02, A8→A-09, A9→A-03, A10→A-11, A13→A-08 · **A11 → declassato a non-gap** · **A12 → gap headline** (≠ riga runtime A-12) · **A4 → non testato** · B1→B-02, B2→B-01, B3→B-03, B4→B-04, B5→B-09. (La rinumerazione runtime ordina per severity, da cui lo shift.)

---

## § 7 — Invarianti dominio in viewport mobile

- **Solo #20** (sidebar: Library personale + Games catalogo/Discover-default) tocca la superficie library-mobile → gap **A-01** (collisione tab/sidebar), amplificata a 375px dove la sidebar collassa dietro l'hamburger (A-10).
- **19/20 invarianti** — i fatti di dominio #1–#9 + le invarianti operative #10–#19 (max-1-live #10, 3-timestamp #11, sorting #12, draft-warning #13, ora-inizio-derived #14, promotion #15, tagged-vs-invited #16/#17, tab-Partite #18, no-parallel-live #19), oltre a drawer-stack / "+ Nuova session" / live-immersive — vivono nel **flow GameNight/Session che SP8 non porta in mobile** → gap headline.
- **Companion** = dominio single-user (Aaron gioca da solo, amici fisici): invarianti GameNight/Session **non applicabili** a s05/06/07. Nessuna violazione inventata.

**Conclusione**: "validare le 20 invarianti in mobile" con i mockup SP8 è quasi vuoto (**1/20** toccata: solo #20). Il valore reale della sessione mobile è: (a) risolvere A-01 su #20; (b) registrare il flow GameNight/Session (le altre 19) come prossima wave di parità mobile.

---

## Note / limitazioni

- Sessione runtime operatore-guidata (5 turni + socratic). Prototipo del bundle **replayato**, non rigenerato.
- **Bug bundle fixato** in questa PR (asset sibling non risolti in `mockups/` → mirror aggiunto allo script).
- **A4 pre-analisi** non esercitato a runtime (resta finding statico low).
- Le proposte fix di § 1/§ 2 riflettono le 5 decisioni socratiche del Turn 4; nessuna viola le 20 invarianti.
- Riconciliazione "20 vs 14" invarianti: domain model consolidato 14→20 dopo il baseline (5 originali + 15 derivate).
