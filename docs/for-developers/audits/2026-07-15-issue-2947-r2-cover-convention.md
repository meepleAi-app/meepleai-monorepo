# Issue #2947 — Deterministic R2 cover keys

All cover write-sites now compose a deterministic physical R2 key that
`CoverUrlResolver` reconstructs from the DB-persisted key via
`IBlobStorageService.GetPresignedUrlForRawKeyAsync` (the earlier
`IBlobStorageService.StoreAsync` path minted a random `Guid.NewGuid()` fileId
that could not be reconstructed).

| Layer | Writer | DB key (persisted) | Physical R2 object | Resolver read |
|-------|--------|--------------------|--------------------|---------------|
| L2 Wikidata | `CoverR2UploadPipeline` | `covers/{gameId}/cover` | `covers/{gameId}/cover.webp` | `{dbKey}.webp` |
| L2.5 BGG | `BggCoverUploadPipeline` | `bgg-covers/{bggId}/cover{ext}` | same as DB key | `{dbKey}` (no suffix) |
| L4 PDF (pipeline + backfill) | `PdfCoverUploadPipeline` | `covers/pdf/{pdfId:D}/cover` | `covers/pdf/{pdfId:D}/cover-preview.webp` | `{dbKey}-preview.webp` |

BGG is the only layer that does NOT append a suffix at read time: it keeps the
source image extension (BGG serves jpg/png, not webp), so the DB key IS the
physical key.

## Write-sites migrated

- `BggCoverDownloader.DownloadAndUploadAsync` — now delegates to
  `IBggCoverUploadPipeline.UploadAsync`, a raw `IAmazonS3.PutObjectAsync` call
  against the deterministic key `bgg-covers/{bggId}/cover{ext}` (mirrors the
  existing `CoverR2UploadPipeline` / `PdfCoverUploadPipeline` pattern).
- `BackfillPdfCoversJob.ProcessOneAsync` (Generated branch) — now calls
  `IPdfCoverUploadPipeline.UploadAsync(dbKey, previewBytes, ct)` instead of two
  `IBlobStorageService.StoreAsync` calls. Only the preview size is written; the
  thumbnail size is dropped since the resolver never reads it.
- `PdfProcessingPipelineService.ExtractCoverImageAsync` (Generated branch) —
  same change as the backfill job, via an optional `IPdfCoverUploadPipeline?`
  constructor parameter (null-safe for pre-#2947 unit-test constructors).

## Resolver change

`CoverUrlResolver.ResolvePublicAsync`'s L2.5 BGG branch switched from the
legacy `GetPresignedDownloadUrlAsync(fileId, category, resourceKey)` — which
validated both arguments via `PathSecurity.ValidateIdentifier` (rejecting `/`
and `.`, so a slash-and-dot-containing key like `bgg-covers/13/cover.jpg`
always failed silently) — to `GetPresignedUrlForRawKeyAsync(BggCoverR2Key)`
with no suffix appended.

## Root cause

`S3BlobStorageService.StoreAsync` mints a random `Guid.NewGuid()` fileId
(`S3BlobStorageService.cs:69`), producing a physical key
`game-images/{resourceKey}/{guid}_{file}` that cannot be reconstructed from the
DB-persisted key alone. Every migrated write-site instead talks directly to
`IAmazonS3.PutObjectAsync` with a key computed purely from IDs already
persisted on the entity (`bggId`, `pdfId`), so the resolver can rebuild the
exact physical key at read time without any additional lookup or random
component.

## Test coverage

- Unit tests (`Mock<IAmazonS3>`) assert the exact deterministic
  `PutObjectRequest.Key` for `BggCoverUploadPipeline`.
- `CoverUrlResolverTests` covers the L2.5 raw-key resolution path plus a
  slash/dot regression guard.
- `BggCoverDownloaderTests` / `BackfillPdfCoversJobTests` /
  `PdfProcessingPipelineServiceCoverTests` assert the pipeline is invoked with
  the deterministic key and that the legacy `StoreAsync` call is never made.
- `CoverR2ConventionIntegrationTests` (gated, `[Trait("Category", "Integration")]`)
  proves the full write → resolve → HTTP GET round-trip against a real
  S3-compatible store (MinIO Testcontainer / `TEST_S3_ENDPOINT`). Skips locally
  per the documented `DisablePayloadSigning`-over-HTTP MinIO limitation; runs
  in the gated CI/staging lane.
