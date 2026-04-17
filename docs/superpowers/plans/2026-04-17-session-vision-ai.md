# Session Vision AI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable users to upload images during game sessions so the AI agent can visually analyze board state and provide context-aware responses.

**Architecture:** Extend `ILlmClient` with multimodal content parts (text + image). Add `IImagePreprocessor` for resize/optimization/fallback. New `SessionSnapshot` entity in SessionTracking BC for persistent board photos with lazy GameState extraction. Frontend: image attachments in ChatInputBar + dedicated SnapshotPanel.

**Tech Stack:** .NET 9 (SkiaSharp for image processing), PostgreSQL (JSONB for GameState), Next.js 16 (React Query + Zustand), existing S3/local storage via `IBlobStorageService`.

**Spec:** `docs/superpowers/specs/2026-04-17-session-vision-ai-design.md`

---

## File Structure

### Backend — New Files
| File | Responsibility |
|------|---------------|
| `Services/LlmClients/ContentPart.cs` | ContentPart types + LlmMessage record |
| `Services/LlmClients/VisionCapability.cs` | `SupportsVision` extension, vision-aware model lists |
| `Services/ImageProcessing/IImagePreprocessor.cs` | Interface + records (ProcessedImage, ImageProcessingOptions) |
| `Services/ImageProcessing/SkiaImagePreprocessor.cs` | SkiaSharp implementation: resize, JPEG convert |
| `BoundedContexts/SessionTracking/Domain/Entities/SessionSnapshot.cs` | Aggregate: SessionSnapshot + SnapshotImage |
| `BoundedContexts/SessionTracking/Domain/Repositories/ISessionSnapshotRepository.cs` | Repository interface |
| `BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionSnapshotRepository.cs` | EF Core implementation |
| `BoundedContexts/SessionTracking/Application/Commands/SnapshotCommands.cs` | CreateSessionSnapshotCommand + handler |
| `BoundedContexts/SessionTracking/Application/Queries/SnapshotQueries.cs` | GetSessionSnapshots + GetLatestGameState queries + handlers |
| `BoundedContexts/SessionTracking/Application/Services/IGameStateExtractor.cs` | Interface for lazy GameState extraction |
| `BoundedContexts/SessionTracking/Application/Services/GameStateExtractor.cs` | Vision LLM call to extract structured game state |
| `BoundedContexts/KnowledgeBase/Domain/VisionTierLimits.cs` | Tier-based limits for vision features |
| `Routing/SessionTracking/SessionSnapshotEndpoints.cs` | Snapshot CRUD endpoints |

### Backend — Modified Files
| File | Changes |
|------|---------|
| `Services/LlmClients/ILlmClient.cs` | Add multimodal overloads + `SupportsVision` property |
| `Services/LlmClients/DeepSeekLlmClient.cs` | Implement multimodal methods, `SupportsVision = true` |
| `Services/LlmClients/OpenRouterLlmClient.cs` | Implement multimodal methods, `SupportsVision = true` |
| `Services/LlmClients/OllamaLlmClient.cs` | Stub multimodal (throw), `SupportsVision = false` |
| `Services/ILlmService.cs` | Add multimodal overloads on ILlmService |
| `BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs` | Implement multimodal GenerateCompletion with vision routing |
| `BoundedContexts/SessionTracking/Application/Commands/ChatCommandHandlers.cs` | Extend AskSessionAgentCommandHandler for images + lazy extraction |
| `BoundedContexts/SessionTracking/Application/Commands/ChatCommandValidators.cs` | Add validation for images |
| `BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/SessionTrackingServiceExtensions.cs` | Register new services |
| `BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register ImagePreprocessor |
| `Infrastructure/MeepleAiDbContext.cs` | Add DbSet for SessionSnapshot + SnapshotImage |
| `Routing/SessionTracking/SessionPlayerActionsEndpoints.cs` | Update ask-agent endpoint for multipart |

### Frontend — New Files
| File | Responsibility |
|------|---------------|
| `hooks/useChatImageAttachments.ts` | State management for ephemeral image attachments |
| `hooks/queries/useSessionSnapshots.ts` | React Query hooks for snapshots + game state |
| `lib/api/clients/sessionSnapshotsClient.ts` | API client for snapshot CRUD |
| `components/session/SessionSnapshotPanel.tsx` | Main snapshot panel with timeline |
| `components/session/SnapshotCard.tsx` | Individual snapshot card with status badge |
| `components/session/SnapshotUploadDialog.tsx` | Upload dialog with caption input |
| `components/session/GameStateDisplay.tsx` | Formatted GameState JSON display |

### Frontend — Modified Files
| File | Changes |
|------|---------|
| `components/chat/panel/ChatInputBar.tsx` | Add image button, preview thumbnails, multipart send |
| `components/chat/panel/ChatMessageBubble.tsx` | Render image thumbnails in user messages |

---

## Task 1: ContentPart Types + LlmMessage

**Files:**
- Create: `apps/api/src/Api/Services/LlmClients/ContentPart.cs`

- [ ] **Step 1: Create ContentPart types**

```csharp
// apps/api/src/Api/Services/LlmClients/ContentPart.cs
namespace Api.Services.LlmClients;

/// <summary>
/// Base type for multimodal content parts in LLM messages.
/// Follows the OpenAI content parts standard.
/// </summary>
internal abstract record ContentPart;

/// <summary>
/// Text content part for LLM messages.
/// </summary>
internal record TextContentPart(string Text) : ContentPart;

/// <summary>
/// Image content part with base64-encoded data.
/// </summary>
internal record ImageContentPart(string Base64Data, string MediaType) : ContentPart
{
    /// <summary>
    /// Creates a data URI for the image (e.g., "data:image/jpeg;base64,...").
    /// Used by OpenAI-compatible APIs.
    /// </summary>
    public string ToDataUri() => $"data:{MediaType};base64,{Base64Data}";
}

/// <summary>
/// A message in an LLM conversation with multimodal content parts.
/// </summary>
internal record LlmMessage(string Role, IReadOnlyList<ContentPart> Content)
{
    /// <summary>
    /// Creates a simple text-only message.
    /// </summary>
    public static LlmMessage FromText(string role, string text) =>
        new(role, [new TextContentPart(text)]);

    /// <summary>
    /// Returns true if this message contains any image content parts.
    /// </summary>
    public bool HasImages => Content.Any(c => c is ImageContentPart);
}
```

- [ ] **Step 2: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add apps/api/src/Api/Services/LlmClients/ContentPart.cs
git commit -m "feat(vision): add ContentPart types and LlmMessage for multimodal LLM support"
```

---

## Task 2: Extend ILlmClient with Multimodal Overloads

**Files:**
- Modify: `apps/api/src/Api/Services/LlmClients/ILlmClient.cs`

- [ ] **Step 1: Add multimodal methods and SupportsVision to ILlmClient**

Add these members to the `ILlmClient` interface after the existing methods:

```csharp
    /// <summary>
    /// Whether this provider supports vision/image content parts.
    /// Providers returning false will receive text-only fallback.
    /// </summary>
    bool SupportsVision { get; }

    /// <summary>
    /// Generate a multimodal chat completion with content parts (text + images).
    /// </summary>
    Task<LlmCompletionResult> GenerateCompletionAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a streaming multimodal chat completion.
    /// </summary>
    IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model,
        IReadOnlyList<LlmMessage> messages,
        double temperature,
        int maxTokens,
        CancellationToken ct = default);
```

Add required using at top:

```csharp
using Api.Services.LlmClients;
```

