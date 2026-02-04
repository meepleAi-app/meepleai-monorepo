# Admin Shared Games Management - Implementation Summary

**Created**: 2026-02-04
**Epic Issue**: #3532
**Status**: Ready for Implementation

---

## 🎯 Scope Definition

### What We're Building
Unified admin/editor interface for managing the shared games catalog with:
1. **BGG Import** - Single game import via UI (bulk import separate)
2. **PDF Upload & Approval** - Editor uploads → Admin approves → RAG processing
3. **Approval Queue** - Admin reviews pending games/PDFs with bulk actions
4. **Role-Based Permissions** - Editors create/upload, Admins approve/publish

### Key Requirements Discovered

**✅ Confirmed**:
- **Scenario C (Hybrid)**: Admins publish directly, Editors submit for approval
- **BGG Import**: Both Editor and Admin can import (rate limit warning needed)
- **PDF Upload**: Both can upload, but Editor uploads require admin approval for RAG
- **Approval Scope**: Only PDFs need approval, metadata edits don't
- **Draft Behavior**: Editing published game → Draft status (requires re-approval)

**🎨 Design**:
- Professional editorial aesthetic (serif display + modern sans)
- Card-based grid (not tables)
- Status-driven color system
- Mockup: `docs/mockups/admin-shared-games-catalog.html`

---

## 📊 Issues Created

