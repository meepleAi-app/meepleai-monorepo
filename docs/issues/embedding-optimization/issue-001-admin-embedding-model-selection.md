# Issue #1: Admin UI - Embedding Model Selection (OpenRouter Support)

**Priority**: 🔴 Critical
**Category**: Configuration & Cost Optimization
**Effort**: 12-16 hours
**Impact**: High - Enables flexible cost management

---

## 📋 Problem Statement

Currently, the embedding model is **hardcoded in configuration files** (`.env`), requiring code deployment to change providers or models. Admins cannot:
- Switch between Ollama (free) and OpenRouter (paid) at runtime
- Test different OpenRouter embedding models
- Optimize cost/accuracy trade-off dynamically
- See cost estimates before selecting a model

**Current Limitations**:
```bash
# .env.production - STATIC configuration
EMBEDDING_PROVIDER=openai
EMBEDDING_MODEL=text-embedding-3-small

# Changing requires:
# 1. Edit .env file on server
# 2. Restart API service
# 3. No cost visibility
```

---

## 🎯 Objectives

1. **Admin UI for Model Selection**: Dynamic dropdown with all available embedding models
2. **OpenRouter Integration**: Support 10+ OpenRouter embedding models
3. **Cost Estimation**: Show cost per 1M tokens before selection
4. **Runtime Switching**: Change model without restart (warm reload)
5. **Validation**: Ensure selected model is available and working
6. **Audit Trail**: Track model changes in database

---

## 💰 Business Impact

### Cost Savings
| Scenario | Monthly Cost | Savings |
|----------|--------------|---------|
| **Current**: OpenAI text-embedding-3-small | $30-50/mese | Baseline |
| **Ollama nomic-embed-text** (local) | $0/mese | **100% ($30-50)** |
| **OpenRouter Jina AI** (free tier) | $0/mese | **100% ($30-50)** |
| **OpenRouter voyage-2** (premium) | $15/mese | **50% ($15-35)** |

### Flexibility Benefits
- ✅ Test models in staging without code changes
- ✅ Optimize cost/accuracy trade-off per environment
- ✅ Rapid model switching for A/B testing
- ✅ Rollback to previous model instantly

---

## 🛠️ Technical Implementation

### 1. Database Schema (Migration)

