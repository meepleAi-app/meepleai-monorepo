#!/bin/bash

# Board Game AI - Issue Generation Script
# Creates 85 GitHub issues for Phase 1A (Months 1-6)
#
# Usage: bash tools/create-bgai-issues.sh
#
# Prerequisites:
# - gh CLI installed and authenticated
# - Labels created (run create-bgai-labels.sh first)
# - Milestones created (Month 1-6: PDF Processing, LLM Integration, etc.)

set -e

echo "🚀 Creating Board Game AI Issues..."
echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo ""

# Month 1: PDF Processing Pipeline (15 issues)
echo "📋 Month 1: PDF Processing Pipeline (15 issues)"

gh issue create --title "[BGAI-001] Setup LLMWhisperer account and API key configuration" \
  --body "**Goal**: Configure LLMWhisperer API for Stage 1 PDF extraction

**Tasks**:
- [ ] Create account at https://llmwhisperer.com
- [ ] Generate API key
- [ ] Add API key to appsettings.json
- [ ] Add API key to .env files
- [ ] Test API connection

**Acceptance Criteria**:
- API key securely configured
- Connection test passes
- Documentation updated

**References**: solo-developer-execution-plan.md Week 1 Day 1" \
  --label "board-game-ai,month-1,backend,configuration" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-002] Implement LlmWhispererPdfExtractor (C# HttpClient wrapper)" \
  --body "**Goal**: Create C# client for LLMWhisperer API

**Tasks**:
- [ ] Create LlmWhispererPdfExtractor class
- [ ] Implement IHttpClientFactory pattern
- [ ] Add retry logic (3 attempts)
- [ ] Add timeout configuration (30s)
- [ ] Handle API errors gracefully
- [ ] Log API calls

**Acceptance Criteria**:
- HttpClient wrapper functional
- Retry logic tested
- Error handling complete
- Logging operational

**Dependencies**: [BGAI-001]
**References**: solo-developer-execution-plan.md Week 1 Days 2-3" \
  --label "board-game-ai,month-1,backend,pdf" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-003] Add LLMWhisperer configuration (appsettings + feature flag)" \
  --body "**Goal**: Configuration management for LLMWhisperer integration

**Tasks**:
- [ ] Add LLMWhisperer section to appsettings.json
- [ ] Create feature flag PDF:UseLLMWhisperer
- [ ] Add timeout configuration
- [ ] Add quality threshold configuration
- [ ] Validate configuration on startup

**Acceptance Criteria**:
- Configuration properly structured
- Feature flag functional
- Defaults reasonable
- Validation working

**Dependencies**: [BGAI-001], [BGAI-002]
**References**: solo-developer-execution-plan.md Week 1 Day 4" \
  --label "board-game-ai,month-1,backend,configuration" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-004] Unit tests for LlmWhispererPdfExtractor (12 tests minimum)" \
  --body "**Goal**: Comprehensive test coverage for LLMWhisperer client

**Test Cases**:
1. Successful PDF extraction
2. API timeout handling
3. Invalid PDF error
4. Network error retry
5. Quality threshold validation
6. Configuration validation
7. HttpClient disposal
8. Null input handling
9. Empty response handling
10. Large file handling
11. Concurrent requests
12. API rate limit handling

**Acceptance Criteria**:
- All 12 tests passing
- Code coverage ≥90%
- Tests use Moq for HttpClient
- Integration tests with TestContainers

**Dependencies**: [BGAI-002], [BGAI-003]
**References**: solo-developer-execution-plan.md Week 1 Day 5" \
  --label "board-game-ai,month-1,backend,testing" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-005] Create FastAPI service for SmolDocling (Python)" \
  --body "**Goal**: Python microservice for SmolDocling PDF processing

**Tasks**:
- [ ] Setup FastAPI project structure
- [ ] Install SmolDocling dependencies
- [ ] Create /extract endpoint (POST)
- [ ] Add health check endpoint
- [ ] Implement PDF processing logic
- [ ] Add error handling
- [ ] Add logging (structured JSON)
- [ ] Create Dockerfile

