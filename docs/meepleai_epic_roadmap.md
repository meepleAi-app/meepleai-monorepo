# MeepleAI - Epic e Roadmap

## 🎯 Visione del Prodotto

**MeepleAI** è un assistente AI per regolamenti di giochi da tavolo che processa PDF di regolamenti, fornisce ricerca semantica tramite vector embeddings e risponde intelligentemente a domande sul gameplay.

**Tech Stack:**
- Backend: ASP.NET Core 8.0, PostgreSQL, Qdrant, Redis
- Frontend: Next.js 14, React 18, TypeScript
- AI: OpenRouter API (embeddings + LLM)
- Infra: Docker Compose, n8n

---

## 📊 EPIC-01: Gestione Documenti PDF

**Obiettivo di Business:**
Permettere agli utenti di caricare regolamenti PDF e renderli immediatamente interrogabili tramite AI, eliminando la necessità di leggere manuali di 100+ pagine.

**Valore per l'Utente:**
- Caricamento semplice e veloce di regolamenti
- Validazione automatica del formato PDF
- Preview del documento prima dell'upload
- Feedback chiaro su errori e problemi
- Tracking del processo di indicizzazione

**Stato Attuale:**
- ✅ Upload wizard multi-step (upload.tsx - 44KB)
- ✅ Estrazione testo PDF (PdfTextExtractionService - Docnet.Core)
- ✅ Estrazione tabelle (PdfTableExtractionService - iText7)
- ✅ Chunking intelligente (TextChunkingService - 512 chars, 50 overlap)
- ✅ Embedding generation (EmbeddingService - OpenRouter)
- ✅ Storage su Qdrant (QdrantService)
- ⚠️ Gestione errori parziale
- ❌ Validazione PDF completa
- ❌ Preview PDF

**Criteri di Completamento:**
- [ ] Validazione formato PDF (tipo, dimensione, numero pagine)
- [ ] Preview PDF prima dell'upload con thumbnails
- [ ] Gestione errori dettagliata con messaggi user-friendly
- [ ] Progress tracking granulare (upload, extraction, chunking, embedding, indexing)
- [ ] Retry automatico per fallimenti transitori
- [ ] Support multi-file upload simultaneo
- [ ] Upload success rate >95%
- [ ] Tempo medio processing <30 secondi

**Timeline:** Q4 2025 - Q1 2026 (3 mesi)

**Metriche di Successo:**
- Upload success rate: >95%
- Average processing time: <30s
- User satisfaction (post-upload survey): >4/5
- Error recovery rate: >90%

**Feature Breakdown:**
1. Validazione PDF pre-upload
2. Preview PDF con zoom e navigation
3. Error handling robusto
4. Progress tracking granulare
5. Multi-file upload

**Dipendenze:**
- Nessuna dipendenza bloccante

**Rischi:**
- PDF corrotti o malformati
- File troppo grandi (>100MB)
- Formati PDF non standard

---

## 📊 EPIC-02: Ricerca Semantica e RAG

**Obiettivo di Business:**
Fornire risposte accurate e contestualizzate alle domande degli utenti, citando esattamente le sezioni pertinenti dei regolamenti.

**Valore per l'Utente:**
- Risposte immediate a domande sul gameplay
- Citazioni esatte con numeri di pagina
- Contesto completo dalla fonte originale
- Cache delle risposte frequenti per velocità
- Support multilingua per regolamenti internazionali

**Stato Attuale:**
- ✅ RAG pipeline funzionante (RagService)
- ✅ Semantic search su Qdrant
- ✅ Embedding queries (EmbeddingService)
- ✅ LLM response generation (LlmService)
- ⚠️ Response caching Redis (parziale - AiResponseCacheService)
- ⚠️ Citazioni senza page numbers
- ❌ Multilingua
- ❌ Fine-tuning