- [ ] **Step 2: Verify build fails (providers don't implement yet)**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | grep "error CS"`
Expected: CS0535 errors for each provider not implementing the new members.

- [ ] **Step 3: Implement multimodal on DeepSeekLlmClient**

Add to `DeepSeekLlmClient.cs`:

```csharp
    public bool SupportsVision => true;

    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string model, IReadOnlyList<LlmMessage> messages,
        double temperature, int maxTokens, CancellationToken ct = default)
    {
        var requestMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content.Select<ContentPart, object>(c => c switch
            {
                TextContentPart t => new { type = "text", text = t.Text },
                ImageContentPart img => new { type = "image_url", image_url = new { url = img.ToDataUri() } },
                _ => new { type = "text", text = string.Empty }
            }).ToArray()
        }).ToArray();

        var requestBody = new
        {
            model,
            messages = requestMessages,
            temperature,
            max_tokens = maxTokens,
            stream = false
        };

        return await SendCompletionRequestAsync(requestBody, model, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model, IReadOnlyList<LlmMessage> messages,
        double temperature, int maxTokens,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var requestMessages = messages.Select(m => new
        {
            role = m.Role,
            content = m.Content.Select<ContentPart, object>(c => c switch
            {
                TextContentPart t => new { type = "text", text = t.Text },
                ImageContentPart img => new { type = "image_url", image_url = new { url = img.ToDataUri() } },
                _ => new { type = "text", text = string.Empty }
            }).ToArray()
        }).ToArray();

        var requestBody = new
        {
            model,
            messages = requestMessages,
            temperature,
            max_tokens = maxTokens,
            stream = true
        };

        await foreach (var chunk in SendStreamingRequestAsync(requestBody, model, ct).ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
```

Note: `SendCompletionRequestAsync` and `SendStreamingRequestAsync` are private methods already in DeepSeekLlmClient that handle the HTTP call + response parsing. The multimodal methods reuse them — only the message format changes. If these private methods take the full request body as `object`, this works directly. If they take `(systemPrompt, userPrompt)`, refactor to extract the HTTP logic into a shared method that takes the request body object.

- [ ] **Step 4: Implement multimodal on OpenRouterLlmClient**

Same pattern as DeepSeek (OpenAI-compatible content parts format). Add:

```csharp
    public bool SupportsVision => true;
```

And implement the two multimodal methods with the same content-parts serialization pattern. OpenRouter uses the identical OpenAI message format.

- [ ] **Step 5: Implement stub on OllamaLlmClient**

```csharp
    public bool SupportsVision => false;

    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string model, IReadOnlyList<LlmMessage> messages,
        double temperature, int maxTokens, CancellationToken ct = default)
    {
        throw new NotSupportedException("Ollama does not support multimodal/vision requests. Use a vision-capable provider.");
    }

    public IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string model, IReadOnlyList<LlmMessage> messages,
        double temperature, int maxTokens, CancellationToken ct = default)
    {
        throw new NotSupportedException("Ollama does not support multimodal/vision requests. Use a vision-capable provider.");
    }
```

- [ ] **Step 6: Verify build succeeds**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Services/LlmClients/
git commit -m "feat(vision): extend ILlmClient with multimodal content parts and SupportsVision"
```

---

## Task 3: Extend ILlmService + HybridLlmService with Multimodal

**Files:**
- Modify: `apps/api/src/Api/Services/ILlmService.cs`
- Modify: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs`

- [ ] **Step 1: Add multimodal methods to ILlmService**

Add to `ILlmService` interface:

```csharp
    /// <summary>
    /// Generate a multimodal completion with content parts (text + images).
    /// Automatically routes to a vision-capable provider.
    /// </summary>
    Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);

    /// <summary>
    /// Generate a streaming multimodal completion.
    /// </summary>
    IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default);
```

Add at top of file:

```csharp
using Api.Services.LlmClients;
```

- [ ] **Step 2: Implement in HybridLlmService**

Add to `HybridLlmService`:

```csharp
    public async Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        CancellationToken ct = default)
    {
        var userContext = LlmUserContextMapper.CreateDefault(source);
        var selection = await _providerSelector.SelectProviderAsync(userContext, RagStrategy.Balanced, source, ct)
            .ConfigureAwait(false);

        bool requiresVision = messages.Any(m => m.HasImages);
        var client = selection.Client;

        // If vision required but provider doesn't support it, try fallback
        if (requiresVision && !client.SupportsVision)
        {
            var fallback = await _providerSelector.GetNextFallbackAsync(
                client.ProviderName, new HashSet<string>(StringComparer.Ordinal) { client.ProviderName }, ct)
                .ConfigureAwait(false);

            if (fallback?.Client.SupportsVision == true)
                client = fallback.Client;
            // else proceed with text-only (images will be ignored by caller)
        }

        if (requiresVision && client.SupportsVision)
        {
            return await client.GenerateCompletionAsync(
                selection.ModelId, messages, DefaultTemperature, DefaultMaxTokens, ct)
                .ConfigureAwait(false);
        }

        // Fallback: extract text only from messages
        var systemPrompt = string.Join("\n",
            messages.Where(m => m.Role == "system").SelectMany(m => m.Content.OfType<TextContentPart>()).Select(t => t.Text));
        var userPrompt = string.Join("\n",
            messages.Where(m => m.Role == "user").SelectMany(m => m.Content.OfType<TextContentPart>()).Select(t => t.Text));

        return await GenerateCompletionAsync(systemPrompt, userPrompt, source, ct).ConfigureAwait(false);
    }

    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages,
        RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var userContext = LlmUserContextMapper.CreateDefault(source);
        var selection = await _providerSelector.SelectProviderAsync(userContext, RagStrategy.Balanced, source, ct)
            .ConfigureAwait(false);

        bool requiresVision = messages.Any(m => m.HasImages);
        var client = selection.Client;

        if (requiresVision && !client.SupportsVision)
        {
            var fallback = await _providerSelector.GetNextFallbackAsync(
                client.ProviderName, new HashSet<string>(StringComparer.Ordinal) { client.ProviderName }, ct)
                .ConfigureAwait(false);

            if (fallback?.Client.SupportsVision == true)
                client = fallback.Client;
        }

        if (requiresVision && client.SupportsVision)
        {
            await foreach (var chunk in client.GenerateCompletionStreamAsync(
                selection.ModelId, messages, DefaultTemperature, DefaultMaxTokens, ct)
                .ConfigureAwait(false))
            {
                yield return chunk;
            }
            yield break;
        }

        // Fallback: text-only
        var systemPrompt = string.Join("\n",
            messages.Where(m => m.Role == "system").SelectMany(m => m.Content.OfType<TextContentPart>()).Select(t => t.Text));
        var userPrompt = string.Join("\n",
            messages.Where(m => m.Role == "user").SelectMany(m => m.Content.OfType<TextContentPart>()).Select(t => t.Text));

        await foreach (var chunk in GenerateCompletionStreamAsync(systemPrompt, userPrompt, source, ct)
            .ConfigureAwait(false))
        {
            yield return chunk;
        }
    }
```

Note: Check that `DefaultTemperature` and `DefaultMaxTokens` constants exist in HybridLlmService (they should based on the existing code pattern). If not, use `0.3` and `500` directly.

- [ ] **Step 3: Verify build**

Run: `cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/Services/ILlmService.cs apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/HybridLlmService.cs
git commit -m "feat(vision): add multimodal methods to ILlmService and HybridLlmService"
```

---

## Task 4: IImagePreprocessor + SkiaSharp Implementation

**Files:**
- Create: `apps/api/src/Api/Services/ImageProcessing/IImagePreprocessor.cs`
- Create: `apps/api/src/Api/Services/ImageProcessing/SkiaImagePreprocessor.cs`

- [ ] **Step 1: Create IImagePreprocessor interface**

```csharp
// apps/api/src/Api/Services/ImageProcessing/IImagePreprocessor.cs
namespace Api.Services.ImageProcessing;

internal record ProcessedImage(byte[] Data, string MediaType, int Width, int Height, long SizeBytes);

internal record ImageProcessingOptions(
    int MaxWidth = 1024,
    int MaxHeight = 1024,
    long MaxSizeBytes = 5_000_000,
    bool ConvertToJpeg = true);

internal interface IImagePreprocessor
{
    /// <summary>
    /// Resize and optimize an image for vision API consumption.
    /// </summary>
    Task<ProcessedImage> ProcessAsync(byte[] imageData, string mediaType, ImageProcessingOptions? options = null);

    /// <summary>
    /// Validate that the given data is a supported image format.
    /// Returns the detected media type or null if unsupported.
    /// </summary>
    string? DetectMediaType(byte[] data);
}
```

- [ ] **Step 2: Create SkiaImagePreprocessor**

```csharp
// apps/api/src/Api/Services/ImageProcessing/SkiaImagePreprocessor.cs
using SkiaSharp;

namespace Api.Services.ImageProcessing;

internal sealed class SkiaImagePreprocessor : IImagePreprocessor
{
    private static readonly ImageProcessingOptions DefaultOptions = new();

    public Task<ProcessedImage> ProcessAsync(byte[] imageData, string mediaType, ImageProcessingOptions? options = null)
    {
        options ??= DefaultOptions;

        using var original = SKBitmap.Decode(imageData)
            ?? throw new ArgumentException("Unable to decode image data.");

        int targetWidth = original.Width;
        int targetHeight = original.Height;

        // Scale down if exceeds max dimensions
        if (targetWidth > options.MaxWidth || targetHeight > options.MaxHeight)
        {
            double scale = Math.Min(
                (double)options.MaxWidth / targetWidth,
                (double)options.MaxHeight / targetHeight);
            targetWidth = (int)(targetWidth * scale);
            targetHeight = (int)(targetHeight * scale);
        }

        using var resized = original.Resize(new SKImageInfo(targetWidth, targetHeight), SKFilterQuality.High);
        if (resized is null)
            throw new InvalidOperationException("Failed to resize image.");

        using var image = SKImage.FromBitmap(resized);

        var format = options.ConvertToJpeg ? SKEncodedImageFormat.Jpeg : SKEncodedImageFormat.Png;
        var outputMediaType = options.ConvertToJpeg ? "image/jpeg" : mediaType;
        int quality = 85;

        using var encoded = image.Encode(format, quality);
        var data = encoded.ToArray();

        // If still too large, reduce quality
        if (data.Length > options.MaxSizeBytes && options.ConvertToJpeg)
        {
            quality = 60;
            using var reEncoded = image.Encode(SKEncodedImageFormat.Jpeg, quality);
            data = reEncoded.ToArray();
        }

        return Task.FromResult(new ProcessedImage(data, outputMediaType, targetWidth, targetHeight, data.Length));
    }

    public string? DetectMediaType(byte[] data)
    {
        if (data.Length < 4) return null;

        // JPEG: FF D8 FF
        if (data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
            return "image/jpeg";

        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "image/png";

        // WebP: RIFF....WEBP
        if (data.Length >= 12 && data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46
            && data[8] == 0x57 && data[9] == 0x45 && data[10] == 0x42 && data[11] == 0x50)
            return "image/webp";

        return null;
    }
}
```

- [ ] **Step 3: Add SkiaSharp NuGet package**

Run: `cd apps/api/src/Api && dotnet add package SkiaSharp --version 2.88.*`

- [ ] **Step 4: Register in DI**

Add to `KnowledgeBaseServiceExtensions.cs` in the `AddLlmServices` method:

```csharp
    services.AddSingleton<IImagePreprocessor, SkiaImagePreprocessor>();
```

Add using: `using Api.Services.ImageProcessing;`

- [ ] **Step 5: Verify build**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | tail -5`
Expected: Build succeeded.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Services/ImageProcessing/ apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs apps/api/src/Api/Api.csproj
git commit -m "feat(vision): add IImagePreprocessor with SkiaSharp resize and format detection"
```

---

## Task 5: VisionTierLimits

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/VisionTierLimits.cs`

- [ ] **Step 1: Create VisionTierLimits**

```csharp
// apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/VisionTierLimits.cs
namespace Api.BoundedContexts.KnowledgeBase.Domain;

internal record VisionTierConfig(
    int MaxImagesPerMessage,
    int MaxSnapshotsPerSession,
    int MaxImagesPerSnapshot,
    int MaxImageResolution,
    bool GameStateExtractionEnabled);

internal static class VisionTierLimits
{
    private static readonly IReadOnlyDictionary<string, VisionTierConfig> TierConfigs =
        new Dictionary<string, VisionTierConfig>(StringComparer.OrdinalIgnoreCase)
        {
            ["alpha"] = new(MaxImagesPerMessage: 5, MaxSnapshotsPerSession: 20, MaxImagesPerSnapshot: 5, MaxImageResolution: 2048, GameStateExtractionEnabled: true),
            ["free"] = new(MaxImagesPerMessage: 2, MaxSnapshotsPerSession: 5, MaxImagesPerSnapshot: 3, MaxImageResolution: 1024, GameStateExtractionEnabled: false),
            ["premium"] = new(MaxImagesPerMessage: 5, MaxSnapshotsPerSession: 30, MaxImagesPerSnapshot: 10, MaxImageResolution: 2048, GameStateExtractionEnabled: true),
        };

    private static readonly VisionTierConfig DefaultConfig = TierConfigs["free"];

    public static VisionTierConfig GetConfig(string? tier)
    {
        if (tier is null) return DefaultConfig;
        return TierConfigs.GetValueOrDefault(tier, DefaultConfig);
    }
}
```

- [ ] **Step 2: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -3
git add apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/VisionTierLimits.cs
git commit -m "feat(vision): add VisionTierLimits with tier-based configuration"
```

---

## Task 6: SessionSnapshot Entity + Repository

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionSnapshot.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionSnapshotRepository.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionSnapshotRepository.cs`

- [ ] **Step 1: Create SessionSnapshot + SnapshotImage entities**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionSnapshot.cs
using System.ComponentModel.DataAnnotations;

namespace Api.BoundedContexts.SessionTracking.Domain.Entities;

/// <summary>
/// Persistent snapshot of game board state with images.
/// Supports lazy GameState extraction via vision AI.
/// </summary>
public class SessionSnapshot
{
    private readonly List<SnapshotImage> _images = [];

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid UserId { get; private set; }
    public int TurnNumber { get; private set; }

    [MaxLength(200)]
    public string? Caption { get; private set; }

    /// <summary>
    /// JSON-structured game state extracted by vision AI. Null until lazy extraction runs.
    /// </summary>
    public string? ExtractedGameState { get; private set; }

    public IReadOnlyList<SnapshotImage> Images => _images.AsReadOnly();

    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    private SessionSnapshot() { }

    public static SessionSnapshot Create(Guid sessionId, Guid userId, int turnNumber, string? caption)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session ID required.", nameof(sessionId));
        if (userId == Guid.Empty) throw new ArgumentException("User ID required.", nameof(userId));
        if (turnNumber < 0) throw new ArgumentOutOfRangeException(nameof(turnNumber));
        if (caption?.Length > 200) throw new ArgumentException("Caption max 200 chars.", nameof(caption));

        return new SessionSnapshot
        {
            Id = Guid.NewGuid(),
            SessionId = sessionId,
            UserId = userId,
            TurnNumber = turnNumber,
            Caption = caption,
        };
    }

    public void AddImage(string storageKey, string mediaType, int width, int height)
    {
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("Storage key required.", nameof(storageKey));

        _images.Add(new SnapshotImage
        {
            Id = Guid.NewGuid(),
            StorageKey = storageKey,
            MediaType = mediaType,
            Width = width,
            Height = height,
            OrderIndex = _images.Count,
        });
    }

    public void UpdateGameState(string gameStateJson)
    {
        ExtractedGameState = gameStateJson ?? throw new ArgumentNullException(nameof(gameStateJson));
        UpdatedAt = DateTime.UtcNow;
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Individual image within a session snapshot.
/// </summary>
public class SnapshotImage
{
    public Guid Id { get; internal set; }

    [MaxLength(500)]
    public string StorageKey { get; internal set; } = string.Empty;

    [MaxLength(50)]
    public string MediaType { get; internal set; } = string.Empty;
    public int Width { get; internal set; }
    public int Height { get; internal set; }
    public int OrderIndex { get; internal set; }

    // Navigation — EF Core
    public Guid SessionSnapshotId { get; internal set; }
}
```

- [ ] **Step 2: Create repository interface**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionSnapshotRepository.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

internal interface ISessionSnapshotRepository
{
    Task<SessionSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<List<SessionSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task<SessionSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task AddAsync(SessionSnapshot snapshot, CancellationToken ct = default);
    Task UpdateAsync(SessionSnapshot snapshot, CancellationToken ct = default);
    Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
```

- [ ] **Step 3: Create EF Core repository implementation**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionSnapshotRepository.cs
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Infrastructure.Repositories;

internal sealed class SessionSnapshotRepository : ISessionSnapshotRepository
{
    private readonly MeepleAiDbContext _db;

    public SessionSnapshotRepository(MeepleAiDbContext db) => _db = db;

    public async Task<SessionSnapshot?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await _db.SessionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted, ct)
            .ConfigureAwait(false);

    public async Task<List<SessionSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken ct = default) =>
        await _db.SessionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .Where(s => s.SessionId == sessionId && !s.IsDeleted)
            .OrderByDescending(s => s.TurnNumber)
            .ThenByDescending(s => s.CreatedAt)
            .ToListAsync(ct)
            .ConfigureAwait(false);

    public async Task<SessionSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken ct = default) =>
        await _db.SessionSnapshots
            .Include(s => s.Images.OrderBy(i => i.OrderIndex))
            .Where(s => s.SessionId == sessionId && !s.IsDeleted)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

    public async Task AddAsync(SessionSnapshot snapshot, CancellationToken ct = default) =>
        await _db.SessionSnapshots.AddAsync(snapshot, ct).ConfigureAwait(false);

    public Task UpdateAsync(SessionSnapshot snapshot, CancellationToken ct = default)
    {
        _db.SessionSnapshots.Update(snapshot);
        return Task.CompletedTask;
    }

    public async Task<int> CountBySessionIdAsync(Guid sessionId, CancellationToken ct = default) =>
        await _db.SessionSnapshots.CountAsync(s => s.SessionId == sessionId && !s.IsDeleted, ct)
            .ConfigureAwait(false);

    public async Task SaveChangesAsync(CancellationToken ct = default) =>
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
}
```

- [ ] **Step 4: Add DbSet to MeepleAiDbContext**

Add to `Infrastructure/MeepleAiDbContext.cs`:

```csharp
    public DbSet<BoundedContexts.SessionTracking.Domain.Entities.SessionSnapshot> SessionSnapshots => Set<BoundedContexts.SessionTracking.Domain.Entities.SessionSnapshot>();
```

- [ ] **Step 5: Add EF configuration**

Find or create the EF configuration file for SessionTracking and add configuration for SessionSnapshot + SnapshotImage. The configuration should include:
- `HasColumnName("snake_case")` for all columns (per CLAUDE.md: always explicit snake_case)
- `HasQueryFilter(s => !s.IsDeleted)` for soft delete
- SnapshotImage as owned collection or separate table with FK

- [ ] **Step 6: Register repository in DI**

Add to `SessionTrackingServiceExtensions.cs`:

```csharp
    services.AddScoped<ISessionSnapshotRepository, SessionSnapshotRepository>();
```

Add required usings.

- [ ] **Step 7: Create EF migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddSessionSnapshots`

Review the generated migration SQL. Verify it creates `session_snapshots` and `snapshot_images` tables with correct column names and indexes.

- [ ] **Step 8: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build 2>&1 | tail -3
git add apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Entities/SessionSnapshot.cs apps/api/src/Api/BoundedContexts/SessionTracking/Domain/Repositories/ISessionSnapshotRepository.cs apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/Repositories/SessionSnapshotRepository.cs apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/SessionTrackingServiceExtensions.cs apps/api/src/Api/Infrastructure/Migrations/
git commit -m "feat(vision): add SessionSnapshot entity, repository, and EF migration"
```

---

## Task 7: Snapshot Commands + Handlers

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SnapshotCommands.cs`

- [ ] **Step 1: Create commands and handler**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SnapshotCommands.cs
using MediatR;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain;
using Api.Middleware.Exceptions;
using Api.Services.ImageProcessing;
using Api.Services.Pdf;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public record SnapshotImageUpload(byte[] Data, string MediaType, string? FileName);

public record CreateSessionSnapshotCommand(
    Guid SessionId,
    Guid UserId,
    int TurnNumber,
    string? Caption,
    List<SnapshotImageUpload> Images
) : IRequest<CreateSnapshotResult>;

public record CreateSnapshotResult(Guid SnapshotId, int ImageCount);

internal sealed class CreateSessionSnapshotCommandHandler
    : IRequestHandler<CreateSessionSnapshotCommand, CreateSnapshotResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IBlobStorageService _blobStorage;

    public CreateSessionSnapshotCommandHandler(
        ISessionRepository sessionRepository,
        ISessionSnapshotRepository snapshotRepository,
        IImagePreprocessor imagePreprocessor,
        IBlobStorageService blobStorage)
    {
        _sessionRepository = sessionRepository;
        _snapshotRepository = snapshotRepository;
        _imagePreprocessor = imagePreprocessor;
        _blobStorage = blobStorage;
    }

    public async Task<CreateSnapshotResult> Handle(
        CreateSessionSnapshotCommand request, CancellationToken ct)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, ct).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Validate tier limits
        var tierConfig = VisionTierLimits.GetConfig("alpha"); // TODO: resolve user tier from session
        var currentCount = await _snapshotRepository.CountBySessionIdAsync(request.SessionId, ct).ConfigureAwait(false);
        if (currentCount >= tierConfig.MaxSnapshotsPerSession)
            throw new ConflictException($"Maximum {tierConfig.MaxSnapshotsPerSession} snapshots per session reached.");

        if (request.Images.Count > tierConfig.MaxImagesPerSnapshot)
            throw new ConflictException($"Maximum {tierConfig.MaxImagesPerSnapshot} images per snapshot.");

        if (request.Images.Count == 0)
            throw new ConflictException("At least one image is required.");

        var snapshot = SessionSnapshot.Create(request.SessionId, request.UserId, request.TurnNumber, request.Caption);

        var options = new ImageProcessingOptions(
            MaxWidth: tierConfig.MaxImageResolution,
            MaxHeight: tierConfig.MaxImageResolution);

        for (int i = 0; i < request.Images.Count; i++)
        {
            var img = request.Images[i];
            var processed = await _imagePreprocessor.ProcessAsync(img.Data, img.MediaType, options).ConfigureAwait(false);

            var fileName = $"img_{i}.jpg";
            using var stream = new MemoryStream(processed.Data);
            var storageResult = await _blobStorage.StoreAsync(
                stream, fileName, $"snapshots/{request.SessionId}/{snapshot.Id}", ct).ConfigureAwait(false);

            if (!storageResult.Success)
                throw new InvalidOperationException($"Failed to store snapshot image: {storageResult.ErrorMessage}");

            snapshot.AddImage(
                storageResult.FilePath ?? $"snapshots/{request.SessionId}/{snapshot.Id}/{fileName}",
                processed.MediaType,
                processed.Width,
                processed.Height);
        }

        await _snapshotRepository.AddAsync(snapshot, ct).ConfigureAwait(false);
        await _snapshotRepository.SaveChangesAsync(ct).ConfigureAwait(false);

        return new CreateSnapshotResult(snapshot.Id, snapshot.Images.Count);
    }
}
```

- [ ] **Step 2: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/SnapshotCommands.cs
git commit -m "feat(vision): add CreateSessionSnapshotCommand with image processing and storage"
```

---

## Task 8: Snapshot Queries + GameState Extractor

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/SnapshotQueries.cs`
- Create: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/GameStateExtractor.cs`

- [ ] **Step 1: Create queries and handlers**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/SnapshotQueries.cs
using MediatR;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Services.Pdf;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public record SessionSnapshotDto(
    Guid Id, Guid SessionId, int TurnNumber, string? Caption,
    bool HasGameState, DateTime CreatedAt,
    List<SnapshotImageDto> Images);

public record SnapshotImageDto(Guid Id, string? DownloadUrl, string MediaType, int Width, int Height, int OrderIndex);

public record GameStateResult(Guid SnapshotId, string? GameStateJson, bool IsExtracted, DateTime SnapshotCreatedAt);

// --- Get all snapshots for a session ---
public record GetSessionSnapshotsQuery(Guid SessionId) : IRequest<List<SessionSnapshotDto>>;

internal sealed class GetSessionSnapshotsQueryHandler
    : IRequestHandler<GetSessionSnapshotsQuery, List<SessionSnapshotDto>>
{
    private readonly ISessionSnapshotRepository _repository;
    private readonly IBlobStorageService _blobStorage;

    public GetSessionSnapshotsQueryHandler(ISessionSnapshotRepository repository, IBlobStorageService blobStorage)
    {
        _repository = repository;
        _blobStorage = blobStorage;
    }

    public async Task<List<SessionSnapshotDto>> Handle(GetSessionSnapshotsQuery request, CancellationToken ct)
    {
        var snapshots = await _repository.GetBySessionIdAsync(request.SessionId, ct).ConfigureAwait(false);
        var result = new List<SessionSnapshotDto>();

        foreach (var s in snapshots)
        {
            var images = new List<SnapshotImageDto>();
            foreach (var img in s.Images)
            {
                var url = await _blobStorage.GetPresignedDownloadUrlAsync(
                    img.StorageKey, $"snapshots/{s.SessionId}/{s.Id}").ConfigureAwait(false);
                images.Add(new SnapshotImageDto(img.Id, url, img.MediaType, img.Width, img.Height, img.OrderIndex));
            }
            result.Add(new SessionSnapshotDto(s.Id, s.SessionId, s.TurnNumber, s.Caption,
                s.ExtractedGameState is not null, s.CreatedAt, images));
        }

        return result;
    }
}

// --- Get latest game state ---
public record GetLatestGameStateQuery(Guid SessionId) : IRequest<GameStateResult?>;

internal sealed class GetLatestGameStateQueryHandler
    : IRequestHandler<GetLatestGameStateQuery, GameStateResult?>
{
    private readonly ISessionSnapshotRepository _repository;

    public GetLatestGameStateQueryHandler(ISessionSnapshotRepository repository)
    {
        _repository = repository;
    }

    public async Task<GameStateResult?> Handle(GetLatestGameStateQuery request, CancellationToken ct)
    {
        var latest = await _repository.GetLatestBySessionIdAsync(request.SessionId, ct).ConfigureAwait(false);
        if (latest is null) return null;

        return new GameStateResult(latest.Id, latest.ExtractedGameState,
            latest.ExtractedGameState is not null, latest.CreatedAt);
    }
}
```

- [ ] **Step 2: Create GameStateExtractor service**

```csharp
// apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/GameStateExtractor.cs
using System.Text.Json;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Services;
using Api.Services.LlmClients;
using Api.Services.Pdf;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SessionTracking.Application.Services;

internal interface IGameStateExtractor
{
    /// <summary>
    /// Lazy-extract game state from the latest snapshot's images.
    /// Returns the extracted JSON or null if extraction fails/not applicable.
    /// Idempotent: skips if GameState already extracted for this snapshot.
    /// </summary>
    Task<string?> ExtractIfNeededAsync(Guid sessionId, string? gameName, CancellationToken ct = default);
}

internal sealed class GameStateExtractor : IGameStateExtractor
{
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILlmService _llmService;
    private readonly ILogger<GameStateExtractor> _logger;

    public GameStateExtractor(
        ISessionSnapshotRepository snapshotRepository,
        IBlobStorageService blobStorage,
        ILlmService llmService,
        ILogger<GameStateExtractor> logger)
    {
        _snapshotRepository = snapshotRepository;
        _blobStorage = blobStorage;
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<string?> ExtractIfNeededAsync(Guid sessionId, string? gameName, CancellationToken ct = default)
    {
        var snapshot = await _snapshotRepository.GetLatestBySessionIdAsync(sessionId, ct).ConfigureAwait(false);
        if (snapshot is null) return null;

        // Idempotent: already extracted
        if (snapshot.ExtractedGameState is not null) return snapshot.ExtractedGameState;

        try
        {
            // Build multimodal messages for vision extraction
            var contentParts = new List<ContentPart>();
            foreach (var img in snapshot.Images)
            {
                var storageKey = img.StorageKey;
                var gameId = $"snapshots/{snapshot.SessionId}/{snapshot.Id}";
                using var stream = await _blobStorage.RetrieveAsync(storageKey, gameId, ct).ConfigureAwait(false);
                if (stream is null) continue;

                using var ms = new MemoryStream();
                await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
                var base64 = Convert.ToBase64String(ms.ToArray());
                contentParts.Add(new ImageContentPart(base64, img.MediaType));
            }

            if (contentParts.Count == 0)
            {
                _logger.LogWarning("No images could be loaded for snapshot {SnapshotId}", snapshot.Id);
                return null;
            }

            var gameContext = gameName is not null ? $"The game is: {gameName}." : "Unknown board game.";

            var systemPrompt = $"""
                You are a board game image analyzer. Analyze the images and return structured JSON.
                {gameContext}
                Return ONLY valid JSON in this format:
                {{
                  "turn_estimate": null,
                  "players": [],
                  "board_description": "description of what you see",
                  "notable_state": [],
                  "confidence": 0.5
                }}
                """;

            contentParts.Add(new TextContentPart("Extract the game state from these photos."));

            var messages = new List<LlmMessage>
            {
                LlmMessage.FromText("system", systemPrompt),
                new("user", contentParts)
            };

            var result = await _llmService.GenerateMultimodalCompletionAsync(messages, RequestSource.AgentTask, ct)
                .ConfigureAwait(false);

            if (!result.Success)
            {
                _logger.LogWarning("GameState extraction failed: {Error}", result.ErrorMessage);
                return null;
            }

            // Validate JSON and check confidence
            var response = result.Response.Trim();
            // Strip markdown code fences if present
            if (response.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = response.IndexOf('\n');
                var lastFence = response.LastIndexOf("```", StringComparison.Ordinal);
                if (firstNewline > 0 && lastFence > firstNewline)
                    response = response[(firstNewline + 1)..lastFence].Trim();
            }

            using var doc = JsonDocument.Parse(response);
            var confidence = doc.RootElement.TryGetProperty("confidence", out var conf) ? conf.GetDouble() : 0.0;

            if (confidence < 0.4)
            {
                _logger.LogInformation("GameState extraction confidence {Confidence} below threshold for snapshot {SnapshotId}", confidence, snapshot.Id);
                return null;
            }

            snapshot.UpdateGameState(response);
            await _snapshotRepository.UpdateAsync(snapshot, ct).ConfigureAwait(false);
            await _snapshotRepository.SaveChangesAsync(ct).ConfigureAwait(false);

            return response;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse GameState JSON for snapshot {SnapshotId}", snapshot.Id);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GameState extraction error for snapshot {SnapshotId}", snapshot.Id);
            return null;
        }
    }
}
```

- [ ] **Step 3: Register GameStateExtractor in DI**

Add to `SessionTrackingServiceExtensions.cs`:

```csharp
    services.AddScoped<IGameStateExtractor, GameStateExtractor>();
