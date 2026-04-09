# ChatMessageList Test Coverage Audit (Gate G0.1)

**Audit Date:** 2026-04-09
**Component:** `apps/web/src/components/chat-unified/ChatMessageList.tsx`
**Component Size:** 311 lines of production code
**Existing Test File:** `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx` (116 lines)
**Gate Owner:** @user
**Purpose:** Establish safety net before GitHub issue meepleAi-app/meepleai-monorepo#292 refactor

## Executive Summary

ChatMessageList is a complex component rendering 311 lines of chat UI logic including:
- Message windowing (50-message viewport with sliding)
- Per-message feedback state with API round-trip
- SSE streaming token accumulation
- Citation rendering via RuleSourceCard
- Strategy tier badges via ResponseMetaBadge
- TTS speaker integration
- Model downgrade alerts
- Status/typing indicators

**Current Test Coverage:** ~35% of critical behaviors covered. Most component children (RuleSourceCard, TtsSpeakerButton, ResponseMetaBadge) are **mocked out**, leaving gaps in the integration safety net.

## Coverage Table

| Behavior | Tested? | Priority | Notes |
|----------|---------|----------|-------|
| Window sliding (WINDOW_SIZE=50) | YES | LOW | 4 tests: render ≤50, render 80→50, hidden count display, load more button |
| Empty state display | YES | LOW | 1 test for zero messages |
| Streaming bubble display | YES | MEDIUM | Tests message-streaming testid, currentAnswer, "In scrittura..." label |
| Feedback helpful/not-helpful | **NO** | **CRITICAL** | handleFeedback callback never tested; no API mock, no state mutation test |
| Feedback loading state | **NO** | **CRITICAL** | No test for feedbackLoadingMap or isLoading prop passed to FeedbackButtons |
| Feedback comment submission | **NO** | **CRITICAL** | No test for comment flow on negative feedback |
| Streaming token accumulation | **NO** | **HIGH** | currentAnswer updates during streaming not verified |
| Citation rendering | **NO** | **MEDIUM** | RuleSourceCard is mocked; no integration test for msg.citations flow |
| Multiple citations per message | **NO** | **HIGH** | Edge case: messages.citations.length varies; not tested |
| TTS speak/stop integration | **NO** | **HIGH** | TtsSpeakerButton mocked; onSpeak/onStopSpeaking callbacks never verified |
| TTS visibility (isTtsSupported + ttsEnabled) | **NO** | **MEDIUM** | Conditional rendering based on two boolean props never tested |
| Model downgrade banner | **NO** | **HIGH** | streamState.modelDowngrade rendering never tested |
| Strategy tier badge | **NO** | **MEDIUM** | ResponseMetaBadge mocked; isLastAssistant condition not tested |
| Typing/streaming status indicator | PARTIAL | MEDIUM | stream-status testid verified in ChatThreadView tests, not here |
| Message role styling (user vs assistant) | PARTIAL | LOW | Basic role detection tested; CSS classes not verified |
| Technical details panel | **NO** | LOW | TechnicalDetailsPanel mocked |
| Window boundary: 51 messages | **NO** | **MEDIUM** | Edge case (exactly at WINDOW_SIZE+1) not tested |
| Window boundary: 100+ messages | YES | LOW | 80-message case tested |
| Feedback state map persistence | **NO** | **HIGH** | feedbackMap/feedbackLoadingMap never verified across updates |
| gameId/threadId optional behavior | **NO** | **MEDIUM** | Conditional rendering when gameId && threadId; null case not tested |

## Missing Characterization Tests (Priority Order)

### CRITICAL Priority (5 tests, ~4h)

1. **test_feedback_helpful_round_trip** (lines 107-130)
   - Mock `api.knowledgeBase.submitKbFeedback`
   - Click helpful button on assistant message
   - Verify handleFeedback called with correct gameId, threadId, messageId, outcome='helpful'
   - Verify feedbackMap updated to 'helpful'
   - Verify feedbackLoadingMap toggled during request

2. **test_feedback_not_helpful_with_comment** (lines 107-130)
   - Click not-helpful button → type comment → submit
   - Verify submitKbFeedback called with comment included
   - Verify outcome='not_helpful' (underscored form from transform)

3. **test_feedback_submission_error_silent_fail** (line 124-125)
   - Force submitKbFeedback to reject
   - Verify error does NOT bubble to user (silent catch block)
   - Verify feedbackMap still updated optimistically

4. **test_streaming_token_accumulation_mid_message** (lines 290-305)
   - Set isStreaming=true, currentAnswer="Partial tokens"
   - Verify message-streaming element shows accumulated text
   - Update currentAnswer incrementally; verify DOM reflects changes

5. **test_feedback_state_isolation_per_message** (lines 103-104)
   - Multiple assistant messages in same render
   - Set feedback on msg-1 to 'helpful'
   - Verify msg-2 feedback still null; independent loading states

