# Brainstorming Session: Admin Shared Games Management

**Date**: 2026-02-04
**Participants**: User + Claude (SuperClaude Brainstorming Mode)
**Duration**: ~45 minutes
**Outcome**: Epic #3532 + 7 sub-issues created

---

## 📋 Initial Request

**User Query**: "Ci sono issue aperte o da creare per eseguire il flusso admin gestisce shared games, carica pdf?"

**Translation**: "Are there open issues or should we create them for the admin workflow: manage shared games, upload PDF?"

---

## 🔍 Discovery Process

### Round 1: Context Analysis
**Actions**:
- ✅ Checked recent commits (found `ImportGameFromBggCommandHandler`, `AddDocumentToSharedGameCommand`)
- ✅ Verified existing issues (no duplicates found)
- ✅ Analyzed backend commands (129 existing commands in `SharedGameCatalog`)
- ✅ Reviewed existing user flows (`docs/11-user-flows/admin-role/01-approval-workflow.md`)

**Findings**:
- Backend commands ALREADY exist for most operations
- Approval workflow partially documented
- NO unified admin UI
- NO PDF upload interface
- NO BGG import UI (only API endpoints)

### Round 2: Requirements Elicitation

**Discovery Questions Asked**:

1. **Shared Games Management Scope** → What admin operations needed?
2. **PDF Upload Context** → When/how are PDFs uploaded?
3. **Workflow Trigger** → Where does admin start the workflow?
4. **Permissions** → Who can do what (Editor vs Admin)?

### Round 3: Collaborative Discovery

**User Responses**:

**Q1: Punto di Partenza?**
→ **A: Option A** - Dashboard Admin → Sezione "Shared Games Catalog"

**Q2: Workflow Upload PDF?**
→ **A: Options A + B** - Durante creazione AND post-creazione

**Q3: BGG Import + PDF?**
→ **C: PDF opzionale, caricamento manuale possibile**

**Q4: Chi può fare cosa?**
→ Detailed permission matrix:
- Import BGG: Editor + Admin (warning: BGG rate limits!)
- Upload PDF: Editor + Admin (Editor requires approval for embedding)
- Approve: Admin only
- Metadata edits: Both (but published game → Draft if Editor edits)

**Q5: Scenario Preference?**
→ **C: Hybrid** - Admin direct publish, Editor submit for approval

---

## 💡 Key Insights Discovered

### Permission Model Clarity
```
Editor Powers:
  ✅ Import from BGG → Creates Draft
  ✅ Upload PDF → Status "Awaiting Approval"
  ✅ Edit metadata → Draft state if published
  ❌ Approve publications
  ❌ Approve PDFs for RAG processing

Admin Powers:
  ✅ All Editor capabilities
  ✅ Approve publications → Published
  ✅ Approve PDFs → Trigger RAG
  ✅ Edit published metadata → Stays published
  ✅ Delete games
```

### Approval Scope Refinement
- **PDFs**: Require approval for RAG embedding (cost control, quality)
- **Metadata**: No approval needed, just Draft status
- **Published Edits**: Editor edits → Draft, Admin edits → Stays published

### BGG Integration Nuances
- Rate limits are critical concern
- Both roles can import (democratized)
- Warning UI needed to prevent abuse
- Bulk import is separate epic (out of scope)

---

## 🎨 Design Decisions

### Aesthetic Direction
**Chosen**: Professional Editorial Data-Dense

**Rationale**:
- Target users: Power users (admins/editors) who need efficiency
- Context: Content management (not consumer-facing)
- Inspiration: Sanity, Contentful, Strapi (high-end CMS)

