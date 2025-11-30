# Azul 15 Q&A Pairs - Planning Options
**Date**: 2025-11-30
**Issue**: #998 (BGAI-058)

## Option 1: Balanced Coverage (Recommended ✅)

### Distribution
- **4 Easy (setup)**: Setup fondamentale
- **8 Medium (gameplay + scoring)**: 5 gameplay + 3 scoring
- **3 Hard (edge_cases)**: Casi limite e varianti

### Rationale
- Segue esattamente pattern Terraforming Mars (#997) e Wingspan (#997)
- Distribuzione: setup 26.7%, gameplay/scoring 53.3%, edge_cases 20%
- Massima copertura meccaniche uniche di Azul
- Focus su scoring system (aspetto distintivo del gioco)

### Coverage Map
**Easy (4)**:
1. Setup tessere vassoio per numero giocatori
2. Riempimento iniziale vassoi (4 piastrelle)
3. Segnalino primo giocatore e criterio iniziale
4. Segnapunti e plancia giocatore (setup base)

**Medium-Gameplay (5)**:
5. Due modi per prendere piastrelle (vassoio vs centro)
6. Regola linee quadrettate (colore uguale, destra→sinistra)
7. Restrizione colore già in parete
8. Linea del pavimento (quando usarla)
9. Completamento linea quadrettata (requisito)

**Medium-Scoring (3)**:
10. Sistema punteggio base (isolata vs connesse)
11. Calcolo punti orizzontali+verticali
12. Punti negativi linea pavimento

**Hard-Edge Cases (3)**:
13. Sacchetto vuoto durante rifornimento
14. Piastrelle eccedenti con linea pavimento piena
15. Fine partita e bonus finali (linee complete, colori completi)

---

## Option 2: Gameplay-Heavy Focus

### Distribution
- **4 Easy (setup)**: Identico a Option 1
- **9 Medium (gameplay-focused)**: 7 gameplay + 2 scoring
- **2 Hard (edge_cases)**: Solo casi critici

### Rationale
- Maggior focus su meccaniche di gioco
- Meno emphasis su scoring (più intuitivo)
- Solo edge cases critici (sacchetto vuoto, eccedenze)

### Coverage Map
**Easy (4)**: Identico a Option 1

**Medium-Gameplay (7)**:
5. Due modi prendere piastrelle
6. Regola linee quadrettate
7. Restrizione colore parete
8. Linea pavimento
9. Rivestimento pareti (sequenza)
10. Preparazione round successivo
11. Fase offerta vassoi (termine fase)

**Medium-Scoring (2)**:
12. Sistema punteggio base
13. Punti negativi linea pavimento

**Hard (2)**:
14. Sacchetto vuoto
15. Fine partita

---

## Comparison Analysis

| Criteria | Option 1 | Option 2 |
|----------|----------|----------|
| **Pattern Alignment** | ✅ 100% match TM/Wingspan | ⚠️ 90% match |
| **Scoring Coverage** | ✅ Comprehensive (3 Q&A) | ⚠️ Basic (2 Q&A) |
| **Gameplay Depth** | ✅ Balanced (5 Q&A) | ✅✅ Deep (7 Q&A) |
| **Edge Cases** | ✅ Good (3 Q&A) | ⚠️ Minimal (2 Q&A) |
| **Azul Uniqueness** | ✅✅ Scoring è distintivo | ⚠️ Less emphasis |
| **RAG Testing Value** | ✅ High variety | ⚠️ Lower variety |
| **Consistency** | ✅✅ Exact previous pattern | ⚠️ New distribution |

## Recommendation: **Option 1** ✅

### Reasons (95% Confidence)
1. **Perfect Pattern Match**: Esattamente 4-8-3 come TM e Wingspan
2. **Azul Distinctive**: Scoring system è elemento chiave del gioco
3. **RAG Testing**: Varietà domande migliora dataset quality
4. **Edge Cases**: 3 Q&A coprono tutti scenari critici
5. **Consistency**: Mantiene standard annotation stabilito

### Implementation Strategy
- Use Option 1 distribution
- Ensure precise Italian terminology from rulebook
- Reference specific page numbers from PDF
- Include both positive and negative forbidden keywords
- Annotated_by: `expert_azul_bgai058`
- Annotated_at: Current timestamp
- ID format: `azul_301` to `azul_315` (following pattern)
