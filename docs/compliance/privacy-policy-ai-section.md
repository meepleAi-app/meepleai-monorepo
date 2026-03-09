# Privacy Policy — AI Features Section

**Issue**: #5514 | **Epic**: #5506 (GDPR Compliance for LLM Subsystem)
**Date**: 2026-03-09 | **Version**: 1.0

> This section should be integrated into the main Privacy Policy page at `/privacy`.

---

## How We Use Artificial Intelligence

### What AI Features We Offer

MeepleAI uses artificial intelligence to help you with board game rules, strategies, and gameplay questions. When you use our AI chat features, your questions are processed by language models (LLMs) to generate helpful responses.

### What Data We Process

When you interact with our AI features, we process:

- **Your questions and messages**: The text you type in chat conversations
- **Game context**: Rules and information about the board game you're asking about
- **Recent conversation history**: Up to 10 recent messages to maintain conversation context
- **Technical identifiers**: A pseudonymized user identifier for service operation

We do **not** send the following to AI providers:

- Your email address, name, or account details
- Your payment or billing information
- Your game library or collection data
- Your authentication credentials

### Which AI Providers We Use

| Provider | Location | Usage | Data Handling |
|----------|----------|-------|---------------|
| **Ollama** (self-hosted) | EU (our servers) | ~80% of requests | Fully under our control, no third-party transfer |
| **OpenRouter** | USA | ~20% of requests | Processed under Data Processing Agreement with Standard Contractual Clauses |

We prioritize processing your requests on our self-hosted EU infrastructure. External providers are used only when our local models cannot adequately answer your question.

### How We Protect Your Data

Before sending any data to external AI providers, we:

1. **Strip personally identifiable information** (PII) from your messages using automated detection
2. **Pseudonymize your user identifier** so it cannot be traced back to your account
3. **Encrypt all data in transit** using TLS 1.3
4. **Minimize data sent** to only what's necessary for generating a response

### Data Retention

| Data Type | Retention Period | Purpose |
|-----------|-----------------|---------|
| AI request logs | 30 days | Service monitoring and quality assurance |
| Conversation memories | 90 days | Maintaining conversation context |
| Aggregated usage statistics | 1 year | Service improvement (no personal data) |

After the retention period, data is automatically deleted by our systems.

### Your Rights

#### Consent Management

You can manage your AI consent preferences at any time in **Settings > AI Consent** (`/settings/ai-consent`). You have the option to:

- **Consent to AI processing**: Enable AI-powered features
- **Consent to external providers**: Allow processing by external AI providers (OpenRouter)
- **Withdraw consent**: Disable AI features at any time

#### Opt-Out

You can completely disable AI features in **Settings > AI Consent**. When AI is disabled:

- No data is sent to any AI provider
- Chat features are not available
- Your existing AI data is not affected (you can request deletion separately)

#### Right to Erasure (Art. 17)

You can request deletion of all your AI-related data:

- **Self-service**: Visit **Settings > Usage** to delete your AI data
- **Contact us**: Email our data protection contact for assisted deletion

When you request erasure, we delete:

- All AI request logs associated with your account
- All conversation memories
- All cached session data

File-based logs use pseudonymized identifiers and are automatically purged within 30 days.

#### Right to Access (Art. 15)

You can request a copy of your AI-related data by contacting our data protection team.

### Legal Basis

| Processing | Legal Basis | Reference |
|------------|-------------|-----------|
| Self-hosted AI (Ollama) | Legitimate interest (Art. 6(1)(f)) | Necessary for service functionality |
| External AI (OpenRouter) | Consent (Art. 6(1)(a)) | Explicit consent required and tracked |
| Usage analytics | Legitimate interest (Art. 6(1)(f)) | Pseudonymized, operational necessity |

### International Transfers

When your data is processed by OpenRouter (USA), it is protected by:

- Standard Contractual Clauses (Art. 46(2)(c))
- Technical supplementary measures (PII stripping, encryption)
- Data Processing Agreement with GDPR-compliant terms

For more details, see our Transfer Impact Assessment.

### Changes to This Section

We version our AI privacy terms and track your consent against specific versions. If we make material changes to how we process AI data, we will notify you and request updated consent where required.

**Current version**: 1.0 (March 2026)

### Contact

For questions about our AI data processing, contact our data protection team at [contact information].

---

*This section is part of the MeepleAI Privacy Policy. Last updated: March 2026.*
