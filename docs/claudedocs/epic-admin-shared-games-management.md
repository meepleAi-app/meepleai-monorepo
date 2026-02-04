# Epic: Admin Shared Games Management with PDF Workflow

**Issue Type**: Epic
**Priority**: High
**Labels**: `epic`, `admin`, `editor`, `backend`, `frontend`, `shared-games`

---

## Overview

Comprehensive admin/editor workflow for managing the shared games catalog, including:
- BGG import (single + bulk)
- PDF upload and RAG processing approval
- Game metadata CRUD operations
- Approval queue for pending games
- Role-based permissions (Admin vs Editor)

## Context

**Current State**:
- ✅ Backend commands exist (`ImportGameFromBggCommand`, `AddDocumentToSharedGameCommand`, etc.)
- ✅ Approval workflow partially implemented (`ApproveSharedGamePublicationCommand`)
- ❌ No unified admin UI for catalog management
- ❌ No PDF upload interface with approval workflow
- ❌ No BGG import UI (only API endpoints)

**Target Users**:
- **Admins**: Full control (import, upload, approve, publish)
- **Editors**: Can import and upload, but PDFs require admin approval for RAG processing

---

## User Stories

### Epic-Level Stories

**As an Admin**, I want a centralized dashboard to manage shared games, so I can efficiently maintain the community catalog.

**As an Editor**, I want to import games from BGG and upload PDFs, so I can contribute to the catalog (pending admin approval).

**As an Admin**, I want to approve PDF uploads before RAG processing, so I can ensure quality and prevent abuse.

---

## Requirements

### Functional Requirements

1. **Dashboard Page**
   - Grid/card view of all shared games
   - Status badges (Draft, Pending, Published, Processing)
   - Filters: status, date, submitter, has-PDFs
   - Search: title, BGG ID, publisher
   - Stats cards: total games, pending approvals, PDF coverage, avg review time

2. **BGG Import**
   - Modal form with BGG ID/URL input
   - Live preview of fetched metadata
   - Auto-submit for approval option
   - Rate limit warnings
   - Bulk import capability (separate issue)

3. **Game Detail Page**
   - Tabbed interface: Details | Documents | Review History
   - Metadata display (players, time, complexity, etc.)
   - PDF upload section with drag-drop
   - List existing documents with status (Processed, Awaiting Approval)
   - Editor/Admin permission-aware UI

4. **PDF Upload Workflow**
   - **Editors**: Can upload → status "Awaiting Approval"
   - **Admins**: Can upload → immediate processing OR approve pending uploads
   - RAG processing triggers ONLY after admin approval
   - Visual feedback: upload progress, processing status

5. **Approval Queue**
   - List view of pending games (submitted by editors)
   - Filter by: urgency (>7 days), submitter, has-PDFs
   - Bulk actions: approve multiple, reject with reason
   - Preview game before approving
   - SLA indicators (days pending)

6. **Role-Based Permissions**
   | Action | Editor | Admin |
   |--------|--------|-------|
   | Import from BGG | ✅ (→ Draft) | ✅ (→ Draft or Published) |
   | Upload PDF | ✅ (→ Awaiting Approval) | ✅ (→ Immediate Processing) |
   | Approve Publication | ❌ | ✅ |
   | Approve PDF for RAG | ❌ | ✅ |
   | Edit Metadata | ✅ (→ Draft if Published) | ✅ (Stays Published) |
   | Delete Game | ❌ (Request only) | ✅ |

### Non-Functional Requirements

- **Performance**: Dashboard loads <2s with 1000+ games (pagination)
- **UX**: Clear status indicators, no ambiguous states
- **Accessibility**: WCAG 2.1 AA compliant forms and navigation
- **Security**: Role checks on all mutations, file upload validation
- **Monitoring**: Track approval times, PDF processing failures

---

## Technical Architecture

### Frontend (Next.js 15 + React 19)

**Pages/Routes**:
```
/admin/shared-games                  # Dashboard (grid view)
/admin/shared-games/[id]             # Game detail
/admin/shared-games/approval-queue   # Approval queue
```

**Components**:
- `SharedGamesDashboard.tsx` - Main grid with filters/search
- `ImportFromBggModal.tsx` - BGG import form
- `GameDetailPage.tsx` - Tabbed detail view
- `PdfUploadSection.tsx` - Drag-drop upload with status
- `ApprovalQueueList.tsx` - Pending games list

**State Management**:
- React Query for server state (games list, detail, approvals)
- Zustand for UI state (filters, selected items, modal open/close)

### Backend (.NET 9 + ASP.NET Minimal APIs)

**New Endpoints** (if needed):
- `GET /api/v1/admin/shared-games?status=&search=` - List with filters
- `GET /api/v1/admin/shared-games/{id}/documents` - List PDFs with status
- `POST /api/v1/admin/shared-games/{id}/documents/approve` - Approve PDF for RAG
- `GET /api/v1/admin/shared-games/approval-queue` - Pending approvals

**Existing Commands** (reuse):
- `ImportGameFromBggCommand` (single import)
- `BulkImportGamesCommand` (bulk import - separate issue)
- `AddDocumentToSharedGameCommand` (PDF upload)
- `ApproveSharedGamePublicationCommand` (approve game)
- `UpdateSharedGameCommand` (edit metadata)

**New Command** (if needed):
- `ApproveDocumentForRagProcessingCommand` - Approve PDF → trigger embedding

