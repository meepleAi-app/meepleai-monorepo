# Hetzner DPA -- Appendix 1: Description of Data Processing

**Document**: Appendix 1 to the Data Processing Agreement between MeepleAI (Controller) and Hetzner Online GmbH (Processor)

**Reference**: Hetzner Online GmbH DPA v1.2 (February 16, 2026)

**Effective Date**: March 10, 2026

**Controller**: Aaron Degrassi, operating as MeepleAI

**Processor**: Hetzner Online GmbH, Industriestr. 25, 91710 Gunzenhausen, Germany

---

## 1. Scope, Type, and Purpose of Data Processing

MeepleAI is an AI-powered board game assistant offered as a Software-as-a-Service (SaaS) application hosted on Hetzner Cloud infrastructure located in Germany and/or Finland (European Union).

The Processor provides cloud infrastructure services (compute, storage, networking) on which the Controller operates the MeepleAI application. The Processor does not access, inspect, or otherwise process personal data stored within the Controller's infrastructure except as strictly necessary to provide the contracted hosting services or as required by applicable law.

The data processing carried out on the Processor's infrastructure encompasses the following operations:

1. **User Account Management** -- Registration, authentication (including multi-factor authentication), session management, profile management, and account lifecycle operations (creation, modification, deletion).

2. **AI Chat Processing** -- Retrieval-Augmented Generation (RAG) queries, multi-agent AI inference, vector similarity search, conversation memory storage, and AI response generation. Approximately 80% of AI inference is processed locally via self-hosted models (Ollama); the remaining approximately 20% is routed to an external sub-processor (OpenRouter Inc.) with PII stripped prior to transmission.

3. **PDF Document Processing** -- Upload, optical character recognition (OCR), text extraction, semantic chunking, and vector embedding generation for board game rulebooks and related documentation.

4. **Game Library Management** -- Storage and retrieval of user game collections, wishlists, play history records, game ratings, and session tracking data.

5. **Community Shared Game Catalog** -- Maintenance of a community-contributed game database with administrative approval workflows, soft-delete mechanisms, and content moderation capabilities.

6. **System Administration and Monitoring** -- Logging, audit trail generation, usage analytics, cost tracking, rate limiting, and background job execution for automated data lifecycle management.

---

## 2. Types of Personal Data Processed

The following table enumerates the categories of personal data processed on the Processor's infrastructure, the specific data fields within each category, the applicable legal basis under Regulation (EU) 2016/679 (GDPR), and the corresponding retention period.

| Category | Data Fields | Legal Basis | Retention |
|---|---|---|---|
| Identity Data | Email address, display name | Art. 6(1)(b) GDPR -- performance of a contract | Account lifetime; deleted upon account erasure |
| Authentication Data | Password hash (PBKDF2-SHA256, 210,000 iterations), session token hashes (SHA-256), OAuth access and refresh tokens (AES encrypted via ASP.NET Data Protection API), TOTP secrets (encrypted at rest), backup codes (SHA-256 hashed) | Art. 6(1)(b) GDPR -- performance of a contract | Session tokens: 30 days (automated cleanup); OAuth tokens: until revocation or refresh cycle; Account credentials: account lifetime |
| Technical and Log Data | IP address, user agent string (truncated to 256 characters), device fingerprint (SHA-256 hash derived from user agent string) | Art. 6(1)(f) GDPR -- legitimate interest (security monitoring, fraud prevention, abuse detection) | 30 days (automated cleanup) |
| AI Interaction Data | Chat prompts (PII automatically stripped via automated detection before any external processing), AI-generated responses, conversation memory embeddings (vector representations) | Art. 6(1)(a) GDPR -- explicit consent (opt-in for AI features) combined with Art. 6(1)(f) GDPR -- legitimate interest (service delivery) | Request logs: 30 days (automated daily deletion); Conversation memory: 90 days (automated daily deletion); Pseudonymization applied after 7 days |
| Usage and Analytics Data | API key usage counts, LLM token consumption metrics, cost tracking (USD), request latency measurements, rate limit counters | Art. 6(1)(f) GDPR -- legitimate interest (service improvement, capacity planning, cost management) | 30 days (automated daily deletion) |
| Preference Data | Language preference, UI theme selection, data retention preference (configurable in days), notification preferences | Art. 6(1)(b) GDPR -- performance of a contract | Account lifetime; deleted upon account erasure |
| Gamification Data | User level, experience points, achievement records, badge awards | Art. 6(1)(b) GDPR -- performance of a contract | Account lifetime; deleted upon account erasure |
| Consent Records | AI processing consent flag, external provider consent flag, consent version identifier, consent timestamp (UTC) | Art. 6(1)(c) GDPR -- compliance with a legal obligation (Art. 7(1) GDPR requirement to demonstrate valid consent) | Permanent retention (legally required to demonstrate proof of consent; survives account deletion) |
| Game Library Data | Game collection entries, wishlist entries, play history records, game ratings and reviews | Art. 6(1)(b) GDPR -- performance of a contract | User-configurable retention period (default: 90 days for play history); collection and wishlist data retained for account lifetime |

