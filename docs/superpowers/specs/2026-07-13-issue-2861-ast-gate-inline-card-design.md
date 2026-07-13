# Design — Issue #2861 (C4): AST gate against inline card re-implementation

**Data:** 2026-07-13
**Issue:** [#2861](https://github.com/meepleAi-app/meepleai-monorepo/issues/2861) (ST7) — umbrella [#2863](https://github.com/meepleAi-app/meepleai-monorepo/issues/2863) "MeepleCard family debt teardown"
**Audit:** `docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md` (§6 ST7; barriera CRITICAL "nessun esempio mockup↔render / guasto silenzioso")
**Branch:** `feature/issue-2861-ast-gate-inline-card` da `main-dev`
**Dipendenze:** C1 (#2858, mergiato — PR #2890). **Complementa** la ESLint rule `local/no-standalone-card-renderer` di C1.

---

## 1. Problema

C1 ha aggiunto un import-boundary che impedisce di "rubare" i parts di `meeple-card/` per riassemblare una card. Ma non cattura la reimplementazione **raw-HTML** (nessun import): il vecchio `MeepleCardGame` rendeva cover/stelle/badge inline con glifi `★`/`☆` e tint entità, senza importare nulla. C4 è il gate mancante contro questo pattern.

**Calibrazione (come in C1).** La verifica sul codice mostra che i primitivi (stelle/cover/badge) sono **ubiqui e legittimi**: i glifi `★`/`☆` compaiono in ~15 file (search-result, filtri, hero, il componente condiviso `ui/feedback/Stars.tsx`). Un gate ingenuo "rileva stelle/cover/badge inline" produrrebbe decine di falsi positivi. La premessa "detect inline cover/stars/badge" dell'audit va resa precisa.

**Segnale preciso disponibile.** Il nome `*Card` da solo è troppo ampio (100+ generici, lezione C1), ma **intersecato con i glifi-stella inline** diventa preciso: enumerazione esaustiva → esattamente **2** componenti `*Card` non-canonici rendono stelle inline senza comporre `<MeepleCard>` (`HubGameCard`, `HubToolkitCard`). Gli altri ~13 file con stelle non sono `*Card` (o compongono/canonici). Cover e badge sono forme troppo comuni (`<img>`, pill) per essere gated senza rumore.

## 2. Decisione (brainstorm 2026-07-13)

**Gate = `*Card` + stelle inline + no `<MeepleCard>`.** Un test Vitest AST fallisce quando un componente `*Card` (fuori dalle dir canoniche, non esente/allowlisted) rende glifi `★`/`☆` inline e non compone `<MeepleCard>`. Cover/badge **non** gated (coperti dall'import-boundary di C1 per il furto dei parts). Gli 2 esistenti sono grandfathered in allowlist.

## 3. Il gate

**File:** `apps/web/src/components/ui/data-display/meeple-card/__tests__/no-inline-card-reimplementation.test.tsx` (accanto a `call-site-coverage.test.tsx`, stesso pattern: Babel parse + `@babel/traverse` + `glob` + safeguard path-drift + floor file-scansionati).

**Scansione:** `.tsx` di produzione sotto `src/{app,components}`, escludendo `**/__tests__/**`, `src/app/(public)/dev/**`, `src/components/**/dev/**`, `**/showcase/**`, e le dir canoniche `ui/data-display/meeple-card/**` + `ui/data-display/extra-meeple-card/**`.

**Condizione di fallimento** (tutte insieme, per-file):
1. il file dichiara un componente esportato con nome che matcha `/^[A-Z][A-Za-z0-9]*Card([A-Z][A-Za-z0-9]*)?$/` (es. `HubGameCard`, `FooCard`, `FooCardTile`); **e**
2. il file contiene un glifo-stella `★` (U+2605) o `☆` (U+2606) in un `JSXText` o `StringLiteral`/`TemplateLiteral` dentro JSX (non in un commento); **e**
3. il file **non** contiene alcun `JSXOpeningElement` di nome `MeepleCard`; **e**
4. il path (relativo ad `apps/web/`, forward-slash) **non** è in `STAR_CARD_ALLOWLIST`.

→ il test lancia con `file:line` e messaggio: comporre `<MeepleCard>` (adapter) oppure, per un rating non-entità, usare il componente condiviso `@/components/ui/feedback/Stars`.

**Granularità per-file** (come `call-site-coverage`): la co-presenza di `*Card` + stelle + assenza di `MeepleCard` nello stesso file è il segnale. Un raro falso positivo (file con un `*Card` senza stelle + una funzione non-card con stelle) si risolve via allowlist con motivazione.

## 4. Baseline allowlist

```ts
// Grandfathered hand-rolled star-cards (pre-C4). Follow-up: convert to MeepleCard
// adapters like MeepleCardGame in C1 (#2858) — tracked separately, out of C4 scope.
const STAR_CARD_ALLOWLIST: ReadonlySet<string> = new Set([
  'src/components/features/hub/HubGameCard.tsx',
  'src/components/features/hub/HubToolkitCard.tsx',
]);
```

Enumerazione esaustiva verificata: questi sono gli **unici 2** file che violano oggi. Il gate parte quindi verde (2 grandfathered, 0 nuovi). Nuove violazioni: fix (comporre MeepleCard / usare `Stars`) o allowlist esplicita con motivazione in commit.

## 5. Relazione con C1 (nessun overlap)

- **C1** `local/no-standalone-card-renderer` (ESLint, import-boundary): non puoi **importare** i parts/variants di `meeple-card/` da fuori → non riassembli una card dai pezzi canonici.
- **C4** (questo gate, AST body-inspection): non puoi **hand-rollare** il rating (`★`/`☆`) in un `*Card` senza comporre `MeepleCard` → cattura la reimplementazione raw-HTML che C1 non vede.

I due layer insieme coprono sia il furto-dei-parts sia il raw-HTML.

## 6. Testing

Il gate test contiene i propri casi di auto-verifica **inline** (fixture-string parse-ate con Babel, non file su disco), per validare la logica di rilevamento senza dipendere dallo stato del repo:
- **Violazione:** un `*Card` con `★` inline e senza `<MeepleCard>` → rilevato.
- **OK — compone:** un `*Card` con `★` che rende anche `<MeepleCard>` → non rilevato.
- **OK — no stelle:** un `*Card` senza glifi-stella → non rilevato.
- **OK — non-card con stelle:** un componente non-`*Card` con `★` → non rilevato.
- **OK — commento:** un `*Card` con `★` solo in un commento → non rilevato (AST, non testo).

Più l'assertion principale sul repo reale: gli unici hit sono i 2 allowlisted → zero violazioni non-allowlisted.

**Verifica finale:** `pnpm exec vitest run <gate>`, `pnpm typecheck`, `pnpm lint`, `pnpm build` (build per confermare che il nuovo test non rompe nulla — è solo un test, rischio basso, ma incluso per completezza gate).

## 7. Scope

**In scope:** il test-gate + `STAR_CARD_ALLOWLIST` (2 voci) + nota nella `card-decision-table.md` (§ come il gate difende la composizione).

**Fuori scope (deferiti):**
- Convertire `HubGameCard`/`HubToolkitCard` ad adapter MeepleCard → task a sé (come la conversione di `MeepleCardGame` in C1); grandfathered qui, candidati follow-up.
- Gating di cover/badge → forme troppo comuni; il furto dei parts è già coperto da C1.
- Migrare i ~13 file con rating inline al componente `Stars` condiviso → cleanup non richiesto da questa issue.

## 8. Rischi

| Rischio | Mitigazione |
|---|---|
| Falso positivo (file con `*Card` senza stelle + funzione non-card con stelle) | Granularità per-file documentata; allowlist con motivazione (pattern `SPREAD_ALLOWLIST`) |
| Un `*Card` legittimo con rating a stelle non-entità (es. un mini-widget) verrebbe flaggato | È by-design: usare il componente condiviso `Stars` o allowlist con motivazione — il gate è un checkpoint deliberato |
| Glob rotto → pass vacuo | Safeguard floor file-scansionati (>50) + path-drift check su `package.json` (come `call-site-coverage`) |
| Rilevamento glifi in stringhe non-JSX (es. una costante) | Il walk AST limita `★`/`☆` a `JSXText`/`StringLiteral`/`TemplateLiteral` dentro il sottoalbero JSX |

## 9. Acceptance criteria

- [ ] `no-inline-card-reimplementation.test.tsx` presente accanto a `call-site-coverage.test.tsx`, con AST Babel + safeguard path-drift + floor.
- [ ] Rileva `*Card` + `★`/`☆` inline JSX + no `<MeepleCard>`, escludendo dir canoniche/test/dev/showcase.
- [ ] `STAR_CARD_ALLOWLIST` = `{HubGameCard, HubToolkitCard}` con nota follow-up; zero violazioni non-allowlisted sul repo.
- [ ] Casi di auto-verifica inline (violazione + 4 varianti OK incl. commento).
- [ ] `card-decision-table.md` documenta il gate.
- [ ] `pnpm exec vitest run` (gate) verde, `pnpm typecheck`, `pnpm lint`, `pnpm build` verdi.