**Acceptance Criteria**:
- FastAPI service functional
- SmolDocling integration working
- Health check operational
- Docker image builds successfully

**Tech Stack**: Python 3.11, FastAPI, SmolDocling, uvicorn
**References**: solo-developer-execution-plan.md Week 2 Days 6-7" \
  --label "board-game-ai,month-1,backend,python,microservice" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-006] Docker configuration for pdf-processor service" \
  --body "**Goal**: Containerize SmolDocling Python service

**Tasks**:
- [ ] Create Dockerfile for pdf-processor
- [ ] Add to docker-compose.yml
- [ ] Configure service networking
- [ ] Add health check in Docker
- [ ] Setup volume mounts for PDFs
- [ ] Configure environment variables
- [ ] Test container startup

**Acceptance Criteria**:
- Docker image builds successfully
- Service starts via docker-compose up
- Health check passes
- Can communicate with API service

**Dependencies**: [BGAI-005]
**References**: solo-developer-execution-plan.md Week 2 Day 8" \
  --label "board-game-ai,month-1,devops,docker" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-007] Implement SmolDoclingPdfExtractor (C# client)" \
  --body "**Goal**: C# client for SmolDocling Python service

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

**Dependencies**: [BGAI-005], [BGAI-006]
**References**: solo-developer-execution-plan.md Week 2 Day 9" \
  --label "board-game-ai,month-1,backend,pdf" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-008] Integration tests for SmolDocling pipeline" \
  --body "**Goal**: End-to-end testing of SmolDocling integration

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

**Dependencies**: [BGAI-006], [BGAI-007]
**References**: solo-developer-execution-plan.md Week 2 Day 10" \
  --label "board-game-ai,month-1,backend,testing,integration" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-009] Migrate to IPdfTextExtractor interface (maps to #940)" \
  --body "**Goal**: Refactor PDF processing to use adapter pattern

**Tasks**:
- [ ] Create IPdfTextExtractor interface
- [ ] Implement DocnetPdfExtractor (existing logic)
- [ ] Implement LlmWhispererPdfExtractor
- [ ] Implement SmolDoclingPdfExtractor
- [ ] Update DI registration
- [ ] Migrate existing usages

**Acceptance Criteria**:
- Interface properly abstracted
- All 3 extractors implement interface
- Existing tests pass
- DI configuration correct

**Links**: This maps to existing issue #940
**Dependencies**: [BGAI-002], [BGAI-007]
**References**: solo-developer-execution-plan.md Week 3 Days 11-12" \
  --label "board-game-ai,month-1,backend,refactoring,ddd" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-010] Implement EnhancedPdfProcessingOrchestrator (3-stage fallback)" \
  --body "**Goal**: Orchestrate 3-stage PDF processing pipeline with fallback

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

**Dependencies**: [BGAI-009]
**References**: solo-developer-execution-plan.md Week 3 Days 13-14" \
  --label "board-game-ai,month-1,backend,pdf,orchestration" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-011] Integration tests for 3-stage PDF pipeline (end-to-end)" \
  --body "**Goal**: Comprehensive E2E testing of PDF pipeline

**Test Scenarios**:
1. **Happy Path**: Stage 1 succeeds (high quality)
2. **Fallback to Stage 2**: Stage 1 fails, Stage 2 succeeds
3. **Fallback to Stage 3**: Stages 1&2 fail, Stage 3 succeeds
4. **Quality Gate**: Stage 1 low quality, Stage 2 high quality
5. **All Stages Fail**: Error handling
6. **Performance**: P95 latency <5s per stage

**Acceptance Criteria**:
- All scenarios covered
- Uses real services (Testcontainers)
- Quality scores validated
- Performance benchmarks pass

**Dependencies**: [BGAI-010]
**References**: solo-developer-execution-plan.md Week 3 Day 15" \
  --label "board-game-ai,month-1,backend,testing,integration,e2e" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-012] PDF quality validation (≥0.80 quality score)" \
  --body "**Goal**: Automated quality validation for PDF extraction