```sql
-- File: migrations/20250122_add_embedding_model_config.sql

-- Table: system_configurations (existing, add new keys)
INSERT INTO system_configurations (key, value, value_type, category, description, is_secret)
VALUES
  ('Embedding:Provider', 'ollama', 'string', 'AI', 'Embedding provider: ollama or openrouter', false),
  ('Embedding:Model', 'nomic-embed-text', 'string', 'AI', 'Embedding model name', false),
  ('Embedding:Dimensions', '768', 'integer', 'AI', 'Embedding vector dimensions', false),
  ('Embedding:CostPer1MTokens', '0.000', 'decimal', 'AI', 'Cost per 1M tokens (USD)', false),
  ('Embedding:LastUpdatedBy', 'system', 'string', 'AI', 'User who last changed model', false),
  ('Embedding:LastUpdatedAt', NOW()::text, 'string', 'AI', 'Timestamp of last model change', false);

-- Table: embedding_model_catalog (NEW)
CREATE TABLE embedding_model_catalog (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    provider VARCHAR(50) NOT NULL,                    -- 'ollama' or 'openrouter'
    model_id VARCHAR(100) NOT NULL UNIQUE,            -- e.g., 'text-embedding-3-small'
    display_name VARCHAR(200) NOT NULL,               -- 'OpenAI Embedding 3 Small'
    dimensions INTEGER NOT NULL,                      -- 1536
    cost_per_1m_tokens DECIMAL(10, 6) NOT NULL,      -- 0.020000
    max_tokens INTEGER,                               -- 8191
    multilingual BOOLEAN DEFAULT false,               -- true/false
    description TEXT,                                 -- 'General purpose embedding model'
    is_active BOOLEAN DEFAULT true,                   -- Admin can disable
    is_free_tier BOOLEAN DEFAULT false,               -- true for free models
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Create index
CREATE INDEX idx_embedding_model_catalog_provider ON embedding_model_catalog(provider);
CREATE INDEX idx_embedding_model_catalog_active ON embedding_model_catalog(is_active);

-- Seed data: OpenRouter models
INSERT INTO embedding_model_catalog (provider, model_id, display_name, dimensions, cost_per_1m_tokens, max_tokens, multilingual, description, is_free_tier)
VALUES
  -- OpenAI (via OpenRouter)
  ('openrouter', 'text-embedding-3-small', 'OpenAI Embedding 3 Small', 1536, 0.020, 8191, true, 'Cost-effective general purpose model', false),
  ('openrouter', 'text-embedding-3-large', 'OpenAI Embedding 3 Large', 3072, 0.130, 8191, true, 'Highest accuracy, more expensive', false),
  ('openrouter', 'text-embedding-ada-002', 'OpenAI Ada 002 (Legacy)', 1536, 0.100, 8191, true, 'Legacy model, higher cost', false),

  -- Voyage AI
  ('openrouter', 'voyage-2', 'Voyage AI v2', 1024, 0.100, 16000, true, 'Optimized for retrieval tasks', false),
  ('openrouter', 'voyage-large-2', 'Voyage AI Large v2', 1536, 0.120, 16000, true, 'Large model for complex queries', false),

  -- Jina AI (Free tier)
  ('openrouter', 'jina-embeddings-v2-base-en', 'Jina Embeddings v2 (EN)', 768, 0.000, 8192, false, 'FREE - English only', true),
  ('openrouter', 'jina-embeddings-v2-base-multi', 'Jina Embeddings v2 (Multi)', 768, 0.020, 8192, true, 'Multilingual support', false),

  -- Cohere
  ('openrouter', 'cohere-embed-english-v3', 'Cohere Embed English v3', 1024, 0.100, 512, false, 'Optimized for English', false),
  ('openrouter', 'cohere-embed-multilingual-v3', 'Cohere Embed Multi v3', 1024, 0.100, 512, true, 'Multilingual support', false),

  -- Ollama (local, free)
  ('ollama', 'nomic-embed-text', 'Nomic Embed Text (Local)', 768, 0.000, 8192, true, 'FREE - Self-hosted via Ollama', true),
  ('ollama', 'all-minilm', 'All-MiniLM (Local)', 384, 0.000, 256, true, 'FREE - Fast, smaller dimensions', true),
  ('ollama', 'mxbai-embed-large', 'MxBai Embed Large (Local)', 1024, 0.000, 512, true, 'FREE - Higher quality local model', true);

-- Audit table for model changes
CREATE TABLE embedding_model_change_log (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    previous_provider VARCHAR(50),
    previous_model VARCHAR(100),
    new_provider VARCHAR(50) NOT NULL,
    new_model VARCHAR(100) NOT NULL,
    changed_by_user_id UUID REFERENCES users(id),
    reason TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE INDEX idx_embedding_model_change_log_created ON embedding_model_change_log(created_at DESC);
```

### 2. Backend: Dynamic Model Repository

```csharp
// File: BoundedContexts/SystemConfiguration/Domain/Repositories/IEmbeddingModelRepository.cs

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

public interface IEmbeddingModelRepository
{
    /// <summary>
    /// Get all active embedding models from catalog
    /// </summary>
    Task<List<EmbeddingModelCatalogEntry>> GetActiveModelsAsync(CancellationToken ct = default);

    /// <summary>
    /// Get model details by ID
    /// </summary>
    Task<EmbeddingModelCatalogEntry?> GetModelByIdAsync(string modelId, CancellationToken ct = default);

    /// <summary>
    /// Get current active embedding configuration
    /// </summary>
    Task<EmbeddingModelConfig> GetCurrentConfigAsync(CancellationToken ct = default);

    /// <summary>
    /// Update embedding model configuration
    /// </summary>
    Task<bool> UpdateConfigAsync(
        string provider,
        string modelId,
        Guid changedByUserId,
        string? reason = null,
        CancellationToken ct = default);

    /// <summary>
    /// Log model change to audit trail
    /// </summary>
    Task LogModelChangeAsync(
        string previousProvider,
        string previousModel,
        string newProvider,
        string newModel,
        Guid changedByUserId,
        string? reason = null,
        CancellationToken ct = default);
}

// Domain models
public record EmbeddingModelCatalogEntry
{
    public Guid Id { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int Dimensions { get; init; }
    public decimal CostPer1MTokens { get; init; }
    public int? MaxTokens { get; init; }
    public bool Multilingual { get; init; }
    public string? Description { get; init; }
    public bool IsFreeTier { get; init; }
}

public record EmbeddingModelConfig
{
    public string Provider { get; init; } = string.Empty;
    public string Model { get; init; } = string.Empty;
    public int Dimensions { get; init; }
    public decimal CostPer1MTokens { get; init; }
    public DateTime LastUpdatedAt { get; init; }
    public string? LastUpdatedBy { get; init; }
}
```

