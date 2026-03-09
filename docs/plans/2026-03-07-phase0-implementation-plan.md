# Implementation Plan: Phase 0 — Session Photo Attachments

**Epic**: #5358
**Issues**: #5359–#5374 (16 issues)
**Branch**: `main-dev`
**Date**: 2026-03-07
**Method**: `/implementa` per issue with dependency-ordered execution

---

## Dependency Graph

```
Layer 1 (Foundation — no deps):
  #5359 [SP-01] Domain entity + value objects + repository

Layer 2 (Persistence — depends on L1):
  #5360 [SP-02] EF Core config + migration

Layer 3 (Services — depends on L2):
  #5361 [SP-03] ISessionAttachmentService (upload, thumbnail, S3)

Layer 4 (CQRS — depends on L3, can parallelize):
  #5362 [SP-04] UploadSessionAttachment command ──┐
  #5363 [SP-05] List/Get queries                  ├── parallel
  #5364 [SP-06] Delete command                    ──┘

Layer 5 (API + Background — depends on L4):
  #5365 [SP-07] API endpoints          ──┐
  #5366 [SP-08] Cleanup background job  ├── parallel
  #5367 [SP-09] Snapshot metadata ext.  ──┘

Layer 6 (Frontend — depends on L5 endpoints):
  #5368 [SP-10] PhotoUploadModal       ──┐
  #5369 [SP-11] SessionPhotoGallery     ├── parallel
  #5370 [SP-12] ResumePhotoReview      ──┘

Layer 7 (Integration — depends on L6):
  #5371 [SP-13] ToolRail integration   ──┐
  #5372 [SP-14] Session pause flow     ──┘ parallel

Layer 8 (Testing — depends on all):
  #5373 [SP-15] Backend tests          ──┐
  #5374 [SP-16] Frontend tests + E2E   ──┘ parallel
```

---

## Execution Order

### Sprint 1: Backend Foundation (Issues 1-3, sequential)

#### 1. `/implementa #5359 --base-branch main-dev`
**[SP-01] SessionAttachment domain entity + value objects + repository**

- **Scope**: Domain layer only, no persistence
- **Key files to create**:
  - `BoundedContexts/GameManagement/Domain/Entities/SessionAttachment.cs`
  - `BoundedContexts/GameManagement/Domain/ValueObjects/AttachmentType.cs`
  - `BoundedContexts/GameManagement/Domain/Repositories/ISessionAttachmentRepository.cs`
- **Reference pattern**: `SessionMedia.cs` (SessionTracking BC) — same FileId/ThumbnailFileId string pattern
- **Estimated complexity**: Low
- **Branch**: `feature/issue-5359-session-attachment-entity`

#### 2. `/implementa #5360 --base-branch main-dev`
**[SP-02] EF Core configuration + migration**

- **Scope**: Infrastructure persistence layer
- **Key files to create**:
  - `Infrastructure/Configurations/GameManagement/SessionAttachmentEntityConfiguration.cs`
  - `Infrastructure/Entities/GameManagement/SessionAttachmentEntity.cs`
  - `Infrastructure/Repositories/GameManagement/SessionAttachmentRepository.cs`
  - Migration: `AddSessionAttachments`
- **Reference pattern**: `SessionMediaEntityConfiguration.cs` — snake_case, soft delete filter, cascade
- **DB schema**: `docs/plans/2026-03-07-publisher-db-schema.md` (lines 14-63)
- **Branch**: `feature/issue-5360-session-attachment-ef`

#### 3. `/implementa #5361 --base-branch main-dev`
**[SP-03] ISessionAttachmentService — upload, thumbnail, S3**

- **Scope**: Application service wrapping IBlobStorageService
- **Key files to create**:
  - `BoundedContexts/GameManagement/Application/Services/ISessionAttachmentService.cs`
  - `BoundedContexts/GameManagement/Application/Services/SessionAttachmentService.cs`
- **Reference pattern**: `IBlobStorageService` — StoreAsync returns BlobStorageResult with FileId
- **Key decisions**:
  - Storage path: `session_photos/{sessionId}/{fileId}_{sanitizedFileName}` (not `pdf_uploads/`)
  - Thumbnail: Resize to 300x300 max (use System.Drawing or ImageSharp)
  - Content type validation: image/jpeg, image/png only
  - File size limit: 1KB–10MB
- **Branch**: `feature/issue-5361-attachment-service`

---

### Sprint 2: CQRS Commands/Queries (Issues 4-6, parallelizable)

#### 4. `/implementa #5362 --base-branch main-dev`
**[SP-04] UploadSessionAttachment command + handler + validator**

