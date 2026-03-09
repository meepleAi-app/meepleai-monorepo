# Voice Input Feature Specification

**Document Version**: 1.0
**Date**: 2026-03-07
**Status**: Draft
**Authors**: Expert Specification Panel (Wiegers, Adzic, Fowler, Nygard, Cockburn, Crispin)
**Prerequisite Research**: `docs/plans/voice-input/web-speech-api-research.md`

---

## 1. Executive Summary

Voice input is MeepleAI's differentiating feature: a board gamer at the table speaks a rule question into their phone and gets an answer in seconds. This is the "TikTok moment" -- the feature that gets filmed and shared.

**Strategic value**: No competitor in the board game companion space offers voice-first Q&A during gameplay. This transforms MeepleAI from "another chat app" into a hands-free game assistant.

**Two-phase delivery**:

| Phase | Technology | Timeline | Browser Support | Cost |
|-------|-----------|----------|-----------------|------|
| **Phase 1 (MVP)** | Web Speech API (browser STT) + SpeechSynthesis (browser TTS) | 2-3 weeks | Chrome/Edge only (~75% market) | $0/month |
| **Phase 2 (Production)** | MediaRecorder + WebSocket + Deepgram Nova-3 (server STT) + SpeechSynthesis (browser TTS) | 4-6 weeks after Phase 1 | All browsers with microphone support (~95% market) | ~$18/month per 1000 sessions |

**Key architectural constraint**: The voice layer is a new input/output channel for the existing chat pipeline. It MUST NOT modify the chat message flow, the SSE streaming architecture, or the RAG pipeline. Voice produces text input; the chat system processes it identically to typed text. The response text is optionally spoken aloud via TTS.

**Integration point**: The existing `ChatThreadView.tsx` component has a pre-defined but unimplemented action `{id: 'voice', icon: 'mic', action: 'chat:voice'}` in `config/actions.ts:482-487`. This specification defines what happens when that action is activated.

---

## 2. User Stories & Use Cases

### 2.1 Primary Actor: Board Game Player

**Actor profile**: A person playing a physical board game at a table with 2-6 other people. Their phone is on the table or in hand. They have a rule question and want an answer without stopping the game to search through the rulebook or type a message.

### 2.2 User Stories

| ID | Story | Priority | Phase |
|----|-------|----------|-------|
| US-V-001 | As a player, I want to ask a rule question by voice so I can keep my hands free for the game. | P0 | 1 |
| US-V-002 | As a player, I want to hear the answer spoken aloud so I don't have to read my phone screen. | P1 | 1 |
| US-V-003 | As a player, I want to interrupt the spoken answer by tapping the screen or speaking again. | P1 | 1 |
| US-V-004 | As a player, I want to see my spoken question as text in the chat so I can verify what was heard. | P0 | 1 |
| US-V-005 | As a player, I want a quick-ask mode where I can voice a question without navigating to a specific chat thread. | P2 | 1 |
| US-V-006 | As a player, I want voice input to work in noisy environments (game table with multiple people). | P0 | 2 |
| US-V-007 | As a player on Firefox/Safari, I want voice input to work even though my browser lacks SpeechRecognition. | P1 | 2 |
| US-V-008 | As a player, I want to choose whether answers are spoken aloud or just shown as text. | P1 | 1 |
| US-V-009 | As a player, I want visual feedback showing that the system is listening to me. | P0 | 1 |
| US-V-010 | As a player, I want to switch between Italian and English for voice input. | P2 | 2 |

### 2.3 Use Case: Ask a Rule Question by Voice During Gameplay

**Primary Actor**: Board Game Player
**Goal**: Get an answer to a rule question without interrupting gameplay flow
**Scope**: MeepleAI chat interface (existing thread or Quick Ask)
**Level**: User goal
**Preconditions**: Player has an active chat thread with an agent linked to a game that has uploaded rulebook(s). Browser supports required APIs. Microphone permission granted.

**Main Success Scenario (MSS)**:

1. Player taps the mic button in the chat input area.
2. System requests microphone permission (first time only; browser remembers).
3. System displays listening state: pulsing ring around mic icon, "Listening..." label.
4. Player speaks their question: "Can I build two roads in one turn?"
5. System displays interim transcript as the player speaks.
6. Player stops speaking. System detects 2 seconds of silence and auto-stops listening.
7. System displays final transcript in the input area and shows "Sending..." state.
8. System sends the transcript text to the existing chat pipeline via `handleSendMessage(transcript)`.
9. User message appears in the chat thread (identical to a typed message).
10. SSE streaming begins; assistant response streams in (existing flow, unchanged).
11. When the complete response is available, system speaks it aloud via SpeechSynthesis.
12. System returns to idle state.

**Extensions**:

| Step | Extension | Handling |
|------|-----------|----------|
| 2a | Permission denied | Show persistent banner: "Microphone access required for voice input. [Enable in Settings]". Fall back to text input. |
| 3a | Browser does not support SpeechRecognition (Phase 1) | Show toast: "Voice input requires Chrome or Edge. Use text input instead." Mic button shows disabled state with tooltip. |
| 3b | Browser does not support SpeechRecognition (Phase 2) | Silently use MediaRecorder + server-side STT path. No user-visible difference. |
| 5a | No speech detected for 5 seconds | Auto-stop. Show: "No speech detected. Tap the mic to try again." Return to idle. |
| 5b | Recognition error (network, audio) | Show error inline: "Couldn't process voice. Try again or type your question." Return to idle. |
| 6a | Player taps mic button again to manually stop | System stops listening immediately, processes what was captured so far. |
| 6b | Transcript is empty after processing | Show: "Didn't catch that. Tap the mic to try again." Return to idle. |
| 8a | Player edits transcript before sending | Player can modify the transcript text in the input field, then tap send. This is the "review before send" option. |
| 11a | Player taps screen during TTS playback | `speechSynthesis.cancel()` called. TTS stops. Response text remains visible. |
| 11b | Player starts speaking during TTS playback (barge-in) | STT `onspeechstart` triggers `speechSynthesis.cancel()`. New listening session begins. |
| 11c | TTS auto-read preference is OFF | Skip step 11. Response is text-only. |

### 2.4 Use Case: Quick Ask Mode

**Primary Actor**: Board Game Player
**Goal**: Ask a voice question without navigating to a specific chat thread
**Scope**: Standalone `/ask` route
**Level**: User goal
**Preconditions**: Player is logged in and has at least one game with an uploaded rulebook.

**Main Success Scenario**:

1. Player navigates to `/ask` (or taps a floating "Quick Ask" button on the dashboard).
2. System shows a minimal UI: large mic button, game selector dropdown, last-used game pre-selected.
3. Player taps the mic button.
4. Steps 3-11 from the primary use case execute.
5. Response is shown in a single-message view (not a full chat thread).
6. System auto-creates a chat thread in the background for history purposes.
7. Player can tap "Continue in Chat" to open the full thread, or ask another question.

---

## 3. Functional Requirements

