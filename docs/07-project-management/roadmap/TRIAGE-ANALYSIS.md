# 🔍 Analisi Triage - 132 Issue Senza Priorità

**Data Analisi**: 2025-11-23
**Analyst**: Claude Code
**Scope**: 132 issue su 165 totali (80%) senza priorità esplicita [P0]-[P3]

---

## 📊 Executive Summary

**SCOPERTA CRITICA**: Le issue del progetto usano DUE sistemi di prioritizzazione diversi:

### Sistema 1: Title-based (Recente - 21 issue)
- **[P0]** Critical (3 issue): #1729, #1728, #1727
- **[P1]** High (4 issue): #1733-#1730
- **[P2]** Medium (8 issue): #1741-#1734
- **[P3]** Low (6 issue): #1747-#1742

### Sistema 2: Label-based (Legacy - 132 issue)
- **priority: high** / **priority-high**: ~40-50 issue (stima)
- **priority: medium** / **priority-medium**: ~30-40 issue (stima)
- **priority: low** / **priority-low**: ~25-30 issue (stima)
- **Nessuna priorità**: ~15-20 issue (stima)

### 🚨 RISCHIO MAGGIORE

**Issue critiche identificate SENZA priorità [P0]-[P3]:**

| # | Titolo | Area | Rischio | Priorità Suggerita |
|---|--------|------|---------|-------------------|
| **#575** | AUTH-08: Admin Override for 2FA Locked-Out Users | Auth/Security | 🔴 ALTO | **P1** |
| **#576** | SEC-05: AUTH-07 Security Penetration Testing | Security | 🔴 CRITICO | **P1** |

---

## 📋 Categorizzazione per Area

### 1. SECURITY & AUTHENTICATION (2-5 issue stimati)

**Priorità Stimata**: P0-P1 (CRITICAL/HIGH)

| Issue | Titolo | Labels Attuali | Priorità Suggerita |
|-------|--------|----------------|-------------------|
| #575 | AUTH-08: Admin Override for 2FA Locked-Out Users | backend, infrastructure, security | **P1** 🔥 |
| #576 | SEC-05: AUTH-07 Security Penetration Testing | security, testing | **P1** 🔥 |

**Razionale**: Issues di sicurezza e autenticazione sono sempre critiche. Blocco 2FA può impedire accesso admin in produzione.

---

### 2. BOARD GAME AI (BGAI) - MVP Core (40-50 issue stimati)

**Priorità Stimata**: P1-P2 (HIGH-MEDIUM)

**Campione Identificato** (dalla pagina 4):

| Issue Range | Area | Label Attuale | Priorità Suggerita |
|-------------|------|---------------|-------------------|
| #1001-#1011 | BGAI Components & Integration | priority-high | **P1-P2** |
| #1002 | ResponseCard component (answer, confidence, citations) | priority-high, frontend | **P2** |
| #1003 | GameSelector dropdown component | priority-high, frontend | **P2** |
| #1004 | Loading and error states (UI/UX) | priority-high, frontend | **P2** |
| #1005 | Jest tests for Q&A components (20 tests) | priority-high, testing | **P2** |
| #1006 | Backend API integration (/api/v1/board-game-ai/ask) | priority-high, backend | **P1** 🔥 |
| #1007 | Streaming SSE support for real-time responses | priority-high, backend/frontend | **P1** 🔥 |
| #1008 | Error handling and retry logic | priority-high, frontend | **P2** |
| #1009 | Month 5 E2E testing | priority-high, e2e | **P2** |
| #1010-#1011 | Dataset annotation (7 Wonders, Catan, etc.) | priority-high, dataset | **P2** |