---

## 3. Categories of Data Subjects

The following categories of natural persons are affected by the data processing described in this Appendix:

- **Registered Users**: Natural persons who create a MeepleAI account to access the AI board game assistant service, including all features related to game library management, AI-assisted chat, document processing, and community participation.

- **Platform Administrators**: A limited number of internal administrators with elevated system access privileges for the purposes of content management, user support, system configuration, and monitoring.

- **Publisher Users** (planned, not yet active): Game publishers who will submit rulebook content for AI-assisted analysis. This category of data subjects is not yet active as of the effective date of this Appendix and will be addressed in an amendment upon activation.

---

## 4. Special Categories of Data (Art. 9 GDPR)

No special categories of personal data within the meaning of Art. 9(1) GDPR are processed. The MeepleAI application does not collect, store, or otherwise process data revealing:

- Racial or ethnic origin
- Political opinions
- Religious or philosophical beliefs
- Trade union membership
- Genetic data
- Biometric data for the purpose of uniquely identifying a natural person
- Data concerning health
- Data concerning a natural person's sex life or sexual orientation

The AI chat functionality includes automated PII stripping to prevent inadvertent processing of sensitive data disclosed by users in free-text inputs. Any such data detected is removed before processing and is not stored.

---

## 5. Data Transfers to Sub-Processors Outside Hetzner Infrastructure

The Controller engages the following sub-processors that receive data originating from the Processor's infrastructure. These transfers are initiated and controlled exclusively by the Controller's application logic; the Processor (Hetzner) has no role in or visibility into these transfers.

| Sub-Processor | Country | Data Transferred | Safeguard | Purpose |
|---|---|---|---|---|
| OpenRouter Inc. | United States | AI prompts with PII stripped via automated detection; conversation context limited to a maximum of 10 messages per request; pseudonymized user identifier (first 16 hexadecimal characters of SHA-256 hash of internal user ID) | Standard Contractual Clauses pursuant to Art. 46(2)(c) GDPR; explicit user consent obtained via opt-in mechanism prior to any data transfer | AI model inference for approximately 20% of requests; the remaining 80% are processed locally via self-hosted Ollama instances on the Processor's infrastructure |
| BoardGameGeek (public API) | United States | Game title search queries only; no personal data is included in any request | Not applicable -- no personal data transferred | Enrichment of game metadata (titles, descriptions, player counts, images) from public catalog |

**Data expressly excluded from external transfer**: The following data categories are never transmitted to any external sub-processor: email addresses, display names, password hashes or credentials, authentication tokens, payment information, game library data, session tokens, IP addresses, or device fingerprints.

---

## 6. Technical Architecture on Hetzner Infrastructure

The following describes the technical environment operated by the Controller on the Processor's infrastructure:

| Component | Specification |
|---|---|
| **Hetzner Product** | Hetzner Cloud Server (CX-series) |
| **Data Center Location** | Germany and/or Finland (European Union) |
| **Operating System** | Linux (managed exclusively by the Controller) |
| **Application Stack** | .NET 9 API server, Next.js 16 frontend application, Python microservices (embedding, reranking, document processing) |
| **Primary Database** | PostgreSQL 16 (relational data, user accounts, game catalog) |
| **Cache and Session Store** | Redis 7 (session management, rate limiting, transient caching) |
| **Vector Database** | Qdrant (AI embedding storage for RAG retrieval) |
| **Reverse Proxy** | Traefik with TLS 1.2+ termination |
| **Containerization** | Docker Compose (all services containerized) |
| **Encryption in Transit** | TLS 1.2 or higher enforced for all external and internal communications |
| **Encryption at Rest** | Controller's responsibility per DPA Appendix 2 Section 7; implemented via operating system and application-level encryption mechanisms |