### 3.1 Voice Input (STT)

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-001 | The system SHALL provide a mic button in the chat input area that activates voice recognition. | Mic button visible next to the text input. Tapping it starts listening. Button uses the existing `chat:voice` action definition from `config/actions.ts`. | 1 |
| FR-V-002 | The system SHALL display interim transcription results in real-time as the user speaks. | Interim text appears in the input textarea within 500ms of speech. Text updates as recognition refines. | 1 |
| FR-V-003 | The system SHALL auto-stop listening after 2 seconds of silence. | Recognition stops automatically. Final transcript is placed in the input field. Silence threshold is configurable (default: 2000ms). | 1 |
| FR-V-004 | The system SHALL allow manual stop by tapping the mic button again. | Tapping mic during listening immediately stops recognition and processes captured audio. | 1 |
| FR-V-005 | The system SHALL auto-send the transcript after recognition completes (configurable). | Default behavior: auto-send after 500ms delay (allowing user to review). User can toggle "Review before send" in voice settings. | 1 |
| FR-V-006 | The system SHALL support Italian (`it-IT`) and English (`en-US`) speech recognition. | Language selection follows the app's current locale. Manual override available in voice settings. | 1 |
| FR-V-007 | The system SHALL display the final transcript as a user message in the chat thread, identical to typed messages. | The message object is `{role: 'user', content: transcript}`. No visual difference from typed messages. Optional "via voice" indicator (small mic icon on message). | 1 |
| FR-V-008 | The system SHALL handle the case where no speech is detected within a timeout period. | If no `speechstart` event fires within 5 seconds, recognition stops. Error message displayed. | 1 |
| FR-V-009 | The system SHALL cancel any in-progress STT when the user navigates away from the chat. | Navigation (route change, tab close) calls `recognition.abort()` and cleans up all audio resources. | 1 |
| FR-V-010 | The system SHALL provide server-side STT via MediaRecorder + WebSocket + Deepgram for browsers without SpeechRecognition support. | MediaRecorder captures audio as `audio/webm;codecs=opus`. Audio chunks sent via WebSocket to `.NET API`. API forwards to Deepgram Nova-3 streaming endpoint. Transcript returned via WebSocket. | 2 |
| FR-V-011 | The system SHALL enforce a maximum voice input duration of 30 seconds per utterance. | After 30 seconds, recognition auto-stops with message: "Maximum recording time reached." | 1 |

### 3.2 Voice Output (TTS)

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-020 | The system SHALL speak the assistant's response aloud using SpeechSynthesis when TTS is enabled. | After SSE streaming completes (Complete event received), the full response text is spoken via `speechSynthesis.speak()`. | 1 |
| FR-V-021 | The system SHALL allow the user to toggle TTS on/off via a speaker icon button. | Toggle persisted in localStorage. Default: ON when voice input was used for the triggering question, OFF when question was typed. | 1 |
| FR-V-022 | The system SHALL select an appropriate Italian voice for TTS. | Voice selection priority: (1) Edge "Isabella Online Natural", (2) Chrome "Google italiano", (3) any voice with `lang.startsWith('it')`. Fallback to default voice if no Italian voice found. | 1 |
| FR-V-023 | The system SHALL support barge-in: user can interrupt TTS by tapping the stop button or starting to speak. | `speechSynthesis.cancel()` called on user interaction. 600ms cooldown before new synthesis (Firefox workaround). | 1 |
| FR-V-024 | The system SHALL NOT speak responses longer than 500 characters. | Responses exceeding 500 characters are truncated for TTS with "... read the full response on screen" appended. Full text always shown in chat. | 1 |
| FR-V-025 | The system SHALL strip citations, markdown formatting, and URLs from text before speaking. | TTS receives plain text only. Citation markers like `[p.15]` removed. Markdown headers, bold, links converted to plain text. | 1 |

### 3.3 Quick Ask Mode

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-030 | The system SHALL provide a `/ask` route with a minimal voice-first interface. | Route renders a centered mic button, game selector, and single response area. No chat history visible. | 1 |
| FR-V-031 | The system SHALL pre-select the user's last-used game in Quick Ask mode. | Game selector defaults to the game from the most recent chat thread. User can change. | 1 |
| FR-V-032 | The system SHALL auto-create a chat thread when a Quick Ask question is sent. | Thread created via existing `api.chat.createThread()`. Thread ID stored for "Continue in Chat" navigation. | 1 |
| FR-V-033 | The system SHALL provide a "Continue in Chat" link after displaying the response. | Link navigates to `/chat/[threadId]` with the full thread context. | 1 |

### 3.4 Voice Settings & Preferences

| ID | Requirement | Acceptance Criteria | Phase |
|----|------------|---------------------|-------|
| FR-V-040 | The system SHALL persist voice preferences in localStorage. | Preferences: `{ ttsEnabled: boolean, autoSend: boolean, voiceLang: string, ttsVoiceURI: string | null }`. Key: `meepleai-voice-prefs`. | 1 |
| FR-V-041 | The system SHALL provide a voice settings panel accessible from the chat input area. | Small gear icon next to mic button opens a popover with TTS toggle, auto-send toggle, language selector. | 1 |

---

## 4. Non-Functional Requirements

| ID | Requirement | Target | Measurement Method |
|----|------------|--------|-------------------|
| NFR-V-001 | Voice recognition latency (speech end to final transcript) | Phase 1: < 2000ms (Chrome). Phase 2: < 800ms (Deepgram streaming). | Performance instrumentation: timestamp at `onspeechend` vs timestamp at final result callback. |
| NFR-V-002 | TTS start latency (response received to first spoken word) | < 500ms | Performance instrumentation: timestamp at SSE Complete event vs `SpeechSynthesisUtterance.onstart`. |
| NFR-V-003 | Total end-to-end latency (speech end to TTS start) | Phase 1: < 8 seconds. Phase 2: < 6 seconds. | Includes STT + RAG pipeline + LLM + TTS init. Measured from `onspeechend` to `utterance.onstart`. |
| NFR-V-004 | Voice feature bundle size | < 15 KB gzipped (Phase 1). < 25 KB gzipped (Phase 2, excluding Deepgram SDK). | Webpack bundle analyzer. |
| NFR-V-005 | Microphone permission grant rate | > 80% of users who tap the mic button | Analytics event: `voice_permission_requested` vs `voice_permission_granted`. |
| NFR-V-006 | Voice input success rate (transcript accepted and sent) | > 70% (Phase 1, quiet). > 85% (Phase 2, any environment). | Analytics: `voice_transcript_sent` / `voice_session_started`. |
| NFR-V-007 | Accessibility: screen reader compatibility | Voice button and all states announced correctly by NVDA, VoiceOver, TalkBack. | Manual testing with each screen reader. |
| NFR-V-008 | Accessibility: keyboard operation | Voice button activable via Enter/Space. Escape cancels listening. | Automated: Playwright keyboard test. |
| NFR-V-009 | Memory usage during voice session | < 50 MB additional memory over baseline chat | Chrome DevTools memory profiling. |
| NFR-V-010 | Battery impact on mobile | Voice session (30 questions) uses < 5% battery on mid-range Android phone | Manual testing with Android battery stats. |
| NFR-V-011 | GDPR compliance (Phase 1) | Consent banner shown before first microphone use explaining that audio is sent to Google servers. | Manual verification. |
| NFR-V-012 | GDPR compliance (Phase 2) | Deepgram processes audio via EU endpoint. No audio persisted beyond transcription. | Infrastructure configuration audit. |
| NFR-V-013 | Concurrent voice sessions | System handles 100 simultaneous WebSocket STT connections per API instance. | Load test with k6/Artillery. |

---

## 5. Architecture & Design

### 5.1 Phase 1: Browser-Native MVP

```
User's Browser
+---------------------------------------------------------------+
|                                                               |
|  ChatThreadView.tsx                                           |
|  +----------------------------------------------------------+|
|  |  Existing chat messages + SSE streaming (UNCHANGED)       ||
|  +----------------------------------------------------------+|
|  |  Chat Input Area                                          ||
|  |  +------------------------------------------------------+||
|  |  |  [textarea]  [VoiceMicButton]  [SendButton]          |||
|  |  +------------------------------------------------------+||
|  +----------------------------------------------------------+|
|                                                               |
|  useVoiceInput() hook                                        |
|  +----------------------------------------------------------+|
|  |  ISpeechRecognitionProvider  (abstraction layer)          ||
|  |  +------------------------------------------------------+||
|  |  |  WebSpeechProvider (Phase 1)                          |||
|  |  |  - wraps window.webkitSpeechRecognition               |||
|  |  |  - handles interim/final results                      |||
|  |  |  - manages silence detection                          |||
|  |  +------------------------------------------------------+||
|  +----------------------------------------------------------+|
|                                                               |
|  useVoiceOutput() hook                                       |
|  +----------------------------------------------------------+|
|  |  - wraps window.speechSynthesis                           ||
|  |  - manages voice selection                                ||
|  |  - handles barge-in via cancel()                          ||
|  +----------------------------------------------------------+|
|                                                               |
+---------------------------------------------------------------+
           |                                    |
           | handleSendMessage(transcript)      | (existing SSE flow)
           v                                    v
    Existing Chat Pipeline               Existing RAG Pipeline
    (ZERO CHANGES)                       (ZERO CHANGES)
```

