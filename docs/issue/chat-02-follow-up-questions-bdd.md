# CHAT-02: AI-Generated Follow-Up Questions - BDD Specification

## Feature Overview

**Feature**: AI-Generated Follow-Up Questions
**Epic**: EPIC-03 (Chat Interface)
**Priority**: Medium
**Sprint**: 5-6
**Issue**: #401

**User Story**:
> As a user who just received an answer, I want to see suggested follow-up questions so that I can explore related rules without thinking of what to ask next.

**Success Metrics**:
- 40% of users click at least one suggested question
- Average session length increases by 50%
- Reduced "I don't know what to ask" friction

---

## BDD Scenarios

### Scenario 1: Generate Follow-Up Questions After QA Response

```gherkin
Feature: AI-Generated Follow-Up Questions
  As a user exploring board game rules
  I want to see intelligent follow-up questions after each answer
  So that I can deepen my understanding without thinking of what to ask next

Scenario: User receives follow-up questions after asking about Tic-Tac-Toe
  Given I am authenticated as "user@meepleai.dev"
  And I have selected the game "Tic-Tac-Toe"
  And the game has indexed PDF rulebook content
  When I ask the question "How do I win Tic-Tac-Toe?"
  Then I should receive an answer about winning conditions
  And I should see 3-5 follow-up questions
  And the follow-up questions should be relevant to Tic-Tac-Toe rules
  And each question should be 5-15 words long
  And the questions should explore different aspects (clarifications, extensions, scenarios)

  Examples:
    | Original Question          | Follow-Up Question Examples                          |
    | "How do I win?"           | "What happens if there's a tie?"                     |
    |                            | "How many players can play Tic-Tac-Toe?"             |
    |                            | "Can you explain the basic setup?"                   |
    | "How do I castle in chess?"| "Can I castle after moving my king?"                 |
    |                            | "What happens if my rook is captured?"               |
    |                            | "Is queenside castling different from kingside?"     |
```

### Scenario 2: Follow-Up Questions Are Cached

```gherkin
Scenario: Follow-up questions are cached with QA response
  Given I am authenticated as "user@meepleai.dev"
  And I have selected the game "Chess"
  When I ask "How do I castle?" with generateFollowUps=true
  Then the response should be cached in Redis with key "v2:qa:followups:{game_id}:{hash}"
  And the cached response should include follow-up questions

  When I ask the same question again
  Then the response should be retrieved from cache
  And the follow-up questions should be identical to the first request
  And the cache hit metric should increment
```

### Scenario 3: Follow-Up Generation Timeout

```gherkin
Scenario: Follow-up generation times out gracefully
  Given I am authenticated as "user@meepleai.dev"
  And the LLM service is slow (>10 seconds response time)
  When I ask a question with generateFollowUps=true
  Then I should receive the QA answer within 3 seconds
  And the followUpQuestions field should be null
  And a warning log should be created: "Follow-up generation timed out"
  And the QA request should NOT fail
```

### Scenario 4: LLM Returns Malformed JSON

```gherkin
Scenario: LLM returns invalid JSON for follow-ups
  Given I am authenticated as "user@meepleai.dev"
  And the LLM service returns non-JSON text
  When I ask a question with generateFollowUps=true
  Then the system should retry JSON parsing once

  When all retry attempts fail
  Then I should receive the QA answer
  And the followUpQuestions field should be null
  And a warning log should be created: "Follow-up JSON parsing failed"
  And the generation error metric should increment
```

### Scenario 5: Feature Flag Disabled

```gherkin
Scenario: Follow-up questions disabled via configuration
  Given the FollowUpQuestions:Enabled config is set to false
  And I am authenticated as "user@meepleai.dev"
  When I ask a question with generateFollowUps=true
  Then I should receive the QA answer
  And the followUpQuestions field should be null
  And no LLM call should be made for follow-up generation
```

### Scenario 6: Click Tracking

```gherkin
Scenario: User clicks a follow-up question
  Given I am on the chat page
  And I have received a QA response with 5 follow-up questions
  When I click the second follow-up question "How many players can play?"
  Then the question should be populated in the chat input field
  And an analytics event should be sent with:
    | Field                | Value                          |
    | eventType            | "follow_up_question_clicked"   |
    | chatId               | {current_chat_id}              |
    | originalQuestion     | {original_question}            |
    | followUpQuestion     | "How many players can play?"   |
    | questionIndex        | 1 (0-indexed)                  |
  And the click metric should increment
```

### Scenario 7: Streaming Endpoint with Follow-Ups

