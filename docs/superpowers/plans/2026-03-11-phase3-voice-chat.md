# Phase 3: Voice in Chat — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Users can speak to AI agents via a microphone button in the chat input. Speech-to-text via Whisper API (paid users) with Web Speech API fallback (free users). Agent responses read aloud via browser TTS.

**Architecture:** Single new backend endpoint (`POST /api/v1/speech/transcribe`) proxying to OpenAI Whisper. Frontend: mic button in existing chat input, `SpeechService` abstraction with fallback, TTS via browser `SpeechSynthesis API`. Tier-gated: Whisper for `Normal`/`Premium`, Web Speech API for `Free`.

**Tech Stack:** .NET 9 (multipart form), OpenAI Whisper API, Next.js 16, MediaRecorder API, Web Speech API, SpeechSynthesis API

**Spec:** `docs/superpowers/specs/2026-03-11-admin-invite-onboarding-design.md` — Phase 3

**Independent of:** Phases 1 and 2 (voice works for any authenticated user)

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
| `components/chat/VoiceChatButton.tsx` | Mic toggle button with recording state |
| `hooks/useAudioRecorder.ts` | MediaRecorder hook |
| `lib/services/SpeechService.ts` | Whisper + Web Speech API abstraction |
| `components/chat/TextToSpeechButton.tsx` | Speaker button per message |
| `hooks/useTextToSpeech.ts` | SpeechSynthesis hook |

### Frontend — Modified Files
| File | Change |
|------|--------|
| `components/ui/meeple/chat-message.tsx` | Add TTS speaker button |
| Chat input component (find exact path) | Add mic button next to send |

### Configuration
| File | Description |
|------|-------------|
| `infra/secrets/speech.secret` | WHISPER_API_KEY, WHISPER_MODEL |

### Tests
| File | Scope |
|------|-------|
| `Api.Tests/KnowledgeBase/Commands/TranscribeAudioCommandTests.cs` | Unit: tier gating, handler |
| `apps/web/__tests__/chat/VoiceChatButton.test.tsx` | Frontend: mic states |
| `apps/web/__tests__/chat/SpeechService.test.ts` | Frontend: fallback logic |

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
) : ICommand<TranscriptionResult>;

internal class TranscribeAudioCommandHandler : ICommandHandler<TranscribeAudioCommand, TranscriptionResult>
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

### Task 5: Create useAudioRecorder hook

- [ ] **Step 1: Write hook**

```typescript
import { useState, useRef, useCallback } from 'react';

interface AudioRecorderState {
  isRecording: boolean;
  duration: number;
  audioBlob: Blob | null;
  error: string | null;
}

export function useAudioRecorder(maxDurationMs = 30000) {
  const [state, setState] = useState<AudioRecorderState>({
    isRecording: false, duration: 0, audioBlob: null, error: null,
  });
  const mediaRecorderRef = useRef<MediaRecorder | null>(null);
  const chunksRef = useRef<Blob[]>([]);
  const timerRef = useRef<NodeJS.Timeout | null>(null);
  const startTimeRef = useRef<number>(0);

  const startRecording = useCallback(async () => {
    try {
      const stream = await navigator.mediaDevices.getUserMedia({ audio: true });
      const mediaRecorder = new MediaRecorder(stream, { mimeType: 'audio/webm' });
      mediaRecorderRef.current = mediaRecorder;
      chunksRef.current = [];
      startTimeRef.current = Date.now();

      mediaRecorder.ondataavailable = (e) => { if (e.data.size > 0) chunksRef.current.push(e.data); };
      mediaRecorder.onstop = () => {
        const blob = new Blob(chunksRef.current, { type: 'audio/webm' });
        setState(prev => ({ ...prev, isRecording: false, audioBlob: blob }));
        stream.getTracks().forEach(t => t.stop());
      };

      mediaRecorder.start();
      setState(prev => ({ ...prev, isRecording: true, error: null, audioBlob: null }));

      // Auto-stop after max duration
      timerRef.current = setInterval(() => {
        const elapsed = Date.now() - startTimeRef.current;
        setState(prev => ({ ...prev, duration: Math.floor(elapsed / 1000) }));
        if (elapsed >= maxDurationMs) stopRecording();
      }, 1000);
    } catch (err) {
      setState(prev => ({ ...prev, error: 'Permesso microfono necessario' }));
    }
  }, [maxDurationMs]);

  const stopRecording = useCallback(() => {
    if (mediaRecorderRef.current?.state === 'recording') {
      mediaRecorderRef.current.stop();
    }
    if (timerRef.current) clearInterval(timerRef.current);
  }, []);

  return { ...state, startRecording, stopRecording };
}
```

- [ ] **Step 2: Test + commit**

### Task 6: Create SpeechService

- [ ] **Step 1: Write service class** with Whisper → Web Speech API fallback
- [ ] **Step 2: Test fallback logic + commit**

### Task 7: Create VoiceChatButton component

- [ ] **Step 1: Write component** — idle (mic icon) → recording (red pulse + timer) → transcribing (spinner)
- [ ] **Step 2: Write test + commit**

### Task 8: Create TTS components

- [ ] **Step 1: Write useTextToSpeech hook** — wraps SpeechSynthesis API
- [ ] **Step 2: Write TextToSpeechButton** — speaker icon on agent messages
- [ ] **Step 3: Add auto-read toggle in chat header**
- [ ] **Step 4: Test + commit**

### Task 9: Integrate into existing chat

- [ ] **Step 1: Read current chat input component** to find exact insertion point
- [ ] **Step 2: Add VoiceChatButton** next to send button
- [ ] **Step 3: Add TextToSpeechButton** to chat-message.tsx
- [ ] **Step 4: Wire transcription result** to input field
- [ ] **Step 5: Full build + test + commit**