**Criteri di Completamento:**
- [ ] Citazioni con page numbers esatti
- [ ] Multi-language support (EN, IT, DE, FR, ES)
- [ ] Redis cache optimization (hit rate >70%)
- [ ] Response quality metrics tracking
- [ ] Search result ranking personalizzato
- [ ] Context window management ottimizzato
- [ ] Answer accuracy >85%
- [ ] Response time <2s (P95)

**Timeline:** Q1 2026 (2 mesi)

**Metriche di Successo:**
- Answer accuracy: >85%
- Response time P95: <2s
- Cache hit rate: >70%
- User satisfaction: >4.2/5
- Citation accuracy: >95%

**Feature Breakdown:**
1. Page number extraction e tracking
2. Multi-language embedding models
3. Redis cache optimization
4. Response quality scoring
5. Search ranking algorithm

**Dipendenze:**
- EPIC-01 completata (documenti indicizzati)

**Rischi:**
- Qualità embedding su lingue diverse
- Latenza OpenRouter API
- Cache invalidation strategy

---

## 📊 EPIC-03: Interfaccia Chat Intelligente

**Obiettivo di Business:**
Creare un'esperienza conversazionale naturale e fluida che renda l'interazione con l'AI intuitiva come chattare con un esperto di giochi.

**Valore per l'Utente:**
- Chat interface moderna e reattiva
- Storico conversazioni persistente
- Streaming responses in real-time
- Suggested follow-up questions
- Context switching tra documenti
- Voice input/output (futuro)

**Stato Attuale:**
- ✅ Chat interface base (chat.tsx - 14.3KB)
- ✅ Message history (ChatEntity, ChatLogEntity)
- ✅ AI request logging (AiRequestLogService)
- ✅ Correlation ID tracking
- ❌ Streaming responses
- ❌ Suggested questions
- ❌ Document context switching
- ❌ Voice I/O

**Criteri di Completamento:**
- [ ] Streaming responses (Server-Sent Events)
- [ ] AI-generated follow-up questions
- [ ] Multi-document context switching
- [ ] Loading states e skeleton screens
- [ ] Message editing e deletion
- [ ] Conversation branching
- [ ] Export conversation (PDF/TXT)
- [ ] User engagement >70%
- [ ] Session duration >5min

**Timeline:** Q4 2025 - Q1 2026 (3 mesi)

**Metriche di Successo:**
- User engagement rate: >70%
- Average session duration: >5min
- Messages per session: >8
- Follow-up question usage: >40%
- User satisfaction: >4.3/5

**Feature Breakdown:**
1. SSE streaming implementation
2. Follow-up question generation
3. Context switching UI
4. UX polish (loading, animations)
5. Conversation management

**Dipendenze:**
- EPIC-02 (RAG) per risposte accurate

**Rischi:**
- SSE compatibility browser
- Context switching complexity
- Performance con chat lunghe

---

## 📊 EPIC-04: Editor Regolamenti e Versioning

**Obiettivo di Business:**
Permettere editing collaborativo dei regolamenti e tracking delle modifiche nel tempo, utile per errata e aggiornamenti ufficiali.

**Valore per l'Utente:**
- Edit collaborativo dei regolamenti
- Confronto visuale tra versioni
- Change tracking dettagliato
- Timeline delle modifiche
- Comments e annotations
- Merge di modifiche

**Stato Attuale:**
- ✅ Editor base (editor.tsx - 15.7KB)
- ✅ Version comparison (versions.tsx - 20.1KB)
- ✅ RuleSpecDiffService
- ✅ RuleSpecService CRUD
- ✅ Game metadata management
- ⚠️ Rich text editing limitato
- ❌ Visual change tracking
- ❌ Collaborative editing

**Criteri di Completamento:**
- [ ] Rich text editor (TipTap o Slate.js)
- [ ] Visual diff viewer con highlights
- [ ] User comments e annotations
- [ ] Version timeline UI
- [ ] Conflict resolution per merge
- [ ] Real-time collaborative editing
- [ ] Version comparison usage >40%
- [ ] Edit satisfaction >4/5

**Timeline:** Q1-Q2 2026 (3 mesi)

