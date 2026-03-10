# Transfer Impact Assessment: EU → USA Data Transfer via OpenRouter

**Issue**: #5508 | **Epic**: #5506 (GDPR Compliance for LLM Subsystem)
**Date**: 2026-03-09 | **Legal Basis**: GDPR Art. 46(2)(c) — Standard Contractual Clauses

## 1. Transfer Overview

| Aspect | Details |
|--------|---------|
| **Data Exporter** | MeepleAI (EU-based application) |
| **Data Importer** | OpenRouter, Inc. (USA) |
| **Transfer Mechanism** | Standard Contractual Clauses (SCCs) |
| **Purpose** | AI-powered board game question answering |
| **Frequency** | ~20% of LLM requests (80% routed locally via Ollama) |

## 2. Nature of Data Transferred

### Data Categories

| Category | Contains PII? | Mitigation |
|----------|--------------|------------|
| User queries (prompts) | Potentially | PII stripping before transfer (#5510) |
| Game rules context | No | Static reference data |
| Chat history (recent) | Potentially | Limited to 10 recent messages |
| Model selection metadata | No | Technical routing data |

### Data NOT Transferred

- User account information (email, name, password)
- Payment/billing data
- Session tokens or authentication credentials
- User profile data
- Game library/collection data

## 3. Legal Framework Analysis — Destination Country (USA)

### Post-Schrems II Assessment

| Factor | Assessment |
|--------|-----------|
| **FISA Section 702** | Applies to communications service providers; OpenRouter processes API data, not communications |
| **Executive Order 14086** | Establishes proportionality and necessity requirements for US intelligence access |
| **EU-US Data Privacy Framework** | Provides adequacy mechanism for certified organizations |
| **Risk to data subjects** | Low — data is board game queries, not sensitive personal data |

### Risk Level: **LOW**

Board game queries and AI-assisted rules explanations do not constitute sensitive data categories under GDPR Art. 9. The risk of US government surveillance interest in board game discussions is negligible.

## 4. Supplementary Measures

### Technical Measures (Implemented)

| Measure | Description | Issue |
|---------|-------------|-------|
| **PII Detection & Stripping** | Regex-based detection of emails, phones, names before transfer | #5510 |
| **Local-First Routing** | 80% of requests processed by self-hosted Ollama (EU, no transfer) | #5087 |
| **Log Pseudonymization** | UserIds replaced with SHA-256 hash after 7 days | #5511 |
| **JSONL Log Hashing** | UserIds hashed before writing to any log file | Existing |
| **Data Minimization** | Only necessary context sent (no full user profile) | By design |
| **TLS Encryption** | All API calls to OpenRouter over HTTPS/TLS 1.3 | Infrastructure |
| **Right to Erasure** | Users can delete all LLM-related data | #5509 |
| **Consent Tracking** | Explicit consent for external AI processing | #5512 |
| **Opt-Out** | Users can disable all AI features | #5513 |

### Organizational Measures

| Measure | Description |
|---------|-------------|
| **DPA with OpenRouter** | Data Processing Agreement with GDPR clauses (#5507) |
| **Sub-processor monitoring** | OpenRouter maintains sub-processor list |
| **Incident response** | 72-hour breach notification commitment |
| **Staff training** | Development team trained on GDPR requirements |
| **Regular review** | Annual TIA review cycle |

## 5. Risk Assessment Matrix

| Risk | Likelihood | Impact | Mitigation | Residual Risk |
|------|-----------|--------|------------|---------------|
| US government access to data | Very Low | Low | Non-sensitive data, PII stripped | Negligible |
| Data breach at OpenRouter | Low | Medium | PII stripping, pseudonymization | Low |
| Unauthorized sub-processing | Low | Medium | DPA, sub-processor transparency | Low |
| Data retention beyond purpose | Low | Low | Transient processing, 30-day retention | Low |
| Re-identification from queries | Very Low | Low | PII stripping, data minimization | Negligible |

### Overall Residual Risk: **LOW**

## 6. Conclusion

The transfer of limited, PII-stripped board game query data to OpenRouter (USA) presents **low residual risk** when combined with:

1. Standard Contractual Clauses
2. Technical supplementary measures (PII stripping, local-first routing, pseudonymization)
3. Low sensitivity of transferred data (board game queries)
4. EU-US Data Privacy Framework provisions

**Recommendation**: Transfer is permissible under GDPR Art. 46(2)(c) with SCCs and supplementary measures in place.

## 7. Review Schedule

| Review Type | Frequency | Next Review |
|-------------|-----------|-------------|
| Full TIA reassessment | Annual | March 2027 |
| Supplementary measures review | Semi-annual | September 2026 |
| Legal framework monitoring | Ongoing | Continuous |
| Incident-triggered review | As needed | N/A |

---

*This assessment follows the EDPB Recommendations 01/2020 on supplementary measures for international transfers.*