### 3. Backend: CQRS Handlers

```csharp
// File: BoundedContexts/SystemConfiguration/Application/Queries/GetEmbeddingModelsQuery.cs

public record GetEmbeddingModelsQuery : IRequest<GetEmbeddingModelsResponse>;

public record GetEmbeddingModelsResponse
{
    public List<EmbeddingModelDto> AvailableModels { get; init; } = new();
    public EmbeddingModelDto? CurrentModel { get; init; }
}

public record EmbeddingModelDto
{
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public int Dimensions { get; init; }
    public decimal CostPer1MTokens { get; init; }
    public bool IsFreeTier { get; init; }
    public bool Multilingual { get; init; }
    public string? Description { get; init; }
    public bool IsCurrent { get; init; }
}

// Handler
public class GetEmbeddingModelsQueryHandler : IRequestHandler<GetEmbeddingModelsQuery, GetEmbeddingModelsResponse>
{
    private readonly IEmbeddingModelRepository _repo;

    public async Task<GetEmbeddingModelsResponse> Handle(
        GetEmbeddingModelsQuery query,
        CancellationToken ct)
    {
        var models = await _repo.GetActiveModelsAsync(ct);
        var currentConfig = await _repo.GetCurrentConfigAsync(ct);

        var modelDtos = models.Select(m => new EmbeddingModelDto
        {
            Provider = m.Provider,
            ModelId = m.ModelId,
            DisplayName = m.DisplayName,
            Dimensions = m.Dimensions,
            CostPer1MTokens = m.CostPer1MTokens,
            IsFreeTier = m.IsFreeTier,
            Multilingual = m.Multilingual,
            Description = m.Description,
            IsCurrent = m.Provider == currentConfig.Provider && m.ModelId == currentConfig.Model
        }).ToList();

        var currentModel = modelDtos.FirstOrDefault(m => m.IsCurrent);

        return new GetEmbeddingModelsResponse
        {
            AvailableModels = modelDtos,
            CurrentModel = currentModel
        };
    }
}
```