**Key principle**: Phase 1 adds a new input mechanism (voice-to-text) and a new output mechanism (text-to-speech) around the EXISTING chat flow. The transcript string produced by voice input enters the same `handleSendMessage()` function that typed text uses. The response text that triggers TTS is the same `answer` string from the `onComplete` SSE callback.

### 5.2 Phase 2: Server-Side Production

```
User's Browser                          MeepleAI .NET API
+-------------------------------+       +-------------------------------+
|                               |       |                               |
|  MediaRecorder                |       |  VoiceController              |
|  (audio/webm;codecs=opus)     |       |  /api/v1/voice/transcribe     |
|       |                       |       |       |                       |
|       | audio chunks          |       |       | audio stream          |
|       v                       |       |       v                       |
|  WebSocket client      -------|------>|  WebSocket handler            |
|                               |       |       |                       |
|       ^                       |       |       v                       |
|       | transcript events     |       |  Deepgram Nova-3              |
|       |                <------|-------|  (streaming STT)              |
|       |                       |       |       |                       |
|  useVoiceInput() hook         |       |  Returns transcript           |
|  ServerSttProvider            |       |                               |
|  (replaces WebSpeechProvider) |       +-------------------------------+
|                               |
+-------------------------------+
```

**WebSocket message protocol**:

```typescript
// Client -> Server
type VoiceClientMessage =
  | { type: 'audio_chunk'; data: ArrayBuffer }     // Raw audio data
  | { type: 'end_of_speech' }                       // User stopped speaking
  | { type: 'cancel' };                             // User cancelled

// Server -> Client
type VoiceServerMessage =
  | { type: 'transcript_interim'; text: string }    // Partial result
  | { type: 'transcript_final'; text: string; confidence: number }
  | { type: 'error'; code: string; message: string }
  | { type: 'ready' };                              // Server ready to receive
```

**Backend endpoint** (CQRS pattern per project conventions):

```csharp
// Command
public record TranscribeAudioCommand(
    Stream AudioStream,
    string Language,      // "it-IT" or "en-US"
    Guid UserId
) : IRequest<TranscriptionResult>;

// Handler
public class TranscribeAudioHandler : IRequestHandler<TranscribeAudioCommand, TranscriptionResult>
{
    private readonly ITranscriptionService _transcription;
    // Deepgram SDK integration
}

// Endpoint (WebSocket upgrade handled at middleware level)
// POST /api/v1/voice/transcribe (for non-streaming fallback)
// WS   /api/v1/voice/stream     (for streaming)
```

### 5.3 Component Interfaces

#### 5.3.1 Provider Abstraction (enables Phase 1 -> Phase 2 swap)

```typescript
// File: src/lib/voice/types.ts

/** Recognition state machine */
export type VoiceRecognitionState =
  | 'idle'           // Not listening, ready to start
  | 'requesting'     // Requesting microphone permission
  | 'listening'      // Actively listening for speech
  | 'processing'     // Speech ended, waiting for final transcript
  | 'error';         // Error occurred, showing error state

/** Events emitted by any STT provider */
export interface SpeechRecognitionEvents {
  onInterimResult: (text: string) => void;
  onFinalResult: (text: string, confidence: number) => void;
  onSpeechStart: () => void;
  onSpeechEnd: () => void;
  onError: (error: VoiceError) => void;
  onStateChange: (state: VoiceRecognitionState) => void;
}

/** Error types for voice operations */
export interface VoiceError {
  code: VoiceErrorCode;
  message: string;
  recoverable: boolean;
}

export type VoiceErrorCode =
  | 'permission_denied'        // User denied mic access
  | 'not_supported'            // Browser doesn't support required APIs
  | 'no_speech'                // No speech detected within timeout
  | 'audio_capture_error'      // Microphone hardware error
  | 'network_error'            // Network unavailable (Web Speech API)
  | 'server_error'             // Server-side STT failed (Phase 2)
  | 'aborted'                  // Recognition was programmatically cancelled
  | 'language_not_supported';  // Requested language unavailable

/** Configuration for STT providers */
export interface SpeechRecognitionConfig {
  language: string;                  // BCP-47 tag: 'it-IT', 'en-US'
  interimResults: boolean;           // Show partial results (default: true)
  silenceTimeoutMs: number;          // Auto-stop after silence (default: 2000)
  maxDurationMs: number;             // Max recording length (default: 30000)
  noSpeechTimeoutMs: number;         // Error if no speech detected (default: 5000)
}

/** Provider interface -- implemented by WebSpeechProvider (Phase 1)
 *  and ServerSttProvider (Phase 2) */
export interface ISpeechRecognitionProvider {
  readonly isSupported: boolean;
  readonly state: VoiceRecognitionState;
  start(config: SpeechRecognitionConfig, events: SpeechRecognitionEvents): void;
  stop(): void;    // Graceful stop -- process captured audio
  abort(): void;   // Immediate cancel -- discard everything
  dispose(): void; // Clean up all resources
}
```

#### 5.3.2 Voice Input Hook

```typescript
// File: src/hooks/useVoiceInput.ts

export interface UseVoiceInputOptions {
  language?: string;           // Default: app locale
  autoSend?: boolean;          // Auto-send transcript (default: true)
  silenceTimeoutMs?: number;   // Default: 2000
  onTranscript?: (text: string, confidence: number) => void;
  onError?: (error: VoiceError) => void;
}

export interface UseVoiceInputReturn {
  /** Current recognition state */
  state: VoiceRecognitionState;
  /** Interim transcript (updates as user speaks) */
  interimText: string;
  /** Final transcript (set when recognition completes) */
  finalText: string;
  /** Start listening */
  startListening: () => void;
  /** Stop listening (process what was captured) */
  stopListening: () => void;
  /** Cancel listening (discard) */
  cancelListening: () => void;
  /** Whether voice input is supported in current browser */
  isSupported: boolean;
  /** Current error, if any */
  error: VoiceError | null;
  /** Clear error state */
  clearError: () => void;
}

export function useVoiceInput(options?: UseVoiceInputOptions): UseVoiceInputReturn;
```

#### 5.3.3 Voice Output Hook

```typescript
// File: src/hooks/useVoiceOutput.ts

export interface UseVoiceOutputOptions {
  language?: string;              // Default: app locale
  preferredVoiceURI?: string;     // Specific voice to use
  maxTextLength?: number;         // Truncate for TTS (default: 500)
  rate?: number;                  // Speech rate 0.5-2.0 (default: 1.0)
}

export interface UseVoiceOutputReturn {
  /** Speak text aloud */
  speak: (text: string) => void;
  /** Stop speaking */
  stop: () => void;
  /** Whether currently speaking */
  isSpeaking: boolean;
  /** Whether TTS is supported */
  isSupported: boolean;
  /** Available voices for current language */
  availableVoices: SpeechSynthesisVoice[];
  /** Currently selected voice */
  selectedVoice: SpeechSynthesisVoice | null;
  /** Set preferred voice */
  setVoice: (voiceURI: string) => void;
}

export function useVoiceOutput(options?: UseVoiceOutputOptions): UseVoiceOutputReturn;
```

#### 5.3.4 Voice Preferences Store

