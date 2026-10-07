# Classificazione degli orfani `data-testid` nella suite E2E

**Data**: 2026-10-08 · **Origine**: bonifica guidata dalla baseline del gate [#4120](https://github.com/meepleAi-app/meepleai-monorepo/issues/4120) · **Stato**: audit, decisione non presa

> **Convenzione**: ogni misura porta il comando che la riproduce. I numeri sono esiti del passaggio del 2026-10-08 su `feature/issue-4120-orphan-testid-gate`, non affermazioni sul presente.

---

## 1. Perché questo documento esiste

Dopo la prima fetta di bonifica (le schede, −137 sedi) il passo ovvio sarebbe continuare a rinominare selettori. **Non lo è**, e il motivo è il reperto di questo audit: gli orfani residui appartengono a **due categorie che vogliono trattamenti opposti**, e trattarli allo stesso modo trasformerebbe test morti in test sottilmente sbagliati.

Far risolvere un selettore non rende significativo un test scritto contro una UI immaginata. Lo rende eseguibile — e quindi rosso, o peggio verde per il motivo sbagliato.

## 2. Le due categorie

### A — Deriva di strumentazione: la funzione esiste, l'hook no

L'elemento è renderizzato, ma nessuno gli ha mai messo un `data-testid`, oppure ne porta uno diverso. È il caso che la prima fetta ha chiuso: `MeepleCard` esponeva `'data-testid'` nel contratto e tutte le varianti lo propagavano, ma **nessun chiamante lo passava**.

Esempi verificati come esistenti:

| cluster | componente | testid dichiarati |
|---|---|---|
| citazioni in chat (`citation-card` 12 sedi, `citation` 8) | `src/components/features/game-chat/CitationChip.tsx` esiste | **nessuno** in produzione: le sole occorrenze sono mock sotto `__tests__/` |
| preview PDF (`current-page` 11, `zoom-in`/`zoom-out`/`zoom-level` 7+6+7) | i componenti esistono | nessuno fra quelli cercati |
| slot agente (`slot-card` 10) | i componenti esistono | nessuno fra quelli cercati |
| banner offline (`offline-banner` 12) | i componenti esistono | nessuno fra quelli cercati |

**Trattamento ammesso**: aggiungere un `data-testid` a un elemento **già renderizzato**, come fatto per `MeepleCard`. Mai introdurre UI per far passare un'asserzione.

### B — Funzione assente: la spec descrive una UI che non c'è

Qui il selettore non è il problema: è il sintomo. La spec è non eseguibile per costruzione, e lo resterebbe anche con il selettore giusto.

**Caso dimostrato — `e2e/admin/shared-games-bulk-import.spec.ts` (54 sedi, il secondo file più contaminato):**

```bash
# la rotta che visita
grep -n "page.goto" apps/web/e2e/admin/shared-games-bulk-import.spec.ts
#   /admin/games/import/bulk  →  redirige a /admin/content?tab=games&section=import

# i testid che cerca, contro il codice di produzione
for t in bulk-import-uploader json-textarea bulk-import-preview total-count valid-count \
         duplicate-count preview-table confirm-import bulk-import-results; do
  printf '%-24s %s\n' "$t" "$(grep -rl "data-testid=\"$t\"" apps/web/src --include=*.tsx | grep -v __tests__ | wc -l)"
done
# tutti 0
```

Nessun componente di bulk import esiste: `grep -rln "bulk-import\|BulkImport" apps/web/src` restituisce un dialog di import BGG, una storia di glossario e dei client API — niente con textarea JSON, uploader, tabella di preview e conteggi.

**Altro caso**: `dev-panel` (15 sedi) e `dev-badge` (6) — `grep -rln DevPanel apps/web/src` → **nessun file**.

**Trattamento**: skip con motivo classificato (`DIFETTO:` o `PREVISTO:` secondo il gate `SkipReasonClassArchitectureTests`) più una issue che registri l'intento, oppure eliminazione con l'intento registrato — il precedente è lo scaffold #4068, eliminato in #4106.

## 3. Un primo taglio misurabile: le rotte

Una spec le cui rotte non esistono **né come pagina né come sorgente di redirect** è quasi certamente di categoria B.

```bash
# il classificatore vive nello scratchpad della sessione; la misura è riproducibile
# confrontando i `page.goto` di ogni spec con le rotte di src/app e le `source:` di next.config.js
```

| sedi orfane | rotte visitate | assenti | file |
|---|---|---|---|
| 61 | 1 | **1** | `e2e/editor/dashboard.spec.ts` → `/editor/dashboard` |
| 33 | 8 | 3 | `e2e/epic-2-agent-system.spec.ts` → `/library/games/azul`, `/admin/strategies/create`, `/library/games/simple-game` |
| 15 | 1 | **1** | `e2e/collection-dashboard.spec.ts` → `/dashboard/collection` |
| 14 | 3 | **3** | `e2e/visual-docs/editor-flows/content-management.visual.spec.ts` → `/admin/games/game-1/{faq,quick-questions,errata}` |
| 14 | 1 | **1** | `e2e/visual-docs/editor-flows/document-management.visual.spec.ts` → `/admin/games/game-1/documents` |
| 12 | 5 | 1 | `e2e/epic-4-pdf-status.spec.ts` → `/admin/processing-queue` |

**145 sedi su 1103** stanno in spec le cui rotte sono **tutte** assenti.

### ⚠️ È un limite inferiore, non la misura

Due ragioni, e la seconda è dimostrata:

1. **Il classificatore ignora i redirect solo se li riconosce.** La prima stesura dava 261 invece di 145, perché non confrontava le URL con le `source:` di `next.config.js` — e i redirect Next precedono il routing, quindi `/agent/slots` atterra benissimo su `?tab=slots`. Corretto.
2. **Una rotta che risolve non garantisce che la funzione ci sia.** `shared-games-bulk-import` ha rotta valida (via redirect) e funzione inesistente: il classificatore la segna `0 assenti`, e sbaglia. Il controllo sulle rotte non sostituisce il controllo sui componenti.

Nota a margine: `e2e/epic-2-agent-system.spec.ts` visita `/library/games/azul`, cioè la **forma nuda** di #4105 — quella che risponde 404 di proposito. È una delle sedi che la decisione di #4105 lascia da correggere.

## 4. Cosa serve decidere, prima di continuare

- [ ] **Per la categoria B**: skip classificato o eliminazione? Esiste il precedente dell'eliminazione (#4068 → #4106) e quello dello skip con `DIFETTO:` (#4114). La scelta cambia cosa resta leggibile: un file eliminato lascia solo l'issue, uno skippato lascia la struttura
- [ ] **Per la categoria A**: fino a che punto è lecito strumentare? Aggiungere un testid a un elemento esistente è additivo, ma **rende eseguibile una spec che non ha mai girato** — e il resto delle sue asserzioni non è stato verificato contro la UI vera. Va deciso se la strumentazione arriva con la revisione della spec o prima
- [ ] **Chi classifica i cluster residui**: la tabella sopra copre i primi per peso; i rimanenti vanno passati con la stessa sonda (il componente esiste? i testid fratelli esistono?) prima di toccarli
- [ ] Le sedi di `/library/games/<id>` nuda in `epic-2-agent-system` vanno corrette insieme alle altre di #4105

## 5. Cosa NON serve decidere

Il gate #4120 è già in posizione e blocca il peggioramento, con la baseline per file come inventario. Nessuna di queste decisioni è un prerequisito per mergiare la PR del gate: la bonifica può procedere a fette, e ogni calo si registra con `pnpm lint:orphan-testids --update`.