```csharp
// File: BoundedContexts/SystemConfiguration/Application/Commands/UpdateEmbeddingModelCommand.cs

public record UpdateEmbeddingModelCommand(
    string Provider,
    string ModelId,
    string? Reason = null
) : IRequest<UpdateEmbeddingModelResponse>;

public record UpdateEmbeddingModelResponse
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public EmbeddingModelDto? UpdatedModel { get; init; }
}

// Handler
public class UpdateEmbeddingModelCommandHandler : IRequestHandler<UpdateEmbeddingModelCommand, UpdateEmbeddingModelResponse>
{
    private readonly IEmbeddingModelRepository _repo;
    private readonly IEmbeddingService _embeddingService;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<UpdateEmbeddingModelCommandHandler> _logger;

    public async Task<UpdateEmbeddingModelResponse> Handle(
        UpdateEmbeddingModelCommand command,
        CancellationToken ct)
    {
        // 1. Validate model exists
        var model = await _repo.GetModelByIdAsync(command.ModelId, ct);
        if (model == null)
        {
            return new UpdateEmbeddingModelResponse
            {
                Success = false,
                ErrorMessage = $"Model '{command.ModelId}' not found in catalog"
            };
        }

        // 2. Validate provider matches
        if (model.Provider != command.Provider)
        {
            return new UpdateEmbeddingModelResponse
            {
                Success = false,
                ErrorMessage = $"Provider mismatch: expected '{model.Provider}', got '{command.Provider}'"
            };
        }

        // 3. Test model availability (optional: generate test embedding)
        try
        {
            _logger.LogInformation("Testing new embedding model: {Provider}/{Model}",
                command.Provider, command.ModelId);

            // Generate test embedding
            var testResult = await _embeddingService.GenerateEmbeddingAsync(
                "Test embedding for model validation",
                ct);

            if (!testResult.Success)
            {
                return new UpdateEmbeddingModelResponse
                {
                    Success = false,
                    ErrorMessage = $"Model test failed: {testResult.ErrorMessage}"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to test embedding model");
            return new UpdateEmbeddingModelResponse
            {
                Success = false,
                ErrorMessage = $"Model test exception: {ex.Message}"
            };
        }

        // 4. Get current config for audit
        var currentConfig = await _repo.GetCurrentConfigAsync(ct);

        // 5. Update configuration
        var updated = await _repo.UpdateConfigAsync(
            command.Provider,
            command.ModelId,
            _currentUser.UserId,
            command.Reason,
            ct);

        if (!updated)
        {
            return new UpdateEmbeddingModelResponse
            {
                Success = false,
                ErrorMessage = "Failed to update configuration"
            };
        }

        // 6. Log change to audit trail
        await _repo.LogModelChangeAsync(
            currentConfig.Provider,
            currentConfig.Model,
            command.Provider,
            command.ModelId,
            _currentUser.UserId,
            command.Reason,
            ct);

        _logger.LogInformation(
            "Embedding model updated: {PrevProvider}/{PrevModel} → {NewProvider}/{NewModel} by user {UserId}",
            currentConfig.Provider, currentConfig.Model,
            command.Provider, command.ModelId,
            _currentUser.UserId);

        return new UpdateEmbeddingModelResponse
        {
            Success = true,
            UpdatedModel = new EmbeddingModelDto
            {
                Provider = model.Provider,
                ModelId = model.ModelId,
                DisplayName = model.DisplayName,
                Dimensions = model.Dimensions,
                CostPer1MTokens = model.CostPer1MTokens,
                IsFreeTier = model.IsFreeTier,
                Multilingual = model.Multilingual,
                Description = model.Description,
                IsCurrent = true
            }
        };
    }
}
```

### 4. HTTP Endpoints

```csharp
// File: Routing/AdminEndpoints.cs (add to existing file)

// GET /api/v1/admin/embedding-models
app.MapGet("/api/v1/admin/embedding-models",
    [Authorize(Roles = "Admin")]
    async (IMediator mediator, CancellationToken ct) =>
    {
        var query = new GetEmbeddingModelsQuery();
        var response = await mediator.Send(query, ct);
        return Results.Ok(response);
    })
    .WithName("GetEmbeddingModels")
    .WithTags("Admin", "Embeddings")
    .Produces<GetEmbeddingModelsResponse>();

// PUT /api/v1/admin/embedding-models
app.MapPut("/api/v1/admin/embedding-models",
    [Authorize(Roles = "Admin")]
    async (
        [FromBody] UpdateEmbeddingModelRequest request,
        IMediator mediator,
        CancellationToken ct) =>
    {
        var command = new UpdateEmbeddingModelCommand(
            request.Provider,
            request.ModelId,
            request.Reason);

        var response = await mediator.Send(command, ct);

        return response.Success
            ? Results.Ok(response)
            : Results.BadRequest(response);
    })
    .WithName("UpdateEmbeddingModel")
    .WithTags("Admin", "Embeddings")
    .Produces<UpdateEmbeddingModelResponse>()
    .ProducesProblem(400);

public record UpdateEmbeddingModelRequest
{
    public string Provider { get; init; } = string.Empty;
    public string ModelId { get; init; } = string.Empty;
    public string? Reason { get; init; }
}
```

### 5. Frontend: Admin UI Component

