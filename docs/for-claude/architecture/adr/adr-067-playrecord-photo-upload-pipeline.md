# ADR-067 — PlayRecord Photo Upload Pipeline

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 1 — sub-issue [#2359](https://github.com/meepleAi-app/meepleai-monorepo/issues/2359)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · [spec `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md`](../../../for-developers/specs/2026-06-14-mockup-us-coverage-map.md) §4a US-INT-2

## Context

The `IBlobStorageService` (`apps/api/src/Api/Services/Pdf/IBlobStorageService.cs`) is the unified file storage abstraction, backed by either the local filesystem (`BlobStorageService`) or S3-compatible storage (`S3BlobStorageService`), selected at runtime via the `STORAGE_PROVIDER` env var (factory: `BlobStorageServiceFactory.cs`). The interface uses a `(BlobCategory category, string resourceKey)` pair to construct the storage path; `BlobCategory.SessionPhoto` already exists as a category with the target prefix `session-photos/{sessionId}/`.

A complete prior art for photo upload already exists in `SessionAttachmentService` (`apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/SessionAttachmentService.cs`). It demonstrates:
- Content-type validation (JPEG/PNG allowlist)
- Magic-byte validation (reads the stream header)
- `IBlobStorageService.StoreAsync(stream, fileName, BlobCategory.SessionPhoto, storageFolder, ct)` call
- Thumbnail generation via `SixLabors.ImageSharp` (300px max dimension, JPEG 80 quality)
- Pre-signed URL retrieval for S3 (`GetPresignedDownloadUrlAsync`, 3600s expiry)
- Storage key pattern: `session-photos-{sessionId:N}/{playerId:N}_{Guid.NewGuid():N}{ext}`

The `BlobCategory` enum already has `SessionPhoto` for session-linked photos. PlayRecord photos are a distinct concern (historical records, not live-session state), so a dedicated category is appropriate.

The `unstructured-service` (`apps/unstructured-service/`) is a FastAPI PDF extraction microservice. Its domain model (`domain/models.py`), main entry point, and API schemas are exclusively PDF-centric — there is no image endpoint, no vision inference path, and no OCR for photos. Re-using it for scoresheet OCR would require adding a new `/extract-image` endpoint that the service was not designed for.

The `smoldocling-service` (`apps/smoldocling-service/`) similarly targets structured document parsing (DocLayNet-trained).

The spec (US-INT-2, Cockburn step 5) describes photo upload with "OCR opzionale per scoresheet card; preview inline" and maps this to a "photo max 5MB; OCR opzionale" acceptance criterion. The spec explicitly calls out OCR as opt-in, not default.

No perceptual hashing exists anywhere in the codebase. SHA256 content hashing exists in the PDF seeder (`PdfSeeder.cs:79-91`) using a `ContentHash` column to detect unchanged uploads and skip re-processing — a proven dedup pattern.

## Problem

The specific architectural question: **what is the canonical storage path, OCR strategy, and dedup mechanism for PlayRecord photos?**

Without a decision, US-INT-2b (create form + autosave, includes photo upload) cannot be implemented. Three distinct sub-questions must be resolved:

1. **S3 key layout**: what `BlobCategory` and `resourceKey` to use, so that `IBlobStorageService.StoreAsync` produces a deterministic, idempotent path.
2. **OCR strategy**: which service handles scoresheet text extraction, at what cost trigger, and what the fallback is on failure.
3. **Dedup strategy**: how to detect a photo already uploaded (same physical file re-uploaded, or same scoresheet photographed twice).

## Options Considered

### Photo Storage Path

#### Option P-A — New `PlayRecordPhoto` BlobCategory

Add `PlayRecordPhoto` to the `BlobCategory` enum. Storage key: `play-record-photos/{playRecordId}/{photoId}-{sha256[:8]}.{ext}`.

**Pros**: clean separation from session photos; clear S3 lifecycle policy can be applied per-prefix; aligns with `BlobCategoryExtensions.ToS3Folder()` design intent (each category → distinct prefix).

**Cons**: requires a one-line enum addition and a new case in `BlobCategoryExtensions.ToS3Folder()`.

**Risks**: low. The enum extension is trivial.

#### Option P-B — Reuse `SessionPhoto` BlobCategory with a PlayRecord resource key

Reuse `BlobCategory.SessionPhoto` with `resourceKey = $"play-record-{playRecordId:N}"`.

**Pros**: zero code change to the enum.

**Cons**: conflates two distinct concerns under one category — session-live photos and historical record photos share an S3 prefix (`session-photos/`). S3 lifecycle rules and access logs cannot distinguish them. `SessionAttachmentService` parses its folder key from `session-photos-{sessionId:N}` — name collision risk if a PlayRecord ID happens to match a SessionId (both are Guids, negligible but non-zero).

Rejected: the `BlobCategory` enum was designed for this segmentation (see ADR-context in `IBlobStorageService.cs:11-41`). Adding a new value is the intended extension path.

---

### OCR Strategy

#### Option O-A — Reuse Unstructured service (new `/extract-image` endpoint)

Add an image endpoint to `apps/unstructured-service/` that accepts JPEG/PNG and returns extracted text via Unstructured's vision pipeline.

**Pros**: no new service; unified OCR infrastructure.

**Cons**: Unstructured is a PDF/document extraction library — it processes page-level PDF element types (`TextChunk`, `QualityScore`). Adding image OCR is a distinct inference path requiring a different model or Tesseract backend. The service was not designed for this and has no image endpoint. Extending it risks cross-contaminating the stable PDF pipeline.

**Risks**: high. Service boundary violation.

#### Option O-B — New Python OCR microservice (Tesseract or cloud API)

A new `ocr-service` container running Tesseract (open source) or calling a cloud OCR API (e.g. Google Vision, AWS Textract). Called by the .NET API via HTTP after photo upload, only when `extractScoreFromPhoto: true`.

**Pros**: clean service boundary; Tesseract is already in the Docker ecosystem; avoids polluting the PDF pipeline; can be scaled independently.

**Cons**: new infra to deploy and maintain. Tesseract quality on scoresheet photos (partial occlusion, low contrast, angles) is variable; cloud OCR is more accurate but adds per-call cost.

**Risks**: medium (infra cost, Tesseract quality variance).

#### Option O-C — Opt-in OCR via Unstructured, deferred to Phase 2

Accept the `extractScoreFromPhoto: bool` flag on the upload command, store the photo, but defer the actual OCR call to a future work item. Phase 1 always returns `ocrText: null` regardless of the flag. The `BlobCategory.PlayRecordPhoto` column on the entity reserves space for `OcrText`.

**Pros**: unblocks US-INT-2b immediately (the flag is wired, the pipeline is ready, the inference is deferred). No new service needed for Phase 1. The spec says OCR is opt-in — defaulting to deferred satisfies the acceptance criterion for Phase 1.

**Cons**: the OCR flag has no effect in Phase 1. Users who tick "extract score from photo" get no result. Must be clearly communicated in the UX.

**Risks**: low for Phase 1. The deferred implementation decision (Tesseract vs cloud) does not block the storage pipeline.

---

### Dedup Strategy

#### Option D-A — SHA256 content hash (cryptographic, exact match)

Compute `SHA256(fileBytes)` on upload. Store the hash on the `PlayRecordPhoto` entity. Before persisting to S3, query for an existing photo with the same `(PlayRecordId, Sha256Hash)`. If found, return the existing `BlobUrl` without re-uploading.

**Pros**: exact match; consistent with the `PdfSeeder.ContentHash` pattern already in the codebase (`PdfSeeder.cs:79-91`); deterministic; no false positives; simple to implement (`SHA256.HashData(bytes)` is already used in `RedisTranslationCache.cs:65`).

**Cons**: same photo with even 1-bit difference (recompressed JPEG) is treated as a new upload. Does not catch "same scoresheet photographed twice with slight crop difference".

**Risks**: low. Known limitation is acceptable for MVP.

#### Option D-B — Perceptual hash (pHash / dHash, image similarity)

Compute a 64-bit perceptual hash of the image. Store on the entity. Before upload, query for photos with Hamming distance ≤ threshold (typically ≤ 10 bits for near-duplicate).

**Pros**: catches re-compressed uploads and slight crop variations; better UX for "same scoreboard photo twice" case.

**Cons**: requires a perceptual hashing library (no existing use in the codebase; `SixLabors.ImageSharp` does not include pHash natively). Threshold tuning is non-trivial — too tight misses near-dupes, too loose causes false merges. PostgreSQL Hamming distance query requires a hamming-distance function or bit extension. Significant implementation complexity for a secondary quality-of-life feature.

**Risks**: medium engineering; high maintenance.

Rejected for Phase 1. Can be added later as `PhotoPerceptualHash` column alongside `PhotoSha256Hash` without schema breakage.

---

## Decision

**Storage: Option P-A** — new `BlobCategory.PlayRecordPhoto` with key `play-record-photos/{playRecordId}/{photoId}-{sha256[:8]}.{ext}`.

**OCR: Option O-C** — opt-in flag accepted, OCR execution deferred to Phase 2.

**Dedup: Option D-A** — SHA256 cryptographic hash, consistent with existing `PdfSeeder.ContentHash` pattern.

Rationale: The `SessionAttachmentService` provides a complete prior art for the upload + thumbnail + pre-signed-URL pipeline; the PlayRecord photo pipeline reuses it nearly verbatim, substituting `BlobCategory.PlayRecordPhoto` and `play-record-{playRecordId:N}` as resource key. SHA256 dedup matches the existing pattern and is sufficient for MVP — the "same photo uploaded twice" scenario is the dominant real-world case. OCR deferred: Unstructured is PDF-only, a new Tesseract service is out of scope for US-INT-2, and the spec explicitly marks OCR as opt-in/optional. Deferring OCR execution to Phase 2 unblocks US-INT-2b without introducing new infra.

## Consequences

### Positive

- Photo upload pipeline reuses `IBlobStorageService` (no new infrastructure) and `SixLabors.ImageSharp` thumbnail generation (already a dependency).
- SHA256 dedup prevents S3 re-uploads for the same file — reduces storage cost and idempotency issues on retry.
- `extractScoreFromPhoto` flag is wired in the command from day one — no API shape change needed when Phase 2 adds OCR.
- `BlobCategory.PlayRecordPhoto` prefix enables independent S3 lifecycle policies (e.g. expiry for orphaned records) without touching `session-photos/`.

### Negative

- OCR opt-in flag in Phase 1 is a no-op — requires clear UX feedback ("Score extraction coming soon").
- SHA256 dedup misses near-duplicate photos (same scoresheet, slightly different crop). Acceptable for MVP.
- A new `BlobCategory` enum value must be added to the `BlobCategoryExtensions.ToS3Folder()` switch — trivial but must not be forgotten (adding a `BlobCategory` without updating `ToS3Folder()` throws `ArgumentOutOfRangeException` at runtime).

### Trade-offs Accepted

- Photo size limit: 5 MB (consistent with spec acceptance criterion and existing `SessionAttachmentService` JPEG quality). Client-side resize is a UX enhancement for a future PR.
- Thumbnail: 300px max dimension, JPEG 80 quality — same as `SessionAttachmentService`. Reuse constants.
- Pre-signed URL expiry: 3600s (1 hour) — same as `SessionAttachmentService`. Makes download links time-limited even for completed records.

## Implementation Guidance

**Step 1 — Extend `BlobCategory` enum** (`apps/api/src/Api/Services/Pdf/IBlobStorageService.cs`)

```csharp
/// <summary>Photos attached to a PlayRecord (scoreboard captures, party shots).
/// Target prefix <c>play-record-photos/{playRecordId}/</c>.</summary>
PlayRecordPhoto,
```

Add corresponding case to `BlobCategoryExtensions.ToS3Folder()`:
```csharp
BlobCategory.PlayRecordPhoto => "play-record-photos",
```

**Step 2 — `PlayRecordPhoto` entity** (new, in `GameManagement/Domain/Entities/`)

Fields: `Id (Guid)`, `PlayRecordId (Guid)`, `BlobUrl (string)`, `ThumbnailUrl (string?)`, `FileSizeBytes (long)`, `Sha256Hash (string)`, `OcrText (string?)` (null until Phase 2), `Caption (string?)`, `UploadedByUserId (Guid)`, `UploadedAt (DateTime)`.

`Sha256Hash` carries a UNIQUE partial index `UX_play_record_photos_playrecord_sha256` on `(PlayRecordId, Sha256Hash)` — blocks exact-duplicate uploads at the DB level.

**Step 3 — `UploadPlayRecordPhotoCommand` + handler**

Command: `(Guid PlayRecordId, Guid RequesterUserId, Stream FileStream, string FileName, string ContentType, long FileSize, bool ExtractScoreFromPhoto, string? Caption)`.

Handler logic:
1. Load `PlayRecord` by `PlayRecordId`, assert `RequesterUserId == CreatedByUserId` (creator-only; per ADR-066 creator-level guard).
2. Validate content type and magic bytes (reuse `ImageFileValidator.ValidateMagicBytesAsync`).
3. Compute `SHA256.HashData(fileBytes)` — seek stream to 0 first; convert to hex string.
4. Check `UX_play_record_photos_playrecord_sha256` for existing row → return existing `BlobUrl` if found (idempotent re-upload).
5. `IBlobStorageService.StoreAsync(stream, fileName, BlobCategory.PlayRecordPhoto, $"play-record-{record.Id:N}", ct)`.
6. Generate thumbnail via `SessionAttachmentService.GenerateThumbnailAsync` (extract as a shared internal helper or duplicate locally).
7. Persist `PlayRecordPhoto` entity; `SaveChangesAsync`.
8. If `ExtractScoreFromPhoto && Phase2Enabled` → dispatch OCR job (noop in Phase 1).

**Step 4 — Migration**

EF Core migration: new `play_record_photos` table + `UX_play_record_photos_playrecord_sha256` partial unique index.

**Step 5 — Phase 2 (deferred)**

When OCR is implemented: add a background job or outbox event `PlayRecordPhotoUploadedEvent → OcrExtractionJob`. The `OcrText` column is already nullable and ready to receive the result. A `PATCH /play-records/{id}/photos/{photoId}/ocr` endpoint can trigger on-demand extraction.

## Rollback / Reversibility

- Removing `BlobCategory.PlayRecordPhoto`: update `ToS3Folder()` switch and run a migration to drop the `play_record_photos` table. S3 objects under `play-record-photos/` must be manually purged or a lifecycle rule applied.
- The `OcrText` column (null in Phase 1) can be dropped in a future migration without data loss.
- SHA256 dedup index can be dropped independently of the table if the dedup behaviour needs to change (e.g. to allow re-uploads with the same content after record correction).

## References

- Spec: `docs/for-developers/specs/2026-06-14-mockup-us-coverage-map.md` §4a US-INT-2 step 5, Required ADRs item 2
- Sub-issue: [#2359](https://github.com/meepleAi-app/meepleai-monorepo/issues/2359)
- Tracker: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363)
- Prior art (photo upload): `apps/api/src/Api/BoundedContexts/GameManagement/Infrastructure/Services/SessionAttachmentService.cs`
- `IBlobStorageService` interface + `BlobCategory` enum: `apps/api/src/Api/Services/Pdf/IBlobStorageService.cs`
- `BlobStorageServiceFactory`: `apps/api/src/Api/Services/Pdf/BlobStorageServiceFactory.cs`
- SHA256 dedup pattern: `apps/api/src/Api/Infrastructure/Seeders/Catalog/PdfSeeder.cs:79-91`
- ADR-066 (ownership model — creator-level guard on upload command): `adr-066-playrecord-ownership-model.md`
- ADR-060 (xmin concurrency — applicable to future concurrent photo edits): `adr-060-live-session-persistence.md`
- Unstructured service (PDF-only, not reused): `apps/unstructured-service/src/main.py`