- **Scope**: CQRS command with MediatR
- **Key files**:
  - `Application/Commands/UploadSessionAttachmentCommand.cs`
  - `Application/Commands/UploadSessionAttachmentCommandHandler.cs`
  - `Application/Commands/UploadSessionAttachmentCommandValidator.cs`
- **Validation**: Session exists + active/paused, player is member, content type, file size
- **Branch**: `feature/issue-5362-upload-attachment-cmd`

#### 5. `/implementa #5363 --base-branch main-dev`
**[SP-05] List/Get session attachment queries**

- **Scope**: CQRS queries
- **Key files**:
  - `Application/Queries/GetSessionAttachmentsQuery.cs` (list by session)
  - `Application/Queries/GetSessionAttachmentByIdQuery.cs` (single with presigned URL)
- **Returns**: DTOs with thumbnail URLs, presigned download URLs
- **Branch**: `feature/issue-5363-attachment-queries`

#### 6. `/implementa #5364 --base-branch main-dev`
**[SP-06] Delete session attachment command**

- **Scope**: Soft delete + blob cleanup
- **Key files**:
  - `Application/Commands/DeleteSessionAttachmentCommand.cs`
  - `Application/Commands/DeleteSessionAttachmentCommandHandler.cs`
- **Authorization**: Only uploader or session host can delete
- **Branch**: `feature/issue-5364-delete-attachment`

---

### Sprint 3: API + Background (Issues 7-9, parallelizable)

#### 7. `/implementa #5365 --base-branch main-dev`
**[SP-07] Session attachment API endpoints**

- **Scope**: Minimal API endpoints in LiveSession routing
- **Endpoints**:
  - `POST /api/v1/sessions/{id}/attachments` (multipart upload)
  - `GET /api/v1/sessions/{id}/attachments` (list)
  - `GET /api/v1/sessions/{id}/attachments/{attachmentId}` (detail + presigned URL)
  - `DELETE /api/v1/sessions/{id}/attachments/{attachmentId}`
- **Reference**: `LiveSessionEndpoints.cs` — add attachment sub-routes
- **Branch**: `feature/issue-5365-attachment-endpoints`

#### 8. `/implementa #5366 --base-branch main-dev`
**[SP-08] SessionAttachmentCleanupJob — 90-day retention**

- **Scope**: Background hosted service
- **Key files**:
  - `BoundedContexts/GameManagement/Infrastructure/BackgroundServices/SessionAttachmentCleanupJob.cs`
- **Logic**: Find attachments where `created_at < NOW() - 90 days AND is_deleted = false` on completed sessions → soft delete + blob delete
- **Schedule**: Daily at 3 AM UTC
- **Branch**: `feature/issue-5366-attachment-cleanup`

#### 9. `/implementa #5367 --base-branch main-dev`
**[SP-09] Extend SessionSnapshot metadata for attachment IDs**

- **Scope**: Add attachment references to snapshot delta metadata
- **Changes**:
  - SessionSnapshot: Add `AttachmentIds` (List<Guid>) to snapshot metadata
  - When snapshot is created, capture current attachment IDs
  - Resume flow: Load attachments from snapshot metadata
- **Branch**: `feature/issue-5367-snapshot-attachments`

---

### Sprint 4: Frontend Components (Issues 10-12, parallelizable)

#### 10. `/implementa #5368 --base-branch main-dev`
**[SP-10] PhotoUploadModal — camera/gallery picker with progress**

- **Scope**: React component with XHR upload progress
- **Key files**:
  - `components/session/PhotoUploadModal.tsx`
- **Features**: Camera capture (mobile), gallery pick, drag-drop, upload progress bar, caption input, attachment type selector
- **Pattern**: XHR with relative URL (avoid CORS, route through Next.js proxy)
- **Branch**: `feature/issue-5368-photo-upload-modal`

#### 11. `/implementa #5369 --base-branch main-dev`
**[SP-11] SessionPhotoGallery — grid thumbnails with lightbox**

- **Scope**: Photo gallery component
- **Key files**:
  - `components/session/SessionPhotoGallery.tsx`
- **Features**: Thumbnail grid (3-col mobile, 4-col desktop), lightbox viewer, caption display, delete button (uploader only), filter by attachment type
- **Branch**: `feature/issue-5369-photo-gallery`

#### 12. `/implementa #5370 --base-branch main-dev`
**[SP-12] ResumePhotoReview — board restoration confirmation**

- **Scope**: Session resume helper showing last photos
- **Key files**:
  - `components/session/ResumePhotoReview.tsx`