```

- [ ] **Step 4: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Queries/SnapshotQueries.cs apps/api/src/Api/BoundedContexts/SessionTracking/Application/Services/GameStateExtractor.cs apps/api/src/Api/BoundedContexts/SessionTracking/Infrastructure/DependencyInjection/SessionTrackingServiceExtensions.cs
git commit -m "feat(vision): add snapshot queries, GameStateExtractor with lazy extraction"
```

---

## Task 9: Snapshot Endpoints

**Files:**
- Create: `apps/api/src/Api/Routing/SessionTracking/SessionSnapshotEndpoints.cs`

- [ ] **Step 1: Create endpoint file**

```csharp
// apps/api/src/Api/Routing/SessionTracking/SessionSnapshotEndpoints.cs
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using MediatR;

namespace Api.Routing.SessionTracking;

internal static class SessionSnapshotEndpoints
{
    public static void MapSessionSnapshotEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/live-sessions/{sessionId:guid}/snapshots")
            .RequireAuthorization()
            .WithTags("SessionTracking", "Vision");

        group.MapPost("/", async (
            Guid sessionId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var form = await httpContext.Request.ReadFormAsync(ct).ConfigureAwait(false);
            var userId = Guid.Parse(form["userId"].ToString());
            var turnNumber = int.Parse(form["turnNumber"].ToString());
            var caption = form["caption"].ToString();

            var images = new List<SnapshotImageUpload>();
            foreach (var file in form.Files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct).ConfigureAwait(false);
                images.Add(new SnapshotImageUpload(ms.ToArray(), file.ContentType, file.FileName));
            }

            var command = new CreateSessionSnapshotCommand(sessionId, userId, turnNumber,
                string.IsNullOrWhiteSpace(caption) ? null : caption, images);
            var result = await mediator.Send(command, ct).ConfigureAwait(false);
            return Results.Created($"/api/v1/live-sessions/{sessionId}/snapshots/{result.SnapshotId}", result);
        })
        .DisableAntiforgery()
        .WithName("CreateSessionSnapshot")
        .WithSummary("Upload board state snapshot with images")
        .Produces<CreateSnapshotResult>(201).Produces(400).Produces(404).Produces(409);

        group.MapGet("/", async (Guid sessionId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetSessionSnapshotsQuery(sessionId), ct).ConfigureAwait(false);
            return Results.Ok(result);
        })
        .WithName("GetSessionSnapshots")
        .WithSummary("List all snapshots for a session")
        .Produces<List<SessionSnapshotDto>>(200);

        group.MapGet("/game-state", async (Guid sessionId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetLatestGameStateQuery(sessionId), ct).ConfigureAwait(false);
            return result is null ? Results.NoContent() : Results.Ok(result);
        })
        .WithName("GetLatestGameState")
        .WithSummary("Get latest extracted game state for a session")
        .Produces<GameStateResult>(200).Produces(204);
    }
}
```