**Quality Metrics**:
- Text extraction completeness (>90%)
- Formatting preservation
- Table detection accuracy
- Image caption extraction
- Overall quality score (0.0-1.0)

**Tasks**:
- [ ] Implement quality scoring algorithm
- [ ] Add validation rules
- [ ] Create quality report
- [ ] Add quality gate to pipeline
- [ ] Log quality scores

**Acceptance Criteria**:
- Quality score algorithm functional
- Threshold enforcement (≥0.80)
- Quality reports generated
- Tests validate scoring

**Dependencies**: [BGAI-011]
**References**: solo-developer-execution-plan.md Week 3 Day 15" \
  --label "board-game-ai,month-1,backend,quality" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-013] Bug fixes and edge cases for PDF pipeline" \
  --body "**Goal**: Harden PDF pipeline against edge cases

**Edge Cases**:
- Empty PDFs
- Corrupted files
- Extremely large files (>100MB)
- Password-protected PDFs
- Scanned documents (OCR)
- Non-English text
- Special characters
- Unicode handling

**Tasks**:
- [ ] Test all edge cases
- [ ] Add validation for edge cases
- [ ] Improve error messages
- [ ] Add fallback handling
- [ ] Document limitations

**Acceptance Criteria**:
- All edge cases handled gracefully
- Error messages clear and actionable
- Tests cover edge cases
- Documentation updated

**Dependencies**: [BGAI-012]
**References**: solo-developer-execution-plan.md Week 4 Days 16-17" \
  --label "board-game-ai,month-1,backend,bug-fix" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-014] Code review checklist for PDF processing" \
  --body "**Goal**: Self-review of PDF processing implementation

**Review Areas**:
- [ ] Code quality (SOLID principles)
- [ ] Error handling completeness
- [ ] Logging adequacy
- [ ] Performance optimization
- [ ] Security considerations
- [ ] Test coverage (≥90%)
- [ ] Documentation completeness
- [ ] Configuration validation

**Deliverable**: Checklist document in `docs/code-review/`

**Dependencies**: [BGAI-013]
**References**: solo-developer-execution-plan.md Week 4 Day 18" \
  --label "board-game-ai,month-1,quality,code-review" \
  --milestone "Month 1: PDF Processing"

gh issue create --title "[BGAI-015] Documentation (README, API docs, code comments)" \
  --body "**Goal**: Comprehensive documentation for PDF processing

**Documentation**:
1. **README**: PDF pipeline overview
2. **API Docs**: Endpoint specifications
3. **Code Comments**: Inline documentation
4. **Architecture**: ADR for 3-stage pipeline
5. **Configuration**: Setup guide
6. **Troubleshooting**: Common issues

**Tasks**:
- [ ] Write README for pdf-processor service
- [ ] Document API endpoints
- [ ] Add code comments
- [ ] Create ADR-003 (PDF processing)
- [ ] Write configuration guide
- [ ] Write troubleshooting guide

**Acceptance Criteria**:
- All documentation complete
- Examples provided
- Diagrams included
- Easy to follow

**Dependencies**: [BGAI-014]
**References**: solo-developer-execution-plan.md Week 4 Days 19-20" \
  --label "board-game-ai,month-1,documentation" \
  --milestone "Month 1: PDF Processing"

echo "✅ Month 1 issues created (15/15)"
echo ""

# Month 2: LLM Integration (12 issues)
echo "📋 Month 2: LLM Integration (12 issues)"
echo "⚠️  Run Part 2 script for Month 2-6 issues"
echo ""

echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
echo "✅ Part 1 Complete: 15 issues created"
echo ""
echo "Next Steps:"
echo "1. Review created issues on GitHub"
echo "2. Run tools/create-bgai-issues-part2.sh for Months 2-6"
echo "3. Assign issues to yourself"
echo "4. Start with [BGAI-001]"
echo ""
echo "Dashboard: https://github.com/$(git config --get remote.origin.url | sed 's/.*github.com[:/]\(.*\)\.git/\1/')/issues?q=is:issue+is:open+label:board-game-ai"
