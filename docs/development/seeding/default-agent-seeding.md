# Default Agent Seeding

**POC Multi-Purpose Board Game Assistant**

## Overview

The `DefaultAgentSeeder` creates a baseline AI agent for testing and as foundation for RAG integration. This agent provides professional board game consultation across rules, strategies, recommendations, and comparisons.

## Agent Specifications

### Identity
- **Name**: MeepleAssistant POC
- **Type**: RAG (Retrieval-Augmented Generation)
- **Strategy**: SingleModel (baseline, cost-optimized)
- **Status**: Active by default

### LLM Configuration
- **Provider**: OpenRouter
- **Model**: `anthropic/claude-3-haiku`
- **Cost**: ~$0.00025 per 1K tokens (quasi-free)
- **Temperature**: 0.3 (professional, consistent)
- **MaxTokens**: 2048 (standard conversations)
- **Mode**: Chat (Q&A)

### Capabilities

**Multi-Purpose Coverage**:
- Rule clarifications and interpretation
- Strategic analysis and optimal play
- Game recommendations and comparisons
- House rules evaluation
- Component usage guidance

**Professional Standards**:
- Authoritative expert tone
- Precise board game terminology
- Structured explanations with examples
- Explicit uncertainty handling

**Tool Calling**:
- Compatible with KB system tool calling
- Access to game catalog queries
- Knowledge base retrieval ready
- Future RAG integration prepared

## System Prompt Structure

The 4,850-character professional prompt includes:

### 1. ROLE & EXPERTISE
Defines agent identity and knowledge domains

### 2. KNOWLEDGE BASE INTEGRATION
```
{RAG_CONTEXT}
```
Placeholder for RAG chunk injection in future integration

### 3. RESPONSE GUIDELINES
- Professional standards and tone
- Clarity requirements and structure
- Length management by query type
- Uncertainty handling protocols

### 4. INTERACTION PATTERNS
Structured workflows for:
- Rule clarifications (identify → explain → context → edge cases)
- Strategic advice (analyze → options → trade-offs → recommend)
- Game recommendations (clarify → suggest → explain → differentiate)
- Comparisons (similarities → differences → complexity → recommend)

### 5. LIMITATIONS & BOUNDARIES
Explicit capabilities and constraints

### 6. OUTPUT FORMAT
Consistent response structure:
1. Direct Answer (one sentence)
2. Explanation (supporting details)
3. Additional Insights (if valuable)
4. Sources (citations or "General knowledge")

## Usage

### Running the Seeder

#### Method 1: Programmatic Call

```csharp
using Api.Infrastructure.Seeders;
using Microsoft.Extensions.DependencyInjection;

// In your seeding pipeline
var adminUserId = /* Get admin user ID */;
await DefaultAgentSeeder.SeedDefaultAgentAsync(dbContext, adminUserId, logger);
```

#### Method 2: PowerShell Script

```powershell
# From repository root
.\scripts\seed-default-agent.ps1
```

### Verification

After seeding, verify agent creation:

```sql
-- Check agent
SELECT Id, Name, Type, StrategyName, IsActive
FROM "AgentEntity"
WHERE "Name" = 'MeepleAssistant POC';

-- Check configuration
SELECT ac.Id, ac.LlmModel, ac.Temperature, ac.MaxTokens, ac.IsCurrent
FROM "AgentConfigurationEntity" ac
JOIN "AgentEntity" a ON ac."AgentId" = a."Id"
WHERE a."Name" = 'MeepleAssistant POC';
```

### API Testing

```bash
# Get agent details
curl http://localhost:8080/api/v1/agents

# Create chat with agent
curl -X POST http://localhost:8080/api/v1/chat \
  -H "Content-Type: application/json" \
  -d '{
    "agentId": "<agent-id-from-above>",
    "message": "What are the basic rules of Catan?"
  }'
```

## RAG Integration Path

### Phase 1: POC (Current)
- ✅ Agent seeded with professional prompt
- ✅ SingleModel strategy (no RAG)
- ✅ Tool calling ready
- ✅ Chat mode enabled
- ⏳ Responses from LLM training data only

### Phase 2: RAG Integration (Next)
- 📝 Select game rulebook VectorDocuments
- 📝 Update `SelectedDocumentIdsJson` in configuration
- 📝 Change strategy to `HybridSearch` or `IterativeRAG`
- 📝 System automatically injects RAG context
- ✅ Responses enhanced with game-specific knowledge

### Phase 3: Advanced RAG (Future)
- 📝 Multi-agent consensus for complex queries
- 📝 Iterative refinement for ambiguous questions
- 📝 Citation validation and source tracking
- 📝 Confidence scoring and quality metrics

## Testing Scenarios

### Basic Functionality