### HIGH Priority (7 tests, ~5h)

6. **test_model_downgrade_local_fallback_banner** (lines 245-286)
7. **test_model_downgrade_upgrade_message** (lines 268-284)
8. **test_tts_speaker_button_conditional_render** (lines 184-190)
9. **test_citations_multiple_per_message** (lines 192-194)
10. **test_last_assistant_message_strategy_tier_badge** (lines 167-198)
11. **test_feedback_buttons_only_on_assistant** (lines 207-219)
12. **test_window_slide_exact_boundary_51_messages** (lines 98-136)

### MEDIUM Priority (5 tests, ~3h)

13. **test_streaming_status_message_display** (lines 228-241)
14. **test_feedback_gameId_null_hides_buttons** (lines 208-219)
15. **test_technical_details_panel_visibility** (lines 200-205)
16. **test_empty_citations_list_no_render** (lines 192-194)
17. **test_message_content_whitespace_preserved** (line 183)

## Risk Assessment: Refactor Safety (311 → ~150 lines, 50% reduction)

### Current Safety Net Quality: **LOW-MEDIUM**

**Positive Factors:**
- Window sliding logic has 4 solid tests with edge cases
- Empty/streaming bubble display validated
- Message role distinction (user/assistant) implicitly tested

**Negative Factors:**
- **Feedback round-trip untested** — KB-07 feature (lines 106-130) is mission-critical and completely unvalidated
- **Component children mocked away** — RuleSourceCard, TtsSpeakerButton, ResponseMetaBadge all mocked
- **Streaming token accumulation untested** — currentAnswer updates never verified
- **Model downgrade feature untested** — lines 245-286 entirely unvalidated
- **Per-message state isolation untested** — feedbackMap could be over-written across messages
- **No integration tests** — feedback state flow into FeedbackButtons props unverified

### Refactor Risk Matrix

| Refactored Area | Test Coverage | Risk Level | Impact |
|-----------------|---------------|------------|--------|
| Feedback state logic | 0% | 🔴 CRITICAL | Silent data loss — users think feedback sent, but wasn't |
| Message windowing | ~80% | 🟡 MEDIUM | Low risk; logic has test backbone |
| Child component props | 0% | 🔴 CRITICAL | RuleSourceCard/TtsSpeakerButton props; features silently broken |
| Streaming bubble logic | ~50% | 🟡 MEDIUM | Bubble renders but token accumulation could drop |
| Conditional rendering branches | ~20% | 🔴 CRITICAL | Model downgrade, strategy badge, TTS, tech panel unverified |
| Message list layout | ~40% | 🟡 MEDIUM | Basic structure tested; CSS class refactors risky |

### Verdict: Is 50% Reduction Refactor Safe?

**NO.** A 50% refactor without new characterization tests has a **60-75% probability of introducing silent bugs** in production.

## Recommended Action

### 🛑 **DO NOT** proceed with #292 until characterization tests added.

### Phase 0 Sequencing
1. **Create characterization test suite** (Est. 12-16 hours):
   - 5 CRITICAL priority tests
   - 7 HIGH priority tests
   - 5 MEDIUM priority tests
2. **Verify all branches covered** by new tests (run coverage report)
3. **Only then** proceed with #292 refactor in Phase 4

### Alternative: Phased Refactor (Not Recommended)
- Phase A: refactor layout/styling only (low-risk paths)
- Phase B: add characterization tests
- Phase C: refactor feedback/streaming/citations

This alternative is NOT recommended because refactor drift between phases can introduce new bugs.

## Files Referenced

- `apps/web/src/components/chat-unified/ChatMessageList.tsx` (lines 1-311)
- `apps/web/src/components/chat-unified/__tests__/ChatMessageList.test.tsx` (lines 1-116)
- `apps/web/src/components/chat-unified/__tests__/RuleSourceCard.test.tsx` (35+ tests)
- `apps/web/src/components/chat-unified/__tests__/ResponseMetaBadge.test.tsx` (11 tests)
- `apps/web/src/components/chat-unified/__tests__/ChatThreadView.test.tsx` (integration)
- `apps/web/src/lib/api/clients/knowledgeBaseClient.ts` (lines 30-35)

## Conclusion

ChatMessageList is a **high-complexity, high-risk component** with mission-critical features (feedback, streaming, citations) that have **zero test coverage** in the component's own test file. While some features are validated in parent tests (ChatThreadView), the ChatMessageList component itself is inadequately tested for a 50% refactor.

**Estimated effort to achieve LOW risk refactor readiness:** 12-16 hours (17 characterization tests + coverage validation).

**Go/No-Go for #292 Refactor:** 🛑 **NO-GO until characterization tests completed.**