**Metriche di Successo:**
- Version comparison usage: >40% utenti
- Edit time reduction: >50%
- Collaboration adoption: >30%
- User satisfaction: >4/5

**Feature Breakdown:**
1. Rich text editor integration
2. Visual diff improvements
3. Comments system
4. Timeline UI
5. Real-time collaboration (WebSocket)

**Dipendenze:**
- Nessuna dipendenza bloccante

**Rischi:**
- Complessità real-time sync
- Conflict resolution strategy
- Performance con documenti grandi

---

## 📊 EPIC-05: Amministrazione e Monitoring

**Obiettivo di Business:**
Fornire agli admin visibilità completa sull'uso del sistema, performance e comportamenti degli utenti per ottimizzare l'esperienza e identificare problemi.

**Valore per l'Utente (Admin):**
- Dashboard analytics completo
- User management centralizzato
- Usage metrics e trends
- Performance monitoring
- Rate limiting configurabile
- Audit trail completo

**Stato Attuale:**
- ✅ Admin dashboard base (admin.tsx - 14.2KB)
- ✅ Activity logs (logs.tsx - 6.9KB)
- ✅ Audit logging (AuditService)
- ✅ AI request monitoring (AiRequestLogService)
- ✅ Rate limiting (RateLimitService)
- ⚠️ User management UI limitato
- ❌ Analytics dashboard
- ❌ Performance metrics

**Criteri di Completamento:**
- [ ] User management UI completo (create, edit, delete, roles)
- [ ] Analytics dashboard con charts
- [ ] Usage metrics (MAU, DAU, retention)
- [ ] Performance monitoring (latency, errors)
- [ ] Rate limiting dashboard
- [ ] Export reports (CSV, PDF)
- [ ] Alert system per anomalie
- [ ] Admin task time <5min

**Timeline:** Q1-Q2 2026 (2 mesi)

**Metriche di Successo:**
- Admin task completion: <5min avg
- System uptime: >99.5%
- Issue detection time: <15min
- Admin satisfaction: >4.5/5

**Feature Breakdown:**
1. User management CRUD
2. Analytics dashboard (Chart.js)
3. Performance metrics
4. Rate limiting UI
5. Alert system

**Dipendenze:**
- Logging infrastructure già presente

**Rischi:**
- Performance query analytics
- Alert fatigue
- Privacy compliance (GDPR)

---

## 📊 EPIC-06: Automazione Workflow (n8n)

**Obiettivo di Business:**
Automatizzare processi ripetitivi e integrazioni con servizi esterni per ridurre lavoro manuale e migliorare efficienza.

**Valore per l'Utente:**
- Notifiche automatiche
- Sync con BoardGameGeek
- Aggiornamenti regolamenti automatici
- Email notifications
- Backup automatici
- Custom workflows

**Stato Attuale:**
- ✅ n8n integration (n8n.tsx - 16KB)
- ✅ N8nConfigService
- ✅ Background task service
- ✅ Webhook endpoints
- ❌ Workflow templates
- ❌ Event triggers
- ❌ BGG integration
- ❌ Email notifications

**Criteri di Completamento:**
- [ ] Workflow templates library (10+ templates)
- [ ] Event triggers dal sistema
- [ ] BGG API integration
- [ ] Email notification system
- [ ] Automated backups
- [ ] Custom webhook support
- [ ] Time saved >10h/week
- [ ] Workflow success rate >95%

**Timeline:** Q2 2026 (2 mesi)

**Metriche di Successo:**
- Time saved per week: >10h
- Workflow success rate: >95%
- Automation adoption: >50% utenti
- Integration uptime: >99%

**Feature Breakdown:**
1. Workflow templates pre-built
2. Event trigger system
3. BGG API client
4. Email service integration
5. Backup automation

**Dipendenze:**
- n8n infrastructure già presente

**Rischi:**
- BGG API rate limits
- Email deliverability
- Workflow debugging complexity

---

## 📊 EPIC-07: Autenticazione e Sicurezza