- [ ] **Step 2: Register endpoints in app startup**

Find the file where session endpoints are mapped (search for `MapSessionPlayerActionsEndpoints` or similar) and add:

```csharp
app.MapSessionSnapshotEndpoints();
```

- [ ] **Step 3: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5
git add apps/api/src/Api/Routing/SessionTracking/SessionSnapshotEndpoints.cs
git commit -m "feat(vision): add snapshot CRUD endpoints (upload, list, game-state)"
```

---

## Task 10: Extend AskSessionAgent for Inline Images + Lazy Extraction

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ChatCommandHandlers.cs`
- Modify: `apps/api/src/Api/Routing/SessionTracking/SessionPlayerActionsEndpoints.cs`

- [ ] **Step 1: Extend AskSessionAgentCommand**

In `ChatCommandHandlers.cs`, find `public record AskSessionAgentCommand` and add the Images parameter. If the command is defined elsewhere, find it first:

```csharp
public record ChatImageAttachment(byte[] Data, string MediaType, string? FileName);

public record AskSessionAgentCommand(
    Guid SessionId,
    Guid SenderId,
    string Question,
    int? TurnNumber,
    List<ChatImageAttachment>? Images = null  // NEW - ephemeral inline images
) : IRequest<AskSessionAgentResult>;
```

