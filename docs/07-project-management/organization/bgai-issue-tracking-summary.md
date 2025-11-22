# Board Game AI - Issue Tracking Implementation Summary

**Date**: 2025-01-15 (Updated after architecture change)
**Status**: 9 issues created (#945-#954), 76 remaining, LLMWhisperer removed
**Architecture**: 100% Open Source (Unstructured + SmolDocling + Docnet)
**Document**: solo-developer-execution-plan.md updated with day-by-day tracking

---

## 🔄 Architecture Change (2025-01-15)

### Decision: Replace LLMWhisperer with Unstructured

**Original Plan**:
- Stage 1: LLMWhisperer (API service, $0-99/month, 100 pages/day free tier)
- Stage 2: SmolDocling (VLM open source)
- Stage 3: Docnet (C# fallback)

**New Architecture** (100% Open Source):
- Stage 1: **Unstructured** (Apache 2.0, RAG-optimized, 1.29s, zero cost)
- Stage 2: SmolDocling (VLM 256M, complex layouts)
- Stage 3: Docnet (existing fallback)

**Why Unstructured**:
1. ✅ Apache 2.0 license (commercial-safe, no fees)
2. ✅ "Clean semantic chunks, **perfect for RAG workflows**" (benchmark winner)
3. ✅ Faster than LLMWhisperer (1.29s vs minutes)
4. ✅ Self-hosted (full control, no vendor lock-in)
5. ✅ Italian language support (tesseract-ocr-ita)
6. ✅ Built-in semantic chunking (ideal for embeddings)

**Impact**:
- **Time Saved**: Week 1 reduced from 5 days → 3 days
- **Cost Saved**: ~$600-1200/year in API fees
- **Issues Closed**: #941-#944 (LLMWhisperer)
- **Issues Created**: #952-#954 (Unstructured replacement)

**References**:
- Research: `docs/kb/Sistemi AI per arbitrare giochi da tavolo stato dell'arte 2025.md`
- Analysis: `docs/architecture/pdf-extraction-opensource-alternatives.md`

---

## What Was Done

### 1. Gap Analysis
- **Analyzed** existing GitHub issues vs execution plan
- **Identified** ~75-80 missing issues for Board Game AI features
- **Current issues**: Only #937 (DDD 5%), #940 (PDF migration) exist
- **Missing**: All PDF, LLM, validation, quality, frontend, dataset issues

### 2. Issue Specification (85 issues total)

**Month 1: PDF Processing** (15 issues)
- [BGAI-001] to [BGAI-015]
- LLMWhisperer integration, SmolDocling service, 3-stage pipeline
- Quality validation, testing, documentation

**Month 2: LLM Integration** (12 issues)
- [BGAI-016] to [BGAI-027]
- OpenRouter client, Ollama fallback, adaptive routing
- RAG service integration, performance testing

**Month 3: Multi-Model Validation** (13 issues)
- [BGAI-028] to [BGAI-040]
- Confidence, citation, hallucination validation
- Multi-model consensus, pipeline integration

**Month 4: Quality + Frontend** (15 issues)
- [BGAI-041] to [BGAI-055]
- 5-metric quality framework, Grafana dashboards
- shadcn/ui, design tokens, i18n, base components

**Month 5: Dataset + Q&A** (14 issues)
- [BGAI-056] to [BGAI-069]
- Golden dataset (50 Q&A pairs)
- QuestionInputForm, ResponseCard, GameSelector
- Backend integration, streaming SSE

**Month 6: Completion + Polish** (16 issues)
- [BGAI-070] to [BGAI-085]
- Dataset expansion (100 Q&A total)
- PDF viewer with citations
- Italian localization (200+ strings)
- Final E2E testing, performance validation

### 3. Execution Plan Updates

**File**: `docs/org/solo-developer-execution-plan.md`

**Changes**:
- ✅ Added GitHub Issues Tracking section at top
- ✅ Created issue breakdown by month (all 85 issues listed)
- ✅ Updated Months 1-6 with day-by-day issue references
- ✅ Added Quality Gates at key milestones
- ✅ Mapped Days 1-120 to specific issues

**Example** (Week 1):
```
**Week 1: LLMWhisperer Integration** (Days 1-5)
- Day 1: Setup LLMWhisperer account ([BGAI-001])
- Day 2-3: Implement LlmWhispererPdfExtractor ([BGAI-002])
- Day 4: Add configuration ([BGAI-003])
- Day 5: Unit tests ([BGAI-004])
```

### 4. Scripts Created

**1. Label Creation**
```bash
tools/create-bgai-labels.sh
```
- Creates 17 labels (board-game-ai, month-1 to month-6, technical areas)
- Color-coded for visual organization
- Must run FIRST before creating issues

**2. Issue Generation**
```bash
tools/create-bgai-issues.sh
```
- Creates first 15 issues (Month 1: PDF Processing)
- Detailed descriptions, acceptance criteria, dependencies
- Linked to milestones and labels
- **Note**: Only Month 1 implemented (Part 1)

**3. Part 2 Script** (TO BE CREATED)
```bash
tools/create-bgai-issues-part2.sh
```
- Will create issues [BGAI-016] to [BGAI-085] (Months 2-6)
- Follows same pattern as Part 1
- **Status**: Not yet created (can be generated from template)

---

## How to Use

### Step 1: Create Labels
```bash
bash tools/create-bgai-labels.sh
```

**Validates**:
- gh CLI authenticated
- Repo access available
- Labels created successfully

### Step 2: Create Milestones (Manual)
Go to GitHub Issues → Milestones → New Milestone:
1. **Month 1: PDF Processing** (Jan 15 - Feb 14, 2025)
2. **Month 2: LLM Integration** (Feb 15 - Mar 14, 2025)
3. **Month 3: Multi-Model Validation** (Mar 15 - Apr 14, 2025)
4. **Month 4: Quality Framework** (Apr 15 - May 14, 2025)
5. **Month 5: Golden Dataset** (May 15 - Jun 14, 2025)
6. **Month 6: Italian UI** (Jun 15 - Jul 14, 2025)

### Step 3: Create Issues (Month 1)
```bash
bash tools/create-bgai-issues.sh
```

**Creates**: 15 issues with:
- Detailed descriptions
- Acceptance criteria
- Dependencies listed
- Labels applied
- Milestones assigned

### Step 4: Create Remaining Issues (Months 2-6)
**Option A**: Manually create issues using the template in `solo-developer-execution-plan.md`

**Option B**: Generate Part 2 script following Part 1 pattern

**Option C**: Use GitHub Projects bulk import (CSV)

### Step 5: Project Board Setup (Recommended)
1. Create new Project: "Board Game AI - Phase 1A"
2. Add views:
   - **By Month**: Group by milestone
   - **By Area**: Group by label (pdf, llm, frontend, etc.)
   - **Calendar**: Timeline view (Days 1-120)
3. Add all [BGAI-XXX] issues to project
4. Set up automation:
   - Todo → In Progress (on assignment)
   - In Progress → Done (on close)

---

## Benefits of This Approach

### For Solo Developer
1. **Clear Roadmap**: Exactly what to do each day (Days 1-120)
2. **Progress Tracking**: Visual progress on GitHub Project board
3. **Accountability**: Commit messages link to issues (`closes #BGAI-001`)
4. **Documentation**: Issue descriptions serve as implementation docs
5. **Milestone Tracking**: Month-by-month completion visibility

### For Team (Future)
1. **Onboarding**: New developers see full scope immediately
2. **Parallel Work**: Multiple devs can pick different issues
3. **Context**: Each issue has full context and acceptance criteria
4. **Dependencies**: Clear dependency chains prevent blocking

### For Stakeholders
1. **Transparency**: See exactly what's being worked on
2. **Timeline**: Month-by-month milestones with deadlines
3. **Progress**: Visual completion percentage per month
4. **Reporting**: Easy to generate progress reports

---

## Issue Template

Each issue follows this structure:

```markdown
**Goal**: [One-sentence objective]

**Tasks**:
- [ ] Task 1
- [ ] Task 2
- [ ] Task 3

**Acceptance Criteria**:
- Criterion 1
- Criterion 2
- Criterion 3

**Dependencies**: [BGAI-XXX], [BGAI-YYY]
**References**: solo-developer-execution-plan.md Week X Day Y
**Tech Stack**: [If applicable]
**Links**: [Related issues, docs, PRs]
```

---

## Integration with Execution Plan

### solo-developer-execution-plan.md Structure

**Section 1: GitHub Issues Tracking** (NEW)
- Issue creation status
- All 85 issues listed by month
- Label and milestone setup instructions
- Issue generation script location

**Section 2: Month-by-Month Breakdown** (UPDATED)
- Each week now has day-by-day issue references
- Example: "Day 1: Setup LLMWhisperer account ([BGAI-001])"
- Quality Gates marked with issue references
- Deliverables tied to specific issues

**Section 3: Original Content** (UNCHANGED)
- Git worktree strategy
- Workflow patterns
- Optimization techniques
- Timeline estimates
- Success criteria

---

## Next Steps

### Immediate (This Week)
1. ✅ Create labels with script
2. ✅ Create milestones manually
3. ✅ Run issue creation script (Month 1)
4. ⏳ Review created issues
5. ⏳ Create Project board

### Week 2 (Before Sprint Start)
1. Create remaining issues (Months 2-6)
   - Option A: Manual creation
   - Option B: Generate Part 2 script
   - Option C: CSV bulk import
2. Setup Project board automation
3. Assign first week's issues to yourself
4. Review issue [BGAI-001] in detail

### Sprint 1 Start (Day 1)
1. Move [BGAI-001] to "In Progress"
2. Create feature branch `feature/bgai-001-llmwhisperer-setup`
3. Work on issue
4. Commit with message: "feat(bgai): setup LLMWhisperer account (#BGAI-001)"
5. Mark issue complete
6. Move to [BGAI-002]

---

## Tracking Metrics

### Week-by-Week (20 weeks total)
- Week 1: Issues [BGAI-001] to [BGAI-004] (4 issues)
- Week 2: Issues [BGAI-005] to [BGAI-008] (4 issues)
- Week 3: Issues [BGAI-009] to [BGAI-012] (4 issues)
- Week 4: Issues [BGAI-013] to [BGAI-015] (3 issues)
- ... (repeat for Months 2-6)

### Completion Rate Target
- **Month 1**: 15 issues / 20 days = 0.75 issues/day
- **Month 2**: 12 issues / 20 days = 0.60 issues/day
- **Month 3**: 13 issues / 20 days = 0.65 issues/day
- **Month 4**: 15 issues / 20 days = 0.75 issues/day
- **Month 5**: 14 issues / 20 days = 0.70 issues/day
- **Month 6**: 16 issues / 20 days = 0.80 issues/day
- **Average**: 85 issues / 120 days = **0.71 issues/day**

### Velocity Tracking
Track actual vs planned:
- **Planned**: 0.71 issues/day average
- **Actual**: Track in Project board
- **Adjustment**: If falling behind, identify bottlenecks

---

## Files Modified/Created

### Modified
1. `docs/org/solo-developer-execution-plan.md`
   - Added GitHub Issues Tracking section (top)
   - Updated all monthly breakdowns with day-by-day tracking
   - Linked each day to specific issues
   - Added Quality Gates

### Created
1. `tools/create-bgai-labels.sh`
   - Label creation script (17 labels)

2. `tools/create-bgai-issues.sh`
   - Issue generation script (Month 1, 15 issues)
   - Detailed descriptions and acceptance criteria

3. `docs/org/bgai-issue-tracking-summary.md` (this file)
   - Implementation summary
   - How-to guide
   - Templates and examples

---

## Success Criteria

### Issue Quality
- ✅ Each issue has clear goal statement
- ✅ Tasks are actionable and specific
- ✅ Acceptance criteria are measurable
- ✅ Dependencies are identified
- ✅ References link to execution plan

### Tracking Accuracy
- ✅ All 85 issues mapped to days 1-120
- ✅ No overlaps or gaps in day assignments
- ✅ Quality Gates marked at key milestones
- ✅ Month-by-month deliverables clear

### Documentation
- ✅ Scripts have usage instructions
- ✅ Execution plan updated and coherent
- ✅ Summary document comprehensive
- ✅ Templates provided for consistency

---

## Troubleshooting

### Issue Creation Fails
**Problem**: gh CLI authentication error
**Fix**: `gh auth login` and authenticate

**Problem**: Repository not found
**Fix**: Check `git config --get remote.origin.url`

**Problem**: Labels don't exist
**Fix**: Run `tools/create-bgai-labels.sh` first

### Script Errors
**Problem**: Permission denied
**Fix**: `chmod +x tools/create-bgai-*.sh`

**Problem**: Milestone not found
**Fix**: Create milestones manually first (Step 2)

---

## Resources

### Documentation
- **Execution Plan**: `docs/org/solo-developer-execution-plan.md`
- **Architecture**: `docs/architecture/board-game-ai-architecture-overview.md`
- **Quick Start**: `docs/board-game-ai-QUICK-START.md`

### Scripts
- **Labels**: `tools/create-bgai-labels.sh`
- **Issues (M1)**: `tools/create-bgai-issues.sh`
- **Issues (M2-6)**: TBD

### GitHub
- **Issues**: Filter by label `board-game-ai`
- **Milestones**: Month 1-6
- **Project**: "Board Game AI - Phase 1A" (to be created)

---

**Version**: 1.0
**Last Updated**: 2025-01-15
**Next Review**: Before Sprint 1 Start (Week 2)
