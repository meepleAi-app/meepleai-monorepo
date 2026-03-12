# Phase 3: Voice in Chat — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Users can speak to AI agents via a microphone button in the chat input. Speech-to-text via Whisper API (paid users) with Web Speech API fallback (free users). Agent responses read aloud via browser TTS.

**Architecture:** Single new backend endpoint (`POST /api/v1/speech/transcribe`) proxying to OpenAI Whisper. Frontend: mic button in existing chat input, extends existing `ISpeechRecognitionProvider` abstraction in `lib/voice/` with a new `WhisperProvider`, TTS via existing `useVoiceOutput.ts` extended with language auto-detect. Tier-gated: Whisper for `Normal`/`Premium`, Web Speech API for `Free`.

**Tech Stack:** .NET 9 (multipart form), OpenAI Whisper API, Next.js 16, MediaRecorder API, Web Speech API, SpeechSynthesis API

**Spec:** `docs/superpowers/specs/2026-03-11-admin-invite-onboarding-design.md` — Phase 3

**Independent of:** Phases 1 and 2 (voice works for any authenticated user)

**Existing voice infrastructure (DO NOT duplicate):**
- `apps/web/src/hooks/useVoiceInput.ts` — hook with `ISpeechRecognitionProvider` abstraction
- `apps/web/src/hooks/useVoiceOutput.ts` — SpeechSynthesis management
- `apps/web/src/lib/voice/types.ts` — provider types
- `apps/web/src/lib/voice/providers/provider-factory.ts` — provider creation
- `apps/web/src/lib/voice/providers/web-speech-provider.ts` — Web Speech API provider

---

## File Structure

### Backend — New Files
| File | Responsibility |
|------|---------------|
| `KnowledgeBase/Application/Commands/Speech/TranscribeAudioCommand.cs` | Command + handler |
| `KnowledgeBase/Application/Validators/TranscribeAudioCommandValidator.cs` | Validation |
| `KnowledgeBase/Infrastructure/Services/WhisperTranscriptionService.cs` | OpenAI Whisper client |
| `KnowledgeBase/Infrastructure/Services/ITranscriptionService.cs` | Interface |
| `Routing/SpeechEndpoints.cs` | POST /api/v1/speech/transcribe |

### Backend — Modified Files
| File | Change |
|------|--------|
| `KnowledgeBase/Infrastructure/DependencyInjection/KnowledgeBaseServiceExtensions.cs` | Register ITranscriptionService |
| `Program.cs` | Map speech endpoints |

### Frontend — New Files
| File | Responsibility |
|------|---------------|
| `components/chat/VoiceChatButton.tsx` | Mic toggle button with recording state + quality indicator badge |
| `lib/voice/providers/whisper-provider.ts` | Implements existing `ISpeechRecognitionProvider` using MediaRecorder + backend Whisper proxy |
| `components/chat/TextToSpeechButton.tsx` | Speaker button per message |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `lib/voice/providers/provider-factory.ts` | Add tier-based provider selection (`createProvider(userTier)`) |
| `hooks/useVoiceOutput.ts` | Extend with language auto-detection from response content |
| `lib/voice/types.ts` | Add `WhisperProvider` type to provider union |
| `components/ui/meeple/chat-message.tsx` | Add TTS speaker button |
| Chat input component (find exact path) | Add mic button next to send |

### Configuration
| File | Description |
|------|-------------|
| `infra/secrets/speech.secret` | WHISPER_API_KEY, WHISPER_MODEL |

### Tests
| File | Scope |
|------|-------|
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Commands/Speech/TranscribeAudioCommandHandlerTests.cs` | Unit: tier gating, handler |
| `apps/web/src/__tests__/components/chat-unified/VoiceChatButton.test.tsx` | Frontend: mic states, quality badge |
| `apps/web/src/__tests__/components/chat-unified/WhisperProvider.test.ts` | Frontend: provider + fallback logic |
| `apps/web/src/__tests__/components/chat-unified/VoiceOutput.test.ts` | Frontend: TTS + language auto-detect |

---

## Chunk 1: Backend (Whisper proxy endpoint)

### Task 1: Create ITranscriptionService + Whisper implementation

- [ ] **Step 1: Write interface**

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

public interface ITranscriptionService
{
    Task<TranscriptionResult> TranscribeAsync(Stream audioStream, string fileName, CancellationToken ct = default);
}

public record TranscriptionResult(string Text, string Language, double DurationSeconds);
```

