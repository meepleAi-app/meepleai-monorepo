# OpenRouter DPA Assessment

**Issue**: #5507 | **Epic**: #5506 (GDPR Compliance for LLM Subsystem)
**Date**: 2026-03-09 | **Status**: Assessment Complete

## 1. DPA Status

### OpenRouter Terms of Service

OpenRouter acts as an **API aggregator** routing requests to upstream LLM providers (Anthropic, OpenAI, Google, Meta, etc.). As of March 2026:

- **OpenRouter Privacy Policy**: States they process API requests on behalf of customers
- **Data Processing**: OpenRouter processes prompt data transiently; they do not retain prompts for training
- **Standard terms**: OpenRouter provides standard Terms of Service covering data handling

### DPA Availability

| Aspect | Status |
|--------|--------|
| Standard DPA template | Available via enterprise contact |
| GDPR-specific clauses | Included in DPA |
| Standard Contractual Clauses (SCCs) | Referenced in DPA for EU→USA transfers |
| Sub-processor list | Available (upstream LLM providers) |
| Data breach notification | 72-hour commitment in DPA |

## 2. Risk Assessment

### Data Flow Analysis

```
User (EU) → MeepleAI API (EU) → PII Stripping → OpenRouter (USA) → LLM Provider
                                      ↓
                              Ollama (self-hosted, EU) — 80% of requests
```

### Risk Factors

| Factor | Risk Level | Mitigation |
|--------|-----------|------------|
| Data transfer to USA | Medium | SCCs + supplementary measures |
| PII in prompts | Low | PII detection/stripping (Issue #5510) |
| Data retention at provider | Low | OpenRouter: transient processing only |
| Sub-processor chain | Medium | OpenRouter manages sub-processors |
| Local-first routing | Low | 80% requests stay in EU via Ollama |

### Overall Risk: **LOW-MEDIUM**

## 3. Mitigation Measures

### Technical Safeguards (Implemented)

1. **PII Stripping** (#5510): Detects and redacts PII before sending to OpenRouter
2. **Local-First Routing** (#5087): 80% of requests routed to self-hosted Ollama (EU)
3. **Log Pseudonymization** (#5511): UserIds hashed after 7 days
4. **JSONL Log Hashing**: UserIds SHA-256 hashed before writing to file logs
5. **Right to Erasure** (#5509): Users can delete all LLM-related data
6. **Consent Management** (#5512): Explicit AI consent tracking
7. **Opt-Out Mechanism** (#5513): Users can disable AI features entirely

### Contractual Safeguards

1. OpenRouter DPA with GDPR clauses
2. Standard Contractual Clauses (SCCs) for EU→USA transfers
3. Data breach notification within 72 hours
4. Sub-processor transparency

## 4. Recommendation

**Proceed with OpenRouter** as the external LLM provider with the following conditions:

1. Execute DPA with OpenRouter (enterprise tier)
2. Verify SCCs are in place for EU→USA transfers
3. Maintain PII stripping as mandatory pre-processing step
4. Continue local-first routing strategy (80%+ via Ollama)
5. Review DPA annually or when OpenRouter changes terms

## 5. Action Items

- [x] Assess OpenRouter DPA availability
- [x] Document risk assessment
- [x] Identify mitigation measures
- [ ] Execute formal DPA (requires legal team + OpenRouter enterprise contact)
- [ ] Annual review scheduled

---

*This assessment is for internal compliance purposes. Formal legal review recommended before DPA execution.*
