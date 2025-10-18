# MeepleAI - Struttura Epic e User Stories

## 🎯 Visione del Prodotto

MeepleAI è un assistente AI per regolamenti di giochi da tavolo che:
- Processa PDF di regolamenti
- Fornisce ricerca semantica tramite vector embeddings
- Risponde intelligentemente a domande sul gameplay

---

## 📊 Epic Principali

### EPIC-01: Gestione Documenti PDF
**Obiettivo**: Permettere caricamento, estrazione e indicizzazione efficiente di regolamenti PDF

**Valore per l'utente**: Gli utenti possono caricare regolamenti e ricevere risposte istantanee senza leggere manuali di 100+ pagine

**Criteri di completamento**:
- ✅ Upload multi-file con progress tracking
- ✅ Estrazione testo e tabelle da PDF
- ✅ Chunking intelligente (512 chars, 50 overlap)
- ✅ Embedding generation e storage su Qdrant
- ⚠️ Validazione formato e gestione errori
- ⚠️ Preview PDF prima dell'upload

**Timeline**: Q4 2025 - Q1 2026

**Feature incluse**:
- Upload wizard multi-step (upload.tsx - 44KB)
- PDF text extraction (Docnet.Core)
- PDF table extraction (iText7)
- Vector embedding (OpenRouter API)
- Qdrant vector storage

---

### EPIC-02: Ricerca Semantica e RAG
**Obiettivo**: Fornire risposte accurate basate sul contesto dei regolamenti

**Valore per l'utente**: Risposte precise e contestualizzate alle domande, con citazioni dai regolamenti

**Criteri di completamento**:
- ✅ RAG pipeline funzionante
- ✅ Semantic search su Qdrant
- ⚠️ Response caching con Redis (parziale)
- ⚠️ Citazioni esatte con numeri di pagina
- ⚠️ Gestione multilingua
- ❌ Fine-tuning su domande specifiche

**Timeline**: Q1 2026

**Feature incluse**:
- RagService con semantic search
- EmbeddingService per query encoding
- AiResponseCacheService (Redis)
- LlmService per generazione risposte

---

### EPIC-03: Interfaccia Chat Intelligente
**Obiettivo**: Chat UI intuitiva per interagire con l'AI

**Valore per l'utente**: Conversazioni naturali con l'AI, storico domande, suggerimenti contestuali

**Criteri di completamento**:
- ✅ Chat interface base (chat.tsx - 14.3KB)
- ✅ Storico conversazioni (ChatEntity, ChatLogEntity)
- ⚠️ Streaming responses
- ⚠️ Suggested follow-up questions
- ❌ Voice input/output
- ❌ Multi-document context switching

**Timeline**: Q4 2025 - Q1 2026

**Feature incluse**:
- Chat page con message history
- Chat log persistence
- AI request logging
- Correlation ID tracking

---

### EPIC-04: Editor Regolamenti e Versioning
**Obiettivo**: Permettere editing collaborativo e confronto versioni

**Valore per l'utente**: Tracciare modifiche ai regolamenti, confrontare versioni, gestire errata

**Criteri di completamento**:
- ✅ Editor base (editor.tsx - 15.7KB)
- ✅ Version comparison (versions.tsx - 20.1KB)
- ✅ RuleSpecDiffService
- ⚠️ Rich text editing
- ⚠️ Change tracking visuale
- ❌ Collaborative editing real-time

**Timeline**: Q1 2026

**Feature incluse**:
- Rule specification editor
- Version diff viewer
- RuleSpecService CRUD
- Game metadata management

---

### EPIC-05: Amministrazione e Monitoring
**Obiettivo**: Dashboard admin completo per gestione sistema

**Valore per l'utente**: Admin possono monitorare uso, gestire utenti, analizzare performance

**Criteri di completamento**:
- ✅ Admin dashboard (admin.tsx - 14.2KB)
- ✅ Activity logs (logs.tsx - 6.9KB)
- ✅ Audit logging (AuditService)
- ⚠️ User management UI
- ⚠️ Analytics e metriche
- ⚠️ Rate limiting dashboard

**Timeline**: Q1-Q2 2026

**Feature incluse**:
- Admin dashboard
- Activity log viewer
- Audit trail
- AI request monitoring
- Rate limit management

---

### EPIC-06: Automazione Workflow (n8n)
**Obiettivo**: Automatizzare processi ripetitivi e integrazioni

**Valore per l'utente**: Notifiche automatiche, sync con BGG, aggiornamenti regolamenti

