/**
 * Wire bodies of `POST /api/v1/ingest/pdf` (#4893). Copied from the endpoint, not written from memory:
 *
 * - `apps/api/src/Api/Routing/Pdf/PdfUploadEndpoints.cs`, `HandleStandardUpload`:
 *     uploaded -> `Results.Json(new { documentId, fileName })`
 *     existing -> `Results.Ok(new { existingKbFound = true, existingKb = result.ExistingKb, message = result.Message })`
 *   (both answer 200; the existing body has NO `documentId`)
 * - `ExistingKbInfoDto` (`DocumentProcessing/Application/DTOs/ExistingKbInfoDto.cs`):
 *     `PdfDocumentId, Source ("user" | "shared"), FileName, ProcessingState, TotalChunks (int?),
 *      OriginalGameName (string?), SharedGameId (Guid?)`
 * - `UploadPdfCommandHandler`: message "Existing KB found in your uploads" (source "user", SharedGameId null)
 *   or "Existing KB found in shared catalog" (source "shared", SharedGameId set).
 * - `Program.cs` `ConfigureHttpJsonOptions`: camelCase, nulls ARE written (no DefaultIgnoreCondition).
 *
 * The body the issue captured by hand from a local stack carries the first five fields of
 * `existingKb`; the last two are the DTO's, written as `null`/the game id as the serializer does.
 */

export const EXISTING_PDF_DOCUMENT_ID = '5b0c1a52-7d0e-4e5b-9f3a-2f6a8f0d9c11';
export const SHARED_GAME_ID = '0b6e1c2a-3d4f-4a5b-8c9d-1e2f3a4b5c6d';
export const NEW_DOCUMENT_ID = 'c3d2b1a0-9f8e-4d7c-a6b5-4e3d2c1b0a99';

/** A PDF uploaded now: the only body the web knew. */
export const INGEST_UPLOADED_BODY = {
  documentId: NEW_DOCUMENT_ID,
  fileName: 'catan_en_rulebook.pdf',
} as const;

/** The same content already uploaded by this user for this game (state not terminal yet). */
export const INGEST_EXISTING_USER_BODY = {
  existingKbFound: true,
  existingKb: {
    pdfDocumentId: EXISTING_PDF_DOCUMENT_ID,
    source: 'user',
    fileName: 'catan_en_rulebook.pdf',
    processingState: 'Pending',
    totalChunks: null,
    originalGameName: 'Catan',
    sharedGameId: null,
  },
  message: 'Existing KB found in your uploads',
} as const;

/** The same content already on this catalog game, uploaded by somebody else, fully indexed. */
export const INGEST_EXISTING_SHARED_BODY = {
  existingKbFound: true,
  existingKb: {
    pdfDocumentId: EXISTING_PDF_DOCUMENT_ID,
    source: 'shared',
    fileName: 'catan_en_rulebook.pdf',
    processingState: 'Ready',
    totalChunks: 42,
    originalGameName: 'Catan',
    sharedGameId: SHARED_GAME_ID,
  },
  message: 'Existing KB found in shared catalog',
} as const;