- [ ] **Step 2: Extend AskSessionAgentCommandHandler**

Replace the handler to support vision:

```csharp
internal class AskSessionAgentCommandHandler : IRequestHandler<AskSessionAgentCommand, AskSessionAgentResult>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly ISessionChatRepository _chatRepository;
    private readonly IMediator _mediator;
    private readonly ILlmService _llmService;
    private readonly IImagePreprocessor _imagePreprocessor;
    private readonly IGameStateExtractor _gameStateExtractor;
    private readonly ILogger<AskSessionAgentCommandHandler> _logger;

    public AskSessionAgentCommandHandler(
        ISessionRepository sessionRepository,
        ISessionChatRepository chatRepository,
        IMediator mediator,
        ILlmService llmService,
        IImagePreprocessor imagePreprocessor,
        IGameStateExtractor gameStateExtractor,
        ILogger<AskSessionAgentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _chatRepository = chatRepository;
        _mediator = mediator;
        _llmService = llmService;
        _imagePreprocessor = imagePreprocessor;
        _gameStateExtractor = gameStateExtractor;
        _logger = logger;
    }

    public async Task<AskSessionAgentResult> Handle(AskSessionAgentCommand request, CancellationToken cancellationToken)
    {
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        _ = session.Participants.FirstOrDefault(p => p.Id == request.SenderId)
            ?? throw new NotFoundException($"Participant {request.SenderId} not found in session");

        // Save user question
        var userSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var userMessage = SessionChatMessage.CreateTextMessage(
            request.SessionId, request.SenderId, request.Question, userSeq, request.TurnNumber);
        await _chatRepository.AddAsync(userMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Lazy GameState extraction from snapshots
        var gameState = await _gameStateExtractor.ExtractIfNeededAsync(
            request.SessionId, null, cancellationToken).ConfigureAwait(false);

        var agentType = "tutor";
        string answer;
        float? confidence;

        try
        {
            var systemPrompt = $"You are a helpful board game tutor assisting during a game session (Game ID: {session.GameId}). " +
                               "Answer questions about rules, strategy, and gameplay concisely.";

            if (gameState is not null)
                systemPrompt += $"\n\nCurrent game state (extracted from board photos):\n{gameState}";

            bool hasImages = request.Images is { Count: > 0 };

            if (hasImages)
            {
                // Build multimodal message with inline images
                var userParts = new List<ContentPart>();
                foreach (var img in request.Images!)
                {
                    var processed = await _imagePreprocessor.ProcessAsync(img.Data, img.MediaType).ConfigureAwait(false);
                    var base64 = Convert.ToBase64String(processed.Data);
                    userParts.Add(new ImageContentPart(base64, processed.MediaType));
                }
                userParts.Add(new TextContentPart(request.Question));

                var messages = new List<LlmMessage>
                {
                    LlmMessage.FromText("system", systemPrompt),
                    new("user", userParts)
                };

                var result = await _llmService.GenerateMultimodalCompletionAsync(
                    messages, RequestSource.AgentTask, cancellationToken).ConfigureAwait(false);

                answer = result.Success ? result.Response : "I'm sorry, I couldn't process your question right now. Please try again.";
                confidence = result.Success ? 0.85f : null;
            }
            else
            {
                // Text-only path (existing behavior)
                var result = await _llmService.GenerateCompletionAsync(
                    systemPrompt, request.Question, RequestSource.AgentTask, cancellationToken).ConfigureAwait(false);

                answer = result.Success ? result.Response : "I'm sorry, I couldn't process your question right now. Please try again.";
                confidence = result.Success ? 0.85f : null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM service error for session {SessionId}", request.SessionId);
            answer = "I'm sorry, an error occurred while processing your question. Please try again.";
            confidence = null;
        }

        var agentSeq = await _chatRepository.GetNextSequenceNumberAsync(request.SessionId, cancellationToken).ConfigureAwait(false);
        var agentMessage = SessionChatMessage.CreateAgentResponse(
            request.SessionId, answer, agentSeq, agentType, confidence, null, request.TurnNumber);

        await _chatRepository.AddAsync(agentMessage, cancellationToken).ConfigureAwait(false);
        await _chatRepository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await _mediator.Publish(new SessionChatMessageSentEvent
        {
            SessionId = request.SessionId,
            MessageId = agentMessage.Id,
            SenderId = null,
            MessageType = SessionChatMessageType.AgentResponse,
            Content = answer,
            TurnNumber = request.TurnNumber,
        }, cancellationToken).ConfigureAwait(false);

        return new AskSessionAgentResult(agentMessage.Id, answer, agentType, agentMessage.Confidence, null);
    }
}
```

- [ ] **Step 3: Update ask-agent endpoint for multipart**

In `SessionPlayerActionsEndpoints.cs`, update the ask-agent endpoint to accept multipart form data with optional images:

```csharp
group.MapPost("/game-sessions/{sessionId:guid}/chat/ask-agent", async (
    Guid sessionId,
    HttpContext httpContext,
    IMediator mediator,
    CancellationToken ct) =>
{
    List<ChatImageAttachment>? images = null;
    string question;
    Guid senderId;
    int? turnNumber = null;

    if (httpContext.Request.HasFormContentType)
    {
        var form = await httpContext.Request.ReadFormAsync(ct).ConfigureAwait(false);
        question = form["question"].ToString();
        senderId = Guid.Parse(form["senderId"].ToString());
        if (int.TryParse(form["turnNumber"].ToString(), out var tn)) turnNumber = tn;

        if (form.Files.Count > 0)
        {
            images = [];
            foreach (var file in form.Files)
            {
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct).ConfigureAwait(false);
                images.Add(new ChatImageAttachment(ms.ToArray(), file.ContentType, file.FileName));
            }
        }
    }
    else
    {
        var body = await httpContext.Request.ReadFromJsonAsync<AskSessionAgentCommand>(ct).ConfigureAwait(false)
            ?? throw new BadHttpRequestException("Invalid request body");
        question = body.Question;
        senderId = body.SenderId;
        turnNumber = body.TurnNumber;
    }

    var command = new AskSessionAgentCommand(sessionId, senderId, question, turnNumber, images);
    var result = await mediator.Send(command, ct).ConfigureAwait(false);
    return Results.Ok(result);
})
.DisableAntiforgery()
.RequireAuthorization()
.WithName("AskSessionAgent")
.WithTags("SessionTracking", "Chat", "AI")
.WithSummary("Ask the RAG agent a question in session context, optionally with images")
.Produces<AskSessionAgentResult>(200).Produces(400).Produces(401).Produces(404);
```

- [ ] **Step 4: Verify build + Commit**