**Obiettivo di Business:**
Sistema di autenticazione robusto e sicuro che protegga i dati degli utenti e prevenga accessi non autorizzati.

**Valore per l'Utente:**
- Login sicuro e veloce
- OAuth providers (Google, Discord)
- 2FA per account premium
- Password reset semplice
- Session management
- SSO per enterprise (futuro)

**Stato Attuale:**
- ✅ Session-based auth (AuthService)
- ✅ Cookie management
- ✅ User roles (UserRole enum)
- ✅ User sessions (UserSessionEntity)
- ❌ OAuth providers
- ❌ 2FA
- ❌ Password reset
- ❌ SSO enterprise

**Criteri di Completamento:**
- [ ] OAuth providers (Google, Discord, GitHub)
- [ ] 2FA con TOTP (Authenticator apps)
- [ ] Password reset flow completo
- [ ] Account recovery options
- [ ] Security audit logging
- [ ] Brute force protection
- [ ] Security incidents = 0
- [ ] Auth success rate >99%

**Timeline:** Q1 2026 (2 mesi)

**Metriche di Successo:**
- Security incidents: 0
- Auth success rate: >99%
- OAuth adoption: >60%
- 2FA adoption: >30%
- Password reset time: <2min

**Feature Breakdown:**
1. OAuth integration (Google, Discord)
2. 2FA implementation
3. Password reset flow
4. Security audit improvements
5. Brute force protection

**Dipendenze:**
- Auth infrastructure già presente

**Rischi:**
- OAuth provider downtime
- 2FA recovery scenarios
- Security compliance (GDPR, CCPA)

---

## 📊 EPIC-08: Testing e Quality Assurance

**Obiettivo di Business:**
Garantire qualità del software, ridurre bug in produzione e velocizzare i cicli di sviluppo tramite testing automatizzato.

**Valore per l'Utente (Team):**
- Software affidabile
- Meno bug in produzione
- Deploy sicuri e veloci
- Confidence nel refactoring
- Documentazione vivente (test as docs)

**Stato Attuale:**
- ✅ xUnit + Testcontainers (API)
- ✅ Jest + Testing Library (Web)
- ✅ Playwright (E2E)
- ✅ GitHub Actions CI
- ⚠️ Coverage ~70% (target 90%)
- ⚠️ E2E suite incompleta
- ❌ Performance testing
- ❌ Load testing

**Criteri di Completamento:**
- [ ] Code coverage >90% (backend + frontend)
- [ ] E2E test suite completa (happy paths + edge cases)
- [ ] Visual regression testing
- [ ] Performance testing (Lighthouse CI)
- [ ] Load testing (k6)
- [ ] Mutation testing
- [ ] CI success rate >95%
- [ ] MTTR <2h

**Timeline:** Continuo (ogni sprint)

**Metriche di Successo:**
- Code coverage: >90%
- CI success rate: >95%
- Bugs escaped to prod: <1/sprint
- MTTR: <2h
- Test execution time: <10min

**Feature Breakdown:**
1. Aumentare coverage a 90%
2. E2E test suite completa
3. Visual regression (Percy/Chromatic)
4. Performance testing
5. Load testing setup

**Dipendenze:**
- Tutte le Epic (test per ogni feature)

**Rischi:**
- Test maintenance overhead
- Flaky tests
- CI pipeline time

---

## 🗓️ ROADMAP 6 MESI (Q4 2025 - Q1 2026)

### Sprint 1-2: FOUNDATION (Ottobre 2025)

**Focus:** Stabilizzare sistema esistente

**Obiettivi:**
- Sistema stabile pronto per beta users
- Coverage >90%
- Bug critici risolti
- Docker setup ottimizzato

**User Stories:**
1. [STORY] Aumentare coverage backend a 90%
2. [STORY] Aumentare coverage frontend a 90%
3. [TECH] Fix Docker healthchecks (FATTO)
4. [TECH] Ottimizzare CI pipeline
5. [BUG] Fix session timeout random
6. [STORY] Password reset flow