**Razionale**: Backend API (#1006) e SSE streaming (#1007) sono dipendenze bloccanti per tutto il frontend BGAI.

---

### 3. TESTING INFRASTRUCTURE (20-30 issue stimati)

**Priorità Stimata**: P1-P2 (HIGH-MEDIUM)

**Campione Identificato** (dalla pagina 3):

| Issue | Titolo | Label Attuale | Priorità Suggerita |
|-------|--------|---------------|-------------------|
| #1504 | ✂️ [TEST-006] Split Large Test Files (frontend) | priority: high | **P1** |
| #1503 | 🌐 [TEST-005] Replace Global Fetch Mocks (frontend) | priority: high | **P1** |
| #1502 | 🔄 [TEST-004] Extract SSE Mock Helper (frontend) | priority: high | **P1** |
| #1507 | 📁 [TEST-009] Remove Excessive Regions (backend) | priority: medium | **P2** |
| #1506 | 📊 [TEST-008] Consolidate with Theory Tests (backend) | priority: medium | **P2** |
| #1505 | 🔢 [TEST-007] Reduce Magic Numbers in Tests (backend) | priority: medium | **P2** |

**E2E Testing Issues:**

| Issue | Titolo | Label Attuale | Priorità Suggerita |
|-------|--------|---------------|-------------------|
| #1498 | 📊 [E2E-012] Add E2E Code Coverage Reporting | area/testing | **P2** |
| #1497 | 🌐 [E2E-011] Add Browser Matrix (Firefox, Safari) | area/testing | **P2** |
| #1496 | 📸 [E2E-010] Add Visual Regression Tests | area/testing | **P2** |

**Razionale**: Test infrastructure è prerequisito per raggiungere coverage ≥90% (G1 Gate). Issues #1502-#1504 sbloccano il lavoro di test.

---

### 4. FRONTEND REFACTORING & CLEANUP (15-20 issue stimati)

**Priorità Stimata**: P2-P3 (MEDIUM-LOW)

**Campione Identificato** (dalla pagina 2):

| Issue | Titolo | Label Attuale | Priorità Suggerita |
|-------|--------|---------------|-------------------|
| #1668 | Update Component Imports to Subdirectory Paths | priority: high, frontend | **P2** |
| #1667 | Remove Deprecated Profile Page | priority: high, frontend | **P2** |
| #1666 | Consolidate Duplicate React Components | priority: high, frontend | **P2** |
| #1675 | Implement Missing Frontend Backend APIs | backend, frontend | **P2** |
| #1676 | Remove Backward Compatibility Layers | backend, frontend | **P3** |
| #1677 | Remove Obsolete Data Models | backend | **P3** |
| #1678 | Fix Test Infrastructure Issues | testing | **P1** 🔥 |

**Razionale**: #1678 è test infrastructure (P1), il resto è refactoring (P2-P3).

---

### 5. ADMIN CONSOLE & REPORTING (8-12 issue stimati)

**Priorità Stimata**: P2-P3 (MEDIUM-LOW per MVP)

**Campione Identificato** (dalla pagina 5):

| Issue | Titolo | Labels | Priorità Suggerita |
|-------|--------|--------|-------------------|
| #916 | ReportingService generation + scheduling | admin-console, backend | **P3** |
| #917 | Report templates (4 predefined) | admin-console, backend | **P3** |
| #918 | Email delivery integration for reports | admin-console, backend, email | **P3** ⚠️ |
| #919 | Unit tests ReportingService | admin-console, testing | **P3** |
| #920 | Report builder | admin-console, frontend | **P3** |
| #921 | Enhanced alert configuration UI | admin-console, frontend | **P3** |
| #922 | E2E report generation + Email validation | admin-console, email, testing | **P3** ⚠️ |

**⚠️ Note**: #918, #922 coinvolgono email - verificare sicurezza (injection, spam).

**Razionale**: Admin console è "nice to have" per MVP, ma non blocker per utenti finali.

---

### 6. INFRASTRUCTURE & DEVOPS (10-15 issue stimati)

**Priorità Stimata**: P3-DEFERRED (LOW per MVP)

**Campione Identificato** (dalle prime 30 issue più vecchie):

| Issue | Titolo | Labels | Priorità Suggerita |
|-------|--------|--------|-------------------|
| #701 | Add resource limits to all Docker services | infrastructure, priority-low | **P3** / DEFERRED |
| #702 | Implement Docker Compose profiles | infrastructure, priority-low | **P3** / DEFERRED |
| #703 | Add Traefik reverse proxy layer | infrastructure, priority-low | **DEFERRED** |
| #704 | Create backup automation scripts | infrastructure, priority-low, deferred | **DEFERRED** |
| #705 | Add infrastructure monitoring (cAdvisor) | infrastructure, priority-low | **DEFERRED** |
| #706 | Create operational runbooks documentation | documentation, infrastructure, priority-low | **P3** / DEFERRED |
| #707 | docker-compose.override.yml example | infrastructure, priority-low, deferred | **DEFERRED** |
| #818 | Establish quarterly security scan review process | area/security, deferred, priority-low | **DEFERRED** |

**Razionale**: Produzione-only features, non bloccanti per MVP.

---

## 🎯 Raccomandazioni di Triage

### FASE 1: Normalizzazione Sistema Priorità (1 giorno)

**Azione**: Rinominare TUTTE le 132 issue con formato [Px] nel titolo.

**Script Suggerito**:
```bash
# Pseudocodice per bulk update (richiede GitHub CLI + permessi)
gh issue list --label "priority: high" --json number,title | \
  jq -r '.[] | "gh issue edit \(.number) --title \"[P1] \(.title)\""'
```

**Mapping Label → Title Tag**:
- `priority: high` / `priority-high` → `[P1]`
- `priority: medium` / `priority-medium` → `[P2]`
- `priority: low` / `priority-low` → `[P3]`
- `Nessuna label + review` → `[P2]` o `[P3]` o `DEFERRED`

---

### FASE 2: Triage Critico (4-6 ore)

**Obiettivo**: Identificare TUTTI i P0/P1 nascosti.

#### Blocco 1: Security & Auth (30 min)
- ✅ Review #575, #576
- ⚠️ Cercare altre issue `label:security` o `label:auth` senza priorità
- **Target**: 0 security issue senza priorità

#### Blocco 2: Backend API Dependencies (1h)
- ✅ Review #1006 (Backend API integration)
- ✅ Review #1007 (SSE streaming)
- ⚠️ Identificare endpoint bloccanti per frontend
- **Target**: Tutte le API core MVP marcate P1

#### Blocco 3: Test Infrastructure (1h)
- ✅ Review #1502-#1504 (Frontend test infra)
- ✅ Review #1678 (Fix test infrastructure issues)
- ⚠️ Identificare blockers per coverage 90%
- **Target**: Test infra P1 identificata

#### Blocco 4: Data Integrity & Bugs (1-2h)
- ⚠️ Cercare `label:bug` senza priorità
- ⚠️ Cercare `database`, `migration`, `data-loss` keywords
- **Target**: 0 bug critici non triaggiati

---

### FASE 3: Triage Sistematico Rimanenti (2 giorni)

**Approccio**: Suddividere le ~100 issue rimanenti in blocchi da 25.

#### Giorno 1 - Mattina: Issue #1400-#1500 (25 issue, 2h)
- Categorizzare per Epic/Area
- Assegnare P1/P2/P3/DEFERRED
- Identificare duplicati

#### Giorno 1 - Pomeriggio: Issue #1300-#1400 (25 issue, 2h)
- Categorizzare per Epic/Area
- Assegnare P1/P2/P3/DEFERRED
- Identificare duplicati

#### Giorno 2 - Mattina: Issue #1000-#1300 (50 issue, 3h)
- Focus su BGAI issues
- Prioritizzare MVP core features
- Deferire nice-to-have

#### Giorno 2 - Pomeriggio: Issue #575-#1000 (32 issue, 2h)
- Issue più vecchie (infrastructure, docs, admin)
- Maggior parte probabilmente P3/DEFERRED
- Chiudere obsolete se necessario

---

## 📈 Stima Post-Triage

Basandomi sui campioni analizzati, ecco la distribuzione attesa:

| Priorità | Stima Issue | Effort Stimato |
|----------|-------------|----------------|
| **P0 (nuovi)** | 0-2 | 0-16h |
| **P1 (da 132)** | 15-25 | 120-200h |
| **P2 (da 132)** | 40-50 | 300-450h |
| **P3 (da 132)** | 20-30 | 150-250h |
| **DEFERRED (nuovi)** | 15-25 | - |
| **CLOSED (obsolete)** | 5-10 | - |

**Total MVP (P0+P1+P2+P3)**: 78-110 issue, 570-916h

---

## 🚨 Issue da Verificare IMMEDIATAMENTE

Queste issue richiedono review URGENTE prima del triage completo:

### 1. Security & Auth
- [ ] **#575** - Admin 2FA override (blocker produzione?)
- [ ] **#576** - Security penetration testing (obbligatorio pre-launch)

### 2. Backend Core API
- [ ] **#1006** - Backend API integration (dipendenza frontend)
- [ ] **#1007** - SSE streaming support (real-time core)

### 3. Test Infrastructure
- [ ] **#1678** - Fix test infrastructure issues (blocker coverage)
- [ ] **#1502-#1504** - Test refactoring (prerequisito 90%)

### 4. Data Integrity
- [ ] Cercare issue con keywords: `migration`, `data-loss`, `corruption`, `rollback`

---

## 📋 Checklist Triage

### Pre-Triage
- [ ] Approva bulk rename script (label → title tag)
- [ ] Assegna 2 devs full-time per 3 giorni
- [ ] Setup triage board/project in GitHub
- [ ] Prepara template decision matrix

### Durante Triage
- [ ] Giorno 1: Security, API, Test Infra (P0/P1 discovery)
- [ ] Giorno 2: BGAI issues (#1000-#1300)
- [ ] Giorno 3: Remainder + consolidamento
- [ ] Marcare duplicati per closure
- [ ] Deferire infra/admin console

### Post-Triage
- [ ] Aggiornare ROADMAP.md con numeri reali
- [ ] Re-stimare effort totale
- [ ] Ri-calcolare timeline launch
- [ ] Brief team su risultati
- [ ] Iniziare P0 immediati

---

## 🎓 Lessons Learned

### Problemi Identificati
1. **Doppio sistema priorità**: Title-based vs Label-based
2. **80% issue non triagiate**: Bottleneck planning
3. **Priorità label inconsistenti**: `priority: high` vs `priority-high`
4. **Issue vecchie (2023-2024)**: Molte probabilmente obsolete

### Raccomandazioni Future
1. **Enforcing**: Tutte le nuove issue DEVONO avere [Px] nel titolo
2. **Automazione**: GitHub Action che richiede priority label/tag
3. **Weekly triage**: 30min review nuove issue (no backlog)
4. **Milestone tracking**: Associare tutte issue MVP a milestone "v1.0"

---

**Next Steps**:
1. Review questo documento con il team
2. Approvare raccomandazioni Fase 1-3
3. Schedulare triage session 3 giorni
4. Avviare Fase 1 (normalizzazione)

**Analyst**: Claude Code
**Version**: 1.0
**Date**: 2025-11-23