**Criteri di completamento**:
- ✅ n8n integration (n8n.tsx - 16KB)
- ✅ N8nConfigService
- ⚠️ Workflow templates pre-configurati
- ⚠️ Event triggers da sistema
- ❌ BGG API integration
- ❌ Email notifications

**Timeline**: Q2 2026

**Feature incluse**:
- Workflow management UI
- n8n configuration
- Background task service
- Webhook endpoints

---

### EPIC-07: Autenticazione e Sicurezza
**Obiettivo**: Sistema auth robusto e sicuro

**Valore per l'utente**: Login sicuro, gestione sessioni, protezione dati

**Criteri di completamento**:
- ✅ Session-based auth (AuthService)
- ✅ Cookie management
- ✅ User roles (UserRole enum)
- ⚠️ OAuth providers (Google, Discord)
- ⚠️ 2FA
- ⚠️ Password reset flow
- ❌ SSO enterprise

**Timeline**: Q1 2026

**Feature incluse**:
- Session authentication
- User session management
- Role-based authorization
- Secure cookie handling

---

### EPIC-08: Testing e Quality Assurance
**Obiettivo**: Coverage >90%, CI/CD robusto, test E2E

**Valore per l'utente**: Software affidabile, meno bug, deploy sicuri

**Criteri di completamento**:
- ✅ xUnit + Testcontainers (API)
- ✅ Jest + Playwright (Web)
- ✅ GitHub Actions CI
- ⚠️ Coverage 90% (attuale ~70%)
- ⚠️ E2E test suite completa
- ❌ Performance testing
- ❌ Load testing

**Timeline**: Continuo

**Feature incluse**:
- Unit tests (xUnit, Jest)
- Integration tests (Testcontainers)
- E2E tests (Playwright)
- CI/CD pipeline

---

## 🎯 Roadmap Prioritizzata (Prossimi 6 mesi)

### Sprint 1-2 (Ottobre 2025) - FOUNDATION
**Focus**: Stabilizzare core features esistenti

**Obiettivo**: Sistema stabile e testato pronto per utenti beta
- 🔧 Completare EPIC-08: Portare coverage a 90%
- 🔧 Fix Docker configuration issues (già fatto)
- 🔧 EPIC-07: Implementare password reset
- 🐛 Bug fixing critico

**Milestone**: Beta-ready system

---

### Sprint 3-4 (Novembre 2025) - PDF ENHANCEMENT
**Focus**: EPIC-01 - Migliorare gestione PDF

**Obiettivo**: Upload robusto e validazione
- ✨ Validazione PDF (formato, dimensione, pages)
- ✨ Preview PDF prima dell'upload
- ✨ Gestione errori dettagliata
- ✨ Progress tracking migliorato
- 🎨 UX wizard multi-step

**Milestone**: Production-ready PDF upload

---

### Sprint 5-6 (Dicembre 2025) - SMART CHAT
**Focus**: EPIC-03 - Chat experience migliorata

**Obiettivo**: Chat più intelligente e reattiva
- ✨ Streaming responses (SSE)
- ✨ Suggested follow-up questions
- ✨ Document context switching
- 🎨 Loading states e UX polish
- ⚡ Performance optimization

**Milestone**: v1.0 Chat Release

---

### Sprint 7-8 (Gennaio 2026) - SEARCH OPTIMIZATION
**Focus**: EPIC-02 - RAG migliorato

**Obiettivo**: Risposte più accurate con citazioni
- ✨ Citazioni con page numbers
- ✨ Multi-language support
- ⚡ Redis cache optimization
- 📊 Response quality metrics
- 🔍 Search result ranking

**Milestone**: Smart answers v2.0

---

### Sprint 9-10 (Febbraio 2026) - COLLABORATION
**Focus**: EPIC-04 - Editor avanzato

**Obiettivo**: Editing collaborativo
- ✨ Rich text editor (TipTap/Slate)
- ✨ Visual change tracking
- 🎨 Diff viewer improvements
- 📊 Version timeline
- 👥 User comments

**Milestone**: Collaborative editing beta

---

### Sprint 11-12 (Marzo 2026) - ADMIN & ANALYTICS
**Focus**: EPIC-05 - Admin completo

**Obiettivo**: Dashboard analytics e user management
- ✨ User management UI
- 📊 Analytics dashboard
- 📈 Usage metrics
- ⚡ Performance monitoring
- 🔒 Rate limiting UI

**Milestone**: Full admin suite

---

## 📋 Template Issue GitHub

