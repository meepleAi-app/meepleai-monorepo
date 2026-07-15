# Claude Design — Gap Report Mobile (SP8 parity + libro-game companion)

**Data**: 2026-07-15
**Issue**: [#1890](https://github.com/meepleAi-app/meepleai-monorepo/issues/1890) — Rerun Claude Design demo post-SP8
**Trigger**: ✅ soddisfatto — SP8 merged via PR [#1689](https://github.com/meepleAi-app/meepleai-monorepo/pull/1689) (2026-05-29)
**Baseline desktop**: [`2026-06-04-claude-design-gap-report.md`](./2026-06-04-claude-design-gap-report.md) (38 gap)
**Bundle**: `claude-design-bundle/sp8-mobile/` (17 file, rebuild via `scripts/build-claude-design-bundle.sh sp8`)
**Domain model**: [`2026-06-04-gamenight-session-domain-model.md`](../specs/2026-06-04-gamenight-session-domain-model.md) (20 invarianti)

> ⚠️ **Natura di questo documento**: questa è una **PRE-ANALISI STATICA** dei due mockup SP8 (lettura HTML/JSX + confronto con il DoD dei brief e con il baseline desktop), **NON** l'output di una sessione interattiva su `claude.ai/design`. La sessione runtime (prototipo cliccabile a viewport 375px + socratic loop) è **human-in-the-loop** e resta da eseguire — vedi § 0. Questo report è la **bozza pre-popolata + handoff**: riduce la sessione umana da esplorativa a validazione mirata. I gap qui elencati sono verificati staticamente (con `file:riga`); quelli che richiedono runtime (reflow, focus order effettivo, conflitti gesture) sono marcati `[runtime]`.

---

## § 0 — Come completare la demo (handoff sessione umana)

Gli AC #1 e #2 della issue (rebuild bundle + demo iterativa a viewport mobile) si completano così:

1. **Bundle** (già costruito): `scripts/build-claude-design-bundle.sh sp8` → `claude-design-bundle/sp8-mobile/` (scaffold + 9 mockup + 3 brief in `briefs/`).
   - ⚠️ **Peso ~720KB** (il companion `librogame-runthrough-play-session.html` è 146KB). È ~3× il target ~250KB del workflow. Se Claude Design satura il context: **split in 2 sessioni** — (A) `sp4-library-*` + `mobile-app.jsx` + `primitive-nav-bottom-mobile.html`; (B) `librogame-runthrough-play-session.*` + `translate-viewer`.
2. **System prompt**: primo messaggio = la sezione *"SP8 Mobile Parity + Libro-Game Companion"* in [`docs/for-developers/workflows/claude-design-demo-prompts.md`](../workflows/claude-design-demo-prompts.md). Imposta il **canvas a 375px prima del Turn 1**.
3. **Turni**: (A) library-mobile — default → overflow → filtri bottom-sheet → long-press → bulk → FAB; ciclo empty/loading/error/offline/permission/filtered; reflow tablet 768px. (B) companion — diary (list→editor→save) → history drawer (jump-back) → end-campaign (kebab→3-vie→post-close); offline per tutti e 3.
4. **Output**: aggiornare le § 1–7 di questo file con i gap runtime emersi (marcare come `[runtime-confirmed]` le ipotesi qui elencate) e ancorare le nuove invarianti nel domain model spec.

---

## § 1 — Tabella gap (pre-analisi statica)

Due metà: **A** = library-mobile (`sp4-library-mobile`), **B** = companion (`librogame-runthrough-play-session` state-05/06/07). Categorie: ROUTE / STATE / CTA / ENTITY / TOKEN. Severity ∈ {high, med, low}.

### A — Library mobile parity

| # | cat | schermo | descrizione | evidenza | sev | proposta fix |
|---|-----|---------|-------------|----------|-----|--------------|
| A1 | ENTITY | tab bar /library | Il tab in-page **"Games"** (entity tab: Games/Sessions/Chat) collide semanticamente con la voce **sidebar "Games"** (catalogo, invariante #20). Su mobile, senza sidebar visibile, un utente può leggere "Games" come "il catalogo" invece di "i miei giochi in libreria" | `sp4-library-mobile.jsx:27-31,461-475` | **high** | rinominare il tab (es. "Giochi miei" / usare l'icona entity) o disambiguare via label; decisione IA cross-route |
| A2 | ROUTE | empty state | CTA "+ Aggiungi gioco" → `/library/add` e "Scopri shared" → `/shared-games`: route referenziate, non nel bundle, handler = `showToast` placeholder | `sp4-library-mobile.jsx:544-545` | med | costruire `/library/add` + `/shared-games` o deep-link a flow esistenti |
| A3 | CTA | detail sheet | "Apri scheda" nel drawer dettaglio è un dead-end (`onClose()` — torna indietro, nessun navigate a `/games/[id]`) | `sp4-library-mobile.jsx:256` | med | collegare a Game Detail reale |
| A4 | CTA | card → add-drawer | Commento `// DEMO-NAV` con `window.location.href='sp4-add-game-drawer.html'` (hard-nav a mockup, non route app) | `sp4-library-mobile.jsx:242` | low | sostituire con router push |
| A5 | CTA | permission state | "Chiedi un invito" / "Torna alla mia libreria" → `showToast` placeholder | `sp4-library-mobile.jsx:531-532` | low | flow richiesta accesso shared-library |
| A6 | STATE | error banner | "Riprova" / "Mostra cache" → `showToast` (nessun retry/cache reale — atteso in prototipo fixture) | `sp4-library-mobile.jsx:516-517` | low | wiring retry+cache in impl |
| A7 | ENTITY | bulk actions | Azioni bulk (Archivia/Tag/Esporta/Rimuovi) sono toast-only, nessuna persistenza (fixture) | `sp4-library-mobile.jsx:646-648` | low | wiring mutation |
| A8 | TOKEN | shell + card | 6 valori `rgba()` hardcoded fuori da tokens.css: device shadow `rgba(90,60,20,.22)`/`rgba(0,0,0,.65)` (decorativo frame), cov text-shadow `rgba(255,255,255,.92)`+`rgba(0,0,0,.25)`, bb-kebab bg `rgba(255,255,255,.18)`, overlay scrim `rgba(0,0,0,.42)`. Il commento sorgente `riga 24` dichiara "Nessun hex hardcoded" → **claim falso** | `sp4-library-mobile.html:86,89,243,399,415` | med | tokenizzare overlay/scrim/on-color (riusa debt baseline #38); correggere il commento |
| A9 | STATE | sheet/drawer a11y | Focus-trap **assente** (no FocusLock) ed **ESC-to-close non implementato** nei bottom-sheet (detail/filtri/bulk) nonostante `role="dialog" aria-modal="true"` | `sp4-library-mobile.jsx:228,279,325` | med | focus-trap + ESC handler |
| A10 | STATE | touch target | `.lh-filter` (40px) e `.fopt` (40px) sotto la soglia ≥44px richiesta dal DoD | `sp4-library-mobile.html:193,441` | low | portare a ≥44px |
| A11 | STATE | reduced-motion | `.mcard.lp-arming` (scale .96, feedback long-press) **non escluso** dalla media query `prefers-reduced-motion` | `sp4-library-mobile.html:238,501-504` | low | aggiungere `.mcard` transform alla media query |
| A12 | ENTITY | bottom-nav globale | `[runtime]` La library-mobile usa 3-tab in-page ma il DoD dichiara out-of-scope il "ridisegno bottom-nav globale 5-tab" (`MobileBottomBar`). In una demo full-app non è chiaro dove viva la bottom-nav globale rispetto ai tab di libreria | brief SP8-mobile §"Out of scope" | med | decidere convivenza bottom-nav globale ↔ tab in-page |
| A13 | ENTITY | gesture | `[runtime]` Bottom-sheet ha drag-handle visivo ma **nessun swipe-dismiss** JS; long-press 500ms vs tap-to-open può confliggere su device lenti | `sp4-library-mobile.html:428`; `.jsx:117-136` | low | swipe handler + soglia gesture |

**Positivo (A)**: tutti i **12 stati DoD implementati** (default/empty/loading-skeleton/error/permission/offline/filtered/bulk/overflow/filters-sheet/light+dark/375+768) ✅ · `entityHsl` inline 9-entity conforme ✅ · MeepleCard `variant="list"` riusato ✅ · IA mobile-first (hero compatto, overflow kebab, recente non-sticky) conforme al brief ✅.

### B — Libro-game companion (state-05/06/07)

| # | cat | stato | descrizione | evidenza | sev | proposta fix |
|---|-----|-------|-------------|----------|-----|--------------|
| B1 | STATE | state-06 touch target | `.hy-goto` ("→ vai qui", **CTA primaria** del drawer paragrafi) ha altezza ~24px — ben sotto ≥44px. Anche `.dy-retry`/`.dy-act` (state-05) sotto soglia | `...play-session.html:1062,822-823` | **high** | portare i CTA a ≥44px (regressione a11y su azione primaria) |
| B2 | CTA | state-07 post-close | "📄 Scarica PDF riassunto" → `showToast` (l'endpoint BE async è deviazione accettata dal brief, ma va marcato) + "Torna alla libreria"/"Riapri campagna" placeholder | `...play-session.jsx:380-384` | med | wiring endpoint PDF async + route library/reopen |
| B3 | STATE | state-05/06 a11y | Focus-trap **assente** + ESC-to-close non implementato nel drawer state-06 (ha `role="dialog" aria-modal`); live-region assente sullo skeleton (solo `aria-busy`) | `...play-session.html:1052`; `.jsx:200` | med | focus-trap + ESC + `aria-live` su skeleton |
| B4 | TOKEN | state-06/07 | 5 `rgba(0,0,0,x)` hardcoded per scrim/shadow: drawer `rgba(0,0,0,.4)`/`.25`/`.35`, dialog `rgba(0,0,0,.5)` ×2 | `...play-session.html:956,957,1010,1025,1295` | low | tokenizzare scrim (riusa debt baseline #38) |
| B5 | ENTITY | state-07 reopen | `[runtime]` Nota socratica del brief: "Abbandona" reopen usa **single confirm**; da validare che l'UX mobile non renda la riapertura troppo facile/accidentale | `...play-session.html:1276-1281` | low | validare in socratic |

**Positivo (B)**: tutti gli stati dei **3 nuovi state implementati** (state-05 diary: default/pristine/typed/empty/loading/error/offline; state-06: default/confirm/empty/loading/error/offline; state-07: kebab/dialog-3-vie/loading/error/offline/2 post-close) ✅ · dialog roles corretti (`role="alertdialog"` su 3-vie e jump-back, `role="menu"` su kebab) ✅ · reading text ≥18pt conforme al vincolo table-distance ✅ · confetti reduced-motion safe ✅ · companion model rispettato (single-user, no multi-player) ✅.

---

## § 2 — Top priorità

1. **Flow GameNight/Session non portato in mobile** · ENTITY · global · **il gap headline** — vedi § 4. La issue chiede di validare "drawer stack, modale + Nuova session, live immersivo" in mobile, ma SP8 ha portato in mobile **solo** `/library` e il companion. 18/20 invarianti non hanno alcuna superficie mobile. Effort: **L** (nuova wave mockup mobile game-night).
2. **A1 — tab "Games" ↔ sidebar "Games"** · ENTITY · /library · collisione naming amplificata dall'assenza di sidebar su mobile; tocca l'unica invariante (#20) che SP8 sfiora. Effort: **S** (decisione IA + rename).
3. **B1 — CTA primaria drawer a 24px** · STATE(a11y) · state-06 · "→ vai qui" è l'azione centrale del drawer paragrafi ed è sotto la metà della soglia touch. Effort: **XS**.
4. **A2 — route add/shared non costruite** · ROUTE · empty state · le CTA di uscita dall'empty puntano nel vuoto. Effort: **S** (deep-link) / **M** (route nuove).
5. **A8/B4 — scrim/overlay hardcoded** · TOKEN · multiple · il debt overlay del baseline (#38) si ripresenta in mobile; il commento "nessun hex" è falso. Effort: **S** (batch tokenization).
6. **A9/B3 — focus-trap + ESC assenti nei sheet/drawer** · STATE(a11y) · multiple · pattern modale senza gestione focus/tastiera. Effort: **S** (primitive condivisa).

---

## § 3 — Mobile-specific vs shared (diff vs baseline 2026-06-04)

Requisito AC #4: distinguere gap ereditati dal desktop (**shared**) da gap introdotti dal viewport mobile (**mobile-specific**).

### Shared (ereditati dal desktop baseline)
- **TOKEN overlay/scrim hardcoded** — il baseline lo registra come gap #38 (overlay `rgba(...)` + on-color fuori da tokens.css). Mobile lo **eredita e amplifica** (A8: 6 occorrenze; B4: 5 occorrenze). Non è un difetto nuovo del mobile.
- **CTA verso route non costruite** — pattern già presente nel baseline (es. `/knowledge-base`, `/toolkit/[id]` stub). Mobile ne aggiunge di analoghi (A2: `/library/add`, `/shared-games`).
- **Fixture/toast al posto di persistenza** — comportamento atteso di prototipo, come nel baseline.

### Mobile-specific (nuovi, introdotti dal viewport)
- **A1 — collisione naming tab/sidebar "Games"**: esiste solo perché su mobile la sidebar sparisce e i tab in-page diventano l'unica nav visibile.
- **A13/B (gesture)**: swipe-dismiss assente sui bottom-sheet, conflitto long-press ↔ tap — pattern che nel desktop non esistono.
- **A10/B1 — touch target <44px**: rilevante solo su touch input.
- **A11 — reduced-motion su feedback long-press**: gesture-specifico.
- **A9/B3 — sheet-vs-drawer**: il desktop usa drawer laterali (stack ESC-backtrack testato ≤2 livelli nel baseline); il mobile usa **bottom-sheet 75–80vh** — pattern e gestione focus diversi, non coperti dal drawer-stack desktop.
- **A12 — bottom-nav globale vs tab in-page**: problema di IA che esiste solo nel form-factor mobile.
- **§ 4 gap headline**: l'assenza di parità mobile per il flow GameNight/Session è per definizione mobile-specific.

---

## § 4 — Invarianti dominio in viewport mobile

La issue chiede di "validare che le 20 invarianti dominio reggano in viewport mobile". **Riconciliazione necessaria** (finding del panel):

- Le **20 invarianti** vivono nel bounded-context GameNight/Session (domain model spec). **Nessuno dei 2 mockup SP8 è quel flow**: `sp4-library-mobile` è la route `/library`; il companion è dominio libro-game single-user.
- **Unica invariante toccata**: **#20** (sidebar = 2 voci game-related, Library + Games/Discover). La library-mobile la sfiora ma introduce la collisione naming **A1** (tab in-page "Games" vs voce catalogo "Games"). → da validare/decidere.
- **18/20 invarianti non hanno superficie mobile**: max-1-live (#10), 3-timestamp (#11), sorting (#12), draft-warning (#13), promotion (#15), tagged-vs-invited (#16/#17), tab Partite self-contained (#18), no-parallel-live (#19), + drawer stack e modale "+ Nuova session" — **tutti nel flow GameNight/Session che SP8 non porta in mobile**.
- **Companion**: dominio diverso. Le invarianti GameNight/Session **non si applicano** a state-05/06/07 (una play-session *è* una Session, ma questi 3 stati non toccano il suo lifecycle). Da validare invece contro le **6 functions companion v1** + companion model (single-user, no multi-player, WiFi-instabile, reading ≥18pt) — tutti ✅ nella pre-analisi.

**Conclusione**: la richiesta "validare le 20 invarianti in mobile" con i mockup SP8 è **quasi vuota**. Il valore reale della validazione mobile è: (a) risolvere A1 su #20; (b) registrare che il flow GameNight/Session è la **prossima wave di parità mobile** (gap Top-priorità #1).

---

## § 5 — TOKEN gap (categoria)

Requisito AC #5: pattern mobile non in `tokens.css` → tracciare in categoria TOKEN.

| pattern | occorrenze | riconducibile a | azione |
|---------|-----------|-----------------|--------|
| scrim overlay `rgba(0,0,0,α)` | A8 (`.42`), B4 (`.4`/`.5`/`.35`) | debt baseline #38 (overlay non tokenizzati) | aggiungere token `--scrim-{sm,md,strong}` |
| on-color glow/shadow `rgba(255,255,255,α)` | A8 (cov text-shadow, bb-kebab bg) | debt baseline #38 (on-color) | token `--on-glass`, `--cov-glow` |
| device-frame shadow `rgba(90,60,20,.22)` | A8 | decorativo (frame preview, non prodotto) | accettabile — escludere dal gate |

**Nessun pattern colore mobile *nuovo*** rispetto al baseline: il mobile non introduce hue/shade inediti, riusa il debt overlay già noto. → **non serve una nuova categoria TOKEN gap**; basta chiudere il debt #38 del baseline.

---

## § 6 — Tensioni aperte (per socratic loop umano)

1. **Tab "Games" (in-page) vs "Games" (sidebar catalogo)** — (a) rinominare il tab libreria; (b) rinominare la voce sidebar; (c) affidarsi all'icona-entity per disambiguare. **Racc.**: (a) — il tab è locale a /library, la voce sidebar è il termine canonico di invariante #20.
2. **Bottom-nav globale 5-tab ↔ tab in-page /library** — (a) bottom-nav globale sempre visibile + tab libreria sotto; (b) la libreria "consuma" la bottom-nav mentre è attiva. **Racc.**: (a) — coerente con `MobileBottomBar` shell, ma da validare in demo full-app.
3. **Bottom-sheet stacking** — `[runtime]` se il drawer paragrafi (state-06) si apre sopra un altro sheet, ESC fa backtrack di 1 livello o close-all? Il baseline ha testato lo stack drawer desktop solo ≤2 livelli. **Racc.**: replicare la semantica cascade-store (backtrack 1 livello).
4. **Long-press ↔ tap** — `[runtime]` su device lenti il long-press 500ms può sovrapporsi al tap-to-open. **Racc.**: cancel-on-move + soglia temporale esplicita.

---

## § 7 — Statistiche (pre-analisi statica)

- **Gap totali**: 18 (A: 13 · B: 5)
- **Per categoria**: ENTITY 4 · STATE 6 · CTA 4 · ROUTE 1 · TOKEN 2 (+ headline ENTITY §4)
- **Per severity**: high 3 (flow-parity, A1, B1) · med 7 · low 8
- **Mobile-specific**: 8 · **Shared (ereditati)**: 3 pattern
- **Stati DoD implementati**: A 12/12 ✅ · B 3-stati × ~8 varianti ✅
- **CTA placeholder**: A 5 · B 3
- **TOKEN violazioni DoD "zero hex"**: A 6 · B 5 (tutte riconducibili a debt baseline #38)
- **Invarianti dominio validabili in mobile**: 1/20 (#20, via A1) — le altre 19 fuori-superficie
- **Marker `[GAP-X]` runtime nei mockup**: 0 (i designer non hanno lasciato marker; i gap sono derivati da confronto DoD/baseline)

---

## Note / limitazioni

- **Questo è pre-analisi statica, non la sessione Claude Design runtime.** I gap `[runtime]` (reflow effettivo, focus order, conflitti gesture, stacking sheet) richiedono il prototipo cliccabile a 375px per essere confermati — vedi § 0.
- **Bundle 720KB** rischia saturazione: valutare split in 2 sessioni (§ 0.1).
- Il body della issue #1890 cita `nanolith-nav-bottom-mobile.html`: il file reale è **`primitive-nav-bottom-mobile.html`** (rinominato post-deversioning) — incluso nel bundle sp8.
- La discrepanza "20 invarianti" (issue) vs "14" (gap report baseline sezione 3) è risolta: il domain model è stato consolidato 14→20 dopo il baseline (5 originali + 15 derivate) — fonte canonica = domain model spec, non il gap report.