| Issue | Type | Component | ETA | Priority |
|-------|------|-----------|-----|----------|
| [#3532](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3532) | Epic | Overall | 5-7w | High |
| [#3533](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3533) | Backend | API Endpoints | 3-5d | High |
| [#3534](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3534) | Frontend | Dashboard Grid | 5-7d | High |
| [#3535](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3535) | Frontend | Import Modal | 3-5d | Medium |
| [#3536](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3536) | Frontend | Detail + Upload | 5-7d | High |
| [#3537](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3537) | Frontend | Approval Queue | 3-5d | Medium |
| [#3538](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3538) | Testing | E2E Tests | 3-5d | High |
| [#3539](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3539) | Docs | Admin Guide | 2-3d | Low |

**Total**: 24-37 days (5-7 weeks estimated)

---

## 🔄 Workflow Summary

### Editor Workflow
```
1. Login as Editor
2. Navigate to /admin/shared-games
3. Click "Import from BGG"
4. Enter BGG ID → Fetch metadata → Submit
5. Game created with status "Draft"
6. Navigate to game detail
7. Upload PDF → Status "Awaiting Approval"
8. Wait for admin approval
9. Once approved → RAG processing → PDF "Processed"
```

### Admin Workflow (Direct Publish)
```
1. Login as Admin
2. Import from BGG (same as Editor)
3. Upload PDF → Immediate RAG processing (or manual approval)
4. Click "Approve & Publish"
5. Game status → "Published"
6. Appears in public catalog
```

### Admin Workflow (Review Queue)
```
1. Navigate to /admin/shared-games/approval-queue
2. See list of pending games (submitted by editors)
3. Preview game → Check metadata, PDFs
4. Bulk select 3 games
5. Click "Bulk Approve"
6. Games → "Published" status
7. Editors notified
```

---

## 🏗️ Architecture Overview

### Frontend Routes
```
/admin/shared-games                  # Dashboard (grid view)
/admin/shared-games/[id]             # Game detail (tabs)
/admin/shared-games/approval-queue   # Approval queue
```

### Key Components
```
SharedGamesDashboard.tsx            # Main grid with filters
ImportFromBggModal.tsx              # BGG import form
GameDetailPage.tsx                  # Tabbed detail view
PdfUploadSection.tsx                # Drag-drop upload + list
ApprovalQueuePage.tsx               # Pending approvals
GameStatusBadge.tsx                 # Reusable status indicator
```

### Backend Endpoints

**Existing** (Reuse):
- `POST /api/v1/shared-games/import-from-bgg` - Single import
- `POST /api/v1/shared-games/{id}/documents` - Upload PDF
- `POST /api/v1/admin/shared-games/{id}/approve-publication` - Approve game

**New** (Create in #3533):
- `GET /api/v1/admin/shared-games?status=&search=` - Filtered list
- `POST /api/v1/admin/shared-games/{id}/documents/{docId}/approve` - Approve PDF
- `GET /api/v1/admin/shared-games/approval-queue` - Pending queue

### Database Schema Changes

**Add to `GameDocumentEntity`**:
```csharp
public DocumentApprovalStatus ApprovalStatus { get; set; } = DocumentApprovalStatus.Pending;
public Guid? ApprovedBy { get; set; }
public DateTime? ApprovedAt { get; set; }
public string? ApprovalNotes { get; set; }
```

**Create Enum**:
```csharp
public enum DocumentApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}
```

---

## 🔑 Permission Matrix

| Action | Editor | Admin | Resulting Status |
|--------|--------|-------|------------------|
| Import from BGG | ✅ | ✅ | Draft |
| Upload PDF | ✅ | ✅ | Editor: Pending / Admin: Processing |
| Approve PDF for RAG | ❌ | ✅ | Processing → Processed |
| Approve Game Publication | ❌ | ✅ | Published |
| Edit Metadata (Draft) | ✅ | ✅ | Stays Draft |
| Edit Metadata (Published) | ✅ (→ Draft) | ✅ (Stays Published) | Admin bypass |
| Delete Game | ❌ (Request only) | ✅ | Deleted/Archived |

---

## 📐 Technical Decisions

### PDF Approval Trigger
- **Decision**: PDFs uploaded by Editors have `ApprovalStatus = Pending`
- **Trigger**: Admin calls `/documents/{docId}/approve` → RAG processing starts
- **Why**: Prevents abuse, ensures quality, controls processing costs

### Status State Machine
```
Game States:
Draft → Pending Approval → Published
       ↓ (if rejected)
      Rejected → Draft (if re-submitted)

Document States:
Pending → Approved → Processing → Processed
        ↓ (if rejected)
       Rejected
```

### BGG Rate Limits
- **Constraint**: BGG API rate limits are strict
- **Solution**:
  - Show warning in UI
  - Implement request queuing (backend)
  - Cache BGG responses (24h TTL)
  - Separate issue for bulk import optimization

---

## 🧪 Testing Strategy

### Unit Tests (Backend)
- Command handlers for new endpoints
- Validation logic for approval workflow
- Permission checks (Editor vs Admin)

### Component Tests (Frontend)
- Dashboard rendering with filters
- Import modal validation
- Upload component file handling
- Approval queue bulk selection

### Integration Tests (Backend)
- Full approval workflow (upload → approve → RAG trigger)
- Role-based permission enforcement
- BGG import error handling

### E2E Tests (Playwright)
- Editor imports game → Admin approves
- Editor uploads PDF → Admin approves → RAG processes
- Bulk approval workflow
- Role permission boundaries

---

## 🚀 Implementation Order

**Recommended sequence**:

1. **#3533 (Backend API)** - Foundation for all frontend work
   - Add `ApprovalStatus` to database
   - Create missing endpoints
   - Write integration tests

2. **#3534 (Dashboard)** - Core admin interface
   - Games grid with filters
   - Stats cards
   - Navigation foundation

3. **#3535 (Import Modal)** - Primary workflow enabler
   - BGG import form
   - Error handling
   - Integration with dashboard

4. **#3536 (Game Detail + Upload)** - Critical feature
   - PDF upload UI
   - Approval workflow UI
   - Document status tracking

5. **#3537 (Approval Queue)** - Admin efficiency tool
   - Pending games list
   - Bulk actions
   - Urgency indicators

6. **#3538 (E2E Tests)** - Quality gate
   - Full workflow coverage
   - Role permission tests

7. **#3539 (Documentation)** - Final polish
   - Admin user guide
   - Screenshots

---

## 📝 Definition of Done (Epic)

**Epic is complete when**:
- [ ] All 7 sub-issues closed and merged
- [ ] Admins can import games from BGG via UI
- [ ] Editors can import games → Draft status
- [ ] Editors can upload PDFs → Awaiting Approval
- [ ] Admins can approve PDFs → RAG processing triggered
- [ ] Approval queue shows pending items with bulk actions
- [ ] Dashboard reflects real-time status changes
- [ ] Role-based permissions enforced and tested
- [ ] E2E tests pass in CI/CD
- [ ] Admin guide published
- [ ] Performance: Dashboard <2s with 1000+ games
- [ ] Accessibility: WCAG 2.1 AA verified

---

## 🎯 Success Metrics

**Efficiency**:
- Avg approval time: Target <3 days (currently 2.4d baseline)
- Games imported per week: +20% increase
- PDF coverage: Target 75% (currently 66%)

**Quality**:
- PDF processing success rate: >95%
- Zero permission bypass bugs
- E2E test pass rate: >99%

**Adoption**:
- Admin daily active usage: >80%
- Editor contribution rate: +30%
- Approval queue cleared daily

---

## 🔗 References

- **Epic Issue**: https://github.com/DegrassiAaron/meepleai-monorepo/issues/3532
- **Mockup**: `docs/mockups/admin-shared-games-catalog.html`
- **Epic Spec**: `docs/claudedocs/epic-admin-shared-games-management.md`
- **Existing User Flow**: `docs/11-user-flows/admin-role/01-approval-workflow.md`
- **Backend Commands**: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/`

---

## 🚧 Next Actions

1. **Start with #3533 (Backend API)** - Blocking for all frontend work
2. **Review Epic** - Get team feedback on scope and approach
3. **Design Review** - Validate mockup with stakeholders
4. **Prioritize** - Adjust timeline based on business needs