Copia i blocchi completi qui sotto per creare i file nella directory `.github/ISSUE_TEMPLATE/`

---

### FILE: `.github/ISSUE_TEMPLATE/bug_report.md`

```
---
name: Bug Report
about: Segnala un problema o comportamento inatteso
title: '[BUG] '
labels: bug, needs-triage
---

## Descrizione
Breve descrizione del bug

## Steps to Reproduce
1. Vai a '...'
2. Clicca su '...'
3. Vedi errore

## Comportamento Atteso
Cosa dovrebbe accadere

## Comportamento Attuale
Cosa accade invece

## Scenario BDD
```gherkin
Scenario: [Nome]
  Given [contesto]
  When [azione]
  Then [dovrebbe] ma [accade invece]
```json
## Ambiente
- OS: [Windows/Mac/Linux]
- Browser: [Chrome/Firefox/Safari]
- Versione: [es. v1.2.3]

## Screenshot/Logs
Se applicabile

## Possibile Soluzione
Se hai idee

## Priorità Suggerita
[ ] Critical - Blocca utenti
[ ] High - Impatta feature chiave
[ ] Medium - Bug minore
[ ] Low - Estetico/edge case
```

---

### FILE: `.github/ISSUE_TEMPLATE/feature_request.md`

```
---
name: Feature Request
about: Suggerisci una nuova funzionalità
title: '[FEATURE] '
labels: enhancement, needs-discussion
---

## User Story
Come [tipo utente]
Voglio [funzionalità]
Così che [beneficio]

## Problema/Opportunità
Quale problema risolve o opportunità crea?

## Valore per l'utente
- **Utenti finali**: 
- **Business**: 
- **Team**: 

## Scenari BDD
```gherkin
Scenario: Happy path
  Given [contesto]
  When [azione]
  Then [risultato]

Scenario: Edge case
  Given [contesto alternativo]
  When [azione]
  Then [gestione]
```

## Mockup/Design (opzionale)
Allega o linka mockup

## Acceptance Criteria
- [ ] Criterio 1
- [ ] Criterio 2
- [ ] Criterio 3

## Epic Correlata
Quale Epic? (EPIC-01, EPIC-02, etc.)

## Effort Stimato
[ ] XS (1-2 ore)
[ ] S (1-2 giorni)
[ ] M (3-5 giorni)
[ ] L (1-2 settimane)
[ ] XL (>2 settimane)

## Dipendenze
Issue o feature prerequisite
```

---

### FILE: `.github/ISSUE_TEMPLATE/technical_task.md`

```
---
name: Technical Task
about: Task tecnico (refactor, debt, optimization)
title: '[TECH] '
labels: technical, needs-review
---

## Obiettivo
Cosa vogliamo ottenere tecnicamente

## Contesto Tecnico
- File/moduli coinvolti:
- Architettura attuale:
- Problema tecnico:

## Soluzione Proposta
Come affrontare il problema

## Impatto
- **Performance**: 
- **Manutenibilità**: 
- **Scalabilità**: 
- **Security**: 

## Test Strategy
Come verrà testato

## Rischi
Potenziali problemi

## Definition of Done
- [ ] Codice implementato
- [ ] Test aggiunti (>90% coverage)
- [ ] Documentazione aggiornata
- [ ] Code review approvata
- [ ] Nessuna regressione

## Effort
[ ] S [ ] M [ ] L [ ] XL

## Priority
[ ] P0 (Critical) [ ] P1 (High) [ ] P2 (Medium) [ ] P3 (Low)
```

---

### FILE: `.github/ISSUE_TEMPLATE/user_story.md`

```
---
name: User Story
about: User story formato agile
title: '[STORY] '
labels: user-story, needs-estimation
---

## User Story
**Come** [persona/ruolo]
**Voglio** [obiettivo/desiderio]
**Così che** [beneficio/valore]

## Acceptance Criteria
```gherkin
Feature: [Nome feature]

Scenario: [Scenario principale]
  Given [contesto iniziale]
  And [precondizione]
  When [azione utente]
  Then [risultato atteso]
  And [effetto collaterale]

Scenario: [Edge case]
  Given [contesto alternativo]
  When [azione]
  Then [risultato alternativo]

Scenario: [Gestione errori]
  Given [contesto]
  When [azione non valida]
  Then [messaggio errore]
  And [stato sistema]