```yaml
scenario_1_simple_rule:
  query: "How do you win in Monopoly?"
  expected_behavior:
    - Direct answer first
    - Clear explanation of victory condition
    - Professional tone maintained
    - Source attribution (General knowledge)

scenario_2_strategy_analysis:
  query: "What's the best opening strategy in Catan?"
  expected_behavior:
    - Multiple strategic options presented
    - Trade-offs explained for each
    - Recommendation with reasoning
    - Game phase context provided

scenario_3_unknown_game:
  query: "Explain the rules of [obscure game]"
  expected_behavior:
    - Explicit limitation statement
    - General board game principles if applicable
    - Suggestion to consult official rulebook
    - No fabricated information

scenario_4_comparison:
  query: "Compare Splendor and Century Spice Road"
  expected_behavior:
    - Core similarities identified
    - Key mechanical differences highlighted
    - Complexity assessment for both
    - Recommendation based on preferences
```

### Conversational Context

```yaml
scenario_5_context_maintenance:
  conversation:
    - user: "Tell me about Wingspan"
    - assistant: [Provides overview]
    - user: "What about for 2 players?"
    - assistant: [Should reference Wingspan without re-asking]

  expected_behavior:
    - Maintains conversation context
    - Assumes "it" refers to Wingspan
    - Provides 2-player variant info
```

### RAG Readiness

```yaml
scenario_6_rag_placeholder:
  setup: System injects context into {RAG_CONTEXT}
  query: "What does the rulebook say about setup?"
  expected_behavior:
    - Uses provided context exclusively
    - Cites source document
    - Doesn't add external knowledge
    - Clear attribution format
```

## Maintenance

### Updating System Prompt

Edit `DefaultAgentSeeder.cs` constant:
```csharp
private const string SystemPrompt = @"...";
```

Then re-run seeder or manually update:
```sql
UPDATE "AgentConfigurationEntity"
SET "SystemPromptOverride" = '<new-prompt>'
WHERE "AgentId" IN (
  SELECT "Id" FROM "AgentEntity"
  WHERE "Name" = 'MeepleAssistant POC'
);
```

### Changing Model

Update configuration in database:
```sql
UPDATE "AgentConfigurationEntity"
SET "LlmModel" = 'anthropic/claude-3-5-haiku',
    "Temperature" = 0.4
WHERE "AgentId" IN (
  SELECT "Id" FROM "AgentEntity"
  WHERE "Name" = 'MeepleAssistant POC'
);
```

### Upgrading Strategy

When moving to RAG:
```sql
UPDATE "AgentEntity"
SET "StrategyName" = 'HybridSearch',
    "StrategyParametersJson" = '{"VectorWeight":0.7,"TopK":10,"MinScore":0.55}'
WHERE "Name" = 'MeepleAssistant POC';

UPDATE "AgentConfigurationEntity"
SET "SelectedDocumentIdsJson" = '["<doc-id-1>","<doc-id-2>"]'
WHERE "AgentId" IN (
  SELECT "Id" FROM "AgentEntity"
  WHERE "Name" = 'MeepleAssistant POC'
);
```

## Troubleshooting

### Seeder Fails

**Issue**: Foreign key constraint error for `CreatedBy`
**Solution**: Ensure admin user exists before seeding
```csharp
var adminUser = await db.Users.FirstOrDefaultAsync(u => u.Email == "admin@example.com");
if (adminUser == null) throw new InvalidOperationException("Admin user required for seeding");
```

**Issue**: Agent already exists
**Solution**: Seeder is idempotent - it will skip if agent exists. Check logs for confirmation.

### Configuration Issues

**Issue**: SystemPrompt exceeds 5000 characters
**Solution**: Current prompt is 4,850 chars. If extending, compress or split into multiple sections.

**Issue**: Model not available via OpenRouter
**Solution**: Check OpenRouter model availability: https://openrouter.ai/models
Alternative: `google/gemini-2.0-flash-001:free` (completely free)

### Testing Issues

**Issue**: Tests fail with "Admin user not found"
**Solution**: Tests use in-memory DB with auto-created admin GUID. No real user needed.

## References

- **Agent Domain Model**: `BoundedContexts/KnowledgeBase/Domain/Entities/Agent.cs`
- **Configuration Model**: `BoundedContexts/KnowledgeBase/Domain/Entities/AgentConfiguration.cs`
- **Seeder Implementation**: `Infrastructure/Seeders/DefaultAgentSeeder.cs`
- **Test Suite**: `tests/Api.Tests/Infrastructure/Seeders/DefaultAgentSeederTests.cs`
- **Related Issues**: Epic #3687 (AI Agent System)

---

**Created**: 2026-02-18
**Status**: Ready for integration testing
