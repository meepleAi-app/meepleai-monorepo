# Plan: Epic #5023 — ExtraMeepleCard Drawer System

**Data**: 2026-02-21
**Branch padre**: `frontend-dev`
**Epic**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/5023

---

## Obiettivo

Implementare il sistema di dettaglio per ogni tipo di MeepleCard tramite un drawer interno (Sheet shadcn/ui) che mostra l'ExtraMeepleCard con tab contestuali, attivato dal click sul bottone "i".

## Architettura

```
MeepleCard (entityType + entityId)
    ↓  click "i"
    [internal useState: drawerOpen]
    ↓
ExtraMeepleCardDrawer  (Sheet wrapper)
    ↓  routing per entityType
EntityExtraMeepleCard
    ├── GameExtraMeepleCard    (update: +KB tab, +Agent tab)
    ├── AgentExtraMeepleCard   (nuovo)
    ├── ChatExtraMeepleCard    (nuovo)
    └── KbExtraMeepleCard      (nuovo)
```

---

## Piano di Implementazione

### Fase 1 — Infrastruttura (BLOCCA TUTTO) ⚠️

| Issue | Titolo | Effort | Rischio |
|-------|--------|--------|---------|
| **#5024** | ExtraMeepleCardDrawer — Sheet wrapper base | **S** | Basso |

**Note implementazione #5024**:
- Usare `Sheet`, `SheetContent`, `SheetHeader` da `@/components/ui/sheet`
- Props minimal: `entityType`, `entityId`, `open`, `onClose`
- Il fetch dati è delegato ai singoli EntityExtraMeepleCard (non nel drawer)
- Skeleton loader generico mentre aspetta il render del figlio
- Test: render, open/close, prop validation

**Comando**:
```
/implementa 5024
```

---

### Fase 2 — Parallelizzabile (dopo #5024)

Le issue #5025, #5026, #5027, #5028 sono tutte indipendenti tra loro. Possono essere implementate in qualsiasi ordine.

**Ordine suggerito** (priorità prodotto: Agent > Chat > KB > MeepleCard wiring):

| Ordine | Issue | Titolo | Effort | Rischio | Note chiave |
|--------|-------|--------|--------|---------|-------------|
| 2a | **#5026** | AgentExtraMeepleCard | **M** | Medio | Riusa AgentStatusBadge, AgentStatsDisplay, AgentModelInfo, DocumentStatusBadge |
| 2b | **#5027** | ChatExtraMeepleCard | **M** | Basso | Riusa ChatStatusBadge; API threads da verificare |
| 2c | **#5028** | KbExtraMeepleCard | **M** | Medio | API contenuto estratto da verificare se esiste; usare usePdfProcessingStatus |
| 2d | **#5025** | MeepleCard info button → drawer | **S** | Basso | Rimuovere infoHref, aggiungere entityId + useState interno |

**Comandi (sequenziali, uno alla volta)**:
```
/implementa 5026
/implementa 5027
/implementa 5028
/implementa 5025
```

---

### Fase 3 — Dipende da Fase 2 completa (#5024 + #5026)

| Issue | Titolo | Effort | Rischio | Dipende da |
|-------|--------|--------|---------|-----------|
| **#5029** | GameExtraMeepleCard update (+KB tab, +Agent tab) | **M** | Basso | #5024, #5026 |

**Note implementazione #5029**:
- Tab KB: riusa DocumentStatusBadge (già da #5028)
- Tab Agent: riusa componenti card compatta da #5026
- Test: 2 nuovi tab + integrazione con drawer

**Comando**:
```
/implementa 5029
```

---

## Sequenza Completa /implementa

```bash
# FASE 1 — Infrastruttura
/implementa 5024    # ExtraMeepleCardDrawer

# FASE 2 — Componenti (in sequenza, uno alla volta)
/implementa 5026    # AgentExtraMeepleCard
/implementa 5027    # ChatExtraMeepleCard
/implementa 5028    # KbExtraMeepleCard
/implementa 5025    # MeepleCard info button wiring

# FASE 3 — Update finale
/implementa 5029    # GameExtraMeepleCard update
```

**Totale**: 6 issue | Effort stimato: ~1-2 giorni di lavoro

---

## Rischi e Mitigazioni

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| API threads/chat non esistente | Media | Alto | Verificare prima di implementare #5027; usare mock se assente |
| API contenuto KB non esposta | Media | Medio | Verificare prima di #5028; tab Content con stato "coming soon" se assente |
| infoHref usato da consumer esistenti | Bassa | Medio | Mantenere come prop opzionale deprecated, non rimuovere subito |
| ExtraMeepleCard non adatto a drawer (size) | Bassa | Medio | Aggiungere CSS override nel drawer per responsive |
| Componenti riutilizzabili non trovati | Bassa | Basso | Path confermato: `meeple-card-features/` |

---

## Checklist Finale di Validazione Epic

### Per ogni issue completata
- [ ] Branch creato da `frontend-dev`
- [ ] Componente renderizza senza errori
- [ ] Tutti i tab funzionano (loading, data, empty state, error state)
- [ ] Test unitari passano (`pnpm test`)
- [ ] TypeScript senza errori (`pnpm typecheck`)
- [ ] PR mergiata su `frontend-dev`

### Validazione integrazione Epic (dopo #5029)
- [ ] Click "i" su GameCard → apre drawer con GameExtraMeepleCard (5 tab)
- [ ] Click "i" su AgentCard → apre drawer con AgentExtraMeepleCard (4 tab)
- [ ] Click "i" su ChatCard → apre drawer con ChatExtraMeepleCard (3 tab)
- [ ] Click "i" su KbCard → apre drawer con KbExtraMeepleCard (3 tab)
- [ ] Drawer si chiude con ESC / click fuori / click X
- [ ] Mobile: drawer full-screen, tab scrollabili
- [ ] Desktop: drawer 600px laterale
- [ ] Se `entityId` assente → bottone "i" non visibile
- [ ] Nessun errore console in tutte le varianti
- [ ] `pnpm build` senza errori

### Smoke test manuale
- [ ] Aprire library → click "i" su una game card → tab Details, Rules&FAQs, Stats, KB, Agent visibili
- [ ] Click "i" su agent card → tab Overview, Stats, History, KB visibili
- [ ] Click "i" su chat card → tab Overview, Messages, Context visibili
- [ ] Click "i" su KB card → tab Overview, Content, Status visibili