```json
## Contesto Business
Perché questa story è importante

## Dipendenze Tecniche
- Backend: [API/service necessari]
- Frontend: [componenti/page]
- Database: [entity/migration]
- Infra: [servizi docker]

## Note per lo Sviluppo
Dettagli tecnici utili

## Design/UX
Link a mockup/wireframe

## Story Points
[ ] 1 [ ] 2 [ ] 3 [ ] 5 [ ] 8 [ ] 13

## Epic
EPIC-XX: [nome epic]

## Definition of Done
- [ ] Codice implementato seguendo standard
- [ ] Test automatici (unit + integration)
- [ ] Coverage >90%
- [ ] Code review approvata
- [ ] Documentazione aggiornata
- [ ] Acceptance criteria verificati
- [ ] Nessuna regressione
- [ ] Deploy su environment di test
```

---

## 🏷️ Script per Creare Label su GitHub

Copia e incolla questo script completo per creare tutte le label:

### SCRIPT: `create-labels.sh`

```bash
#!/bin/bash

# Script per creare label GitHub per MeepleAI
# Uso: ./create-labels.sh OWNER/REPO
# Esempio: ./create-labels.sh username/meepleai

REPO=$1

if [ -z "$REPO" ]; then
  echo "Uso: ./create-labels.sh OWNER/REPO"
  exit 1
fi

echo "Creazione label per repository: $REPO"

# Priorità
gh label create "priority:critical" --repo $REPO --color "d73a4a" --description "P0: Blocca utenti, security issue" --force
gh label create "priority:high" --repo $REPO --color "ff6b6b" --description "P1: Feature chiave, bug importante" --force
gh label create "priority:medium" --repo $REPO --color "fbca04" --description "P2: Miglioramenti significativi" --force
gh label create "priority:low" --repo $REPO --color "0e8a16" --description "P3: Nice-to-have, ottimizzazioni minori" --force

# Tipo
gh label create "type:bug" --repo $REPO --color "d73a4a" --description "Bug da fixare" --force
gh label create "type:feature" --repo $REPO --color "a2eeef" --description "Nuova funzionalità" --force
gh label create "type:enhancement" --repo $REPO --color "84b6eb" --description "Miglioramento esistente" --force
gh label create "type:refactor" --repo $REPO --color "fbca04" --description "Refactoring tecnico" --force
gh label create "type:docs" --repo $REPO --color "0075ca" --description "Documentazione" --force
gh label create "type:security" --repo $REPO --color "d73a4a" --description "Security issue" --force
gh label create "type:performance" --repo $REPO --color "c2e0c6" --description "Ottimizzazione performance" --force

# Epic
gh label create "epic:pdf-management" --repo $REPO --color "5319e7" --description "EPIC-01: Gestione PDF" --force
gh label create "epic:rag-search" --repo $REPO --color "5319e7" --description "EPIC-02: RAG e ricerca semantica" --force
gh label create "epic:chat-ui" --repo $REPO --color "5319e7" --description "EPIC-03: Chat interface" --force
gh label create "epic:editor" --repo $REPO --color "5319e7" --description "EPIC-04: Editor e versioning" --force
gh label create "epic:admin" --repo $REPO --color "5319e7" --description "EPIC-05: Admin e monitoring" --force
gh label create "epic:workflow" --repo $REPO --color "5319e7" --description "EPIC-06: Workflow automation" --force
gh label create "epic:auth" --repo $REPO --color "5319e7" --description "EPIC-07: Auth e security" --force
gh label create "epic:testing" --repo $REPO --color "5319e7" --description "EPIC-08: Testing e QA" --force

# Area Tecnica
gh label create "area:frontend" --repo $REPO --color "bfdadc" --description "Next.js/React" --force
gh label create "area:backend" --repo $REPO --color "c5def5" --description "ASP.NET Core" --force
gh label create "area:database" --repo $REPO --color "d4c5f9" --description "PostgreSQL/Qdrant/Redis" --force
gh label create "area:ai" --repo $REPO --color "f9d0c4" --description "RAG/Embeddings/LLM" --force
gh label create "area:infra" --repo $REPO --color "fef2c0" --description "Docker/CI/CD" --force
gh label create "area:ux" --repo $REPO --color "ffc0cb" --description "User experience" --force

# Status
gh label create "status:needs-triage" --repo $REPO --color "ededed" --description "Da valutare" --force
gh label create "status:needs-discussion" --repo $REPO --color "d4c5f9" --description "Richiede discussione" --force
gh label create "status:needs-estimation" --repo $REPO --color "fbca04" --description "Da stimare" --force
gh label create "status:ready" --repo $REPO --color "0e8a16" --description "Pronta per sviluppo" --force
gh label create "status:in-progress" --repo $REPO --color "1d76db" --description "In lavorazione" --force
gh label create "status:blocked" --repo $REPO --color "b60205" --description "Bloccata" --force
gh label create "status:needs-review" --repo $REPO --color "fbca04" --description "In review" --force

# Effort
gh label create "effort:xs" --repo $REPO --color "c2e0c6" --description "1-2 ore" --force
gh label create "effort:s" --repo $REPO --color "bfd4f2" --description "1-2 giorni" --force
gh label create "effort:m" --repo $REPO --color "d4c5f9" --description "3-5 giorni" --force
gh label create "effort:l" --repo $REPO --color "f9d0c4" --description "1-2 settimane" --force
gh label create "effort:xl" --repo $REPO --color "ff6b6b" --description ">2 settimane" --force

echo "✅ Label create con successo!"
```

