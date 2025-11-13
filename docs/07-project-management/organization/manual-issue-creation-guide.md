# Board Game AI - Manual Issue Creation Guide

**Created**: 2025-01-15
**Status**: 6 issues created (#941-#946), 79 remaining
**Link**: https://github.com/DegrassiAaron/meepleai-monorepo/issues

---

## ✅ Already Created (6/85)

| Issue # | GitHub # | Title | Status |
|---------|----------|-------|--------|
| BGAI-001 | #941 | Setup LLMWhisperer account | ✅ Created |
| BGAI-002 | #942 | Implement LlmWhispererPdfExtractor | ✅ Created |
| BGAI-003 | #943 | Add LLMWhisperer configuration | ✅ Created |
| BGAI-004 | #944 | Unit tests for LlmWhispererPdfExtractor | ✅ Created |
| BGAI-005 | #945 | Create FastAPI service for SmolDocling | ✅ Created |
| BGAI-006 | #946 | Docker configuration for pdf-processor | ✅ Created |

---

## 📋 To Create Manually (79 issues)

### Month 1: PDF Processing (9 remaining)

#### BGAI-007: Implement SmolDoclingPdfExtractor (C# client)
```
Title: [BGAI-007] Implement SmolDoclingPdfExtractor (C# client)

Labels: board-game-ai, month-1, backend, pdf

Milestone: Month 1: PDF Processing

Description:
**Goal**: C# client for SmolDocling Python service

**Tasks**:
- [ ] Create SmolDoclingPdfExtractor class
- [ ] Implement IHttpClientFactory pattern
- [ ] Add retry logic (3 attempts)
- [ ] Add circuit breaker pattern
- [ ] Handle service unavailable
- [ ] Add timeout configuration (60s)
- [ ] Log service calls

**Acceptance Criteria**:
- HttpClient wrapper functional
- Retry and circuit breaker working
- Error handling complete
- Logging operational

**Dependencies**: #945 (BGAI-005), #946 (BGAI-006)
**References**: solo-developer-execution-plan.md Week 2 Day 9
```

#### BGAI-008: Integration tests for SmolDocling pipeline
```
Title: [BGAI-008] Integration tests for SmolDocling pipeline

Labels: board-game-ai, month-1, backend, testing, integration

Milestone: Month 1: PDF Processing

Description:
**Goal**: End-to-end testing of SmolDocling integration

**Test Cases**:
1. Successful PDF extraction via service
2. Service timeout handling
3. Service unavailable (circuit breaker)
4. Invalid PDF error from service
5. Large file processing
6. Concurrent requests
7. Service restart recovery

**Acceptance Criteria**:
- All tests passing
- Uses Testcontainers for pdf-processor
- Tests real Docker service
- Code coverage ≥90%

**Dependencies**: #946 (BGAI-006), BGAI-007
**References**: solo-developer-execution-plan.md Week 2 Day 10
```

#### BGAI-009: Migrate to IPdfTextExtractor interface
**Note**: This maps to existing #940
```
Title: [BGAI-009] Migrate to IPdfTextExtractor interface (maps to #940)

Add comment to #940:
"📋 This issue is tracked as BGAI-009 in the Board Game AI execution plan (Month 1, Week 3, Days 11-12)"

Update #940 labels: Add "board-game-ai, month-1"
```

#### BGAI-010: Implement EnhancedPdfProcessingOrchestrator
```
Title: [BGAI-010] Implement EnhancedPdfProcessingOrchestrator (3-stage fallback)

Labels: board-game-ai, month-1, backend, pdf, orchestration

Milestone: Month 1: PDF Processing

Description:
**Goal**: Orchestrate 3-stage PDF processing pipeline with fallback

**Stages**:
1. **Stage 1**: LLMWhisperer (best quality)
2. **Stage 2**: SmolDocling (good quality)
3. **Stage 3**: Docnet (fallback)

**Logic**:
- Try Stage 1, check quality score ≥0.80
- If fails or low quality, try Stage 2
- If fails, fallback to Stage 3
- Track which stage succeeded

**Tasks**:
- [ ] Create EnhancedPdfProcessingOrchestrator class
- [ ] Implement fallback logic
- [ ] Add quality score calculation
- [ ] Add performance tracking
- [ ] Add logging for each stage
- [ ] Configure stage priorities

**Acceptance Criteria**:
- Fallback logic functional
- Quality scores accurate
- Performance tracking operational
- All stages tested

**Dependencies**: #940 (BGAI-009)
**References**: solo-developer-execution-plan.md Week 3 Days 13-14
```

#### BGAI-011 to BGAI-015: Remaining Month 1 Issues
Follow the same pattern from `solo-developer-execution-plan.md` section "Missing Issues Breakdown by Month" (lines 36-51).

---

### Month 2: LLM Integration (12 issues)

#### BGAI-016: Setup OpenRouter account and API key
```
Title: [BGAI-016] Setup OpenRouter account and API key

Labels: board-game-ai, month-2, backend, llm, configuration

Milestone: Month 2: LLM Integration

Description:
**Goal**: Configure OpenRouter API for multi-model LLM access

**Tasks**:
- [ ] Create account at https://openrouter.ai
- [ ] Add $50 credits for testing
- [ ] Generate API key
- [ ] Add API key to appsettings.json (AI:OpenRouter:ApiKey)
- [ ] Add API key to .env files
- [ ] Test API connection with GPT-4
- [ ] Test API connection with Claude 3.5

**Acceptance Criteria**:
- API key securely configured
- Connection tests pass for both models
- Documentation updated
- Cost tracking enabled

**References**: solo-developer-execution-plan.md Week 5 Day 21
```

#### BGAI-017 to BGAI-027: Remaining Month 2 Issues
Follow the pattern from `solo-developer-execution-plan.md` lines 53-65.

---

### Month 3: Multi-Model Validation (13 issues)

#### BGAI-028: ConfidenceValidationService
```
Title: [BGAI-028] ConfidenceValidationService (threshold ≥0.70)

Labels: board-game-ai, month-3, backend, validation

Milestone: Month 3: Multi-Model Validation

Description:
**Goal**: Implement confidence score validation layer

**Tasks**:
- [ ] Create ConfidenceValidationService class
- [ ] Implement confidence threshold check (≥0.70)
- [ ] Add validation result logging
- [ ] Reject responses below threshold
- [ ] Add configuration for threshold
- [ ] Track rejection rate metrics

**Acceptance Criteria**:
- Threshold enforcement working
- Low-confidence responses rejected
- Metrics tracked
- Configuration flexible

**References**: solo-developer-execution-plan.md Week 9 Day 41
```

#### BGAI-029 to BGAI-040: Remaining Month 3 Issues
Follow the pattern from `solo-developer-execution-plan.md` lines 67-80.

---

### Month 4: Quality Framework + Frontend (15 issues)

#### BGAI-041: Extend PromptEvaluationService (5-metric framework)
```
Title: [BGAI-041] Extend PromptEvaluationService (5-metric framework)

Labels: board-game-ai, month-4, backend, quality

Milestone: Month 4: Quality Framework

Description:
**Goal**: Implement 5-metric quality evaluation framework

**Metrics**:
1. **Accuracy**: Correctness against golden dataset
2. **Relevance**: Answer relevance to question
3. **Completeness**: Coverage of all aspects
4. **Clarity**: Response understandability
5. **Citation Quality**: Source reference accuracy

**Tasks**:
- [ ] Extend PromptEvaluationService with 5 metrics
- [ ] Implement scoring algorithms (0.0-1.0)
- [ ] Add aggregation logic (weighted average)
- [ ] Store metric results in database
- [ ] Add metric trend tracking

**Acceptance Criteria**:
- All 5 metrics implemented
- Scoring accurate and consistent
- Results persisted
- Trends trackable

**References**: solo-developer-execution-plan.md Week 13 Days 61-62
```

#### BGAI-042 to BGAI-055: Remaining Month 4 Issues
Follow the pattern from `solo-developer-execution-plan.md` lines 82-97.

---

### Month 5: Golden Dataset + Q&A Interface (14 issues)

#### BGAI-056: Annotation: Terraforming Mars (20 Q&A pairs)
```
Title: [BGAI-056] Annotation: Terraforming Mars (20 Q&A pairs)

Labels: board-game-ai, month-5, backend, dataset, annotation

Milestone: Month 5: Golden Dataset

Description:
**Goal**: Create 20 high-quality Q&A pairs for Terraforming Mars

**Tasks**:
- [ ] Read Terraforming Mars rulebook (Italian)
- [ ] Identify 20 common/complex rule questions
- [ ] Write clear questions (Italian)
- [ ] Write accurate answers with citations
- [ ] Validate answers against rulebook
- [ ] Store in golden dataset format (JSON/CSV)

**Question Categories**:
- Setup (3 questions)
- Turn structure (4 questions)
- Resource management (4 questions)
- Card effects (4 questions)
- Scoring (3 questions)
- Edge cases (2 questions)

**Acceptance Criteria**:
- 20 Q&A pairs complete
- All answers accurate
- Citations to page numbers
- JSON/CSV format validated

**References**: solo-developer-execution-plan.md Week 17 Days 81-82
```

#### BGAI-057 to BGAI-069: Remaining Month 5 Issues
Follow the pattern from `solo-developer-execution-plan.md` lines 99-113.

---

### Month 6: Completion + Polish (16 issues)

#### BGAI-070: Annotation: Scythe, Catan, Pandemic (30 Q&A)
```
Title: [BGAI-070] Annotation: Scythe, Catan, Pandemic (30 Q&A)

Labels: board-game-ai, month-6, backend, dataset, annotation

Milestone: Month 6: Italian UI

Description:
**Goal**: Create 30 Q&A pairs (10 per game)

**Games**:
1. **Scythe** (10 Q&A)
2. **Catan** (10 Q&A)
3. **Pandemic** (10 Q&A)

**Tasks**:
- [ ] Read 3 rulebooks (Italian)
- [ ] Identify 10 questions per game
- [ ] Write questions and answers
- [ ] Validate accuracy
- [ ] Add to golden dataset

**Acceptance Criteria**:
- 30 Q&A pairs complete
- All answers accurate
- Citations included
- Total dataset: 80 Q&A (50 from Month 5 + 30 new)

**References**: solo-developer-execution-plan.md Week 21 Days 101-102
```

#### BGAI-071 to BGAI-085: Remaining Month 6 Issues
Follow the pattern from `solo-developer-execution-plan.md` lines 115-131.

---

## 🎯 Quick Creation Workflow

### For Each Issue:

1. **Navigate**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/new

2. **Copy Template** from this guide (or from `solo-developer-execution-plan.md` lines 36-131)

3. **Fill**:
   - Title: Exact format `[BGAI-XXX] Title`
   - Labels: Copy from template
   - Milestone: Select correct Month (1-6)
   - Description: Copy entire block

4. **Adjust Dependencies**:
   - Replace `[BGAI-XXX]` with `#YYY` for already-created issues
   - Keep `[BGAI-XXX]` for not-yet-created issues

5. **Create**: Click "Submit new issue"

6. **Track**: Mark in this guide as ✅ Created

---

## 📊 Progress Tracking

### Month 1: PDF Processing (6/15 created)
- [x] BGAI-001 (#941)
- [x] BGAI-002 (#942)
- [x] BGAI-003 (#943)
- [x] BGAI-004 (#944)
- [x] BGAI-005 (#945)
- [x] BGAI-006 (#946)
- [ ] BGAI-007 (to create)
- [ ] BGAI-008 (to create)
- [ ] BGAI-009 (link to #940)
- [ ] BGAI-010 (to create)
- [ ] BGAI-011 (to create)
- [ ] BGAI-012 (to create)
- [ ] BGAI-013 (to create)
- [ ] BGAI-014 (to create)
- [ ] BGAI-015 (to create)

### Month 2: LLM Integration (0/12 created)
- [ ] BGAI-016 to BGAI-027

### Month 3: Multi-Model Validation (0/13 created)
- [ ] BGAI-028 to BGAI-040

### Month 4: Quality + Frontend (0/15 created)
- [ ] BGAI-041 to BGAI-055

### Month 5: Dataset + Q&A (0/14 created)
- [ ] BGAI-056 to BGAI-069

### Month 6: Completion (0/16 created)
- [ ] BGAI-070 to BGAI-085

**Total Progress**: 6/85 (7%)

---

## 💡 Tips for Efficiency

### Batch Creation
Create issues in batches by month for context consistency:
1. Month 1 batch (9 remaining)
2. Month 2 batch (12 issues)
3. Month 3 batch (13 issues)
4. Month 4 batch (15 issues)
5. Month 5 batch (14 issues)
6. Month 6 batch (16 issues)

### Copy-Paste Templates
All templates are in:
- This guide (detailed examples)
- `solo-developer-execution-plan.md` lines 36-131 (full list)
- `bgai-issue-tracking-summary.md` (overview)

### Label Quick Reference
```
Common combinations:
- Backend: board-game-ai, month-X, backend
- Frontend: board-game-ai, month-X, frontend
- Testing: board-game-ai, month-X, testing
- Documentation: board-game-ai, month-X, documentation
```

### Milestone Quick Reference
```
#13 = Month 1: PDF Processing
#14 = Month 2: LLM Integration
#15 = Month 3: Multi-Model Validation
#16 = Month 4: Quality Framework
#17 = Month 5: Golden Dataset
#18 = Month 6: Italian UI
```

---

## ✅ Verification Checklist

After creating all issues:

- [ ] All 85 issues created (check https://github.com/DegrassiAaron/meepleai-monorepo/issues?q=is:issue+label:board-game-ai)
- [ ] All have `board-game-ai` label
- [ ] All have correct `month-X` label
- [ ] All assigned to correct milestone
- [ ] Dependencies linked (#XXX format)
- [ ] All have acceptance criteria
- [ ] All reference execution plan

---

## 🔗 Resources

- **Execution Plan**: `docs/org/solo-developer-execution-plan.md`
- **Issue Templates**: Lines 36-131 in execution plan
- **GitHub Issues**: https://github.com/DegrassiAaron/meepleai-monorepo/issues
- **Milestones**: https://github.com/DegrassiAaron/meepleai-monorepo/milestones
- **Labels**: https://github.com/DegrassiAaron/meepleai-monorepo/labels

---

**Version**: 1.0
**Last Updated**: 2025-01-15
**Status**: 6/85 issues created, guide complete for manual creation