```typescript
// File: apps/web/app/admin/embeddings/page.tsx

'use client';

import { useState, useEffect } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Alert, AlertDescription } from '@/components/ui/alert';
import { Badge } from '@/components/ui/badge';
import { Textarea } from '@/components/ui/textarea';
import { apiClient } from '@/lib/api';

interface EmbeddingModel {
  provider: string;
  modelId: string;
  displayName: string;
  dimensions: number;
  costPer1MTokens: number;
  isFreeTier: boolean;
  multilingual: boolean;
  description?: string;
  isCurrent: boolean;
}

interface EmbeddingModelsResponse {
  availableModels: EmbeddingModel[];
  currentModel: EmbeddingModel | null;
}

export default function EmbeddingConfigPage() {
  const [models, setModels] = useState<EmbeddingModel[]>([]);
  const [currentModel, setCurrentModel] = useState<EmbeddingModel | null>(null);
  const [selectedModelId, setSelectedModelId] = useState<string>('');
  const [reason, setReason] = useState('');
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(null);

  // Load models on mount
  useEffect(() => {
    loadModels();
  }, []);

  const loadModels = async () => {
    try {
      setLoading(true);
      const response = await apiClient.get<EmbeddingModelsResponse>('/api/v1/admin/embedding-models');

      setModels(response.data.availableModels);
      setCurrentModel(response.data.currentModel);
      setSelectedModelId(response.data.currentModel?.modelId || '');
      setError(null);
    } catch (err) {
      setError('Failed to load embedding models');
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  const handleUpdate = async () => {
    if (!selectedModelId) {
      setError('Please select a model');
      return;
    }

    const selectedModel = models.find(m => m.modelId === selectedModelId);
    if (!selectedModel) {
      setError('Invalid model selection');
      return;
    }

    try {
      setSaving(true);
      setError(null);
      setSuccess(null);

      await apiClient.put('/api/v1/admin/embedding-models', {
        provider: selectedModel.provider,
        modelId: selectedModel.modelId,
        reason: reason || undefined
      });

      setSuccess(`Embedding model updated to ${selectedModel.displayName}`);
      await loadModels(); // Reload to get updated current model
      setReason(''); // Clear reason field
    } catch (err: any) {
      setError(err.response?.data?.errorMessage || 'Failed to update embedding model');
      console.error(err);
    } finally {
      setSaving(false);
    }
  };

  const selectedModel = models.find(m => m.modelId === selectedModelId);
  const hasChanges = selectedModelId !== currentModel?.modelId;

  // Calculate cost estimate
  const estimateMonthyCost = (costPer1M: number) => {
    // Estimate: 150K embeddings/month × 500 tokens avg
    const tokensPerMonth = 150000 * 500; // 75M tokens
    return (tokensPerMonth / 1000000) * costPer1M;
  };

  if (loading) {
    return (
      <div className="container mx-auto py-8">
        <p>Loading embedding models...</p>
      </div>
    );
  }

  return (
    <div className="container mx-auto py-8 space-y-6">
      <div>
        <h1 className="text-3xl font-bold">Embedding Model Configuration</h1>
        <p className="text-muted-foreground mt-2">
          Configure the embedding model used for PDF indexing and search queries
        </p>
      </div>

      {error && (
        <Alert variant="destructive">
          <AlertDescription>{error}</AlertDescription>
        </Alert>
      )}

      {success && (
        <Alert>
          <AlertDescription>{success}</AlertDescription>
        </Alert>
      )}

      {/* Current Model Card */}
      {currentModel && (
        <Card>
          <CardHeader>
            <CardTitle>Current Model</CardTitle>
            <CardDescription>Active embedding model in production</CardDescription>
          </CardHeader>
          <CardContent className="space-y-2">
            <div className="flex items-center justify-between">
              <span className="font-medium">{currentModel.displayName}</span>
              <div className="flex gap-2">
                {currentModel.isFreeTier && <Badge variant="secondary">FREE</Badge>}
                {currentModel.multilingual && <Badge variant="outline">Multilingual</Badge>}
              </div>
            </div>
            <div className="grid grid-cols-3 gap-4 text-sm">
              <div>
                <p className="text-muted-foreground">Provider</p>
                <p className="font-medium">{currentModel.provider}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Dimensions</p>
                <p className="font-medium">{currentModel.dimensions}</p>
              </div>
              <div>
                <p className="text-muted-foreground">Cost/1M tokens</p>
                <p className="font-medium">
                  {currentModel.costPer1MTokens === 0
                    ? 'FREE'
                    : `$${currentModel.costPer1MTokens.toFixed(3)}`}
                </p>
              </div>
            </div>
            <div>
              <p className="text-muted-foreground text-sm">Estimated Monthly Cost</p>
              <p className="text-lg font-semibold">
                ${estimateMonthyCost(currentModel.costPer1MTokens).toFixed(2)}/month
              </p>
              <p className="text-xs text-muted-foreground">
                Based on 150K embeddings/month (avg 500 tokens each)
              </p>
            </div>
          </CardContent>
        </Card>
      )}

      {/* Model Selection Card */}
      <Card>
        <CardHeader>
          <CardTitle>Change Embedding Model</CardTitle>
          <CardDescription>Select a new embedding model and provider</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="model-select">Embedding Model</Label>
            <Select value={selectedModelId} onValueChange={setSelectedModelId}>
              <SelectTrigger id="model-select">
                <SelectValue placeholder="Select a model..." />
              </SelectTrigger>
              <SelectContent>
                {models.map((model) => (
                  <SelectItem key={model.modelId} value={model.modelId}>
                    <div className="flex items-center justify-between gap-4">
                      <span>{model.displayName}</span>
                      <div className="flex gap-1">
                        {model.isFreeTier && (
                          <Badge variant="secondary" className="text-xs">FREE</Badge>
                        )}
                        {model.multilingual && (
                          <Badge variant="outline" className="text-xs">Multi</Badge>
                        )}
                      </div>
                    </div>
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>

          {/* Selected Model Details */}
          {selectedModel && (
            <div className="p-4 bg-muted rounded-lg space-y-3">
              <h4 className="font-semibold">{selectedModel.displayName}</h4>

              {selectedModel.description && (
                <p className="text-sm text-muted-foreground">{selectedModel.description}</p>
              )}

              <div className="grid grid-cols-2 gap-4 text-sm">
                <div>
                  <p className="text-muted-foreground">Provider</p>
                  <p className="font-medium capitalize">{selectedModel.provider}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Model ID</p>
                  <p className="font-mono text-xs">{selectedModel.modelId}</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Dimensions</p>
                  <p className="font-medium">{selectedModel.dimensions}d</p>
                </div>
                <div>
                  <p className="text-muted-foreground">Cost/1M tokens</p>
                  <p className="font-medium">
                    {selectedModel.costPer1MTokens === 0
                      ? 'FREE ✅'
                      : `$${selectedModel.costPer1MTokens.toFixed(3)}`}
                  </p>
                </div>
              </div>

              {/* Cost Comparison */}
              {currentModel && selectedModel.modelId !== currentModel.modelId && (
                <div className="pt-3 border-t">
                  <p className="text-sm font-medium mb-2">Cost Impact</p>
                  <div className="grid grid-cols-3 gap-2 text-sm">
                    <div>
                      <p className="text-muted-foreground">Current</p>
                      <p className="font-medium">
                        ${estimateMonthyCost(currentModel.costPer1MTokens).toFixed(2)}/mo
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">New</p>
                      <p className="font-medium">
                        ${estimateMonthyCost(selectedModel.costPer1MTokens).toFixed(2)}/mo
                      </p>
                    </div>
                    <div>
                      <p className="text-muted-foreground">Savings</p>
                      <p className={`font-medium ${
                        estimateMonthyCost(selectedModel.costPer1MTokens) < estimateMonthyCost(currentModel.costPer1MTokens)
                          ? 'text-green-600'
                          : 'text-red-600'
                      }`}>
                        {estimateMonthyCost(selectedModel.costPer1MTokens) < estimateMonthyCost(currentModel.costPer1MTokens)
                          ? '-'
                          : '+'}
                        ${Math.abs(
                          estimateMonthyCost(selectedModel.costPer1MTokens) -
                          estimateMonthyCost(currentModel.costPer1MTokens)
                        ).toFixed(2)}
                      </p>
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}

          {/* Reason Field */}
          <div className="space-y-2">
            <Label htmlFor="reason">Reason for Change (Optional)</Label>
            <Textarea
              id="reason"
              placeholder="e.g., Cost optimization, testing new model, accuracy improvement..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
            />
          </div>

          {/* Action Buttons */}
          <div className="flex gap-2">
            <Button
              onClick={handleUpdate}
              disabled={!hasChanges || saving}
            >
              {saving ? 'Updating...' : 'Update Model'}
            </Button>

            {hasChanges && (
              <Button
                variant="outline"
                onClick={() => {
                  setSelectedModelId(currentModel?.modelId || '');
                  setReason('');
                }}
              >
                Cancel
              </Button>
            )}
          </div>

          {/* Warning for non-free models */}
          {selectedModel && !selectedModel.isFreeTier && hasChanges && (
            <Alert>
              <AlertDescription>
                ⚠️ This model has an associated cost of ${selectedModel.costPer1MTokens}/1M tokens.
                Estimated monthly cost: ${estimateMonthyCost(selectedModel.costPer1MTokens).toFixed(2)}
              </AlertDescription>
            </Alert>
          )}
        </CardContent>
      </Card>

      {/* Model Comparison Table */}
      <Card>
        <CardHeader>
          <CardTitle>Available Models Comparison</CardTitle>
        </CardHeader>
        <CardContent>
          <div className="relative overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="border-b">
                <tr className="text-left">
                  <th className="pb-2">Model</th>
                  <th className="pb-2">Provider</th>
                  <th className="pb-2">Dimensions</th>
                  <th className="pb-2">Cost/1M</th>
                  <th className="pb-2">Monthly Est.</th>
                  <th className="pb-2">Features</th>
                </tr>
              </thead>
              <tbody>
                {models
                  .sort((a, b) => a.costPer1MTokens - b.costPer1MTokens) // Sort by cost (free first)
                  .map((model) => (
                    <tr
                      key={model.modelId}
                      className={`border-b hover:bg-muted/50 cursor-pointer ${
                        model.isCurrent ? 'bg-green-50 dark:bg-green-950/20' : ''
                      }`}
                      onClick={() => setSelectedModelId(model.modelId)}
                    >
                      <td className="py-3">
                        <div className="flex items-center gap-2">
                          {model.displayName}
                          {model.isCurrent && (
                            <Badge variant="default">Current</Badge>
                          )}
                        </div>
                      </td>
                      <td className="py-3 capitalize">{model.provider}</td>
                      <td className="py-3">{model.dimensions}</td>
                      <td className="py-3">
                        {model.costPer1MTokens === 0
                          ? <Badge variant="secondary">FREE</Badge>
                          : `$${model.costPer1MTokens.toFixed(3)}`}
                      </td>
                      <td className="py-3 font-medium">
                        ${estimateMonthyCost(model.costPer1MTokens).toFixed(2)}
                      </td>
                      <td className="py-3">
                        <div className="flex gap-1">
                          {model.multilingual && (
                            <Badge variant="outline" className="text-xs">Multi</Badge>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
```