```bash
cd apps/api/src/Api && dotnet build --no-restore 2>&1 | tail -5
git add apps/api/src/Api/BoundedContexts/SessionTracking/Application/Commands/ChatCommandHandlers.cs apps/api/src/Api/Routing/SessionTracking/SessionPlayerActionsEndpoints.cs
git commit -m "feat(vision): extend AskSessionAgent with inline images and lazy GameState extraction"
```

---

## Task 11: Frontend — useChatImageAttachments Hook

**Files:**
- Create: `apps/web/src/hooks/useChatImageAttachments.ts`

- [ ] **Step 1: Create the hook**

```typescript
// apps/web/src/hooks/useChatImageAttachments.ts
'use client';

import { useState, useCallback } from 'react';

export interface ChatImagePreview {
  file: File;
  previewUrl: string;
  mediaType: string;
}

const MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB
const SUPPORTED_TYPES = ['image/jpeg', 'image/png', 'image/webp'];

export function useChatImageAttachments(maxImages: number = 5) {
  const [images, setImages] = useState<ChatImagePreview[]>([]);

  const addImage = useCallback((file: File): string | null => {
    if (!SUPPORTED_TYPES.includes(file.type)) {
      return 'Formato non supportato. Usa JPEG, PNG o WebP.';
    }
    if (file.size > MAX_FILE_SIZE) {
      return 'Immagine troppo grande. Massimo 10MB.';
    }
    setImages(prev => {
      if (prev.length >= maxImages) return prev;
      const previewUrl = URL.createObjectURL(file);
      return [...prev, { file, previewUrl, mediaType: file.type }];
    });
    return null;
  }, [maxImages]);

  const removeImage = useCallback((index: number) => {
    setImages(prev => {
      const removed = prev[index];
      if (removed) URL.revokeObjectURL(removed.previewUrl);
      return prev.filter((_, i) => i !== index);
    });
  }, []);

  const clearImages = useCallback(() => {
    setImages(prev => {
      prev.forEach(img => URL.revokeObjectURL(img.previewUrl));
      return [];
    });
  }, []);

  const hasImages = images.length > 0;
  const canAddMore = images.length < maxImages;

  return { images, addImage, removeImage, clearImages, hasImages, canAddMore };
}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/hooks/useChatImageAttachments.ts
git commit -m "feat(vision): add useChatImageAttachments hook for ephemeral image management"
```

---

## Task 12: Frontend — Extend ChatInputBar with Image Support

**Files:**
- Modify: `apps/web/src/components/chat/panel/ChatInputBar.tsx`

- [ ] **Step 1: Read current ChatInputBar.tsx and extend it**

Add image button (🖼️), file input ref, preview thumbnails, and multipart send capability. Key changes:

1. Import `useChatImageAttachments` hook
2. Add hidden `<input type="file" accept="image/*" multiple>` with ref
3. Add 🖼️ button next to existing 📎 button
4. Show thumbnail previews with X remove button above the textarea when images are present
5. Modify `onSend` prop to accept optional `FormData` or change to `onSendWithImages(text: string, images: File[])` callback
6. On send: if images present, call `onSendWithImages(value, images.map(i => i.file))` then `clearImages()`

The exact code depends on the current ChatInputBar structure. The worker should read the current file and make minimal, focused changes.

- [ ] **Step 2: Verify dev build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/chat/panel/ChatInputBar.tsx
git commit -m "feat(vision): add image attachment button and preview to ChatInputBar"
```

---

## Task 13: Frontend — Extend ChatMessageBubble for Image Display

**Files:**
- Modify: `apps/web/src/components/chat/panel/ChatMessageBubble.tsx`

- [ ] **Step 1: Extend ChatMessageBubble to display images**

Add optional `imageUrls?: string[]` prop. When present, render thumbnail grid above the text content:

```tsx
{imageUrls && imageUrls.length > 0 && (
  <div className="flex gap-1.5 mb-2 flex-wrap">
    {imageUrls.map((url, i) => (
      <img
        key={i}
        src={url}
        alt={`Attachment ${i + 1}`}
        className="w-20 h-15 object-cover rounded-md border border-white/10 cursor-pointer"
        onClick={() => window.open(url, '_blank')}
      />
    ))}
  </div>
)}
```

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/components/chat/panel/ChatMessageBubble.tsx
git commit -m "feat(vision): display image thumbnails in chat message bubbles"
```

---

## Task 14: Frontend — Session Snapshots API Client + Hooks

**Files:**
- Create: `apps/web/src/lib/api/clients/sessionSnapshotsClient.ts`
- Create: `apps/web/src/hooks/queries/useSessionSnapshots.ts`

- [ ] **Step 1: Create API client**

```typescript
// apps/web/src/lib/api/clients/sessionSnapshotsClient.ts
export interface SessionSnapshotDto {
  id: string;
  sessionId: string;
  turnNumber: number;
  caption: string | null;
  hasGameState: boolean;
  createdAt: string;
  images: SnapshotImageDto[];
}

export interface SnapshotImageDto {
  id: string;
  downloadUrl: string | null;
  mediaType: string;
  width: number;
  height: number;
  orderIndex: number;
}

export interface GameStateResult {
  snapshotId: string;
  gameStateJson: string | null;
  isExtracted: boolean;
  snapshotCreatedAt: string;
}

export interface CreateSnapshotResult {
  snapshotId: string;
  imageCount: number;
}

export function createSessionSnapshotsClient() {
  return {
    async getSnapshots(sessionId: string): Promise<SessionSnapshotDto[]> {
      const res = await fetch(`/api/v1/live-sessions/${sessionId}/snapshots`);
      if (!res.ok) throw new Error(`Failed to fetch snapshots: ${res.status}`);
      return res.json();
    },

    async getGameState(sessionId: string): Promise<GameStateResult | null> {
      const res = await fetch(`/api/v1/live-sessions/${sessionId}/snapshots/game-state`);
      if (res.status === 204) return null;
      if (!res.ok) throw new Error(`Failed to fetch game state: ${res.status}`);
      return res.json();
    },

    async createSnapshot(
      sessionId: string,
      userId: string,
      turnNumber: number,
      images: File[],
      caption?: string
    ): Promise<CreateSnapshotResult> {
      const formData = new FormData();
      formData.append('userId', userId);
      formData.append('turnNumber', turnNumber.toString());
      if (caption) formData.append('caption', caption);
      images.forEach(img => formData.append('images', img));

      const res = await fetch(`/api/v1/live-sessions/${sessionId}/snapshots`, {
        method: 'POST',
        body: formData,
      });
      if (!res.ok) throw new Error(`Failed to create snapshot: ${res.status}`);
      return res.json();
    },
  };
}
```

- [ ] **Step 2: Create React Query hooks**

```typescript
// apps/web/src/hooks/queries/useSessionSnapshots.ts
'use client';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { createSessionSnapshotsClient } from '@/lib/api/clients/sessionSnapshotsClient';

const client = createSessionSnapshotsClient();

export const snapshotKeys = {
  all: ['session-snapshots'] as const,
  list: (sessionId: string) => [...snapshotKeys.all, 'list', sessionId] as const,
  gameState: (sessionId: string) => [...snapshotKeys.all, 'game-state', sessionId] as const,
};

export function useSessionSnapshots(sessionId: string, enabled = true) {
  return useQuery({
    queryKey: snapshotKeys.list(sessionId),
    queryFn: () => client.getSnapshots(sessionId),
    enabled,
    staleTime: 30_000, // 30s — snapshots change during active play
  });
}

export function useLatestGameState(sessionId: string, enabled = true) {
  return useQuery({
    queryKey: snapshotKeys.gameState(sessionId),
    queryFn: () => client.getGameState(sessionId),
    enabled,
    staleTime: 60_000, // 1min
  });
}

export function useCreateSnapshot(sessionId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (args: { userId: string; turnNumber: number; images: File[]; caption?: string }) =>
      client.createSnapshot(sessionId, args.userId, args.turnNumber, args.images, args.caption),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: snapshotKeys.list(sessionId) });
      queryClient.invalidateQueries({ queryKey: snapshotKeys.gameState(sessionId) });
    },
  });
}
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/lib/api/clients/sessionSnapshotsClient.ts apps/web/src/hooks/queries/useSessionSnapshots.ts
git commit -m "feat(vision): add session snapshots API client and React Query hooks"
```

---

## Task 15: Frontend — Snapshot UI Components

**Files:**
- Create: `apps/web/src/components/session/GameStateDisplay.tsx`
- Create: `apps/web/src/components/session/SnapshotCard.tsx`
- Create: `apps/web/src/components/session/SnapshotUploadDialog.tsx`
- Create: `apps/web/src/components/session/SessionSnapshotPanel.tsx`

- [ ] **Step 1: Create GameStateDisplay**

```tsx
// apps/web/src/components/session/GameStateDisplay.tsx
'use client';

interface GameStateDisplayProps {
  gameStateJson: string;
}

export function GameStateDisplay({ gameStateJson }: GameStateDisplayProps) {
  let parsed: Record<string, unknown>;
  try {
    parsed = JSON.parse(gameStateJson);
  } catch {
    return <p className="text-xs text-muted-foreground">Dati non disponibili</p>;
  }

  const board = parsed.board_description as string | undefined;
  const notable = parsed.notable_state as string[] | undefined;
  const confidence = parsed.confidence as number | undefined;

  return (
    <div className="mt-2 p-2 bg-black/20 rounded text-xs text-muted-foreground space-y-1">
      {board && <p>{board}</p>}
      {notable && notable.length > 0 && (
        <ul className="list-disc list-inside">
          {notable.map((item, i) => <li key={i}>{item}</li>)}
        </ul>
      )}
      {confidence !== undefined && (
        <p className="text-emerald-500">Confidence: {(confidence * 100).toFixed(0)}%</p>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Create SnapshotCard**

```tsx
// apps/web/src/components/session/SnapshotCard.tsx
'use client';

import type { SessionSnapshotDto } from '@/lib/api/clients/sessionSnapshotsClient';
import { GameStateDisplay } from './GameStateDisplay';