**Deliverables:**
- ✅ Tutti i container Docker healthy
- ✅ Coverage >90% backend e frontend
- ✅ Password reset funzionante
- ✅ CI pipeline <10min

**Milestone:** Beta-Ready System

---

### Sprint 3-4: PDF ENHANCEMENT (Novembre 2025)

**Focus:** EPIC-01 - Upload robusto

**Obiettivi:**
- Upload production-ready
- Validazione completa
- Preview PDF
- Error handling robusto

**User Stories:**
1. [STORY] Validazione PDF pre-upload
2. [STORY] Preview PDF con thumbnails
3. [STORY] Error messages user-friendly
4. [STORY] Progress tracking granulare
5. [STORY] Multi-file upload
6. [TECH] Retry logic per upload failures

**Deliverables:**
- ✅ Validazione PDF (tipo, size, pages)
- ✅ Preview con zoom e navigation
- ✅ Error handling completo
- ✅ Progress bar dettagliato
- ✅ Support multi-file

**Milestone:** Production-Ready Upload

**Metriche Target:**
- Upload success rate: >95%
- Processing time: <30s
- User satisfaction: >4/5

---

### Sprint 5-6: SMART CHAT (Dicembre 2025)

**Focus:** EPIC-03 - Chat experience

**Obiettivi:**
- Streaming responses
- Follow-up questions
- Context switching
- UX polish

**User Stories:**
1. [STORY] Streaming responses con SSE
2. [STORY] AI-generated follow-up questions
3. [STORY] Document context switching
4. [STORY] Loading states e animations
5. [TECH] Performance optimization chat
6. [STORY] Export conversation

**Deliverables:**
- ✅ SSE streaming funzionante
- ✅ Follow-up questions intelligenti
- ✅ Switch tra documenti
- ✅ UX polished
- ✅ Export PDF/TXT

**Milestone:** v1.0 Chat Release

**Metriche Target:**
- User engagement: >70%
- Session duration: >5min
- Follow-up usage: >40%

---

### Sprint 7-8: SEARCH OPTIMIZATION (Gennaio 2026)

**Focus:** EPIC-02 - RAG migliorato

**Obiettivi:**
- Citazioni con page numbers
- Multi-language
- Cache optimization
- Quality metrics

**User Stories:**
1. [STORY] Page number extraction
2. [STORY] Multi-language embeddings
3. [TECH] Redis cache optimization
4. [STORY] Response quality scoring
5. [TECH] Search ranking algorithm
6. [STORY] Citation accuracy tracking

**Deliverables:**
- ✅ Page numbers nelle citazioni
- ✅ Support 5 lingue (EN, IT, DE, FR, ES)
- ✅ Cache hit rate >70%
- ✅ Quality metrics dashboard
- ✅ Ranking personalizzato

**Milestone:** Smart Answers v2.0

**Metriche Target:**
- Answer accuracy: >85%
- Response time: <2s
- Cache hit rate: >70%

---

### Sprint 9-10: COLLABORATION (Febbraio 2026)

**Focus:** EPIC-04 - Editor avanzato

**Obiettivi:**
- Rich text editor
- Visual diff
- Comments system
- Timeline UI

**User Stories:**
1. [STORY] Rich text editor integration
2. [STORY] Visual diff con highlights
3. [STORY] Comments e annotations
4. [STORY] Version timeline
5. [TECH] Conflict resolution
6. [STORY] Real-time collaboration (beta)

**Deliverables:**
- ✅ TipTap/Slate editor
- ✅ Diff viewer migliorato
- ✅ Comment system
- ✅ Timeline visuale
- ⚠️ Real-time collab (beta)

**Milestone:** Collaborative Editing Beta

**Metriche Target:**
- Version comparison: >40%
- Edit time: -50%
- User satisfaction: >4/5

---

### Sprint 11-12: ADMIN & ANALYTICS (Marzo 2026)

**Focus:** EPIC-05 - Admin completo

**Obiettivi:**
- User management
- Analytics dashboard
- Performance monitoring
- Rate limiting UI