- [ ] **Step 2: Write WhisperTranscriptionService**

```csharp
using System.Net.Http.Headers;
using System.Text.Json;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

internal sealed class WhisperTranscriptionService : ITranscriptionService
{
    private readonly HttpClient _httpClient;
    private readonly string _model;
    private readonly ILogger<WhisperTranscriptionService> _logger;

    public WhisperTranscriptionService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<WhisperTranscriptionService> logger)
    {
        _httpClient = httpClientFactory.CreateClient("whisper");
        var apiKey = configuration["WHISPER_API_KEY"]
            ?? throw new InvalidOperationException("WHISPER_API_KEY not configured");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
        _model = configuration["WHISPER_MODEL"] ?? "whisper-1";
        _logger = logger;
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        Stream audioStream, string fileName, CancellationToken ct = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(audioStream), "file", fileName);
        content.Add(new StringContent(_model), "model");
        content.Add(new StringContent("json"), "response_format");

        var response = await _httpClient.PostAsync(
            "https://api.openai.com/v1/audio/transcriptions", content, ct)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        var result = JsonSerializer.Deserialize<WhisperResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        _logger.LogInformation("Transcribed {Length} chars, language: {Lang}",
            result?.Text?.Length ?? 0, result?.Language ?? "unknown");

        return new TranscriptionResult(
            result?.Text ?? string.Empty,
            result?.Language ?? "unknown",
            result?.Duration ?? 0);
    }

    private record WhisperResponse(string Text, string Language, double Duration);
}
```

- [ ] **Step 3: Register in DI + commit**

### Task 2: Create TranscribeAudioCommand with tier gating

- [ ] **Step 1: Write test** — verify Free tier returns 403, Normal/Premium proceeds
- [ ] **Step 2: Write command + handler**

```csharp
internal record TranscribeAudioCommand(
    Stream AudioStream,
    string FileName,
    Guid UserId,
    string UserTier
) : IRequest<TranscriptionResult>;

internal class TranscribeAudioCommandHandler : IRequestHandler<TranscribeAudioCommand, TranscriptionResult>
{
    private readonly ITranscriptionService _transcriptionService;

    public TranscribeAudioCommandHandler(ITranscriptionService transcriptionService)
    {
        _transcriptionService = transcriptionService;
    }

    public async Task<TranscriptionResult> Handle(TranscribeAudioCommand command, CancellationToken ct)
    {
        if (string.Equals(command.UserTier, "free", StringComparison.OrdinalIgnoreCase))
            throw new ForbiddenException("Cloud transcription requires a paid tier. Use browser speech recognition instead.");

        return await _transcriptionService.TranscribeAsync(
            command.AudioStream, command.FileName, ct).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Run tests + commit**

### Task 3: Create SpeechEndpoints

- [ ] **Step 1: Write endpoint** accepting multipart/form-data audio
- [ ] **Step 2: Add rate limiting** (60 requests/hour per user)
- [ ] **Step 3: Register in Program.cs + commit**

### Task 4: Add speech.secret configuration

- [ ] **Step 1: Create `infra/secrets/speech.secret.example`**
- [ ] **Step 2: Update setup-secrets.ps1 to include speech** (optional priority)
- [ ] **Step 3: Commit**

---

## Chunk 2: Frontend (Mic button + STT + TTS)

### Task 5: Create WhisperProvider

> **IMPORTANT:** The codebase already has voice infrastructure at `hooks/useVoiceInput.ts` (with `ISpeechRecognitionProvider` abstraction), `hooks/useVoiceOutput.ts`, and `lib/voice/` (types, provider-factory, web-speech-provider). This task extends it — do NOT duplicate.

- [ ] **Step 1: Write WhisperProvider** at `lib/voice/providers/whisper-provider.ts`

```typescript
import type { ISpeechRecognitionProvider, RecognitionResult } from '../types';