```typescript
// File: src/store/voice/store.ts

export interface VoicePreferences {
  /** TTS enabled (speak responses aloud) */
  ttsEnabled: boolean;
  /** Auto-send transcript without review */
  autoSend: boolean;
  /** Voice input language (BCP-47) */
  voiceLang: string;
  /** Preferred TTS voice URI */
  ttsVoiceURI: string | null;
  /** TTS speech rate */
  ttsRate: number;
}

export interface VoicePreferencesState extends VoicePreferences {
  setTtsEnabled: (enabled: boolean) => void;
  setAutoSend: (autoSend: boolean) => void;
  setVoiceLang: (lang: string) => void;
  setTtsVoiceURI: (uri: string | null) => void;
  setTtsRate: (rate: number) => void;
}

// Zustand store with persist middleware (same pattern as useChatInfoStore)
// Key: 'meepleai-voice-prefs'
```

#### 5.3.5 React Components

```typescript
// File: src/components/chat-unified/VoiceMicButton.tsx
export interface VoiceMicButtonProps {
  state: VoiceRecognitionState;
  onTap: () => void;
  disabled?: boolean;
  className?: string;
}

// File: src/components/chat-unified/VoiceSettingsPopover.tsx
export interface VoiceSettingsPopoverProps {
  preferences: VoicePreferences;
  onPreferencesChange: (prefs: Partial<VoicePreferences>) => void;
  availableVoices: SpeechSynthesisVoice[];
}

// File: src/components/chat-unified/VoiceTranscriptOverlay.tsx
// Renders the interim/final transcript above the input area
export interface VoiceTranscriptOverlayProps {
  interimText: string;
  finalText: string;
  state: VoiceRecognitionState;
  onEdit: (text: string) => void;
  onSend: () => void;
  onCancel: () => void;
}

// File: src/components/chat-unified/TtsSpeakerButton.tsx
// Toggle button shown on assistant messages when TTS is available
export interface TtsSpeakerButtonProps {
  isSpeaking: boolean;
  onToggle: () => void;
}
```

### 5.4 Quick Ask Mode (`/ask` route)

```typescript
// File: src/app/(authenticated)/ask/page.tsx

// Minimal page component:
// - Large centered VoiceMicButton (80x80px)
// - Game selector dropdown (pre-populated with user's games)
// - Single response card (shows latest Q&A pair)
// - "Continue in Chat" link (navigates to /chat/[threadId])
// - "Ask Another" button (resets to listening state)

// Layout: no sidebar, no chat history, no info panel.
// Full-screen mobile optimized. Designed for one-hand operation.
```

**Quick Ask page layout (mobile-first)**:

```
+----------------------------------+
|  [< Back]     Quick Ask     [?]  |
+----------------------------------+
|                                  |
|     Select Game                  |
|     [Catan: Base Game      v]    |
|                                  |
|                                  |
|        +------------------+      |
|        |                  |      |
|        |    [MIC ICON]    |      |  80x80px button
|        |                  |      |  Pulsing ring when listening
|        +------------------+      |
|                                  |
|     "Tap to ask a question"      |
|                                  |
+----------------------------------+
|  [Response card - hidden until   |
|   first response received]       |
|                                  |
|  Q: "Can I build two roads..."   |
|  A: "Yes, in Catan you can..."   |
|                                  |
|  [Continue in Chat]  [Ask Again] |
+----------------------------------+
```

### 5.5 File Structure

```
src/
  lib/
    voice/
      types.ts                        # Interfaces from 5.3.1
      providers/
        web-speech-provider.ts         # Phase 1: browser SpeechRecognition
        server-stt-provider.ts         # Phase 2: MediaRecorder + WebSocket
        provider-factory.ts            # Selects provider based on environment
      utils/
        text-sanitizer.ts              # Strip markdown/citations for TTS
        voice-detection.ts             # Browser capability detection
        voice-analytics.ts             # Analytics events
  hooks/
    useVoiceInput.ts                   # STT hook (5.3.2)
    useVoiceOutput.ts                  # TTS hook (5.3.3)
  store/
    voice/
      store.ts                         # Zustand preferences store (5.3.4)
  components/
    chat-unified/
      VoiceMicButton.tsx               # Mic button component
      VoiceSettingsPopover.tsx          # Settings popover
      VoiceTranscriptOverlay.tsx        # Interim/final text display
      TtsSpeakerButton.tsx             # TTS toggle on messages
  app/
    (authenticated)/
      ask/
        page.tsx                       # Quick Ask page
```

### 5.6 Integration with ChatThreadView.tsx

The voice feature integrates into `ChatThreadView.tsx` with MINIMAL changes to the existing component. The integration points are:

1. **Import hooks**: `useVoiceInput`, `useVoiceOutput`, `useVoicePreferencesStore`
2. **Input area modification**: Add `VoiceMicButton` next to the send button (lines 640-665 of current component)
3. **Transcript overlay**: Render `VoiceTranscriptOverlay` above the input area when `state !== 'idle'`
4. **TTS trigger**: In the existing `onComplete` callback (lines 91-101), add: `if (ttsEnabled && lastMessageWasVoice) speak(answer)`
5. **Message indicator**: Add optional mic icon to user messages that originated from voice

**Lines of code modified in ChatThreadView.tsx**: Approximately 40-60 lines added. Zero existing lines deleted or restructured.

---

## 6. UX Specification

### 6.1 Voice Button States

The `VoiceMicButton` has 5 visual states mapped to `VoiceRecognitionState`:

| State | Icon | Color | Animation | Label (sr-only) | data-testid |
|-------|------|-------|-----------|-----------------|-------------|
| `idle` | Mic outline | `text-muted-foreground` | None | "Start voice input" | `voice-mic-idle` |
| `requesting` | Mic outline | `text-amber-500` | Slow pulse (opacity) | "Requesting microphone access" | `voice-mic-requesting` |
| `listening` | Mic filled | `text-white` on `bg-red-500` | Pulsing ring (scale 1.0->1.3, 1.5s infinite) | "Listening. Tap to stop." | `voice-mic-listening` |
| `processing` | Spinner | `text-amber-500` | Spin animation | "Processing speech" | `voice-mic-processing` |
| `error` | Mic with X | `text-red-500` | Brief shake (200ms) | "Voice error. Tap to retry." | `voice-mic-error` |
| `disabled` | Mic outline | `text-muted-foreground/50` | None | "Voice input not available in this browser" | `voice-mic-disabled` |

**CSS classes for the listening pulse ring**:

```css
/* Tailwind classes on the mic button wrapper */
.voice-listening-ring {
  @apply absolute inset-0 rounded-full bg-red-500/20;
  animation: voice-pulse 1.5s ease-in-out infinite;
}

@keyframes voice-pulse {
  0%, 100% { transform: scale(1); opacity: 0.6; }
  50% { transform: scale(1.3); opacity: 0; }
}
```

### 6.2 Visual Feedback

**Transcript overlay** (appears above input area when listening/processing):

```
+--------------------------------------------------+
|  "Can I build two roads..."  (interim text)       |  text-sm text-muted-foreground italic
|  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~                     |  animated underline on active words
+--------------------------------------------------+
|  [Cancel]                              [Send >]   |  only shown if autoSend is OFF
+--------------------------------------------------+
```

**Transcript overlay styling**:
- Background: `bg-amber-50/80 dark:bg-amber-950/40 backdrop-blur-md`
- Border: `border border-amber-200/50 dark:border-amber-800/50`
- Corner radius: `rounded-xl`
- Padding: `px-4 py-3`
- Position: Absolute, anchored above the input area, max-width matches input area

**TTS speaker button** (on assistant messages):

- Small speaker icon (16x16) at the bottom-right of assistant message bubbles
- Only visible when TTS is supported AND `ttsEnabled` is true
- States: idle (outline), speaking (filled with sound waves animation), unavailable (hidden)

### 6.3 Mobile Considerations

