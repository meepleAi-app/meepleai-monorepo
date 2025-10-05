# MeepleAI Backlog (Feature List)

| ID | Title | Component | Type | Priority | Effort | Labels | Milestone | Dependencies |
|---|---|---|---|---|---:|---|---|---|
| INF-01 | Inizializzare monorepo e Docker Compose | Infra | Task | P0 | 3 | area/infra,kind/task | MVP | nan |
| INF-02 | CI/CD base GitHub Actions | Infra | Task | P0 | 3 | area/infra,kind/ci | MVP | INF-01 |
| SEC-01 | Gestione secrets .env e rotazione chiavi | Security | Task | P0 | 2 | area/security,kind/policy | MVP | INF-01 |
| AUTH-01 | Autenticazione e ruoli (Admin/Editor/User) | Auth | Feature | P0 | 5 | area/auth,kind/feature | MVP | INF-01 |
| AUTH-02 | Baseline accessi in contesto condiviso | Auth | Feature | P0 | 5 | area/auth,kind/feature | V1 | AUTH-01,DB-01 |
| DB-01 | Schema database iniziale | DB | Task | P0 | 3 | area/db,kind/schema | MVP | INF-01 |
| RULE-01 | Definizione formale RuleSpec v0 | RuleSpec | Feature | P0 | 3 | area/rulespec,kind/spec | MVP | DB-01 |
| RULE-02 | Versioning e diff RuleSpec | RuleSpec | Feature | P1 | 3 | area/rulespec,kind/feature | V1 | RULE-01,DB-01 |
| PDF-01 | Upload PDF regolamenti | PDF | Feature | P0 | 3 | area/pdf,kind/feature | MVP | AUTH-01,DB-01 |
| PDF-02 | Parsing testo + OCR | PDF | Feature | P0 | 5 | area/pdf,kind/feature,ai/nlp | MVP | PDF-01 |
| PDF-03 | Parser tabelle/flowchart | PDF | Feature | P1 | 5 | area/pdf,kind/feature,ai/nlp | V1 | PDF-02 |
| EDIT-01 | Editor grafico RuleSpec | Editor | Feature | P1 | 5 | area/ui,area/rulespec,kind/feature | V1 | RULE-01,PDF-02 |
| EDIT-02 | Diff e versioni RuleSpec in UI | Editor | Feature | P2 | 3 | area/ui,area/rulespec,kind/feature | V1 | EDIT-01,RULE-02 |
| AI-01 | Embeddings e indicizzazione Qdrant | AI | Feature | P0 | 5 | area/ai,kind/feature | MVP | PDF-02,DB-01 |
| AI-02 | RAG Explain (10 minuti) | AI | Feature | P0 | 5 | area/ai,kind/feature | MVP | AI-01,RULE-01 |
| AI-03 | RAG Setup guidato | AI | Feature | P1 | 3 | area/ai,kind/feature | V1 | AI-01,RULE-01 |
| AI-04 | Q&A con snippet e fallback Not specified | AI | Feature | P1 | 3 | area/ai,kind/feature | V1 | AI-01 |
| AI-05 | Caching risposte per gioco | AI | Task | P2 | 3 | area/ai,kind/perf | V1 | AI-02,AI-03 |
| UI-01 | Chat UI per ogni Meeple Agent | UI | Feature | P0 | 3 | area/ui,kind/feature | MVP | AI-02 |
| UI-02 | Libreria giochi (CRUD) | UI | Feature | P0 | 3 | area/ui,kind/feature | MVP | AUTH-01,DB-01 |
| UI-03 | Wizard import PDF→RuleSpec | UI | Feature | P1 | 3 | area/ui,kind/feature | V1 | PDF-02,EDIT-01 |
| ADM-01 | Dashboard admin e log richieste | Admin | Feature | P1 | 3 | area/admin,kind/feature | V1 | AI-02 |
| ADM-02 | Gestione n8n workflow | Admin | Feature | P2 | 3 | area/admin,kind/feature | V2 | INF-01 |
| N8N-01 | Webhook /agent/explain | n8n | Feature | P0 | 3 | area/automations,kind/feature | MVP | AI-02 |
| N8N-02 | Webhook /agent/setup | n8n | Feature | P1 | 2 | area/automations,kind/feature | V1 | AI-03 |
| N8N-03 | Webhook /agent/qa | n8n | Feature | P1 | 2 | area/automations,kind/feature | V1 | AI-04 |
| PERF-01 | Rate limiting (Redis token bucket) | Perf | Task | P1 | 2 | area/perf,kind/security | V1 | INF-01 |
| SEC-02 | Audit & RLS tests | Security | Task | P1 | 3 | area/security,kind/test | V1 | AUTH-02,DB-01 |
| OPS-01 | Observability base | Ops | Task | P2 | 3 | area/ops,kind/infra | V1 | INF-02 |
| DOC-01 | README progetto (root) | Docs | Task | P0 | 2 | area/docs,kind/docs | MVP | INF-01 |
| DOC-02 | agents.md per Codex/Claude Code | Docs | Task | P0 | 3 | area/docs,kind/docs | MVP | nan |
| DOC-03 | CONTRIBUTING e SECURITY | Docs | Task | P1 | 2 | area/docs,kind/docs | V1 | SEC-01 |