---

## ✅ Acceptance Criteria

1. **Admin UI Access**
   - [ ] Admin users can access `/admin/embeddings` page
   - [ ] Non-admin users receive 403 Forbidden

2. **Model Catalog Display**
   - [ ] All active models from database displayed
   - [ ] Current model highlighted
   - [ ] Cost per 1M tokens shown for each model
   - [ ] Monthly cost estimate calculated (150K embeddings × 500 tokens)
   - [ ] Free tier models badged

3. **Model Selection**
   - [ ] Admin can select any model from dropdown
   - [ ] Model details (dimensions, cost, provider) shown on selection
   - [ ] Cost comparison (current vs new) displayed
   - [ ] Reason field (optional) available

4. **Model Update**
   - [ ] "Update Model" button enabled only when selection changes
   - [ ] Model test embedding generated before applying
   - [ ] Configuration updated in database
   - [ ] Audit log entry created
   - [ ] Success/error message shown
   - [ ] EmbeddingService uses new model immediately (no restart)

5. **Runtime Model Switching**
   - [ ] EmbeddingService reads model from database (not .env)
   - [ ] Model change reflected within 60 seconds (cache refresh)
   - [ ] Existing embeddings remain valid (dimension check)

6. **Error Handling**
   - [ ] Invalid model selection rejected
   - [ ] Model test failure prevents update
   - [ ] Provider mismatch detected
   - [ ] Database errors shown to admin