interface SnapshotCardProps {
  snapshot: SessionSnapshotDto;
  isLatest?: boolean;
}

export function SnapshotCard({ snapshot, isLatest }: SnapshotCardProps) {
  return (
    <div className={`rounded-lg border p-3 ${isLatest ? 'border-emerald-500/30 bg-emerald-500/5' : 'border-white/10 bg-white/[0.02]'}`}>
      <div className="flex items-center justify-between mb-2">
        <span className={`text-xs font-semibold ${isLatest ? 'text-emerald-500' : 'text-muted-foreground'}`}>
          Turno {snapshot.turnNumber} {isLatest && '— Ultimo'}
        </span>
        <span className={`text-[10px] px-2 py-0.5 rounded ${snapshot.hasGameState ? 'bg-emerald-500/15 text-emerald-500' : 'bg-amber-500/15 text-amber-500'}`}>
          {snapshot.hasGameState ? '✓ Analizzato' : '⏳ Non analizzato'}
        </span>
      </div>
      <div className="flex gap-1.5 mb-2">
        {snapshot.images.map(img => (
          <img
            key={img.id}
            src={img.downloadUrl ?? ''}
            alt={`Snapshot img ${img.orderIndex}`}
            className="w-16 h-12 object-cover rounded border border-white/10"
          />
        ))}
      </div>
      {snapshot.caption && (
        <p className="text-xs text-muted-foreground italic">&ldquo;{snapshot.caption}&rdquo;</p>
      )}
      {snapshot.hasGameState && snapshot.images.length > 0 && (
        <GameStateDisplay gameStateJson={/* fetched separately or embedded */""} />
      )}
    </div>
  );
}
```

Note: The GameState JSON is not included in the snapshot DTO for the list view (to keep payloads small). The GameStateDisplay can be populated from the `useLatestGameState` hook for the latest snapshot only, or added to the DTO if needed.

- [ ] **Step 3: Create SnapshotUploadDialog**

```tsx
// apps/web/src/components/session/SnapshotUploadDialog.tsx
'use client';

import { useState, useRef } from 'react';

interface SnapshotUploadDialogProps {
  open: boolean;
  onClose: () => void;
  onUpload: (images: File[], caption: string, turnNumber: number) => void;
  currentTurn: number;
}