The Processor provides the underlying compute, storage, and network infrastructure. All application-level security measures, access controls, and data protection mechanisms are implemented and maintained by the Controller.

---

## 7. Data Retention and Automated Deletion Schedule

The Controller implements automated data lifecycle management through scheduled background jobs. The following table summarizes retention periods and the corresponding deletion mechanisms:

| Data Type | Retention Period | Cleanup Mechanism |
|---|---|---|
| LLM Request Logs | 30 days | Automated daily job (`LlmRequestLogCleanupJob`, scheduled execution at 04:00 UTC) |
| Conversation Memory | 90 days | Automated daily job (`ConversationMemoryCleanupJob`) |
| Session Tokens | 30 days | Automated cleanup job (`SessionCleanupJob`) |
| Application File Logs (JSONL) | 30 days | Serilog rotating file sink with 30-file retention limit |
| Audit Logs | Configurable by administrator | Automated retention job (`AuditLogRetentionJob`) |
| User Account Data | Until account deletion by data subject or administrator | Self-service deletion (Art. 17 GDPR) or administrative deletion upon verified request |
| Consent Records | Permanent | Retained indefinitely as required by Art. 7(1) GDPR to demonstrate valid consent; not subject to automated deletion |
| AI Interaction Data (pseudonymization) | 7 days until pseudonymization | Automated pseudonymization job strips direct identifiers after 7 days; full deletion at 30 days (logs) or 90 days (memory) |

All automated deletion jobs execute irreversible hard deletes from the database. Soft-deleted records (where applicable) are permanently purged according to the schedules above.

---

## 8. Data Subject Rights Implementation

The Controller provides the following mechanisms for data subjects to exercise their rights under Chapter III of the GDPR. These mechanisms operate within the Controller's application on the Processor's infrastructure:

| Right | GDPR Article | Implementation | Technical Endpoint |
|---|---|---|---|
| Right of Access | Art. 15 | Self-service data export providing a complete copy of all personal data in machine-readable format | `GET /api/v1/users/me/export` |
| Right to Rectification | Art. 16 | Self-service profile editing for all user-modifiable personal data fields | `PUT /api/v1/users/me` |
| Right to Erasure | Art. 17 | Self-service account deletion with cascading removal of all associated personal data (except consent records retained per legal obligation) | `DELETE /api/v1/users/me` |
| Right to Restriction of Processing | Art. 18 | Account suspension by platform administrator upon verified request | Administrative action via admin panel |
| Right to Data Portability | Art. 20 | JSON-format data export containing all personal data provided by the data subject | `GET /api/v1/users/me/export` |
| Right to Object | Art. 21 | Opt-out mechanism for AI-powered features; disabling AI consent prevents all AI processing of user data | `PUT /api/v1/users/me/ai-consent` |

Data subject requests are processed within the statutory period of one month from receipt (Art. 12(3) GDPR). The Controller does not require the Processor's involvement to fulfill data subject requests, as all personal data is accessible and manageable through the Controller's application layer.

---

## 9. Contact Information

**Controller (Data Protection Inquiries)**:

Aaron Degrassi
Email: privacy@meepleai.com

**Processor**:

Hetzner Online GmbH
Industriestr. 25
91710 Gunzenhausen, Germany
Data Protection Officer: datenschutz@hetzner.com

---

*This Appendix 1 forms an integral part of the Data Processing Agreement between the Controller and the Processor. It shall be reviewed and updated as necessary to reflect changes in processing activities, sub-processor arrangements, or applicable data protection requirements.*

*Document prepared by: Aaron Degrassi (Controller)*
*Date: March 10, 2026*
*For use with: Hetzner Online GmbH DPA v1.2 (February 16, 2026)*