7. **Audit Trail**
   - [ ] All model changes logged with timestamp
   - [ ] Changed by user ID recorded
   - [ ] Reason (if provided) saved
   - [ ] Previous model tracked

---

## 🧪 Testing Strategy

### Unit Tests
```csharp
// Test: EmbeddingModelRepositoryTests.cs
[Fact]
public async Task GetActiveModels_ReturnsOnlyActiveModels()
{
    // Arrange
    var repo = CreateRepository();

    // Act
    var models = await repo.GetActiveModelsAsync();

    // Assert
    Assert.All(models, m => Assert.True(m.IsActive));
    Assert.Contains(models, m => m.ModelId == "nomic-embed-text");
}

[Fact]
public async Task UpdateConfig_UpdatesSystemConfiguration()
{
    // Arrange
    var repo = CreateRepository();
    var userId = Guid.NewGuid();

    // Act
    var result = await repo.UpdateConfigAsync(
        "ollama",
        "nomic-embed-text",
        userId,
        "Cost optimization");

    // Assert
    Assert.True(result);
    var config = await repo.GetCurrentConfigAsync();
    Assert.Equal("ollama", config.Provider);
    Assert.Equal("nomic-embed-text", config.Model);
}
```

### Integration Tests
```csharp
// Test: EmbeddingModelEndpointsTests.cs
[Fact]
public async Task GetEmbeddingModels_ReturnsAvailableModels()
{
    // Arrange
    var client = CreateAuthenticatedClient(Role.Admin);

    // Act
    var response = await client.GetAsync("/api/v1/admin/embedding-models");

    // Assert
    response.EnsureSuccessStatusCode();
    var data = await response.Content.ReadFromJsonAsync<GetEmbeddingModelsResponse>();
    Assert.NotNull(data);
    Assert.NotEmpty(data.AvailableModels);
}

[Fact]
public async Task UpdateEmbeddingModel_UpdatesConfigAndLogsChange()
{
    // Arrange
    var client = CreateAuthenticatedClient(Role.Admin);
    var request = new UpdateEmbeddingModelRequest
    {
        Provider = "ollama",
        ModelId = "nomic-embed-text",
        Reason = "Test update"
    };

    // Act
    var response = await client.PutAsJsonAsync("/api/v1/admin/embedding-models", request);

    // Assert
    response.EnsureSuccessStatusCode();

    // Verify config updated
    var getResponse = await client.GetAsync("/api/v1/admin/embedding-models");
    var data = await getResponse.Content.ReadFromJsonAsync<GetEmbeddingModelsResponse>();
    Assert.Equal("nomic-embed-text", data.CurrentModel?.ModelId);

    // Verify audit log created
    // (check embedding_model_change_log table)
}
```