| Concern | Solution |
|---------|----------|
| **Large tap target** | Mic button minimum 48x48px touch target (WCAG 2.5.8). In Quick Ask: 80x80px. |
| **One-hand operation** | Mic button positioned at bottom-right of input area. Quick Ask has centered large button reachable by thumb. |
| **Haptic feedback** | `navigator.vibrate(50)` on mic activation. `navigator.vibrate([30, 50, 30])` on recognition complete. Uses existing `src/lib/haptics.ts`. |
| **Orientation** | Works in both portrait and landscape. Quick Ask is portrait-optimized. |
| **Screen lock** | Recognition stops when screen locks. Warn user if screen lock timeout is < 30 seconds. |
| **Camera capture attr** | Photo upload uses `capture="environment"`. Voice does NOT use file input. Uses `getUserMedia` directly. |
| **iOS Safari** | Phase 1: Show "Use Chrome for voice" message. Phase 2: MediaRecorder path works. SpeechSynthesis works. |
| **PWA** | Voice works in standalone PWA mode. Microphone permission persists. |

### 6.4 Accessibility

| Requirement | Implementation |
|-------------|---------------|
| **Screen reader announcement** | Mic button has `aria-label` that changes with state. State changes announced via `aria-live="polite"` region. |
| **Keyboard operation** | Mic button focusable. Enter/Space activates. Escape cancels listening. Tab moves to next element. |
| **Reduced motion** | Pulse animation disabled when `prefers-reduced-motion: reduce`. Replace with static color change (red background). |
| **High contrast** | Mic button states use sufficient color contrast. Red listening state: WCAG AA contrast ratio > 4.5:1. |
| **ARIA states** | `aria-pressed="true"` when listening. `aria-busy="true"` when processing. `aria-invalid="true"` on error. |
| **Transcript overlay** | `role="status"` with `aria-live="polite"` for interim results. `aria-live="assertive"` for final results. |
| **TTS and screen readers** | When TTS is active AND screen reader detected, prefer screen reader. Set `ttsEnabled: false` automatically. Detect via `navigator.userAgent` heuristics or `matchMedia('(prefers-reduced-motion)')` as proxy. |
| **Focus management** | After voice error, focus returns to mic button. After transcript sent, focus returns to input field. |

---

## 7. Scenarios (Gherkin)

### 7.1 Happy Path: Voice Question in Chat

```gherkin
Feature: Voice input in chat thread

  Background:
    Given I am logged in as a player
    And I have a chat thread for "Catan" with agent "Rules Expert"
    And my browser supports SpeechRecognition
    And I have granted microphone permission

  Scenario: Ask a question by voice and hear the answer
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    Then the mic button should show "listening" state
    And a pulsing red ring should appear around the button
    And the transcript overlay should appear above the input area
    When I speak "Can I build a settlement next to another settlement?"
    Then the interim transcript should show "Can I build a settlement..."
    And the transcript should update in real-time as I speak
    When I stop speaking for 2 seconds
    Then the mic button should show "processing" state
    And the final transcript "Can I build a settlement next to another settlement?" should appear in the input area
    And after 500ms the transcript should be auto-sent
    And a user message "Can I build a settlement next to another settlement?" should appear in the chat
    When the SSE streaming completes with a response
    Then the assistant message should appear in the chat
    And the response should be spoken aloud via TTS
    And the mic button should return to "idle" state

  Scenario: Manually stop listening
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And the mic button shows "listening" state
    And I speak "What happens when"
    When I tap the voice mic button again
    Then the mic button should show "processing" state
    And the system should process "What happens when" as the transcript
    And the transcript should appear in the input area

  Scenario: Edit transcript before sending (autoSend OFF)
    Given my voice preference "autoSend" is set to false
    And I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And I speak "What is the longest road rule?"
    And I stop speaking for 2 seconds
    Then the transcript "What is the longest road rule?" should appear in the input area
    And the transcript should NOT be auto-sent
    And the input field should be editable
    When I modify the input to "What is the Longest Road bonus?"
    And I tap the send button
    Then a user message "What is the Longest Road bonus?" should appear in the chat

  Scenario: Barge-in during TTS playback
    Given TTS is enabled
    And the assistant is currently speaking a response aloud
    When I tap the voice mic button
    Then TTS should stop immediately
    And the mic button should show "listening" state
    And I should be able to ask a new question
```

### 7.2 Error and Edge Cases

```gherkin
  Scenario: No speech detected
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And I do not speak for 5 seconds
    Then the system should auto-stop listening
    And an error message "No speech detected. Tap the mic to try again." should appear
    And the mic button should return to "idle" state after 3 seconds

  Scenario: Microphone permission denied
    Given I have NOT granted microphone permission
    And I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    Then the system should request microphone permission via the browser
    When I deny microphone permission
    Then an error message "Microphone access needed for voice input." should appear
    And a link "Enable in browser settings" should be shown
    And the mic button should show "disabled" state

  Scenario: Browser does not support SpeechRecognition (Phase 1)
    Given I am using Firefox
    And SpeechRecognition is not available
    When the chat page loads
    Then the mic button should show "disabled" state
    And the mic button tooltip should say "Voice input requires Chrome or Edge"
    When I tap the mic button
    Then nothing should happen

  Scenario: Recognition error (network failure)
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And the SpeechRecognition service encounters a network error
    Then an error message "Voice recognition failed. Check your connection or type your question." should appear
    And the mic button should show "error" state
    And the input field should remain functional for typed input

  Scenario: Maximum duration reached
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And I speak continuously for 30 seconds
    Then the system should auto-stop listening
    And a message "Maximum recording time reached." should appear
    And the captured transcript should be processed normally

  Scenario: Navigate away during listening
    Given I am on the chat thread page "/chat/abc-123"
    And the mic button shows "listening" state
    When I navigate to "/chat/new"
    Then voice recognition should be cancelled
    And all audio resources should be released
    And no error should be shown on the new page

  Scenario: SSE streaming in progress when voice activated
    Given the assistant is currently streaming a response
    When I tap the voice mic button
    Then the SSE streaming should continue uninterrupted
    And the mic button should show "listening" state
    And I should be able to queue my next question via voice

  Scenario: Empty transcript after processing
    Given I am on the chat thread page "/chat/abc-123"
    When I tap the voice mic button
    And background noise is captured but no intelligible speech
    And the recognition returns an empty or whitespace-only transcript
    Then an error message "Didn't catch that. Tap the mic to try again." should appear
    And the transcript should NOT be sent to the chat
    And the mic button should return to "idle" state
```

### 7.3 Quick Ask Mode

```gherkin
Feature: Quick Ask voice-first interface

  Background:
    Given I am logged in as a player
    And I have a game "Catan" with an uploaded rulebook
    And my browser supports SpeechRecognition

  Scenario: Quick Ask happy path
    When I navigate to "/ask"
    Then I should see a large mic button in the center
    And I should see a game selector with "Catan" pre-selected
    And the label should say "Tap to ask a question"
    When I tap the large mic button
    And I speak "How many victory points do I need to win?"
    And I stop speaking for 2 seconds
    Then the question should be sent to the Catan agent
    And a response card should appear with the question and answer
    And a "Continue in Chat" button should appear
    And an "Ask Another" button should appear
    When I tap "Continue in Chat"
    Then I should be navigated to the full chat thread page

  Scenario: Quick Ask with different game
    When I navigate to "/ask"
    And I change the game selector to "Ticket to Ride"
    And I tap the mic button
    And I speak "How do I claim a route?"
    Then the question should be sent to the Ticket to Ride agent
```

### 7.4 TTS Scenarios

