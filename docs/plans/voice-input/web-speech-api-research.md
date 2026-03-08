# Web Speech API Research: Voice-First Board Game Companion

**Research Date**: 2026-03-07
**Confidence Level**: High (85%) -- based on 15+ sources cross-referenced
**Purpose**: Implementation decision support for MeepleAI voice interaction

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [SpeechRecognition API (STT)](#1-speechrecognition-api-stt)
3. [SpeechSynthesis API (TTS)](#2-speechsynthesis-api-tts)
4. [Alternatives to Browser Speech API](#3-alternatives-to-browser-speech-api)
5. [UX Patterns for Voice in Web Apps](#4-ux-patterns-for-voice-in-web-apps)
6. [Real-World Examples](#5-real-world-examples)
7. [Recommendation for MeepleAI](#6-recommendation-for-meepleai)
8. [Sources](#sources)

---

## Executive Summary

The Web Speech API is viable for prototyping but **not production-grade** for a multilingual board game companion. Key findings:

- **Browser support is fragmented**: Only Chrome/Edge have full SpeechRecognition; Firefox has zero support; Safari is partial and unreliable.
- **Audio goes to Google's cloud** by default -- privacy concern. On-device recognition is emerging but requires language pack downloads and is not universally available.
- **Noisy environments** (board game table with multiple talkers) will significantly degrade Web Speech API accuracy. Cloud APIs like Deepgram and AssemblyAI handle this better.
- **Italian support exists** but accuracy is lower than English, and the Web Speech API offers no tuning knobs.
- **Recommended architecture**: Push-to-talk UX + server-side STT (Deepgram or OpenAI gpt-4o-mini-transcribe) + browser SpeechSynthesis for TTS (with cloud TTS fallback for Italian quality).

---

## 1. SpeechRecognition API (STT)

### 1.1 Browser Support Matrix

| Browser          | Desktop | Mobile  | Prefix Required     | Notes                                    |
|------------------|---------|---------|----------------------|------------------------------------------|
| Chrome 33+       | Full    | Full    | `webkitSpeechRecognition` | Best implementation; sends audio to Google servers |
| Edge (Chromium)  | Full    | Full    | `webkitSpeechRecognition` | Same engine as Chrome                    |
| Safari 14.1+     | Partial | Partial | `webkitSpeechRecognition` | Slow responses, transcript duplications, continuous mode broken on iOS |
| Firefox          | None    | None    | N/A                  | No support, no plans to implement        |
| Brave            | None    | None    | N/A                  | Explicitly refuses to implement (privacy) |
| Samsung Internet | None    | None    | N/A                  | Not supported                            |

**Practical impact**: ~70-75% of global browser market is covered (Chrome + Edge). Safari's partial support is unreliable enough to require fallback. Firefox users (~3-6%) are completely excluded.

### 1.2 Continuous vs One-Shot Recognition

| Mode | How It Works | Gotchas |
|------|-------------|---------|
| **One-shot** (`continuous: false`) | Listens for a single utterance, stops after silence detected | Reliable across browsers. Default mode. Good for command-style input. |
| **Continuous** (`continuous: true`) | Keeps listening after each result, accumulates transcript | Broken on iOS Safari (single growing string instead of result list). Chrome creates a forever-growing results array. Can silently stop after ~60 seconds. Must handle `onend` to restart. |

**Key gotcha**: In continuous mode on Chrome, the `results` array grows indefinitely and includes both interim and final results. You must track which results are `isFinal` and manage your own transcript buffer.

**Key gotcha**: Chrome may silently stop continuous recognition after ~60 seconds of inactivity or background tab switching. You need an `onend` handler that auto-restarts.

### 1.3 Language Support (Italian + English)

- **Setting language**: `recognition.lang = 'it-IT'` or `recognition.lang = 'en-US'`
- **Italian is supported** in Chrome's speech recognition backend (Google's cloud service)
- **No runtime language switching**: You must stop and restart recognition to change language
- **No bilingual mode**: Cannot recognize Italian and English simultaneously -- must pick one per session
- **Accuracy**: Italian accuracy is lower than English, particularly for:
  - Technical/gaming terminology
  - Proper nouns (game names, character names)
  - Code-switching (mixing Italian and English in one sentence, common among Italian board gamers)

### 1.4 Accuracy in Noisy Environments

**This is the critical weakness for a board game scenario.**

The Web Speech API provides **zero noise handling configuration**. You cannot:
- Set noise gate thresholds
- Enable noise suppression
- Configure voice activity detection sensitivity
- Perform speaker diarization (separate who is speaking)

In a board game setting (3-6 people talking, dice rolling, card shuffling, ambient music):
- Expect **significant accuracy degradation** (no published benchmarks, but community reports suggest 50-70% accuracy vs 90%+ in quiet environments)
- Cross-talk from other players will be captured as part of the transcript
- No way to isolate the primary speaker

**Contrast with cloud APIs**: Deepgram Nova-3 and AssemblyAI Universal are specifically trained for noisy, multi-speaker environments. AssemblyAI reports 30% diarization improvement in noisy far-field scenarios.

### 1.5 Latency

| Metric | Value | Notes |
|--------|-------|-------|
| Interim results | ~200-500ms | Available with `interimResults: true`; useful for visual feedback |
| Final result after speech ends | ~750ms-1500ms | Chrome waits for silence detection (~750ms threshold) then sends to server |
| Server round-trip | ~300-800ms | Depends on network; audio sent to Google's servers |
| Total end-to-end | ~1-2 seconds | From user stops speaking to final transcript available |
| Safari | ~2-3 seconds | Notably slower than Chrome |

**For a board game Q&A**: 1-2 second latency is acceptable. Users ask a question, wait for the answer. This is not a real-time captioning scenario.

### 1.6 Privacy

**Default behavior (Chrome)**: Audio is streamed to Google's cloud servers for processing. Google's privacy policy applies. Audio may be retained for service improvement.

**On-device recognition (emerging)**:
- Chrome is developing `processLocally` flag for SpeechRecognition
- Requires one-time language pack download per language
- Ensures neither audio nor transcripts leave the device
- Not yet universally available; Italian language pack availability is uncertain
- Spec proposal: https://github.com/WebAudio/web-speech-api/blob/main/explainers/on-device-speech-recognition.md

**GDPR consideration**: Voice is classified as personally identifiable information under GDPR. Sending audio to Google's servers without explicit consent is a compliance risk for EU users (Italy is EU).

### 1.7 Known Limitations and Gotchas Summary

1. **60-second timeout**: Continuous recognition may silently stop after ~60s
2. **No offline support**: Requires internet (audio sent to Google)
3. **No custom vocabulary**: Cannot add board game terms, character names
4. **No confidence tuning**: Cannot set minimum confidence threshold
5. **Safari duplications**: Safari sometimes returns duplicate transcript segments
6. **iOS continuous mode broken**: Results come as single growing string, not array
7. **Tab backgrounding**: Chrome stops recognition when tab loses focus
8. **HTTPS required**: SpeechRecognition only works on HTTPS (or localhost)
9. **Single instance**: Only one SpeechRecognition instance can be active per page
10. **No audio access**: You cannot get the raw audio buffer -- only the transcript

---

## 2. SpeechSynthesis API (TTS)

### 2.1 Browser Support

| Browser | Support | Notes |
|---------|---------|-------|
| Chrome | Full | Good voice selection, system + Google voices |
| Edge | Full | Excellent quality via Microsoft neural voices |
| Safari | Full | Decent quality, macOS/iOS system voices |
| Firefox | Full | System voices only, quality varies by OS |

**TTS has much broader support than STT.** All major browsers support SpeechSynthesis, including Firefox.

### 2.2 Voice Quality

- **System voices** (offline): Vary wildly by OS. Windows SAPI voices are robotic. macOS voices are decent. Android/iOS voices are good.
- **Cloud/neural voices** (Chrome, Edge): Much more natural. Edge's "Microsoft Isabella Online (Natural)" for Italian is high quality.
- **Consistency problem**: Voice availability differs per browser AND per OS. You cannot guarantee a specific voice will be available.

### 2.3 Italian Voice Availability

| Platform | Italian Voice | Quality |
|----------|--------------|---------|
| Chrome Desktop | Google italiano | Decent, slightly robotic |
| Edge Desktop | Microsoft Isabella Online (Natural) | Good, neural quality |
| Safari macOS | Alice (Italian) | Decent |
| iOS Safari | System Italian voices | Good (Apple neural TTS) |
| Android Chrome | Google italiano | Decent |
| Firefox (any OS) | OS system voices only | Varies |

**Best Italian TTS in browser**: Edge with Microsoft Isabella Natural voice.

### 2.4 Interruption Handling (Barge-in)

The `speechSynthesis.cancel()` method stops speech immediately and clears the utterance queue.

**Known issues with barge-in**:
1. **Safari/iOS bug**: Calling `cancel()` triggers the `onerror` event on the utterance being spoken (error type: "canceled"). Your error handler must distinguish cancel from real errors.
2. **Firefox timing bug**: After calling `cancel()`, a new `speak()` call made within ~500ms may be silently swallowed. Workaround: use `setTimeout` of 500-600ms before starting new speech.
3. **Implementing barge-in**: Listen for `SpeechRecognition.onspeechstart` (user started talking) and call `speechSynthesis.cancel()` to stop TTS. Works but requires careful state management to avoid recognition/synthesis fighting each other.

**Recommended pattern**:
```javascript
// When user starts speaking, stop TTS
recognition.onspeechstart = () => {
  if (speechSynthesis.speaking) {
    speechSynthesis.cancel();
    // Wait before allowing new synthesis
    bargeInCooldown = true;
    setTimeout(() => { bargeInCooldown = false; }, 600);
  }
};
```

---

## 3. Alternatives to Browser Speech API

### 3.1 Whisper.js (Client-Side, via Transformers.js)

| Aspect | Details |
|--------|---------|
| **Library** | `@huggingface/transformers` (Transformers.js) |
| **Model** | whisper-base (73M params, ~200MB download) |
| **Execution** | WebAssembly (CPU) or WebGPU (GPU-accelerated) |
| **Languages** | 100 languages including Italian |
| **Offline** | Yes, fully offline after model download |
| **Privacy** | All processing local, no data leaves device |

**Feasibility assessment**:

| Factor | Rating | Notes |
|--------|--------|-------|
| Accuracy | Good | Comparable to cloud Whisper for clean audio; degrades with noise |
| Italian accuracy | Good | Whisper was trained on multilingual data; Italian well-represented |
| Model download | Concern | 200MB initial download; can cache in IndexedDB |
| Inference speed (CPU) | Slow | 5-15 seconds for a 10-second clip on mid-range laptop |
| Inference speed (WebGPU) | Acceptable | ~1-3 seconds for 10-second clip on GPU-capable device |
| Mobile performance | Poor | Most mobile devices lack WebGPU; CPU inference too slow for real-time |
| Memory usage | High | ~500MB-1GB RAM during inference |
| Browser support for WebGPU | Limited | Chrome 113+, Edge 113+; no Firefox/Safari |

**Verdict**: Whisper.js is viable for desktop-only, tech-savvy users who accept the initial download. Not suitable as the primary STT solution for a general-audience mobile-friendly web app. Could serve as an offline fallback.

### 3.2 Cloud STT APIs Comparison

| Provider | Model | WER (English) | WER (Multilingual) | Latency (Streaming) | Cost per Minute | Italian Support | Noisy Audio |
|----------|-------|---------------|---------------------|---------------------|-----------------|-----------------|-------------|
| **OpenAI** | gpt-4o-mini-transcribe | ~2.5% | ~5% | Not streaming | $0.006 | Yes | Good |
| **OpenAI** | Whisper API | ~6.5% | ~7.4% | Not streaming | $0.006 | Yes | Good |
| **Deepgram** | Nova-3 | ~8.1% | ~6.8% | <300ms | $0.0043 | Yes | Best |
| **AssemblyAI** | Universal | ~5.4% | ~5.8% | ~300ms | ~$0.006 | Yes | Very good |
| **Google Cloud** | STT v2 | ~5-7% | ~6-8% | ~300ms | $0.006-0.012 | Yes | Good |

**Key differentiators for board game use case**:

- **Deepgram Nova-3**: Best for real-time streaming with sub-300ms latency. Best noise robustness. Speaker diarization built-in. Lowest cost. **Best fit for live board game scenario.**
- **OpenAI gpt-4o-mini-transcribe**: Highest accuracy overall. Not streaming (batch only -- send full audio clip). Good for post-recording transcription, not live interaction.
- **AssemblyAI Universal**: Best balance of accuracy and features. 300ms streaming. Good speaker diarization (30% improvement in noisy conditions).

### 3.3 When to Use Client-Side vs Server-Side

| Scenario | Recommendation | Rationale |
|----------|---------------|-----------|
| Quick voice command ("roll dice", "next turn") | Client-side (Web Speech API) | Low latency, simple utterance, no need for high accuracy |
| Rules question during game | Server-side (Deepgram/OpenAI) | Need high accuracy for RAG query, noisy environment |
| Offline mode | Client-side (Whisper.js) | No network available |
| Privacy-critical users | Client-side (Whisper.js) | No audio leaves device |
| Mobile users | Server-side | Client-side ML too heavy for mobile |
| Italian language | Server-side (Deepgram/OpenAI) | Better multilingual models |

---

## 4. UX Patterns for Voice in Web Apps

### 4.1 Activation Mode Comparison

| Pattern | How It Works | Pros | Cons | Board Game Fit |
|---------|-------------|------|------|----------------|
| **Push-to-talk** | User holds/taps button to speak | Clear intent signal; no false activations; battery efficient | Requires hand interaction (hands may be holding cards) | BEST -- avoids cross-talk from other players |
| **Tap-to-toggle** | Tap once to start, tap again to stop | Hands-free during speaking; clear on/off state | Still requires initial tap; may forget to stop | GOOD -- compromise between PTT and always-on |
| **Always-listening** | Continuously captures audio | Fully hands-free | Battery drain; privacy concern; picks up all table conversation; high false activation rate | POOR -- too many false activations at game table |
| **Wake word** ("Hey Meeple") | Listens for keyword, then captures query | Hands-free; clear activation; familiar pattern | Wake word detection in browser is hard (requires always-on processing); cross-talk may trigger it | MODERATE -- appealing but technically complex in browser |

**Recommendation for MeepleAI**: **Tap-to-toggle** as primary, with clear visual state indicator. User taps mic button, asks question, system auto-stops after silence detection (~2 seconds). This avoids:
- Picking up other players' conversation
- Battery drain from always-listening
- Complexity of wake word detection
- Awkwardness of holding a button while speaking

### 4.2 Visual Feedback Patterns

**During listening state**:
- Pulsing microphone icon (CSS animation, no JS overhead)
- Waveform visualization (Web Audio API `AnalyserNode` -- gives real-time audio levels)
- Interim transcript display (shows words as they're recognized)
- Color state: neutral -> active (green/blue pulse) -> processing (amber) -> error (red)

**Recommended minimal implementation**:
```
[Idle]     -> Gray mic icon, "Tap to ask"
[Listening] -> Blue pulsing ring around mic, show interim text
[Processing] -> Amber spinner, "Thinking..."
[Speaking]  -> Speaker icon with waves, show response text
[Error]    -> Red mic with X, "Didn't catch that. Tap to try again."
```

### 4.3 Error Handling Patterns

| Error | User Experience | Recovery |
|-------|----------------|----------|
| No speech detected (silence) | "I didn't hear anything. Tap the mic and try again." | Auto-return to idle after 3 seconds |
| Low confidence transcript | Show transcript with "Did you mean: [transcript]?" + confirm/retry buttons | Let user confirm or re-speak |
| Network error | "Can't reach the server. Check your connection." | Offer offline mode if Whisper.js loaded |
| Microphone permission denied | "Microphone access needed. Tap to enable in settings." | Link to browser permission settings |
| Background noise too high | "It's noisy -- try speaking closer to your phone." | Increase noise gate threshold |
| Browser not supported | "Voice not supported in this browser. Try Chrome or Edge." | Fall back to text input |

### 4.4 Mobile Considerations

1. **Microphone permission**: Must be requested via user gesture (tap). Cannot auto-request on page load.
2. **Background behavior**: iOS Safari and Chrome both stop audio capture when app is backgrounded. Recognition will halt.
3. **Screen lock**: Recognition stops when screen locks. Cannot run in background.
4. **Battery impact**: Continuous recognition drains battery. Push-to-talk is significantly more efficient.
5. **Haptic feedback**: Use `navigator.vibrate(50)` on mic activation/deactivation for tactile confirmation.
6. **Proximity**: Mobile users naturally hold phone closer to mouth -- better accuracy than desktop mic.

---

## 5. Real-World Examples

### 5.1 Production Web Apps Using Voice

| App/Platform | Technology | Use Case | Relevance to MeepleAI |
|--------------|-----------|----------|----------------------|
| **Google Search** (voice search) | Web Speech API | One-shot voice queries | Similar: quick question, expect answer |
| **Pipecat** (open-source framework) | Deepgram + various LLMs | Multimodal conversational AI | Architecture reference for voice + AI pipeline |
| **Vapi** (voice AI platform) | Custom STT/TTS | Voice agents for customer service | Shows production patterns for voice + LLM |
| **Speechify** | Multiple STT providers | Reading assistance | Multilingual voice handling patterns |
| **Voiceitt** | Custom models | Accessible speech recognition | Handling non-standard speech patterns |

### 5.2 Voice Pattern for "Quick Question During Board Game"

The ideal interaction flow:

```
1. Player has rules question mid-game
2. Taps mic button on phone (on table or in hand)
3. Speaks: "Can I build a settlement next to another settlement?"
4. Visual: pulsing mic -> interim text -> "Thinking..."
5. System: STT -> RAG query -> LLM response -> TTS
6. Audio response plays: "No, settlements must be at least two
   road segments apart. This is the distance rule in Catan."
7. Response text also shown on screen for reference
8. Auto-returns to idle state
```

**Total expected latency budget**:
- STT: ~1-2 seconds (Web Speech API) or ~300ms (Deepgram streaming)
- RAG + LLM: ~2-4 seconds (depends on model and retrieval)
- TTS: ~200ms to start speaking (browser SpeechSynthesis)
- **Total: ~3-6 seconds from speech end to audio response start**

This is acceptable for a board game context where players continue their turn while waiting.

---

## 6. Recommendation for MeepleAI

### Architecture Decision

```
                    +-------------------+
                    |   User's Browser  |
                    |                   |
                    |  [Mic Button]     |
                    |       |           |
                    |  MediaRecorder    |
                    |  (capture audio)  |
                    |       |           |
                    +-------|----------+
                            |
                   WebSocket/HTTP POST
                            |
                    +-------|----------+
                    |   MeepleAI API   |
                    |                  |
                    |  Deepgram STT    |--- streaming transcript
                    |       |          |
                    |  RAG Pipeline    |
                    |       |          |
                    |  LLM Response    |
                    |       |          |
                    |  Response Text   |--- sent back to browser
                    |                  |
                    +------------------+
                            |
                    +-------|----------+
                    |   User's Browser  |
                    |                   |
                    |  SpeechSynthesis  |--- speaks response
                    |  (browser TTS)    |
                    |                   |
                    +-------------------+
```

### Recommended Stack

| Layer | Technology | Rationale |
|-------|-----------|-----------|
| **Audio Capture** | `MediaRecorder` API | Universal browser support; gives raw audio for server processing |
| **STT (primary)** | Deepgram Nova-3 (server-side) | Best noise handling, lowest latency streaming, Italian support, $0.0043/min |
| **STT (fallback)** | Web Speech API | Zero cost; works for simple commands in quiet environment |
| **STT (offline)** | Whisper.js (optional, desktop only) | Privacy-first users; offline scenarios |
| **TTS (primary)** | Browser SpeechSynthesis | Free; instant; works offline; adequate quality |
| **TTS (Italian quality)** | OpenAI TTS API or Edge neural voices | Better Italian naturalness when quality matters |
| **UX pattern** | Tap-to-toggle with auto-stop | Best fit for board game table scenario |

### Cost Estimate

For a typical board game session (2-3 hours, ~50 voice questions):
- Average question: ~5 seconds of audio
- Total audio: ~250 seconds = ~4.2 minutes
- Deepgram cost: 4.2 * $0.0043 = **$0.018 per session**
- At 1000 sessions/month: **$18/month for STT**

### Implementation Priority

1. **Phase 1 (MVP)**: Tap-to-toggle + Web Speech API (free, Chrome-only, quick to build)
2. **Phase 2 (Production)**: Replace STT with Deepgram streaming via WebSocket through API backend
3. **Phase 3 (Enhancement)**: Add Whisper.js as offline fallback; add speaker diarization for multi-player mode

### Key Risks

| Risk | Severity | Mitigation |
|------|----------|------------|
| Board game table noise | High | Push-to-talk UX; Deepgram noise-robust models; "speak closer" prompt |
| Italian accuracy | Medium | Deepgram/OpenAI have good Italian; add custom vocabulary for game terms |
| Browser fragmentation | Medium | Always provide text input as fallback; progressive enhancement |
| GDPR compliance | High | Use Deepgram (EU data processing available); get explicit consent for audio |
| Mobile battery drain | Low | Tap-to-toggle (not always-on); short recognition sessions |

---

## Sources

- [MDN Web Speech API](https://developer.mozilla.org/en-US/docs/Web/API/Web_Speech_API)
- [MDN SpeechRecognition](https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition)
- [MDN SpeechRecognition: continuous property](https://developer.mozilla.org/en-US/docs/Web/API/SpeechRecognition/continuous)
- [MDN SpeechSynthesis.cancel()](https://developer.mozilla.org/en-US/docs/Web/API/SpeechSynthesis/cancel)
- [Can I Use: Speech Recognition](https://caniuse.com/speech-recognition)
- [On-device Web Speech API Explainer](https://github.com/WebAudio/web-speech-api/blob/main/explainers/on-device-speech-recognition.md)
- [W3C TAG On-device Web Speech API Review](https://github.com/w3ctag/design-reviews/issues/1038)
- [Taming the Web Speech API (Andrea Giammarchi)](https://webreflection.medium.com/taming-the-web-speech-api-ef64f5a245e1)
- [Web Speech API: Complete Guide (VocaFuse)](https://vocafuse.com/blog/web-speech-api-vs-cloud-apis/)
- [AssemblyAI: Speech Recognition in the Browser](https://www.assemblyai.com/blog/speech-recognition-javascript-web-speech-api)
- [AssemblyAI: Offline Whisper in Browser](https://www.assemblyai.com/blog/offline-speech-recognition-whisper-browser-node-js)
- [AssemblyAI Benchmarks](https://www.assemblyai.com/benchmarks)
- [AssemblyAI: Speaker Diarization Update](https://www.assemblyai.com/blog/speaker-diarization-update)
- [Deepgram: Speech-to-Text API Pricing 2025](https://deepgram.com/learn/speech-to-text-api-pricing-breakdown-2025)
- [Deepgram: Best Speech-to-Text APIs 2026](https://deepgram.com/learn/best-speech-to-text-apis-2026)
- [Deepgram: Whisper vs Deepgram](https://deepgram.com/learn/whisper-vs-deepgram)
- [Deepgram: Noise-Robust Speech Recognition](https://deepgram.com/learn/noise-robust-speech-recognition-methods-best-practices)
- [Deepgram: Noise Reduction Paradox](https://deepgram.com/learn/the-noise-reduction-paradox-why-it-may-hurt-speech-to-text-accuracy)
- [Whisper WebGPU (Xenova/Hugging Face)](https://github.com/xenova/whisper-web)
- [Whisper WebGPU Tutorial (dev.to)](https://dev.to/proflead/real-time-audio-to-text-in-your-browser-whisper-webgpu-tutorial-j6d)
- [Northflank: Best Open Source STT 2026](https://northflank.com/blog/best-open-source-speech-to-text-stt-model-in-2026-benchmarks)
- [JavaScript Speech Recognition (VideoSDK)](https://www.videosdk.live/developer-hub/stt/javascript-speech-recognition)
- [HadrienGardeur: Web Speech Recommended Voices](https://github.com/HadrienGardeur/web-speech-recommended-voices)
- [Chrome Developers: Voice Driven Web Apps](https://developer.chrome.com/blog/voice-driven-web-apps-introduction-to-the-web-speech-api)
- [Pipecat: Voice AI Framework](https://github.com/pipecat-ai/pipecat)
- [Lollypop: Voice UI Design Best Practices 2025](https://lollypop.design/blog/2025/august/voice-user-interface-design-best-practices/)
- [IxDF: How to Design Voice User Interfaces](https://www.interaction-design.org/literature/article/how-to-design-voice-user-interfaces)
- [Firefox Bug 1522074: cancel() timing issue](https://bugzilla.mozilla.org/show_bug.cgi?id=1522074)
- [Chromium: Web Speech API 60-second limit](https://groups.google.com/a/chromium.org/g/chromium-html5/c/s2XhT-Y5qAc)