### Key Design Choices
- **Typography**: Fraunces (serif display) + Inter (sans body) - Editorial refinement
- **Color Palette**: Warm stone neutrals + deep teal accent (board game themed)
- **Layout**: Card-based grid with generous spacing (NOT cramped tables)
- **Status System**: Context-aware colors (not generic green/red)
  - Draft: Orange (#f59e0b)
  - Pending: Purple (#8b5cf6)
  - Approved: Green (#10b981)
  - Processing: Blue (#3b82f6)

### UX Principles
- Clear status indicators at all times
- Contextual actions based on role
- Progressive disclosure (don't overwhelm)
- Real-time feedback (uploads, processing)

---

## 📦 Deliverables Created

### Documentation
1. **Epic Spec**: `docs/claudedocs/epic-admin-shared-games-management.md`
2. **Implementation Summary**: `docs/claudedocs/admin-shared-games-implementation-summary.md`
3. **Discovery Session**: `docs/claudedocs/brainstorming/2026-02-04-admin-shared-games-discovery.md` (this file)

### Mockup
4. **Interactive HTML Mockup**: `docs/mockups/admin-shared-games-catalog.html`
   - Dashboard with games grid
   - Import from BGG modal
   - Game detail with PDF upload
   - Approval queue

### GitHub Issues
5. **Epic #3532**: [Epic] Admin Shared Games Management with PDF Workflow
6. **Issue #3533**: [Backend] Admin Shared Games API Endpoints
7. **Issue #3534**: [Frontend] Admin Dashboard - Games Grid with Filters
8. **Issue #3535**: [Frontend] Import from BGG Modal Component
9. **Issue #3536**: [Frontend] Game Detail Page with PDF Upload Section
10. **Issue #3537**: [Frontend] Approval Queue Page with Bulk Actions
11. **Issue #3538**: [Testing] E2E Tests - Admin Shared Games Workflow
12. **Issue #3539**: [Docs] Admin Guide - Shared Games Management

---

## 🎯 Scope Boundaries

### In Scope (Epic #3532)
✅ Single game BGG import UI
✅ PDF upload with drag-drop
✅ Admin approval workflow for PDFs
✅ Approval queue with bulk actions
✅ Dashboard with filters and search
✅ Role-based permissions (Editor/Admin)
✅ E2E tests and documentation

### Out of Scope (Future Work)
❌ Bulk BGG import UI (separate epic)
❌ Advanced approval workflows (multi-reviewer)
❌ Game versioning system
❌ Automated quality checks (image resolution)
❌ Admin analytics dashboard
❌ Email/push notifications

---

## 📊 Success Criteria (Recap)

### Epic-Level Goals
- [ ] Admins can manage shared games catalog via UI
- [ ] Editors can contribute (import, upload) with approval workflow
- [ ] PDF uploads trigger RAG processing after admin approval
- [ ] Approval queue enables efficient bulk reviews
- [ ] Role-based permissions enforced correctly
- [ ] Performance: Dashboard <2s with 1000+ games
- [ ] Quality: E2E tests pass, WCAG 2.1 AA compliant

### Business Metrics
- Avg approval time: <3 days (currently 2.4d)
- PDF coverage: Target 75% (currently 66%)
- Editor contribution rate: +30% increase

---

## 🔄 Next Steps

1. **Review Epic #3532** with team/stakeholders
2. **Validate Mockup** (`docs/mockups/admin-shared-games-catalog.html`)
3. **Prioritize Issues** - Adjust timeline based on business needs
4. **Start Implementation** - Begin with #3533 (backend foundation)

---

## 🧠 Lessons Learned

### What Worked Well
- **Systematic discovery questions** uncovered permission nuances early
- **Scenario validation** (A/B/C options) clarified hybrid approach quickly
- **Visual mockup** provided concrete reference for discussion
- **Checking existing code FIRST** prevented duplicate work

### Key Decisions Made
- **Hybrid scenario** balances admin control with editor contribution
- **PDF approval gate** prevents abuse while enabling collaboration
- **Card-based UI** prioritizes usability over data density
- **Incremental implementation** (7 issues) allows parallel work

### Risk Mitigations Identified
- BGG rate limits → Warning UI + queuing system
- Large PDFs → Chunked uploads + progress indicators
- Permission bugs → Comprehensive integration tests
- Performance → Pagination + caching

---

## 📎 Artifacts

**Live Mockup**: Open `docs/mockups/admin-shared-games-catalog.html` in browser

**GitHub Epic**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/3532

**Issue Links**:
- Backend: #3533
- Frontend Dashboard: #3534
- Frontend Import: #3535
- Frontend Detail: #3536
- Frontend Queue: #3537
- Testing: #3538
- Docs: #3539

---

**Session Status**: ✅ Complete - Ready for Implementation
