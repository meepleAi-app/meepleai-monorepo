# Record of Processing Activities — LLM Subsystem

**Issue**: #5515 | **Epic**: #5506 (GDPR Compliance for LLM Subsystem)
**Date**: 2026-03-09 | **Legal Basis**: GDPR Art. 30

## Controller Information

| Field | Value |
|-------|-------|
| **Controller** | MeepleAI |
| **Contact** | [Data Protection Contact] |
| **DPO** | [If appointed] |

## Processing Activity 1: AI-Powered Question Answering

| Field | Details |
|-------|---------|
| **Activity Name** | AI-assisted board game rules Q&A |
| **Purpose** | Provide users with AI-powered answers about board game rules, strategies, and gameplay |
| **Legal Basis** | Legitimate interest (Art. 6(1)(f)) for self-hosted processing; Consent (Art. 6(1)(a)) for external provider processing |
| **Data Subjects** | Registered users (authenticated), anonymous visitors (limited) |

### Categories of Personal Data

| Category | Examples | Sensitivity |
|----------|---------|-------------|
| User queries | "How does trading work in Catan?" | Low |
| Chat history | Recent conversation context (10 messages) | Low |
| User preferences | Game interests, AI consent status | Low |
| Technical identifiers | Pseudonymized UserId, session ID | Low |

### Categories of Recipients

| Recipient | Role | Location | Safeguards |
|-----------|------|----------|------------|
| Ollama (self-hosted) | Primary LLM processor | EU (self-hosted) | Full data control, no third-party transfer |
| OpenRouter, Inc. | Secondary LLM processor | USA | DPA, SCCs, PII stripping (#5510) |
| Upstream LLM providers | Sub-processors via OpenRouter | Various | Managed by OpenRouter DPA |

### International Transfers

| Transfer | Mechanism | Safeguards |
|----------|-----------|------------|
| EU → Self-hosted (Ollama) | No transfer (EU-based) | Full control |
| EU → USA (OpenRouter) | SCCs (Art. 46(2)(c)) | PII stripping, TIA (#5508), DPA (#5507) |

### Retention Periods

| Data Type | Retention | Mechanism |
|-----------|-----------|-----------|
| LLM request logs (DB) | 30 days | `ExpiresAt` + cleanup job |
| LLM request logs (pseudonymized) | 30 days total | UserId hashed after 7 days (#5511), deleted at 30 days |
| JSONL file logs | 30 days | Daily rotation, 30-file retention limit |
| Conversation memories | 90 days | Cleanup job (`ConversationMemoryCleanupJob`) |
| AI consent records | Duration of account | Deleted on account deletion |
| Aggregated analytics | 1 year | No PII (model usage stats only) |

### Technical & Organizational Measures (Art. 32)

| Measure | Implementation | Issue |
|---------|---------------|-------|
| PII detection & stripping | Regex-based PII detector before external API calls | #5510 |
| Pseudonymization | SHA-256 hashed UserIds in logs after 7 days | #5511 |
| Local-first routing | 80% of requests processed by self-hosted Ollama | #5087 |
| Encryption in transit | TLS 1.3 for all API communications | Infrastructure |
| Access control | Admin-only access to LLM logs and analytics | Role-based |
| Right to erasure | `DeleteUserLlmDataCommand` — 3-store deletion | #5509 |
| Consent management | Explicit AI consent tracking with version control | #5512 |
| Opt-out mechanism | Users can disable AI features entirely | #5513 |
| Log hashing | UserIds hashed before writing to JSONL files | Existing |
| Data minimization | Only necessary context sent to LLM providers | By design |

## Processing Activity 2: LLM Usage Analytics

| Field | Details |
|-------|---------|
| **Activity Name** | LLM request monitoring and cost analytics |
| **Purpose** | Monitor API costs, usage patterns, model performance for operational purposes |
| **Legal Basis** | Legitimate interest (Art. 6(1)(f)) — operational necessity |
| **Data Subjects** | Registered users (indirectly, via pseudonymized logs) |

### Data Processed

| Category | Contains PII? | Retention |
|----------|--------------|-----------|
| Model usage statistics | No | 1 year (aggregated) |
| Cost tracking per request | Pseudonymized UserId | 30 days |
| Error rates and latency | No | 30 days |
| Rate limiting counters | Session-level | Transient (Redis TTL) |

## Processing Activity 3: Conversation Memory

| Field | Details |
|-------|---------|
| **Activity Name** | Conversation context persistence |
| **Purpose** | Maintain chat context for coherent multi-turn AI interactions |
| **Legal Basis** | Contract performance (Art. 6(1)(b)) — necessary for service functionality |
| **Data Subjects** | Registered users |

### Data Processed

| Category | Contains PII? | Retention |
|----------|--------------|-----------|
| Message content | Potentially (user-entered text) | 90 days |
| Session identifiers | Pseudonymized | 90 days |
| Vector embeddings | No (mathematical representations) | 90 days |
| Message metadata | Timestamps, message type | 90 days |

## Review & Maintenance

| Activity | Frequency | Responsible |
|----------|-----------|-------------|
| Record update | On processing changes | Development team |
| Accuracy review | Semi-annual | Data protection contact |
| Retention compliance | Automated (cleanup jobs) | System |
| Rights request handling | On request (Art. 15-22) | Operations team |

---

*This record is maintained pursuant to GDPR Art. 30(1) and is available for supervisory authority inspection upon request.*