### Database Schema

**Existing Tables** (SharedGameCatalog context):
- `SharedGameEntity` - Main game entity
- `GameDocumentEntity` - PDFs/documents
- `GameDocumentVersionEntity` - Document versions

**New Fields** (if needed):
- `GameDocumentEntity.ApprovalStatus` (enum: Pending, Approved, Rejected)
- `GameDocumentEntity.ApprovedBy` (UserId)
- `GameDocumentEntity.ApprovedAt` (DateTime)

---

## Implementation Plan

### Phase 1: Backend API Completion (3-5 days)
- ✅ Verify existing commands cover all needs
- Create missing endpoints for:
  - Filtered games list
  - Document approval workflow
  - Approval queue queries
- Add `ApprovalStatus` to `GameDocumentEntity` if missing
- Write integration tests for new endpoints

### Phase 2: Admin Dashboard UI (5-7 days)
- Create dashboard page with games grid
- Implement filters and search
- Add stats cards (total, pending, etc.)
- Create Import from BGG modal
- Add role-based permission checks

### Phase 3: Game Detail + PDF Upload (5-7 days)
- Create game detail page (tabbed interface)
- Build PDF upload component (drag-drop)
- Show existing documents with status
- Implement approval UI for admins
- Add processing status indicators

### Phase 4: Approval Queue (3-5 days)
- Create approval queue page
- List pending games with metadata
- Add bulk approve/reject actions
- Implement urgency indicators (>7 days)
- Add review history tracking

### Phase 5: Testing & Polish (3-5 days)
- E2E tests for full workflows
- Visual regression tests
- Performance optimization (pagination, caching)
- Accessibility audit
- Documentation (admin guide)

**Total Estimated Time**: 19-29 days (4-6 weeks)

---

## Sub-Issues Breakdown

1. **[Backend] Admin Shared Games API Endpoints** (#TBD)
   - Filtered games list endpoint
   - Document approval workflow
   - Approval queue queries

2. **[Frontend] Admin Dashboard - Games Grid** (#TBD)
   - Dashboard page with card grid
   - Filters, search, pagination
   - Stats cards

3. **[Frontend] Import from BGG Modal** (#TBD)
   - Modal form component
   - BGG ID/URL validation
   - Live metadata preview
   - Error handling (rate limits)

4. **[Frontend] Game Detail Page** (#TBD)
   - Tabbed interface
   - Metadata display
   - Edit metadata form

5. **[Frontend] PDF Upload Section** (#TBD)
   - Drag-drop upload component
   - Existing documents list
   - Status indicators (Pending, Processing, Complete)
   - Approval UI for admins

6. **[Frontend] Approval Queue Page** (#TBD)
   - List view with filters
   - Bulk actions
   - Urgency indicators
   - Preview modal

7. **[Testing] E2E Tests - Admin Workflow** (#TBD)
   - Import game from BGG
   - Upload PDF as editor
   - Approve PDF as admin
   - Verify RAG processing triggered

8. **[Docs] Admin Guide - Shared Games Management** (#TBD)
   - User guide for admins
   - Screenshots and workflows
   - Troubleshooting

---

## Acceptance Criteria

### Epic-Level Success Criteria

- [ ] Admins can import games from BGG via UI
- [ ] Editors can import games → Draft status
- [ ] Editors can upload PDFs → Awaiting Approval status
- [ ] Admins can approve PDFs → RAG processing triggered
- [ ] Approval queue shows all pending games
- [ ] Dashboard reflects real-time status changes
- [ ] Role-based permissions enforced on all actions
- [ ] E2E tests cover happy paths and edge cases
- [ ] Performance: Dashboard loads <2s with 1000+ games
- [ ] Accessibility: WCAG 2.1 AA compliance verified

---

## Dependencies

- ✅ Existing backend commands (`ImportGameFromBggCommand`, etc.)
- ✅ BGG API service (`IBggApiService`)
- ✅ Document processing pipeline (PDF upload → Qdrant)
- ⚠️ Role-based authorization middleware (verify implementation)
- ⚠️ Document approval status tracking (add if missing)

---

## Risks & Mitigations

| Risk | Impact | Mitigation |
|------|--------|------------|
| BGG API rate limits | High | Implement queue system, show warnings, cache results |
| Large PDF uploads | Medium | Implement chunked uploads, progress indicators, size limits |
| RAG processing failures | High | Retry mechanism, admin notifications, manual retrigger UI |
| Performance (1000+ games) | Medium | Pagination, virtual scrolling, aggressive caching |
| Role permission bugs | High | Comprehensive integration tests, code reviews |

---

## Out of Scope (Future Enhancements)

- Bulk BGG import UI (separate epic)
- Advanced approval workflows (multi-reviewer, conditional approval)
- Game versioning system
- Automated quality checks (image resolution, description length)
- Admin analytics dashboard (approval metrics, processing times)
- Notification system (email/push for pending approvals)

---

## References

- [User Flow: Admin Approval Workflow](../11-user-flows/admin-role/01-approval-workflow.md)
- [ADR: Document Collections](../01-architecture/adr/adr-026-document-collections.md)
- [Mockup: Admin Shared Games Catalog](../mockups/admin-shared-games-catalog.html)
- [Backend: SharedGameCatalog Commands](../../apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/)

---

**Created**: 2026-02-04
**Last Updated**: 2026-02-04
**Status**: Draft (Awaiting Issue Creation)