```gherkin
Scenario: Streaming QA includes follow-up questions as final event
  Given I am authenticated as "user@meepleai.dev"
  And I have selected the game "Chess"
  When I send a streaming QA request with generateFollowUps=true
  Then I should receive SSE events in this order:
    | Event Type         | Description                          |
    | StateUpdate        | "Searching vector database..."       |
    | Citations          | List of relevant snippets            |
    | Token              | First word of answer                 |
    | Token              | Second word of answer                |
    | ...                | ...                                  |
    | Token              | Last word of answer                  |
    | Complete           | Metadata (tokens, confidence)        |
    | FollowUpQuestions  | Array of 3-5 questions (NEW)         |

  And the FollowUpQuestions event data should be:
    ```json
    {
      "questions": [
        "Can I castle after moving my king?",
        "What happens if my rook is captured?",
        "Is queenside castling different?"
      ]
    }
    ```
```

### Scenario 8: Max Questions Configuration

```gherkin
Scenario: System respects MaxQuestionsPerResponse configuration
  Given the FollowUpQuestions:MaxQuestionsPerResponse config is set to 3
  And the LLM returns 10 follow-up questions
  When I ask a question with generateFollowUps=true
  Then I should receive exactly 3 follow-up questions
  And the remaining 7 questions should be discarded
```

### Scenario 9: Empty or Invalid Questions Filtered

```gherkin
Scenario: System filters out empty or invalid questions
  Given the LLM returns JSON with some empty/whitespace-only questions:
    ```json
    {
      "questions": [
        "How many players can play?",
        "",
        "   ",
        "What happens if there's a tie?",
        null
      ]
    }
    ```
  When I ask a question with generateFollowUps=true
  Then I should receive only 2 valid follow-up questions
  And empty/whitespace/null questions should be filtered out
```

### Scenario 10: Cache Invalidation on Rulebook Update

```gherkin
Scenario: Follow-up question cache is cleared when rulebook is updated
  Given I have asked "How do I win?" for game "Chess"
  And the QA response with follow-ups is cached
  When an admin uploads a new PDF for "Chess"
  Then all cache keys matching "v2:qa:followups:{chess_game_id}:*" should be deleted

  When I ask "How do I win?" again
  Then the response should be regenerated (cache miss)
  And new follow-up questions should be generated based on the updated rulebook
```

---

## Test Implementation Plan

### Phase 1: Backend Unit Tests

**File**: `apps/api/tests/Api.Tests/Services/FollowUpQuestionServiceTests.cs`

**Test Cases**:
1. ✅ `GenerateQuestionsAsync_ValidInput_ReturnsQuestions` - Happy path
2. ✅ `GenerateQuestionsAsync_LlmReturnsNull_ReturnsEmptyList` - Null handling
3. ✅ `GenerateQuestionsAsync_Timeout_ReturnsEmptyList` - Timeout graceful degradation
4. ✅ `GenerateQuestionsAsync_RespectsMaxQuestionsConfig` - Configuration limits
5. ✅ `GenerateQuestionsAsync_FiltersEmptyQuestions` - Validation logic
6. ✅ `GenerateQuestionsAsync_RetriesOnJsonParseFailure` - Retry logic
7. ✅ `GenerateQuestionsAsync_IncludesRAGContextInPrompt` - Prompt construction
8. ✅ `GenerateQuestionsAsync_RecordsMetrics` - Observability

### Phase 2: Backend Integration Tests

**File**: `apps/api/tests/Api.Tests/Integration/FollowUpQuestionsEndpointTests.cs`

**Test Cases**:
1. ✅ `QaEndpoint_WithGenerateFollowUpsTrue_ReturnsFollowUpQuestions` - End-to-end with Testcontainers
2. ✅ `QaEndpoint_WithGenerateFollowUpsFalse_ReturnsNullFollowUps` - Feature flag
3. ✅ `QaEndpoint_CacheHit_ReturnsFollowUpsFromCache` - Cache behavior
4. ✅ `QaEndpoint_CacheInvalidationOnPdfUpdate_RegeneratesFollowUps` - Invalidation
5. ✅ `StreamingEndpoint_SendsFollowUpQuestionsEvent` - SSE event parsing
6. ✅ `AnalyticsEndpoint_TracksFollowUpClick` - Analytics tracking

### Phase 3: Frontend Unit Tests

**File**: `apps/web/src/components/__tests__/FollowUpQuestions.test.tsx`

**Test Cases**:
1. ✅ `renders follow-up questions as pills` - UI rendering
2. ✅ `populates chat input on question click` - Click handler
3. ✅ `sends analytics event on click` - Analytics integration
4. ✅ `handles empty questions array gracefully` - Edge case
5. ✅ `displays loading state while generating` - UX feedback

### Phase 4: E2E Tests

**File**: `apps/web/e2e/follow-up-questions.spec.ts`

