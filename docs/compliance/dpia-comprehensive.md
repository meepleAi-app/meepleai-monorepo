# Data Protection Impact Assessment (DPIA) -- Comprehensive

**GDPR Article 35 | Regulation (EU) 2016/679**

| Field | Value |
|-------|-------|
| **Document ID** | DPIA-2026-001 |
| **Version** | 1.0 |
| **Date** | 2026-03-10 |
| **Controller** | Aaron Degrassi, operating as MeepleAI |
| **Controller Address** | Via Giuseppe Verdi 78, 33050 Perteole (UD), Italy |
| **Contact** | privacy@meepleai.com |
| **Supervisory Authority** | Garante per la protezione dei dati personali (Italy) |
| **Prepared By** | Aaron Degrassi |
| **Review Date** | 2027-03-10 (annual) |
| **Classification** | Internal -- Restricted |

---

## Table of Contents

1. [Introduction and Scope](#1-introduction-and-scope)
2. [DPIA Screening -- Threshold Assessment](#2-dpia-screening--threshold-assessment)
3. [Description of Processing Operations](#3-description-of-processing-operations)
4. [Data Inventory](#4-data-inventory)
5. [Data Flows](#5-data-flows)
6. [Legal Basis and Purpose Limitation](#6-legal-basis-and-purpose-limitation)
7. [Assessment of Necessity and Proportionality](#7-assessment-of-necessity-and-proportionality)
8. [Stakeholder Consultation](#8-stakeholder-consultation)
9. [Risk Assessment](#9-risk-assessment)
10. [Measures to Address Risks](#10-measures-to-address-risks)
11. [Residual Risk Assessment](#11-residual-risk-assessment)
12. [Data Subject Rights](#12-data-subject-rights)
13. [International Transfers](#13-international-transfers)
14. [Sub-Processor Management](#14-sub-processor-management)
15. [Data Retention Schedule](#15-data-retention-schedule)
16. [Breach Notification](#16-breach-notification)
17. [Conclusion and Sign-Off](#17-conclusion-and-sign-off)
18. [Review Schedule and Version History](#18-review-schedule-and-version-history)
19. [References](#19-references)

---

## 1. Introduction and Scope

### 1.1 Purpose

This Data Protection Impact Assessment (DPIA) is conducted pursuant to Article 35 of the General Data Protection Regulation (EU) 2016/679 to systematically assess the risks that MeepleAI's data processing activities pose to the rights and freedoms of natural persons, and to identify appropriate measures to mitigate those risks.

This document supersedes and incorporates the simplified LLM-focused DPIA (`dpia-llm.md`) and expands the assessment to cover all processing activities across the MeepleAI platform.

### 1.2 About MeepleAI

MeepleAI is an AI-powered board game assistant offered as a Software-as-a-Service (SaaS) application. The platform provides:

- **AI Chat**: Retrieval-Augmented Generation (RAG) and multi-agent AI to answer board game rules and strategy questions
- **Document Processing**: PDF upload and OCR processing of board game rulebooks for AI knowledge base ingestion
- **Game Library Management**: Personal collections, wishlists, play history, and session tracking
- **Community Game Catalog**: Shared, community-contributed game database with administrative moderation
- **Gamification**: Achievement and badge system for user engagement

The platform is designed for board game enthusiasts, primarily located in the European Union.

### 1.3 Scope

This DPIA covers all personal data processing activities performed by the MeepleAI platform, including:

- User account registration and authentication
- AI-powered question answering (self-hosted and external provider)
- PDF document upload and processing
- Game library and session management
- Community catalog contributions
- Administrative monitoring and analytics
- Background data lifecycle management

Processing activities are organized across 15 bounded contexts within the application architecture. This assessment addresses the platform holistically rather than treating each context in isolation.

---

## 2. DPIA Screening -- Threshold Assessment

### 2.1 Article 35(1) Threshold Test

A DPIA is required when processing is "likely to result in a high risk to the rights and freedoms of natural persons." The European Data Protection Board (EDPB) Guidelines on DPIA (WP 248 rev.01) provide nine criteria; meeting two or more typically triggers a mandatory DPIA.

| # | EDPB Criterion | Applicable? | Assessment |
|---|----------------|-------------|------------|
| 1 | Evaluation or scoring | No | No user profiling, credit scoring, or behavioral prediction |
| 2 | Automated decision-making with legal or similarly significant effects | No | AI provides informational responses only; no decisions affecting legal rights, access to services, or contractual terms |
| 3 | Systematic monitoring | No | No surveillance, location tracking, or systematic observation of data subjects |
| 4 | Sensitive data or data of a highly personal nature | No | Board game data only; no Art. 9 special categories, no financial data, no health data |
| 5 | Data processed on a large scale | Partial | Moderate user base expected; limited personal data per interaction |
| 6 | Matching or combining datasets | No | Isolated processing per user; no cross-referencing with external datasets |
| 7 | Data concerning vulnerable data subjects | No | General adult consumers; no children, employees, or patients |
| 8 | Innovative use or applying new technological or organizational solutions | Partial | LLM and RAG technology is established but evolving; vector embeddings of user-uploaded documents involve novel processing |
| 9 | Processing that prevents data subjects from exercising a right or using a service or contract | No | Users can fully use the platform without AI features; opt-out available |

**Cross-border transfer** (not in the nine criteria but relevant): EU-to-USA transfer occurs for approximately 20% of LLM requests via OpenRouter.

### 2.2 Screening Conclusion

**Criteria met: 1.5 out of 9** (partial matches on criteria 5 and 8).

A full DPIA is **not strictly mandatory** under the two-criteria threshold. However, this comprehensive DPIA is conducted as **best practice** given:

- Cross-border data transfer to the United States
- Use of AI/LLM technology with evolving regulatory expectations
- The Italian supervisory authority's published DPIA lists
- Proactive compliance posture and accountability principle (Art. 5(2))

---

## 3. Description of Processing Operations

### 3.1 Processing Activities Overview

| # | Activity | Nature | Context | Purpose |
|---|----------|--------|---------|---------|
| PA-01 | User Account Management | Registration, authentication (password + OAuth + 2FA), session management, profile CRUD | Consumer SaaS application | Provide secure access to the platform |
| PA-02 | AI Chat Processing | RAG queries, multi-agent inference, vector search, conversation memory | AI-assisted leisure application | Answer board game rules/strategy questions |
| PA-03 | PDF Document Processing | Upload, OCR, text extraction, chunking, vector embedding generation | Knowledge base construction | Build searchable AI knowledge base from rulebooks |
| PA-04 | Game Library Management | Collections, wishlists, play history, ratings, session notes | Personal game tracking | Help users organize and track their board game hobby |
| PA-05 | Community Shared Catalog | Game metadata curation, admin moderation, soft-delete | Community contribution platform | Maintain a shared board game reference database |
| PA-06 | Gamification | Achievements, badges, experience points, leaderboards | User engagement | Incentivize platform participation |
| PA-07 | System Monitoring | Audit logging, usage analytics, cost tracking, rate limiting | Operational management | Ensure platform stability, manage costs, detect abuse |
| PA-08 | Notification System | In-app alerts, email notifications (planned) | User communication | Inform users of relevant platform events |
| PA-09 | Entity Relationships | Game-to-game links (expansions, sequels, companions) | Content enrichment | Provide game relationship context |
| PA-10 | Session Attachments (planned) | Photo uploads of board state during game sessions | Play session documentation | Visual documentation of gameplay |

### 3.2 Technical Architecture

| Component | Technology | Location | Purpose |
|-----------|-----------|----------|---------|
| API Server | .NET 9 (ASP.NET Minimal APIs, MediatR) | Hetzner Cloud, Germany/Finland (EU) | Application logic, CQRS command/query processing |
| Frontend | Next.js 16, React 19 | Vercel (EU CDN) | User interface |
| Primary Database | PostgreSQL 16 with pgvector | Hetzner Cloud (EU) | Relational data storage |
| Cache/Session Store | Redis 7 | Hetzner Cloud (EU) | Session management, rate limiting, transient caching |
| Vector Database | Qdrant | Hetzner Cloud (EU) | AI embedding storage for RAG retrieval |
| Self-hosted LLM | Ollama | Hetzner Cloud (EU) | Local AI inference (~80% of requests) |
| Embedding Service | Python (sentence-transformers) | Hetzner Cloud (EU) | Text-to-vector conversion |
| Reranker Service | Python (cross-encoder) | Hetzner Cloud (EU) | Search result reranking |
| Document Service | Python (SmolDocling/Unstructured) | Hetzner Cloud (EU) | PDF OCR and text extraction |
| Reverse Proxy | Traefik | Hetzner Cloud (EU) | TLS termination, routing |

All infrastructure is containerized via Docker Compose and hosted within the European Union.

### 3.3 Processing Logic

The CQRS (Command Query Responsibility Segregation) pattern governs all data operations:

1. **Commands** (write operations) pass through FluentValidation before reaching handlers
2. **Queries** (read operations) return DTOs with only necessary fields
3. All operations are mediated through MediatR -- no direct service injection at endpoint level
4. Domain entities use private setters and factory methods, enforcing invariants at the domain layer

---

## 4. Data Inventory

### 4.1 Categories of Personal Data

| Category | Data Fields | Sensitivity | Storage |
|----------|------------|-------------|---------|
| **Identity Data** | Email address, display name | Medium | PostgreSQL |
| **Authentication Data** | Password hash (PBKDF2-SHA256, 210,000 iterations), session token hashes (SHA-256), OAuth access/refresh tokens (AES encrypted), TOTP secrets (encrypted), backup codes (SHA-256 hashed) | High | PostgreSQL |
| **Technical/Log Data** | IP address, user agent string (truncated 256 chars), device fingerprint (SHA-256 hash) | Medium | PostgreSQL, JSONL files |
| **AI Interaction Data** | Chat prompts, AI responses, conversation memory embeddings | Low-Medium | PostgreSQL, Qdrant, Redis |
| **Usage/Analytics Data** | API usage counts, LLM token consumption, cost tracking (USD), latency, rate limit counters | Low | PostgreSQL, Redis |
| **Preference Data** | Language, UI theme, retention preferences, notification preferences | Low | PostgreSQL |
| **Gamification Data** | Level, XP, achievements, badges | Low | PostgreSQL |
| **Consent Records** | AI consent flag, external provider consent flag, consent version, timestamp | Medium (legally significant) | PostgreSQL |
| **Game Library Data** | Collection entries, wishlist, play history, ratings | Low | PostgreSQL |
| **User-Uploaded Documents** | PDF files (board game rulebooks) | Low | Filesystem / S3 |
| **Vector Embeddings** | Mathematical representations derived from documents and queries | Low (non-reversible) | Qdrant |

### 4.2 Categories of Data Subjects

| Category | Description | Estimated Volume |
|----------|-------------|------------------|
| **Registered Users** | Board game enthusiasts who create accounts | Moderate (hundreds to low thousands initially) |
| **Platform Administrators** | Internal users with elevated privileges | 1-3 individuals |
| **Publisher Users** (planned) | Game publishers submitting rulebooks | Not yet active |

### 4.3 Special Categories (Art. 9)

**No special categories of personal data are processed.** The application domain is board games -- a leisure activity. The AI chat functionality includes automated PII stripping to prevent inadvertent processing of sensitive data disclosed by users in free-text inputs.

---

## 5. Data Flows

### 5.1 Primary Data Flow: User Registration and Authentication

```
User Browser → HTTPS/TLS 1.2+ → Traefik (EU) → .NET API (EU)
     ↓
  Validation (FluentValidation)
     ↓
  Password hashing (PBKDF2-SHA256, 210k iterations)
     ↓
  PostgreSQL (EU) — stores account data
     ↓
  Redis (EU) — stores session token
     ↓
  JWT issued to browser (httpOnly, secure, SameSite)
```

No personal data leaves the EU during authentication.

### 5.2 AI Chat Processing Data Flow

```
User Query → .NET API (EU) → PII Detection & Stripping
                                       ↓
                          ┌────────────┴────────────┐
                          ↓                          ↓
                    Ollama (EU)              OpenRouter (USA)
                    ~80% of requests         ~20% of requests
                    No data transfer         PII-stripped prompts only
                          ↓                          ↓
                    AI Response ←──────────── AI Response
                          ↓
                    Log to PostgreSQL (pseudonymized after 7 days)
                          ↓
                    Return to user
```

### 5.3 PDF Document Processing Data Flow

```
PDF Upload → .NET API (EU) → Virus scan / validation
     ↓
  Storage (filesystem or S3 — EU)
     ↓
  SmolDocling/Unstructured Service (EU) → OCR + text extraction
     ↓
  Chunking (semantic splitting)
     ↓
  Embedding Service (EU) → sentence-transformers → vector generation
     ↓
  Qdrant (EU) — vector storage for RAG retrieval
```

PDF processing is entirely EU-based. No document content is sent to external providers.

### 5.4 BGG API Integration

```
Game search query (title only, no PII) → BoardGameGeek API (USA)
     ↓
  Game metadata response (title, description, player count, images)
     ↓
  PostgreSQL (EU) — cached game metadata
```

No personal data is transmitted to BGG. Queries contain only game titles.

---

## 6. Legal Basis and Purpose Limitation

### 6.1 Legal Bases by Processing Activity

| Processing Activity | Legal Basis | GDPR Article | Justification |
|---------------------|------------|--------------|---------------|
| User registration and account management | Performance of a contract | Art. 6(1)(b) | Necessary to provide the service the user has requested |
| Authentication and session management | Performance of a contract | Art. 6(1)(b) | Necessary to secure access to the user's account |
| Self-hosted AI processing (Ollama) | Legitimate interest | Art. 6(1)(f) | Core service functionality; no third-party transfer; user expectation when using an AI assistant |
| External AI processing (OpenRouter) | Explicit consent | Art. 6(1)(a) | Cross-border transfer to US-based processor; opt-in with granular consent tracking |
| Game library and collection management | Performance of a contract | Art. 6(1)(b) | Core platform functionality requested by users |
| PDF document upload and processing | Performance of a contract | Art. 6(1)(b) | User-initiated action to build knowledge base for AI Q&A |
| Usage analytics and monitoring | Legitimate interest | Art. 6(1)(f) | Operational necessity for service stability, cost management, abuse prevention |
| Audit logging | Legitimate interest | Art. 6(1)(f) | Security monitoring and accountability |
| Gamification | Performance of a contract | Art. 6(1)(b) | Platform feature enhancing user engagement |
| Consent record storage | Legal obligation | Art. 6(1)(c) | Art. 7(1) requires demonstrable proof of valid consent |
| Breach notification | Legal obligation | Art. 6(1)(c) | Art. 33 and 34 require breach documentation and notification |

### 6.2 Legitimate Interest Assessments

For processing activities relying on Art. 6(1)(f):

**Self-hosted AI processing (Ollama)**:
- **Interest**: Providing the core AI Q&A service that users expect when using an AI board game assistant
- **Necessity**: No less intrusive alternative delivers equivalent functionality
- **Balancing**: Processing involves low-sensitivity data (board game queries); all processing remains within EU; users can opt out entirely
- **Conclusion**: Legitimate interest is proportionate

**Usage analytics and monitoring**:
- **Interest**: Ensuring operational stability, managing API costs, detecting and preventing abuse
- **Necessity**: Without monitoring, the platform cannot maintain service quality or detect security incidents
- **Balancing**: Data is pseudonymized after 7 days; aggregated analytics contain no PII; retained only 30 days
- **Conclusion**: Legitimate interest is proportionate

### 6.3 Purpose Limitation

Personal data is processed exclusively for the purposes stated above. MeepleAI does not:

- Sell personal data to third parties
- Use personal data for marketing or advertising
- Profile users for behavioral targeting
- Use AI interaction data to train AI models
- Share data with partners beyond stated sub-processors

---

## 7. Assessment of Necessity and Proportionality

### 7.1 Necessity

| Test | Assessment |
|------|-----------|
| **Is processing necessary for the stated purpose?** | Yes. Each processing activity is directly linked to a platform feature that users explicitly request or is operationally required to deliver the service. |
| **Could the purpose be achieved with less personal data?** | Data minimization is already applied: only email and display name for identity; PII-stripped prompts for AI; pseudonymized identifiers in logs. No superfluous data collection exists. |
| **Could the purpose be achieved without processing personal data at all?** | No. User accounts require identity data; AI chat requires user queries; session tracking requires user identifiers. Anonymous usage is possible but provides degraded functionality. |

### 7.2 Proportionality

| Principle | Implementation |
|-----------|---------------|
| **Data minimization (Art. 5(1)(c))** | Only strictly necessary fields collected per activity. No optional PII fields. Chat prompts stripped of PII before external transfer. Logs pseudonymized after 7 days. |
| **Storage limitation (Art. 5(1)(e))** | Automated retention with documented periods: 30 days (logs), 90 days (conversation memory), account lifetime (profile data). Automated cleanup jobs enforce retention limits. |
| **Purpose limitation (Art. 5(1)(b))** | Each data category has a defined, documented purpose. No secondary processing or repurposing. |
| **Accuracy (Art. 5(1)(d))** | Users can update profile data at any time. Automated data lifecycle ensures stale data is purged. |
| **Integrity and confidentiality (Art. 5(1)(f))** | TLS 1.2+ for all communications, encrypted credentials, role-based access control, automated pseudonymization. |

### 7.3 Less Intrusive Alternatives Considered

| Alternative | Assessment | Decision |
|-------------|-----------|----------|
| Fully anonymous usage (no accounts) | Would prevent personalized game library, session tracking, and conversation memory. Significantly degrades user experience. | Rejected -- accounts necessary for core features |
| 100% self-hosted AI (no OpenRouter) | Would eliminate cross-border transfer entirely. However, self-hosted models cannot match the quality of larger commercial models for complex queries. | Partially adopted -- 80% self-hosted, 20% external with explicit consent |
| No AI features | Would eliminate all LLM-related processing. However, AI Q&A is the platform's primary value proposition. | Rejected -- but opt-out mechanism ensures users can disable AI entirely |
| Client-side only processing | Not technically feasible for RAG, which requires server-side vector search and LLM inference. | Rejected -- technical constraint |

---

## 8. Stakeholder Consultation

### 8.1 Data Subject Consultation

As MeepleAI is a pre-launch / early-stage platform, formal data subject consultation has not been conducted. The following mechanisms will capture ongoing data subject views:

- **Consent management UI**: Users actively choose which processing activities they permit
- **AI opt-out**: Users who do not wish to participate in AI processing can disable it without losing access to other features
- **Feedback channels**: In-app feedback and contact email for data protection inquiries
- **Privacy policy**: Published in clear, plain language with specific section on AI processing

### 8.2 DPO Consultation

No Data Protection Officer (DPO) has been appointed. Under Art. 37, a DPO is not required because:

- MeepleAI is not a public authority
- Core activities do not consist of processing requiring regular and systematic monitoring of data subjects on a large scale
- Core activities do not consist of processing special categories of data on a large scale

The controller will reassess the DPO requirement if the user base grows significantly or processing activities change materially.

---

## 9. Risk Assessment

### 9.1 Risk Methodology

Risks are assessed using the following matrix:

**Likelihood scale**:

| Level | Definition |
|-------|-----------|
| Very Low | Highly unlikely; would require extraordinary circumstances |
| Low | Unlikely but conceivable; would require specific conditions |
| Medium | Possible; could occur under foreseeable circumstances |
| High | Likely; expected to occur without additional controls |

**Severity scale**:

| Level | Definition |
|-------|-----------|
| Negligible | Inconvenience; no material impact on rights or freedoms |
| Low | Minor impact; temporary discomfort; easily remediated |
| Medium | Significant impact; distress or material disadvantage |
| High | Severe or irreversible impact; threat to fundamental rights |

**Risk level** = Likelihood x Severity, categorized as: NEGLIGIBLE, LOW, MEDIUM, HIGH, CRITICAL.

### 9.2 Risk Register

#### R01: Unauthorized Access to User Accounts

| Aspect | Assessment |
|--------|-----------|
| **Description** | An attacker gains access to user accounts through credential stuffing, brute force, session hijacking, or exploitation of authentication vulnerabilities |
| **Data at risk** | Email, display name, game library, conversation history |
| **Likelihood** | Low (rate limiting, strong password hashing, session management in place) |
| **Severity** | Medium (personal data exposure but no financial or health data) |
| **Risk Level** | **LOW** |
| **Mitigations** | PBKDF2-SHA256 (210k iterations), session token hashing, 2FA/TOTP support, rate limiting, session expiry (30 days), revoke-all-sessions endpoint |

#### R02: PII Leakage to External LLM Provider

| Aspect | Assessment |
|--------|-----------|
| **Description** | Personal information inadvertently included in AI prompts is transmitted to OpenRouter (USA) |
| **Data at risk** | Information users type into chat (potentially names, emails, personal details) |
| **Likelihood** | Low (automated PII stripping applied before transfer) |
| **Severity** | Medium (data transferred to third-country processor) |
| **Risk Level** | **LOW** |
| **Mitigations** | Regex-based PII detection/stripping (#5510), explicit consent for external processing (#5512), local-first routing (80% via Ollama), data minimization (max 10 messages context) |

#### R03: Cross-Border Data Exposure (EU to USA)

| Aspect | Assessment |
|--------|-----------|
| **Description** | Transfer of PII-stripped prompts to OpenRouter (USA) potentially subject to US surveillance laws (FISA 702) |
| **Data at risk** | AI prompts (PII-stripped), pseudonymized user identifiers |
| **Likelihood** | Low (data is low-sensitivity board game queries) |
| **Severity** | Medium (principle concern regarding adequacy of third-country protections) |
| **Risk Level** | **LOW** |
| **Mitigations** | SCCs (Art. 46(2)(c)), PII stripping, local-first routing, DPA with OpenRouter, Transfer Impact Assessment (#5508), EU-US Data Privacy Framework |

#### R04: Database Breach -- Personal Data Exfiltration

| Aspect | Assessment |
|--------|-----------|
| **Description** | Unauthorized access to PostgreSQL database exposing user records |
| **Data at risk** | Email addresses, display names, hashed passwords, OAuth tokens, game library data |
| **Likelihood** | Low (database not publicly exposed, Docker network isolation, secret management) |
| **Severity** | High (bulk PII exposure affecting potentially all users) |
| **Risk Level** | **MEDIUM** |
| **Mitigations** | Database not exposed on public ports, Docker network isolation, TLS for database connections (planned), LUKS volume encryption (planned), column-level encryption for high-sensitivity fields (planned), secret management via `.secret` files, regular backups |

#### R05: Failure to Fulfill Erasure Requests

| Aspect | Assessment |
|--------|-----------|
| **Description** | User exercises Art. 17 right to erasure but data persists in some storage locations |
| **Data at risk** | All personal data categories |
| **Likelihood** | Very Low (deletion command covers DB + Redis; automated cleanup for logs) |
| **Severity** | High (GDPR right violation) |
| **Risk Level** | **LOW** |
| **Mitigations** | `DeleteUserLlmDataCommand` covering 3-store deletion (#5509), `DELETE /api/v1/users/me` with cascading removal, consent records retained per legal obligation (Art. 7(1)), pseudonymized log entries auto-purged within 30 days |

#### R06: Insufficient Transparency

| Aspect | Assessment |
|--------|-----------|
| **Description** | Users not adequately informed about processing activities, especially AI and cross-border transfers |
| **Data at risk** | N/A (rights concern, not data exposure) |
| **Likelihood** | Low (privacy policy with AI section published; consent UI implemented) |
| **Severity** | Medium (Art. 13/14 violation) |
| **Risk Level** | **LOW** |
| **Mitigations** | Privacy policy AI section (#5514), granular consent management (#5512), opt-out mechanism (#5513), versioned consent tracking |

#### R07: Re-identification from AI Queries or Embeddings

| Aspect | Assessment |
|--------|-----------|
| **Description** | User re-identified from their board game queries, conversation patterns, or vector embeddings |
| **Data at risk** | Pseudonymized interaction data |
| **Likelihood** | Very Low (board game queries are generic; embeddings are non-reversible mathematical representations) |
| **Severity** | Low (limited personal information derivable from game queries) |
| **Risk Level** | **NEGLIGIBLE** |
| **Mitigations** | PII stripping, log pseudonymization after 7 days (#5511), 30-day/90-day retention limits, vector embeddings are not reversible to source text |

#### R08: Unauthorized Access to LLM Logs and Analytics

| Aspect | Assessment |
|--------|-----------|
| **Description** | Unauthorized person accesses LLM request logs, usage analytics, or conversation histories |
| **Data at risk** | User queries, pseudonymized user IDs, usage patterns |
| **Likelihood** | Low (admin-only access, role-based controls) |
| **Severity** | Medium (conversation content may contain user-disclosed information) |
| **Risk Level** | **LOW** |
| **Mitigations** | Role-based access control (admin-only), pseudonymization after 7 days, 30-day log retention, JSONL logs hashed before writing |

#### R09: PDF Document Data Exposure

| Aspect | Assessment |
|--------|-----------|
| **Description** | Uploaded PDF documents (board game rulebooks) are accessed by unauthorized parties |
| **Data at risk** | Document content (generally not personal data -- rulebooks are published materials) |
| **Likelihood** | Very Low (documents stored within EU infrastructure, access-controlled) |
| **Severity** | Low (rulebooks are typically publicly available content; user uploads their own copies) |
| **Risk Level** | **NEGLIGIBLE** |
| **Mitigations** | Path traversal protection, S3 server-side encryption (AES-256), LUKS volume encryption (planned), access control at API level |

#### R10: Session Hijacking or Token Theft

| Aspect | Assessment |
|--------|-----------|
| **Description** | Attacker obtains a valid session token and impersonates a user |
| **Data at risk** | Full account access (profile, game library, AI chat, settings) |
| **Likelihood** | Low (httpOnly/secure/SameSite cookies, session tokens hashed in storage) |
| **Severity** | Medium (account takeover but no financial data at risk) |
| **Risk Level** | **LOW** |
| **Mitigations** | Session tokens hashed with SHA-256 before storage, httpOnly/Secure/SameSite cookie attributes, 30-day session expiry, revoke-all-sessions endpoint, device fingerprinting |

#### R11: Vendor Lock-in / Service Discontinuation

| Aspect | Assessment |
|--------|-----------|
| **Description** | Key infrastructure provider (Hetzner, OpenRouter) discontinues service, affecting data availability or portability |
| **Data at risk** | All data stored on affected infrastructure |
| **Likelihood** | Very Low (Hetzner is an established EU provider; OpenRouter is supplementary) |
| **Severity** | Medium (service disruption; data portability challenges) |
| **Risk Level** | **LOW** |
| **Mitigations** | Regular encrypted backups, data export endpoint (`GET /api/v1/users/me/export`), standard technologies (PostgreSQL, Redis) with no vendor-specific lock-in, self-hosted AI covers 80% of inference |

#### R12: Data Retention Beyond Necessity

| Aspect | Assessment |
|--------|-----------|
| **Description** | Personal data retained longer than necessary for its purpose |
| **Data at risk** | All personal data categories with defined retention periods |
| **Likelihood** | Very Low (automated cleanup jobs enforce retention) |
| **Severity** | Low (storage limitation principle violation) |
| **Risk Level** | **NEGLIGIBLE** |
| **Mitigations** | Automated daily cleanup jobs: `LlmRequestLogCleanupJob` (30 days), `ConversationMemoryCleanupJob` (90 days), `SessionCleanupJob` (30 days), JSONL rotation (30 files), `AuditLogRetentionJob` |

#### R13: Inadequate Encryption at Rest

| Aspect | Assessment |
|--------|-----------|
| **Description** | Personal data stored unencrypted on disk, vulnerable to physical access or disk theft at data center |
| **Data at risk** | All data stored in PostgreSQL, Redis, and Qdrant |
| **Likelihood** | Very Low (Hetzner data centers are ISO 27001 and BSI C5 Type 2 certified; physical security is robust) |
| **Severity** | High (full database exposure if physical security is bypassed) |
| **Risk Level** | **LOW** |
| **Mitigations** | LUKS volume encryption (planned), Hetzner physical security controls, password hashing for credentials, AES encryption for OAuth tokens, planned column-level encryption for emails. See `encryption-at-rest-guide.md` for implementation roadmap. |

### 9.3 Risk Summary Matrix

| Risk Level | Count | Risks |
|------------|-------|-------|
| **CRITICAL** | 0 | -- |
| **HIGH** | 0 | -- |
| **MEDIUM** | 1 | R04 (Database breach) |
| **LOW** | 9 | R01, R02, R03, R05, R06, R08, R10, R11, R13 |
| **NEGLIGIBLE** | 3 | R07, R09, R12 |

**Overall Risk Level: LOW-MEDIUM**

The single MEDIUM risk (R04: Database breach) is addressed by planned encryption-at-rest measures. Once LUKS volume encryption and column-level encryption are implemented, this risk is expected to reduce to LOW.

---

## 10. Measures to Address Risks

### 10.1 Technical Measures

#### 10.1.1 Privacy by Design and by Default (Art. 25)

| Measure | Description | Status |
|---------|-------------|--------|
| PII detection and stripping | Automated regex-based detection of emails, phone numbers, and names in AI prompts before external transfer | Implemented (#5510) |
| Data minimization | Only necessary fields collected; AI prompts limited to 10 messages context | Implemented (by design) |
| Pseudonymization | UserIds in LLM logs replaced with SHA-256 hash after 7 days | Implemented (#5511) |
| Default privacy settings | AI features require explicit opt-in consent; external provider processing requires separate consent | Implemented (#5512) |
| Purpose-specific data models | CQRS pattern ensures commands and queries are scoped to specific operations with only necessary fields | Implemented (architecture) |

#### 10.1.2 Encryption and Data Security (Art. 32)

| Measure | Description | Status |
|---------|-------------|--------|
| TLS 1.2+ for all communications | All API communications encrypted in transit | Implemented |
| Password hashing | PBKDF2-SHA256 with 210,000 iterations | Implemented |
| Session token hashing | SHA-256 hashing before storage | Implemented |
| OAuth token encryption | AES encryption via ASP.NET Data Protection API | Implemented |
| TOTP secret encryption | Encrypted at rest in database | Implemented |
| S3 server-side encryption | AES-256 for uploaded documents | Implemented (when S3 provider used) |
| LUKS volume encryption | Full-disk encryption for all data volumes | Planned |
| PostgreSQL TLS | TLS 1.3 for database connections | Planned |
| Redis TLS | TLS for cache/session store connections | Planned |
| Qdrant TLS | TLS for vector database API | Planned |
| Column-level encryption | Application-layer AES for email, OAuth tokens | Planned |
| Backup encryption | GPG encryption for all backup files | Planned |

See `encryption-at-rest-guide.md` for detailed implementation guide and priority order.

#### 10.1.3 Access Control

| Measure | Description | Status |
|---------|-------------|--------|
| Role-based access control | Admin-only access to LLM logs, analytics, and user management | Implemented |
| JWT-based authentication | Stateless authentication with configurable expiry | Implemented |
| Session management | 30-day expiry, revoke-all-sessions capability | Implemented |
| Docker network isolation | Services accessible only via internal Docker network; databases not exposed on public ports | Implemented |
| API key authentication (Qdrant) | Vector database protected by API key | Implemented |
| Secret management | `.secret` files with gitignore; startup validation | Implemented |

#### 10.1.4 Data Lifecycle Management

| Measure | Description | Status |
|---------|-------------|--------|
| Automated log cleanup | `LlmRequestLogCleanupJob` -- 30-day retention, daily execution at 04:00 UTC | Implemented |
| Conversation memory cleanup | `ConversationMemoryCleanupJob` -- 90-day retention | Implemented |
| Session cleanup | `SessionCleanupJob` -- 30-day session expiry | Implemented |
| Log rotation | JSONL file sink with 30-file retention limit | Implemented |
| Audit log retention | `AuditLogRetentionJob` -- configurable retention | Implemented |
| Pseudonymization schedule | UserIds hashed after 7 days in LLM logs | Implemented (#5511) |

#### 10.1.5 AI-Specific Measures

| Measure | Description | Status |
|---------|-------------|--------|
| Local-first routing | 80% of LLM requests processed by self-hosted Ollama within EU | Implemented (#5087) |
| PII stripping before external transfer | Automated detection and removal of personal data from prompts | Implemented (#5510) |
| Consent-gated external processing | External AI providers only used when user has given explicit consent | Implemented (#5512) |
| Full AI opt-out | Users can disable all AI features entirely | Implemented (#5513) |
| HybridLLM smart routing | Automatic fallback to local models when external provider is unavailable | Implemented (#5087) |
| Circuit breaker | External provider failures trigger automatic switch to local models | Implemented |

### 10.2 Organizational Measures

| Measure | Description | Status |
|---------|-------------|--------|
| Data Processing Agreement (Hetzner) | DPA v1.2 with GDPR-compliant terms; Appendix 1 completed | Implemented |
| Data Processing Agreement (OpenRouter) | DPA with SCCs for EU-USA transfers | In progress (#5507) |
| Transfer Impact Assessment | TIA for EU-USA transfer via OpenRouter | Implemented (#5508) |
| Privacy Policy (AI section) | Clear, plain-language description of AI processing | Implemented (#5514) |
| Breach notification process | 72-hour notification protocol with Garante per la protezione dei dati personali | Documented |
| Record of processing activities | Art. 30 record for LLM subsystem | Implemented (#5515) |
| Development team training | GDPR awareness and secure coding practices | Ongoing |
| Annual DPIA review | Scheduled reassessment of risks and measures | Scheduled (2027-03-10) |
| Sub-processor monitoring | Regular review of OpenRouter terms and sub-processor list | Ongoing |

### 10.3 Measures Addressing the MEDIUM Risk (R04)

The database breach risk (R04) is the only MEDIUM-level risk. The following measures specifically target this risk:

| # | Measure | Priority | Status | Expected Completion |
|---|---------|----------|--------|---------------------|
| 1 | LUKS volume encryption for PostgreSQL, Redis, Qdrant | High | Planned | Q2 2026 |
| 2 | PostgreSQL TLS 1.3 connections | High | Planned | Q2 2026 |
| 3 | Redis TLS connections | High | Planned | Q2 2026 |
| 4 | Qdrant TLS for API | High | Planned | Q2 2026 |
| 5 | Column-level encryption (email, OAuth tokens) | Medium | Planned | Q3 2026 |
| 6 | Encrypted backups (GPG) | Medium | Planned | Q2 2026 |
| 7 | Docker network audit (exposed ports) | High | Partial | Q2 2026 |

Once items 1-4 are implemented, the R04 risk level is expected to reduce from MEDIUM to LOW.

---

## 11. Residual Risk Assessment

### 11.1 Post-Mitigation Risk Summary

| Risk | Pre-Mitigation Level | Post-Mitigation Level | Key Mitigation |
|------|---------------------|----------------------|----------------|
| R01: Unauthorized account access | LOW | LOW | Strong hashing, 2FA, rate limiting |
| R02: PII leakage to LLM provider | LOW | LOW | PII stripping, consent gating |
| R03: Cross-border data exposure | LOW | LOW | SCCs, PII stripping, local-first routing |
| R04: Database breach | MEDIUM | MEDIUM (LOW after encryption-at-rest implementation) | Network isolation, planned LUKS + TLS |
| R05: Erasure request failure | LOW | LOW | Multi-store deletion command |
| R06: Insufficient transparency | LOW | LOW | Privacy policy, consent UI |
| R07: Re-identification from queries | NEGLIGIBLE | NEGLIGIBLE | Pseudonymization, non-reversible embeddings |
| R08: Unauthorized log access | LOW | LOW | RBAC, pseudonymization, retention limits |
| R09: PDF document exposure | NEGLIGIBLE | NEGLIGIBLE | Access control, S3 encryption |
| R10: Session hijacking | LOW | LOW | Token hashing, secure cookies |
| R11: Vendor discontinuation | LOW | LOW | Regular backups, data export, standard tech |
| R12: Excessive data retention | NEGLIGIBLE | NEGLIGIBLE | Automated cleanup jobs |
| R13: Inadequate encryption at rest | LOW | LOW (NEGLIGIBLE after LUKS implementation) | Planned LUKS + column encryption |

### 11.2 Overall Residual Risk

**Overall residual risk: LOW**

No processing activity presents a HIGH or CRITICAL residual risk. The single MEDIUM risk (R04) has a documented implementation plan with target dates that will reduce it to LOW.

### 11.3 Art. 36 Consultation Assessment

Under Article 36(1), prior consultation with the supervisory authority is required only when the DPIA indicates that processing would result in a **high risk** in the absence of measures taken by the controller to mitigate the risk.

**Conclusion: Prior consultation with the Garante per la protezione dei dati personali is NOT required.** No high residual risks have been identified.

---

## 12. Data Subject Rights

### 12.1 Rights Implementation

| Right | GDPR Article | Implementation | Mechanism |
|-------|-------------|----------------|-----------|
| Right of access | Art. 15 | Self-service data export in machine-readable JSON format | `GET /api/v1/users/me/export` |
| Right to rectification | Art. 16 | Self-service profile editing for all user-modifiable fields | `PUT /api/v1/users/me` |
| Right to erasure | Art. 17 | Self-service account deletion with cascading data removal | `DELETE /api/v1/users/me` + `DeleteUserLlmDataCommand` |
| Right to restriction | Art. 18 | Account suspension by administrator upon verified request | Administrative action |
| Right to data portability | Art. 20 | JSON-format export of all user-provided personal data | `GET /api/v1/users/me/export` |
| Right to object | Art. 21 | AI opt-out disables all AI processing of user data | `PUT /api/v1/users/me/ai-consent` |
| Right not to be subject to automated decision-making | Art. 22 | Not applicable -- AI provides informational responses only; no automated decisions with legal or similarly significant effects | N/A |
| Right to withdraw consent | Art. 7(3) | Granular consent management for AI features and external providers | Settings > AI Consent (`/settings/ai-consent`) |

### 12.2 Response Timeframes

Data subject requests are processed within **one month** from receipt per Art. 12(3). Self-service mechanisms (access, rectification, erasure, portability, consent withdrawal) provide **immediate** response.

### 12.3 Erasure Scope

When a user exercises their right to erasure, the following data is deleted:

| Store | Data Deleted | Mechanism |
|-------|-------------|-----------|
| PostgreSQL | User account, profile, game library, play history, ratings, sessions, notifications, gamification data | Cascading `DELETE` |
| PostgreSQL | LLM request logs associated with user | `DeleteUserLlmDataCommand` |
| Redis | Session tokens, rate limit counters, cached data | `DeleteUserLlmDataCommand` |
| Qdrant | Conversation memory embeddings (if attributable) | `DeleteUserLlmDataCommand` |
| JSONL files | Log entries use pseudonymized IDs; auto-purged within 30 days | Automated rotation |

**Exceptions**: Consent records are retained per Art. 7(1) legal obligation to demonstrate valid consent.

---

## 13. International Transfers

### 13.1 Transfer Inventory

| Transfer | Exporter | Importer | Country | Legal Mechanism | Data Transferred |
|----------|---------|----------|---------|-----------------|------------------|
| LLM inference (~20% of requests) | MeepleAI (EU) | OpenRouter, Inc. (USA) | United States | SCCs (Art. 46(2)(c)) | PII-stripped AI prompts, pseudonymized user ID, conversation context (max 10 messages) |
| Game metadata lookup | MeepleAI (EU) | BoardGameGeek (USA) | United States | Not applicable | Game title search queries only -- no personal data transferred |

### 13.2 Transfer Impact Assessment Summary

A full Transfer Impact Assessment has been conducted for the OpenRouter transfer (see `transfer-impact-assessment.md`). Key findings:

- **Data sensitivity**: Low -- board game queries with PII removed
- **US legal framework**: FISA 702 applies to communications providers; OpenRouter processes API data, not communications. Executive Order 14086 provides proportionality safeguards.
- **Supplementary measures**: PII stripping, local-first routing (80% EU-only), pseudonymization, consent gating, data minimization
- **Residual risk**: LOW

### 13.3 Data Expressly Excluded from Transfer

The following data categories are **never** transmitted outside the EU:

- Email addresses and display names
- Password hashes and authentication credentials
- OAuth access/refresh tokens
- Session tokens
- IP addresses and device fingerprints
- Game library and collection data
- Payment or billing information (not collected)
- PDF documents and their raw text content

---

## 14. Sub-Processor Management

### 14.1 Current Sub-Processors

| Sub-Processor | Role | Location | DPA Status | Monitoring |
|---------------|------|----------|-----------|------------|
| **Hetzner Online GmbH** | Infrastructure processor (compute, storage, networking) | Germany/Finland (EU) | DPA v1.2 executed, Appendix 1 completed | ISO 27001 + BSI C5 Type 2 certified; breach notification per DPA Section 5.8 |
| **OpenRouter, Inc.** | External LLM API aggregator | United States | DPA in progress (#5507) with SCCs | Standard terms reviewed; sub-processor list available; data handling assessed |
| **Upstream LLM Providers** (via OpenRouter) | Model inference | Various | Managed by OpenRouter DPA | Indirect -- managed through OpenRouter contractual chain |

### 14.2 Sub-Processor Obligations

All sub-processors are contractually required to:

- Process data only on documented instructions from the controller
- Ensure persons authorized to process data have committed to confidentiality
- Implement appropriate technical and organizational security measures (Art. 32)
- Not engage additional sub-processors without prior authorization
- Assist with data subject rights fulfillment
- Delete or return all personal data at the end of the processing relationship
- Make available all information necessary to demonstrate compliance
- Notify the controller without undue delay of any personal data breach

### 14.3 Sub-Processor Changes

Any change to sub-processor arrangements triggers a review of this DPIA, including:

- Addition of new sub-processors
- Change in sub-processor data center locations
- Material changes to sub-processor terms of service
- Sub-processor security incidents

---

## 15. Data Retention Schedule

| Data Type | Retention Period | Cleanup Mechanism | Legal Basis for Period |
|-----------|-----------------|-------------------|----------------------|
| LLM request logs (identifiable) | 7 days | Pseudonymization job (SHA-256 hash of UserIds) | Data minimization (Art. 5(1)(c)) |
| LLM request logs (pseudonymized) | 30 days total | `LlmRequestLogCleanupJob` (daily at 04:00 UTC) | Storage limitation (Art. 5(1)(e)) |
| JSONL file logs | 30 days | Serilog rotating file sink (30-file limit) | Storage limitation |
| Conversation memories | 90 days | `ConversationMemoryCleanupJob` (daily) | Necessary for multi-turn conversation context |
| Session tokens | 30 days | `SessionCleanupJob` | Security -- limit window of session validity |
| Audit logs | Configurable | `AuditLogRetentionJob` | Accountability (Art. 5(2)) |
| User account data | Account lifetime | Deleted on user-initiated account deletion | Contract performance (Art. 6(1)(b)) |
| Consent records | Permanent | Not subject to automated deletion | Legal obligation (Art. 7(1)) |
| Aggregated analytics | 1 year | No PII; aggregated model usage statistics only | Legitimate interest (service improvement) |
| Game library data | Account lifetime (play history: user-configurable, default 90 days) | User-initiated deletion or automated retention | Contract performance |
| PDF documents | Until user deletes or account deletion | User-initiated or cascading on account deletion | Contract performance |
| Vector embeddings | Tied to source document lifecycle | Deleted when source document is deleted | Derived data -- follows source retention |
| Rate limit counters | Transient | Redis TTL (automatic expiry) | Legitimate interest (abuse prevention) |

---

## 16. Breach Notification

### 16.1 Process Summary

A detailed Breach Notification Process has been documented (see `breach-notification-process.md`). Key elements:

- **72-hour protocol**: Four-phase response (Containment, Investigation, Decision, Notification)
- **Supervisory authority**: Garante per la protezione dei dati personali (Italy)
- **Art. 33 notification**: Required unless breach is unlikely to result in risk to rights and freedoms
- **Art. 34 notification**: Required when breach is likely to result in high risk to data subjects
- **Breach register**: All breaches recorded per Art. 33(5) regardless of notification obligation
- **Post-incident review**: Required within 7 days of containment

### 16.2 Detection Mechanisms

- Hetzner contractual notification (DPA Section 5.8)
- Application monitoring (`OpenRouterRpmAlertBackgroundService`, `BudgetAlertBackgroundService`, circuit breaker events)
- Security audit findings
- User reports
- Third-party disclosure

### 16.3 Hetzner-Specific Procedures

Per the DPA, Hetzner Online GmbH is contractually required to notify MeepleAI immediately upon becoming aware of a breach. The 72-hour clock starts when MeepleAI becomes aware, not when Hetzner detected the incident. Hetzner holds ISO 27001 and BSI C5 Type 2 certifications.

---

## 17. Conclusion and Sign-Off

### 17.1 DPIA Outcome

Based on this comprehensive assessment:

1. **Full DPIA was not strictly required** based on the EDPB two-criteria threshold, but has been conducted as best practice given cross-border transfers and AI technology use.

2. **13 risks identified**: 0 Critical, 0 High, 1 Medium, 9 Low, 3 Negligible.

3. **The single MEDIUM risk** (R04: Database breach) has a documented mitigation plan (LUKS encryption, TLS for all services, column-level encryption) with target completion in Q2-Q3 2026.

4. **Overall residual risk is LOW**. No processing activity presents a high risk to the rights and freedoms of data subjects after mitigation measures are applied.

5. **Prior consultation with the supervisory authority is NOT required** (Art. 36) as no high residual risk has been identified.

6. **36 technical and organizational measures** are documented, of which 28 are implemented and 8 are planned with target dates.

### 17.2 Recommendations

| Priority | Recommendation | Target Date |
|----------|---------------|-------------|
| High | Complete LUKS volume encryption implementation | Q2 2026 |
| High | Enable TLS for PostgreSQL, Redis, and Qdrant internal connections | Q2 2026 |
| High | Execute formal DPA with OpenRouter including SCCs | Q2 2026 |
| Medium | Implement column-level encryption for email and OAuth tokens | Q3 2026 |
| Medium | Implement encrypted backup procedures (GPG) | Q2 2026 |
| Medium | Conduct Docker network audit for exposed ports | Q2 2026 |
| Low | Evaluate pg_tde for PostgreSQL transparent data encryption | Q3 2026 |
| Low | Reassess DPO appointment requirement if user base grows significantly | Annual review |

### 17.3 Sign-Off

| Role | Name | Date | Signature |
|------|------|------|-----------|
| Data Controller | Aaron Degrassi | 2026-03-10 | _________________ |

### 17.4 Conditions for Processing

Processing may proceed under the conditions that:

1. All implemented measures remain operational and are monitored
2. Planned measures (especially encryption-at-rest) are implemented according to the stated timeline
3. This DPIA is reviewed on the scheduled dates or when triggering events occur
4. Any material change to processing activities triggers a reassessment

---

## 18. Review Schedule and Version History

### 18.1 Review Triggers

This DPIA must be reviewed:

- **Annually** on the scheduled review date (next: 2027-03-10)
- **When processing scope changes** (new data categories, new features, expanded user base)
- **When new sub-processors are added** or existing sub-processor terms change materially
- **When new LLM providers are integrated** beyond Ollama and OpenRouter
- **After any personal data breach** as part of the post-incident review
- **When relevant legislation changes** (e.g., EU AI Act requirements become applicable)
- **When the user base grows significantly** (may require DPO appointment reassessment)

### 18.2 Version History

| Version | Date | Author | Description |
|---------|------|--------|-------------|
| 1.0 | 2026-03-10 | Aaron Degrassi | Initial comprehensive DPIA covering all processing activities. Incorporates and supersedes `dpia-llm.md` (simplified LLM assessment). |

---

## 19. References

### 19.1 Legal and Regulatory References

- Regulation (EU) 2016/679 (GDPR), particularly Articles 5, 6, 7, 9, 12-22, 25, 30, 32, 33, 34, 35, 36, 44-49
- EDPB Guidelines on Data Protection Impact Assessment (WP 248 rev.01)
- EDPB Recommendations 01/2020 on supplementary measures for international transfers
- EU-US Data Privacy Framework
- Italian DPA (Garante) published list of processing operations requiring DPIA

### 19.2 MeepleAI Compliance Documents

| Document | Location | Purpose |
|----------|----------|---------|
| DPIA -- LLM Processing (simplified) | `docs/compliance/dpia-llm.md` | Superseded by this document |
| Transfer Impact Assessment | `docs/compliance/transfer-impact-assessment.md` | EU-USA transfer via OpenRouter |
| Record of Processing Activities | `docs/compliance/record-of-processing-llm.md` | Art. 30 record for LLM subsystem |
| Privacy Policy -- AI Section | `docs/compliance/privacy-policy-ai-section.md` | User-facing AI processing disclosure |
| OpenRouter DPA Assessment | `docs/compliance/openrouter-dpa-assessment.md` | Sub-processor risk assessment |
| Encryption at Rest Guide | `docs/compliance/encryption-at-rest-guide.md` | Technical implementation guide for Art. 32 measures |
| Breach Notification Process | `docs/compliance/breach-notification-process.md` | Art. 33/34 notification protocol |
| Hetzner DPA Appendix 1 | `docs/compliance/hetzner-dpa-appendix-1-meepleai.md` | Processor agreement details |

### 19.3 External References

- Hetzner DPA: https://www.hetzner.com/legal/data-processing-agreement
- GDPR full text: https://gdpr-info.eu/
- EDPB DPIA guidelines: https://ec.europa.eu/newsroom/article29/items/611236
- Garante per la protezione dei dati personali: https://www.garanteprivacy.it/
- ASP.NET Core Data Protection: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/

---

*This Data Protection Impact Assessment is maintained pursuant to GDPR Article 35 and follows the EDPB Guidelines on DPIA (WP 248 rev.01). It is available for supervisory authority inspection upon request.*