```gherkin
Feature: Text-to-Speech response output

  Scenario: TTS auto-reads response after voice question
    Given TTS is enabled
    And I asked a question using voice input
    When the SSE streaming completes with response "You need 10 victory points to win in Catan."
    Then the response should be spoken aloud
    And the text should also appear as a chat message
    And a small speaker icon should pulse on the message during speech

  Scenario: TTS does NOT auto-read after typed question
    Given TTS is enabled
    And I typed a question using the keyboard
    When the SSE streaming completes with a response
    Then the response should NOT be spoken aloud
    And the speaker icon should show as idle (not auto-activated)

  Scenario: TTS truncates long responses
    Given TTS is enabled
    And I asked a question using voice input
    When the SSE streaming completes with a response of 800 characters
    Then only the first 500 characters should be spoken
    And the spoken text should end with "read the full response on screen"
    And the full 800-character response should appear in the chat

  Scenario: TTS strips formatting
    Given TTS is enabled
    And the response contains "**Important:** You must [see page 15] follow the rules"
    Then the spoken text should be "Important: You must follow the rules"
    And citations and markdown should be removed

  Scenario: TTS respects user preference
    Given TTS is disabled in voice settings
    And I asked a question using voice input
    When the SSE streaming completes with a response
    Then the response should NOT be spoken aloud

  Scenario: Stop TTS with screen tap
    Given TTS is currently speaking a response
    When I tap the speaker icon on the message
    Then TTS should stop immediately
    And the speaker icon should return to idle state
```

---

## 8. Failure Modes & Graceful Degradation

### 8.1 Failure Mode Catalog

| # | Failure | Detection | Impact | Severity | Recovery Strategy |
|---|---------|-----------|--------|----------|-------------------|
| FM-1 | Browser lacks SpeechRecognition (Firefox, Brave) | `!('webkitSpeechRecognition' in window)` at component mount | Voice input unavailable | Medium | Phase 1: Mic button disabled with tooltip. Phase 2: Auto-switch to server STT. Text input always available. |
| FM-2 | Microphone permission denied | `PermissionError` from `getUserMedia` or `recognition.onerror` with `error: 'not-allowed'` | Voice input blocked | High | Show persistent inline message with link to browser settings. Never auto-request again (browser blocks). |
| FM-3 | Microphone hardware failure | `recognition.onerror` with `error: 'audio-capture'` | Voice input fails | Medium | Show "Microphone not available" error. Suggest checking system audio settings. Fall back to text. |
| FM-4 | Network failure during recognition (Phase 1) | `recognition.onerror` with `error: 'network'` | Transcript not received | Medium | Show "Network error" message. Offer retry. Audio already sent to Google and lost. |
| FM-5 | SpeechRecognition silently stops (60s Chrome bug) | `recognition.onend` fires without preceding `onerror` or valid result | Recognition ends unexpectedly | Low | Auto-restart recognition if `state === 'listening'` and no final result received. Max 3 restarts per session. |
| FM-6 | WebSocket connection failure (Phase 2) | WebSocket `onerror` or `onclose` with abnormal code | Server STT unavailable | High | Circuit breaker: after 3 failures in 60 seconds, fall back to Web Speech API (if available) or text input. Show banner. |
| FM-7 | Deepgram API unavailable (Phase 2) | HTTP 503 or timeout from Deepgram | Server STT unavailable | High | Backend returns error via WebSocket. Frontend falls back to Web Speech API or text input. Circuit breaker in .NET API with 30-second half-open check. |
| FM-8 | TTS voice not available | `speechSynthesis.getVoices()` returns empty or no Italian voice | Response not spoken | Low | Use default voice. If no voices at all, disable TTS silently. Log analytics event. |
| FM-9 | TTS fails to speak | `utterance.onerror` fires | Response not spoken | Low | Log error. Response text is always visible in chat. TTS failure is non-critical. |
| FM-10 | Tab backgrounded during recognition | Chrome stops recognition when tab loses focus | Recognition interrupted | Low | `document.onvisibilitychange` handler: if `hidden`, stop recognition gracefully with message "Voice paused -- tap mic to resume." |
| FM-11 | Multiple rapid mic taps (debounce) | User taps mic button multiple times quickly | State confusion | Low | Debounce: ignore taps within 300ms of last state change. |
| FM-12 | Audio context blocked by browser autoplay policy | `AudioContext` creation fails without user gesture | Voice features broken | Medium | All audio initialization gated behind user gesture (mic button tap). Never auto-start audio context on page load. |

### 8.2 Degradation Hierarchy

```
Full Voice Experience (STT + TTS)
    |
    | STT fails
    v
Text Input + TTS (type question, hear answer)
    |
    | TTS fails
    v
Text Input + Text Output (standard chat -- always available)
```

**Principle**: Text input and text output are ALWAYS available. Voice is a progressive enhancement. The chat system never depends on voice. A complete failure of all voice features leaves the user with the exact same experience they have today.

### 8.3 Circuit Breaker Configuration (Phase 2)

```typescript
// WebSocket STT circuit breaker (in useVoiceInput or provider)
const CIRCUIT_BREAKER_CONFIG = {
  failureThreshold: 3,       // Open circuit after 3 failures
  resetTimeoutMs: 30_000,    // Try half-open after 30 seconds
  monitorWindowMs: 60_000,   // Failures counted within this window
  fallbackProvider: 'web-speech',  // Fall back to browser STT
};
```

```csharp
// .NET API Deepgram circuit breaker (in DI registration)
services.AddHttpClient<IDeepgramClient>()
    .AddPolicyHandler(Policy
        .Handle<HttpRequestException>()
        .Or<TimeoutException>()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 3,
            durationOfBreak: TimeSpan.FromSeconds(30)
        ));
```

### 8.4 Timeouts

| Operation | Timeout | On Timeout |
|-----------|---------|------------|
| Microphone permission request | 30 seconds (browser-controlled) | Browser shows "blocked" state |
| No speech detected | 5 seconds | Auto-stop + "No speech detected" message |
| Silence after speech (auto-stop) | 2 seconds | Stop recognition, process transcript |
| Max recording duration | 30 seconds | Auto-stop + "Max time reached" message |
| WebSocket connection (Phase 2) | 5 seconds | Fall back to Web Speech API or text |
| Deepgram transcription (Phase 2) | 10 seconds | Return error via WebSocket |
| TTS utterance start | 3 seconds | Log warning, skip TTS for this response |

---

## 9. Test Strategy

### 9.1 Testing Pyramid

```
                    /\
                   /  \
                  / E2E \        3-5 Playwright tests (manual trigger, not CI)
                 /--------\
                /Integration\    10-15 tests (hooks with mocked providers)
               /--------------\
              /   Unit Tests    \  30-40 tests (providers, utils, store, components)
             /--------------------\
```

### 9.2 Unit Tests (Automated, CI)

| Component | Test Count | Testing Approach |
|-----------|-----------|------------------|
| `WebSpeechProvider` | 8-10 | Mock `window.webkitSpeechRecognition`. Test state transitions, event dispatching, error handling, timeout enforcement. |
| `text-sanitizer.ts` | 5-7 | Pure function tests. Input markdown/citations -> output plain text. |
| `voice-detection.ts` | 3-4 | Mock `window` properties. Test browser capability detection. |
| `VoiceMicButton` | 5-6 | Render with each state. Verify correct icon, color, animation class, aria attributes. Snapshot tests. |
| `VoiceTranscriptOverlay` | 4-5 | Render with interim/final text. Test edit, send, cancel callbacks. |
| `VoiceSettingsPopover` | 3-4 | Render with preferences. Test toggle callbacks. |
| `TtsSpeakerButton` | 2-3 | Render speaking/idle states. Test toggle callback. |
| `voice/store.ts` | 3-4 | Test Zustand store: preference changes, persistence, defaults. |
| `useVoiceOutput` | 4-5 | Mock `window.speechSynthesis`. Test speak, stop, voice selection, truncation. |

**Mocking pattern for SpeechRecognition** (following project convention with `vi.hoisted`):

```typescript
// In test file
const mockRecognition = vi.hoisted(() => ({
  start: vi.fn(),
  stop: vi.fn(),
  abort: vi.fn(),
  addEventListener: vi.fn(),
  removeEventListener: vi.fn(),
  continuous: false,
  interimResults: false,
  lang: '',
  onresult: null as ((event: unknown) => void) | null,
  onerror: null as ((event: unknown) => void) | null,
  onend: null as (() => void) | null,
  onspeechstart: null as (() => void) | null,
  onspeechend: null as (() => void) | null,
}));

vi.mock('@/lib/voice/providers/web-speech-provider', () => ({
  // ... or mock at window level:
}));

beforeEach(() => {
  Object.defineProperty(window, 'webkitSpeechRecognition', {
    value: vi.fn(() => mockRecognition),
    writable: true,
  });
});
```