- **Features**: Show photos from last snapshot, "Board restored?" confirmation, side-by-side current vs saved state
- **Branch**: `feature/issue-5370-resume-photo-review`

---

### Sprint 5: Integration (Issues 13-14, parallelizable)

#### 13. `/implementa #5371 --base-branch main-dev`
**[SP-13] ToolRail integration — Camera tool button**

- **Scope**: Add camera tool to existing ToolRail
- **Changes**: Add Camera icon button to ToolRail, opens PhotoUploadModal
- **Reference**: Existing ToolRail component with tool switching via Zustand sessionStore
- **Branch**: `feature/issue-5371-toolrail-camera`

#### 14. `/implementa #5372 --base-branch main-dev`
**[SP-14] Session pause flow — photo upload prompt**

- **Scope**: Prompt user to take photos before pausing session
- **Changes**: Intercept pause action, show "Take a photo of the board?" dialog, optional photo upload, then proceed with pause
- **Branch**: `feature/issue-5372-pause-photo-prompt`

---

### Sprint 6: Test Suites (Issues 15-16, parallelizable)

#### 15. `/implementa #5373 --base-branch main-dev`
**[SP-15] Backend tests — entity, service, endpoints**

- **Scope**: Comprehensive backend test coverage
- **Test categories**:
  - Unit: SessionAttachment entity (creation, validation, soft delete, state)
  - Unit: SessionAttachmentService (upload, thumbnail, delete)
  - Unit: Command handlers + validators
  - Integration: Repository with Testcontainers
  - Integration: API endpoints (upload, list, delete)
  - Background: Cleanup job (retention logic)
- **Target**: 90%+ coverage on new code
- **Branch**: `feature/issue-5373-backend-tests`

#### 16. `/implementa #5374 --base-branch main-dev`
**[SP-16] Frontend tests — components + E2E upload flow**

- **Scope**: Frontend test coverage
- **Test categories**:
  - Vitest: PhotoUploadModal (validation, progress, error handling)
  - Vitest: SessionPhotoGallery (rendering, filtering, delete)
  - Vitest: ResumePhotoReview (photo display, confirmation)
  - Vitest: ToolRail camera integration
  - Playwright E2E: Full upload flow (open modal → select file → upload → gallery)
- **Target**: 85%+ coverage on new components
- **Branch**: `feature/issue-5374-frontend-tests`

---

## Execution Commands

```bash
# Sprint 1: Sequential (each depends on previous)
/implementa #5359 --base-branch main-dev
/implementa #5360 --base-branch main-dev
/implementa #5361 --base-branch main-dev

# Sprint 2: Can run after Sprint 1
/implementa #5362 --base-branch main-dev
/implementa #5363 --base-branch main-dev
/implementa #5364 --base-branch main-dev

# Sprint 3: Can run after Sprint 2
/implementa #5365 --base-branch main-dev
/implementa #5366 --base-branch main-dev
/implementa #5367 --base-branch main-dev

# Sprint 4: Can run after Sprint 3 (endpoints needed for API calls)
/implementa #5368 --base-branch main-dev
/implementa #5369 --base-branch main-dev
/implementa #5370 --base-branch main-dev

# Sprint 5: Can run after Sprint 4
/implementa #5371 --base-branch main-dev
/implementa #5372 --base-branch main-dev

# Sprint 6: Can run after Sprint 5
/implementa #5373 --base-branch main-dev
/implementa #5374 --base-branch main-dev
```

---

## Risk Mitigation

| Risk | Mitigation |
|------|-----------|
| Thumbnail generation library choice | Use SkiaSharp (cross-platform) — already in .NET ecosystem |
| S3 storage path conflict with PDF uploads | Use `session_photos/` prefix instead of `pdf_uploads/` |
| Large photo uploads timeout | XHR with chunked upload, 10MB limit, progress tracking |
| Mobile camera API differences | Use HTML5 `<input type="file" accept="image/*" capture="environment">` |
| Cleanup job deleting active session photos | Only clean completed sessions older than 90 days |
| Snapshot metadata size growth | Store only attachment IDs (UUID[]), not full metadata |

## Estimated Effort

| Sprint | Issues | Estimated LOC | Focus |
|--------|--------|---------------|-------|
| 1 | 3 | ~800 | Domain + persistence |
| 2 | 3 | ~600 | CQRS commands/queries |
| 3 | 3 | ~700 | API + background |
| 4 | 3 | ~1200 | React components |
| 5 | 2 | ~400 | Integration |
| 6 | 2 | ~1500 | Tests |
| **Total** | **16** | **~5200** | |
