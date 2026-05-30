# T0 Spike — PDF download endpoint vs doc states + pdfjs workerSrc multi-setup

**Date:** 2026-05-30
**Status:** decision
**Scope:** outcome decisorio per T5 (locked branch) e T1 (workerSrc setup pattern)

---

## (a) Endpoint /api/v1/pdfs/{id}/download vs processingStatus

**Handler location:**
- Route definition: `apps/api/src/Api/Routing/Pdf/PdfRetrievalEndpoints.cs:61-65`
- Handler method: `HandleDownloadPdf` at line 165
- Query handler: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Queries/DownloadPdfQueryHandler.cs`

**Analysis of DownloadPdfQueryHandler.Handle (lines 43-101):**

The handler executes three steps:

1. **Step 1 (lines 43-56):** Fetches PDF metadata from DB — selects `Id`, `SharedGameId`, `PrivateGameId`, `FileName`, `FilePath`, `ContentType`, `UploadedByUserId`. **`processingStatus` / `ProcessingStatus` is not selected and not inspected.**

2. **Step 2 (lines 64-76):** Authorization check — SharedGame PDFs (SharedGameId set, PrivateGameId null) are accessible to all authenticated users; private PDFs require owner or admin. **No status-based gate.**

3. **Step 3 (lines 78-100):** Retrieves the blob from storage via `IBlobStorageService.RetrieveAsync`. Returns `null` (→ 404 at the routing layer) only if the blob is absent from storage. Returns 403 only via `UnauthorizedAccessException`.

**Behavior on processingStatus:**

| State | Behavior |
|---|---|
| `ready` | 200 with file blob (blob exists) |
| `processing` / `queued` | 200 with file blob if blob is already in storage (upload succeeded before indexing); 404 if blob not yet uploaded |
| `failed` | 200 with file blob if blob exists; 404 if blob upload also failed |

The upload pipeline uploads the file to blob storage at the beginning of the processing workflow (before text extraction and indexing). Therefore in practice any doc with a non-null `FilePath` / non-missing blob will return 200 regardless of indexing status.

**Decision: OK**

The endpoint returns 200 + file blob for all docs where the file exists in storage, with no gate on `processingStatus`. T5 implements the locked branch as designed (preview available for `processing`/`queued`/`failed` states; FR-2 remains in scope).

---

## (b) pdfjs.GlobalWorkerOptions.workerSrc multi-setup

**Current usage — 4 call sites in codebase:**

| File | Line | Setup style |
|---|---|---|
| `apps/web/src/components/pdf/PdfPreview.tsx` | 16 | module-level (immediate) |
| `apps/web/src/components/pdf/PdfViewerModal.tsx` | 17 | module-level (immediate) |
| `apps/web/src/components/features/game-chat/CitationPdfTab.tsx` | 36 | module-level (immediate) |
| `apps/web/src/components/chat-unified/PdfPageModal.tsx` | 20 | inside `dynamic()` import callback |

**All 4 assignments set the identical value:**

```ts
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url,
).toString();
```

**Behavior — idempotent:**

`pdfjs.GlobalWorkerOptions.workerSrc` is a plain mutable string property on a singleton object shared across all modules in the same bundle. Each assignment resolves `new URL('pdfjs-dist/build/pdf.worker.min.mjs', import.meta.url).toString()` to the same webpack/Next.js bundled asset URL (the asset path is resolved at build time, not relative to the file's URL in production). Writing the same string value twice is a no-op. The existing pattern — 3 module-level assignments — already demonstrates this is safe: `PdfPreview`, `PdfViewerModal`, and `CitationPdfTab` all coexist without issue.

There is no race condition or ordering constraint because:
1. JavaScript module evaluation is synchronous.
2. The worker is not spawned until `Document` mounts, which happens after all module-level code has run.

**Decision: module-level setup in PdfInlineViewer**

`PdfInlineViewer.tsx` can follow the existing pattern: module-level assignment at the top of the file (after `import { pdfjs } from 'react-pdf'`). CitationPdfTab keeps its current module-level setup unchanged (T2 removes it only if T2 consolidates the import, which is a separate decision). No singleton module `pdfjs-worker.ts` is needed.