/**
 * Implements ISpeechRecognitionProvider using MediaRecorder to capture audio
 * and the backend Whisper proxy endpoint (POST /api/v1/speech/transcribe)
 * for cloud-based transcription. Used for Normal/Premium tier users.
 */
export class WhisperProvider implements ISpeechRecognitionProvider {
  private mediaRecorder: MediaRecorder | null = null;
  private chunks: Blob[] = [];
  private stream: MediaStream | null = null;

  readonly id = 'whisper' as const;

  async start(onResult: (result: RecognitionResult) => void, onError: (error: Error) => void): Promise<void> {
    try {
      this.stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      this.mediaRecorder = new MediaRecorder(this.stream, { mimeType: 'audio/webm' });
      this.chunks = [];

      this.mediaRecorder.ondataavailable = (e) => {
        if (e.data.size > 0) this.chunks.push(e.data);
      };

      this.mediaRecorder.onstop = async () => {
        const blob = new Blob(this.chunks, { type: 'audio/webm' });
        this.stream?.getTracks().forEach(t => t.stop());
        try {
          const formData = new FormData();
          formData.append('audio', blob, 'recording.webm');
          const res = await fetch('/api/v1/speech/transcribe', { method: 'POST', body: formData });
          if (!res.ok) throw new Error(`Transcription failed: ${res.status}`);
          const data = await res.json();
          onResult({ text: data.text, language: data.language, isFinal: true });
        } catch (err) {
          onError(err instanceof Error ? err : new Error('Transcription failed'));
        }
      };

      this.mediaRecorder.start();
    } catch (err) {
      onError(err instanceof Error ? err : new Error('Permesso microfono necessario'));
    }
  }

  stop(): void {
    if (this.mediaRecorder?.state === 'recording') {
      this.mediaRecorder.stop();
    }
  }

  isSupported(): boolean {
    return typeof navigator !== 'undefined'
      && !!navigator.mediaDevices?.getUserMedia
      && typeof MediaRecorder !== 'undefined';
  }
}
```

- [ ] **Step 2: Add `'whisper'` to provider type union in `lib/voice/types.ts`**
- [ ] **Step 3: Test + commit**

### Task 6: Update provider-factory.ts with tier-based selection

> **Extends existing** `lib/voice/providers/provider-factory.ts` — do NOT create a new `SpeechService`.

- [ ] **Step 1: Add `createProvider(userTier)` function** — returns `WhisperProvider` for `Normal`/`Premium` tiers, `WebSpeechProvider` for `Free` tier
- [ ] **Step 2: Test tier-based selection logic + commit**

### Task 7: Create VoiceChatButton component

- [ ] **Step 1: Write component** — idle (mic icon) → recording (red pulse + timer) → transcribing (spinner)
- [ ] **Step 2: Add quality indicator badge** — display "HD" badge when using WhisperProvider, or browser icon when using WebSpeechProvider (free tier)
- [ ] **Step 3: Add tooltip for free users** — "Trascrizione base — Upgrade per qualita HD"
- [ ] **Step 4: Write test + commit**

### Task 8: Extend useVoiceOutput.ts with language auto-detection

> **Extends existing** `hooks/useVoiceOutput.ts` — do NOT create a new `useTextToSpeech.ts` hook.

- [ ] **Step 1: Add language auto-detection** — detect language from agent response content and set appropriate `SpeechSynthesisUtterance.lang`
- [ ] **Step 2: Write TextToSpeechButton** — speaker icon on agent messages, uses extended `useVoiceOutput`
- [ ] **Step 3: Add auto-read toggle in chat header**
- [ ] **Step 4: Test TTS + language detection + commit**

### Task 9: Integrate into existing chat

- [ ] **Step 1: Read current chat input component** to find exact insertion point
- [ ] **Step 2: Add VoiceChatButton** next to send button
- [ ] **Step 3: Add TextToSpeechButton** to chat-message.tsx
- [ ] **Step 4: Wire transcription result** to input field
- [ ] **Step 5: Full build + test + commit**