### E2E Tests
```typescript
// Test: admin-embedding-selection.spec.ts
test('admin can change embedding model', async ({ page }) => {
  // Login as admin
  await loginAsAdmin(page);

  // Navigate to embeddings page
  await page.goto('/admin/embeddings');

  // Select different model
  await page.getByRole('combobox', { name: 'Embedding Model' }).click();
  await page.getByRole('option', { name: /Nomic Embed Text/ }).click();

  // Verify cost estimate shown
  await expect(page.getByText(/\$0\.00\/month/)).toBeVisible();

  // Update model
  await page.getByRole('button', { name: 'Update Model' }).click();

  // Verify success message
  await expect(page.getByText(/Embedding model updated/)).toBeVisible();

  // Verify current model badge
  await expect(page.getByRole('button', { name: /Nomic Embed Text/ }).getByText('Current')).toBeVisible();
});
```

---

## 📈 Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| **Cost Flexibility** | 10+ models available | Model catalog count |
| **Admin Adoption** | >80% admins use feature | Usage analytics |
| **Model Switching Time** | <60 seconds | Time from update to active |
| **Cost Transparency** | 100% accurate estimates | Compare actual vs estimated |
| **Error Rate** | <1% failed updates | Update failures / total updates |

---

## 🔗 Dependencies

- System Configuration infrastructure (existing)
- Admin authentication & authorization
- EmbeddingService refactoring (to read from DB)
- Database migration tooling

---

## 📚 Related Issues

- [Issue #2: Query Embedding Cache](./issue-002-query-embedding-cache.md) - Performance optimization
- [Issue #6: Embedding Cost Monitoring](./issue-006-embedding-cost-monitoring.md) - Cost tracking

---

**Created**: 2025-11-22
**Author**: Engineering Team
**Estimated Effort**: 12-16 hours
**Priority**: 🔴 Critical