**Test Cases**:
1. ✅ `displays follow-up questions after QA response` - Full user flow
2. ✅ `clicking follow-up question populates chat input` - Interaction
3. ✅ `follow-up questions not shown if feature disabled` - Configuration
4. ✅ `displays loading state while generating follow-ups` - UX

---

## Acceptance Criteria (from Issue #401)

### Backend
- [ ] Generate 3-5 follow-up questions using LLM
- [ ] Questions based on RAG context and conversation history
- [ ] Cache questions with response (avoid regeneration)
- [ ] Track which suggested questions users click (analytics)

### Frontend
- [ ] Display questions as clickable buttons below response
- [ ] Clicking button sends question immediately (populates input)
- [ ] Questions styled distinctly from regular messages
- [ ] User can dismiss suggestions (future enhancement)

### Testing
- [ ] Unit tests for question generation logic
- [ ] Integration tests with LLM mock
- [ ] E2E test: answer appears → suggestions appear → click suggestion

---

## Definition of Done

- [ ] Code implemented and functional
- [ ] Unit tests written and passing (90% coverage)
- [ ] Integration tests passing (Testcontainers)
- [ ] E2E test covering full user flow
- [ ] Code review approved (self-review via deep-think-developer)
- [ ] Documentation updated (CLAUDE.md, API docs)
- [ ] CI/CD pipeline green
- [ ] Tested in local environment (Docker Compose)
- [ ] No regressions identified
- [ ] Performance tested (generation time < 2s)
- [ ] Analytics tracking implemented and verified
- [ ] Observability metrics (Grafana dashboard panel)

---

## Implementation Order

1. **Backend Core** (Day 1-2):
   - [ ] Add `GenerateJsonAsync<T>()` to `ILlmService` and `LlmService`
   - [ ] Implement `FollowUpQuestionService`
   - [ ] Extend `QaResponse` model
   - [ ] Add configuration (`FollowUpQuestionsConfiguration`)
   - [ ] Register services in DI (`Program.cs`)

2. **Backend Endpoints** (Day 2-3):
   - [ ] Integrate into `/api/v1/agents/qa` endpoint
   - [ ] Integrate into `/api/v1/agents/qa/stream` endpoint
   - [ ] Add analytics endpoint `/api/v1/analytics/followups/click`
   - [ ] Update cache service for versioned keys

3. **Backend Tests** (Day 3-4):
   - [ ] Unit tests for `FollowUpQuestionService`
   - [ ] Unit tests for `LlmService.GenerateJsonAsync<T>()`
   - [ ] Integration tests for QA endpoint with follow-ups
   - [ ] Integration tests for streaming endpoint
   - [ ] Integration tests for cache behavior

4. **Frontend UI** (Day 4-5):
   - [ ] Create `FollowUpQuestions` component
   - [ ] Integrate into `chat.tsx`
   - [ ] Add click handler and analytics tracking
   - [ ] Style follow-up buttons (pills with hover effects)

5. **Frontend Tests** (Day 5):
   - [ ] Unit tests for `FollowUpQuestions` component
   - [ ] E2E tests for full user interaction flow
   - [ ] A11y tests for follow-up buttons

6. **Observability** (Day 6):
   - [ ] Add OpenTelemetry metrics (`MeepleAiMetrics.cs`)
   - [ ] Add Grafana dashboard panel
   - [ ] Verify Seq logs for follow-up generation

7. **Documentation** (Day 6):
   - [ ] Update `CLAUDE.md` with architecture details
   - [ ] Update API Swagger/OpenAPI documentation
   - [ ] Create `docs/issue/chat-02-follow-up-questions-implementation.md`

---

## Risk Mitigation

| Risk | Mitigation |
|------|------------|
| LLM JSON reliability | Retry logic (2 attempts) + graceful degradation |
| Latency impact | Parallel execution + 10s timeout + caching |
| Low click-through rate | A/B testing different question styles + analytics funnel |
| Cache key collision | Versioned keys (v2:qa:followups) + invalidation tests |
| Feature breaks existing QA | Backward compatibility (optional field) + extensive tests |

---

## Success Validation

**Week 1 Metrics**:
- [ ] Follow-up questions appear in 100% of QA responses (when enabled)
- [ ] JSON parse success rate >90%
- [ ] QA latency p95 <2.5s (vs 1.5s baseline = <67% increase)
- [ ] No errors in CI/CD pipeline
- [ ] 90%+ test coverage maintained

**Week 2-4 Metrics** (Post-Deployment):
- [ ] Click-through rate ≥40% (primary goal)
- [ ] Session length increase ≥50%
- [ ] Question quality rated "helpful" by ≥60% of users
- [ ] Error rate <1% for follow-up generation
- [ ] Cache hit rate remains >60%

---

**Document Version**: 1.0
**Created**: 2025-10-18
**Author**: Claude Code (MeepleAI Development Team)
