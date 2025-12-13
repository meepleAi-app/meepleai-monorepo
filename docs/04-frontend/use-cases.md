# Frontend Use Cases & User Journeys

**Status**: Production  
**Owner**: Frontend Team + Product  
**Last Updated**: 2025-12-13T10:59:23.970Z

---

## Primary User Personas

### 1. Board Game Player (Italian Speaker)
**Goal**: Get accurate rule clarifications during gameplay  
**Tech Savviness**: Low to Medium  
**Device**: Primarily mobile (80%), tablet (15%), desktop (5%)  
**Context**: Active game session, needs quick answers

### 2. Admin User
**Goal**: Manage system, users, and content  
**Tech Savviness**: High  
**Device**: Desktop (90%), tablet (10%)  
**Context**: Office hours, system maintenance

### 3. Content Contributor
**Goal**: Upload and manage rulebook PDFs  
**Tech Savviness**: Medium  
**Device**: Desktop (70%), tablet (30%)  
**Context**: Content preparation, batch uploads

---

## Core Use Cases

### UC-001: Ask Italian Board Game Rule Question

**Primary Actor**: Board Game Player  
**Preconditions**:
- Rulebook uploaded and indexed
- User has active session (optional for public beta)

**Main Success Scenario**:
1. User navigates to `/chat`
2. User selects game from catalog (autocomplete)
3. User types question in Italian
4. System retrieves relevant chunks (hybrid search)
5. System validates answer (5-layer defense)
6. System displays answer with citation (page number + snippet)
7. IF confidence ≥0.70, display answer
8. IF confidence <0.70, display explicit uncertainty message

**Extensions**:
- 3a. No relevant chunks found → suggest manual rulebook search
- 5a. Multi-model disagreement → escalate to human review queue
- 6a. Citation page number mismatch → flag for admin review

**Acceptance Criteria**:
- Answer displayed within 3s (P95)
- Citation always includes page number
- Confidence score visible to user
- Italian language throughout

---

### UC-002: Upload Rulebook PDF

**Primary Actor**: Content Contributor  
**Preconditions**: Authenticated user with contributor role

**Main Success Scenario**:
1. User navigates to `/upload`
2. User selects PDF file(s) (drag-drop or file picker)
3. System validates PDF (size <50MB, format valid)
4. User selects game from catalog OR enters new game metadata
5. System uploads to backend
6. System processes PDF (3-stage fallback: Unstructured → SmolDocling → Docnet)
7. System displays quality report
8. IF quality ≥0.80, mark ready for indexing
9. System indexes to Qdrant (vector embeddings)

**Extensions**:
- 3a. Invalid PDF → display error, allow retry
- 7a. Quality <0.80 → display recommendations, allow manual review
- 8a. Duplicate detected → prompt merge or replace

**Acceptance Criteria**:
- Multi-file upload supported (up to 10 files)
- Progress indicator for each file
- Quality report generated within 30s
- Clear error messages for failures

---

### UC-003: Language Switching

**Primary Actor**: Board Game Player  
**Preconditions**: None

**Main Success Scenario**:
1. User clicks language switcher (IT/EN toggle)
2. System saves preference to backend (if authenticated)
3. System reloads page with new locale
4. All UI text updates to selected language
5. RAG content preserves original language (mixed-language support)

**Acceptance Criteria**:
- Language switch <500ms
- Preference persists across sessions
- No data loss during switch
- SEO hreflang tags updated

---

## Mobile-Specific Use Cases

### UC-M01: Mobile Game Night Assistance

**Context**: User playing board game at friend's house, poor Wi-Fi

**Journey**:
1. Open MeepleAI on mobile browser
2. App detects slow connection → enables lite mode
3. Search recent questions (cached)
4. Ask new question → optimistic UI (show loading state)
5. IF offline → queue question for when online
6. Receive answer → cache for offline access

**Requirements**:
- Responsive design (mobile-first)
- Offline queue for questions
- Cached recent conversations
- Touch-optimized UI

---

## Error Recovery Scenarios

### E-001: Backend API Unavailable

**Scenario**: User asks question, backend returns 503

**Recovery Path**:
1. Frontend displays user-friendly error: "Il servizio è temporaneamente non disponibile"
2. Offer retry button
3. Suggest cached previous answers
4. Show backend status page link

---

## Future Use Cases

- **UC-004**: Multilingual Rule Comparison (compare Italian vs English rules)
- **UC-005**: Community Q&A (upvote/downvote answers)
- **UC-006**: Rule Clarification History (track common questions per game)

---

**See Also**:
- [Architecture](./architecture.md) - Technical implementation
- [Internationalization Strategy](./internationalization-strategy.md) - i18n details
- [Testing Strategy](./testing-strategy.md) - How use cases are tested

---

**Maintained by**: Frontend Team + Product  
**Review Frequency**: Quarterly

