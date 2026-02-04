# RAG Tier-Strategy Configuration Guide

**Admin guide for configuring tier-strategy access and model mappings**

Last updated: 2026-02-04

---

## Overview

The MeepleAI RAG system uses a three-layer architecture for model selection:

```
User Tier → Available Strategies → Strategy → Model Selection
```

**Key Principle**: User tier controls which strategies are available, but does NOT directly select models. Strategy selection determines which LLM model is used.

---

## Accessing the Configuration UI

1. Log in as an Admin user
2. Navigate to **Admin** → **RAG** → **Tier Strategy Config**
3. URL: `/admin/rag/tier-strategy-config`

---

## Tier-Strategy Access Matrix

### Understanding the Matrix

The matrix shows which strategies are available to each user tier:

| Tier | FAST | BALANCED | PRECISE | EXPERT | CONSENSUS | CUSTOM |
|------|:----:|:--------:|:-------:|:------:|:---------:|:------:|
| **Anonymous** | ❌ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **User** | ✅ | ✅ | ❌ | ❌ | ❌ | ❌ |
| **Editor** | ✅ | ✅ | ✅ | ❌ | ❌ | ❌ |
| **Admin** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |

### Enabling/Disabling Strategies

1. In the **Tier-Strategy Access Matrix** section, toggle the switch for any tier/strategy combination
2. Changes are saved immediately (optimistic update with server validation)
3. A "default" badge indicates system defaults; modified entries show as "custom"

### Access Control Rules

- **Anonymous**: No access to RAG system (authentication required)
- Changes to Anonymous tier are disabled (system restriction)
- CUSTOM strategy is always Admin-only
- At least one strategy must remain enabled per tier (except Anonymous)

---

## Strategy-Model Mapping

### Understanding Mappings

Each strategy maps to a primary LLM model with optional fallbacks:

| Strategy | Primary Model | Provider | Fallback | Typical Cost |
|----------|--------------|----------|----------|--------------|
| **FAST** | Llama 3.3 70B | OpenRouter | GPT-4o-mini | $0 (free) |
| **BALANCED** | DeepSeek Chat | DeepSeek | Claude Haiku | ~$0.01/query |
| **PRECISE** | Claude Sonnet 4.5 | Anthropic | Haiku, GPT-4o-mini | ~$0.13/query |
| **EXPERT** | Claude Sonnet 4.5 | Anthropic | GPT-4o | ~$0.10/query |
| **CONSENSUS** | Multi-model | Mixed | N/A | ~$0.09/query |
| **CUSTOM** | Admin configured | Varies | Admin configured | Variable |

### Editing Model Mappings

1. In the **Strategy-Model Mappings** section, click **Edit** for the strategy to modify
2. Update the following fields:
   - **Provider**: Select from OpenRouter, Anthropic, DeepSeek, Ollama, Mixed
   - **Primary Model**: Enter the model identifier (e.g., `anthropic/claude-sonnet-4.5`)
   - **Fallback Models**: Comma-separated list of fallback models
3. Click **Save** to apply changes

### Model Identifier Format

Use the provider-specific model identifier format:
- OpenRouter: `openai/gpt-4o-mini`, `meta-llama/llama-3.3-70b-instruct:free`
- Anthropic: `anthropic/claude-sonnet-4.5`, `anthropic/claude-haiku-4.5`
- DeepSeek: `deepseek-chat`, `deepseek-reasoner`
- Ollama: `llama3:8b`, `mistral:7b`

---

## Best Practices

### Tier Configuration

1. **Start with defaults**: The default configuration is optimized for cost-effectiveness
2. **Enable progressively**: Consider enabling PRECISE for Editors before enabling EXPERT
3. **Monitor usage**: Track which strategies are actually used per tier
4. **Cost awareness**: PRECISE and EXPERT strategies use premium models

### Model Configuration

1. **Test fallbacks**: Ensure fallback models are available and working
2. **Consider latency**: Free models may have higher latency under load
3. **Balance cost vs quality**: BALANCED strategy offers good cost-quality ratio
4. **CUSTOM strategy**: Reserve for special use cases (e.g., testing new models)

### Security Considerations

1. **CONSENSUS strategy**: Multiple models increase cost significantly
2. **CUSTOM strategy**: Should only be used by trusted admins
3. **Audit changes**: All configuration changes are logged

---

## Resetting to Defaults

To reset all tier-strategy configuration to system defaults:

1. Navigate to the configuration page
2. Use the **Reset to Defaults** button (if available)
3. Confirm the action

**Warning**: This will remove all custom configurations and restore system defaults.

---

## Troubleshooting

### Strategy Not Available to Users

1. Check the Tier-Strategy Access Matrix
2. Verify the user's tier assignment
3. Ensure the strategy is enabled for that tier

### Model Not Working

1. Check the Strategy-Model Mapping configuration
2. Verify the model identifier is correct
3. Check provider availability and API keys
4. Review fallback model configuration

### Performance Issues

1. Consider using FAST strategy for simple queries
2. Check if PRECISE strategy is overused
3. Monitor model latency and availability

---

## API Reference

### Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/v1/admin/tier-strategy/matrix` | Get access matrix |
| GET | `/api/v1/admin/tier-strategy/model-mappings` | Get model mappings |
| PUT | `/api/v1/admin/tier-strategy/access` | Update tier-strategy access |
| PUT | `/api/v1/admin/tier-strategy/model-mapping` | Update strategy-model mapping |
| POST | `/api/v1/admin/tier-strategy/reset` | Reset to defaults |

### Authentication

All endpoints require Admin role authentication.

---

## Related Documentation

- [RAG System Overview](../03-api/rag/HOW-IT-WORKS.md)
- [Layer 1: Routing](../03-api/rag/02-layer1-routing.md)
- [RAG Flow Diagram](../03-api/rag/diagrams/rag-flow-current.md)
- [Migration Guide](../02-development/migrations/tier-strategy-refactor.md)
