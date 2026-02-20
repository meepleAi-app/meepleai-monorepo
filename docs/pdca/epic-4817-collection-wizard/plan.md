# Plan: Epic #4817 - User Collection Wizard

## Obiettivo
Implementare wizard drawer unificato "Aggiungi Gioco alla Collezione" con 3 step (Source → PDF → Info & Save), riutilizzando componenti esistenti.

## Sequenza di Implementazione

### Fase 1: Backend Foundation
| # | Issue | Comando | Dipendenze | Effort |
|---|-------|---------|------------|--------|
| 1 | #4823 Backend Game Preview API | `/implementa #4823` | Nessuna | 1d |

**Razionale**: L'endpoint backend deve esistere prima che il frontend possa consumarlo. Nessuna dipendenza.

### Fase 2: Frontend Foundation
| # | Issue | Comando | Dipendenze | Effort |
|---|-------|---------|------------|--------|
| 2 | #4818 AddGameSheet Drawer + State Machine | `/implementa #4818` | #4823 | 2-3d |

**Razionale**: Il drawer è il contenitore per tutti gli step. Deve esistere prima di implementare i singoli step.

### Fase 3: Wizard Steps (sequenziali)
| # | Issue | Comando | Dipendenze | Effort |
|---|-------|---------|------------|--------|
| 3 | #4819 Step 1 - Game Source | `/implementa #4819` | #4818 | 2d |
| 4 | #4820 Step 2 - KB & PDF | `/implementa #4820` | #4818 | 2d |
| 5 | #4821 Step 3 - Info & Save | `/implementa #4821` | #4818 | 1-2d |

**Razionale**: Gli step sono tecnicamente indipendenti (ognuno è un componente separato), ma implementarli in ordine sequenziale 1→2→3 permette di testare il flusso incrementalmente. Step 2 e 3 potrebbero essere parallelizzati ma la sequenza aiuta la coerenza del wizard context.

### Fase 4: Integration
| # | Issue | Comando | Dipendenze | Effort |
|---|-------|---------|------------|--------|
| 6 | #4822 MeepleCard Rewire | `/implementa #4822` | #4818, #4820, #4821 | 1d |

**Razionale**: L'integrazione nella MeepleCard richiede che il drawer e almeno Step 2+3 siano funzionanti (il flusso da GameCard salta Step 1).

### Fase 5: Testing
| # | Issue | Comando | Dipendenze | Effort |
|---|-------|---------|------------|--------|
| 7 | #4824 E2E Tests | `/implementa #4824` | Tutte le precedenti | 1-2d |

**Razionale**: I test E2E coprono l'intero flusso e richiedono che tutto sia implementato.

## Dependency Graph

```
Fase 1:  #4823 (Backend API)
            │
Fase 2:  #4818 (Drawer + State)
            │
         ┌──┼──────────┐
Fase 3:  │  │           │
      #4819  #4820   #4821
     (Step1) (Step2) (Step3)
         └──┼──────────┘
            │
Fase 4:  #4822 (MeepleCard Rewire)
            │
Fase 5:  #4824 (E2E Tests)
```

## Strategia per ogni `/implementa`

Ogni issue segue il flusso standard:
1. **Branch**: `feature/issue-{N}-{desc}` da `main-dev`
2. **Parent tracking**: `git config branch.feature/issue-{N}.parent main-dev`
3. **Implementazione**: Codice → Test → Code Review
4. **PR**: Target `main-dev` (parent branch)
5. **Merge**: Merge PR → Delete branch → Update issue
6. **Sync**: Pull `main-dev` prima della prossima issue

## Rischi e Mitigazioni

| Rischio | Probabilità | Mitigazione |
|---------|-------------|-------------|
| Conflitti merge tra step | Media | Merge ogni issue prima di iniziare la successiva |
| Componenti riutilizzati non compatibili | Bassa | Verificare API dei componenti esistenti nella Fase 2 |
| Backend API insufficiente | Bassa | Endpoint preview semplice, fallback a chiamate multiple |
| Quota check nel wizard | Media | Verificare quota PRIMA di aprire il wizard |

## Metriche di Successo

- [ ] Tutti gli step del wizard funzionano end-to-end
- [ ] Flusso da GameCard: 2 click per aggiungere gioco (open wizard → save)
- [ ] Flusso da Library: 4 click (open → search → select → save)
- [ ] Test coverage ≥85% frontend, ≥90% backend
- [ ] Zero regressioni sui flussi esistenti (add to collection, PDF upload)

## Effort Totale Stimato
**10-13 giorni** di lavoro sequenziale

---
*Piano creato: 2026-02-19*
*Epic: #4817*
*Branch target: main-dev*