### 9.3 Integration Tests (Automated, CI)

| Test | What It Validates |
|------|-------------------|
| `useVoiceInput` + `WebSpeechProvider` | Full hook lifecycle: start -> interim -> final -> auto-send. With mocked `SpeechRecognition`. |
| `useVoiceInput` + error handling | All 8 error codes produce correct state and user message. |
| `useVoiceInput` + `useVoiceOutput` | Barge-in: starting voice input cancels active TTS. |
| `ChatThreadView` + voice integration | Mic button renders, tap triggers listening, transcript flows to `handleSendMessage`. Requires shallow render of `ChatThreadView` with mocked hooks. |
| `VoicePreferencesStore` + localStorage | Preferences persist across store recreations. |
| Quick Ask page | Game selector populated, mic button functional, response card renders after mock response. |

### 9.4 E2E Tests (Manual Trigger, NOT in CI)

Voice E2E tests cannot run in standard CI because:
- `SpeechRecognition` is not available in headless Chrome
- Microphone input cannot be simulated without OS-level audio injection
- TTS output cannot be verified without audio capture

**E2E test approach**:

```typescript
// File: src/__tests__/e2e/voice-input.spec.ts
// Tagged: @manual @voice
// Run: pnpm test:e2e -- --grep @voice (developer machine only)

test.describe('Voice Input E2E @manual @voice', () => {
  test('mic button visible and states work', async ({ page }) => {
    // Navigate to chat thread
    // Verify mic button renders with correct initial state
    // This CAN run in CI - tests DOM, not actual audio
    await page.goto('/chat/test-thread-id');
    await expect(page.getByTestId('voice-mic-idle')).toBeVisible();
  });

  test.skip('full voice flow with real microphone', async () => {
    // Manual test only -- requires real browser with mic access
    // Tester speaks into microphone, verifies transcript appears
  });
});
```

**CI-safe E2E tests** (test DOM/UI, not audio):
1. Mic button renders on chat page
2. Mic button shows disabled state when SpeechRecognition unavailable (mock `window`)
3. Voice settings popover opens and preferences are toggleable
4. Quick Ask page renders with game selector and mic button
5. Transcript overlay appears when state is simulated to 'listening'

### 9.5 Testing Voice in CI/CD

Since actual voice recognition cannot be tested in CI, the strategy is:

1. **Provider interface testing**: `ISpeechRecognitionProvider` implementations are tested with mocks at the unit level. The interface contract guarantees interchangeability.
2. **Event simulation**: Integration tests fire synthetic events (onresult, onerror, onend) on the mocked SpeechRecognition object to verify the full data flow.
3. **Visual regression**: Screenshot tests for mic button states (idle, listening, processing, error) using Playwright's screenshot comparison.
4. **Contract testing (Phase 2)**: The WebSocket protocol between frontend and backend is tested with a mock WebSocket server that sends predefined `VoiceServerMessage` sequences.

### 9.6 Accessibility Testing

| Test | Method | Tool |
|------|--------|------|
| Screen reader announcement of mic states | Manual | NVDA (Windows), VoiceOver (Mac/iOS) |
| Keyboard operation | Automated | Playwright: Tab to mic, Enter to activate, Escape to cancel |
| Color contrast of all states | Automated | axe-core via `@axe-core/playwright` |
| Focus management after voice operations | Automated | Playwright: verify `document.activeElement` after each state change |
| Reduced motion preference | Automated | Playwright: set `prefers-reduced-motion: reduce`, verify no animation classes |

---

## 10. Migration Path (Phase 1 to Phase 2)

### 10.1 What Changes

| Layer | Phase 1 | Phase 2 | Change Type |
|-------|---------|---------|-------------|
| **STT Provider** | `WebSpeechProvider` | `ServerSttProvider` | **Swap** (same interface) |
| **Provider Factory** | Returns `WebSpeechProvider` | Returns `ServerSttProvider` (prefers) or `WebSpeechProvider` (fallback) | **Modify** |
| **useVoiceInput hook** | Unchanged | Unchanged | **None** |
| **useVoiceOutput hook** | Unchanged | Unchanged | **None** |
| **UI Components** | Unchanged | Unchanged | **None** |
| **Chat integration** | Unchanged | Unchanged | **None** |
| **Backend** | None | New WebSocket endpoint + Deepgram integration | **New** |
| **Infrastructure** | None | Deepgram API key secret | **New** |

### 10.2 Provider Factory Logic (Phase 2)

```typescript
// File: src/lib/voice/providers/provider-factory.ts

export function createSpeechRecognitionProvider(): ISpeechRecognitionProvider {
  // Phase 2: prefer server-side STT
  if (isServerSttEnabled()) {
    return new ServerSttProvider({
      websocketUrl: `${getWsBaseUrl()}/api/v1/voice/stream`,
      fallbackProvider: createWebSpeechProviderIfAvailable(),
    });
  }

  // Phase 1 / Phase 2 fallback: browser-native STT
  if ('webkitSpeechRecognition' in window || 'SpeechRecognition' in window) {
    return new WebSpeechProvider();
  }

  // No STT available
  return new NullSpeechProvider(); // isSupported: false, all methods no-op
}

function isServerSttEnabled(): boolean {
  return process.env.NEXT_PUBLIC_VOICE_SERVER_STT === 'true';
}
```

### 10.3 Migration Checklist

```
Phase 2 migration steps:
[ ] 1. Implement ServerSttProvider (src/lib/voice/providers/server-stt-provider.ts)
[ ] 2. Implement .NET WebSocket endpoint (VoiceController.cs)
[ ] 3. Integrate Deepgram SDK in .NET API (ITranscriptionService)
[ ] 4. Add circuit breaker for Deepgram (Polly policy)
[ ] 5. Add deepgram.secret to infra/secrets/
[ ] 6. Update provider-factory.ts to prefer ServerSttProvider
[ ] 7. Add NEXT_PUBLIC_VOICE_SERVER_STT=true to environment
[ ] 8. Add WebSocket connection tests
[ ] 9. Load test WebSocket endpoint (100 concurrent connections)
[ ] 10. Update GDPR consent text (no longer sending to Google)
[ ] 11. Monitor Deepgram costs for first 30 days
```

### 10.4 Feature Flag

```bash
# .env.local
NEXT_PUBLIC_VOICE_ENABLED=true          # Master toggle for voice feature
NEXT_PUBLIC_VOICE_SERVER_STT=false      # Phase 1: false (browser STT), Phase 2: true (server STT)
```

The `VoiceMicButton` checks `NEXT_PUBLIC_VOICE_ENABLED`. If `false`, the mic button is not rendered at all (not even in disabled state). This allows a clean rollback.

---

## 11. Cost Analysis

### 11.1 Phase 1 Costs

| Item | Cost | Notes |
|------|------|-------|
| Development | ~2-3 weeks (1 developer) | Hooks, components, integration, tests |
| Infrastructure | $0/month | Browser-native APIs, no server costs |
| STT | $0/month | Web Speech API is free (Google pays) |
| TTS | $0/month | Browser SpeechSynthesis is free |
| **Total Phase 1** | **$0/month recurring** | One-time development cost only |

### 11.2 Phase 2 Costs

| Item | Monthly Cost | Calculation |
|------|-------------|-------------|
| Deepgram Nova-3 STT | $18 at 1K sessions | 1000 sessions x 50 questions x 5 sec = 4167 min x $0.0043 |
| Deepgram Nova-3 STT | $180 at 10K sessions | Linear scaling |
| WebSocket infrastructure | $0 (included) | .NET API already runs; WebSocket is a connection upgrade |
| Development | ~4-6 weeks (1 developer) | Server provider, .NET endpoint, Deepgram integration, load testing |
| **Total Phase 2** | **~$18-180/month** | Scales linearly with usage |