export function SnapshotUploadDialog({ open, onClose, onUpload, currentTurn }: SnapshotUploadDialogProps) {
  const [caption, setCaption] = useState('');
  const [turnNumber, setTurnNumber] = useState(currentTurn);
  const [files, setFiles] = useState<File[]>([]);
  const inputRef = useRef<HTMLInputElement>(null);

  if (!open) return null;

  const handleFileChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    if (e.target.files) setFiles(Array.from(e.target.files));
  };

  const handleSubmit = () => {
    if (files.length === 0) return;
    onUpload(files, caption, turnNumber);
    setFiles([]);
    setCaption('');
    onClose();
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/50">
      <div className="bg-card border border-white/10 rounded-xl p-6 w-full max-w-md space-y-4">
        <h3 className="text-lg font-semibold">Nuovo Snapshot</h3>

        <div>
          <label className="text-sm text-muted-foreground">Immagini</label>
          <input ref={inputRef} type="file" accept="image/*" multiple onChange={handleFileChange}
            className="mt-1 block w-full text-sm" />
          {files.length > 0 && <p className="text-xs text-muted-foreground mt-1">{files.length} immagini selezionate</p>}
        </div>

        <div>
          <label className="text-sm text-muted-foreground">Turno</label>
          <input type="number" value={turnNumber} min={0} onChange={e => setTurnNumber(Number(e.target.value))}
            className="mt-1 block w-full rounded bg-white/5 border border-white/10 px-3 py-1.5 text-sm" />
        </div>

        <div>
          <label className="text-sm text-muted-foreground">Descrizione (opzionale)</label>
          <input type="text" value={caption} onChange={e => setCaption(e.target.value)} maxLength={200} placeholder="Es: Dopo aver costruito la città"
            className="mt-1 block w-full rounded bg-white/5 border border-white/10 px-3 py-1.5 text-sm" />
        </div>

        <div className="flex justify-end gap-2 pt-2">
          <button onClick={onClose} className="px-4 py-2 text-sm rounded border border-white/10">Annulla</button>
          <button onClick={handleSubmit} disabled={files.length === 0}
            className="px-4 py-2 text-sm rounded bg-amber-500 text-black font-medium disabled:opacity-50">
            Carica
          </button>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 4: Create SessionSnapshotPanel**

```tsx
// apps/web/src/components/session/SessionSnapshotPanel.tsx
'use client';

import { useState } from 'react';
import { useSessionSnapshots, useCreateSnapshot } from '@/hooks/queries/useSessionSnapshots';
import { SnapshotCard } from './SnapshotCard';
import { SnapshotUploadDialog } from './SnapshotUploadDialog';

interface SessionSnapshotPanelProps {
  sessionId: string;
  userId: string;
  currentTurn: number;
}

export function SessionSnapshotPanel({ sessionId, userId, currentTurn }: SessionSnapshotPanelProps) {
  const [uploadOpen, setUploadOpen] = useState(false);
  const { data: snapshots, isLoading } = useSessionSnapshots(sessionId);
  const createSnapshot = useCreateSnapshot(sessionId);

  const handleUpload = (images: File[], caption: string, turnNumber: number) => {
    createSnapshot.mutate({ userId, turnNumber, images, caption: caption || undefined });
  };

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <div>
          <h3 className="text-sm font-semibold text-amber-500">📸 Stato Partita</h3>
          <p className="text-xs text-muted-foreground">{snapshots?.length ?? 0} snapshot salvati</p>
        </div>
        <button onClick={() => setUploadOpen(true)}
          className="text-xs px-3 py-1.5 rounded-lg border border-amber-500/40 text-amber-500 hover:bg-amber-500/10">
          + Nuovo Snapshot
        </button>
      </div>

      {isLoading && <p className="text-xs text-muted-foreground">Caricamento...</p>}

      <div className="space-y-2">
        {snapshots?.map((s, i) => (
          <SnapshotCard key={s.id} snapshot={s} isLatest={i === 0} />
        ))}
      </div>

      {!isLoading && snapshots?.length === 0 && (
        <p className="text-xs text-muted-foreground text-center py-4">
          Nessuno snapshot. Scatta una foto del tavolo per iniziare.
        </p>
      )}

      <SnapshotUploadDialog
        open={uploadOpen}
        onClose={() => setUploadOpen(false)}
        onUpload={handleUpload}
        currentTurn={currentTurn}
      />
    </div>
  );
}
```

- [ ] **Step 5: Verify build + Commit**

```bash
cd apps/web && pnpm build 2>&1 | tail -10
git add apps/web/src/components/session/GameStateDisplay.tsx apps/web/src/components/session/SnapshotCard.tsx apps/web/src/components/session/SnapshotUploadDialog.tsx apps/web/src/components/session/SessionSnapshotPanel.tsx
git commit -m "feat(vision): add SessionSnapshotPanel UI components (upload, cards, game state display)"
```

---

## Task 16: Wire Snapshot Panel into Session Page

**Files:**
- Modify: The session play page or layout where tools/panels are shown

- [ ] **Step 1: Find the session play page**

Search for the session live/play page component. It's likely at `apps/web/src/app/(authenticated)/sessions/live/[sessionId]/page.tsx` or similar. Add `SessionSnapshotPanel` as a tab or section alongside existing session tools (DiceRoller, Scoreboard, etc.).

- [ ] **Step 2: Import and render**

```tsx
import { SessionSnapshotPanel } from '@/components/session/SessionSnapshotPanel';

// In the session page, add alongside other tools:
<SessionSnapshotPanel
  sessionId={sessionId}
  userId={currentUserId}
  currentTurn={currentTurn}
/>
```

- [ ] **Step 3: Verify build + Commit**

```bash
cd apps/web && pnpm build 2>&1 | tail -10
git add apps/web/src/app/
git commit -m "feat(vision): wire SessionSnapshotPanel into session play page"
```

---

## Task 17: Backend Unit Tests

**Files:**
- Create: Test files in `apps/api/tests/Api.Tests/`

- [ ] **Step 1: Test ContentPart types**

Create `apps/api/tests/Api.Tests/Services/LlmClients/ContentPartTests.cs`:

```csharp
using Api.Services.LlmClients;

namespace Api.Tests.Services.LlmClients;

public class ContentPartTests
{
    [Fact]
    public void TextContentPart_stores_text()
    {
        var part = new TextContentPart("Hello");
        Assert.Equal("Hello", part.Text);
    }

    [Fact]
    public void ImageContentPart_generates_data_uri()
    {
        var part = new ImageContentPart("abc123", "image/jpeg");
        Assert.Equal("data:image/jpeg;base64,abc123", part.ToDataUri());
    }

    [Fact]
    public void LlmMessage_FromText_creates_text_only()
    {
        var msg = LlmMessage.FromText("user", "Hello");
        Assert.Single(msg.Content);
        Assert.IsType<TextContentPart>(msg.Content[0]);
        Assert.False(msg.HasImages);
    }

    [Fact]
    public void LlmMessage_HasImages_true_when_contains_image()
    {
        var msg = new LlmMessage("user", new ContentPart[]
        {
            new ImageContentPart("abc", "image/png"),
            new TextContentPart("Describe this"),
        });
        Assert.True(msg.HasImages);
    }
}
```

- [ ] **Step 2: Test VisionTierLimits**

Create `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/VisionTierLimitsTests.cs`:

```csharp
using Api.BoundedContexts.KnowledgeBase.Domain;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain;

public class VisionTierLimitsTests
{
    [Fact]
    public void Alpha_tier_has_generous_limits()
    {
        var config = VisionTierLimits.GetConfig("alpha");
        Assert.Equal(5, config.MaxImagesPerMessage);
        Assert.Equal(20, config.MaxSnapshotsPerSession);
        Assert.True(config.GameStateExtractionEnabled);
    }

    [Fact]
    public void Free_tier_has_conservative_limits()
    {
        var config = VisionTierLimits.GetConfig("free");
        Assert.Equal(2, config.MaxImagesPerMessage);
        Assert.Equal(5, config.MaxSnapshotsPerSession);
        Assert.False(config.GameStateExtractionEnabled);
    }

    [Fact]
    public void Null_tier_returns_free_defaults()
    {
        var config = VisionTierLimits.GetConfig(null);
        Assert.Equal(2, config.MaxImagesPerMessage);
    }

    [Fact]
    public void Unknown_tier_returns_free_defaults()
    {
        var config = VisionTierLimits.GetConfig("nonexistent");
        Assert.Equal(2, config.MaxImagesPerMessage);
    }
}
```

- [ ] **Step 3: Test SessionSnapshot entity**

Create `apps/api/tests/Api.Tests/BoundedContexts/SessionTracking/Domain/SessionSnapshotTests.cs`:

```csharp
using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.Tests.BoundedContexts.SessionTracking.Domain;

public class SessionSnapshotTests
{
    [Fact]
    public void Create_sets_required_properties()
    {
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var snapshot = SessionSnapshot.Create(sessionId, userId, 3, "Turn 3 state");

        Assert.Equal(sessionId, snapshot.SessionId);
        Assert.Equal(userId, snapshot.UserId);
        Assert.Equal(3, snapshot.TurnNumber);
        Assert.Equal("Turn 3 state", snapshot.Caption);
        Assert.Null(snapshot.ExtractedGameState);
        Assert.Empty(snapshot.Images);
        Assert.False(snapshot.IsDeleted);
    }

    [Fact]
    public void Create_throws_on_empty_session_id()
    {
        Assert.Throws<ArgumentException>(() =>
            SessionSnapshot.Create(Guid.Empty, Guid.NewGuid(), 0, null));
    }

    [Fact]
    public void AddImage_adds_with_correct_order()
    {
        var snapshot = SessionSnapshot.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null);
        snapshot.AddImage("key1", "image/jpeg", 800, 600);
        snapshot.AddImage("key2", "image/png", 1024, 768);

        Assert.Equal(2, snapshot.Images.Count);
        Assert.Equal(0, snapshot.Images[0].OrderIndex);
        Assert.Equal(1, snapshot.Images[1].OrderIndex);
    }

    [Fact]
    public void UpdateGameState_sets_json_and_timestamp()
    {
        var snapshot = SessionSnapshot.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null);
        snapshot.UpdateGameState("{\"confidence\": 0.8}");

        Assert.Equal("{\"confidence\": 0.8}", snapshot.ExtractedGameState);
        Assert.NotNull(snapshot.UpdatedAt);
    }

    [Fact]
    public void SoftDelete_marks_deleted()
    {
        var snapshot = SessionSnapshot.Create(Guid.NewGuid(), Guid.NewGuid(), 1, null);
        snapshot.SoftDelete();

        Assert.True(snapshot.IsDeleted);
        Assert.NotNull(snapshot.DeletedAt);
    }
}
```

- [ ] **Step 4: Test SkiaImagePreprocessor**

Create `apps/api/tests/Api.Tests/Services/ImageProcessing/SkiaImagePreprocessorTests.cs`:

```csharp
using Api.Services.ImageProcessing;
using SkiaSharp;

namespace Api.Tests.Services.ImageProcessing;

public class SkiaImagePreprocessorTests
{
    private readonly SkiaImagePreprocessor _sut = new();

    private static byte[] CreateTestImage(int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Blue);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [Fact]
    public async Task ProcessAsync_resizes_large_image()
    {
        var largeImage = CreateTestImage(4000, 3000);
        var options = new ImageProcessingOptions(MaxWidth: 1024, MaxHeight: 1024);

        var result = await _sut.ProcessAsync(largeImage, "image/jpeg", options);

        Assert.True(result.Width <= 1024);
        Assert.True(result.Height <= 1024);
        Assert.Equal("image/jpeg", result.MediaType);
    }

    [Fact]
    public async Task ProcessAsync_preserves_small_image_dimensions()
    {
        var smallImage = CreateTestImage(640, 480);
        var options = new ImageProcessingOptions(MaxWidth: 1024, MaxHeight: 1024);

        var result = await _sut.ProcessAsync(smallImage, "image/jpeg", options);

        Assert.Equal(640, result.Width);
        Assert.Equal(480, result.Height);
    }

    [Fact]
    public void DetectMediaType_jpeg()
    {
        var jpeg = CreateTestImage(10, 10);
        Assert.Equal("image/jpeg", _sut.DetectMediaType(jpeg));
    }

    [Fact]
    public void DetectMediaType_unknown_returns_null()
    {
        Assert.Null(_sut.DetectMediaType(new byte[] { 0x00, 0x01, 0x02, 0x03 }));
    }
}
```

- [ ] **Step 5: Run tests**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~ContentPart|FullyQualifiedName~VisionTier|FullyQualifiedName~SessionSnapshot|FullyQualifiedName~SkiaImagePreprocessor" --verbosity normal 2>&1 | tail -20`

Expected: All tests pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/tests/Api.Tests/
git commit -m "test(vision): add unit tests for ContentPart, VisionTierLimits, SessionSnapshot, ImagePreprocessor"
```

---

## Task 18: Frontend Tests

**Files:**
- Create: `apps/web/__tests__/hooks/useChatImageAttachments.test.ts`

- [ ] **Step 1: Test useChatImageAttachments hook**

```typescript
// apps/web/__tests__/hooks/useChatImageAttachments.test.ts
import { renderHook, act } from '@testing-library/react';
import { useChatImageAttachments } from '@/hooks/useChatImageAttachments';

// Mock URL.createObjectURL / revokeObjectURL
const mockCreateObjectURL = vi.fn(() => 'blob:mock-url');
const mockRevokeObjectURL = vi.fn();
Object.defineProperty(globalThis, 'URL', {
  value: { createObjectURL: mockCreateObjectURL, revokeObjectURL: mockRevokeObjectURL },
  writable: true,
});

function createMockFile(name: string, type: string, size: number): File {
  const buffer = new ArrayBuffer(size);
  return new File([buffer], name, { type });
}

describe('useChatImageAttachments', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('starts with empty images', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    expect(result.current.images).toHaveLength(0);
    expect(result.current.hasImages).toBe(false);
    expect(result.current.canAddMore).toBe(true);
  });

  it('adds a valid image', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    const file = createMockFile('test.jpg', 'image/jpeg', 1000);

    act(() => {
      const error = result.current.addImage(file);
      expect(error).toBeNull();
    });

    expect(result.current.images).toHaveLength(1);
    expect(result.current.hasImages).toBe(true);
  });

  it('rejects unsupported file types', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    const file = createMockFile('test.pdf', 'application/pdf', 1000);

    act(() => {
      const error = result.current.addImage(file);
      expect(error).toContain('Formato non supportato');
    });

    expect(result.current.images).toHaveLength(0);
  });

  it('rejects oversized files', () => {
    const { result } = renderHook(() => useChatImageAttachments());
    const file = createMockFile('big.jpg', 'image/jpeg', 15 * 1024 * 1024);

    act(() => {
      const error = result.current.addImage(file);
      expect(error).toContain('troppo grande');
    });
  });

  it('respects max images limit', () => {
    const { result } = renderHook(() => useChatImageAttachments(2));

    act(() => {
      result.current.addImage(createMockFile('1.jpg', 'image/jpeg', 100));
      result.current.addImage(createMockFile('2.jpg', 'image/jpeg', 100));
    });

    expect(result.current.canAddMore).toBe(false);
  });

  it('removes an image and revokes URL', () => {
    const { result } = renderHook(() => useChatImageAttachments());

    act(() => {
      result.current.addImage(createMockFile('1.jpg', 'image/jpeg', 100));
    });

    act(() => {
      result.current.removeImage(0);
    });

    expect(result.current.images).toHaveLength(0);
    expect(mockRevokeObjectURL).toHaveBeenCalled();
  });

  it('clearImages removes all and revokes URLs', () => {
    const { result } = renderHook(() => useChatImageAttachments());

    act(() => {
      result.current.addImage(createMockFile('1.jpg', 'image/jpeg', 100));
      result.current.addImage(createMockFile('2.jpg', 'image/jpeg', 100));
    });

    act(() => {
      result.current.clearImages();
    });

    expect(result.current.images).toHaveLength(0);
    expect(mockRevokeObjectURL).toHaveBeenCalledTimes(2);
  });
});
```

- [ ] **Step 2: Run frontend tests**

Run: `cd apps/web && pnpm test -- --run __tests__/hooks/useChatImageAttachments.test.ts 2>&1 | tail -20`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```bash
git add apps/web/__tests__/hooks/useChatImageAttachments.test.ts
git commit -m "test(vision): add frontend tests for useChatImageAttachments hook"
```

---

## Task 19: Final Build Verification + Lint

- [ ] **Step 1: Backend full build**

Run: `cd apps/api/src/Api && dotnet build 2>&1 | tail -5`
Expected: Build succeeded.

- [ ] **Step 2: Frontend full build**

Run: `cd apps/web && pnpm build 2>&1 | tail -10`
Expected: Build succeeded.

- [ ] **Step 3: Frontend lint**

Run: `cd apps/web && pnpm lint 2>&1 | tail -10`
Fix any lint errors.

- [ ] **Step 4: Frontend typecheck**

Run: `cd apps/web && pnpm typecheck 2>&1 | tail -10`
Fix any type errors.

- [ ] **Step 5: Run backend unit tests**

Run: `cd apps/api/src/Api && dotnet test --filter "Category=Unit" --verbosity quiet 2>&1 | tail -10`
Ensure no regressions.

- [ ] **Step 6: Commit any fixes**

```bash
git add -A
git commit -m "fix(vision): resolve lint and type errors from vision AI integration"
```