**User Stories:**
1. [STORY] User management CRUD
2. [STORY] Analytics dashboard
3. [STORY] Usage metrics tracking
4. [STORY] Performance monitoring
5. [STORY] Rate limiting dashboard
6. [STORY] Export reports

**Deliverables:**
- ✅ User management UI
- ✅ Analytics con charts
- ✅ Metrics dashboard
- ✅ Performance monitoring
- ✅ Report export

**Milestone:** Full Admin Suite

**Metriche Target:**
- Admin task time: <5min
- System uptime: >99.5%
- Admin satisfaction: >4.5/5

---

## 📈 METRICHE DI SUCCESSO GLOBALI

### Performance
- **P95 Latency:** <100ms (API endpoints)
- **P95 Response Time:** <2s (AI responses)
- **Upload Processing:** <30s average
- **System Uptime:** >99.5%

### Quality
- **Code Coverage:** >90%
- **CI Success Rate:** >95%
- **Bugs in Production:** <1/sprint
- **MTTR:** <2h

### User Engagement
- **MAU:** 500+ (entro Q2 2026)
- **DAU/MAU Ratio:** >30%
- **Session Duration:** >5min
- **Messages per Session:** >8
- **Retention (30d):** >50%

### Business
- **NPS:** >50
- **User Satisfaction:** >4.2/5
- **Feature Adoption:** >60% per feature principale
- **Support Tickets:** <10/week

---

## 🎯 PRIORITÀ ISSUE CONSIGLIATE

### Immediate (Sprint 1-2)
1. **[TECH] Coverage backend a 90%** - EPIC-08, effort:L, priority:critical
2. **[TECH] Coverage frontend a 90%** - EPIC-08, effort:L, priority:critical
3. **[STORY] Password reset flow** - EPIC-07, effort:M, priority:high
4. **[BUG] Session timeout random** - EPIC-07, effort:S, priority:high

### High Priority (Sprint 3-4)
1. **[STORY] Validazione PDF pre-upload** - EPIC-01, effort:M, priority:high
2. **[STORY] Preview PDF** - EPIC-01, effort:L, priority:high
3. **[STORY] Error handling robusto** - EPIC-01, effort:M, priority:high

### Next (Sprint 5-6)
1. **[STORY] Streaming responses SSE** - EPIC-03, effort:L, priority:high
2. **[STORY] Follow-up questions** - EPIC-03, effort:M, priority:medium
3. **[STORY] Context switching** - EPIC-03, effort:M, priority:medium

---

## 🔄 WORKFLOW AGILE

### Sprint Planning (ogni 2 settimane)
1. Review roadmap
2. Select user stories from backlog
3. Estimate effort (story points)
4. Assign to team members
5. Create GitHub milestone

### Daily Standup
1. Cosa ho fatto ieri
2. Cosa farò oggi
3. Blockers/impedimenti

### Sprint Review (fine sprint)
1. Demo delle feature completate
2. Feedback da stakeholders
3. Update metrics dashboard

### Sprint Retrospective
1. Cosa è andato bene
2. Cosa migliorare
3. Action items

---

## 📚 RISORSE UTILI

### Documentazione
- [CLAUDE.md](CLAUDE.md) - Contesto progetto per Claude
- [backend-api.md](.claude/backend-api.md) - Backend architecture
- [frontend-web.md](.claude/frontend-web.md) - Frontend architecture
- [infra-docker.md](.claude/infra-docker.md) - Infrastructure

### Tool
- GitHub Projects - Kanban board
- GitHub Issues - Issue tracking
- GitHub Actions - CI/CD
- Docker Compose - Local dev

### Comandi Rapidi
```bash
# Issue management
gh issue create --template user_story.md
gh issue list --label "epic:pdf-management"
gh issue edit 123 --add-label "status:ready"

# Sprint management
gh api repos/:owner/:repo/milestones -f title="Sprint 1"
gh issue edit 123 --milestone "Sprint 1"
```