**Per eseguire:**
```bash
chmod +x create-labels.sh
./create-labels.sh YOUR-USERNAME/meepleai
```

---

## 📈 Metriche di Successo

### Per Epic
- **EPIC-01**: Upload success rate >95%, avg processing time <30s
- **EPIC-02**: Answer accuracy >85%, response time <2s
- **EPIC-03**: User engagement >70%, session duration >5min
- **EPIC-04**: Version comparison usage >40% utenti
- **EPIC-05**: Admin task completion <5min avg
- **EPIC-06**: Workflow automation saves >10h/week
- **EPIC-07**: Security incidents = 0, auth success >99%
- **EPIC-08**: Coverage >90%, CI success rate >95%

### KPI Generali
- 🎯 **User Satisfaction**: NPS >50
- ⚡ **Performance**: P95 latency <100ms
- 🐛 **Quality**: <1 critical bug/sprint
- 📊 **Coverage**: >90% code coverage
- 🚀 **Deployment**: <15min deploy time
- ⏱️ **MTTR**: <2h mean time to recovery

---

## 🔄 Workflow Issue

### 1. Creazione Issue
```
Nuovo issue → Template compilato → Label assegnate
```

### 2. Triage
```
needs-triage → Team review → Priorità + Epic + Effort → ready
```

### 3. Sprint Planning
```
ready issues → Sprint planning → Assign → in-progress
```

### 4. Sviluppo (BDD)
```
in-progress → Scrivi test (BDD) → Implementa → Test pass → needs-review
```

### 5. Review
```
needs-review → Code review → Approvato → Merge → Done
```

### 6. Tracking
```
Aggiorna Epic progress → Verifica metrics → Retrospective
```

---

## 💡 Best Practices

### Per Issue
1. **Titolo chiaro**: `[TYPE] Breve descrizione (max 60 char)`
2. **Scenario BDD**: Sempre includere Given-When-Then
3. **Acceptance Criteria**: Verificabili e testabili
4. **Context**: Spiegare PERCHÉ, non solo COSA
5. **Linking**: Collegare a Epic e dipendenze

### Per User Stories
1. **Atomic**: Una story = una feature testabile
2. **Independent**: Minimizzare dipendenze
3. **Negotiable**: Dettagli discussi con team
4. **Valuable**: Valore chiaro per utente
5. **Estimable**: Effort chiaro
6. **Small**: Completabile in uno sprint
7. **Testable**: Acceptance criteria verificabili

### Per Epic
1. **Business Value**: Chiaro valore per utenti/business
2. **Timeboxed**: Timeline realistica (1-3 mesi)
3. **Measurable**: Metriche di successo definite
4. **Decomposable**: Dividibile in user stories
5. **Progressive**: Valore incrementale

---

## 🚀 Quick Start

### Creare una nuova Issue
```bash
# Via GitHub CLI
gh issue create --template bug_report.md --label "type:bug,needs-triage"

# Via Web
# GitHub → Issues → New Issue → Scegli template
```

### Assegnare Issue a Epic
```bash
# Aggiungi label epic
gh issue edit 123 --add-label "epic:pdf-management"
```

### Creare Sprint Milestone
```bash
# Crea milestone
gh api repos/:owner/:repo/milestones -f title="Sprint 1" -f due_on="2025-11-01T23:59:59Z"

# Assegna issue
gh issue edit 123 --milestone "Sprint 1"
```

### Query Issue
```bash
# Issue ready per sprint
gh issue list --label "status:ready" --label "priority:high"

# Issue Epic-01
gh issue list --label "epic:pdf-management"

# Issue in progress
gh issue list --label "status:in-progress"
```