### 11.3 Cost Scaling Table

| Monthly Sessions | Voice Questions | Audio Minutes | Deepgram Cost | Per-Session Cost |
|-----------------|----------------|---------------|---------------|------------------|
| 100 | 5,000 | 417 | $1.79 | $0.018 |
| 1,000 | 50,000 | 4,167 | $17.92 | $0.018 |
| 10,000 | 500,000 | 41,667 | $179.17 | $0.018 |
| 100,000 | 5,000,000 | 416,667 | $1,791.67 | $0.018 |

**Break-even analysis**: If voice input increases user retention by even 5%, the $18/month cost at 1K sessions is negligible compared to customer lifetime value. At 100K sessions ($1,792/month), this should be funded by premium tier subscriptions.

### 11.4 Cost Controls

| Control | Implementation |
|---------|---------------|
| Per-user daily limit | Max 100 voice questions per user per day (configurable). After limit: "Voice limit reached. Type your question or try again tomorrow." |
| Audio duration limit | Max 30 seconds per utterance. Prevents accidental long recordings. |
| Rate limiting | Existing 10 req/min rate limiter on chat proxy applies to voice-originated messages too. |
| Free tier restriction | Consider limiting free tier to 20 voice questions/day, unlimited for premium. |
| Monitoring | Dashboard showing daily audio minutes processed, cost, per-user breakdown. Alert at 80% of monthly budget. |

---

## 12. Open Questions & Risks

### 12.1 Open Questions

| # | Question | Impact | Decision Needed By | Proposed Default |
|---|----------|--------|-------------------|------------------|
| OQ-1 | Should voice messages be visually distinguished from typed messages (small mic icon)? | UX polish | Phase 1 development start | Yes, subtle mic icon on voice-originated user messages |
| OQ-2 | Should Quick Ask (`/ask`) be available to non-authenticated users as a demo? | Growth/conversion | Phase 1 development start | No -- requires game + rulebook context which needs auth |
| OQ-3 | Should TTS read the response in the same language the question was asked, or always in the user's locale? | UX edge case | Phase 1 | Match the response language (AI responds in same language as question) |
| OQ-4 | Should we add a "voice" message type to the backend ChatMessage model? | Data model | Before Phase 1 backend work | No -- voice produces text identical to typed. Add `inputMethod: 'voice' \| 'text'` metadata if analytics needed. |
| OQ-5 | Should Phase 2 WebSocket go through the Next.js API proxy or directly to the .NET API? | Architecture | Before Phase 2 | Through Next.js proxy (same CORS/auth pattern as existing SSE) |
| OQ-6 | Should we support voice input in the sidebar game detail panel (`AgentChatPanel`)? | Scope | After Phase 1 | Defer -- chat thread view first, sidebar later |
| OQ-7 | Maximum TTS text length (currently proposed: 500 chars). Is this the right cutoff? | UX | Phase 1 testing | 500 chars. Validate with real rulebook answers during testing. |
| OQ-8 | Should wake word detection ("Hey Meeple") be on the roadmap? | Future feature | Post Phase 2 | Not now -- technically complex in browser, high false positive risk at game table |

### 12.2 Risk Register

| # | Risk | Probability | Impact | Mitigation | Contingency |
|---|------|------------|--------|------------|-------------|
| R-1 | Board game table noise makes Phase 1 voice unusable | High | High | Clear UX guidance ("speak close to phone"). Phase 2 with Deepgram has much better noise handling. | Accept reduced accuracy. "Didn't catch that" retry loop. Always available text fallback. |
| R-2 | Italian game terminology misrecognized | Medium | Medium | Phase 2: Deepgram custom vocabulary/keywords feature. Phase 1: show transcript for user verification. | Auto-send OFF for Italian locale. User reviews and corrects transcript. |
| R-3 | Safari iOS breaks SpeechRecognition mid-session | High | Medium | Phase 1: Do not support Safari. Show "Use Chrome" message. Phase 2: server STT works on all browsers. | Safari users use text input. No degraded voice experience. |
| R-4 | Google deprecates or restricts free SpeechRecognition API | Low | High | Phase 2 eliminates dependency. | Accelerate Phase 2 timeline. |
| R-5 | Deepgram pricing increases significantly | Low | Medium | Contract/pricing tier lock. Budget alerts at 80%. | Switch to AssemblyAI or OpenAI Whisper API (similar pricing, easy swap behind `ITranscriptionService`). |
| R-6 | User privacy concerns about microphone access | Medium | Medium | Clear permission dialog. GDPR consent. No audio persisted. "Voice is optional" messaging. | Respect denial gracefully. Never nag for permission. |
| R-7 | Voice feature distracts from core product quality | Low | High | Ship Phase 1 as MVP behind feature flag. Validate with 10 beta users before general release. | Disable via feature flag if quality issues. |
| R-8 | WebSocket scaling issues under load (Phase 2) | Medium | High | Load test at 2x expected peak. Horizontal scaling of .NET API already in place. | Circuit breaker falls back to Web Speech API. |

### 12.3 Out of Scope (Explicitly Excluded)

- Multi-speaker diarization ("Who asked that question?")
- Voice commands for non-chat actions (dice rolling, timer, scoring)
- Offline voice recognition (Whisper.js) -- deferred to Phase 3+
- Real-time voice translation (ask in Italian, get answer in English)
- Voice-based game state updates ("I just built a city")
- Custom wake word detection
- Audio recording and playback of questions
- Voice output via cloud TTS (e.g., ElevenLabs, OpenAI TTS)

---

## Appendix A: Analytics Events

| Event | Trigger | Properties |
|-------|---------|------------|
| `voice_feature_loaded` | Voice components mount | `{ supported: boolean, phase: 1\|2, browser: string }` |
| `voice_permission_requested` | First mic button tap | `{ browser: string }` |
| `voice_permission_granted` | User grants mic access | `{ browser: string }` |
| `voice_permission_denied` | User denies mic access | `{ browser: string }` |
| `voice_session_started` | Mic button tapped, listening begins | `{ language: string, threadId: string }` |
| `voice_transcript_received` | Final transcript returned | `{ charCount: number, confidence: number, durationMs: number }` |
| `voice_transcript_sent` | Transcript submitted to chat | `{ charCount: number, wasEdited: boolean, autoSend: boolean }` |
| `voice_error` | Any voice error | `{ errorCode: VoiceErrorCode, phase: 1\|2 }` |
| `voice_tts_started` | TTS begins speaking | `{ charCount: number, language: string }` |
| `voice_tts_interrupted` | User interrupts TTS | `{ method: 'button'\|'barge_in', spokenPercentage: number }` |
| `voice_quick_ask_used` | Quick Ask page interaction | `{ gameId: string, continuedInChat: boolean }` |

## Appendix B: Glossary

| Term | Definition |
|------|-----------|
| STT | Speech-to-Text. Converting spoken audio to text transcript. |
| TTS | Text-to-Speech. Converting text to spoken audio. |
| Barge-in | User interrupts the system while it is speaking (TTS). |
| Interim result | Partial transcript that updates in real-time as user speaks. Not final. |
| Final result | Definitive transcript after speech recognition processing completes. |
| Silence detection | System detects that the user has stopped speaking (no audio energy above threshold). |
| Provider | An implementation of `ISpeechRecognitionProvider` that performs STT via a specific technology. |
| Circuit breaker | Pattern that detects repeated failures and temporarily stops attempting the failing operation. |
| Web Speech API | W3C browser API for speech recognition and synthesis. Chrome-only for recognition. |
| Deepgram Nova-3 | Cloud-based STT service with streaming support and noise robustness. |
| Quick Ask | Standalone `/ask` route with minimal UI for one-shot voice questions. |
